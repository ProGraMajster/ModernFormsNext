using System.Diagnostics;
using System.Text.Json;
using Xunit;
using IOPath = System.IO.Path;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsNativePerformanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativePaintResizeAndInputUseOneOptionalRecorderOnTheOwningThread(bool highDpi)
    {
        if (!OperatingSystem.IsWindows()) return;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(IOPath.Combine(directory.FullName, "ModernFormsNext.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string hostPath = IOPath.Combine(directory.FullName,
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost", "bin", configuration,
            "net10.0-windows", "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll");
        Assert.True(File.Exists(hostPath), $"The native host was not built: {hostPath}");
        var start = new ProcessStartInfo("dotnet")
        {
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(hostPath);
        start.ArgumentList.Add(highDpi ? "--high-dpi" : "--performance");
        using Process host = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native performance host.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Task<string> outputTask = host.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> errorTask = host.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await host.WaitForExitAsync(timeout.Token);
            string output = await outputTask;
            string errors = await errorTask;
            Assert.True(host.ExitCode == 0, $"Native host exited with {host.ExitCode}: {errors}\n{output}");
            if (highDpi) {
                string dpiLine = Assert.Single(output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                    item => item.StartsWith("HIGH_DPI:", StringComparison.Ordinal));
                using var dpiResult = JsonDocument.Parse(dpiLine["HIGH_DPI:".Length..]);
                Assert.Equal(8, dpiResult.RootElement.GetProperty("results").GetArrayLength());
                Assert.True(dpiResult.RootElement.GetProperty("maximized").GetBoolean());
                Assert.True(dpiResult.RootElement.GetProperty("restored").GetBoolean());
                return;
            }
            string line = Assert.Single(output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                item => item.StartsWith("PERFORMANCE:", StringComparison.Ordinal));
            using JsonDocument result = JsonDocument.Parse(line["PERFORMANCE:".Length..]);
            var root = result.RootElement;
            Assert.True(root.GetProperty("Frames").GetInt32() >= 2);
            Assert.True(root.GetProperty("Completed").GetBoolean());
            Assert.Equal("Windows", root.GetProperty("Backend").GetString());
            Assert.Equal("NativePaint", root.GetProperty("NativeBoundary").GetString());
            Assert.True(root.GetProperty("RecordingStopped").GetBoolean());
            Assert.True(root.GetProperty("InputEvents").GetInt64() >= 1);
            Assert.Equal(0, root.GetProperty("RecorderFailures").GetInt64());
            Assert.True(root.GetProperty("BackingGeneration").GetInt64() > 1);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await host.WaitForExitAsync(cleanupTimeout.Token);
            }
        }
    }
}
