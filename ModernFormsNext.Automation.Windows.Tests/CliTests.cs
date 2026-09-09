using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Cli")]
[Trait("Category", "Process")]
public sealed class CliTests
{
    [Fact]
    public async Task CliSmokeUsesSeparateProcessesForListRootsFindActionWaitAndInspect()
    {
        await using var host = await ProcessFixture.Start();
        var list = await Run(null, "list", "--json"); Assert.Equal(0, list.Exit); Assert.Contains(host.Application!.InstanceId, list.Output);
        var roots = await Target(host, "roots"); Assert.Equal(0, roots.Exit);
        var find = await Target(host, "find", "--automation-id", "invoke"); Assert.Equal(0, find.Exit);
        var handle = Handle(find.Output);
        var action = await Target(host, "action", "--session", handle.SessionId, "--node", handle.RuntimeId, "--action", "Invoke");
        Assert.Equal(0, action.Exit); Assert.Contains("Accepted", action.Output);
        var wait = await Target(host, "wait", "--condition", "ValueEquals", "--automation-id", "status", "--equals", "Invoked:1");
        Assert.Equal(0, wait.Exit); Assert.Contains("Satisfied", wait.Output);
        var status = await Target(host, "find", "--automation-id", "status"); var statusHandle = Handle(status.Output);
        var inspect = await Target(host, "inspect", "--session", statusHandle.SessionId, "--node", statusHandle.RuntimeId);
        Assert.Equal(0, inspect.Exit); Assert.Contains("Invoked:1", inspect.Output);
        SaveSmoke("cli-smoke", new[] { list, roots, find, action, wait, inspect });
    }

    [Fact]
    public async Task CliTreeHasBoundedDepthAndReadableText()
    {
        await using var host = await ProcessFixture.Start();
        var tree = await Target(host, "tree", "--depth", "0"); Assert.Equal(0, tree.Exit);
        using var json = JsonDocument.Parse(tree.Output); Assert.Single(json.RootElement.GetProperty("nodes").EnumerateArray());
        var text = await Run(null, "tree", "--instance", host.Application!.InstanceId, "--root", host.RootId, "--depth", "1");
        Assert.Equal(0, text.Exit); Assert.Contains("invoke", text.Output); Assert.DoesNotContain("PASSWORD-MARKER-97", text.Output);
    }

    [Fact]
    public async Task CliSetValueUsesStdinAndNeverEchoesTheProtectedParameter()
    {
        await using var host = await ProcessFixture.Start(); const string secret = "CLI-ACTION-PARAMETER-MARKER-97";
        var action = await Run(secret, "action", "--instance", host.Application!.InstanceId, "--root", host.RootId,
            "--session", host.Password.SessionId, "--node", host.Password.RuntimeId, "--action", "SetValue", "--value-stdin", "--json");
        Assert.Equal(0, action.Exit);
        var inspected = await Target(host, "inspect", "--session", host.Password.SessionId, "--node", host.Password.RuntimeId);
        Assert.Equal(0, inspected.Exit); Assert.DoesNotContain(secret, action.Output + action.Error + inspected.Output + string.Join("", host.Output));
    }

    [Fact]
    public async Task CliPrivacyOutputDoesNotExposeCustomGettersOrExceptions()
    {
        await using var host = await ProcessFixture.Start("--privacy");
        var tree = await Target(host, "tree"); Assert.Equal(3, tree.Exit);
        foreach (string marker in new[] { "PASSWORD-MARKER-97", "CUSTOM-GETTER-MARKER-97", "SENSITIVE-MARKER-97", "EXCEPTION-MARKER-97" })
            Assert.DoesNotContain(marker, tree.Output + tree.Error);
    }

