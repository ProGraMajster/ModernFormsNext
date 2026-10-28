using System.Diagnostics;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsNativeProcessCollection.Name)]
public sealed class WindowsNativeVirtualizationTests
{
    [Fact]
    public async Task ActualHwndVirtualizes100kItemsAndRoutesNativeKeyboardWheelFocusAndMutations()
    {
        if (!OperatingSystem.IsWindows()) return;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var start = new ProcessStartInfo("dotnet")
        {
            CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost",
            "bin", configuration, "net10.0-windows", "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll"));
        start.ArgumentList.Add("--virtualization");
        using Process host = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native virtualization host.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var outputTask = host.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = host.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await host.WaitForExitAsync(timeout.Token);
            string output = await outputTask, errors = await errorTask;
            Assert.True(host.ExitCode == 0, $"Native virtualization host exited {host.ExitCode}: {errors}\n{output}");
            Assert.Contains("VIRTUALIZATION:NATIVE:", output);
            Assert.Contains("VIRTUALIZATION:PASS", output);
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
