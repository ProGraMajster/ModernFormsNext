namespace ModernFormsNext.VisualStudioExtension.Detection;

/// <summary>
/// Resolves companion file paths for ModernFormsNext designable code files.
/// </summary>
public sealed class ModernFormsDesignFileLocator
{
    /// <summary>
    /// Gets the conventional design metadata path for a code file.
    /// </summary>
    /// <param name="codeFilePath">The primary <c>.cs</c> file path.</param>
    /// <returns>The sibling <c>.mfdesign</c> path.</returns>
    public string GetDesignFilePath(string codeFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeFilePath);

        return Path.ChangeExtension(codeFilePath, ".mfdesign");
    }

    /// <summary>
    /// Gets the conventional generated designer code path for a code file.
    /// </summary>
    /// <param name="codeFilePath">The primary <c>.cs</c> file path.</param>
    /// <returns>The sibling <c>.Designer.cs</c> path.</returns>
    public string GetDesignerCodePath(string codeFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeFilePath);

        var directory = Path.GetDirectoryName(codeFilePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(codeFilePath);
        return Path.Combine(directory, $"{fileName}.Designer.cs");
    }

    /// <summary>
    /// Gets the conventional primary code file path for a design metadata file.
    /// </summary>
    /// <param name="designFilePath">The <c>.mfdesign</c> file path.</param>
    /// <returns>The sibling primary <c>.cs</c> path.</returns>
    public string GetCodeFilePath(string designFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designFilePath);

        return Path.ChangeExtension(designFilePath, ".cs");
    }
}
