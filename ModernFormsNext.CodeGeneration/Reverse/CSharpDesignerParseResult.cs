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
        Success = document is not null && diagnostics.Count == 0;
    }

    /// <summary>
    /// Gets whether the entire supported document was parsed without omissions or diagnostics.
    /// </summary>
    public bool Success { get; }

    /// <summary>Gets whether the document is an incomplete projection available for diagnostics only.</summary>
    public bool IsPartial => Document is not null && !Success
        && Diagnostics.All(diagnostic => diagnostic.Severity != CSharpDesignerDiagnosticSeverity.Error);

    /// <summary>
    /// Gets the parsed projection, or <see langword="null"/> when no model was produced.
    /// Check <see cref="Success"/> before applying the projection to a design.
    /// </summary>
    public DesignDocument? Document { get; }

    /// <summary>
    /// Gets diagnostics produced during parsing.
    /// </summary>
    public IReadOnlyList<CSharpDesignerDiagnostic> Diagnostics { get; }
}
