namespace ModernFormsNext.Designer;

/// <summary>
/// Provides host-specific services used by the reusable ModernFormsNext designer shell.
/// </summary>
/// <remarks>
/// Standalone hosts can implement this interface to route designer status and output messages
/// to their own UI. The Visual Studio extension uses the same contract to connect the shared
/// designer shell to the Visual Studio document and output infrastructure.
/// </remarks>
public interface IDesignerHostEnvironment
{
    /// <summary>
    /// Gets the current designer document path, or <see langword="null"/> when the document
    /// has not been associated with a file yet.
    /// </summary>
    string? CurrentDocumentPath { get; }

    /// <summary>
    /// Gets the current project file path or project directory supplied by the host, or
    /// <see langword="null"/> when the designer is not attached to a project.
    /// </summary>
    /// <remarks>
    /// Visual Studio hosts should provide this value so shared designer panels such as the
    /// Solution Explorer can show the real project tree instead of guessing from the active
    /// design document location.
    /// </remarks>
    string? CurrentProjectPath { get; }

    /// <summary>
    /// Reports a short status message to the host.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    void ReportStatus(string message);

    /// <summary>
    /// Reports an output/log message to the host.
    /// </summary>
    /// <param name="message">The output message to append.</param>
    void ReportOutput(string message);
}

/// <summary>
/// Receives verbose Designer diagnostics without adding them to the user-facing output panel.
/// </summary>
/// <remarks>
/// This internal extension is implemented by diagnostic-aware hosts. The shared Designer still
/// writes its per-process diagnostic file when the host does not provide this optional sink.
/// </remarks>
internal interface IDesignerDiagnosticHostEnvironment
{
    /// <summary>
    /// Reports one diagnostic trace entry to the hosting process.
    /// </summary>
    /// <param name="message">The diagnostic message to record.</param>
    void ReportDiagnostic(string message);
}
