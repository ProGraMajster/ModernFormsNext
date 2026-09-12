using System.Text.Json;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

public sealed class ViewportProtocolTests
{
    [Fact]
    public void OldOperationPayloadOmitsNewOptionalMember()
    {
        var json = Protocol.Element(new Operation { Action = AccessibleActions.Invoke });
        Assert.False(json.TryGetProperty("scroll", out _));
        Assert.Null(Protocol.Read<Operation>(json).Scroll);
    }
    [Fact]
    public void TypedRequestRoundTripsWithoutTextOrNativeSentinels()
    {
        var operation = new Operation { Action = AccessibleActions.Scroll,
            Scroll = ScrollOperation.From(AccessibleScrollRequest.ToPercent(null, 75)) };
        var roundTrip = Protocol.Read<Operation>(Protocol.Element(operation));
        var request = roundTrip.Scroll!.ToRequest();
        Assert.Equal(AccessibleScrollRequestKind.Percent, request.Kind);
        Assert.Null(request.Horizontal); Assert.Equal(75, request.Vertical);
        Assert.Null(roundTrip.Text); Assert.Null(roundTrip.Number);
    }
    [Fact]
    public void MalformedOrUnknownModeIsRejected()
    {
        Assert.Throws<AutomationTransportException>(() => new ScrollOperation(99, null, null, 0, 0).ToRequest());
        var invalid = Assert.Throws<AutomationTransportException>(() => new ScrollOperation(1, -1, null, 0, 0).ToRequest());
        Assert.Equal(AutomationTransportError.InvalidRequest, invalid.Error);
        using var json = JsonDocument.Parse("{\"scroll\":{\"kind\":0,\"horizontalAmount\":0,\"verticalAmount\":0,\"unexpected\":1}}");
        Assert.Throws<JsonException>(() => Protocol.Read<Operation>(json.RootElement));
    }
}
