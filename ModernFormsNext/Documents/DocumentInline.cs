using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Documents;

/// <summary>
/// Represents inline content inside a text-bearing document block.
/// </summary>
/// <remarks>
/// Inline elements affect text layout and rendering within paragraphs, headings, list items, and
/// similar blocks. They do not create vertical spacing by themselves.
/// </remarks>
public abstract class DocumentInline
{
    private protected DocumentInline()
    {
    }
}

/// <summary>
/// Represents plain inline text.
/// </summary>
public sealed class TextInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="TextInline"/> instance.
    /// </summary>
    /// <param name="text">The text to render.</param>
    public TextInline(string? text)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>
    /// Gets the text to render.
    /// </summary>
    public string Text { get; }
}

/// <summary>
/// Represents strongly emphasized inline content, commonly rendered as bold text.
/// </summary>
public sealed class StrongInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="StrongInline"/> instance.
    /// </summary>
    /// <param name="inlines">The nested inline content.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inlines"/> is <see langword="null"/>.</exception>
    public StrongInline(IEnumerable<DocumentInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);
        Inlines = inlines.ToArray();
    }

    /// <summary>
    /// Gets the nested inline content.
    /// </summary>
    public IReadOnlyList<DocumentInline> Inlines { get; }
}

/// <summary>
/// Represents emphasized inline content, commonly rendered as italic text.
/// </summary>
public sealed class EmphasisInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="EmphasisInline"/> instance.
    /// </summary>
    /// <param name="inlines">The nested inline content.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inlines"/> is <see langword="null"/>.</exception>
    public EmphasisInline(IEnumerable<DocumentInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);
        Inlines = inlines.ToArray();
    }

    /// <summary>
    /// Gets the nested inline content.
    /// </summary>
    public IReadOnlyList<DocumentInline> Inlines { get; }
}

/// <summary>
/// Represents struck-through inline content.
/// </summary>
public sealed class StrikethroughInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="StrikethroughInline"/> instance.
    /// </summary>
    /// <param name="inlines">The nested inline content.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inlines"/> is <see langword="null"/>.</exception>
    public StrikethroughInline(IEnumerable<DocumentInline> inlines)
    {
        ArgumentNullException.ThrowIfNull(inlines);
        Inlines = inlines.ToArray();
    }

    /// <summary>
    /// Gets the nested inline content.
    /// </summary>
    public IReadOnlyList<DocumentInline> Inlines { get; }
}

/// <summary>
/// Represents inline code text.
/// </summary>
public sealed class CodeInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="CodeInline"/> instance.
    /// </summary>
    /// <param name="text">The code text.</param>
    public CodeInline(string? text)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>
    /// Gets the code text.
    /// </summary>
    public string Text { get; }
}

/// <summary>
/// Represents a clickable inline link.
/// </summary>
public sealed class LinkInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="LinkInline"/> instance.
    /// </summary>
    /// <param name="destination">The link destination, such as a URL or document anchor.</param>
    /// <param name="inlines">The visible inline content for the link.</param>
    /// <param name="title">An optional link title.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inlines"/> is <see langword="null"/>.</exception>
    public LinkInline(string? destination, IEnumerable<DocumentInline> inlines, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(inlines);
        Destination = destination ?? string.Empty;
        Inlines = inlines.ToArray();
        Title = title;
    }

    /// <summary>
    /// Gets the link destination, such as a URL or document anchor.
    /// </summary>
    public string Destination { get; }

    /// <summary>
    /// Gets the visible inline content for the link.
    /// </summary>
    public IReadOnlyList<DocumentInline> Inlines { get; }

    /// <summary>
    /// Gets an optional link title.
    /// </summary>
    public string? Title { get; }
}

/// <summary>
/// Represents an inline image reference in a document.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ImageInline"/> stores the image source and accessible fallback text without tying
/// the document model to any input format. The current viewer renders mixed inline images as
/// fallback text because the text layout layer does not yet expose true object-run flow.
/// </para>
/// <para>
/// Use <see cref="ImageBlock"/> when an image should participate as a block-level visual resource.
/// <see cref="MarkdownParser"/> converts standalone Markdown images to <see cref="ImageBlock"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var document = new Document(new DocumentBlock[]
/// {
///     new ParagraphBlock(new DocumentInline[]
///     {
///         new ImageInline("Images/logo.png", "ModernFormsNext logo", "Logo")
///     })
/// });
/// </code>
/// </example>
public sealed class ImageInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="ImageInline"/> instance.
    /// </summary>
    /// <param name="source">The image source, such as an HTTP URL, file URI, relative file path, or data URI.</param>
    /// <param name="altText">The fallback text displayed when the image is unavailable.</param>
    /// <param name="title">An optional image title.</param>
    public ImageInline(string? source, string? altText = null, string? title = null)
    {
        Source = source ?? string.Empty;
        AltText = altText ?? string.Empty;
        Title = title;
    }

    /// <summary>
    /// Gets the image source, such as an HTTP URL, file URI, relative file path, or data URI.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets fallback text for the image.
    /// </summary>
    /// <remarks>
    /// Renderers should use this value when the image is loading, fails to load, or when a
    /// non-visual representation of the document is required.
    /// </remarks>
    public string AltText { get; }

    /// <summary>
    /// Gets an optional image title.
    /// </summary>
    public string? Title { get; }
}

/// <summary>
/// Represents a line break inside inline content.
/// </summary>
public sealed class LineBreakInline : DocumentInline
{
    /// <summary>
    /// Initializes a new <see cref="LineBreakInline"/> instance.
    /// </summary>
    /// <param name="hard">A value indicating whether the break is a hard line break.</param>
    public LineBreakInline(bool hard = true)
    {
        Hard = hard;
    }

    /// <summary>
    /// Gets a value indicating whether the break is a hard line break.
    /// </summary>
    /// <remarks>
    /// Hard breaks are rendered as line breaks. Soft breaks may be collapsed to spaces by
    /// renderers that follow normal paragraph wrapping behavior.
    /// </remarks>
    public bool Hard { get; }
}
