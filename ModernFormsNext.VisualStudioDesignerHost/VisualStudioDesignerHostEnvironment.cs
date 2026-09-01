using ModernFormsNext.Designer;

namespace ModernFormsNext.VisualStudioDesignerHost;

/// <summary>
/// Provides file, status, and output integration for the standalone designer host launched by Visual Studio.
/// </summary>
public sealed class VisualStudioDesignerHostEnvironment :
    IDesignerHostEnvironment,
    IDesignerDiagnosticHostEnvironment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisualStudioDesignerHostEnvironment"/> class.
    /// </summary>
    /// <param name="currentDocumentPath">The active <c>.mfdesign</c> document path.</param>
    /// <param name="currentProjectPath">The project path supplied by Visual Studio, if known.</param>
    public VisualStudioDesignerHostEnvironment(string? currentDocumentPath, string? currentProjectPath = null)
    {
        CurrentDocumentPath = currentDocumentPath;
        CurrentProjectPath = currentProjectPath;
    }

    /// <inheritdoc/>
    public string? CurrentDocumentPath { get; private set; }

    /// <inheritdoc/>
    public string? CurrentProjectPath { get; private set; }

    /// <summary>
    /// Updates the active Visual Studio document context used by designer services.
    /// </summary>
    /// <param name="currentDocumentPath">The active <c>.mfdesign</c> document path.</param>
    /// <param name="currentProjectPath">The active project path, if known.</param>
    public void UpdateContext(string? currentDocumentPath, string? currentProjectPath)
    {
        CurrentDocumentPath = currentDocumentPath;
        CurrentProjectPath = currentProjectPath;
    }

    /// <inheritdoc/>
    public void ReportStatus(string message)
    {
        DesignerHostDiagnosticLog.Write($"DESIGNER_STATUS {message}");
        Console.WriteLine(message);
    }

    /// <inheritdoc/>
    public void ReportOutput(string message)
    {
        DesignerHostDiagnosticLog.Write($"DESIGNER_OUTPUT {message}");
        Console.WriteLine(message);
    }

    void IDesignerDiagnosticHostEnvironment.ReportDiagnostic(string message)
        => DesignerHostDiagnosticLog.Write(message);
}
