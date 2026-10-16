using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsUiCollection.Name)]
public sealed class WindowsVirtualizationCertificationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PacedNativeHostCertifiesListMetricsAndRetainedMemory()
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
        start.ArgumentList.Add("--virtualization-certification");
        using Process host = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native certification host.");
        // The new explicit lane includes 120 one-second message-loop intervals. The pre-existing
        // 30-second native virtualization regression and its assertions remain unchanged.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var stdout = host.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = host.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await host.WaitForExitAsync(timeout.Token);
            string text = await stdout, errors = await stderr;
            output.WriteLine(text);
            Assert.True(host.ExitCode == 0, $"Certification host exited {host.ExitCode}: {errors}\n{text}");
            Assert.Contains("VIRTUALIZATION_CERTIFICATION:NATIVE:", text);
            Assert.Contains("VIRTUALIZATION_CERTIFICATION:PASS", text);
            Assert.Contains("SOAK steps=6500", text);
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
