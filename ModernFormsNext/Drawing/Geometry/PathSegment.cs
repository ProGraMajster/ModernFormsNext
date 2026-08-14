using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Defines one mutable segment in a <see cref="PathFigure"/>.
/// </summary>
/// <remarks>
/// Segments may be shared by multiple figures. A rendered mutation raises <see cref="Changed"/>
/// so every owning figure and geometry invalidates its cache.
/// </remarks>
public abstract class PathSegment
{
    /// <summary>Occurs after a segment property changes its rendered path.</summary>
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> after a rendered property changes.</summary>
    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    internal static void ValidatePoint(PointF point, string parameterName)
        => Geometry.ValidatePoint(point, parameterName);
}
