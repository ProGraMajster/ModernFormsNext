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
        : this(severity, message, line, column, syntax, null, null)
    {
    }

    /// <summary>Initializes a diagnostic with an optional one-based, end-exclusive source span.</summary>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="line">The optional starting line.</param>
    /// <param name="column">The optional starting column.</param>
    /// <param name="syntax">Optional unsupported syntax text.</param>
    /// <param name="endLine">The optional line at the end of the span.</param>
    /// <param name="endColumn">The optional exclusive column at the end of the span.</param>
    public CSharpDesignerDiagnostic(CSharpDesignerDiagnosticSeverity severity, string message,
        int? line, int? column, string? syntax, int? endLine, int? endColumn)
    {
        Severity = severity;
        Message = message;
        Line = line;
        Column = column;
        Syntax = syntax;
        EndLine = endLine;
        EndColumn = endColumn;
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

    /// <summary>Gets the optional one-based line at the end of the source span.</summary>
    public int? EndLine { get; }

    /// <summary>Gets the optional one-based, exclusive column at the end of the source span.</summary>
    public int? EndColumn { get; }
}
