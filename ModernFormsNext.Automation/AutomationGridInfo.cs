using System.Collections.Immutable;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation;

/// <summary>Contains detached counts and traversal order from a canonical grid provider.</summary>
/// <param name="Rows">The number of semantic rows.</param>
/// <param name="Columns">The number of semantic columns, omitting hidden columns.</param>
/// <param name="IsTable">Whether header relationships are provided.</param>
/// <param name="Traversal">The primary table traversal order.</param>
public sealed record AutomationGridInfo(int Rows, int Columns, bool IsTable, AccessibleTableTraversal Traversal);

/// <summary>Contains detached cell coordinates and canonical IDs without retaining live peers.</summary>
/// <remarks>
/// IDs refer to the same session/root as the containing node snapshot. Associated headers may be
/// outside the bounded captured subtree; an ID does not imply that its snapshot was captured.
/// Re-read after structural changes. Sensitive and failed metadata is omitted.
/// </remarks>
/// <param name="GridRuntimeId">The containing grid's invariant decimal runtime ID.</param>
/// <param name="Row">The zero-based semantic row.</param>
/// <param name="Column">The zero-based semantic column.</param>
/// <param name="RowSpan">The positive row span.</param>
/// <param name="ColumnSpan">The positive column span.</param>
/// <param name="RowHeaderRuntimeIds">The bounded row header IDs.</param>
/// <param name="ColumnHeaderRuntimeIds">The bounded column header IDs.</param>
public sealed record AutomationGridCellInfo(string GridRuntimeId, int Row, int Column, int RowSpan, int ColumnSpan,
    ImmutableArray<string> RowHeaderRuntimeIds, ImmutableArray<string> ColumnHeaderRuntimeIds);
