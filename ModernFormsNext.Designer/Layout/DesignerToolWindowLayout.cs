namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Stores layout preferences for a designer tool window.
/// </summary>
/// <remarks>
/// The layout model is intentionally host-serializable: a Visual Studio extension, standalone
/// playground, or future IDE integration can persist these values and feed them back into
/// <see cref="ModernFormsDesignerOptions"/>.
/// </remarks>
public sealed class DesignerToolWindowLayout
{
    /// <summary>
    /// Gets or sets the dock side used by the tool window.
    /// </summary>
    public DesignerToolWindowSide Side { get; set; }

    /// <summary>
    /// Gets or sets the display mode used by the tool window.
    /// </summary>
    public DesignerToolWindowMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the preferred size in logical pixels.
    /// </summary>
    /// <remarks>
    /// For left and right dock sides this is interpreted as width. For bottom dock side this
    /// is interpreted as height.
    /// </remarks>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the order of the tool window within its dock side.
    /// </summary>
    public int Order { get; set; }
}
