using System.Diagnostics;
using System.Text.Json;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ModernFormsNext.Automation.Windows.Tests;

internal sealed class ProcessFixture : IAsyncDisposable
{
    internal Process Process { get; }
    internal AutomationApplicationInfo? Application { get; private set; }
    internal string RootId { get; private set; } = "";
    internal string OtherRootId { get; private set; } = "";
    internal AutomationNodeHandle Password { get; private set; }
    internal List<string> Output { get; } = [];
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ProcessFixture(string[] arguments)
    {
        Process = new() { StartInfo = new ProcessStartInfo(Executable("ModernFormsNext.Automation.Windows.TestHost"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (string argument in arguments) Process.StartInfo.ArgumentList.Add(argument);
        Process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (Output) Output.Add(e.Data);
            if (e.Data.StartsWith("READY:", StringComparison.Ordinal))
            {
                using var json = JsonDocument.Parse(e.Data[6..]); var root = json.RootElement;
                Application = root.GetProperty("Application").Deserialize<AutomationApplicationInfo>();
                RootId = root.GetProperty("RootId").GetString()!; OtherRootId = root.GetProperty("OtherRootId").GetString()!;
                Password = root.GetProperty("Password").Deserialize<AutomationNodeHandle>(); ready.TrySetResult();
            }
            if (e.Data.StartsWith("START-ERROR:", StringComparison.Ordinal)) ready.TrySetException(new Exception(e.Data));
            if (e.Data.StartsWith("SERVER-STOPPED:", StringComparison.Ordinal)) stopped.TrySetResult();
        };
        Process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (Output) Output.Add(e.Data); };
        Process.Start(); Process.BeginOutputReadLine(); Process.BeginErrorReadLine();
    }

    internal static async Task<ProcessFixture> Start(params string[] arguments)
    {
        var host = new ProcessFixture(arguments);
        try { await host.ready.Task.WaitAsync(TimeSpan.FromSeconds(20)); return host; }
        catch { await host.DisposeAsync(); throw; }
    }
    internal async Task Send(string command) { await Process.StandardInput.WriteLineAsync(command); await Process.StandardInput.FlushAsync(); }
    internal async Task StopServer() { await Send("stop-server"); await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
    internal Task<WindowsAutomationClient> Connect(AutomationCapability capabilities = AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions)
        => WindowsAutomationClient.ConnectAsync(Application!, capabilities);

    internal static string Executable(string project)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        return System.IO.Path.Combine(directory.FullName, project, "bin", configuration, "net10.0-windows", project + ".exe");
    }
    public async ValueTask DisposeAsync()
    {
        if (!Process.HasExited)
        {
            try { await Send("quit"); await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch { if (!Process.HasExited) Process.Kill(true); await Process.WaitForExitAsync(); }
        }
        Process.Dispose();
    }
}
