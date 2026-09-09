using System.Collections.Immutable;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation;

/// <summary>Specifies the canonical coordinate reference for bounds; no clipping or occlusion guarantee is implied.</summary>
public enum AutomationCoordinateSpace
{
    /// <summary>Canonical screen coordinates from a window peer.</summary>
    Screen,
    /// <summary>Canonical coordinates relative to a windowless Skia surface.</summary>
    Surface
}

/// <summary>Contains detached canonical bounds in logical pixels.</summary>
/// <param name="X">The left coordinate.</param>
/// <param name="Y">The top coordinate.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="CoordinateSpace">The coordinate reference supplied by the registered host.</param>
public readonly record struct AutomationBounds(int X, int Y, int Width, int Height, AutomationCoordinateSpace CoordinateSpace);

/// <summary>Describes why string/value/range metadata was omitted.</summary>
[Flags]
public enum AutomationRedaction
{
    /// <summary>No privacy redaction was required.</summary>
    None = 0,
    /// <summary>This node or an ancestor was marked sensitive or protected.</summary>
    Sensitive = 1,
    /// <summary>Privacy could not be established because a privacy/state getter failed.</summary>
    PrivacyUnknown = 2
}

/// <summary>Contains immutable semantic data captured on the UI thread, with no live framework references.</summary>
/// <remarks>
/// Text fields are omitted for sensitive/protected subtrees. Child IDs describe captured active edges;
/// Truncated means the child sequence or payload is incomplete. Inspect the result's Issues as well.
/// CaptureId groups one UI operation, not a business transaction or semantic revision.
/// </remarks>
public sealed class AutomationNodeSnapshot
{
    internal AutomationNodeSnapshot(AutomationNodeHandle handle, string rootId, string? automationId,
        string? name, AccessibleRole role, AccessibleControlType controlType, AccessibleStates states,
        AccessibleActions actions, string? value, AccessibleRangeValue? range, AutomationBounds bounds,
        string? parentId, ImmutableArray<string> children, AutomationRedaction redaction, string captureId, bool truncated)
    {
        Handle = handle; RootId = rootId; AutomationId = automationId; Name = name; Role = role;
        ControlType = controlType; States = states; SupportedActions = actions; Value = value;
        RangeValue = range; Bounds = bounds; ParentRuntimeId = parentId; ChildRuntimeIds = children;
        Redaction = redaction; CaptureId = captureId; Truncated = truncated;
    }

    /// <summary>Gets the session-scoped canonical handle.</summary>
    public AutomationNodeHandle Handle { get; }
    /// <summary>Gets the semantic session nonce.</summary>
    public string SessionId => Handle.SessionId;
    /// <summary>Gets the root registration ID, required when resolving this handle.</summary>
    public string RootId { get; }
    /// <summary>Gets the canonical Int64 runtime ID as a precision-preserving decimal string.</summary>
    public string RuntimeId => Handle.RuntimeId;
    /// <summary>Gets the potentially nonunique locator, or null when absent/redacted/unavailable.</summary>
    public string? AutomationId { get; }
    /// <summary>Gets the canonical name, or null when absent/redacted/unavailable.</summary>
    public string? Name { get; }
    /// <summary>Gets the canonical role.</summary>
    public AccessibleRole Role { get; }
    /// <summary>Gets the canonical normalized control type.</summary>
    public AccessibleControlType ControlType { get; }
    /// <summary>Gets captured canonical state flags.</summary>
    public AccessibleStates States { get; }
    /// <summary>Gets captured advertised actions; actions must recheck the live peer.</summary>
    public AccessibleActions SupportedActions { get; }
    /// <summary>Gets the safe semantic value, or null when absent/redacted/unavailable.</summary>
    public string? Value { get; }
    /// <summary>Gets safe immutable numeric metadata, or null when absent/redacted/unavailable.</summary>
    public AccessibleRangeValue? RangeValue { get; }
    /// <summary>Gets canonical bounds without additional clipping/occlusion inference.</summary>
    public AutomationBounds Bounds { get; }
    /// <summary>Gets the captured parent ID within this root; null for the registered root.</summary>
    public string? ParentRuntimeId { get; }
    /// <summary>Gets the immutable sequence of captured active child IDs in canonical order.</summary>
    public ImmutableArray<string> ChildRuntimeIds { get; }
    /// <summary>Gets privacy redaction reasons.</summary>
    public AutomationRedaction Redaction { get; }
    /// <summary>Gets the opaque identity of the capturing operation.</summary>
    public string CaptureId { get; }
    /// <summary>Gets whether limits prevented complete capture of this node's fields or children.</summary>
    public bool Truncated { get; }
}