    [Fact]
    public async Task CliAsyncWaitObservesCompletedWithoutClientSleep()
    {
        await using var host = await ProcessFixture.Start();
        var found = await Target(host, "find", "--automation-id", "async"); var handle = Handle(found.Output);
        var accepted = await Target(host, "action", "--session", handle.SessionId, "--node", handle.RuntimeId, "--action", "Invoke");
        Assert.Contains("Accepted", accepted.Output);
        var before = await Target(host, "find", "--automation-id", "status"); Assert.Contains("Pending", before.Output);
        var pending = Target(host, "wait", "--condition", "ValueEquals", "--automation-id", "status", "--equals", "Completed");
        Assert.False(pending.IsCompleted); await host.Send("complete");
        var done = await pending; Assert.Equal(0, done.Exit); Assert.Contains("Completed", done.Output);
        SaveSmoke("cli-async", new[] { accepted, before, done });
    }

    [Fact]
    public async Task CliRequiresExplicitIdentityAndSupportsPidSelection()
    {
        await using var first = await ProcessFixture.Start(); await using var second = await ProcessFixture.Start();
        var ambiguous = await Run(null, "info", "--json"); Assert.Equal(2, ambiguous.Exit); Assert.Contains("InstanceOrPidRequired", ambiguous.Output);
        var selected = await Run(null, "info", "--pid", second.Process.Id.ToString(), "--json");
        Assert.Equal(0, selected.Exit); Assert.Contains(second.Application!.InstanceId, selected.Output); Assert.DoesNotContain(first.Application!.InstanceId, selected.Output);
    }

    [Fact]
    public async Task CliUnknownMutationHasDistinctExitCode()
    {
        await using var host = await ProcessFixture.Start();
        var found = await Target(host, "find", "--automation-id", "crash-action"); var handle = Handle(found.Output);
        var action = await Target(host, "action", "--session", handle.SessionId, "--node", handle.RuntimeId, "--action", "Invoke");
        Assert.Equal(5, action.Exit); Assert.Contains("OutcomeUnknown", action.Output);
        await host.Process.WaitForExitAsync(); await AutomationDiscovery.DiscoverAsync();
    }

    [Fact]
    public async Task OversizedStdinFailsBeforeChangingTheTarget()
    {
        await using var host = await ProcessFixture.Start();
        var found = await Target(host, "find", "--automation-id", "input"); var handle = Handle(found.Output);
        var action = await Run(new string('x', 4097), "action", "--instance", host.Application!.InstanceId, "--root", host.RootId,
            "--session", handle.SessionId, "--node", handle.RuntimeId, "--action", "SetValue", "--value-stdin", "--json");
        Assert.Equal(2, action.Exit); Assert.DoesNotContain(new string('x', 100), action.Output + action.Error);
        Assert.Contains("Initial", (await Target(host, "inspect", "--session", handle.SessionId, "--node", handle.RuntimeId)).Output);
    }

    private static AutomationNodeHandle Handle(string output)
    {
        using var json = JsonDocument.Parse(output);
        var handle = json.RootElement.GetProperty("value").GetProperty("handle");
        return new(handle.GetProperty("sessionId").GetString()!, handle.GetProperty("runtimeId").GetString()!);
    }
    private static Task<CliResult> Target(ProcessFixture host, string command, params string[] arguments)
        => Run(null, [command, "--instance", host.Application!.InstanceId, "--root", host.RootId, "--json", .. arguments]);

    private static async Task<CliResult> Run(string? input, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(ProcessFixture.Executable("ModernFormsNext.Automation.Cli"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (input is not null) await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        return new(process.ExitCode, await stdout, await stderr);
    }

    private static void SaveSmoke(string name, IEnumerable<CliResult> results)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "ModernFormsNext.slnx"))) root = root.Parent;
        Assert.NotNull(root);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string directory = System.IO.Path.Combine(root.FullName, "artifacts", "issue-97-phase1b", "cli", configuration);
        Directory.CreateDirectory(directory);
        File.WriteAllText(System.IO.Path.Combine(directory, name + ".txt"), string.Join("\n", results.Select(result => $"Exit: {result.Exit}\n{result.Output}\n{result.Error}")));
    }
    private sealed record CliResult(int Exit, string Output, string Error);
}
