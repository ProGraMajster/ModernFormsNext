using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ModernFormsNext.VisualStudioExtension.Detection;

internal sealed class ModernFormsDesignableFileDetector
{
    public ModernFormsDesignableFileInfo? Inspect(string codeFilePath)
    {
        if (string.IsNullOrWhiteSpace(codeFilePath)
            || !string.Equals(Path.GetExtension(codeFilePath), ".cs", StringComparison.OrdinalIgnoreCase)
            || codeFilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(codeFilePath), "Program.cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var designFilePath = Path.ChangeExtension(codeFilePath, ".mfdesign");
        var designerCodePath = Path.Combine(
            Path.GetDirectoryName(codeFilePath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(codeFilePath) + ".Designer.cs");
        var hasDesignFile = IsModernFormsNextDesignFile(designFilePath);
        var projectInfo = InspectProject(codeFilePath);
        var isPartialModernFormsType = TryInspectPartialModernFormsNextType(
            codeFilePath,
            projectInfo.HasModernFormsNextReference,
            out var isUserControl,
            out var declaredClassName,
            out var namespaceName);
        var isDesignable = hasDesignFile
            || projectInfo.HasExplicitDesignerMetadata
            || isPartialModernFormsType;

        return new ModernFormsDesignableFileInfo(
            codeFilePath,
            designerCodePath,
            designFilePath,
            namespaceName,
            declaredClassName ?? Path.GetFileNameWithoutExtension(codeFilePath),
            isUserControl,
            hasDesignFile,
            projectInfo.HasExplicitDesignerMetadata,
            isDesignable);
    }

    private static bool IsModernFormsNextDesignFile(string designFilePath)
    {
        if (!File.Exists(designFilePath))
            return false;

        try
        {
            var text = File.ReadAllText(designFilePath);

            return text.IndexOf("\"toolName\"", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("ModernFormsNext", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("\"className\"", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("\"formName\"", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("\"controls\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static ProjectInspectionResult InspectProject(string codeFilePath)
    {
        var projectPath = FindNearestProjectFile(Path.GetDirectoryName(codeFilePath));

        if (projectPath is null)
            return ProjectInspectionResult.Empty;

        try
        {
            var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var relativePath = GetRelativePath(projectDirectory, codeFilePath).Replace('\\', '/');
            var fileName = Path.GetFileName(codeFilePath);
            var project = XDocument.Load(projectPath);

            var hasModernFormsNextReference = project.Descendants().Any(element =>
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

            var hasExplicitDesignerMetadata = project.Descendants().Any(element =>
            {
                if (!string.Equals(element.Name.LocalName, "Compile", StringComparison.OrdinalIgnoreCase))
                    return false;

                var include = ((string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))?.Replace('\\', '/');

                if (include is null)
                    return false;

                var matchesFile = string.Equals(include, relativePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(include, fileName, StringComparison.OrdinalIgnoreCase);

                return matchesFile
                    && element.Elements().Any(child =>
                        string.Equals(child.Name.LocalName, "ModernFormsNextDesigner", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(child.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(child.Name.LocalName, "SubType", StringComparison.OrdinalIgnoreCase)
                            && (string.Equals(child.Value.Trim(), "ModernFormsNextForm", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(child.Value.Trim(), "ModernFormsNextUserControl", StringComparison.OrdinalIgnoreCase)));
            });

            return new ProjectInspectionResult(hasModernFormsNextReference, hasExplicitDesignerMetadata);
        }
        catch
        {
            return ProjectInspectionResult.Empty;
        }
    }

    private static bool TryInspectPartialModernFormsNextType(
        string codeFilePath,
        bool hasModernFormsNextReference,
        out bool isUserControl,
        out string? className,
        out string? namespaceName)
    {
        isUserControl = false;
        className = null;
        namespaceName = null;

        if (!hasModernFormsNextReference || !File.Exists(codeFilePath))
            return false;

        try
        {
            var source = File.ReadAllText(codeFilePath);
            var hasModernFormsUsing = Regex.IsMatch(source, @"^\s*using\s+ModernFormsNext(\.[\w.]+)?\s*;", RegexOptions.Multiline);
            var hasWindowsFormsUsing = Regex.IsMatch(source, @"^\s*using\s+System\.Windows\.Forms(\.[\w.]+)?\s*;", RegexOptions.Multiline);

            foreach (Match match in Regex.Matches(
                source,
                @"\b(?<modifiers>(?:(?:public|private|protected|internal|sealed|abstract|static|unsafe|new)\s+)*)partial\s+class\s+(?<name>\w+)\s*:\s*(?<base>[A-Za-z_][\w.<>]*)"))
            {
                var baseTypeName = match.Groups["base"].Value;
                var modifiers = match.Groups["modifiers"].Value;
                className = match.Groups["name"].Value;
                namespaceName = ReadNamespace(source, match.Index);

                if (baseTypeName.StartsWith("ModernFormsNext.", StringComparison.Ordinal))
                {
                    isUserControl = baseTypeName.EndsWith(".UserControl", StringComparison.Ordinal);

                    if (isUserControl && !IsSupportedUserControlDeclaration(modifiers, source, match.Index))
                    {
                        isUserControl = false;
                        continue;
                    }

                    return baseTypeName.EndsWith(".Form", StringComparison.Ordinal)
                        || isUserControl
                        || baseTypeName.EndsWith(".Control", StringComparison.Ordinal)
                        || baseTypeName.EndsWith(".ModernForm", StringComparison.Ordinal)
                        || baseTypeName.EndsWith(".ModernControl", StringComparison.Ordinal);
                }

                if (hasWindowsFormsUsing)
                    continue;

                if (hasModernFormsUsing
                    && (string.Equals(baseTypeName, "Form", StringComparison.Ordinal)
                        || string.Equals(baseTypeName, "UserControl", StringComparison.Ordinal)
                        || string.Equals(baseTypeName, "Control", StringComparison.Ordinal)
                        || string.Equals(baseTypeName, "ModernForm", StringComparison.Ordinal)
                        || string.Equals(baseTypeName, "ModernControl", StringComparison.Ordinal)))
                {
                    isUserControl = string.Equals(baseTypeName, "UserControl", StringComparison.Ordinal);

                    if (isUserControl && !IsSupportedUserControlDeclaration(modifiers, source, match.Index))
                    {
                        isUserControl = false;
                        continue;
                    }

                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsSupportedUserControlDeclaration(
        string modifiers,
        string source,
        int declarationIndex)
        => Regex.IsMatch(modifiers, @"\bpublic\b")
        && !Regex.IsMatch(modifiers, @"\babstract\b")
        && !IsInsideTypeDeclaration(source, declarationIndex);

    private static bool IsInsideTypeDeclaration(string source, int declarationIndex)
    {
        var prefix = source.Substring(0, Math.Max(0, declarationIndex));
        return Regex.Matches(
                prefix,
                @"\b(?:class|struct|record)\s+[A-Za-z_]\w*[^;{}]*\{")
            .Cast<Match>()
            .Any(match => IsBraceScopeOpen(prefix, match.Index + match.Length - 1));
    }

    private static string? ReadNamespace(string source, int declarationIndex)
    {
        var prefix = source.Substring(0, Math.Max(0, declarationIndex));
        var matches = Regex.Matches(
            prefix,
            @"^\s*namespace\s+(?<name>[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*)\s*(?<terminator>[;{])",
            RegexOptions.Multiline);

        if (matches.Count == 0)
            return null;

        var segments = matches
            .Cast<Match>()
            .Where(match => match.Groups["terminator"].Value == ";"
                || IsBraceScopeOpen(prefix, match.Index + match.Length - 1))
            .Select(match => Regex.Replace(match.Groups["name"].Value, @"\s+", string.Empty))
            .ToArray();
        return segments.Length == 0 ? null : string.Join(".", segments);
    }

    private static bool IsBraceScopeOpen(string source, int openingBraceIndex)
    {
        var depth = 0;

        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return false;
        }

        return depth > 0;
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

    private static string GetRelativePath(string baseDirectory, string path)
    {
        var baseUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(baseDirectory)));
        var pathUri = new Uri(Path.GetFullPath(path));

        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;

    private sealed class ProjectInspectionResult
    {
        public static readonly ProjectInspectionResult Empty = new(false, false);

        public ProjectInspectionResult(
            bool hasModernFormsNextReference,
            bool hasExplicitDesignerMetadata)
        {
            HasModernFormsNextReference = hasModernFormsNextReference;
            HasExplicitDesignerMetadata = hasExplicitDesignerMetadata;
        }

        public bool HasModernFormsNextReference { get; }

        public bool HasExplicitDesignerMetadata { get; }
    }
}
