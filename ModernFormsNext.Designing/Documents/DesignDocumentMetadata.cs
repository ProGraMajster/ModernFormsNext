namespace ModernFormsNext.Designing;

/// <summary>
/// Stores versioning and tool information for a designer document.
/// </summary>
/// <remarks>
/// Metadata is kept separate from the visual tree so future designer tools can add
/// non-rendering information without changing the core model shape.
/// </remarks>
public sealed class DesignDocumentMetadata
{
    /// <summary>
    /// Gets or sets the designer document format version.
    /// </summary>
    public int FormatVersion { get; set; } = DesignDocumentSerializer.CurrentFormatVersion;

    /// <summary>
    /// Gets or sets the optional tool name that last wrote the document.
    /// </summary>
    public string? ToolName { get; set; }
}
