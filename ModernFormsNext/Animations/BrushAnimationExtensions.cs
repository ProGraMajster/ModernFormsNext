using ModernFormsNext.Drawing;

namespace ModernFormsNext.Animations;

/// <summary>
/// Provides explicit in-place animation helpers for observable brushes.
/// </summary>
public static class BrushAnimationExtensions
{
    /// <summary>
    /// Animates a supported brush in place toward a compatible target brush.
    /// </summary>
    /// <param name="brush">The brush instance to mutate and use as animation owner.</param>
    /// <param name="target">A compatible target snapshot that is not mutated.</param>
    /// <param name="duration">The non-negative unscaled duration.</param>
    /// <param name="key">The owner-local replacement key.</param>
    /// <param name="easing">Optional easing function.</param>
    /// <param name="scheduler">Optional scheduler; the default UI scheduler is used when null.</param>
    /// <returns>A handle for cancellation, pause/resume, state, and completion.</returns>
    /// <remarks>
    /// <para>
    /// Supported concrete types are <see cref="SolidColorBrush"/>,
    /// <see cref="LinearGradientBrush"/>, <see cref="RadialGradientBrush"/>, and
    /// <see cref="SweepGradientBrush"/>. Because this helper preserves and mutates the identity of
    /// <paramref name="brush"/>, source and target must have the same concrete type and gradient
    /// stop count. Value-style transitions that can replace a brush reference use
    /// <see cref="AnimationInterpolators.CreateBrushInterpolator"/> and additionally support
    /// normalized stop counts and solid-to-gradient transitions. Type and structure validation
    /// happens before the in-place animation is scheduled.
    /// </para>
    /// <para>
    /// This method intentionally mutates <paramref name="brush"/>. If it is stored in a dynamic
    /// resource, every subscribed control repaints as colors, offsets, geometry, opacity, or
    /// transform change. Use a control-local clone or
    /// <see cref="AnimationInterpolators.CreateBrushInterpolator"/> when only one consumer should
    /// transition. Mutation and <see cref="Brush.Changed"/> occur on the UI thread on Windows and
    /// Android and do not request layout.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="brush"/> or <paramref name="target"/> is null.</exception>
    /// <exception cref="ArgumentException">Types or gradient stop structures are incompatible.</exception>
    /// <exception cref="NotSupportedException">The concrete brush type is not supported.</exception>
    public static AnimationHandle AnimateTo(
        this Brush brush,
        Brush target,
        TimeSpan duration,
        string key = "Brush",
        Func<float, float>? easing = null,
        AnimationScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(target);
        BrushAnimationPlan plan = BrushAnimationPlan.Create(brush, target, brush);
        return (scheduler ?? AnimationScheduler.Default).Start(
            brush,
            key,
            plan.Apply,
            new AnimationOptions
            {
                Duration = duration,
                Easing = easing ?? Easings.Linear
            });
    }
}
