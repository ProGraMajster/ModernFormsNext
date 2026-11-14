namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Provides payload-free diagnostics for the existing lifecycle state publisher.</summary>
/// <remarks>A lifecycle provider can implement this capability independently of its activation
/// or controller interfaces. Read it on the provider's owning UI thread.</remarks>
public interface IPlatformApplicationStateDiagnostics
{
    /// <summary>Returns a detached read-only history of recent completed state notifications.</summary>
    /// <returns>Entries in provider sequence order. The history can be empty.</returns>
    IReadOnlyList<PlatformApplicationStateDelivery> GetRecentStateDeliveries();
}

/// <summary>Distinguishes the two directions of a state handoff.</summary>
public enum PlatformApplicationStateOperation
{
    /// <summary>The provider asked observers for a save payload.</summary>
    Save,
    /// <summary>The provider supplied a restore payload to observers.</summary>
    Restore
}

/// <summary>Describes state notification delivery without retaining the payload or observer exceptions.</summary>
/// <param name="Sequence">The provider-local sequence, shared with activation and lifecycle events.</param>
/// <param name="Operation">Whether the provider requested state or delivered it.</param>
/// <param name="Reason">The reason supplied by the requesting host or application.</param>
/// <param name="SchemaVersion">The final supplied schema version, or null when no save state was supplied.</param>
/// <param name="FailedObserverCount">Number of provider event subscriptions whose callbacks threw.</param>
/// <remarks>This describes notification delivery. It does not establish persistence, schema
/// acceptance or restoration of application UI.</remarks>
public readonly record struct PlatformApplicationStateDelivery(
    long Sequence, PlatformApplicationStateOperation Operation, PlatformApplicationStateReason Reason,
    int? SchemaVersion, int FailedObserverCount);
