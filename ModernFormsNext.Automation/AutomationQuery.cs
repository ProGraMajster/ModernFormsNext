using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation;

/// <summary>Combines exact ordinal semantic filters with all-required and all-excluded state predicates.</summary>
/// <remarks>Null filters are ignored; an empty string matches only an empty string. Filters see redacted snapshots, never private values.</remarks>
public sealed record AutomationQuery
{
    /// <summary>Gets the optional, potentially nonunique semantic locator.</summary>
    public string? AutomationId { get; init; }
    /// <summary>Gets the optional accessible name; this is not arbitrary Control.Text.</summary>
    public string? Name { get; init; }
    /// <summary>Gets the optional normalized semantic type.</summary>
    public AccessibleControlType? ControlType { get; init; }
    /// <summary>Gets flags that must all be present.</summary>
    public AccessibleStates RequiredStates { get; init; }
    /// <summary>Gets flags that must all be absent.</summary>
    public AccessibleStates ExcludedStates { get; init; }

    internal bool Matches(AutomationNodeSnapshot node)
        => (AutomationId is null || string.Equals(AutomationId, node.AutomationId, StringComparison.Ordinal))
        && (Name is null || string.Equals(Name, node.Name, StringComparison.Ordinal))
        && (ControlType is null || ControlType == node.ControlType)
        && (node.States & RequiredStates) == RequiredStates
        && (node.States & ExcludedStates) == 0;
}

/// <summary>Bounds every live traversal in a session. Options are immutable after initialization.</summary>
/// <remarks>
/// Root depth is zero. Child attempts, including malformed or hidden children, consume the node budget.
/// Limits never interrupt an individual application getter: custom peers must return promptly.
/// </remarks>
public sealed record AutomationQueryOptions
{
    /// <summary>Gets the maximum depth, from 0 to 256; the default is 64.</summary>
    public int MaxDepth { get; init; } = 64;
    /// <summary>Gets the maximum node/edge attempts, from 1 to 100000; the default is 4096.</summary>
    public int MaxNodes { get; init; } = 4096;
    /// <summary>Gets the maximum returned roots or nodes, from 1 to 100000; the default is 1024.</summary>
    public int MaxResults { get; init; } = 1024;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDepth, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxDepth, 256);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxNodes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxNodes, 100000);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxResults, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxResults, 100000);
    }
}
