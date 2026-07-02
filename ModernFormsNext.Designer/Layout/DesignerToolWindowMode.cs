namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Describes how a designer tool window is displayed within its dock side.
/// </summary>
public enum DesignerToolWindowMode
{
    /// <summary>
    /// The tool window is visible as a split panel in its dock side.
    /// </summary>
    Docked,

    /// <summary>
    /// The tool window shares its dock side with other tabbed tool windows.
    /// </summary>
    Tabbed,

    /// <summary>
    /// The tool window is collapsed to a lightweight auto-hide strip.
    /// </summary>
    AutoHide,

    /// <summary>
    /// The tool window is not visible.
    /// </summary>
    Hidden
}
