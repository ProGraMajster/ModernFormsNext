using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Privacy")]
[Trait("Category", "Process")]
public sealed class TransportPrivacyTests
{
    [Fact]
    public async Task RealWireNeverContainsProtectedGettersExceptionsOrActionParameters()
    {
        await using var host = await ProcessFixture.Start("--privacy"); await using var raw = await RawClient.Connect(host.Application!);
        await raw.Handshake(host.Application!);
        var passwordResponse = await raw.Request(2, RequestKind.Inspect, new Operation { RootId = host.RootId, Handle = host.Password });
        var password = Protocol.ReadResult<AutomationNodeSnapshot>(passwordResponse.Result!.Value).Value!;
        Assert.Null(password.Value); Assert.Null(password.Name); Assert.Null(password.AutomationId);
        Assert.NotEqual(AutomationRedaction.None, password.Redaction);
        var found = await raw.Request(3, RequestKind.FindAll, new Operation { RootId = host.RootId, Query = new() });
        var nodes = Protocol.ReadResult<System.Collections.Immutable.ImmutableArray<AutomationNodeSnapshot>>(found.Result!.Value);
        Assert.Equal(AutomationErrorCode.GetterFault, nodes.Error);
        Assert.Contains(nodes.Value, node => node.Redaction != AutomationRedaction.None && node.Handle != host.Password);
        var fault = Assert.Single(nodes.Value.Where(node => node.AutomationId == "fault"));
        var action = await raw.Request(4, RequestKind.PerformAction, new Operation
        { RootId = host.RootId, Handle = fault.Handle, Action = AccessibleActions.SetValue, Text = "ACTION-PARAMETER-SECRET-97" });
        Assert.Equal(AutomationErrorCode.ApplicationError, Protocol.Read<AutomationActionResult>(action.Result!.Value).Error);
        var wait = await raw.Request(5, RequestKind.WaitForCondition, new Operation
        { RootId = host.RootId, Condition = new() { Kind = AutomationWaitKind.ValueEquals, Handle = host.Password, Value = "PASSWORD-MARKER-97" } });
        // An unrelated custom getter may make the root capture incomplete; either safe refusal
        // preserves privacy. Equality can never report secret satisfaction.
        Assert.NotEqual(AutomationWaitStatus.Satisfied, Protocol.Read<AutomationWaitResult>(wait.Result!.Value).Status);
        string output = string.Join("\n", raw.Responses.Concat(host.Output));
        foreach (string marker in new[] { "PASSWORD-MARKER-97", "CUSTOM-GETTER-MARKER-97", "SENSITIVE-MARKER-97",
            "EXCEPTION-MARKER-97", "ACTION-MARKER-97", "ACTION-PARAMETER-SECRET-97", "password-secret" }) Assert.DoesNotContain(marker, output);
    }
}
