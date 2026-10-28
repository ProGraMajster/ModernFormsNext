namespace ModernFormsNext.Animations;

/// <summary>Configures animation between two resolved control visual states.</summary>
public sealed class VisualStateTransition
{
    private TimeSpan duration = TimeSpan.FromMilliseconds(150);
    private Func<float, float> easing = Easings.CubicOut;
    private BrushInterpolationOptions brushInterpolation = new();

    /// <summary>Gets or sets the brush-only interpolation policy captured when this transition starts.</summary>
    /// <remarks>
    /// Defaults preserve encoded sRGB and the original compatibility matrix. Extended morphing
    /// and linear-light RGB are opt-in. This policy affects background, text and border brushes;
    /// fallback color channels retain their existing behavior. Configure on the UI thread before
    /// activation. Mutating these options does not restart or alter an active transition.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public BrushInterpolationOptions BrushInterpolation
    {
        get => brushInterpolation;
        set => brushInterpolation = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the unscaled duration shared by visual and supported layout-aware metrics.
    /// </summary>
    /// <remarks>
    /// A zero duration applies the target style immediately without starting the shared tick
    /// source. Negative values are rejected.
    /// </remarks>
    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Transition duration cannot be negative.");
            duration = value;
        }
    }

    /// <summary>Gets or sets the transition easing.</summary>
    public Func<float, float> Easing
    {
        get => easing;
        set => easing = value ?? throw new ArgumentNullException(nameof(value));
    }
}
