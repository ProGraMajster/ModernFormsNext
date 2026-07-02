using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.CSharp;

/// <summary>
/// Contains generated C# designer code and validation information.
/// </summary>
public sealed class CSharpDesignerGenerationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpDesignerGenerationResult"/> class.
    /// </summary>
    /// <param name="code">The generated C# code, or an empty string when validation failed.</param>
    /// <param name="validation">The validation result used during generation.</param>
    public CSharpDesignerGenerationResult(string code, DesignDocumentValidationResult validation)
    {
        Code = code;
        Validation = validation;
    }

    /// <summary>
    /// Gets the generated C# code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the validation result for the source document.
    /// </summary>
    public DesignDocumentValidationResult Validation { get; }

    /// <summary>
    /// Gets a value indicating whether generation completed without validation errors.
    /// </summary>
    public bool Succeeded => Validation.IsValid;
}
