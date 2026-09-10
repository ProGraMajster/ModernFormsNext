using System.Diagnostics;
using System.Text.Json;
using Xunit;
using IOPath = System.IO.Path;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsNativeApplicationLifecycleTests
{
    [Fact]
    public async Task RealNativeMessagesDriveOneApplicationLoopAndItsRegisteredAnimationScheduler()
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
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost", "bin", configuration, "net10.0-windows",
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll");
        Assert.True(File.Exists(hostPath), $"The native lifecycle host was not built: {hostPath}");
        var start = new ProcessStartInfo("dotnet")
        {
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(hostPath);
        start.ArgumentList.Add("--lifecycle");
        using Process host = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native lifecycle host.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<string> outputTask = host.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> errorTask = host.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await host.WaitForExitAsync(timeout.Token);
            string output = await outputTask;
            string errors = await errorTask;
            Assert.True(host.ExitCode == 0, $"Native lifecycle host exited with {host.ExitCode}: {errors}\n{output}");
            string line = Assert.Single(output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                item => item.StartsWith("LIFECYCLE:", StringComparison.Ordinal));
            using JsonDocument result = JsonDocument.Parse(line["LIFECYCLE:".Length..]);
            JsonElement root = result.RootElement;
            Assert.Equal(1, root.GetProperty("Activations").GetInt32());
            Assert.Equal(1, root.GetProperty("ExitCalls").GetInt32());
            Assert.True(root.GetProperty("TwoWindowsObserved").GetBoolean());
            Assert.True(root.GetProperty("WindowActivationObserved").GetBoolean());
            Assert.True(root.GetProperty("InactiveAnimationContinues").GetBoolean());
            Assert.True(root.GetProperty("SuspendPausesAnimation").GetBoolean());
            Assert.True(root.GetProperty("ResumeRestartsAnimation").GetBoolean());
            Assert.Equal("Exited", root.GetProperty("FinalPhase").GetString());
            Assert.Equal(new[] { "Phase:Exiting", "OnExit", "Phase:Exited" },
                root.GetProperty("ExitOrder").EnumerateArray().Select(item => item.GetString()));
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
