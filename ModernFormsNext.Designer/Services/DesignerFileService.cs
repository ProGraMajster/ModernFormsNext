using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModernFormsNext.Designer.Services;

internal sealed class DesignerFileService
{
    private readonly IDesignerHostEnvironment? environment;
    private readonly Func<string?>? currentDocumentPathProvider;
    private readonly Func<IReadOnlyList<DesignAnimationDefinitionDescriptor>>? animationDefinitionsProvider;

    public DesignerFileService(
        IDesignerHostEnvironment? environment = null,
        Func<string?>? currentDocumentPathProvider = null,
        Func<IReadOnlyList<DesignAnimationDefinitionDescriptor>>? animationDefinitionsProvider = null)
    {
        this.environment = environment;
        this.currentDocumentPathProvider = currentDocumentPathProvider;
        this.animationDefinitionsProvider = animationDefinitionsProvider;
    }

    public DesignDocument LoadDesignDocument(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return DesignDocumentSerializer.Default.Load(path);
    }

    public string SaveDesignDocument(DesignDocument document, string? explicitPath = null)
    {
        var path = GetDesignDocumentPath(document, explicitPath);
        DesignDocumentSerializer.Default.Save(path, document);
        return path;
    }

    public DesignerGenerationFileResult PrepareDesignerCode(
        DesignDocument document,
        string? explicitDesignDocumentPath = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var designDocumentPath = GetDesignDocumentPath(document, explicitDesignDocumentPath);
        // The reverse service owns parser state. Keep each generation/import operation isolated so
        // a background external-change parse cannot race a foreground normal save.
        var roundTrip = new CSharpDesignerRoundTripService();
        var result = roundTrip.Generate(
            document,
            new CSharpDesignerGenerationOptions
            {
                SourceFilePath = IOPath.GetFileName(designDocumentPath),
                DesignHash = roundTrip.ComputeDesignHash(document),
                AnimationDefinitions = animationDefinitionsProvider?.Invoke() ?? []
            });

        if (!result.Succeeded)
            return new DesignerGenerationFileResult(false, string.Empty, string.Empty, result.Validation.Errors);

        var path = GetGeneratedCodePath(document, designDocumentPath);
        return new DesignerGenerationFileResult(true, path, result.Code, Array.Empty<string>());
    }

    public DesignerGenerationFileResult GenerateDesignerCode(
        DesignDocument document,
        string? explicitDesignDocumentPath = null)
    {
        var result = PrepareDesignerCode(document, explicitDesignDocumentPath);
        if (result.Succeeded)
            DesignerAtomicFileWriter.WriteUtf8(result.Path, result.Code);

        return result;
    }

    public CSharpDesignerParseResult ImportDesignerCode(
        string path,
        CSharpDesignerParseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return ImportDesignerCodeText(File.ReadAllText(path), options);
    }

