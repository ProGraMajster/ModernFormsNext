namespace ModernFormsNext.CrossPlatform.Sample.Tests;

public sealed class AndroidToolingContractTests
{
    private static readonly string[] RequiredScripts =
    [
        "AndroidTools.psm1",
        "Resolve-AndroidSdk.ps1",
        "Resolve-Adb.ps1",
        "Get-AndroidDevices.ps1",
        "Get-AndroidAvds.ps1",
        "Start-AndroidEmulator.ps1",
        "Wait-AndroidDevice.ps1",
        "Build-CrossPlatformSample.ps1",
        "Install-CrossPlatformSample.ps1",
        "Launch-CrossPlatformSample.ps1",
        "Run-CrossPlatformSample.ps1",
        "Watch-ModernFormsNextLogcat.ps1",
        "Collect-AndroidDiagnostics.ps1",
        "Test-AndroidTooling.ps1"
    ];

    [Fact]
    public void RepositoryContainsTheCompleteAndroidCommandLineWorkflow()
    {
        foreach (var script in RequiredScripts)
            Assert.True(File.Exists(Path.Combine(AndroidScriptsDirectory, script)), $"Missing Android script: {script}");
    }

    [Fact]
    public void StandaloneBuildEmbedsAssembliesAndSignsTheApk()
    {
        var script = ReadScript("Build-CrossPlatformSample.ps1");

        Assert.Contains("SignAndroidPackage", script, StringComparison.Ordinal);
        Assert.Contains("EmbedAssembliesIntoApk=true", script, StringComparison.Ordinal);
        Assert.Contains("net10.0-android", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EmulatorDataIsWipedOnlyByAnExplicitSwitch()
    {
        var script = ReadScript("Start-AndroidEmulator.ps1");

        Assert.Contains("[switch]$WipeData", script, StringComparison.Ordinal);
        Assert.Contains("if ($WipeData)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'-wipe-data'", script[..script.IndexOf("if ($WipeData)", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void ToolingHasPureParsersTimeoutCancellationAndNoUserSpecificPath()
    {
        var module = File.ReadAllText(Path.Combine(AndroidScriptsDirectory, "AndroidTools.psm1"));

        Assert.Contains("ConvertFrom-AdbDevicesOutput", module, StringComparison.Ordinal);
        Assert.Contains("ConvertFrom-AvdListOutput", module, StringComparison.Ordinal);
        Assert.Contains("TimeoutSeconds", module, StringComparison.Ordinal);
        Assert.Contains("CancellationToken", module, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"C:\\Users\\[^\\]+", module);
    }

    [Fact]
    public void AndroidHostUsesTheSharedImePipelineWithoutNativeEditText()
    {
        var host = File.ReadAllText(Path.Combine(SampleDirectory, "Platforms", "Android", "AndroidAppHost.cs"));
        var page = File.ReadAllText(Path.Combine(SampleDirectory, "MainPage.cs"));

        Assert.Contains("controlSurface.SetComposingText", host, StringComparison.Ordinal);
        Assert.Contains("controlSurface.DeleteSurroundingText", host, StringComparison.Ordinal);
        Assert.DoesNotContain("EditText", host, StringComparison.Ordinal);
        Assert.Contains("ScrollableControl", page, StringComparison.Ordinal);
        Assert.Contains("MultiLine = true", page, StringComparison.Ordinal);
        Assert.Contains("Zażółć gęślą jaźń", page, StringComparison.Ordinal);
    }

    private static string ReadScript(string name)
        => File.ReadAllText(Path.Combine(AndroidScriptsDirectory, name));

    private static string AndroidScriptsDirectory => Path.Combine(RepositoryRoot, "scripts", "android");

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
}
