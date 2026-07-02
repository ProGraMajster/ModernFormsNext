using ModernFormsNext.Designer;

namespace ModernFormsNext.VisualStudioExtension.Hosting;

/// <summary>
/// Adapts Visual Studio document services to the reusable designer host environment contract.
/// </summary>
/// <remarks>
/// This skeleton currently stores messages in memory. The VSSDK implementation should forward
/// status messages to the Visual Studio status bar and output messages to an output pane.
/// </remarks>
public sealed class VisualStudioDesignerHost : IDesignerHostEnvironment
{
    private readonly List<string> outputLines = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualStudioDesignerHost"/> class.
    /// </summary>
    /// <param name="currentDocumentPath">The path of the open <c>.mfdesign</c> document.</param>
    /// <param name="primaryCodeFilePath">The primary user-authored C# file path.</param>
    /// <param name="currentProjectPath">The project file or project directory that owns the design document.</param>
    public VisualStudioDesignerHost(
        string? currentDocumentPath,
        string? primaryCodeFilePath = null,
        string? currentProjectPath = null)
    {
        CurrentDocumentPath = currentDocumentPath;
        PrimaryCodeFilePath = primaryCodeFilePath;
        CurrentProjectPath = currentProjectPath;
    }

    /// <inheritdoc/>
    public string? CurrentDocumentPath { get; }

    /// <inheritdoc/>
    public string? CurrentProjectPath { get; }

    /// <summary>
    /// Gets the primary user-authored C# file associated with the design document.
    /// </summary>
    public string? PrimaryCodeFilePath { get; }

    /// <summary>
    /// Gets the last status message reported by the designer.
    /// </summary>
    public string LastStatusMessage { get; private set; } = "Ready";

    /// <summary>
    /// Gets output messages reported by the designer.
    /// </summary>
    public IReadOnlyList<string> OutputLines => outputLines;

    /// <inheritdoc/>
    public void ReportStatus(string message)
    {
        LastStatusMessage = message;
    }

    /// <inheritdoc/>
    public void ReportOutput(string message)
    {
        outputLines.Add(message);
    }
}
