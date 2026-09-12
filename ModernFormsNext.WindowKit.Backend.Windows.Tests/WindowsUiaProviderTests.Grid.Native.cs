using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public async Task RealHwndGridAndCalendarExposeLiveTableEditSelectionAndPopupLifetime()
    {
        if (!OperatingSystem.IsWindows()) return;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string rootPath = FindRepositoryRoot();
        string Tool(string name) => System.IO.Path.Combine(rootPath, name, "bin", configuration, "net10.0-windows", name + ".dll");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using Process host = StartGridProcess(Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost"), "--grid-calendar");
        var hostErrors = host.StandardError.ReadToEndAsync();
        IntPtr hwnd = IntPtr.Zero;
        try
        {
            string? line = await ReadLineStartingWithAsync(host, "HWND:", timeout.Token);
            Assert.NotNull(line);
            Assert.True(long.TryParse(line.AsSpan(5), out long raw));
            hwnd = new(raw);
            using Process client = StartGridProcess(Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient"),
                "--grid-calendar", raw.ToString(CultureInfo.InvariantCulture));
            var outputTask = client.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = client.StandardError.ReadToEndAsync(timeout.Token);
            try { await client.WaitForExitAsync(timeout.Token); }
            finally { if (!client.HasExited) { client.Kill(entireProcessTree: true); await client.WaitForExitAsync(); } }
            string output = await outputTask, error = await errorTask;
            Assert.True(client.ExitCode == 0, $"Native Grid/Calendar client failed: {error}\n{output}");
            using var result = JsonDocument.Parse(output);
            foreach (string property in new[] {
                "Counts", "HeaderMetadata", "ItemMetadata", "EditCommitted", "Selection", "TailInitiallyOffscreen", "Revealed",
                "IdentityAfterSort", "ReadOnly", "RemovedPeerDenied", "UncheckedStillEnabled", "CheckboxReenabled", "Expanded",
                "NoDuplicateCalendar", "CalendarMetadata", "CalendarCommitted", "RetiredCalendarDenied" })
                Assert.True(result.RootElement.GetProperty(property).GetBoolean(), $"{property}: {output}");
        }
        finally
        {
            if (hwnd != IntPtr.Zero) _ = PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await host.WaitForExitAsync(cleanup.Token); }
            catch (OperationCanceledException) when (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
        }
        Assert.True(host.ExitCode == 0, await hostErrors);
    }

    private static Process StartGridProcess(string assembly, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(assembly);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start native Grid/Calendar test process.");
    }
}
