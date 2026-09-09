using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Process")]
[Trait("Category", "Lifetime")]
public sealed class LifetimeTests
{
    [Fact]
    public async Task TwoProcessesWithTheSameNameHaveDistinctDiscoverableIdentity()
    {
        await using var first = await ProcessFixture.Start(); await using var second = await ProcessFixture.Start();
        Assert.Equal(first.Application!.ApplicationName, second.Application!.ApplicationName);
        Assert.NotEqual(first.Application.InstanceId, second.Application.InstanceId); Assert.NotEqual(first.Process.Id, second.Process.Id);
        var discovered = await AutomationDiscovery.DiscoverAsync();
        Assert.Contains(first.Application, discovered); Assert.Contains(second.Application, discovered);
        await using var a = await first.Connect(); await using var b = await second.Connect();
        Assert.NotEqual(a.Info.SessionId, b.Info.SessionId);
    }

    [Fact]
    public async Task CanonicalStaleAndUnsupportedErrorsSurviveTransport()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var input = (await client.FindOneAsync(host.RootId, new() { AutomationId = "input" })).Value!;
        Assert.Equal(AutomationErrorCode.StaleNode, (await client.InspectAsync(host.RootId, input.Handle with { SessionId = "stale" })).Error);
        Assert.Equal(AutomationErrorCode.ActionUnsupported, (await client.PerformActionAsync(host.RootId, input.Handle, AccessibleActions.Invoke)).Error);
        await host.Send("remove-input");
        var gone = await client.WaitForConditionAsync(host.RootId, new() { Kind = AutomationWaitKind.NodeNotExposed, Handle = input.Handle });
        Assert.Equal(AutomationWaitStatus.Satisfied, gone.Status);
        Assert.Equal(AutomationErrorCode.NodeUnavailable, (await client.InspectAsync(host.RootId, input.Handle)).Error);
    }

    [Fact]
    public async Task RealWindowCloseSatisfiesRootEnded()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var wait = client.WaitForConditionAsync(host.OtherRootId, new() { Kind = AutomationWaitKind.RootEnded });
        await host.Send("close-root"); Assert.Equal(AutomationWaitStatus.Satisfied, (await wait).Status);
        Assert.DoesNotContain((await client.GetRootsAsync()).Value, root => root.RootId == host.OtherRootId);
    }

    [Fact]
    public async Task TimeoutAndCancellationHaveStructuredWaitResultsAndReleaseRequestSlot()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var condition = new AutomationWaitCondition { Query = new() { AutomationId = "not-yet" } };
        Assert.Equal(AutomationWaitStatus.TimedOut,
            (await client.WaitForConditionAsync(host.RootId, condition, new() { Timeout = TimeSpan.FromMilliseconds(50) })).Status);
        using var cancellation = new CancellationTokenSource();
        var pending = client.WaitForConditionAsync(host.RootId, condition, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        try { Assert.Equal(AutomationWaitStatus.Cancelled, (await pending).Status); }
        catch (AutomationTransportException error) { Assert.Equal(AutomationTransportError.Cancelled, error.Error); }
        Assert.Equal(AutomationErrorCode.None, (await client.GetRootsAsync()).Error);
    }

    [Fact]
    public async Task DisconnectEndsWaitButServerAndBorrowedSessionRemainAlive()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var pending = client.WaitForConditionAsync(host.RootId, new() { Query = new() { AutomationId = "absent" } });
        await client.DisconnectAsync();
        var error = await Assert.ThrowsAsync<AutomationTransportException>(() => pending);
        Assert.Contains(error.Error, new[] { AutomationTransportError.SessionEnded, AutomationTransportError.ApplicationUnavailable });
        await client.DisposeAsync(); await client.DisconnectAsync();
        Assert.Equal(AutomationTransportError.SessionEnded,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => client.GetRootsAsync())).Error);
        await using var next = await Reconnect(host);
        Assert.Equal(client.Info.SessionId, next.Info.SessionId);
        Assert.Equal(AutomationErrorCode.None, (await next.GetRootsAsync()).Error);
    }

    [Fact]
    public async Task ServerStopEndsWaitRemovesFilesAndPreservesBorrowedCore()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var pending = client.WaitForConditionAsync(host.RootId, new() { Query = new() { AutomationId = "absent" } });
        await host.StopServer();
        var error = await Assert.ThrowsAsync<AutomationTransportException>(() => pending);
        Assert.Contains(error.Error, new[] { AutomationTransportError.SessionEnded, AutomationTransportError.ApplicationUnavailable });
        Assert.False(File.Exists(AutomationDiscovery.DescriptorPath(host.Application!.InstanceId)));
        Assert.False(File.Exists(AutomationDiscovery.AuthPath(host.Application.InstanceId)));
        Assert.Contains("SERVER-STOPPED:True", host.Output);
        Assert.Equal(AutomationTransportError.ApplicationUnavailable, (await Assert.ThrowsAsync<AutomationTransportException>(() => host.Connect())).Error);
    }

    [Fact]
    public async Task ProcessKillEndsWaitAndNextDiscoveryRemovesStaleRecords()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect();
        var pending = client.WaitForConditionAsync(host.RootId, new() { Query = new() { AutomationId = "absent" } });
        host.Process.Kill(true); await host.Process.WaitForExitAsync();
        Assert.Equal(AutomationTransportError.ApplicationUnavailable, (await Assert.ThrowsAsync<AutomationTransportException>(() => pending)).Error);
        Assert.DoesNotContain(await AutomationDiscovery.DiscoverAsync(), x => x.InstanceId == host.Application!.InstanceId);
        Assert.False(File.Exists(AutomationDiscovery.DescriptorPath(host.Application!.InstanceId)));
    }

    [Theory]
    [InlineData(AccessibleActions.Invoke)]
    [InlineData(AccessibleActions.SetValue)]
    public async Task ActionWhichKillsProcessHasUnknownOutcomeAndIsNeverRetried(AccessibleActions action)
    {
        await using var host = await ProcessFixture.Start("--crash-setvalue"); await using var client = await host.Connect();
        var target = (await client.FindOneAsync(host.RootId, new()
        { AutomationId = action == AccessibleActions.Invoke ? "crash-action" : "crash-value" })).Value!;
        var error = await Assert.ThrowsAsync<AutomationTransportException>(() => client.PerformActionAsync(host.RootId, target.Handle, action,
            action == AccessibleActions.SetValue ? AutomationActionValue.FromText("UNKNOWN-OUTCOME-PRIVATE-97") : null));
        Assert.Equal(AutomationTransportError.OutcomeUnknown, error.Error);
        await host.Process.WaitForExitAsync(); Assert.Single(host.Output, x => x == "ACTION-ENTERED");
        Assert.DoesNotContain("UNKNOWN-OUTCOME-PRIVATE-97", string.Join("", host.Output));
        Assert.Equal(AutomationTransportError.SessionEnded,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => client.GetRootsAsync())).Error);
        await AutomationDiscovery.DiscoverAsync();
    }

    private static async Task<WindowsAutomationClient> Reconnect(ProcessFixture host)
    {
        // EOF cancels the old server wait asynchronously. Retry only connection acquisition,
        // bounded by monotonic time; never retry an action or introduce a fixed sleep.
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        while (true)
        {
            try { return await host.Connect(); }
            catch (AutomationTransportException error) when (error.Error == AutomationTransportError.Busy
                && System.Diagnostics.Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(5)) { await Task.Yield(); }
        }
    }
}
