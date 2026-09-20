using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ModernFormsNext.Designer.Services;

internal static partial class DesignerProjectUserControlDiscovery
{
    private static IReadOnlyList<string> ReadReferencePaths(string? projectPath, string directory, List<string> diagnostics)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var projectFiles = File.Exists(projectPath) && string.Equals(IOPath.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase)
                ? new[] { projectPath! } : Directory.GetFiles(directory, "*.csproj");
            if (projectFiles.Length != 1)
            {
                if (projectFiles.Length > 1) diagnostics.Add("Multiple project files share this directory. Open the Designer with an explicit project path to resolve references.");
                return [];
            }
            var projectFile = projectFiles[0];
            var project = ReadProjectXml(projectFile);
            var configuration = Property(project, "Configuration") ?? "Debug";
            var framework = Property(project, "TargetFramework");
            if (framework is null && Property(project, "TargetFrameworks") is { } frameworks)
            {
                framework = frameworks.Split(';')[0];
                diagnostics.Add($"Multi-target project: metadata uses '{framework}'. Other targets require a separate host/project context.");
            }
            foreach (var element in project.Descendants().Where(e => e.Name.LocalName is "Reference" or "ProjectReference"))
            {
                if (element.AncestorsAndSelf().Any(e => e.Attribute("Condition") is not null))
                {
                    diagnostics.Add($"Conditional reference '{element.Attribute("Include")?.Value}' is not evaluated. Use an unconditional HintPath for Designer metadata.");
                    continue;
                }
                var hint = element.Elements().FirstOrDefault(e => e.Name.LocalName == "HintPath")?.Value;
                var include = element.Attribute("Include")?.Value;
                if (element.Name.LocalName == "ProjectReference" && include is not null)
                {
                    var path = ResolveLiteralPath(include, directory, configuration, framework, diagnostics);
                    if (path is not null) AddProjectOutput(path, configuration, framework, result, diagnostics);
                }
                else if (hint is not null)
                {
                    var path = ResolveLiteralPath(hint, directory, configuration, framework, diagnostics);
                    if (path is not null) AddReference(path, result, diagnostics);
                }
                else if (include is not null && !include.StartsWith("System", StringComparison.Ordinal))
                    diagnostics.Add($"Reference '{include}' has no HintPath; no assembly probing or project execution is performed. Provide a HintPath or restore package assets.");
            }