    public CSharpDesignerParseResult ImportDesignerCodeText(
        string sourceText,
        CSharpDesignerParseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);
        return new CSharpDesignerRoundTripService().ParseDesignerCode(sourceText, options);
    }

    public DesignerEventHandlerFileResult EnsureEventHandlerMethod(
        DesignDocument document,
        string handlerName,
        Type? eventHandlerType)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);

        var codePath = GetFormCodePath(document);

        if (string.IsNullOrWhiteSpace(codePath) || !File.Exists(codePath))
            return new DesignerEventHandlerFileResult(false, codePath ?? string.Empty, $"Cannot add event handler {handlerName}: form code file was not found.");

        var sourceText = File.ReadAllText(codePath);
        var root = CSharpSyntaxTree.ParseText(sourceText).GetRoot();
        var classDeclaration = root
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(type => string.Equals(type.Identifier.ValueText, document.ClassName, StringComparison.Ordinal))
            ?? root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

        if (classDeclaration is null)
            return new DesignerEventHandlerFileResult(false, codePath, $"Cannot add event handler {handlerName}: no class declaration was found.");

        if (classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(method => string.Equals(method.Identifier.ValueText, handlerName, StringComparison.Ordinal)))
        {
            return new DesignerEventHandlerFileResult(true, codePath, $"Event handler {handlerName} already exists in {IOPath.GetFileName(codePath)}.");
        }

        var lineEnding = sourceText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var parameters = WriteEventHandlerParameters(eventHandlerType);
        var methodText =
            $"{lineEnding}    private void {handlerName}({parameters}){lineEnding}" +
            $"    {{{lineEnding}" +
            $"        // TODO: Add event handling logic.{lineEnding}" +
            $"    }}{lineEnding}";
        var insertionIndex = classDeclaration.CloseBraceToken.SpanStart;
        var updatedText = sourceText.Insert(insertionIndex, methodText);
        DesignerAtomicFileWriter.WriteUtf8(codePath, updatedText);

        return new DesignerEventHandlerFileResult(true, codePath, $"Added event handler {handlerName} to {IOPath.GetFileName(codePath)}.");
    }

    public string GetDesignDocumentPath(DesignDocument document, string? explicitPath = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!string.IsNullOrWhiteSpace(explicitPath))
            return DesignerDocumentPath.NormalizeDesignPath(explicitPath)!;

        var activeDocumentPath = currentDocumentPathProvider?.Invoke();

        if (!string.IsNullOrWhiteSpace(activeDocumentPath))
            return DesignerDocumentPath.NormalizeDesignPath(activeDocumentPath)!;

        if (currentDocumentPathProvider is null
            && !string.IsNullOrWhiteSpace(environment?.CurrentDocumentPath))
            return DesignerDocumentPath.NormalizeDesignPath(environment.CurrentDocumentPath)!;

        return IOPath.Combine(AppContext.BaseDirectory, $"{document.ClassName}.mfdesign");
    }

    public string GetGeneratedCodePath(DesignDocument document, string? explicitDesignDocumentPath = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var activeDocumentPath = !string.IsNullOrWhiteSpace(explicitDesignDocumentPath)
            ? explicitDesignDocumentPath
            : currentDocumentPathProvider?.Invoke();

        if (!string.IsNullOrWhiteSpace(activeDocumentPath))
        {
            var designPath = DesignerDocumentPath.NormalizeDesignPath(activeDocumentPath)!;
            var directory = IOPath.GetDirectoryName(designPath);
            var fileName = IOPath.GetFileNameWithoutExtension(designPath);

            if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(fileName))
                return IOPath.Combine(directory, $"{fileName}.Designer.cs");
        }

        if (currentDocumentPathProvider is null
            && !string.IsNullOrWhiteSpace(environment?.CurrentDocumentPath))
        {
            var designPath = DesignerDocumentPath.NormalizeDesignPath(environment.CurrentDocumentPath)!;
            var directory = IOPath.GetDirectoryName(designPath);
            var fileName = IOPath.GetFileNameWithoutExtension(designPath);

            if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(fileName))
                return IOPath.Combine(directory, $"{fileName}.Designer.cs");
        }

        return IOPath.Combine(AppContext.BaseDirectory, $"{document.ClassName}.Designer.cs");
    }

    private string? GetFormCodePath(DesignDocument document)
    {
        var activeDocumentPath = currentDocumentPathProvider?.Invoke();

        if (!string.IsNullOrWhiteSpace(activeDocumentPath))
            return IOPath.Combine(
                IOPath.GetDirectoryName(DesignerDocumentPath.NormalizeDesignPath(activeDocumentPath)!) ?? string.Empty,
                $"{IOPath.GetFileNameWithoutExtension(DesignerDocumentPath.NormalizeDesignPath(activeDocumentPath)!)}.cs");

        if (currentDocumentPathProvider is null
            && !string.IsNullOrWhiteSpace(environment?.CurrentDocumentPath))
            return IOPath.Combine(
                IOPath.GetDirectoryName(DesignerDocumentPath.NormalizeDesignPath(environment.CurrentDocumentPath)!) ?? string.Empty,
                $"{IOPath.GetFileNameWithoutExtension(DesignerDocumentPath.NormalizeDesignPath(environment.CurrentDocumentPath)!)}.cs");

        return IOPath.Combine(AppContext.BaseDirectory, $"{document.ClassName}.cs");
    }

    private static string WriteEventHandlerParameters(Type? eventHandlerType)
    {
        var invoke = eventHandlerType?.GetMethod("Invoke");

        if (invoke is null)
            return "object? sender, global::System.EventArgs e";

        return string.Join(
            ", ",
            invoke.GetParameters().Select((parameter, index) =>
                $"{WriteTypeName(parameter.ParameterType)} {SanitizeParameterName(parameter.Name, index)}"));
    }

    private static string SanitizeParameterName(string? name, int index)
        => string.IsNullOrWhiteSpace(name) ? $"arg{index}" : name;

    private static string WriteTypeName(Type type)
    {
        if (type == typeof(void))
            return "void";

        if (type == typeof(object))
            return "object?";

        if (type == typeof(string))
            return "string?";

        if (type == typeof(bool))
            return "bool";

        if (type == typeof(int))
            return "int";

        if (type == typeof(float))
            return "float";

        if (type == typeof(double))
            return "double";

        if (type.IsByRef)
            return WriteTypeName(type.GetElementType()!);

        if (type.IsGenericType)
        {
            var typeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
            var tickIndex = typeName.IndexOf('`', StringComparison.Ordinal);
            if (tickIndex >= 0)
                typeName = typeName[..tickIndex];

            return $"global::{typeName}<{string.Join(", ", type.GetGenericArguments().Select(WriteTypeName))}>";
        }

        return $"global::{type.FullName ?? type.Name}";
    }
}

internal readonly record struct DesignerEventHandlerFileResult(bool Succeeded, string Path, string Message);
