namespace ModernFormsNext;

/// <summary>
/// Specifies how <see cref="MarkdownImageAssetProcessor"/> handles an existing destination file.
/// </summary>
public enum MarkdownImageAssetCollisionBehavior
{
    /// <summary>
    /// Cancels the asset operation without changing the existing file.
    /// </summary>
    Cancel,

    /// <summary>
    /// Replaces the existing file after the new content has been copied to a temporary file.
    /// </summary>
    Overwrite,

    /// <summary>
    /// Generates a unique filename by appending a numeric suffix.
    /// </summary>
    GenerateUniqueName,

    /// <summary>
    /// Keeps the existing destination file and returns its Markdown reference.
    /// </summary>
    UseExisting
}
