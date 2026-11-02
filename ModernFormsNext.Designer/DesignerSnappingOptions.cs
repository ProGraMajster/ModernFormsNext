namespace ModernFormsNext.Designer;

/// <summary>Configures transient alignment guides and the design-surface grid.</summary>
/// <remarks>
/// This immutable host preference is not part of .mfdesign, generated code, runtime layout,
/// document dirty state or undo history. Configure a shell before construction, or replace
/// DesignerSession.SnappingOptions on the owning UI thread. Alt temporarily bypasses snapping;
/// AltGraph does not. Guide acquisition and release use logical surface pixels, independently
/// of the monitor DPI and document zoom. Grid spacing uses logical document pixels.
/// </remarks>
/// <example><code>
/// var options = new ModernFormsDesignerOptions
/// {
///     SnappingOptions = new DesignerSnappingOptions { ShowGrid = true, GridSize = 12, SnapToGrid = true }
/// };
/// </code></example>
public sealed class DesignerSnappingOptions
{
    private int gridSize = 8;

    /// <summary>Gets the immutable default: guides enabled, visible eight-pixel grid, grid snapping off.</summary>
    public static DesignerSnappingOptions Default { get; } = new();

    /// <summary>Gets or initializes whether pointer movement and resizing use snapping.</summary>
    /// <remarks>Defaults to true. False permits unrestricted pointer motion while retaining constraints and transactions.</remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets or initializes whether the surface shows its grid, independently of snapping.</summary>
    /// <remarks>Defaults to true. Very dense grids are drawn at a coarser multiple to bound painting work.</remarks>
    public bool ShowGrid { get; init; } = true;

    /// <summary>Gets or initializes whether enabled snapping also rounds unmatched axes to the grid.</summary>
    /// <remarks>Defaults to false. Nearby smart guides take precedence. Alt bypasses both kinds of snapping.</remarks>
    public bool SnapToGrid { get; init; }

    /// <summary>Gets or initializes the square grid spacing in logical document pixels.</summary>
    /// <remarks>Defaults to eight. The accepted range is one through 4096; rendered density does not alter snap spacing.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside one through 4096.</exception>
    public int GridSize
    {
        get => gridSize;
        init
        {
            if (value is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(value), "Grid spacing must be between 1 and 4096 logical pixels.");
            gridSize = value;
        }
    }
}
