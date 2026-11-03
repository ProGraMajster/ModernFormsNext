using ModernFormsNext.Designer;

namespace ModernFormsNext.VisualStudioDesignerHost;

/// <summary>
/// Provides file, status, and output integration for the standalone designer host launched by Visual Studio.
/// </summary>
public sealed class VisualStudioDesignerHostEnvironment :
    IDesignerHostEnvironment,
    IDesignerCodeDocumentHostEnvironment,
    IDesignerDiagnosticHostEnvironment
{
    internal DesignerCodeRequestBroker? CodeRequests { get; set; }
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
        if (!string.Equals(CurrentDocumentPath, currentDocumentPath, StringComparison.OrdinalIgnoreCase)) CodeRequests?.CancelAll();
        CurrentDocumentPath = currentDocumentPath;
        CurrentProjectPath = currentProjectPath;
    }

    /// <inheritdoc/>
    public async Task<string?> ReadCodeDocumentAsync(string path, CancellationToken cancellationToken)
    {
        var result = CodeRequests is null || CurrentDocumentPath is null ? (false, string.Empty)
            : await CodeRequests.RequestAsync("READ", CurrentDocumentPath, path, string.Empty, 0, string.Empty, cancellationToken);
        return result.Item1 ? result.Item2 : null;
    }

    /// <inheritdoc/>
    public async Task<bool> InsertCodeAsync(string path, string expectedText, int insertionOffset,
        string insertedText, CancellationToken cancellationToken)
        => CodeRequests is not null && CurrentDocumentPath is not null
            && (await CodeRequests.RequestAsync("INSERT", CurrentDocumentPath, path, expectedText, insertionOffset, insertedText, cancellationToken)).Success;

    /// <inheritdoc/>
    public async Task<bool> NavigateToCodeAsync(string path, string expectedText, int offset, CancellationToken cancellationToken)
        => CodeRequests is not null && CurrentDocumentPath is not null
            && (await CodeRequests.RequestAsync("NAVIGATE", CurrentDocumentPath, path, expectedText, offset, string.Empty, cancellationToken)).Success;

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
