namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Describes a diagnostic produced while parsing generated C# designer code.
/// </summary>
public sealed class CSharpDesignerDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpDesignerDiagnostic"/> class.
    /// </summary>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="line">The optional one-based source line.</param>
    /// <param name="column">The optional one-based source column.</param>
    /// <param name="syntax">Optional unsupported syntax text.</param>
    public CSharpDesignerDiagnostic(
        CSharpDesignerDiagnosticSeverity severity,
        string message,
        int? line = null,
        int? column = null,
        string? syntax = null)
    {
        Severity = severity;
        Message = message;
        Line = line;
        Column = column;
        Syntax = syntax;
    }

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public CSharpDesignerDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional one-based source line.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Gets the optional one-based source column.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// Gets the optional unsupported syntax text.
    /// </summary>
    public string? Syntax { get; }
}
