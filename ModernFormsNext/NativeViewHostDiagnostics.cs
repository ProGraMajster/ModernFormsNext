using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

/// <summary>Describes the current native hosting lifecycle.</summary>
public enum NativeViewHostState
{
    /// <summary>No peer is attached.</summary>
    Detached,
    /// <summary>A native peer is attached and visible.</summary>
    Attached,
    /// <summary>An attached peer is hidden or its presentation is unavailable.</summary>
    Suspended,
    /// <summary>The backend or current composition is unsupported.</summary>
    Unsupported,
    /// <summary>Peer creation or an update failed.</summary>
    Faulted
}

/// <summary>Provides a pointer-free snapshot; it never includes native handles or feature content.</summary>
/// <param name="State">Current lifecycle state.</param>
/// <param name="Placement">Logical and native geometry, scale and effective state.</param>
/// <param name="Generation">Monotonic session identity.</param>
/// <param name="Reason">A framework-generated reason, without native content.</param>
public readonly record struct NativeViewHostDiagnostics(
    NativeViewHostState State, NativeViewPlacement Placement, long Generation, string? Reason);
