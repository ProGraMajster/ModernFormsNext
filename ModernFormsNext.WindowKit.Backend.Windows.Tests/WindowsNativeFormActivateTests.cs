using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsNativeFormActivateTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ActivationRequestsUseRealWindowsAndPreserveLifetimeAndModality()
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
        start.ArgumentList.Add("--form-activate");
        using Process host = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native activation host.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string> outputTask = host.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> errorTask = host.StandardError.ReadToEndAsync(timeout.Token);
        try {
            await host.WaitForExitAsync(timeout.Token);
            string report = await outputTask, errors = await errorTask;
            output.WriteLine(report); // Preserve OBSERVED versus foreground-policy limitations in TRX.
            Assert.True(host.ExitCode == 0, $"Native activation host exited with {host.ExitCode}: {errors}\n{report}");
            foreach (bool chrome in new[] { false, true }) {
                Assert.Contains($"FORM_ACTIVATE:NATIVE:{chrome}:", report);
                Assert.Contains($"FORM_ACTIVATE:HIDDEN_CLOSED:{chrome}:PASS", report);
                Assert.Contains($"FORM_ACTIVATE:MODAL:{chrome}:PASS", report);
                foreach (string operation in new[] { "activate", "hide", "close" })
                    Assert.Contains($"FORM_ACTIVATE:REENTRANCY:{chrome}:{operation}:", report);
            }
            Assert.Contains("FORM_ACTIVATE:PASS", report);
        }
        finally {
            if (!host.HasExited) {
                host.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await host.WaitForExitAsync(cleanup.Token);
            }
        }
    }
}
