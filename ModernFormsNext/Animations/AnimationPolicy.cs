namespace ModernFormsNext.Animations;

/// <summary>
/// Provides the central policy for enabling, scaling, or reducing UI motion.
/// </summary>
/// <remarks>
/// The policy is shared by one <see cref="AnimationScheduler"/>. Properties are thread-safe.
/// Disabling animations, enabling reduced motion, or setting <see cref="DurationScale"/> to zero
/// completes active animations at their final value on the UI thread and prevents timer ticks.
/// Native operating-system reduced-motion discovery is applied independently from the explicit
/// application preference. Either source can request immediate completion.
/// </remarks>
public sealed class AnimationPolicy
{
    private readonly object sync = new();
    private bool animationsEnabled = true;
    private bool reducedMotion;
    private bool platformReducedMotion;
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
    /// Gets whether reduced motion is effectively requested and sets the application preference.
    /// </summary>
    /// <remarks>
    /// Reduced motion has the same scheduling behavior as disabling animations: final values are
    /// applied immediately. The getter combines this explicit application preference with the
    /// current native platform preference. Setting this property does not override a platform
    /// reduced-motion request.
    /// </remarks>
    public bool ReducedMotion
    {
        get
        {
            lock (sync)
                return reducedMotion || platformReducedMotion;
        }
        set
        {
            bool changed;
            lock (sync)
            {
                bool previous = reducedMotion || platformReducedMotion;
                reducedMotion = value;
                changed = previous != (reducedMotion || platformReducedMotion);
            }

            if (changed)
                Changed?.Invoke(this, EventArgs.Empty);
        }
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
                return !animationsEnabled || reducedMotion || platformReducedMotion || durationScale == 0d;
        }
    }

    /// <summary>Gets whether reduced motion was explicitly requested by application code.</summary>
    /// <remarks>
    /// Use this read-only value when a settings UI must preserve the application preference
    /// independently from the effective <see cref="ReducedMotion"/> value contributed by the
    /// operating system.
    /// </remarks>
    public bool ApplicationReducedMotion
    {
        get
        {
            lock (sync)
                return reducedMotion;
        }
    }

    internal void SetPlatformReducedMotion(bool value)
    {
        bool changed;
        lock (sync)
        {
            bool previous = reducedMotion || platformReducedMotion;
            platformReducedMotion = value;
            changed = previous != (reducedMotion || platformReducedMotion);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
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
