using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Documents;

/// <summary>
/// Represents a platform-neutral rich document composed of block elements.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Document"/> is the shared ModernFormsNext document model used by controls such as
/// <see cref="DocumentViewer"/> and <see cref="MarkdownViewer"/>. The model is intentionally not
/// tied to Markdown; Markdown is only one possible input format that can be converted into this
/// representation.
/// </para>
/// <para>
/// Assign a new instance to <see cref="DocumentViewer.Document"/> when the contents change. The
/// viewer invalidates its cached layout when the property is assigned. Mutating a document instance
/// after it has been assigned is not observed automatically by the document viewer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var document = new Document(new DocumentBlock[]
/// {
///     new HeadingBlock(1, new DocumentInline[] { new TextInline("ModernFormsNext") }),
///     new ParagraphBlock(new DocumentInline[] { new TextInline("Code-first UI documents.") })
/// });
///
/// var viewer = new DocumentViewer
/// {
///     Document = document
/// };
/// </code>
/// </example>
public sealed class Document
{
    /// <summary>
    /// Initializes a new empty <see cref="Document"/> instance.
    /// </summary>
    public Document()
        : this(Array.Empty<DocumentBlock>())
    {
    }

    /// <summary>
    /// Initializes a new <see cref="Document"/> instance with the specified blocks.
    /// </summary>
    /// <param name="blocks">The top-level blocks contained by the document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is <see langword="null"/>.</exception>
    public Document(IEnumerable<DocumentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        Blocks = blocks.ToArray();
    }

    /// <summary>
    /// Gets an empty document instance.
    /// </summary>
    public static Document Empty { get; } = new Document();

    /// <summary>
    /// Gets the top-level blocks in document order.
    /// </summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    /// <summary>
    /// Converts the document to a plain-text representation.
    /// </summary>
    /// <returns>A plain-text representation of the document contents.</returns>
    /// <remarks>
    /// The conversion preserves readable content for non-text elements such as list markers,
    /// table cells, footnote references, and image fallback text. It does not include Markdown or
    /// HTML markup and is intended for complete-document export. Use
    /// <see cref="DocumentViewer.SelectedText"/> for the viewer's current selection.
    /// </remarks>
    public string GetPlainText()
        => DocumentTextConverter.ToPlainText(this);
}
