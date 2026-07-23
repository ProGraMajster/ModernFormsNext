using System.ComponentModel;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Animations;

/// <summary>
/// Draws bounded scheduler-driven waves for pointer, touch, and keyboard activation.
/// </summary>
public sealed class RippleEffect : InteractionEffect
{
    private readonly List<RippleInstance> ripples = [];
    private TimeSpan duration = TimeSpan.FromMilliseconds(450);
    private Func<float, float> easing = Easings.CubicOut;
    private int maxConcurrentRipples = 4;
    private float fixedRadius = 48f;
    private long nextId;
    private RippleLayer layer = RippleLayer.AboveBackgroundBelowContent;
    private RippleRadiusMode radiusMode = RippleRadiusMode.CoverControl;
    private RippleEvictionPolicy evictionPolicy = RippleEvictionPolicy.Oldest;

    /// <summary>Gets or sets the wave color including its initial alpha.</summary>
    public Color Color { get; set; } = Color.FromArgb(90, 255, 255, 255);

    /// <summary>Gets or sets one wave duration.</summary>
    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Ripple duration cannot be negative.");
            duration = value;
        }
    }

    /// <summary>Gets or sets the radius easing. Alpha always fades linearly.</summary>
    [Browsable(false)]
    public Func<float, float> Easing
    {
        get => easing;
        set => easing = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets whether pointer waves originate at the contact location.</summary>
    [DefaultValue(true)]
    public bool StartFromPointer { get; set; } = true;

    /// <summary>Gets or sets how final radius is resolved.</summary>
    [DefaultValue(RippleRadiusMode.CoverControl)]
    public RippleRadiusMode RadiusMode
    {
        get => radiusMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            radiusMode = value;
            InvalidateTarget();
        }
    }

    /// <summary>Gets or sets the fixed radius in logical pixels.</summary>
    public float FixedRadius
    {
        get => fixedRadius;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Ripple radius must be finite and non-negative.");
            fixedRadius = value;
        }
    }

    /// <summary>Gets or sets the explicit ripple render layer.</summary>
    [DefaultValue(RippleLayer.AboveBackgroundBelowContent)]
    public RippleLayer Layer
    {
        get => layer;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            layer = value;
            InvalidateTarget();
        }
    }

    /// <summary>Gets or sets the bounded number of concurrently rendered waves.</summary>
    public int MaxConcurrentRipples
    {
        get => maxConcurrentRipples;
        set
        {
            if (value is < 1 or > 32)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Concurrent ripple count must be from 1 through 32.");
            maxConcurrentRipples = value;
            EvictOverflow();
        }
    }

    /// <summary>Gets or sets the explicit bounded-wave eviction policy.</summary>
    [DefaultValue(RippleEvictionPolicy.Oldest)]
    public RippleEvictionPolicy EvictionPolicy
    {
        get => evictionPolicy;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            evictionPolicy = value;
        }
    }

    /// <summary>Gets the current number of active waves for diagnostics.</summary>
    [Browsable(false)]
    public int ActiveRippleCount => ripples.Count;

    /// <inheritdoc/>
    public override InteractionEffectLayer RenderLayer
        => Layer == RippleLayer.AboveContent
            ? InteractionEffectLayer.AboveContent
            : InteractionEffectLayer.AboveBackgroundBelowContent;

    /// <inheritdoc/>
    protected override void OnPointerDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || Target is null)
            return;
        PointF origin = StartFromPointer
            ? new PointF(e.X, e.Y)
            : GetCenter(Target);
        StartRipple(origin, e.PointerId);
    }

    /// <inheritdoc/>
    protected override void OnPointerCanceled() => CancelCore();

    /// <inheritdoc/>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode.In(Keys.Space, Keys.Enter) && Target is { } target)
            StartRipple(GetCenter(target), pointerId: -1);
    }

    /// <inheritdoc/>
    protected override void OnRender(InteractionEffectRenderContext context)
    {
        if (ripples.Count == 0)
            return;

        float scale = (float)context.Scaling;
        using var paint = new SKPaint { IsAntialias = true };
        foreach (RippleInstance ripple in ripples)
        {
            float originX = ripple.Origin.X * scale;
            float originY = ripple.Origin.Y * scale;
            float radius = ResolveRadius(ripple.Origin, context.Target) * scale * ripple.EasedProgress;
            int alpha = (int)MathF.Round(Color.A * (1f - ripple.Progress));
            paint.Color = new SKColor(Color.R, Color.G, Color.B, (byte)Math.Clamp(alpha, 0, 255));
            context.Canvas.DrawCircle(originX, originY, radius, paint);
        }
    }

    /// <inheritdoc/>
    protected override void CancelCore()
    {
        if (ripples.Count > 0)
        {
            foreach (RippleInstance ripple in ripples.ToArray())
                ripple.Handle?.Cancel();
            ripples.Clear();
            InvalidateTarget();
        }
        base.CancelCore();
    }

    private void StartRipple(PointF origin, int pointerId)
    {
        if (Target is not { Enabled: true } target ||
            target.Site?.DesignMode == true ||
            Scheduler.Policy.ShouldCompleteImmediately)
            return;

        while (ripples.Count >= MaxConcurrentRipples)
            EvictOne();

        var ripple = new RippleInstance(++nextId, pointerId, origin);
        ripples.Add(ripple);
        AnimationHandle handle = Scheduler.Start(
            this,
            $"Ripple:{ripple.Id}",
            progress =>
            {
                ripple.Progress = progress;
                ripple.EasedProgress = Easing(progress);
                if (!float.IsFinite(ripple.EasedProgress))
                    throw new InvalidOperationException("Ripple easing returned NaN or infinity.");
                if (progress >= 1f)
                    ripples.Remove(ripple);
                InvalidateTarget();
            },
            new AnimationOptions
            {
                Duration = Duration,
                Easing = Easings.Linear,
                ReplacementMode = AnimationReplacementMode.Replace
            });
        ripple.Handle = handle;
        InvalidateTarget();
    }

    private void EvictOverflow()
    {
        while (ripples.Count > MaxConcurrentRipples)
            EvictOne();
    }

    private void EvictOne()
    {
        if (ripples.Count == 0)
            return;
        RippleInstance ripple = EvictionPolicy switch
        {
            RippleEvictionPolicy.Oldest => ripples[0],
            _ => throw new ArgumentOutOfRangeException(nameof(EvictionPolicy))
        };
        ripples.Remove(ripple);
        ripple.Handle?.Cancel();
    }

    private float ResolveRadius(PointF origin, Control target)
    {
        if (RadiusMode == RippleRadiusMode.Fixed)
            return FixedRadius;
        float farthestX = MathF.Max(origin.X, target.Width - origin.X);
        float farthestY = MathF.Max(origin.Y, target.Height - origin.Y);
        return MathF.Sqrt((farthestX * farthestX) + (farthestY * farthestY));
    }

    private static PointF GetCenter(Control target)
        => new(target.Width / 2f, target.Height / 2f);

    private sealed class RippleInstance(long id, int pointerId, PointF origin)
    {
        public long Id { get; } = id;
        public int PointerId { get; } = pointerId;
        public PointF Origin { get; } = origin;
        public float Progress { get; set; }
        public float EasedProgress { get; set; }
        public AnimationHandle? Handle { get; set; }
    }
}
