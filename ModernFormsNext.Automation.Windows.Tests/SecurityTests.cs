using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Text;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Security")]
public sealed class SecurityTests
{
    [Theory]
    [InlineData("bad")]
    [InlineData("missing")]
    [InlineData("instance")]
    public async Task InvalidAuthenticationCannotQuery(string failure)
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        var response = await raw.Handshake(host.Application!, failure == "bad" ? Convert.ToBase64String(new byte[32]) : null,
            omitSecret: failure == "missing", instance: failure == "instance" ? Guid.NewGuid().ToString("N") : null);
        Assert.Equal(AutomationTransportError.AuthenticationFailed, response.Error); Assert.Null(response.Result);
    }

    [Fact]
    public async Task SemanticRequestBeforeHandshakeIsRejected()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        Assert.Equal(AutomationTransportError.AuthenticationFailed, (await raw.Request(1, RequestKind.GetRoots, new { })).Error);
    }

    [Fact]
    public async Task IncompatibleProtocolIsExplicit()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        Assert.Equal(AutomationTransportError.ProtocolMismatch, (await raw.Handshake(host.Application!, version: 2)).Error);
    }

    [Fact]
    public async Task OnlyOneAuthenticatedClientOwnsTheServer()
    {
        await using var host = await ProcessFixture.Start(); await using var first = await host.Connect();
        var error = await Assert.ThrowsAsync<AutomationTransportException>(() => host.Connect());
        Assert.Equal(AutomationTransportError.Busy, error.Error);
        Assert.Equal(AutomationErrorCode.None, (await first.GetRootsAsync()).Error);
    }

    [Fact]
    public async Task NegotiationCannotGrantOrExecuteUnrequestedCapabilities()
    {
        await using var host = await ProcessFixture.Start(); await using var client = await host.Connect(AutomationCapability.Inspect);
        Assert.Equal(AutomationCapability.Inspect, client.Info.Capabilities);
        var roots = await client.GetRootsAsync();
        Assert.Equal(AutomationTransportError.CapabilityDenied,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => client.PerformActionAsync(host.RootId, roots.Value[0].Handle, AccessibleActions.Invoke))).Error);
        Assert.Equal(AutomationTransportError.CapabilityDenied,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => client.FindAllAsync(host.RootId, new()))).Error);
    }

    [Theory]
    [InlineData(0, AutomationTransportError.InvalidRequest)]
    [InlineData(-1, AutomationTransportError.InvalidRequest)]
    [InlineData(int.MaxValue, AutomationTransportError.PayloadTooLarge)]
    public async Task RealPipeRejectsInvalidFrameLength(int length, AutomationTransportError expected)
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, length);
        await raw.Pipe.WriteAsync(header, raw.Token);
        Assert.Equal(expected, (await raw.Read()).Error);
    }

    [Fact]
    public async Task RealPipeRejectsMalformedJsonWithoutEchoingInput()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        await Protocol.WriteFrame(raw.Pipe, Encoding.UTF8.GetBytes("{SECRET-MALFORMED-MARKER"), raw.Token);
        Assert.Equal(AutomationTransportError.InvalidRequest, (await raw.Read()).Error);
        Assert.DoesNotContain("SECRET-MALFORMED-MARKER", string.Join("", raw.Responses));
    }

    [Fact]
    public async Task SeparateStringBudgetRejectsOversizedActionValue()
    {
        await using var host = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(host.Application!);
        await raw.Handshake(host.Application!);
        var response = await raw.Request(2, RequestKind.PerformAction, new Operation { RootId = host.RootId, Text = new string('s', 4097) });
        Assert.Equal(AutomationTransportError.PayloadTooLarge, response.Error);
        Assert.DoesNotContain(new string('s', 100), string.Join("", raw.Responses));
    }

    [Fact]
    public async Task ResponseBudgetHasStructuredFailureAndConnectionRemainsUsable()
    {
        await using var host = await ProcessFixture.Start("--small-response"); await using var client = await host.Connect();
        Assert.Equal(AutomationTransportError.PayloadTooLarge,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => client.FindAllAsync(host.RootId, new()))).Error);
        Assert.Equal(AutomationErrorCode.None, (await client.GetRootsAsync()).Error);
    }

    [Fact]
    public async Task DiscoveryAndSecretsHaveProtectedCurrentUserOnlyAcl()
    {
        await using var host = await ProcessFixture.Start(); var user = PipeSecurityPolicy.CurrentUser();
        AutomationDiscovery.ValidateDirectory(user);
        AutomationDiscovery.ValidateFile(AutomationDiscovery.DescriptorPath(host.Application!.InstanceId), user);
        AutomationDiscovery.ValidateFile(AutomationDiscovery.AuthPath(host.Application.InstanceId), user);
        string token = Convert.ToBase64String(AutomationDiscovery.ReadSecret(host.Application));
        Assert.DoesNotContain(token, await File.ReadAllTextAsync(AutomationDiscovery.DescriptorPath(host.Application.InstanceId)));
        Assert.DoesNotContain(token, string.Join("", host.Output));
        await using var raw = await RawClient.Connect(host.Application); await raw.Handshake(host.Application);
        var acl = raw.Pipe.GetAccessControl();
        Assert.Equal(user, acl.GetOwner(typeof(System.Security.Principal.SecurityIdentifier)));
        Assert.True(acl.AreAccessRulesProtected);
        var rules = acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
        Assert.Single(rules.Cast<PipeAccessRule>()); Assert.Equal(user, Assert.IsType<PipeAccessRule>(rules[0]).IdentityReference);
        Assert.DoesNotContain(token, string.Join("", raw.Responses));
    }

    [Fact]
    public async Task StoppedLifetimeSecretCannotAuthenticateAnotherInstance()
    {
        await using var old = await ProcessFixture.Start();
        string oldToken = Convert.ToBase64String(AutomationDiscovery.ReadSecret(old.Application!));
        await old.StopServer();
        Assert.False(File.Exists(AutomationDiscovery.AuthPath(old.Application!.InstanceId)));
        await using var next = await ProcessFixture.Start(); await using var raw = await RawClient.Connect(next.Application!);
        Assert.Equal(AutomationTransportError.AuthenticationFailed, (await raw.Handshake(next.Application!, oldToken)).Error);
        Assert.Equal(AutomationTransportError.ApplicationUnavailable,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => old.Connect())).Error);
    }

    [Fact]
    public async Task SamePidWithWrongProcessStartIsStaleAndRemoved()
    {
        await using var host = await ProcessFixture.Start();
        string staleNonce = Guid.NewGuid().ToString("N");
        var stale = host.Application! with { InstanceId = staleNonce, ProcessStartUtcTicks = "1",
            EndpointName = AutomationDiscovery.Endpoint(host.Process.Id, staleNonce) };
        AutomationDiscovery.Publish(stale, new byte[32], PipeSecurityPolicy.CurrentUser());
        Assert.DoesNotContain(await AutomationDiscovery.DiscoverAsync(), item => item.InstanceId == staleNonce);
        Assert.False(File.Exists(AutomationDiscovery.DescriptorPath(staleNonce))); Assert.False(File.Exists(AutomationDiscovery.AuthPath(staleNonce)));
        Assert.Equal(AutomationTransportError.ApplicationUnavailable,
            (await Assert.ThrowsAsync<AutomationTransportException>(() => WindowsAutomationClient.ConnectAsync(stale))).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedDiscoveryCannotRedirectCleanupToAnotherFile(bool absolutePath)
    {
        await using var host = await ProcessFixture.Start();
        string nonce = Guid.NewGuid().ToString("N");
        var user = PipeSecurityPolicy.CurrentUser();
        var counterfeit = host.Application! with { InstanceId = nonce, ProcessStartUtcTicks = "1",
            EndpointName = AutomationDiscovery.Endpoint(host.Process.Id, nonce) };
        AutomationDiscovery.Publish(counterfeit, new byte[32], user);
        string target = AutomationDiscovery.DescriptorPath(host.Application.InstanceId);
        string before = await File.ReadAllTextAsync(target);
        try
        {
            string injected = absolutePath ? target : "../v1/" + host.Application.InstanceId;
            await File.WriteAllTextAsync(AutomationDiscovery.DescriptorPath(nonce),
                System.Text.Json.JsonSerializer.Serialize(counterfeit with { InstanceId = injected, EndpointName = target }, Protocol.Json));
            Assert.DoesNotContain(await AutomationDiscovery.DiscoverAsync(), app => app.InstanceId == nonce);
            AutomationDiscovery.Remove(injected);
            Assert.Equal(before, await File.ReadAllTextAsync(target));
            Assert.True(File.Exists(AutomationDiscovery.AuthPath(host.Application.InstanceId)));
            Assert.True(File.Exists(AutomationDiscovery.DescriptorPath(nonce)));
            await using var client = await host.Connect();
            Assert.Equal(AutomationErrorCode.None, (await client.GetRootsAsync()).Error);
        }
        finally { AutomationDiscovery.Remove(nonce); }
    }
}
