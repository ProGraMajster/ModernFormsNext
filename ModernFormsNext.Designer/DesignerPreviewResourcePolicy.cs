namespace ModernFormsNext.Designer;

/// <summary>Configures the ordinary table-layout budget for inert Designer previews.</summary>
/// <remarks>
/// This immutable host setting is never read from a .mfdesign document. It does not constrain
/// runtime TableLayoutPanel, document persistence or generated C#. Zero disables an ordinary
/// limit for trusted projects; the independent hard safety ceilings always remain in force.
/// Exceeding either budget produces a lazy diagnostic placeholder without creating a runtime
/// table. Bounds are conservative: authored positions, spans and possible automatic growth
/// count even when they would eventually be clipped or hidden. Existing node, depth, property,
/// text and bitmap safety limits remain independent of this policy.
/// </remarks>
/// <example>
/// <code>
/// var options = new ModernFormsDesignerOptions
/// {
///     PreviewResourcePolicy = new DesignerPreviewResourcePolicy
///     {
///         MaximumTableStrips = 1024,
///         MaximumTableCells = 0
///     }
/// };
/// </code>
/// </example>
public sealed class DesignerPreviewResourcePolicy
{
    private int maximumTableStrips = 256;
    private int maximumTableCells = 65_536;

    /// <summary>Gets the immutable default preview policy.</summary>
    public static DesignerPreviewResourcePolicy Default { get; } = new();

    /// <summary>Gets the non-configurable ceiling for the conservative extent of either table axis.</summary>
    public const int HardMaximumTableStrips = 4096;

    /// <summary>Gets the non-configurable combined cell-footprint ceiling for one preview projection.</summary>
    public const int HardMaximumTableCells = 262_144;

    /// <summary>Gets the non-configurable conservative table placement-work ceiling per projection.</summary>
    /// <remarks>This is an admission estimate, not elapsed time or an exact allocation count.</remarks>
    public const long HardMaximumTableLayoutWork = 16 * 1024 * 1024;

    /// <summary>Gets or initializes the ordinary per-axis strip limit; zero disables this ordinary limit.</summary>
    /// <remarks>Defaults to 256. A larger value never raises the independent hard safety ceilings.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaximumTableStrips
    {
        get => maximumTableStrips;
        init { ArgumentOutOfRangeException.ThrowIfNegative(value); maximumTableStrips = value; }
    }

    /// <summary>Gets or initializes the ordinary combined cell-footprint limit; zero disables this ordinary limit.</summary>
    /// <remarks>Defaults to 65,536. Nested tables share this budget within one projection.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaximumTableCells
    {
        get => maximumTableCells;
        init { ArgumentOutOfRangeException.ThrowIfNegative(value); maximumTableCells = value; }
    }
}
