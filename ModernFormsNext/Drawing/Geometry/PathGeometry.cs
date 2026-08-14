namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents reusable vector contours composed from figures and line or Bézier segments.
/// </summary>
/// <remarks>
/// The collection hierarchy is fully observable: changing a segment, figure, or collection raises
/// <see cref="Geometry.Changed"/> and invalidates every sharing <see cref="ModernFormsNext.Path"/>.
/// </remarks>
public sealed class PathGeometry : Geometry
{
    private GeometryFillRule fillRule;

    /// <summary>Initializes an empty path using the non-zero winding fill rule.</summary>
    public PathGeometry()
    {
        Figures.Changed += HandleFiguresChanged;
    }

    /// <summary>Gets the observable ordered contour collection.</summary>
    public PathFigureCollection Figures { get; } = new();

    /// <summary>Gets or sets the rule used to determine filled regions.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined enum value.</exception>
    public GeometryFillRule FillRule
    {
        get => fillRule;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The geometry fill rule is not defined.");
            if (fillRule == value)
                return;

            fillRule = value;
            OnChanged();
        }
    }

    private void HandleFiguresChanged(object? sender, EventArgs e) => OnChanged();
}
