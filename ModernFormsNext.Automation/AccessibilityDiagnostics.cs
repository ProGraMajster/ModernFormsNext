using System.Collections.Immutable;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation;

/// <summary>Identifies a provable condition in captured, non-redacted semantic metadata.</summary>
public enum AccessibilityDiagnosticCode
{
    /// <summary>An interactive semantic control has no non-whitespace accessible name.</summary>
    MissingInteractiveName = 1,
    /// <summary>A node simultaneously advertises mutually exclusive expanded and collapsed states.</summary>
    ContradictoryExpansionState = 2,
    /// <summary>Captured bounds have a negative width or height.</summary>
    InvalidBounds = 3,
    /// <summary>Captured numeric metadata is invalid or a read-only range advertises a value-changing action.</summary>
    InvalidRange = 4,
    /// <summary>A captured grid cell has invalid coordinates or exceeds captured containing-grid dimensions.</summary>
    InvalidGridCoordinates = 5
}

/// <summary>Contains only a stable code and canonical identities, never application text or values.</summary>
public sealed class AccessibilityDiagnostic
{
    internal AccessibilityDiagnostic(AccessibilityDiagnosticCode code, AutomationNodeSnapshot node)
    { Code = code; Handle = node.Handle; RootId = node.RootId; CaptureId = node.CaptureId; }
    /// <summary>Gets the stable diagnostic category.</summary>
    public AccessibilityDiagnosticCode Code { get; }
    /// <summary>Gets the session-scoped canonical handle from the original capture.</summary>
    public AutomationNodeHandle Handle { get; }
    /// <summary>Gets the registration needed to resolve the handle through the existing session.</summary>
    public string RootId { get; }
    /// <summary>Gets the original capture identity, not a current semantic revision.</summary>
    public string CaptureId { get; }
}

/// <summary>Contains a bounded, detached diagnostic result with explicit coverage limitations.</summary>
public sealed class AccessibilityDiagnosticReport
{
    internal AccessibilityDiagnosticReport(ImmutableArray<AccessibilityDiagnostic> diagnostics, bool incomplete, bool truncated)
    { Diagnostics = diagnostics; CoverageIncomplete = incomplete; Truncated = truncated; }
    /// <summary>Gets diagnostics in captured node order, then stable code order.</summary>
    public ImmutableArray<AccessibilityDiagnostic> Diagnostics { get; }
    /// <summary>Gets whether faults, privacy, capture limits or diagnostic limits prevented complete analysis.</summary>
    /// <remarks>False does not certify accessibility compliance; only the implemented metadata checks ran.</remarks>
    public bool CoverageIncomplete { get; }
    /// <summary>Gets whether the diagnostic output limit omitted additional findings.</summary>
    public bool Truncated { get; }
}

