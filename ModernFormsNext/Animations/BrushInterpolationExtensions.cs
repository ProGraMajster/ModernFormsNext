using ModernFormsNext.Drawing;

namespace ModernFormsNext.Animations;

/// <summary>Provides opt-in temporal color and extended brush animation helpers.</summary>
/// <remarks>
/// These overloads use the canonical brush planner and shared scheduler. Configure and mutate on
/// the UI thread. The policy precedes duration so existing duration/key calls stay unambiguous.
/// </remarks>
/// <example>
/// <code>
/// brush.AnimateTo(target,
///     new BrushInterpolationOptions { ColorSpace = ColorInterpolationSpace.LinearSrgb },
///     TimeSpan.FromMilliseconds(200));
/// </code>
/// </example>
// Adding overloads to the legacy extension types causes Roslyn to hoist NullableContextAttribute
// from their old methods to the type. Keep those declarations stable for strict API compatibility.
public static class BrushInterpolationExtensions
{
    /// <summary>Animates a supported brush in place with a captured interpolation policy.</summary>
    /// <param name="brush">The mutable destination and animation owner.</param>
    /// <param name="target">The borrowed target, whose authored values are not changed.</param>
    /// <param name="interpolation">The required policy captured before scheduling.</param>
    /// <param name="duration">The non-negative unscaled duration.</param>
    /// <param name="key">The owner-local replacement key.</param>
    /// <param name="easing">Optional easing; null uses linear interpolation.</param>
    /// <param name="scheduler">Optional scheduler; null uses the shared default.</param>
    /// <returns>The animation handle.</returns>
    /// <remarks>
    /// Identity, exact type and gradient stop count stay fixed. Extended morphing additionally
    /// permits glass with matching layer flags. No-paint reference replacement belongs to
    /// <see cref="AnimationInterpolators.CreateBrushInterpolator(BrushInterpolationOptions)"/>.
    /// Changed frames raise existing brush notifications and repaint consumers without layout.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A required brush or policy is null.</exception>
    /// <exception cref="ArgumentException">The fixed destination cannot represent the target structure.</exception>
    /// <exception cref="NotSupportedException">The concrete type or planning size is unsupported.</exception>
    public static AnimationHandle AnimateTo(this Brush brush, Brush target,
        BrushInterpolationOptions interpolation, TimeSpan duration, string key = "Brush",
        Func<float, float>? easing = null, AnimationScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        return AnimateBrushCore(brush, target, duration, interpolation.Clone(), key, easing, scheduler);
    }

    /// <summary>Animates one stop in place using an explicit temporal RGB space.</summary>
    /// <param name="stop">The mutable stop and animation owner.</param>
    /// <param name="target">The borrowed target snapshot.</param>
    /// <param name="colorSpace">The RGB space; alpha stays separately linear.</param>
    /// <param name="duration">The non-negative unscaled duration.</param>
    /// <param name="key">The owner-local replacement key.</param>
    /// <param name="easing">Optional easing; null uses linear interpolation.</param>
    /// <param name="scheduler">Optional scheduler; null uses the shared default.</param>
    /// <returns>The scheduled handle.</returns>
    /// <remarks>Values are captured before scheduling. UI-thread updates use the containing brush's
    /// existing render invalidation. Offset remains clamped to 0..1 even with overshooting easing.</remarks>
    /// <exception cref="ArgumentNullException">A required stop is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The color space is undefined.</exception>
    public static AnimationHandle AnimateTo(this GradientStop stop, GradientStop target,
        ColorInterpolationSpace colorSpace, TimeSpan duration, string key = "GradientStop",
        Func<float, float>? easing = null, AnimationScheduler? scheduler = null)
        => AnimateGradientStopCore(stop, target, duration, colorSpace, key, easing, scheduler);

    internal static AnimationHandle AnimateBrushCore(Brush brush, Brush target, TimeSpan duration,
        BrushInterpolationOptions? interpolation, string key, Func<float, float>? easing, AnimationScheduler? scheduler)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(target);
        BrushAnimationPlan plan = BrushAnimationPlan.Create(brush, target, brush, interpolation);
        return (scheduler ?? AnimationScheduler.Default).Start(brush, key, plan.Apply,
            new AnimationOptions { Duration = duration, Easing = easing ?? Easings.Linear });
    }

    internal static AnimationHandle AnimateGradientStopCore(GradientStop stop, GradientStop target, TimeSpan duration,
        ColorInterpolationSpace colorSpace, string key, Func<float, float>? easing, AnimationScheduler? scheduler)
    {
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(target);
        var colorInterpolator = AnimationInterpolators.CreateColorInterpolator(colorSpace);
        var fromColor = stop.PaintColor;
        float fromOffset = stop.Offset;
        var targetColor = target.PaintColor;
        float targetOffset = target.Offset;
        return (scheduler ?? AnimationScheduler.Default).Start(stop, key, progress =>
        {
            stop.PaintColor = colorInterpolator.Interpolate(fromColor, targetColor, progress);
            stop.Offset = Math.Clamp(AnimationInterpolators.Float.Interpolate(fromOffset, targetOffset, progress), 0f, 1f);
        }, new AnimationOptions { Duration = duration, Easing = easing ?? Easings.Linear });
    }
}
