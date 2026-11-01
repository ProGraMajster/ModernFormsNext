namespace ModernFormsNext.Designing;

/// <summary>
/// Tracks ordered Designer control selection and its primary node without UI framework dependencies.
/// </summary>
/// <remarks>
/// Nodes are borrowed, never cloned or disposed. Selection alone does not mutate the document,
/// layout, dirty state or undo history. Notifications are synchronous on the caller's thread;
/// Designer hosts must use their session's UI thread. The service provides no synchronization.
/// </remarks>
public sealed class DesignerSelectionService
{
    private IReadOnlyList<DesignControlNode> selectedNodes = Array.Empty<DesignControlNode>();

    /// <summary>
    /// Raised after ordered membership or the primary node changes.
    /// </summary>
    /// <remarks>The complete new selection is visible before notification. Reentrant selection wins.</remarks>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Gets the primary selected control, or <see langword="null"/> for the design root.
    /// </summary>
    /// <remarks>The primary is always a member of <see cref="SelectedNodes"/> when selection is nonempty.</remarks>
    public DesignControlNode? SelectedNode { get; private set; }

    /// <summary>
    /// Gets an immutable, ordered snapshot of the selected control nodes.
    /// </summary>
    /// <remarks>
    /// Membership uses object identity. An empty snapshot represents root selection. A retained
    /// snapshot does not change after a later selection; the nodes themselves remain borrowed.
    /// </remarks>
    public IReadOnlyList<DesignControlNode> SelectedNodes => selectedNodes;

    /// <summary>
    /// Replaces selection with one Designer control, or the design root.
    /// </summary>
    /// <param name="node">The node to select, or <see langword="null"/> to clear the selection.</param>
    public void Select(DesignControlNode? node)
    {
        if (selectedNodes.Count == (node is null ? 0 : 1) && ReferenceEquals(SelectedNode, node))
            return;

        SelectNodes(node is null ? [] : [node], node);
    }

    /// <summary>
    /// Replaces selection with distinct controls in input order and an optional primary member.
    /// </summary>
    /// <param name="nodes">Borrowed nodes; duplicates are removed by identity. Empty selects the root.</param>
    /// <param name="primaryNode">A selected member, or null to use the last distinct input node.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodes"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown before changing selection for null entries, a nonmember primary, or input exceeding
    /// the existing Designer document node budget. This limit does not constrain runtime controls.
    /// </exception>
    /// <remarks>
    /// Input is materialized and validated before state changes. The service tracks identity only;
    /// the owning session remains responsible for active-document and component-boundary rules.
    /// At most one notification is raised, after both membership and primary are committed.
    /// </remarks>
    /// <example>
    /// <code>
    /// selection.SelectNodes([nameTextBoxNode, saveButtonNode], primaryNode: saveButtonNode);
    /// </code>
    /// </example>
    public void SelectNodes(IEnumerable<DesignControlNode> nodes, DesignControlNode? primaryNode = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var members = new List<DesignControlNode>();
        var seen = new HashSet<DesignControlNode>(ReferenceEqualityComparer.Instance);
        int visited = 0;
        foreach (var node in nodes)
        {
            // Bound enumeration as well as distinct membership: repeated candidates must not
            // bypass the existing data-model budget or leave partial selection on failure.
            if (++visited > DesignDocumentValidator.MaximumNodeCount)
                throw new ArgumentException("Selection input exceeds the Designer node budget.", nameof(nodes));
            if (node is null)
                throw new ArgumentException("Selection cannot contain a null node.", nameof(nodes));
            if (seen.Add(node)) members.Add(node);
        }

        if (primaryNode is not null && !seen.Contains(primaryNode))
            throw new ArgumentException("The primary must be a selected member.", nameof(primaryNode));
        primaryNode ??= members.Count == 0 ? null : members[^1];
        bool unchanged = ReferenceEquals(SelectedNode, primaryNode) && selectedNodes.Count == members.Count;
        for (int index = 0; unchanged && index < members.Count; index++)
            unchanged = ReferenceEquals(selectedNodes[index], members[index]);
        if (unchanged) return;

        selectedNodes = members.AsReadOnly();
        SelectedNode = primaryNode;
        // Do no work after observers run: a nested Select/SelectNodes owns the newer state.
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void Clear() => Select(null);
}
