using System.Drawing;

namespace ModernFormsNext.Accessibility;

/// <summary>Describes one viewport axis in logical control units, independently of enabled state.</summary>
public readonly record struct AccessibleScrollAxis
{
    /// <summary>Creates finite axis metadata with inclusive movement endpoints.</summary>
    /// <param name="offset">Current offset, between minimum and maximum.</param>
    /// <param name="minimum">Inclusive starting offset.</param>
    /// <param name="maximum">Inclusive ending offset; this is not reduced by the page amount.</param>
    /// <param name="viewportLength">Actual visible length, not the scrollbar's clamped LargeChange.</param>
    /// <param name="smallChange">Logical movement for a small request.</param>
    /// <param name="largeChange">Logical movement for a page request.</param>
    /// <exception cref="ArgumentOutOfRangeException">Metadata is nonfinite, negative, inverted or outside its range.</exception>
    public AccessibleScrollAxis(double offset, double minimum, double maximum, double viewportLength, double smallChange, double largeChange)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum < minimum || !double.IsFinite(maximum - minimum))
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (!double.IsFinite(offset) || offset < minimum || offset > maximum) throw new ArgumentOutOfRangeException(nameof(offset));
        if (!double.IsFinite(viewportLength) || viewportLength < 0 || !double.IsFinite(viewportLength + maximum - minimum))
            throw new ArgumentOutOfRangeException(nameof(viewportLength));
        if (!double.IsFinite(smallChange) || smallChange < 0) throw new ArgumentOutOfRangeException(nameof(smallChange));
        if (!double.IsFinite(largeChange) || largeChange < 0) throw new ArgumentOutOfRangeException(nameof(largeChange));
        Offset = offset; Minimum = minimum; Maximum = maximum; ViewportLength = viewportLength;
        SmallChange = smallChange; LargeChange = largeChange;
    }
    /// <summary>Gets the current logical offset.</summary>
    public double Offset { get; }
    /// <summary>Gets the inclusive minimum logical offset.</summary>
    public double Minimum { get; }
    /// <summary>Gets the inclusive maximum logical offset.</summary>
    public double Maximum { get; }
    /// <summary>Gets the actual visible length in logical units.</summary>
    public double ViewportLength { get; }
    /// <summary>Gets the small logical movement.</summary>
    public double SmallChange { get; }
    /// <summary>Gets the page logical movement.</summary>
    public double LargeChange { get; }
    /// <summary>Gets geometric scrollability, independently of the control's enabled state.</summary>
    public bool IsScrollable => Maximum > Minimum && ViewportLength > 0;
    /// <summary>Gets normalized position from zero to 100, or null for an unavailable axis.</summary>
    public double? Percent => IsScrollable ? (Offset - Minimum) / (Maximum - Minimum) * 100 : null;
    /// <summary>Gets the visible percentage of the total extent, or 100 for an unavailable axis.</summary>
    public double ViewPercent => IsScrollable ? ViewportLength / (ViewportLength + Maximum - Minimum) * 100 : 100;
}

/// <summary>Contains immutable viewport geometry in the same coordinate reference as its peer's Bounds.</summary>
/// <param name="Horizontal">Horizontal logical metrics.</param>
/// <param name="Vertical">Vertical logical metrics.</param>
/// <param name="ViewportBounds">Visible viewport rectangle in canonical peer coordinates.</param>
public readonly record struct AccessibleScrollInfo(AccessibleScrollAxis Horizontal, AccessibleScrollAxis Vertical, Rectangle ViewportBounds);

public partial class AccessibleObject
{
    /// <summary>Gets optional current viewport metadata. Read on the owning UI thread; does not scroll or focus.</summary>
    /// <remarks>Disabled viewports can report geometry while rejecting actions. Sensitive peers must not expose content extent.</remarks>
    public virtual AccessibleScrollInfo? ScrollInfo => null;
}
