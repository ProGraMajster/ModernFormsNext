namespace ModernFormsNext;

/// <summary>
/// Identifies how an image source supplied to <see cref="MarkdownEditor"/> should be handled.
/// </summary>
public enum MarkdownImageSourceKind
{
    /// <summary>
    /// Inserts the source as a Markdown reference without copying it.
    /// </summary>
    /// <remarks>
    /// Use this value for HTTP or HTTPS URLs, data URIs, relative paths, or any other reference
    /// whose lifetime is managed by the host application.
    /// </remarks>
    Reference,

    /// <summary>
    /// Copies a local source file according to <see cref="MarkdownImageAssetOptions"/> and inserts
    /// the resulting relative reference.
    /// </summary>
    LocalFile
}
