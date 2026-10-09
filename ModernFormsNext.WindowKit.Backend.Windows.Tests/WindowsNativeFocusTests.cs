using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsNativeFocusTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("--focus", "FOCUS:PASS")]
    [InlineData("--native-view", "NATIVE_VIEW:PASS:")]
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
