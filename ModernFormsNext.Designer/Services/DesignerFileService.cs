using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModernFormsNext.Designer.Services;

internal sealed partial class DesignerFileService
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

        var read = DesignerStableFileReader.Instance.Read(path);
        if (read is not { Exists: true, Error: null, Text: { } source })
            throw new IOException(read.Error ?? "The Designer source file is missing.");
        return ImportDesignerCodeText(source, options);
    }

    public CSharpDesignerParseResult ImportDesignerCodeText(
        string sourceText,
        CSharpDesignerParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        return new CSharpDesignerRoundTripService().ParseDesignerCode(sourceText, options);
    }

    internal CSharpDesignerParseResult ProjectDesignerCode(DesignDocument document)
    {
        var roundTrip = new CSharpDesignerRoundTripService();
        var generated = roundTrip.Generate(document, new CSharpDesignerGenerationOptions
        {
            AnimationDefinitions = animationDefinitionsProvider?.Invoke() ?? []
        });
        return generated.Succeeded
            ? ImportDesignerCodeText(generated.Code, new CSharpDesignerParseOptions { RootKind = document.RootKind })
            : new CSharpDesignerParseResult(null, generated.Validation.Errors.Select(error =>
                new CSharpDesignerDiagnostic(CSharpDesignerDiagnosticSeverity.Error, error)).ToArray());
    }

    public DesignerEventHandlerFileResult EnsureEventHandlerMethod(
        DesignDocument document,
        string handlerName,
        Type? eventHandlerType)
        => EnsureEventHandlerMethodCore(document, handlerName, WriteEventHandlerParameters(eventHandlerType));

    internal DesignerEventHandlerFileResult EnsureEventHandlerMethodFromMetadata(
        DesignDocument document, string handlerName, string parameters)
    {
        if (!DesignerEventSource.TryReadParameters(parameters, out _))
            return new(false, string.Empty, "Cannot generate an unsupported event signature. Edit the handler in source.");
        return EnsureEventHandlerMethodCore(document, handlerName, parameters);
    }

    private DesignerEventHandlerFileResult EnsureEventHandlerMethodCore(
        DesignDocument document, string handlerName, string parameters)
    {
        // Legacy internal callers remain synchronous only for standalone files. A live editor
        // must use the asynchronous path; falling through to disk could overwrite unsaved text.
        if (environment is IDesignerCodeDocumentHostEnvironment)
            return new(false, string.Empty, "Use the asynchronous host event editor for live C# buffers.");
        try
        {
            var prepared = PrepareEventHandlerAsync(document, handlerName, parameters, true, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (prepared.Plan.Succeeded && !ApplyEventHandlerAsync(prepared, CancellationToken.None).GetAwaiter().GetResult())
                return new(false, prepared.Path, "Source changed or the edit was denied; the binding was not changed.");
            return new(prepared.Plan.Succeeded, prepared.Path, prepared.Plan.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(false, string.Empty, $"Cannot edit event source: {ex.Message}"); }
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

    internal string? GetFormCodePath(DesignDocument document)
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

        // An unsaved design document does not authorize editing an executable's directory.
        return null;
    }

    internal static string WriteEventHandlerParameters(Type? eventHandlerType)
    {
        var invoke = eventHandlerType?.GetMethod("Invoke");

        if (invoke is null)
            return "object? sender, global::System.EventArgs e";

        if (invoke.ReturnType != typeof(void) || invoke.GetParameters().Any(p => p.ParameterType.IsByRef
            || p.ParameterType.IsPointer || p.ParameterType.IsByRefLike || p.ParameterType.IsFunctionPointer))
            return "unsupported signature";

        return string.Join(
            ", ",
            invoke.GetParameters().Select((parameter, index) =>
                $"{WriteTypeName(parameter.ParameterType)} {SanitizeParameterName(parameter.Name, index)}"));
    }

    private static string SanitizeParameterName(string? name, int index)
        => string.IsNullOrWhiteSpace(name) ? $"arg{index}"
            : SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

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

        if (type.IsArray)
            return WriteTypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";

        if (type.IsGenericType || type.IsNested)
        {
            var segments = new Stack<Type>();
            for (var current = type; current is not null; current = current.DeclaringType) segments.Push(current);
            var arguments = type.GetGenericArguments();
            var index = 0;
            var names = new List<string>();
            foreach (var segment in segments)
            {
                var name = segment.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0)
                {
                    var arity = int.Parse(name[(tick + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                    name = name[..tick] + "<" + string.Join(", ", arguments.Skip(index).Take(arity).Select(WriteTypeName)) + ">";
                    index += arity;
                }
                names.Add(name);
            }
            return "global::" + (string.IsNullOrEmpty(type.Namespace) ? string.Empty : type.Namespace + ".") + string.Join(".", names);
        }

        return $"global::{(type.FullName ?? type.Name).Replace('+', '.')}";
    }
}

internal readonly record struct DesignerEventHandlerFileResult(bool Succeeded, string Path, string Message);
