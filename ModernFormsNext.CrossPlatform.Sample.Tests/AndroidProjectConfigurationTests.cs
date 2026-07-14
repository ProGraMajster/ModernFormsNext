using System.Xml.Linq;

namespace ModernFormsNext.CrossPlatform.Sample.Tests;

public sealed class AndroidProjectConfigurationTests
{
    private const string ApplicationId = "com.programajster.modernformsnext.sample";
    private const string ActivityName = ApplicationId + ".MainActivity";

    [Fact]
    public void ProjectIsOneNonMauiMultiTargetApplication()
    {
        var project = LoadProject();

        Assert.Equal(
            "net10.0-windows;net10.0-android",
            Property(project, "TargetFrameworks").Value);
        Assert.DoesNotContain(project.Descendants(), element => element.Name.LocalName == "UseMaui");
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "AndroidApplication"),
            element => element.Value == "true" && IsAndroidCondition(element));
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "OutputType"),
            element => element.Value == "Exe" && IsAndroidCondition(element));
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "OutputType"),
            element => element.Value == "WinExe" && ParentCondition(element).Contains("net10.0-windows", StringComparison.Ordinal));
    }

    [Fact]
    public void AndroidLaunchConfigurationDisablesOnlyUnsupportedHotReload()
    {
        var project = LoadProject();

        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "SupportsHotReload"),
            element => element.Value == "false" && IsAndroidCondition(element));
        Assert.DoesNotContain(project.Descendants(), element => element.Name.LocalName == "AndroidUseSharedRuntime");
        Assert.DoesNotContain(project.Descendants(), element => element.Name.LocalName == "UseMaui");
        Assert.DoesNotContain(project.Descendants(), element => element.Name.LocalName == "MinSdkVersion");
        Assert.DoesNotContain(project.Descendants(), element => element.Name.LocalName == "TargetSdkVersion");
    }

    [Fact]
    public void AndroidDebugAndReleaseUseIntentionalDeploymentModes()
    {
        var project = LoadProject();
        var groups = project.Root!.Elements().Where(element => element.Name.LocalName == "PropertyGroup").ToArray();
        var debug = Assert.Single(groups, group => ParentCondition(group).Contains("'$(Configuration)' == 'Debug'", StringComparison.Ordinal));
        var release = Assert.Single(groups, group => ParentCondition(group).Contains("'$(Configuration)' == 'Release'", StringComparison.Ordinal));

        Assert.Equal("false", Property(debug, "EmbedAssembliesIntoApk").Value);
        Assert.Equal("portable", Property(debug, "DebugType").Value);
        Assert.Equal("true", Property(debug, "DebugSymbols").Value);
        Assert.Equal("false", Property(debug, "RunAOTCompilation").Value);
        Assert.Equal("true", Property(release, "EmbedAssembliesIntoApk").Value);
        Assert.Equal("true", Property(release, "RunAOTCompilation").Value);
    }

    [Fact]
    public void PackageManifestAndLauncherComponentAreStable()
    {
        var project = LoadProject();
        Assert.Equal(ApplicationId, Property(project, "ApplicationId").Value);
        Assert.Equal("apk", Property(project, "AndroidPackageFormats").Value);
        Assert.Equal("23.0", Property(project, "SupportedOSPlatformVersion").Value);

        var mainActivity = File.ReadAllText(Path.Combine(SampleDirectory, "Platforms", "Android", "MainActivity.cs"));
        Assert.Contains($"Name = \"{ActivityName}\"", mainActivity, StringComparison.Ordinal);
        Assert.Contains("MainLauncher = true", mainActivity, StringComparison.Ordinal);
        Assert.Contains("Exported = true", mainActivity, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestDeclaresOnlyTheOptionalCameraPermission()
    {
        var manifest = XDocument.Load(Path.Combine(SampleDirectory, "Platforms", "Android", "AndroidManifest.xml"));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";

        var effectivePermissions = manifest.Root!.Elements("uses-permission")
            .Where(element => (string?)element.Attribute(tools + "node") != "remove")
            .Select(element => (string?)element.Attribute(android + "name") ?? string.Empty)
            .ToArray();

        Assert.Equal(["android.permission.CAMERA"], effectivePermissions);
        Assert.DoesNotContain("android.permission.READ_PHONE_STATE", effectivePermissions);
        Assert.DoesNotContain("android.permission.READ_EXTERNAL_STORAGE", effectivePermissions);
        Assert.DoesNotContain("android.permission.WRITE_EXTERNAL_STORAGE", effectivePermissions);
    }

    private static string SampleDirectory => Path.Combine(
        RepositoryRoot,
        "samples",
        "ModernFormsNext.CrossPlatform.Sample");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ModernFormsNext.slnx")))
                directory = directory.Parent;

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate the ModernFormsNext repository root.");
        }
    }

    private static XDocument LoadProject()
        => XDocument.Load(Path.Combine(SampleDirectory, "ModernFormsNext.CrossPlatform.Sample.csproj"));

    private static XElement Property(XContainer container, string name)
        => Assert.Single(container.Descendants(), element => element.Name.LocalName == name);

    private static bool IsAndroidCondition(XElement element)
        => ParentCondition(element).Contains("net10.0-android", StringComparison.Ordinal);

    private static string ParentCondition(XElement element)
        => (string?)element.Attribute("Condition")
            ?? (string?)element.Parent?.Attribute("Condition")
            ?? string.Empty;
}