/// <summary>Performs bounded, read-only checks over an existing AutomationSession capture.</summary>
/// <remarks>
/// Safe on any thread after capture. This consumer performs no live reads, tree traversal, actions,
/// rendering or focus changes. It retains no snapshot, control or text after returning. A redacted,
/// truncated or getter-failed node never becomes a definite missing-name finding. Capture fresh data
/// through the existing session when needed; results are observations, not compliance certification.
/// </remarks>
/// <example><code>
/// var capture = await session.FindAllAsync(root.RootId, new AutomationQuery());
/// var report = AccessibilityDiagnostics.Analyze(capture);
/// foreach (var diagnostic in report.Diagnostics)
///     Console.WriteLine($"{diagnostic.Code}: {diagnostic.Handle.RuntimeId}");
/// </code></example>
public static class AccessibilityDiagnostics
{
    /// <summary>Analyzes detached semantic metadata without invoking any application code.</summary>
    /// <param name="capture">An existing bounded node-array result, including its capture issues.</param>
    /// <param name="maximumDiagnostics">Maximum returned findings, from 1 to 4096; the default is 256.</param>
    /// <returns>A detached report containing codes and handles only.</returns>
    /// <exception cref="ArgumentNullException">The capture is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The requested limit is outside its bounds.</exception>
    public static AccessibilityDiagnosticReport Analyze(AutomationResult<ImmutableArray<AutomationNodeSnapshot>> capture,
        int maximumDiagnostics = 256)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDiagnostics, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumDiagnostics, 4096);
        var result = ImmutableArray.CreateBuilder<AccessibilityDiagnostic>();
        bool incomplete = capture.Error != AutomationErrorCode.None || capture.Truncated || capture.Issues.Length != 0;
        if (capture.Value.IsDefaultOrEmpty) return new(result.ToImmutable(), incomplete, false);
        var affected = capture.Issues.Where(i => i.RuntimeId is not null).Select(i => i.RuntimeId!).ToHashSet(StringComparer.Ordinal);
        bool unknownFault = capture.Issues.Any(i => i.RuntimeId is null);
        bool Safe(AutomationNodeSnapshot node) => !unknownFault && !node.Truncated && node.Redaction == AutomationRedaction.None
            && !affected.Contains(node.RuntimeId);
        // This index references already captured DTOs only. It is not a semantic tree, and a
        // missing parent/grid snapshot is never treated as a broken relationship.
        var grids = new Dictionary<(string Root, string Id), AutomationNodeSnapshot>();
        foreach (var node in capture.Value)
            if (Safe(node) && node.GridInfo is not null) grids.TryAdd((node.RootId, node.RuntimeId), node);
        bool truncated = false;
        void Add(AutomationNodeSnapshot node, AccessibilityDiagnosticCode code)
        {
            if (result.Count == maximumDiagnostics) { truncated = true; incomplete = true; return; }
            result.Add(new(code, node));
        }
        foreach (var node in capture.Value) {
            if (!Safe(node)) { incomplete = true; continue; }
            if (NeedsName(node.ControlType) && string.IsNullOrWhiteSpace(node.Name)) Add(node, AccessibilityDiagnosticCode.MissingInteractiveName);
            if ((node.States & (AccessibleStates.Expanded | AccessibleStates.Collapsed)) == (AccessibleStates.Expanded | AccessibleStates.Collapsed))
                Add(node, AccessibilityDiagnosticCode.ContradictoryExpansionState);
            if (node.Bounds.Width < 0 || node.Bounds.Height < 0) Add(node, AccessibilityDiagnosticCode.InvalidBounds);
            if (node.RangeValue is { } range && (!double.IsFinite(range.Minimum) || !double.IsFinite(range.Maximum)
                || !double.IsFinite(range.Value) || range.Minimum > range.Maximum || range.Value < range.Minimum || range.Value > range.Maximum
                || !double.IsFinite(range.SmallChange) || range.SmallChange < 0 || !double.IsFinite(range.LargeChange) || range.LargeChange < 0
                || range.IsReadOnly && (node.SupportedActions & AccessibleActions.SetValue) != 0))
                Add(node, AccessibilityDiagnosticCode.InvalidRange);
            if (node.GridCell is { } cell) {
                bool invalid = cell.Row < 0 || cell.Column < 0 || cell.RowSpan <= 0 || cell.ColumnSpan <= 0;
                if (grids.TryGetValue((node.RootId, cell.GridRuntimeId), out var gridNode) && gridNode.SessionId == node.SessionId
                    && gridNode.CaptureId == node.CaptureId && gridNode.GridInfo is { } grid)
                    invalid |= (long)cell.Row + cell.RowSpan > grid.Rows || (long)cell.Column + cell.ColumnSpan > grid.Columns;
                if (invalid) Add(node, AccessibilityDiagnosticCode.InvalidGridCoordinates);
            }
            if (truncated) break;
        }
        return new(result.ToImmutable(), incomplete, truncated);
    }

    private static bool NeedsName(AccessibleControlType type) => type is AccessibleControlType.Button
        or AccessibleControlType.CheckBox or AccessibleControlType.RadioButton or AccessibleControlType.Switch
        or AccessibleControlType.Edit or AccessibleControlType.ComboBox or AccessibleControlType.Slider
        or AccessibleControlType.Spinner or AccessibleControlType.Hyperlink or AccessibleControlType.TabItem
        or AccessibleControlType.MenuItem or AccessibleControlType.ListItem;
}
