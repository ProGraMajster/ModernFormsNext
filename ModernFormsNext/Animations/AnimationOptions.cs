namespace ModernFormsNext.Animations;

/// <summary>
/// Configures duration, delay, easing, and replacement for a scheduled animation.
/// </summary>
/// <remarks>
/// The scheduler copies these values when an animation starts, so changing an options instance
/// does not alter an animation already in progress. Duration and delay are scaled by the current
/// <see cref="AnimationPolicy.DurationScale"/>. All values use monotonic elapsed time on Windows
/// and Android.
/// </remarks>
/// <example>
/// <code>
/// var options = new AnimationOptions
/// {
///     Duration = TimeSpan.FromMilliseconds(200),
///     Easing = Easings.EaseOut
/// };
/// </code>
/// </example>
public sealed class AnimationOptions
{
    private TimeSpan duration = TimeSpan.FromMilliseconds(250);
    private TimeSpan delay;
    private Func<float, float> easing = Easings.Linear;
    private AnimationReplacementMode replacementMode = AnimationReplacementMode.Replace;

    /// <summary>
    /// Gets or sets the unscaled animation duration.
    /// </summary>
    /// <value>A non-negative duration. The default is 250 milliseconds.</value>
    /// <remarks>
    /// A zero duration applies the final value once on the UI thread without starting the tick
    /// source. A delayed dispatcher frame never extends this duration because progress is based on
    /// elapsed monotonic time.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Animation duration cannot be negative.");
            duration = value;
        }
    }

    /// <summary>
    /// Gets or sets the unscaled delay before progress begins.
    /// </summary>
    /// <value>A non-negative duration. The default is zero.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Delay
    {
        get => delay;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Animation delay cannot be negative.");
            delay = value;
        }
    }

    /// <summary>
    /// Gets or sets the easing function applied to intermediate progress.
    /// </summary>
    /// <value>A function receiving progress in 0..1. The default is <see cref="Easings.Linear"/>.</value>
    /// <remarks>
    /// Finite results outside 0..1 are supported for overshoot curves. NaN, infinity, or an
    /// exception faults only the affected animation. Endpoints are applied exactly as 0 and 1.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The assigned function is null.</exception>
    public Func<float, float> Easing
    {
        get => easing;
        set => easing = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets how an existing animation with the same owner and key is handled.
    /// </summary>
    /// <value>The default is <see cref="AnimationReplacementMode.Replace"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public AnimationReplacementMode ReplacementMode
    {
        get => replacementMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The replacement mode is not defined.");
            replacementMode = value;
        }
    }

    internal AnimationOptionsSnapshot CreateSnapshot(AnimationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        double scale = policy.DurationScale;
        return new AnimationOptionsSnapshot(
            ScaleTime(Duration, scale),
            ScaleTime(Delay, scale),
            Easing,
            ReplacementMode,
            policy.ShouldCompleteImmediately);
    }

    private static TimeSpan ScaleTime(TimeSpan value, double scale)
    {
        if (value == TimeSpan.Zero || scale == 0d)
            return TimeSpan.Zero;

        double ticks = value.Ticks * scale;
        if (!double.IsFinite(ticks) || ticks > TimeSpan.MaxValue.Ticks)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "The duration scale produces an unsupported interval.");

        return TimeSpan.FromTicks((long)Math.Round(ticks, MidpointRounding.AwayFromZero));
    }
}

internal readonly record struct AnimationOptionsSnapshot(
    TimeSpan Duration,
    TimeSpan Delay,
    Func<float, float> Easing,
    AnimationReplacementMode ReplacementMode,
    bool CompleteImmediately);
