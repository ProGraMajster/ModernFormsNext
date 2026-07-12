namespace ModernFormsNext;

/// <summary>
/// Specifies which surfaces are displayed by a <see cref="MarkdownEditor"/>.
/// </summary>
public enum MarkdownEditorViewMode
{
    /// <summary>
    /// Displays only the editable Markdown source.
    /// </summary>
    Editor,

    /// <summary>
    /// Displays only the native <see cref="MarkdownViewer"/> preview.
    /// </summary>
    Preview,

    /// <summary>
    /// Displays the source editor and native preview side by side.
    /// </summary>
    Split
}
