using System;
using ModernFormsNext.VisualStudioExtension.Commands;
using Xunit;

namespace ModernFormsNext.VisualStudioExtension.Vsix.Tests;

public sealed class DesignerHostIpcClientTests
{
    [Fact]
    public void SaveRejectsAResponseWithAnotherRequestId()
    {
        var result = DesignerHostIpcClient.ParseSaveResponse(
            "expected-request",
            "SAVE_RESULT\tdifferent-request\tSAVED");

        Assert.Equal(DesignerHostSaveOutcome.Failed, result.Outcome);
        Assert.Equal("expected-request", result.RequestId);
        Assert.Contains("mismatched", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TimedOutSaveDoesNotPoisonTheNextCorrelatedResponse()
    {
        var timedOut = DesignerHostIpcClient.ParseSaveResponse("timed-out-request", response: null);
        var retry = DesignerHostIpcClient.ParseSaveResponse(
            "retry-request",
            "SAVE_RESULT\tretry-request\tSAVED");

        Assert.Equal(DesignerHostSaveOutcome.Failed, timedOut.Outcome);
        Assert.Equal("The Designer host did not respond to the save request.", timedOut.Error);
        Assert.Equal(DesignerHostSaveOutcome.Saved, retry.Outcome);
        Assert.NotEqual(timedOut.RequestId, retry.RequestId);
    }
}
