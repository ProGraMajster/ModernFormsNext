using System;

namespace ModernFormsNext;

/// <summary>
/// Reports the result of copying or resolving a local Markdown image asset.
/// </summary>
public sealed class MarkdownImageAssetResult
{
    internal MarkdownImageAssetResult(
        MarkdownImageAssetStatus status,
        string? destinationPath = null,
        string? markdownSource = null,
        string? errorMessage = null,
        Exception? exception = null)
    {
        Status = status;
        DestinationPath = destinationPath;
        MarkdownSource = markdownSource;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    /// <summary>Gets the destination file path, when one was selected.</summary>
    public string? DestinationPath { get; }

    /// <summary>Gets a human-readable error message when <see cref="Status"/> is failed.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Gets the underlying operational exception, when available.</summary>
    public Exception? Exception { get; }

    /// <summary>Gets a value indicating whether a usable Markdown source was produced.</summary>
    public bool IsSuccess => Status is MarkdownImageAssetStatus.Copied or MarkdownImageAssetStatus.UsedExisting;

    /// <summary>Gets the normalized Markdown image source, when successful.</summary>
    public string? MarkdownSource { get; }

    /// <summary>Gets the operation status.</summary>
    public MarkdownImageAssetStatus Status { get; }
}
