namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Identifies the current health of a native animation-settings provider.
/// </summary>
public enum PlatformAnimationProviderState
{
    /// <summary>No platform provider is available.</summary>
    Unavailable,

    /// <summary>The provider returned a native platform value.</summary>
    Ready,

    /// <summary>The provider used the compatibility fallback after a native read failed.</summary>
    Fallback,

    /// <summary>Platform integration is disabled for a design-time environment.</summary>
    Disabled
}

/// <summary>
/// Represents an immutable platform animation-preference snapshot.
/// </summary>
public sealed class PlatformAnimationSettingsSnapshot
{
    /// <summary>
    /// Initializes a platform animation-preference snapshot.
    /// </summary>
    /// <param name="source">A stable human-readable source identifier.</param>
    /// <param name="reducedMotion">Whether the platform requests reduced motion.</param>
    /// <param name="animationsEnabled">Whether the platform permits UI animations.</param>
    /// <param name="lastPlatformUpdate">The last native read attempt, in UTC.</param>
    /// <param name="fallbackUsed">Whether compatibility defaults were used.</param>
    /// <param name="providerState">The provider health state.</param>
    /// <param name="lastError">The last non-sensitive native error, if any.</param>
    public PlatformAnimationSettingsSnapshot(
        string source,
        bool reducedMotion,
        bool animationsEnabled,
        DateTimeOffset? lastPlatformUpdate,
        bool fallbackUsed,
        PlatformAnimationProviderState providerState,
        string? lastError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Source = source;
        ReducedMotion = reducedMotion;
        AnimationsEnabled = animationsEnabled;
        LastPlatformUpdate = lastPlatformUpdate;
        FallbackUsed = fallbackUsed;
        ProviderState = providerState;
        LastError = lastError;
    }

    /// <summary>Gets the platform source identifier.</summary>
    public string Source { get; }

    /// <summary>Gets whether the platform requests reduced motion.</summary>
    public bool ReducedMotion { get; }

    /// <summary>Gets whether the platform permits UI animations.</summary>
    public bool AnimationsEnabled { get; }

    /// <summary>Gets the last native read attempt, in UTC.</summary>
    public DateTimeOffset? LastPlatformUpdate { get; }

    /// <summary>Gets whether compatibility defaults were used.</summary>
    public bool FallbackUsed { get; }

    /// <summary>Gets the provider health state.</summary>
    public PlatformAnimationProviderState ProviderState { get; }

    /// <summary>Gets the last non-sensitive native error, if any.</summary>
    public string? LastError { get; }
}

/// <summary>
/// Provides data for a native animation-preference change.
/// </summary>
public sealed class PlatformAnimationSettingsChangedEventArgs : EventArgs
{
    /// <summary>Initializes change data for two immutable snapshots.</summary>
    /// <param name="previous">The previous provider snapshot.</param>
    /// <param name="current">The current provider snapshot.</param>
    public PlatformAnimationSettingsChangedEventArgs(
        PlatformAnimationSettingsSnapshot previous,
        PlatformAnimationSettingsSnapshot current)
    {
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
    }

    /// <summary>Gets the previous provider snapshot.</summary>
    public PlatformAnimationSettingsSnapshot Previous { get; }

    /// <summary>Gets the current provider snapshot.</summary>
    public PlatformAnimationSettingsSnapshot Current { get; }
}

/// <summary>
/// Provides observable native animation and reduced-motion preferences.
/// </summary>
/// <remarks>
/// Implementations must be safe without an active window or activity. They must not invoke
/// <see cref="Changed"/> handlers while holding an implementation lock. Consumers are responsible
/// for marshaling policy mutations to their UI dispatcher.
/// </remarks>
public interface IPlatformAnimationSettings
{
    /// <summary>Gets the most recent immutable provider snapshot.</summary>
    PlatformAnimationSettingsSnapshot Current { get; }

    /// <summary>Occurs after a meaningful native animation preference changes.</summary>
    event EventHandler<PlatformAnimationSettingsChangedEventArgs>? Changed;

    /// <summary>
    /// Reads the current platform preference and returns the resulting snapshot.
    /// </summary>
    /// <returns>The refreshed immutable snapshot.</returns>
    PlatformAnimationSettingsSnapshot Refresh();
}
