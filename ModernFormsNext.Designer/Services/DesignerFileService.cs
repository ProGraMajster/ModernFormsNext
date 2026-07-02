using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

internal sealed class DesignerFileService
{
    private readonly IDesignerHostEnvironment? environment;
    private readonly Func<string?>? currentDocumentPathProvider;
    private readonly CSharpDesignerRoundTripService roundTrip = new();

    public DesignerFileService(IDesignerHostEnvironment? environment = null, Func<string?>? currentDocumentPathProvider = null)
    {
        this.environment = environment;
        this.currentDocumentPathProvider = currentDocumentPathProvider;
    }

    public DesignDocument LoadDesignDocument(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return DesignDocumentSerializer.Default.Load(path);
    }

    public string SaveDesignDocument(DesignDocument document)
    {
        var path = GetDesignDocumentPath(document);
        DesignDocumentSerializer.Default.Save(path, document);
        return path;
    }

    public DesignerGenerationFileResult GenerateDesignerCode(DesignDocument document)
    {
        var designDocumentPath = GetDesignDocumentPath(document);
        var result = roundTrip.Generate(
            document,
            new CSharpDesignerGenerationOptions
            {
                SourceFilePath = Path.GetFileName(designDocumentPath),
                DesignHash = roundTrip.ComputeDesignHash(document)
            });

        if (!result.Succeeded)
            return new DesignerGenerationFileResult(false, string.Empty, string.Empty, result.Validation.Errors);

        var path = GetGeneratedCodePath(document);
        File.WriteAllText(path, result.Code);
        return new DesignerGenerationFileResult(true, path, result.Code, Array.Empty<string>());
    }

    public CSharpDesignerParseResult ImportDesignerCode(
        string path,
        CSharpDesignerParseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var sourceText = File.ReadAllText(path);
        return roundTrip.ParseDesignerCode(sourceText, options);
    }

    private string GetDesignDocumentPath(DesignDocument document)
    {
        var activeDocumentPath = currentDocumentPathProvider?.Invoke();

        if (!string.IsNullOrWhiteSpace(activeDocumentPath))
            return DesignerDocumentPath.NormalizeDesignPath(activeDocumentPath)!;

        if (!string.IsNullOrWhiteSpace(environment?.CurrentDocumentPath))
            return DesignerDocumentPath.NormalizeDesignPath(environment.CurrentDocumentPath)!;

        return Path.Combine(AppContext.BaseDirectory, $"{document.ClassName}.mfdesign");
    }

    private string GetGeneratedCodePath(DesignDocument document)
    {
        var activeDocumentPath = currentDocumentPathProvider?.Invoke();

        if (!string.IsNullOrWhiteSpace(activeDocumentPath))
        {
            var designPath = DesignerDocumentPath.NormalizeDesignPath(activeDocumentPath)!;
            var directory = Path.GetDirectoryName(designPath);
            var fileName = Path.GetFileNameWithoutExtension(designPath);

            if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(fileName))
                return Path.Combine(directory, $"{fileName}.Designer.cs");
        }

        if (!string.IsNullOrWhiteSpace(environment?.CurrentDocumentPath))
        {
            var designPath = DesignerDocumentPath.NormalizeDesignPath(environment.CurrentDocumentPath)!;
            var directory = Path.GetDirectoryName(designPath);
            var fileName = Path.GetFileNameWithoutExtension(designPath);

            if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(fileName))
                return Path.Combine(directory, $"{fileName}.Designer.cs");
        }

        return Path.Combine(AppContext.BaseDirectory, $"{document.ClassName}.Designer.cs");
    }
}
