using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation;

/// <summary>Identifies a bounded predicate over current canonical semantic data.</summary>
public enum AutomationWaitKind
{
    /// <summary>A unique matching node is exposed.</summary>
    NodeExists,
    /// <summary>The target is absent from a complete active semantic traversal.</summary>
    NodeNotExposed,
    /// <summary>The exposed target is not Unavailable.</summary>
    Enabled,
    /// <summary>The target is exposed in the active semantic projection; no occlusion guarantee is implied.</summary>
    Exposed,
    /// <summary>The target has the Focused state.</summary>
    Focused,
    /// <summary>The target has the Selected state.</summary>
    Selected,
    /// <summary>The unredacted semantic Value equals the supplied text ordinally.</summary>
    ValueEquals,
    /// <summary>The target contains every supplied state flag.</summary>
    StateContains,
    /// <summary>The root registration has ended, including explicit unregistration or root disposal.</summary>
    RootEnded
}

/// <summary>Describes one live predicate without an expression language or private control access.</summary>
/// <remarks>Supply exactly one Handle or Query except for RootEnded, which takes neither. Queries must resolve uniquely.</remarks>
public sealed record AutomationWaitCondition
{
    /// <summary>Gets the predicate to evaluate.</summary>
    public AutomationWaitKind Kind { get; init; }
    /// <summary>Gets the optional exact session-scoped target.</summary>
    public AutomationNodeHandle? Handle { get; init; }
    /// <summary>Gets the optional canonical query evaluated afresh after every wakeup.</summary>
    public AutomationQuery? Query { get; init; }
    /// <summary>Gets the expected value for ValueEquals, at most 4096 characters. Never include secrets in diagnostics.</summary>
    public string? Value { get; init; }
    /// <summary>Gets all required flags for StateContains.</summary>
    public AccessibleStates States { get; init; }
}

/// <summary>Bounds a live wait independently of semantic traversal budgets.</summary>
public sealed record AutomationWaitOptions
{
    /// <summary>Gets the monotonic timeout, from zero to 60 seconds; default 10 seconds. Zero still performs an immediate check.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Gets the fallback interval, from 20 milliseconds to one second; default 100 milliseconds.</summary>
    public TimeSpan ReconciliationInterval { get; init; } = TimeSpan.FromMilliseconds(100);
}

/// <summary>Explains why a live wait ended; satisfaction is distinct from an accepted action.</summary>
public enum AutomationWaitStatus
{
    /// <summary>The current semantic predicate was satisfied.</summary>
    Satisfied,
    /// <summary>The monotonic deadline elapsed.</summary>
    TimedOut,
    /// <summary>The caller cancelled the wait.</summary>
    Cancelled,
    /// <summary>The semantic session ended.</summary>
    SessionEnded,
    /// <summary>The registered root ended before a node predicate was satisfied.</summary>
    RootEnded,
    /// <summary>A safe semantic error prevents evaluating the predicate.</summary>
    Failed
}

/// <summary>Contains a completed wait's detached evidence and monotonic elapsed time.</summary>
/// <param name="Status">The reason the wait ended.</param>
/// <param name="Error">The safe semantic error, if any.</param>
/// <param name="Snapshot">The satisfied target, or null for absence/lifetime predicates and unsuccessful waits.</param>
/// <param name="Elapsed">Elapsed monotonic time, including queued UI work.</param>
public sealed record AutomationWaitResult(AutomationWaitStatus Status, AutomationErrorCode Error,
    AutomationNodeSnapshot? Snapshot, TimeSpan Elapsed);
