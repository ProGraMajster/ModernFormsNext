namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents reusable vector contours composed from figures and line, Bézier or arc segments.
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

    /// <summary>Parses bounded SVG-compatible path data into an observable geometry.</summary>
    /// <param name="data">Invariant-culture M/L/H/V/C/S/Q/T/A/Z commands, in either case.</param>
    /// <returns>A new geometry with identity transform and non-zero fill rule.</returns>
    /// <remarks>
    /// Accepts path data only, not SVG XML, styles, resources or URLs. Relative coordinates, repeated
    /// parameter sets and smooth curves are supported. Negative arc radii are normalized to positive.
    /// Admission is limited to 262144 characters, 16384 segments and 1024 figures before native work.
    /// These fixed parsing ceilings also apply to trusted input; use typed geometry for larger data.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    /// <exception cref="FormatException">Syntax, finite numeric range or an admission ceiling is invalid.</exception>
    /// <example><code>var geometry = PathGeometry.Parse("M 10 40 Q 40 0 70 40 A 20 20 0 0 1 110 40 Z");</code></example>
    public static PathGeometry Parse(string data) => PathDataParser.Parse(data);

    /// <summary>Writes canonical invariant-culture absolute path data.</summary>
    /// <returns>Data containing M/L/Q/C/A/Z commands with round-trip float precision.</returns>
    /// <remarks>
    /// Fill rule and geometry transform are separate properties and are not encoded. The result may
    /// exceed <see cref="Parse"/>'s admission ceilings for a large geometry constructed with typed APIs.
    /// Does not execute application code, perform layout or invalidate the geometry.
    /// </remarks>
    /// <exception cref="NotSupportedException">A custom segment has no path-data representation.</exception>
    public string ToPathData() => PathDataParser.Write(this);

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
