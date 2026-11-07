using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext.Designer.Properties;

/// <summary>Bounds expanded typed data before recursive projection allocates persistence values.</summary>
internal static class DesignerGeometryAdmission
{
    internal static void Require(Geometry geometry)
    {
        var pending = new Stack<(Geometry Geometry, int Depth)>();
        pending.Push((geometry, 0));
        int visits = 0;
        while (pending.TryPop(out var item))
        {
            if (++visits > DesignDocumentValidator.MaximumPropertyValueCount || item.Depth > DesignDocumentValidator.MaximumTreeDepth - 2)
                throw new FormatException("Designer geometry exceeds the expanded data or nesting budget.");
            switch (item.Geometry)
            {
                case LineGeometry or RectangleGeometry or EllipseGeometry: break;
                case PathGeometry path:
                    foreach (var figure in path.Figures)
                    {
                        visits += 1 + figure.Segments.Count;
                        if (visits > DesignDocumentValidator.MaximumPropertyValueCount)
                            throw new FormatException("Designer geometry exceeds the expanded data budget.");
                        foreach (var segment in figure.Segments)
                            if (segment is not (LineSegment or QuadraticBezierSegment or BezierSegment or ArcSegment))
                                throw new FormatException("The data-only Designer does not admit custom path segments.");
                    }
                    break;
                case GeometryGroup group:
                    if (group.Children.Count > DesignDocumentValidator.MaximumPropertyValueCount - visits - pending.Count)
                        throw new FormatException("Designer geometry exceeds the expanded data budget.");
                    foreach (var child in group.Children) pending.Push((child, item.Depth + 1));
                    break;
                case CombinedGeometry combined:
                    if (combined.Geometry1 is { } first) pending.Push((first, item.Depth + 1));
                    if (combined.Geometry2 is { } second) pending.Push((second, item.Depth + 1));
                    break;
                default: throw new FormatException("The data-only Designer does not admit custom geometry.");
            }
        }
    }
}
