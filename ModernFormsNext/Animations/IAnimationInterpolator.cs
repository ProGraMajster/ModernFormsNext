namespace ModernFormsNext.Animations;

/// <summary>
/// Produces an intermediate value between two values for eased animation progress.
/// </summary>
/// <typeparam name="T">The value type being animated.</typeparam>
/// <remarks>
/// Interpolation executes on the scheduler's UI thread. Implementations should avoid per-frame
/// allocation and must not perform blocking work. Progress is finite and is normally in 0..1, but
/// custom easing may deliberately provide values outside that range for overshoot effects.
/// </remarks>
public interface IAnimationInterpolator<T>
{
    /// <summary>
    /// Interpolates from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">The captured start value.</param>
    /// <param name="to">The target value.</param>
    /// <param name="progress">Finite eased progress, with exact endpoints 0 and 1.</param>
    /// <returns>The intermediate value.</returns>
    T Interpolate(T from, T to, float progress);
}
