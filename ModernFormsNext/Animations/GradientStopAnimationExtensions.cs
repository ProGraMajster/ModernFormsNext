using ModernFormsNext.Drawing;

namespace ModernFormsNext.Animations;

/// <summary>
/// Provides explicit in-place animation for observable gradient stops.
/// </summary>
public static class GradientStopAnimationExtensions
{
    /// <summary>
    /// Animates a gradient stop's color and normalized offset in place.
    /// </summary>
    /// <param name="stop">The observable stop to mutate and use as animation owner.</param>
    /// <param name="target">The target stop snapshot.</param>
    /// <param name="duration">The non-negative unscaled duration.</param>
    /// <param name="key">The owner-local replacement key.</param>
    /// <param name="easing">Optional easing function.</param>
    /// <param name="scheduler">Optional scheduler; the default is used when null.</param>
    /// <returns>A handle controlling the animation.</returns>
    /// <remarks>
    /// The start color and offset are captured before scheduling. Updates run on the UI thread and
    /// flow through the containing gradient brush's existing change notification, producing
    /// render-only invalidation for its consumers. The offset remains clamped to 0..1 even when an
    /// easing curve overshoots.
    /// </remarks>
    public static AnimationHandle AnimateTo(
        this GradientStop stop,
        GradientStop target,
        TimeSpan duration,
        string key = "GradientStop",
        Func<float, float>? easing = null,
        AnimationScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(target);
        System.Drawing.Color fromColor = stop.PaintColor;
        float fromOffset = stop.Offset;
        System.Drawing.Color targetColor = target.PaintColor;
        float targetOffset = target.Offset;
        return (scheduler ?? AnimationScheduler.Default).Start(
            stop,
            key,
            progress =>
            {
                stop.PaintColor = AnimationInterpolators.Color.Interpolate(fromColor, targetColor, progress);
                stop.Offset = Math.Clamp(
                    AnimationInterpolators.Float.Interpolate(fromOffset, targetOffset, progress),
                    0f,
                    1f);
            },
            new AnimationOptions
            {
                Duration = duration,
                Easing = easing ?? Easings.Linear
            });
    }
}
