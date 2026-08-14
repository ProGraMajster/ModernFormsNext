using System.ComponentModel;
using ModernFormsNext.Drawing;

namespace ModernFormsNext;

/// <summary>Displays a reusable platform-neutral <see cref="Geometry"/>.</summary>
/// <remarks>
/// The same Geometry may be assigned to multiple Path controls. Every control observes changes
/// through a weak subscription, invalidates independently, and owns its own disposable native-path
/// cache. Assign null to render and hit-test nothing.
/// </remarks>
/// <example>
/// <code>
/// var geometry = new PathGeometry();
/// var figure = new PathFigure(new PointF(10, 90), isClosed: true);
/// figure.Segments.Add(new LineSegment(new PointF(50, 10)));
/// figure.Segments.Add(new LineSegment(new PointF(90, 90)));
/// geometry.Figures.Add(figure);
///
/// var path = new ModernFormsNext.Path
/// {
///     Data = geometry,
///     Fill = new SolidColorBrush(Color.Gold)
/// };
/// </code>
/// </example>
[DisplayName("Path")]
[Category("Shapes")]
[Description("Draws reusable platform-neutral vector Geometry.")]
public sealed class Path : Shape
{
    /// <summary>Initializes an empty Path.</summary>
    public Path()
    {
    }

    /// <summary>Gets or sets the reusable geometry rendered by this control.</summary>
    [Category("Geometry")]
    [Description("The reusable platform-neutral Geometry rendered by this Path.")]
    [DefaultValue(null)]
    public Geometry? Data
    {
        get => DefiningGeometry;
        set => SetDefiningGeometry(value);
    }
}
