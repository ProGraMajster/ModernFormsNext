namespace ModernFormsNext.Animations;

/// <summary>
/// Provides the central policy for enabling, scaling, or reducing UI motion.
/// </summary>
/// <remarks>
/// The policy is shared by one <see cref="AnimationScheduler"/>. Properties are thread-safe.
/// Disabling animations, enabling reduced motion, or setting <see cref="DurationScale"/> to zero
/// completes active animations at their final value on the UI thread and prevents timer ticks.
/// Native operating-system reduced-motion discovery is not yet automatic.
/// </remarks>
public sealed class AnimationPolicy
{
    private readonly object sync = new();
    private bool animationsEnabled = true;
    private bool reducedMotion;
    private double durationScale = 1d;

    /// <summary>
    /// Gets or sets whether animations are enabled.
    /// </summary>
    /// <remarks>
    /// When disabled, animations still apply their final value and complete successfully, but do
    /// not start the shared tick source. This can be changed from any thread.
    /// </remarks>
    public bool AnimationsEnabled
    {
        get
        {
            lock (sync)
                return animationsEnabled;
        }
        set => SetValue(ref animationsEnabled, value);
    }

    /// <summary>
    /// Gets or sets whether reduced motion is requested by the application.
    /// </summary>
    /// <remarks>
    /// Reduced motion currently has the same scheduling behavior as disabling animations: final
    /// values are applied immediately. A future backend integration may initialize this value from
    /// the platform accessibility preference.
    /// </remarks>
    public bool ReducedMotion
    {
        get
        {
            lock (sync)
                return reducedMotion;
        }
        set => SetValue(ref reducedMotion, value);
    }

    /// <summary>
    /// Gets or sets the multiplier applied to durations and delays of newly started animations.
    /// </summary>
    /// <value>A finite non-negative value. The default is 1.</value>
    /// <remarks>
    /// A value of 0 completes without ticking, 0.5 runs in half the configured time, and 2 runs in
    /// twice the configured time. Existing animations retain the scale captured at start.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN, or infinity.</exception>
    public double DurationScale
    {
        get
        {
            lock (sync)
                return durationScale;
        }
        set
        {
            if (!double.IsFinite(value) || value < 0d)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Duration scale must be finite and non-negative.");

            bool changed;
            lock (sync)
            {
                changed = durationScale != value;
                durationScale = value;
            }

            if (changed)
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal bool ShouldCompleteImmediately
    {
        get
        {
            lock (sync)
                return !animationsEnabled || reducedMotion || durationScale == 0d;
        }
    }

    internal event EventHandler? Changed;

    private void SetValue(ref bool field, bool value)
    {
        bool changed;
        lock (sync)
        {
            changed = field != value;
            field = value;
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
