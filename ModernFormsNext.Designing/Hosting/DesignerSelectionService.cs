namespace ModernFormsNext.Designing;

/// <summary>
/// Tracks the current designer selection without depending on any UI framework types.
/// </summary>
public sealed class DesignerSelectionService
{
    /// <summary>
    /// Raised after the selected node changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Gets the currently selected control node.
    /// </summary>
    public DesignControlNode? SelectedNode { get; private set; }

    /// <summary>
    /// Selects a designer control node.
    /// </summary>
    /// <param name="node">The node to select, or <see langword="null"/> to clear the selection.</param>
    public void Select(DesignControlNode? node)
    {
        if (ReferenceEquals(SelectedNode, node))
            return;

        SelectedNode = node;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void Clear() => Select(null);
}
