namespace ModernFormsNext.Designing;

/// <summary>
/// Describes the result of a designer surface hit-test.
/// </summary>
public sealed class DesignerHitTestResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerHitTestResult"/> class.
    /// </summary>
    /// <param name="node">The node hit by the test, or <see langword="null"/> when no node was hit.</param>
    /// <param name="bounds">The absolute bounds used for the hit-test.</param>
    public DesignerHitTestResult(DesignControlNode? node, DesignBounds bounds)
    {
        Node = node;
        Bounds = bounds;
    }

    /// <summary>
    /// Gets an empty hit-test result.
    /// </summary>
    public static DesignerHitTestResult Empty { get; } = new(null, default);

    /// <summary>
    /// Gets the hit designer node.
    /// </summary>
    public DesignControlNode? Node { get; }

    /// <summary>
    /// Gets the absolute bounds of the hit node.
    /// </summary>
    public DesignBounds Bounds { get; }

    /// <summary>
    /// Gets a value indicating whether a node was hit.
    /// </summary>
    public bool IsHit => Node is not null;
}
