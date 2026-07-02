using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Generates sibling <c>.Designer.cs</c> files for <c>.mfdesign</c> documents.
/// </summary>
/// <remarks>
/// This class delegates all C# generation to <see cref="CSharpDesignerGenerator"/> so the
/// Visual Studio extension never contains its own designer-code generator.
/// </remarks>
public sealed class MfDesignFileGenerator
{
    private readonly CSharpDesignerRoundTripService roundTrip = new();

    /// <summary>
    /// Generates C# designer code for the specified document.
    /// </summary>
    /// <param name="document">The design document to generate from.</param>
    /// <returns>The generated code and validation result.</returns>
    public CSharpDesignerGenerationResult Generate(DesignDocument document)
        => roundTrip.Generate(document);

    /// <summary>
    /// Generates C# designer code for the specified document and source path.
    /// </summary>
    /// <param name="document">The design document to generate from.</param>
    /// <param name="designDocumentPath">The source <c>.mfdesign</c> path.</param>
    /// <returns>The generated code and validation result.</returns>
    public CSharpDesignerGenerationResult Generate(DesignDocument document, string designDocumentPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(designDocumentPath);

        return roundTrip.Generate(
            document,
            new CSharpDesignerGenerationOptions
            {
                SourceFilePath = Path.GetFileName(designDocumentPath),
                DesignHash = roundTrip.ComputeDesignHash(document)
            });
    }

    /// <summary>
    /// Gets the conventional sibling generated-code path for a design document path.
    /// </summary>
    /// <param name="designDocumentPath">The <c>.mfdesign</c> path.</param>
    /// <returns>The sibling <c>.Designer.cs</c> path.</returns>
    public string GetGeneratedCodePath(string designDocumentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designDocumentPath);

        var directory = Path.GetDirectoryName(designDocumentPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(designDocumentPath);
        return Path.Combine(directory, $"{name}.Designer.cs");
    }
}
