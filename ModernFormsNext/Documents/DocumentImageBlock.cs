using System;

namespace ModernFormsNext.Documents;

/// <summary>
/// Represents a block-level image in a document.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ImageBlock"/> is independent of Markdown. It stores a resource source and fallback
/// text that a renderer can use while the image is loading or when the image cannot be decoded.
/// </para>
/// <para>
/// <see cref="DocumentViewer"/> loads block images asynchronously through its per-viewer image
/// cache and renders them with SkiaSharp. The image source is not loaded by the document model.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var document = new Document(new DocumentBlock[]
/// {
///     new ImageBlock("Images/logo.png", "Application logo", "Logo")
/// });
/// </code>
/// </example>
public sealed class ImageBlock : DocumentBlock
{
    /// <summary>
    /// Initializes a new <see cref="ImageBlock"/> instance.
    /// </summary>
    /// <param name="source">The image source, such as an HTTP URL, file URI, relative file path, or data URI.</param>
    /// <param name="altText">The fallback text displayed when the image is unavailable.</param>
    /// <param name="title">An optional image title.</param>
    public ImageBlock(string? source, string? altText = null, string? title = null)
    {
        Source = source ?? string.Empty;
        AltText = altText ?? string.Empty;
        Title = title;
    }

    /// <summary>
    /// Gets fallback text for the image.
    /// </summary>
    /// <remarks>
    /// Renderers should use this value while the image is loading, when loading fails, and when a
    /// non-visual representation of the document is requested.
    /// </remarks>
    public string AltText { get; }

    /// <summary>
    /// Gets the image source, such as an HTTP URL, file URI, relative file path, or data URI.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets an optional image title.
    /// </summary>
    public string? Title { get; }
}
