namespace ModernFormsNext.VisualStudioExtension;

/// <summary>
/// Specifies how the out-of-process ModernFormsNext Designer window is presented by Visual Studio.
/// </summary>
public enum DesignerHostingMode
{
    /// <summary>
    /// Embeds the Designer window as a child of the Visual Studio document pane.
    /// </summary>
    Integrated,

    /// <summary>
    /// Presents the Designer as an independent top-level window while retaining Visual Studio
    /// document ownership and the shared Designer session and persistence pipeline.
    /// </summary>
    Standalone
}
