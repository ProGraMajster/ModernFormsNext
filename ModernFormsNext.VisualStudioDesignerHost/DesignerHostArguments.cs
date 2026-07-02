namespace ModernFormsNext.VisualStudioDesignerHost;

/// <summary>
/// Contains command-line arguments passed from the Visual Studio extension to the
/// out-of-process ModernFormsNext designer host.
/// </summary>
public sealed class DesignerHostArguments
{
    private DesignerHostArguments(string? designDocumentPath, string? projectPath)
    {
        DesignDocumentPath = designDocumentPath;
        ProjectPath = projectPath;
    }

    /// <summary>
    /// Gets the <c>.mfdesign</c> document path to load, or <see langword="null"/> when the
    /// host should start with a default sample document.
    /// </summary>
    public string? DesignDocumentPath { get; }

    /// <summary>
    /// Gets the owning project file or project directory supplied by Visual Studio, or
    /// <see langword="null"/> when the host should infer it from the design document.
    /// </summary>
    public string? ProjectPath { get; }

    /// <summary>
    /// Parses command-line arguments supplied by the VSIX launcher.
    /// </summary>
    /// <param name="args">The command-line argument array.</param>
    /// <returns>The parsed designer host arguments.</returns>
    public static DesignerHostArguments Parse(IReadOnlyList<string> args)
    {
        string? designFile = null;
        string? projectPath = null;

        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--design-file", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                designFile = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(args[index], "--project", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                projectPath = args[index + 1];
                index++;
            }
        }

        if (string.IsNullOrWhiteSpace(designFile)
            && args.Count > 0
            && !string.IsNullOrWhiteSpace(args[0])
            && !args[0].StartsWith("--", StringComparison.Ordinal))
        {
            designFile = args[0];
        }

        return new DesignerHostArguments(designFile, projectPath);
    }
}
