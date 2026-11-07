namespace ModernFormsNext.Drawing;

/// <summary>Combines ordered child contours under one fill rule and transform.</summary>
/// <remarks>
/// This is contour composition, not a Boolean union. The group's fill rule governs all appended
/// contours, after each child's own transform and before the group's transform. Children remain
/// shareable; their changes invalidate the group without retaining a discarded group through a child.
/// </remarks>
/// <example><code>
/// var group = new GeometryGroup { FillRule = GeometryFillRule.EvenOdd };
/// group.Children.Add(new RectangleGeometry(new RectangleF(0, 0, 100, 100)));
/// group.Children.Add(new EllipseGeometry(new RectangleF(25, 25, 50, 50)));
/// </code></example>
public sealed class GeometryGroup : Geometry
{
    private GeometryFillRule fillRule;
    /// <summary>Initializes an empty group with the winding fill rule.</summary>
    public GeometryGroup()
    {
        Children = new(this);
        Children.Changed += (_, _) => OnChanged();
    }
    /// <summary>Gets the ordered borrowed child geometries.</summary>
    public GeometryCollection Children { get; }
    /// <summary>Gets or sets the fill rule applied to the combined contours.</summary>
    public GeometryFillRule FillRule
    {
        get => fillRule;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (fillRule == value) return;
            fillRule = value;
            OnChanged();
        }
    }
}
