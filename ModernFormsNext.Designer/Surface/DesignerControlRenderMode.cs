namespace ModernFormsNext.Designer.Surface;

/// <summary>
/// Specifies how controls are drawn on the designer surface.
/// </summary>
public enum DesignerControlRenderMode
{
    /// <summary>
    /// Controls are rendered through the ModernFormsNext runtime renderers when available.
    /// </summary>
    Runtime,

    /// <summary>
    /// Controls are rendered as deterministic designer placeholders.
    /// </summary>
    Placeholder
}
