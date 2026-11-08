using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Theory]
    [InlineData("small")]
    [InlineData("large")]
    public async Task NativePresentationPreservesIdentityBuffersAndExternalEventsFor100Cycles(string size)
    {
        if (!OperatingSystem.IsWindows()) return;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string root = FindRepositoryRoot();
        string Tool(string name) => System.IO.Path.Combine(root, name, "bin", configuration, "net10.0-windows", name + ".dll");
        string evidence = System.IO.Path.Combine(root, "artifacts", "native-presentation", configuration + "-" + size + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using Process host = StartGridProcess(Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost"), "--presentation-" + size);
        var errors = host.StandardError.ReadToEndAsync();
        Task<string>? samples = null;
        nint hwnd = 0;
        try {
            string? line = await ReadLineStartingWithAsync(host, "HWND:", timeout.Token);
            Assert.NotNull(line); hwnd = new nint(long.Parse(line[5..]));
            samples = host.StandardOutput.ReadToEndAsync();
            using Process client = StartGridProcess(Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient"), "--presentation", hwnd.ToString());
            var output = client.StandardOutput.ReadToEndAsync(); var clientErrors = client.StandardError.ReadToEndAsync();
            try { await client.WaitForExitAsync(timeout.Token); }
            finally { if (!client.HasExited) { client.Kill(entireProcessTree: true); await client.WaitForExitAsync(); } }
            string result = await output, error = await clientErrors;
            await File.WriteAllTextAsync(System.IO.Path.Combine(evidence, "client.json"), result);
            await File.WriteAllTextAsync(System.IO.Path.Combine(evidence, "client.stderr.log"), error);
            Assert.True(client.ExitCode == 0, $"{error}\nEvidence: {evidence}");
            using var json = JsonDocument.Parse(result);
            Assert.Equal(200, json.RootElement.GetProperty("delivered").GetInt32());
        } finally {
            if (hwnd != 0) _ = PostMessage(hwnd, 0x0010, 0, 0);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await host.WaitForExitAsync(cleanup.Token); }
            catch (OperationCanceledException) when (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
            await File.WriteAllTextAsync(System.IO.Path.Combine(evidence, "host.samples.log"), samples is null ? "No HWND" : await samples);
            await File.WriteAllTextAsync(System.IO.Path.Combine(evidence, "host.stderr.log"), await errors);
        }
        Assert.True(host.ExitCode == 0, $"Native host failed. Evidence: {evidence}");
    }
}
