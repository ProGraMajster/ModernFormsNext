using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Process")]
public sealed class LiveProcessTests
{
    [Fact]
    public async Task RealWindowDiscoveryAuthQueryInvokeWaitInspectAndDisconnect()
    {
        await using var host = await ProcessFixture.Start();
        Assert.Contains(await AutomationDiscovery.DiscoverAsync(), x => x.InstanceId == host.Application!.InstanceId);
        await using var client = await host.Connect();
        var roots = await client.GetRootsAsync(); Assert.Equal(AutomationErrorCode.None, roots.Error); Assert.Equal(2, roots.Value.Length);
        var tree = await client.GetChildrenAsync(host.RootId, roots.Value[0].Handle); Assert.NotEmpty(tree.Value);
        var button = await client.FindOneAsync(host.RootId, new() { AutomationId = "invoke" }); Assert.Equal(AutomationErrorCode.None, button.Error);
        var action = await client.PerformActionAsync(host.RootId, button.Value!.Handle, AccessibleActions.Invoke);
        Assert.Equal(AutomationActionStatus.Accepted, action.Status);
        var wait = await client.WaitForConditionAsync(host.RootId, new()
        { Kind = AutomationWaitKind.ValueEquals, Query = new() { AutomationId = "status" }, Value = "Invoked:1" });
        Assert.Equal(AutomationWaitStatus.Satisfied, wait.Status);
        Assert.Equal("Invoked:1", (await client.InspectAsync(host.RootId, wait.Snapshot!.Handle)).Value!.Value);
        Assert.Equal(AutomationErrorCode.None, await client.CheckpointAsync());
        await client.DisconnectAsync();
    }

    [Fact]
    public async Task AsyncAcceptedRemainsPendingUntilObservablePostcondition()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var button = await client.FindOneAsync(host.RootId, new() { AutomationId = "async" });
        Assert.Equal(AutomationActionStatus.Accepted, (await client.PerformActionAsync(host.RootId, button.Value!.Handle, AccessibleActions.Invoke)).Status);
        Assert.Equal("Pending", (await client.FindOneAsync(host.RootId, new() { AutomationId = "status" })).Value!.Value);
        var wait = client.WaitForConditionAsync(host.RootId, new()
        { Kind = AutomationWaitKind.ValueEquals, Query = new() { AutomationId = "status" }, Value = "Completed" });
        Assert.False(wait.IsCompleted); await host.Send("complete");
        var done = await wait; Assert.Equal(AutomationWaitStatus.Satisfied, done.Status);
        Assert.Equal("Completed", (await client.InspectAsync(host.RootId, done.Snapshot!.Handle)).Value!.Value);
    }

    [Fact]
    public async Task ReferenceWithoutStartHasNoDiscoveryEvenInRelease()
    {
        await using var host = await ProcessFixture.Start("--no-server");
        Assert.Null(host.Application);
        Assert.DoesNotContain(await AutomationDiscovery.DiscoverAsync(), x => x.ProcessId == host.Process.Id);
    }
}
