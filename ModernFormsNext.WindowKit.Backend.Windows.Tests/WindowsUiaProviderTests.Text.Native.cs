using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public partial class WindowsUiaProviderTests
{
    [Fact]
    public async Task RealHwndTextRangesCrossProcessReadSelectScrollAndRespectPrivacy()
    {
        if (!OperatingSystem.IsWindows()) return;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string rootPath = FindRepositoryRoot();
        string Tool(string name) => System.IO.Path.Combine(rootPath, name, "bin", configuration, "net10.0-windows", name + ".dll");
        string hostPath = Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost");
        string clientPath = Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient");
        Assert.True(File.Exists(hostPath)); Assert.True(File.Exists(clientPath));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using Process host = StartTextProcess(hostPath, "--text");
        var hostErrors = host.StandardError.ReadToEndAsync();
        IntPtr hwnd = IntPtr.Zero;
        try {
            string? line = await ReadLineStartingWithAsync(host, "HWND:", timeout.Token);
            Assert.NotNull(line);
            Assert.True(long.TryParse(line.AsSpan(5), out long raw));
            hwnd = new(raw);
            using Process client = StartTextProcess(clientPath, "--text", raw.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var outputTask = client.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = client.StandardError.ReadToEndAsync(timeout.Token);
            try { await client.WaitForExitAsync(timeout.Token); }
            finally { if (!client.HasExited) { client.Kill(entireProcessTree: true); await client.WaitForExitAsync(); } }
            string output = await outputTask, error = await errorTask;
            Assert.True(client.ExitCode == 0, $"Native Text client failed: {error}");
            using var result = JsonDocument.Parse(output);
            foreach (string property in new[] {
                "FullText", "CloneMatches", "EndpointOrder", "EnclosingElement", "NoEmbeddedObjects",
                "MixedFormatting", "AttributeSearch", "VisibleGeometry", "PointRange", "Selection",
                "CharacterNavigation", "WordNavigation", "InitiallyScrolledOut", "ScrollRevealed",
                "SelectionPreserved", "FocusPreserved", "VisibleRanges", "ReadDidNotEdit",
                "RetainedRangeRebased", "ReadOnlyAttribute", "ReadOnlySelection", "PasswordFlag",
                "FreshTextUnavailable", "RetainedTextDenied", "RetainedGeometryDenied", "RemovedRangeDenied" })
                Assert.True(result.RootElement.GetProperty(property).GetBoolean(), property);
        }
        finally {
            if (hwnd != IntPtr.Zero) _ = PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await host.WaitForExitAsync(cleanup.Token); }
            catch (OperationCanceledException) when (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
        }
        Assert.True(host.ExitCode == 0, await hostErrors);
    }

    private static Process StartTextProcess(string assembly, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(assembly);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start native Text test process.");
    }
}
