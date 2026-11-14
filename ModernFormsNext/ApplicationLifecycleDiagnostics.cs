using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext;

/// <summary>Contains detached lifecycle metadata suitable for developer diagnostics without private payloads.</summary>
public sealed class ApplicationLifecycleDiagnostics
{
    internal ApplicationLifecycleDiagnostics(PlatformApplicationLifecycleSnapshot snapshot,
        PlatformActivationKind? lastActivationKind, int openWindowCount, int activeWindowCount,
        PlatformApplicationLifecycleEventArgs[] recentTransitions,
        IReadOnlyList<PlatformApplicationStateDelivery>? recentStateDeliveries)
    {
        Snapshot = snapshot;
        LastActivationKind = lastActivationKind;
        OpenWindowCount = openWindowCount;
        ActiveWindowCount = activeWindowCount;
        RecentTransitions = Array.AsReadOnly(recentTransitions);
        RecentStateDeliveries = recentStateDeliveries;
    }

    /// <summary>Gets the immutable normalized phase, platform state and host-generation metadata.</summary>
    public PlatformApplicationLifecycleSnapshot Snapshot { get; }

    /// <summary>Gets the last activation category, without its arguments, locations or URI.</summary>
    public PlatformActivationKind? LastActivationKind { get; }

    /// <summary>Gets the number of open framework Forms; popups and borrowed platform surfaces are excluded.</summary>
    public int OpenWindowCount { get; }

    /// <summary>Gets the number of active framework Forms independently from application foreground state.</summary>
    public int ActiveWindowCount { get; }

    /// <summary>Gets at most 64 transitions in notification order, without activation or saved-state payloads.</summary>
    public IReadOnlyList<PlatformApplicationLifecycleEventArgs> RecentTransitions { get; }

    /// <summary>Gets completed save and restore notifications without state contents or exception text.</summary>
    /// <remarks>The built-in provider retains at most 64. A null value means the provider does not
    /// expose this diagnostic capability. An empty collection means it has no recorded delivery.</remarks>
    public IReadOnlyList<PlatformApplicationStateDelivery>? RecentStateDeliveries { get; }
}
