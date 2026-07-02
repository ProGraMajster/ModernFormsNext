namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Identifies a reusable tool window hosted by the ModernFormsNext designer shell.
/// </summary>
/// <remarks>
/// Hosts can use these identifiers to persist layout choices in the same way Visual Studio
/// persists placement for toolbox, document outline, property, output, and solution panels.
/// </remarks>
public enum DesignerToolWindowId
{
    /// <summary>
    /// The toolbox panel that lists controls and components available for insertion.
    /// </summary>
    Toolbox,

    /// <summary>
    /// The document outline panel that shows the hierarchy of controls in the active document.
    /// </summary>
    DocumentOutline,

    /// <summary>
    /// The property grid panel for the selected form or control.
    /// </summary>
    Properties,

    /// <summary>
    /// The solution explorer panel that shows files from the active project.
    /// </summary>
    SolutionExplorer,

    /// <summary>
    /// The output panel that displays designer diagnostics and command log entries.
    /// </summary>
    Output
}