            var assetsPath = IOPath.Combine(directory, "obj", "project.assets.json");
            if (File.Exists(assetsPath))
            {
                if (File.GetLastWriteTimeUtc(projectFile) > File.GetLastWriteTimeUtc(assetsPath))
                    diagnostics.Add("Package assets are older than the project. Restore/rebuild, then Refresh Toolbox to update reference metadata.");
                using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
                var root = assets.RootElement;
                if (root.TryGetProperty("targets", out var targets)
                    && root.TryGetProperty("libraries", out var libraries)
                    && root.TryGetProperty("packageFolders", out var folders))
                {
                    var target = targets.EnumerateObject().FirstOrDefault(t => t.Name == framework);
                    if (target.Value.ValueKind == JsonValueKind.Undefined)
                        target = targets.EnumerateObject().FirstOrDefault(t => !t.Name.Contains('/'));
                    if (target.Value.ValueKind == JsonValueKind.Object)
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        if (!libraries.TryGetProperty(library.Name, out var info)
                            || !info.TryGetProperty("type", out var kind) || kind.GetString() != "package"
                            || !info.TryGetProperty("path", out var packagePath)
                            || !library.Value.TryGetProperty("compile", out var compile)) continue;
                        foreach (var entry in compile.EnumerateObject().Where(e => e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                        {
                            var path = folders.EnumerateObject().Select(folder => IOPath.Combine(folder.Name, packagePath.GetString()!, entry.Name))
                                .FirstOrDefault(File.Exists);
                            if (path is null) diagnostics.Add($"Missing restored reference '{library.Name}/{entry.Name}'. Restore, then Refresh Toolbox.");
                            else AddReference(IOPath.GetFullPath(path), result, diagnostics);
                        }
                    }
                }
            }
            else if (project.Descendants().Any(e => e.Name.LocalName == "PackageReference"))
                diagnostics.Add("No obj/project.assets.json: restore packages, then Refresh Toolbox. Package metadata is unavailable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or JsonException or ArgumentException or InvalidOperationException)
        { diagnostics.Add($"Cannot inspect project references: {exception.Message}. Restore/rebuild, then Refresh Toolbox."); }
        return result.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static XDocument ReadProjectXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024
        });
        return XDocument.Load(reader);
    }

    private static string? Property(XDocument project, string name) => project.Descendants()
        .LastOrDefault(e => e.Name.LocalName == name && e.Parent?.Name.LocalName == "PropertyGroup"
            && !e.AncestorsAndSelf().Any(a => a.Attribute("Condition") is not null))?.Value.Trim();

    private static string? ResolveLiteralPath(string value, string directory, string configuration, string? framework, List<string> diagnostics)
    {
        value = value.Replace("$(MSBuildProjectDirectory)", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("$(ProjectDir)", directory + IOPath.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            .Replace("$(Configuration)", configuration, StringComparison.OrdinalIgnoreCase)
            .Replace("$(TargetFramework)", framework ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (value.Contains('$') || value.Contains('@') || value.IndexOfAny(['*', '?', ';']) >= 0)
        {
            diagnostics.Add($"Reference path '{value}' requires unsupported project evaluation. Provide a literal HintPath; no MSBuild targets are run.");
            return null;
        }
        return IOPath.GetFullPath(value, directory);
    }

    private static void AddReference(string path, HashSet<string> result, List<string> diagnostics)
    {
        // Framework and BCL symbols always come from this Designer's trusted runtime. Loading a
        // second framework version would make type identity ambiguous rather than enable preview.
        var name = IOPath.GetFileNameWithoutExtension(path);
        if (name is "ModernFormsNext" or "ModernFormsNext.Designing"
            || name.StartsWith("System.", StringComparison.Ordinal) || name is "mscorlib" or "netstandard") return;
        if (File.Exists(path)) result.Add(IOPath.GetFullPath(path));
        else diagnostics.Add($"Missing reference '{path}'. Build/restore it, then Refresh Toolbox. Existing controls remain placeholders.");
    }

    private static void AddProjectOutput(string projectPath, string configuration, string? consumerFramework,
        HashSet<string> result, List<string> diagnostics)
    {
        if (!File.Exists(projectPath))
        {
            diagnostics.Add($"Missing referenced project '{projectPath}'. Fix the ProjectReference and Refresh Toolbox.");
            return;
        }
        var project = ReadProjectXml(projectPath);
        var directory = IOPath.GetDirectoryName(projectPath)!;
        var framework = Property(project, "TargetFramework");
        if (framework is null && Property(project, "TargetFrameworks") is { } frameworks)
        {
            var choices = frameworks.Split(';');
            framework = choices.Contains(consumerFramework) ? consumerFramework : choices[0];
            diagnostics.Add($"Referenced multi-target project '{projectPath}': inspecting '{framework}'.");
        }
        if (framework is null)
        {
            diagnostics.Add($"Cannot resolve TargetFramework for '{projectPath}' without project evaluation. Supply a built DLL HintPath.");
            return;
        }
        var output = Property(project, "OutputPath") ?? IOPath.Combine("bin", configuration);
        var outputDirectory = ResolveLiteralPath(output, directory, configuration, framework, diagnostics);
        if (outputDirectory is null) return;
        if (!string.Equals(Property(project, "AppendTargetFrameworkToOutputPath"), "false", StringComparison.OrdinalIgnoreCase))
            outputDirectory = IOPath.Combine(outputDirectory, framework);
        var assemblyName = Property(project, "AssemblyName") ?? IOPath.GetFileNameWithoutExtension(projectPath);
        var path = IOPath.Combine(outputDirectory, assemblyName + ".dll");
        AddReference(path, result, diagnostics);
        if (File.Exists(path) && (File.GetLastWriteTimeUtc(projectPath) > File.GetLastWriteTimeUtc(path)
            || EnumerateProjectFiles(directory, "*.cs").Any(source => File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(path))))
            diagnostics.Add($"Stale build '{path}': project/source is newer. Rebuild the referenced project and Refresh Toolbox; displayed binary metadata may be outdated.");
    }
}
