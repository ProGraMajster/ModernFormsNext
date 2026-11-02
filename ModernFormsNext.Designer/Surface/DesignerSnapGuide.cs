using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal enum DesignerSnapGuideKind { Edge, Center, Padding, Margin, Baseline, Spacing }

// Coordinates remain document units until the existing surface/DPI boundary paints them.
internal readonly record struct DesignerSnapGuide(bool Vertical, double Coordinate, double From,
    double To, DesignerSnapGuideKind Kind, double? Gap = null);

// A selected forest may span more than Int32.MaxValue even though each authored rectangle is
// valid. Keep unions, centers, margins and candidate offsets wide until mutation admission.
internal readonly record struct DesignerSnapBounds(long Left, long Top, long Right, long Bottom)
{
    internal long Width => Right - Left;
    internal long Height => Bottom - Top;
    internal double CenterX => (Left + Right) / 2d;
    internal double CenterY => (Top + Bottom) / 2d;
    internal static DesignerSnapBounds From(DesignBounds value)
        => new(value.X, value.Y, (long)value.X + value.Width, (long)value.Y + value.Height);
    internal DesignerSnapBounds Translate(long x, long y) => new(Left + x, Top + y, Right + x, Bottom + y);
    internal static DesignerSnapBounds Union(DesignerSnapBounds first, DesignerSnapBounds second)
        => new(Math.Min(first.Left, second.Left), Math.Min(first.Top, second.Top),
            Math.Max(first.Right, second.Right), Math.Max(first.Bottom, second.Bottom));
}
