using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Documents;

/// <summary>
/// Represents an inline reference to a footnote.
/// </summary>
/// <remarks>
/// The reference stores display metadata only. It does not own the footnote content; footnote
/// content is represented by <see cref="FootnoteGroupBlock"/> elsewhere in the document.
/// </remarks>
public sealed class FootnoteReferenceInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="FootnoteReferenceInline"/> instance.
    /// </summary>
    /// <param name="order">The one-based display order of the footnote.</param>
    /// <param name="label">The source label or identifier of the footnote.</param>
    public FootnoteReferenceInline(int order, string? label = null)
    {
        Order = Math.Max(1, order);
        Label = label ?? string.Empty;
    }

    /// <summary>
    /// Gets the source label or identifier of the footnote.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the one-based display order of the footnote.
    /// </summary>
    public int Order { get; }
}

/// <summary>
/// Represents the block-level footnote section of a document.
/// </summary>
/// <remarks>
/// <see cref="FootnoteGroupBlock"/> keeps footnotes grouped so viewers can render them after the
/// main content using a consistent native layout.
/// </remarks>
public sealed class FootnoteGroupBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="FootnoteGroupBlock"/> instance.
    /// </summary>
    /// <param name="footnotes">The footnotes in display order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="footnotes"/> is <see langword="null"/>.</exception>
    public FootnoteGroupBlock(IEnumerable<DocumentFootnote> footnotes)
    {
        ArgumentNullException.ThrowIfNull(footnotes);
        Footnotes = footnotes.ToArray();
    }

    /// <summary>
    /// Gets the footnotes in display order.
    /// </summary>
    public IReadOnlyList<DocumentFootnote> Footnotes { get; }
}

/// <summary>
/// Represents a single footnote entry.
/// </summary>
public sealed class DocumentFootnote
{
    /// <summary>
    /// Initializes a new <see cref="DocumentFootnote"/> instance.
    /// </summary>
    /// <param name="order">The one-based display order of the footnote.</param>
    /// <param name="label">The source label or identifier of the footnote.</param>
    /// <param name="blocks">The blocks displayed as the footnote body.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is <see langword="null"/>.</exception>
    public DocumentFootnote(int order, string? label, IEnumerable<DocumentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        Order = Math.Max(1, order);
        Label = label ?? string.Empty;
        Blocks = blocks.ToArray();
    }

    /// <summary>
    /// Gets the blocks displayed as the footnote body.
    /// </summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    /// <summary>
    /// Gets the source label or identifier of the footnote.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the one-based display order of the footnote.
    /// </summary>
    public int Order { get; }
}
