namespace ModernFormsNext.Automation;

/// <summary>Identifies a safe, structured semantic outcome without exporting application exception text.</summary>
public enum AutomationErrorCode
{
    /// <summary>The query completed without an error.</summary>
    None,
    /// <summary>A complete search found no matching node.</summary>
    NodeNotFound,
    /// <summary>At least two nodes match a request requiring exactly one.</summary>
    AmbiguousMatch,
    /// <summary>The handle belongs to another session or an ended root registration.</summary>
    StaleNode,
    /// <summary>The node is not currently exposed by the requested root; this does not imply disposal.</summary>
    NodeUnavailable,
    /// <summary>The action is not advertised or is outside the supported Phase 1a action set.</summary>
    ActionUnsupported,
    /// <summary>The current state or canonical action path rejected the request.</summary>
    ActionRejected,
    /// <summary>The request has an invalid type, value, range, scope, or flag combination.</summary>
    InvalidArgument,
    /// <summary>A live operation or dispatcher call failed; private exception data is not returned.</summary>
    ApplicationError,
    /// <summary>The semantic session has stopped.</summary>
    SessionEnded,
    /// <summary>The session or root does not allow this operation.</summary>
    CapabilityDenied,
    /// <summary>A traversal, result, or text budget prevented complete capture.</summary>
    LimitExceeded,
    /// <summary>A custom semantic getter threw; its private exception data is not returned.</summary>
    GetterFault,
    /// <summary>A child, parent edge, or child count violates the canonical tree contract.</summary>
    MalformedTree,
    /// <summary>A child edge or parent chain contains a cycle.</summary>
    CycleDetected,
    /// <summary>A runtime ID or canonical object occurs more than once in a root traversal.</summary>
    DuplicateRuntimeId
}

/// <summary>Identifies the semantic field associated with a safe capture diagnostic.</summary>
public enum AutomationProperty
{
    /// <summary>The diagnostic concerns an entire node or traversal.</summary>
    Node,
    /// <summary>The privacy marker could not be read.</summary>
    IsSensitive,
    /// <summary>The state flags could not be read.</summary>
    States,
    /// <summary>The active projection could not be read.</summary>
    View,
    /// <summary>The parent edge is invalid or unavailable.</summary>
    Parent,
    /// <summary>The child sequence is invalid or unavailable.</summary>
    Children,
    /// <summary>The locator could not be safely captured.</summary>
    AutomationId,
    /// <summary>The semantic name could not be safely captured.</summary>
    Name,
    /// <summary>The role could not be read.</summary>
    Role,
    /// <summary>The normalized control type could not be read.</summary>
    ControlType,
    /// <summary>The advertised actions could not be read.</summary>
    SupportedActions,
    /// <summary>The value could not be safely captured.</summary>
    Value,
    /// <summary>The numeric range could not be safely captured.</summary>
    RangeValue,
    /// <summary>The bounds could not be read.</summary>
    Bounds,
    /// <summary>The viewport metadata could not be safely captured.</summary>
    ScrollInfo,
    /// <summary>The grid metadata could not be safely captured.</summary>
    GridInfo,
    /// <summary>The cell metadata could not be safely captured.</summary>
    GridCell
}

/// <summary>Contains only controlled diagnostic metadata; it never contains an exception or application message.</summary>
/// <param name="Code">The failure category.</param>
/// <param name="RuntimeId">The affected canonical ID as an invariant decimal string, when known.</param>
/// <param name="Property">The affected field.</param>
public sealed record AutomationIssue(AutomationErrorCode Code, string? RuntimeId, AutomationProperty Property);
