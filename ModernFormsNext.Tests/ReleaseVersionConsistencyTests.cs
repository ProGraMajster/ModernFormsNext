using System.Xml.Linq;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ReleaseVersionConsistencyTests
{
    private const string ExpectedVersion = "1.11.1";

    private static readonly string[] PackableProjects =
    [
        "ModernFormsNext/ModernFormsNext.csproj",
        "ModernFormsNext.Automation/ModernFormsNext.Automation.csproj",
        "ModernFormsNext.Automation.Windows/ModernFormsNext.Automation.Windows.csproj",
        "ModernFormsNext.CodeGeneration/ModernFormsNext.CodeGeneration.csproj",
        "ModernFormsNext.Designer/ModernFormsNext.Designer.csproj",
        "ModernFormsNext.Designing/ModernFormsNext.Designing.csproj",
        "ModernFormsNext.Testing/ModernFormsNext.Testing.csproj",
        "ModernFormsNext.SystemNotifications.AppSdk/ModernFormsNext.SystemNotifications.AppSdk.csproj",
        "ModernFormsNext.SystemNotifications.WinRT/ModernFormsNext.SystemNotifications.WinRT.csproj",
        "ModernFormsNext.Templates/ModernFormsNext.Templates.csproj",
        "ModernFormsNext.WindowKit/ModernFormsNext.WindowKit.csproj",
        "ModernFormsNext.WindowKit.Backend/ModernFormsNext.WindowKit.Backend.csproj",
        "ModernFormsNext.WindowKit.Backend.Android/ModernFormsNext.WindowKit.Backend.Android.csproj",
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
    public void PatchReleasePreservesLibraryAssemblyIdentity()
    {
        // Inspect the built framework: package version defaults must not silently change
        // the assembly identity used by existing applications when installing a patch.
        Assert.Equal(new Version(1, 11, 0, 0), typeof(Control).Assembly.GetName().Version);
        Assert.Equal("1.11.1.0", System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(Control).Assembly.Location).FileVersion);
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
    public void PackageValidatorInventoryMatchesEveryPackableProject()
    {
        string script = File.ReadAllText(IOPath.Combine(RepositoryFileEnumerator.FindRepositoryRoot(), "scripts/Validate-ReleasePackages.ps1"));
        string[] ids = System.Text.RegularExpressions.Regex.Matches(script, "Id = \\\"(?<id>ModernFormsNext[^\\\"]*)\\\"")
            .Select(match => match.Groups["id"].Value).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(PackableProjects.Select(path => IOPath.GetFileNameWithoutExtension(path)).Order(StringComparer.Ordinal), ids);
    }

    [Fact]
    public void AndroidPackagePreservesBothSurfacesAndExperimentalPolicy()
    {
        XDocument project = LoadXml("ModernFormsNext.WindowKit.Backend.Android/ModernFormsNext.WindowKit.Backend.Android.csproj");
        Assert.Equal("true", ElementValue(project, "IsPackable"));
        Assert.Equal("false", ElementValue(project, "GeneratePackageOnBuild"));
        Assert.Equal("ModernFormsNext.WindowKit.Backend.Android", ElementValue(project, "PackageId"));
        Assert.Equal("net10.0;net10.0-android", ElementValue(project, "TargetFrameworks"));
        Assert.Equal("23.0", ElementValue(project, "SupportedOSPlatformVersion"));
        Assert.Contains("Experimental", ElementValue(project, "Description"), StringComparison.Ordinal);
        Assert.Contains("experimental", ElementValue(project, "PackageTags"), StringComparison.Ordinal);
        Assert.NotEqual("true", ElementValue(project, "DevelopmentDependency"));
        XDocument manifest = LoadXml("ModernFormsNext.WindowKit.Backend.Android/Properties/AndroidManifest.xml");
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XElement sdk = Assert.Single(manifest.Root!.Elements("uses-sdk"));
        Assert.Equal("23", sdk.Attribute(android + "minSdkVersion")?.Value);
        Assert.Equal("36", sdk.Attribute(android + "targetSdkVersion")?.Value);
        Assert.Empty(manifest.Root.Elements("uses-permission"));
        Assert.Empty(manifest.Root.Elements("application"));
        string script = File.ReadAllText(IOPath.Combine(RepositoryFileEnumerator.FindRepositoryRoot(), "scripts/Validate-ReleasePackages.ps1"));
        Assert.Contains("Id = \"ModernFormsNext.WindowKit.Backend.Android\"; Frameworks = @(\"net10.0\", \"net10.0-android\"); Symbols = $true; Template = $false", script, StringComparison.Ordinal);
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
