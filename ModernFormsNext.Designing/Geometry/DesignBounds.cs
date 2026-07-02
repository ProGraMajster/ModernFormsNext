namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a rectangular region in designer logical pixels.
/// </summary>
/// <param name="X">The horizontal coordinate of the left edge in logical pixels.</param>
/// <param name="Y">The vertical coordinate of the top edge in logical pixels.</param>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <remarks>
/// Bounds are relative to the owning designer container. For root controls this is
/// the form surface; for nested controls this is the parent control node.
/// </remarks>
public readonly record struct DesignBounds(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Gets the horizontal coordinate immediately after the right edge.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Gets the vertical coordinate immediately after the bottom edge.
    /// </summary>
    public int Bottom => Y + Height;

    /// <summary>
    /// Determines whether the specified point is inside the bounds.
    /// </summary>
    /// <param name="x">The horizontal coordinate to test.</param>
    /// <param name="y">The vertical coordinate to test.</param>
    /// <returns><see langword="true"/> when the point is inside the rectangle; otherwise, <see langword="false"/>.</returns>
    public bool Contains(int x, int y)
        => x >= X && y >= Y && x < Right && y < Bottom;
}
