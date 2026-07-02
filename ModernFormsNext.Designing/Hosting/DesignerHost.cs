namespace ModernFormsNext.Designing;

/// <summary>
/// Hosts a designer document and model-level services for selection and hit-testing.
/// </summary>
/// <remarks>
/// This host deliberately avoids UI-specific types so it can be reused by a standalone
/// playground today and a Visual Studio designer surface later.
/// </remarks>
public sealed class DesignerHost
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerHost"/> class.
    /// </summary>
    /// <param name="document">The initial designer document.</param>
    public DesignerHost(DesignDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets or sets the hosted designer document.
    /// </summary>
    public DesignDocument Document { get; set; }

    /// <summary>
    /// Gets the selection service for the hosted document.
    /// </summary>
    public DesignerSelectionService Selection { get; } = new();

    /// <summary>
    /// Replaces the hosted document and clears selection.
    /// </summary>
    /// <param name="document">The new document.</param>
    public void LoadDocument(DesignDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Selection.Clear();
    }

    /// <summary>
    /// Performs a rectangular hit-test against the document control tree.
    /// </summary>
    /// <param name="x">The horizontal coordinate in document logical pixels.</param>
    /// <param name="y">The vertical coordinate in document logical pixels.</param>
    /// <returns>The hit-test result. If multiple controls overlap, the last control in document order wins.</returns>
    public DesignerHitTestResult HitTest(int x, int y)
        => HitTest(Document.Controls, x, y, offsetX: 0, offsetY: 0) ?? DesignerHitTestResult.Empty;

    /// <summary>
    /// Selects the node at the specified point, or clears selection when no node is hit.
    /// </summary>
    /// <param name="x">The horizontal coordinate in document logical pixels.</param>
    /// <param name="y">The vertical coordinate in document logical pixels.</param>
    /// <returns>The hit-test result used for selection.</returns>
    public DesignerHitTestResult SelectAt(int x, int y)
    {
        var result = HitTest(x, y);
        Selection.Select(result.Node);
        return result;
    }

    private static DesignerHitTestResult? HitTest(
        DesignControlCollection controls,
        int x,
        int y,
        int offsetX,
        int offsetY)
    {
        for (var index = controls.Count - 1; index >= 0; index--)
        {
            var control = controls[index];
            var absoluteBounds = new DesignBounds(
                offsetX + control.Bounds.X,
                offsetY + control.Bounds.Y,
                control.Bounds.Width,
                control.Bounds.Height);

            if (!absoluteBounds.Contains(x, y))
                continue;

            var childHit = HitTest(control.Children, x, y, absoluteBounds.X, absoluteBounds.Y);

            return childHit ?? new DesignerHitTestResult(control, absoluteBounds);
        }

        return null;
    }
}
