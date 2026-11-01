using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

// Child processes still share desktop focus, input and visible pixels with other native hosts.
// Use a nonparallel process collection so their checks cannot overlap another collection's windows.
[Collection(WindowsNativeProcessCollection.Name)]
public sealed class WindowsNativeFocusTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("--focus", "FOCUS:PASS")]
    [InlineData("--tooltips", "TOOLTIP:PASS:HWND")]
    [InlineData("--native-view", "NATIVE_VIEW:PASS:")]
    [InlineData("--pages", "PAGE_NAVIGATION:PASS:")]
    [InlineData("--page-composition", "PAGE_COMPOSITION:PASS:")]
    [InlineData("--app-shell", "APP_SHELL:PASS:")]
    [InlineData("--shadow-typography", "SHADOW_TYPOGRAPHY:PASS:")]
    [InlineData("--layout-policy", "LAYOUT_POLICY:PASS:")]
    [InlineData("--layout-policy-immediate", "LAYOUT_POLICY:PASS:")]
    [InlineData("--visual-state-metrics", "VISUAL_STATE_METRICS:PASS:")]
    [InlineData("--visual-state-metrics-immediate", "VISUAL_STATE_METRICS:PASS:")]
    [InlineData("--brush-morph", "BRUSH_MORPH:PASS:")]
    [InlineData("--brush-morph-immediate", "BRUSH_MORPH:PASS:")]
    public async Task CanonicalFocusAndTextHandoffUseRealHwndAndRetireOnHideAndClose(string argument, string marker)
    {
        if (!OperatingSystem.IsWindows()) return;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string hostPath = System.IO.Path.Combine(directory.FullName,
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost", "bin", configuration,
            "net10.0-windows", "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll");
        var start = new ProcessStartInfo("dotnet") {
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(hostPath);
        start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native focus host.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            string report = await stdout, errors = await stderr;
            output.WriteLine(report);
            Assert.True(process.ExitCode == 0, $"Native focus host failed: {errors}\n{report}");
            Assert.Contains(marker, report);
            if (argument == "--tooltips")
                Assert.Contains("TOOLTIP:NATIVE_ALPHA_PIXELS_FOCUS_MENU:PASS", report);
            if (argument == "--layout-policy-immediate")
                Assert.Contains("native-policy-immediate=True", report);
            if (argument == "--visual-state-metrics-immediate")
                Assert.Contains("VISUAL_STATE_METRICS immediate=True", report);
            if (argument == "--brush-morph-immediate")
                Assert.Contains("BRUSH_MORPH immediate=True", report);
            if (argument == "--shadow-typography")
            {
                Assert.Contains("DYNAMIC_RESOURCE:NATIVE:11:PASS:typed-merged-factory-replacement", report);
                Assert.Contains("RTL_LAYOUT:PASS:", report);
                Assert.Contains("RTL_LAYOUT:NATIVE_PAINT_TAB_SHIFT_TAB:PASS", report);
                Assert.Contains("BINDING_NAVIGATOR:PASS:", report);
                Assert.Contains("BINDING_NAVIGATOR:NATIVE_PAINT_ENTER_TAB_SHIFT_TAB:PASS", report);
            }
            if (argument == "--focus")
            {
                Assert.Contains("FOCUS:HWND_TEXT_HANDOFF:False:PASS", report);
                Assert.Contains("FOCUS:HWND_TEXT_HANDOFF:True:PASS", report);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanup.Token);
            }
        }
    }
}
