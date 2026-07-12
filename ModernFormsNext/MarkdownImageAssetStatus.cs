namespace ModernFormsNext;

/// <summary>
/// Describes the outcome of a Markdown image asset operation.
/// </summary>
public enum MarkdownImageAssetStatus
{
    /// <summary>The source was copied into the destination directory.</summary>
    Copied,

    /// <summary>An existing destination file was retained.</summary>
    UsedExisting,

    /// <summary>The operation was cancelled by collision policy.</summary>
    Cancelled,

    /// <summary>The operation failed validation or file I/O.</summary>
    Failed
}
