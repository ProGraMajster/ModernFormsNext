using System.Diagnostics;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsNativeScrollLayoutTests
{
    [Fact]
    public async Task NativeFlowLayoutPreservesScrollGeometryAcrossVisibilityAndInput()
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
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost", "bin", configuration, "net10.0-windows",
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll");
        var start = new ProcessStartInfo("dotnet") {
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(hostPath);
        start.ArgumentList.Add("--scroll-layout");
        using Process host = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native scroll layout host.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string> outputTask = host.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> errorTask = host.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await host.WaitForExitAsync(timeout.Token);
            string output = await outputTask, errors = await errorTask;
            Assert.True(host.ExitCode == 0, $"Native scroll layout host exited with {host.ExitCode}: {errors}\n{output}");
            Assert.Contains("SCROLL_LAYOUT:PASS", output);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await host.WaitForExitAsync(cleanup.Token);
            }
        }
    }
}
