using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace ModernFormsNext.Designer.Services;

internal sealed record DesignerProjectUserControlInfo(
    string Name,
    string FullName,
    string SourceFilePath)
{
    public string? DisplayName { get; init; }
    public string Category { get; init; } = "My Project";
    public string? Description { get; init; }
    public string? DefaultEventName { get; init; }
    public bool VisibleInToolbox { get; init; } = true;
    public string FrameworkBaseTypeName { get; init; } = "ModernFormsNext.UserControl";
    public IReadOnlyList<DesignerCustomProperty> Properties { get; init; } = [];
    public IReadOnlyList<DesignerCustomEvent> Events { get; init; } = [];
    public IReadOnlyList<string> HiddenPropertyNames { get; init; } = [];
    public IReadOnlyList<string> HiddenEventNames { get; init; } = [];
}

/// <summary>
/// Discovers project-owned UserControl types without loading or executing the user assembly.
/// </summary>
internal static partial class DesignerProjectUserControlDiscovery
{
    public static IReadOnlyList<DesignerProjectUserControlInfo> Discover(string? projectPath)
        => DiscoverCatalog(projectPath).Controls;

    private static IReadOnlyList<DesignerProjectUserControlInfo> DiscoverSourceCandidates(string? projectPath)
    {
        var projectDirectory = GetProjectDirectory(projectPath);

        if (projectDirectory is null)
            return [];

        try
        {
            var inputs = ReadProjectDeclarations(projectDirectory).Take(4097).ToArray();
            if (inputs.Length > 4096) throw new InvalidDataException("Project metadata exceeds its declaration budget.");
            var declarations = inputs
                .GroupBy(declaration => declaration.FullName, StringComparer.Ordinal)
                .Select(MergePartialDeclarations)
                .ToArray();
            var userControls = new HashSet<string>(StringComparer.Ordinal);
            var declarationsByName = declarations.ToDictionary(declaration => declaration.FullName, StringComparer.Ordinal);
            var changed = true;
            int rounds = 0;

            while (changed)
            {
                if (++rounds > 64) throw new InvalidDataException("Project metadata exceeds its inheritance discovery budget.");
                changed = false;

                foreach (var declaration in declarations)
                {
                    if (userControls.Contains(declaration.FullName)
                        || !IsUserControlBase(declaration, declarationsByName, userControls))
                    {
                        continue;
                    }

                    userControls.Add(declaration.FullName);
                    changed = true;
                }
            }

            return declarations
                .Where(declaration => userControls.Contains(declaration.FullName)
                    && declaration.IsPublic
                    && !declaration.IsAbstract
                    && !declaration.IsGeneric)
                .Select(declaration => new DesignerProjectUserControlInfo(
                    declaration.Name,
                    declaration.FullName,
                    declaration.SourceFilePath))
                .OrderBy(control => control.Name, StringComparer.Ordinal)
                .ThenBy(control => control.FullName, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static bool Matches(DesignerProjectUserControlInfo control, string typeName)
    {
        var normalized = NormalizeTypeName(typeName);
        return string.Equals(normalized, control.Name, StringComparison.Ordinal)
            || string.Equals(normalized, control.FullName, StringComparison.Ordinal);
    }

    internal static string NormalizeTypeName(string typeName)
    {
        var normalized = typeName.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
        var assemblySeparator = normalized.IndexOf(',');

        return assemblySeparator >= 0
            ? normalized[..assemblySeparator].Trim()
            : normalized;
    }

    internal static string? GetProjectDirectory(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        if (Directory.Exists(projectPath))
            return IOPath.GetFullPath(projectPath);

        return File.Exists(projectPath)
            ? IOPath.GetDirectoryName(IOPath.GetFullPath(projectPath))
            : null;
    }

    private static IEnumerable<ProjectClassDeclaration> ReadProjectDeclarations(string directory)
    {
        int count = 0;
        long remaining = DesignerEventSource.MaximumProjectCharacters;
        foreach (var path in EnumerateProjectFiles(directory, "*.cs", maximumEntries: 8192))
        {
            if (++count > DesignerEventSource.MaximumProjectFiles)
                throw new InvalidDataException("Project metadata exceeds its source file budget.");
            var text = ReadEventSourceText(path);
            if ((remaining -= text.Length) < 0)
                throw new InvalidDataException("Project metadata exceeds its source text budget.");
            foreach (var declaration in ReadDeclarations(path, text))
                yield return declaration;
        }
    }

    private static IEnumerable<ProjectClassDeclaration> ReadDeclarations(string path, string text)
    {
        if (!DesignerEventSource.TryParse(text, path, out var tree, out var error))
            throw new InvalidDataException(error);
        var root = tree.GetCompilationUnitRoot();

        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            // Nested UserControls cannot be represented by the current top-level partial-class
            // document model. Generic top-level declarations remain in the inheritance graph so a
            // concrete non-generic subclass can still be discovered, but are filtered from Toolbox.
            if (declaration.Ancestors().OfType<TypeDeclarationSyntax>().Any())
                continue;

            var name = declaration.Identifier.ValueText;
            var namespaceName = ReadNamespace(declaration);
            var fullName = string.IsNullOrWhiteSpace(namespaceName) ? name : $"{namespaceName}.{name}";
            var usings = ReadUsings(declaration).ToArray();
            var baseTypeName = ResolveAliases(
                declaration.BaseList?.Types.FirstOrDefault()?.Type.ToString(),
                usings);
            yield return new ProjectClassDeclaration(
                name,
                namespaceName,
                fullName,
                baseTypeName,
                path,
                declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)),
                declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword)),
                declaration.TypeParameterList is not null,
                HasNamespaceUsing(usings, "ModernFormsNext"),
                HasNamespaceUsing(usings, "System.Windows.Forms"));
        }
    }

    private static bool IsUserControlBase(
        ProjectClassDeclaration declaration,
        IReadOnlyDictionary<string, ProjectClassDeclaration> declarations,
        HashSet<string> knownUserControls)
    {
        var baseTypeName = declaration.BaseTypeName;

        if (string.IsNullOrWhiteSpace(baseTypeName))
            return false;

        var normalized = NormalizeTypeName(baseTypeName);
        var lookupName = RemoveGenericArguments(normalized);
        var projectBase = FindProjectBase(declaration, lookupName, declarations);

        if (projectBase is not null)
            return knownUserControls.Contains(projectBase.FullName);

        if (string.Equals(lookupName, "ModernFormsNext.UserControl", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(lookupName, "UserControl", StringComparison.Ordinal)
            && declaration.HasModernFormsNextUsing
            && !declaration.HasWindowsFormsUsing;
    }

    private static string ReadNamespace(ClassDeclarationSyntax declaration)
    {
        var segments = new Stack<string>();

        for (SyntaxNode? node = declaration.Parent; node is not null; node = node.Parent)
        {
            if (node is FileScopedNamespaceDeclarationSyntax fileScoped)
                segments.Push(fileScoped.Name.ToString());

            if (node is NamespaceDeclarationSyntax blockScoped)
                segments.Push(blockScoped.Name.ToString());
        }

        return string.Join(".", segments);
    }

    internal static bool IsExcludedProjectArtifact(string path, string projectDirectory)
    {
        var relative = IOPath.GetRelativePath(projectDirectory, path);
        return relative
            .Split(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("artifacts", StringComparison.OrdinalIgnoreCase)
                || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || segment.Equals(".vs", StringComparison.OrdinalIgnoreCase));
    }

    internal static IEnumerable<string> EnumerateProjectFiles(
        string projectDirectory,
        string searchPattern,
        int maximumEntries = int.MaxValue)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(projectDirectory);
        var entries = 0;

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();
            // Count all visited entries, including files that do not match the requested
            // suffix. A matching-file counter cannot bound scanning a large directory.
            var options = new EnumerationOptions { AttributesToSkip = 0,
                IgnoreInaccessible = true, RecurseSubdirectories = false };
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", options))
            {
                if (++entries > maximumEntries)
                    throw new InvalidDataException("Project enumeration exceeds its entry safety budget.");
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    if (!IsExcludedProjectArtifact(entry.FullName, projectDirectory))
                        pendingDirectories.Push(entry.FullName);
                }
                else if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                    searchPattern, entry.Name, ignoreCase: OperatingSystem.IsWindows()))
                    yield return entry.FullName;
            }
        }
    }

    private static ProjectClassDeclaration MergePartialDeclarations(
        IGrouping<string, ProjectClassDeclaration> declarations)
    {
        var declaration = declarations
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.BaseTypeName))
            ?? declarations.First();

        return declaration with
        {
            IsPublic = declarations.Any(candidate => candidate.IsPublic),
            IsAbstract = declarations.Any(candidate => candidate.IsAbstract),
            IsGeneric = declarations.Any(candidate => candidate.IsGeneric),
            HasModernFormsNextUsing = declarations.Any(candidate => candidate.HasModernFormsNextUsing),
            HasWindowsFormsUsing = declarations.Any(candidate => candidate.HasWindowsFormsUsing)
        };
    }

    private static ProjectClassDeclaration? FindProjectBase(
        ProjectClassDeclaration declaration,
        string typeName,
        IReadOnlyDictionary<string, ProjectClassDeclaration> declarations)
    {
        if (!typeName.Contains('.'))
        {
            var relativeName = string.IsNullOrWhiteSpace(declaration.Namespace)
                ? typeName
                : $"{declaration.Namespace}.{typeName}";
            declarations.TryGetValue(relativeName, out var relative);

            if (relative is not null)
                return relative;

            return null;
        }

        if (!string.IsNullOrWhiteSpace(declaration.Namespace))
        {
            var relativeName = $"{declaration.Namespace}.{typeName}";
            declarations.TryGetValue(relativeName, out var relative);

            if (relative is not null)
                return relative;
        }

        return declarations.GetValueOrDefault(typeName);
    }

    private static IEnumerable<UsingDirectiveSyntax> ReadUsings(ClassDeclarationSyntax declaration)
        => declaration.Ancestors()
            .SelectMany(node => node switch
            {
                CompilationUnitSyntax compilationUnit => compilationUnit.Usings,
                BaseNamespaceDeclarationSyntax namespaceDeclaration => namespaceDeclaration.Usings,
                _ => []
            });

    private static bool HasNamespaceUsing(IEnumerable<UsingDirectiveSyntax> usings, string namespaceName)
        => usings.Any(usingDirective => usingDirective.Alias is null
            && string.Equals(
                NormalizeTypeName(usingDirective.Name?.ToString() ?? string.Empty),
                namespaceName,
                StringComparison.Ordinal));

    private static string? ResolveAliases(
        string? typeName,
        IEnumerable<UsingDirectiveSyntax> usings)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return typeName;

        var normalized = NormalizeTypeName(typeName);

        foreach (var usingDirective in usings)
        {
            var alias = usingDirective.Alias?.Name.Identifier.ValueText;
            var target = usingDirective.Name?.ToString();

            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(target))
                continue;

            target = NormalizeTypeName(target);

            if (string.Equals(normalized, alias, StringComparison.Ordinal))
                return target;

            if (normalized.StartsWith(alias + ".", StringComparison.Ordinal))
                return target + normalized[alias.Length..];
        }

        return normalized;
    }

    private static string RemoveGenericArguments(string typeName)
    {
        var result = new StringBuilder(typeName.Length);
        var depth = 0;

        foreach (var character in typeName)
        {
            if (character == '<')
            {
                depth++;
                continue;
            }

            if (character == '>')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (depth == 0 && !char.IsWhiteSpace(character))
                result.Append(character);
        }

        return result.ToString();
    }

    private sealed record ProjectClassDeclaration(
        string Name,
        string Namespace,
        string FullName,
        string? BaseTypeName,
        string SourceFilePath,
        bool IsPublic,
        bool IsAbstract,
        bool IsGeneric,
        bool HasModernFormsNextUsing,
        bool HasWindowsFormsUsing);
}
