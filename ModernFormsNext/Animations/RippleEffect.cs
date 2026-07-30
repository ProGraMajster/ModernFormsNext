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
    private RippleOverflowPolicy overflowPolicy = RippleOverflowPolicy.RemoveOldest;

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
            if (EvictOverflow())
                InvalidateTarget();
        }
    }

    /// <summary>Gets or sets how a new wave is handled when the active-wave limit is reached.</summary>
    /// <remarks>
    /// The default is <see cref="RippleOverflowPolicy.RemoveOldest"/>, preserving the original
    /// bounded-ripple behavior. <see cref="RippleOverflowPolicy.IgnoreNew"/> does not allocate a
    /// ripple instance or scheduler handle.
    /// </remarks>
    [DefaultValue(RippleOverflowPolicy.RemoveOldest)]
    public RippleOverflowPolicy OverflowPolicy
    {
        get => overflowPolicy;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            overflowPolicy = value;
        }
    }

    /// <summary>Gets or sets the compatibility alias for <see cref="OverflowPolicy"/>.</summary>
    /// <remarks>
    /// This property preserves source compatibility with the original single-policy API. New code
    /// should use <see cref="OverflowPolicy"/>.
    /// </remarks>
    [DefaultValue(RippleEvictionPolicy.Oldest)]
    public RippleEvictionPolicy EvictionPolicy
    {
        get => OverflowPolicy switch
        {
            RippleOverflowPolicy.RemoveOldest => RippleEvictionPolicy.Oldest,
            RippleOverflowPolicy.RemoveNewest => RippleEvictionPolicy.Newest,
            RippleOverflowPolicy.IgnoreNew => RippleEvictionPolicy.IgnoreNew,
            RippleOverflowPolicy.ReplaceAll => RippleEvictionPolicy.ReplaceAll,
            _ => throw new InvalidOperationException("The ripple overflow policy is invalid.")
        };
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            OverflowPolicy = value switch
            {
                RippleEvictionPolicy.Oldest => RippleOverflowPolicy.RemoveOldest,
                RippleEvictionPolicy.Newest => RippleOverflowPolicy.RemoveNewest,
                RippleEvictionPolicy.IgnoreNew => RippleOverflowPolicy.IgnoreNew,
                RippleEvictionPolicy.ReplaceAll => RippleOverflowPolicy.ReplaceAll,
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
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
    protected override void OnPointerCanceled(int? pointerId)
    {
        if (pointerId is null)
        {
            CancelCore();
            return;
        }

        bool removed = false;
        foreach (RippleInstance ripple in ripples
            .Where(item => item.PointerId == pointerId.Value)
            .ToArray())
        {
            ripples.Remove(ripple);
            ripple.Handle?.Cancel();
            removed = true;
        }
        if (removed)
            InvalidateTarget();
    }

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

        if (!PrepareForNewRipple())
            return;

        var ripple = new RippleInstance(++nextId, pointerId, origin);
        ripples.Add(ripple);
        try
        {
            AnimationHandle handle = Scheduler.Start(
                this,
                $"Ripple:{ripple.Id}",
                progress =>
                {
                    ripple.Progress = progress;
                    try
                    {
                        ripple.EasedProgress = Easing(progress);
                        if (!float.IsFinite(ripple.EasedProgress))
                            throw new InvalidOperationException("Ripple easing returned NaN or infinity.");
                    }
                    catch
                    {
                        // A faulted scheduler entry will not produce another frame. Remove its
                        // visual state before rethrowing so it cannot remain painted forever.
                        ripples.Remove(ripple);
                        InvalidateTarget();
                        throw;
                    }
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
        }
        catch
        {
            ripples.Remove(ripple);
            InvalidateTarget();
            throw;
        }
        InvalidateTarget();
    }

    private bool EvictOverflow()
    {
        bool removed = false;
        if (OverflowPolicy == RippleOverflowPolicy.ReplaceAll && ripples.Count > MaxConcurrentRipples)
        {
            CancelAllRipples();
            return true;
        }

        while (ripples.Count > MaxConcurrentRipples)
        {
            bool removeNewest = OverflowPolicy is RippleOverflowPolicy.RemoveNewest
                or RippleOverflowPolicy.IgnoreNew;
            EvictOne(removeNewest);
            removed = true;
        }
        return removed;
    }

    private bool PrepareForNewRipple()
    {
        if (ripples.Count < MaxConcurrentRipples)
            return true;

        switch (OverflowPolicy)
        {
            case RippleOverflowPolicy.RemoveOldest:
                EvictOne(removeNewest: false);
                return true;
            case RippleOverflowPolicy.RemoveNewest:
                EvictOne(removeNewest: true);
                return true;
            case RippleOverflowPolicy.IgnoreNew:
                return false;
            case RippleOverflowPolicy.ReplaceAll:
                CancelAllRipples();
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(OverflowPolicy));
        }
    }

    private void EvictOne(bool removeNewest)
    {
        if (ripples.Count == 0)
            return;
        RippleInstance ripple = removeNewest ? ripples[^1] : ripples[0];
        ripples.Remove(ripple);
        ripple.Handle?.Cancel();
    }

    private void CancelAllRipples()
    {
        foreach (RippleInstance ripple in ripples.ToArray())
            ripple.Handle?.Cancel();
        ripples.Clear();
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
