using ModernFormsNext.Documents;
using MfnDocument = ModernFormsNext.Documents.Document;

namespace ModernFormsNext;

/// <summary>
/// Displays Markdown source by converting it into the ModernFormsNext document model and rendering
/// it through <see cref="DocumentViewer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MarkdownViewer"/> does not contain a separate Markdown renderer. Assigning
/// <see cref="Markdown"/> parses the source with <see cref="MarkdownParser"/>, converts it into a
/// <see cref="Documents.Document"/>, and then uses the normal document layout and SkiaSharp
/// renderer inherited from <see cref="DocumentViewer"/>.
/// </para>
/// <para>
/// <see langword="null"/> and empty Markdown source are treated as an empty document. Links raise
/// <see cref="DocumentViewer.LinkClicked"/> and are not opened automatically.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var viewer = new MarkdownViewer
/// {
///     Markdown = """
///     # ModernFormsNext
///
///     **Multiplatform UI framework**
///
///     - Windows
///     - Android
///     - SkiaSharp
///
///     Visit [GitHub](https://github.com/ProGraMajster/ModernFormsNext).
///     """
/// };
/// </code>
/// </example>
public class MarkdownViewer : DocumentViewer
{
    private readonly MarkdownParser parser = new();
    private string markdown = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="MarkdownViewer"/> instance.
    /// </summary>
    public MarkdownViewer()
    {
        Document = MfnDocument.Empty;
    }

    /// <summary>
    /// Gets or sets the Markdown source displayed by the viewer.
    /// </summary>
    /// <remarks>
    /// Assigning this property reparses the Markdown into a new <see cref="Documents.Document"/>
    /// and invalidates the shared document layout. The property accepts <see langword="null"/>
    /// from nullable-oblivious callers and treats it the same as <see cref="string.Empty"/>.
    /// </remarks>
    public string Markdown
    {
        get => markdown;
        set
        {
            value ??= string.Empty;

            if (markdown == value)
                return;

            markdown = value;
            Document = parser.Parse(markdown);
        }
    }
}
