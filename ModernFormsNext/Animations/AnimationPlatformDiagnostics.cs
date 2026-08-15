using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.Animations;

/// <summary>
/// Represents a read-only snapshot of native animation-policy integration.
/// </summary>
public sealed class AnimationPlatformDiagnostics
{
    internal AnimationPlatformDiagnostics(
        string source,
        bool reducedMotion,
        bool animationsEnabled,
        double platformDurationScale,
        DateTimeOffset? lastPlatformUpdate,
        bool fallbackUsed,
        PlatformAnimationProviderState providerState,
        string? lastError)
    {
        Source = source;
        ReducedMotion = reducedMotion;
        AnimationsEnabled = animationsEnabled;
        PlatformDurationScale = platformDurationScale;
        LastPlatformUpdate = lastPlatformUpdate;
        FallbackUsed = fallbackUsed;
        ProviderState = providerState;
        LastError = lastError;
    }

    /// <summary>Gets the native or fallback source name.</summary>
    public string Source { get; }

    /// <summary>Gets the effective reduced-motion state used by the scheduler.</summary>
    public bool ReducedMotion { get; }

    /// <summary>Gets whether animations are effectively enabled.</summary>
    public bool AnimationsEnabled { get; }

    /// <summary>Gets the native duration multiplier applied to newly started animations.</summary>
    public double PlatformDurationScale { get; }

    /// <summary>Gets the last native provider read attempt, in UTC.</summary>
    public DateTimeOffset? LastPlatformUpdate { get; }

    /// <summary>Gets whether the native provider used compatibility defaults.</summary>
    public bool FallbackUsed { get; }

    /// <summary>Gets the current provider health state.</summary>
    public PlatformAnimationProviderState ProviderState { get; }

    /// <summary>Gets the last non-sensitive provider error, if any.</summary>
    public string? LastError { get; }
}
