using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.History;

/// <summary>Retains ordered selection and primary at one existing history boundary.</summary>
internal readonly struct DesignerSelectionSnapshot
{
    private readonly IReadOnlyList<DesignControlNode>? nodes;
    private readonly DesignControlNode? primary;

    private DesignerSelectionSnapshot(DesignerSelectionService selection)
    {
        // The canonical service exposes immutable membership snapshots. Borrow that snapshot
        // rather than copying the selection at every nested transaction or pointer operation.
        nodes = selection.SelectedNodes;
        primary = selection.SelectedNode;
    }

    internal static DesignerSelectionSnapshot Capture(DesignerSelectionService selection) => new(selection);

    internal bool Matches(DesignerSelectionService selection)
    {
        var expected = nodes ?? Array.Empty<DesignControlNode>();
        if (!ReferenceEquals(primary, selection.SelectedNode) || expected.Count != selection.SelectedNodes.Count)
            return false;
        for (int index = 0; index < expected.Count; index++)
            if (!ReferenceEquals(expected[index], selection.SelectedNodes[index])) return false;
        return true;
    }

    internal void Restore(DesignerSelectionService selection)
        => selection.SelectNodes(nodes ?? Array.Empty<DesignControlNode>(), primary);
}
