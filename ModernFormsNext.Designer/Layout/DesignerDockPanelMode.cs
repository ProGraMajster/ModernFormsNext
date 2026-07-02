namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Describes how optional tool panels are placed inside <see cref="ModernFormsDesignerShell"/>.
/// </summary>
public enum DesignerDockPanelMode
{
    /// <summary>
    /// The panel is visible in the right tool area above the property grid.
    /// </summary>
    RightSplit,

    /// <summary>
    /// The panel shares the right tool area with the property grid and is selected
    /// through a lightweight tab strip.
    /// </summary>
    RightTabbed,

    /// <summary>
    /// The panel is collapsed to an auto-hide strip.
    /// </summary>
    AutoHide
}
