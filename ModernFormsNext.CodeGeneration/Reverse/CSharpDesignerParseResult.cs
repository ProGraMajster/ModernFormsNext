using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Contains the result of reverse parsing C# designer code into a design document.
/// </summary>
public sealed class CSharpDesignerParseResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpDesignerParseResult"/> class.
    /// </summary>
    /// <param name="document">The parsed document, or <see langword="null"/> when parsing failed.</param>
    /// <param name="diagnostics">Diagnostics produced during parsing.</param>
    public CSharpDesignerParseResult(
        DesignDocument? document,
        IReadOnlyList<CSharpDesignerDiagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
        Success = document is not null
            && !diagnostics.Any(diagnostic => diagnostic.Severity == CSharpDesignerDiagnosticSeverity.Error);
    }

    /// <summary>
    /// Gets a value indicating whether a usable design document was produced.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the parsed design document, or <see langword="null"/> when parsing failed.
    /// </summary>
    public DesignDocument? Document { get; }

    /// <summary>
    /// Gets diagnostics produced during parsing.
    /// </summary>
    public IReadOnlyList<CSharpDesignerDiagnostic> Diagnostics { get; }
}
