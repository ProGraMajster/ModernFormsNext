using System.Collections.Immutable;

namespace ModernFormsNext.Automation;

/// <summary>Contains a detached query result and safe information about incomplete or failed capture.</summary>
/// <typeparam name="T">The immutable DTO or immutable DTO array returned by the operation.</typeparam>
/// <remarks>A nonempty partial Value does not certify completeness. Check Error, Truncated and Issues before drawing conclusions.</remarks>
public sealed class AutomationResult<T>
{
    internal AutomationResult(T? value, AutomationErrorCode error, string captureId,
        bool truncated = false, ImmutableArray<AutomationIssue> issues = default)
    {
        Value = value; Error = error; CaptureId = captureId; Truncated = truncated;
        Issues = issues.IsDefault ? [] : issues;
    }
    /// <summary>Gets detached captured data; failures without data return null for a single DTO or an initialized empty immutable array.</summary>
    public T? Value { get; }
    /// <summary>Gets the primary result code; None means complete capture without faults.</summary>
    public AutomationErrorCode Error { get; }
    /// <summary>Gets whether a budget prevented a complete result.</summary>
    public bool Truncated { get; }
    /// <summary>Gets safe structured diagnostics with no exception messages or request values.</summary>
    public ImmutableArray<AutomationIssue> Issues { get; }
    /// <summary>Gets the opaque capture operation identity.</summary>
    public string CaptureId { get; }
}

/// <summary>Contains detached metadata for an explicitly registered root.</summary>
/// <param name="RootId">The unique registration identity.</param>
/// <param name="Handle">The root's canonical handle.</param>
/// <param name="Capabilities">The effective allowed operations.</param>
/// <param name="CoordinateSpace">The reference frame for canonical bounds.</param>
public sealed record AutomationRootInfo(string RootId, AutomationNodeHandle Handle,
    AutomationCapability Capabilities, AutomationCoordinateSpace CoordinateSpace);
