using ModernFormsNext.Designing;
using ModernFormsNext.VisualStudioExtension.Editors;

namespace ModernFormsNext.VisualStudioExtension.Hosting;

/// <summary>
/// Coordinates save and generated-code output for Visual Studio-hosted designer documents.
/// </summary>
public sealed class VisualStudioDesignerFileService
{
    private readonly VisualStudioDesignerDocumentAdapter documentAdapter = new();
    private readonly MfDesignFileGenerator fileGenerator = new();

    /// <summary>
    /// Saves the design document and writes a sibling <c>.Designer.cs</c> file.
    /// </summary>
    /// <param name="designDocumentPath">The <c>.mfdesign</c> path.</param>
    /// <param name="document">The document to save and generate from.</param>
    /// <returns>The generated-code path.</returns>
    public string SaveAndGenerate(string designDocumentPath, DesignDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designDocumentPath);
        ArgumentNullException.ThrowIfNull(document);

        documentAdapter.Save(designDocumentPath, document);

        var generation = fileGenerator.Generate(document, designDocumentPath);

        if (!generation.Succeeded)
            throw new InvalidOperationException("Designer code generation failed: " + string.Join("; ", generation.Validation.Errors));

        var generatedCodePath = fileGenerator.GetGeneratedCodePath(designDocumentPath);
        File.WriteAllText(generatedCodePath, generation.Code);
        return generatedCodePath;
    }
}
