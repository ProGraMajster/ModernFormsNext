using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernFormsNext.Designing;

namespace ModernFormsNext.VisualStudioExtension.Detection;

/// <summary>
/// Detects whether a C# file represents a ModernFormsNext form or control that can be designed.
/// </summary>
/// <remarks>
/// The detector deliberately avoids registering all <c>.cs</c> files as designer files. A file
/// is considered designable only when it has a valid companion <c>.mfdesign</c>, explicit
/// ModernFormsNext project metadata, or a ModernFormsNext project reference plus a partial class
/// inheriting from a known ModernFormsNext form/control base type. Bare WinForms-looking
/// <c>Form</c> or <c>Control</c> inheritance is not enough by itself.
/// </remarks>
public sealed class ModernFormsDesignableFileDetector
{
    private static readonly string[] KnownBareBaseTypes =
    [
        "Form",
        "UserControl",
        "Control",
        "ModernForm",
        "ModernControl"
    ];

    private readonly ModernFormsDesignFileLocator fileLocator = new();

    /// <summary>
    /// Inspects a C# file and returns ModernFormsNext designer metadata for it.
    /// </summary>
    /// <param name="codeFilePath">The primary <c>.cs</c> file path.</param>
    /// <returns>Information about the file, or <see langword="null"/> when the path is not a C# file.</returns>
    public ModernFormsDesignableFileInfo? Inspect(string codeFilePath)
    {
        if (string.IsNullOrWhiteSpace(codeFilePath)
            || !string.Equals(Path.GetExtension(codeFilePath), ".cs", StringComparison.OrdinalIgnoreCase)
            || codeFilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var designFilePath = fileLocator.GetDesignFilePath(codeFilePath);
        var designerCodePath = fileLocator.GetDesignerCodePath(codeFilePath);
        var hasDesignFile = IsModernFormsNextDesignFile(designFilePath);
        var hasDesignerCodeFile = File.Exists(designerCodePath);
        var projectInfo = ReadProjectInfo(codeFilePath);
        var hasProjectMetadata = projectInfo.HasExplicitDesignerMetadata;

        ClassDeclarationSyntax? classDeclaration = null;
        string? namespaceName = null;
        string? className = Path.GetFileNameWithoutExtension(codeFilePath);
        string? baseTypeName = null;
        var isPartial = false;
        var rootKind = DesignRootKind.Form;
        var isSupportedUserControlRoot = true;
        var inheritsKnownType = false;
        var hasInitializeComponent = false;

        if (TryReadSource(codeFilePath, out var sourceText))
        {
            var root = CSharpSyntaxTree.ParseText(sourceText).GetCompilationUnitRoot();
            var hasModernFormsUsing = HasModernFormsNextUsing(root);
            var hasWindowsFormsUsing = HasWindowsFormsUsing(root);

            classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(candidate => IsModernFormsClassCandidate(
                    candidate,
                    hasModernFormsUsing,
                    hasWindowsFormsUsing,
                    hasDesignFile,
                    hasProjectMetadata,
                    projectInfo.HasModernFormsNextReference))
                ?? root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (classDeclaration is not null)
            {
                namespaceName = ReadNamespace(classDeclaration);
                className = classDeclaration.Identifier.ValueText;
                isPartial = classDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
                baseTypeName = classDeclaration.BaseList?.Types.FirstOrDefault()?.Type.ToString();
                rootKind = IsUserControlBaseType(baseTypeName, hasModernFormsUsing, hasWindowsFormsUsing)
                    ? DesignRootKind.UserControl
                    : DesignRootKind.Form;
                isSupportedUserControlRoot = rootKind != DesignRootKind.UserControl
                    || IsSupportedUserControlDeclaration(classDeclaration);
                inheritsKnownType = IsKnownModernFormsNextBaseType(
                    baseTypeName,
                    hasModernFormsUsing,
                    hasWindowsFormsUsing,
                    hasDesignFile,
                    hasProjectMetadata,
                    projectInfo.HasModernFormsNextReference);
                hasInitializeComponent = HasInitializeComponent(classDeclaration);
            }
        }

        var isDesignable = hasDesignFile
            || hasProjectMetadata
            || (projectInfo.HasModernFormsNextReference
                && isPartial
                && inheritsKnownType
                && isSupportedUserControlRoot);

        return new ModernFormsDesignableFileInfo(
            codeFilePath,
            designerCodePath,
            designFilePath,
            namespaceName,
            className,
            baseTypeName,
            isPartial,
            inheritsKnownType,
            hasInitializeComponent,
            hasDesignFile,
            hasDesignerCodeFile,
            hasProjectMetadata,
            isDesignable)
        {
            RootKind = rootKind
        };
    }

    private static bool TryReadSource(string path, out string source)
    {
        source = string.Empty;

        try
        {
            if (!File.Exists(path))
                return false;

            source = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsModernFormsClassCandidate(
        ClassDeclarationSyntax classDeclaration,
        bool hasModernFormsUsing,
        bool hasWindowsFormsUsing,
        bool hasDesignFile,
        bool hasProjectMetadata,
        bool hasModernFormsNextReference)
    {
        var baseTypeName = classDeclaration.BaseList?.Types.FirstOrDefault()?.Type.ToString();
        var isUserControl = IsUserControlBaseType(baseTypeName, hasModernFormsUsing, hasWindowsFormsUsing);

        return classDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
        && (!isUserControl || IsSupportedUserControlDeclaration(classDeclaration))
        && IsKnownModernFormsNextBaseType(
            baseTypeName,
            hasModernFormsUsing,
            hasWindowsFormsUsing,
            hasDesignFile,
            hasProjectMetadata,
            hasModernFormsNextReference);
    }

    private static bool IsSupportedUserControlDeclaration(ClassDeclarationSyntax declaration)
        => declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))
        && !declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword))
        && declaration.TypeParameterList is null
        && !declaration.Ancestors().OfType<TypeDeclarationSyntax>().Any();

    private static bool IsKnownModernFormsNextBaseType(
        string? baseTypeName,
        bool hasModernFormsUsing,
        bool hasWindowsFormsUsing,
        bool hasDesignFile,
        bool hasProjectMetadata,
        bool hasModernFormsNextReference)
    {
        if (string.IsNullOrWhiteSpace(baseTypeName))
            return false;

        var normalized = baseTypeName.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();

        if (normalized.StartsWith("ModernFormsNext.", StringComparison.Ordinal))
            return KnownBareBaseTypes.Any(baseType => normalized.EndsWith("." + baseType, StringComparison.Ordinal));

        if (!KnownBareBaseTypes.Any(baseType => string.Equals(normalized, baseType, StringComparison.Ordinal)))
            return false;

        if (hasWindowsFormsUsing && !hasDesignFile && !hasProjectMetadata)
            return false;

        return hasModernFormsUsing
            && (hasModernFormsNextReference || hasDesignFile || hasProjectMetadata);
    }

    private static bool IsUserControlBaseType(
        string? baseTypeName,
        bool hasModernFormsUsing,
        bool hasWindowsFormsUsing)
    {
        if (string.IsNullOrWhiteSpace(baseTypeName))
            return false;

        var normalized = baseTypeName.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
        return string.Equals(normalized, "ModernFormsNext.UserControl", StringComparison.Ordinal)
            || string.Equals(normalized, "UserControl", StringComparison.Ordinal)
                && hasModernFormsUsing
                && !hasWindowsFormsUsing;
    }

    private static bool HasModernFormsNextUsing(CompilationUnitSyntax root)
        => root.Usings.Any(usingDirective =>
        {
            var namespaceName = usingDirective.Name?.ToString();

            return usingDirective.Alias is null
                && (string.Equals(namespaceName, "ModernFormsNext", StringComparison.Ordinal)
                    || (namespaceName?.StartsWith("ModernFormsNext.", StringComparison.Ordinal) ?? false));
        });

    private static bool HasWindowsFormsUsing(CompilationUnitSyntax root)
        => root.Usings.Any(usingDirective =>
        {
            var namespaceName = usingDirective.Name?.ToString();

            return usingDirective.Alias is null
                && (string.Equals(namespaceName, "System.Windows.Forms", StringComparison.Ordinal)
                    || (namespaceName?.StartsWith("System.Windows.Forms.", StringComparison.Ordinal) ?? false));
        });

    private static bool HasInitializeComponent(ClassDeclarationSyntax classDeclaration)
        => classDeclaration.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Any(method => string.Equals(method.Identifier.ValueText, "InitializeComponent", StringComparison.Ordinal))
        || classDeclaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression switch
            {
                IdentifierNameSyntax identifier => string.Equals(identifier.Identifier.ValueText, "InitializeComponent", StringComparison.Ordinal),
                MemberAccessExpressionSyntax memberAccess => string.Equals(memberAccess.Name.Identifier.ValueText, "InitializeComponent", StringComparison.Ordinal),
                _ => false
            });

    private static string? ReadNamespace(ClassDeclarationSyntax classDeclaration)
    {
        var segments = new Stack<string>();

        for (SyntaxNode? node = classDeclaration.Parent; node is not null; node = node.Parent)
        {
            if (node is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                segments.Push(fileScopedNamespace.Name.ToString());

            if (node is NamespaceDeclarationSyntax namespaceDeclaration)
                segments.Push(namespaceDeclaration.Name.ToString());
        }

        return segments.Count == 0 ? null : string.Join(".", segments);
    }

    private static ProjectInspectionResult ReadProjectInfo(string codeFilePath)
    {
        var projectPath = FindNearestProjectFile(Path.GetDirectoryName(codeFilePath));

        if (projectPath is null)
            return ProjectInspectionResult.Empty;

        try
        {
            var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var relativePath = Path.GetRelativePath(projectDirectory, codeFilePath).Replace('\\', '/');
            var fileName = Path.GetFileName(codeFilePath);
            var project = XDocument.Load(projectPath);

            var hasModernFormsReference = project.Descendants().Any(element =>
            {
                if (string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase))
                {
                    var include = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update");

                    return string.Equals(include, "ModernFormsNext", StringComparison.OrdinalIgnoreCase);
                }

                if (string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                {
                    var include = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update");

                    return include is not null
                        && string.Equals(Path.GetFileName(include), "ModernFormsNext.csproj", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            });

            var hasExplicitDesignerMetadata = project.Descendants("Compile").Any(compile =>
            {
                var include = ((string?)compile.Attribute("Include") ?? (string?)compile.Attribute("Update"))?.Replace('\\', '/');
                if (include is null)
                    return false;

                var matchesFile = string.Equals(include, relativePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(include, fileName, StringComparison.OrdinalIgnoreCase);

                return matchesFile
                    && compile.Elements().Any(element =>
                        (string.Equals(element.Name.LocalName, "ModernFormsNextDesigner", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                        || (string.Equals(element.Name.LocalName, "SubType", StringComparison.OrdinalIgnoreCase)
                            && (string.Equals(element.Value.Trim(), "ModernFormsNextForm", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(element.Value.Trim(), "ModernFormsNextUserControl", StringComparison.OrdinalIgnoreCase))));
            });

            return new ProjectInspectionResult(hasModernFormsReference, hasExplicitDesignerMetadata);
        }
        catch
        {
            return ProjectInspectionResult.Empty;
        }
    }

    private static bool IsModernFormsNextDesignFile(string designFilePath)
    {
        if (!File.Exists(designFilePath))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(designFilePath));
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("metadata", out var metadata)
                && metadata.ValueKind == JsonValueKind.Object
                && metadata.TryGetProperty("toolName", out var toolName)
                && toolName.ValueKind == JsonValueKind.String
                && (toolName.GetString()?.StartsWith("ModernFormsNext", StringComparison.Ordinal) ?? false))
            {
                return true;
            }

            return root.TryGetProperty("namespace", out _)
                && root.TryGetProperty("className", out _)
                && root.TryGetProperty("formName", out _)
                && root.TryGetProperty("size", out var size)
                && size.ValueKind == JsonValueKind.Object
                && size.TryGetProperty("width", out _)
                && size.TryGetProperty("height", out _)
                && root.TryGetProperty("controls", out var controls)
                && controls.ValueKind == JsonValueKind.Array;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindNearestProjectFile(string? directory)
    {
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var project = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();

            if (project is not null)
                return project;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private sealed record ProjectInspectionResult(
        bool HasModernFormsNextReference,
        bool HasExplicitDesignerMetadata)
    {
        public static ProjectInspectionResult Empty { get; } = new(false, false);
    }
}
