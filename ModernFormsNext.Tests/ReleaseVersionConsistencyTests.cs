using System.Xml.Linq;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ReleaseVersionConsistencyTests
{
    private const string ExpectedVersion = "1.10.0";

    private static readonly string[] PackableProjects =
    [
        "ModernFormsNext/ModernFormsNext.csproj",
        "ModernFormsNext.CodeGeneration/ModernFormsNext.CodeGeneration.csproj",
        "ModernFormsNext.Designer/ModernFormsNext.Designer.csproj",
        "ModernFormsNext.Designing/ModernFormsNext.Designing.csproj",
        "ModernFormsNext.Templates/ModernFormsNext.Templates.csproj",
        "ModernFormsNext.WindowKit/ModernFormsNext.WindowKit.csproj",
        "ModernFormsNext.WindowKit.Backend/ModernFormsNext.WindowKit.Backend.csproj",
        "ModernFormsNext.WindowKit.Backend.Windows/ModernFormsNext.WindowKit.Backend.Windows.csproj"
    ];

    [Fact]
    public void CentralPackageAndVsixVersionsAreCoordinated()
    {
        XDocument properties = LoadXml("Directory.Build.props");

        Assert.Equal(ExpectedVersion, ElementValue(properties, "ModernFormsNextPackageVersion"));
        Assert.Equal(ExpectedVersion, ElementValue(properties, "ModernFormsNextVisualStudioExtensionVersion"));
    }

    [Fact]
    public void EveryPackableProjectUsesTheCentralPackageVersion()
    {
        string root = RepositoryFileEnumerator.FindRepositoryRoot();
        RepositoryFileEnumeration enumeration = RepositoryFileEnumerator.EnumerateFiles(
            root,
            path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        string[] actualProjects = enumeration.Files
            .Where(path => string.Equals(ElementValue(LoadXml(root, path), "IsPackable"), "true", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string[] expectedProjects = PackableProjects.Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            expectedProjects.SequenceEqual(actualProjects, StringComparer.Ordinal),
            enumeration.FormatDiagnostics(expectedProjects));

        foreach (string projectPath in PackableProjects)
        {
            XDocument project = LoadXml(projectPath);
            Assert.Equal("$(ModernFormsNextPackageVersion)", ElementValue(project, "Version"));
        }
    }

    [Fact]
    public void ActiveVsixAndTemplateMetadataUseTheReleaseVersion()
    {
        XDocument manifest = LoadXml("ModernFormsNext.VisualStudioExtension.Vsix/source.extension.vsixmanifest");
        XElement identity = Assert.Single(manifest.Descendants(), element => element.Name.LocalName == "Identity");
        Assert.Equal(ExpectedVersion, identity.Attribute("Version")?.Value);

        XDocument vsixProject = LoadXml("ModernFormsNext.VisualStudioExtension.Vsix/ModernFormsNext.VisualStudioExtension.Vsix.csproj");
        Assert.Equal("$(ModernFormsNextVisualStudioExtensionVersion)", ElementValue(vsixProject, "Version"));
        Assert.Equal("$(ModernFormsNextVisualStudioExtensionVersion).0", ElementValue(vsixProject, "AssemblyVersion"));
        Assert.Equal("$(ModernFormsNextVisualStudioExtensionVersion).0", ElementValue(vsixProject, "FileVersion"));

        XDocument templateProject = LoadXml("ModernFormsNext.Templates/templates/modernformsnext-app/MyApp.csproj");
        XElement packageReference = Assert.Single(templateProject.Descendants("PackageReference"));
        Assert.Equal("ModernFormsNext", packageReference.Attribute("Include")?.Value);
        Assert.Equal(ExpectedVersion, packageReference.Attribute("Version")?.Value);

        AssertRegistrationVersion("ModernFormsNext.VisualStudioExtension/ModernFormsDesignerPackage.cs");
        AssertRegistrationVersion("ModernFormsNext.VisualStudioExtension.Vsix/ModernFormsDesignerPackage.cs");
    }

    [Fact]
    public void PublishedPackagePolicyRequiresReadmeIconXmlDocsAndSymbols()
    {
        XDocument properties = LoadXml("Directory.Build.props");
        Assert.Equal("README.md", ElementValue(properties, "PackageReadmeFile"));
        Assert.Equal("icon.png", ElementValue(properties, "PackageIcon"));
        Assert.Equal("true", ElementValue(properties, "IncludeSymbols"));
        Assert.Equal("snupkg", ElementValue(properties, "SymbolPackageFormat"));
        Assert.Equal("true", ElementValue(properties, "PublishRepositoryUrl"));

        foreach (string projectPath in PackableProjects.Where(path => !path.Contains("Templates", StringComparison.Ordinal)))
        {
            XDocument project = LoadXml(projectPath);
            Assert.Equal("true", ElementValue(project, "GenerateDocumentationFile"));
            Assert.True(project.Descendants().Any(HasPackedFile("README.md")), $"{projectPath} must pack README.md.");
            Assert.True(project.Descendants().Any(HasPackedFile("icon.png")), $"{projectPath} must pack icon.png.");
        }

        XDocument template = LoadXml("ModernFormsNext.Templates/ModernFormsNext.Templates.csproj");
        Assert.Equal("false", ElementValue(template, "IncludeSymbols"));
        Assert.True(template.Descendants().Any(HasPackedFile("README.md")), "Template package must pack README.md.");
        Assert.True(template.Descendants().Any(HasPackedFile("icon.png")), "Template package must pack icon.png.");
    }

    [Fact]
    public void TemplatePackageExcludesBuildAndIdeArtifacts()
    {
        XDocument template = LoadXml("ModernFormsNext.Templates/ModernFormsNext.Templates.csproj");
        XElement content = Assert.Single(
            template.Descendants("Content"),
            element => (element.Attribute("Include")?.Value ?? string.Empty).StartsWith("templates", StringComparison.Ordinal));
        string exclude = content.Attribute("Exclude")?.Value ?? string.Empty;

        foreach (string requiredPattern in new[] { "bin", "obj", ".vs", "*.user", "*.suo", "*.tmp", "*.cache" })
            Assert.Contains(requiredPattern, exclude, StringComparison.OrdinalIgnoreCase);
    }

    private static Func<XElement, bool> HasPackedFile(string fileName)
        => element => string.Equals(element.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            && (element.Attribute("Include")?.Value ?? string.Empty).EndsWith(fileName, StringComparison.OrdinalIgnoreCase);

    private static void AssertRegistrationVersion(string relativePath)
    {
        string text = File.ReadAllText(IOPath.Combine(RepositoryFileEnumerator.FindRepositoryRoot(), relativePath.Replace('/', IOPath.DirectorySeparatorChar)));
        Assert.Contains($"InstalledProductRegistration", text, StringComparison.Ordinal);
        Assert.Contains($"\"{ExpectedVersion}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"1.8.0\"", text, StringComparison.Ordinal);
    }

    private static XDocument LoadXml(string relativePath)
        => LoadXml(RepositoryFileEnumerator.FindRepositoryRoot(), relativePath);

    private static XDocument LoadXml(string root, string relativePath)
        => XDocument.Load(IOPath.Combine(root, relativePath.Replace('/', IOPath.DirectorySeparatorChar)));

    private static string? ElementValue(XDocument document, string name)
        => document.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim();

}
