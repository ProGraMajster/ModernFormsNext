using ModernFormsNext.Designing;
using ModernFormsNext.VisualStudioExtension.Editors;
using ModernFormsNext.Designer.Services;

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

        var generation = fileGenerator.Generate(
            document,
            designDocumentPath,
            DesignerProjectAnimationDefinitionDiscovery.Discover(FindProjectPath(designDocumentPath)));

        if (!generation.Succeeded)
            throw new InvalidOperationException("Designer code generation failed: " + string.Join("; ", generation.Validation.Errors));

        var generatedCodePath = fileGenerator.GetGeneratedCodePath(designDocumentPath);
        File.WriteAllText(generatedCodePath, generation.Code);
        return generatedCodePath;
    }

    private static string FindProjectPath(string designDocumentPath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(designDocumentPath));
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string? project = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (project is not null)
                return project;
            directory = Directory.GetParent(directory)?.FullName;
        }
        return Path.GetDirectoryName(Path.GetFullPath(designDocumentPath)) ?? string.Empty;
    }
}
