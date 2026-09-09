using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Security")]
public sealed class ProtocolPolicyTests
{
    [Theory]
    [InlineData("--inspect-session")]
    [InlineData("--inspect-server")]
    public async Task ServerAndSessionPoliciesRestrictNegotiation(string policy)
    {
        await using var host = await ProcessFixture.Start(policy); await using var client = await host.Connect();
        Assert.Equal(AutomationCapability.Inspect, client.Info.Capabilities);
        Assert.Equal(AutomationTransportError.CapabilityDenied,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => client.FindAllAsync(host.RootId, new()))).Error);
    }

    [Fact]
    public async Task RootPolicyRemainsCanonicalAfterTransportNegotiation()
    {
        await using var host = await ProcessFixture.Start("--inspect-root"); await using var client = await host.Connect();
        var root = Assert.Single((await client.GetRootsAsync()).Value, root => root.RootId == host.RootId);
        Assert.Equal(AutomationCapability.Inspect, root.Capabilities);
        Assert.Equal(AutomationErrorCode.CapabilityDenied, (await client.FindAllAsync(host.RootId, new())).Error);
        Assert.Equal(AutomationErrorCode.CapabilityDenied, (await client.PerformActionAsync(host.RootId, root.Handle, AccessibleActions.Invoke)).Error);
    }

    [Fact]
    public async Task DuplicateStartDoesNotCreateAnotherEndpointForTheSameSession()
    {
        await using var host = await ProcessFixture.Start("--double-start");
        Assert.Contains(host.Output, text => text.Contains("\"DuplicateRejected\":true", StringComparison.Ordinal));
        Assert.Single(await AutomationDiscovery.DiscoverAsync(), app => app.ProcessId == host.Process.Id);
    }

    [Fact]
    public async Task OnlyOneOutstandingRequestAndCancelDoesNotNeedItsOwnSlot()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        await raw.Handshake(host.Application!);
        await raw.Send(2, RequestKind.WaitForCondition, new Operation { RootId = host.RootId,
            Condition = new() { Query = new() { AutomationId = "absent" } } });
        var busy = await raw.Request(3, RequestKind.GetRoots, new { }); Assert.Equal(AutomationTransportError.Busy, busy.Error); Assert.Equal(3, busy.Id);
        await raw.Send(2, RequestKind.Cancel, new { });
        var cancelled = await raw.Read(); Assert.Equal(2, cancelled.Id);
        Assert.Equal(AutomationWaitStatus.Cancelled, Protocol.Read<AutomationWaitResult>(cancelled.Result!.Value).Status);
        Assert.Equal(AutomationTransportError.None, (await raw.Request(4, RequestKind.GetRoots, new { })).Error);
    }

    [Fact]
    public async Task RequestDeadlineIsDistinctFromCallerCancellation()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        await raw.Handshake(host.Application!);
        var result = await raw.Request(2, RequestKind.WaitForCondition, new Operation { RootId = host.RootId,
            Condition = new() { Query = new() { AutomationId = "absent" } } }, milliseconds: 50);
        Assert.Equal(AutomationTransportError.DeadlineExceeded, result.Error);
        Assert.Equal(AutomationTransportError.None, (await raw.Request(3, RequestKind.GetRoots, new { })).Error);
    }

    [Fact]
    public async Task ReplayingCompletedRequestIdIsRejected()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        await raw.Handshake(host.Application!); await raw.Request(2, RequestKind.GetRoots, new { });
        Assert.Equal(AutomationTransportError.InvalidRequest, (await raw.Request(2, RequestKind.GetRoots, new { })).Error);
    }
}
