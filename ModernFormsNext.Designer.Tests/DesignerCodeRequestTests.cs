using ModernFormsNext.VisualStudioDesignerHost;
using ModernFormsNext.VisualStudioExtension;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerCodeRequestTests
{
    private static string DesignPath => IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-code-contract", "Sample.mfdesign");
    private static string SourcePath => IOPath.ChangeExtension(DesignPath, ".cs");

    [Fact]
    public void RequestRoundTripPreservesUnicodeBufferAndUtf16Offsets()
    {
        var request = Request("INSERT", "// Zażółć\r\n", "    // handler\r\n", 5);
        Assert.True(DesignerCodeRequest.TryDecode(request.Encode(), out var decoded));
        Assert.Equal(request.ExpectedText, decoded.ExpectedText);
        Assert.Equal(request.Insertion, decoded.Insertion);
        Assert.Equal(5, decoded.Offset);
        Assert.True(decoded.IsAuthorized(DesignPath));
        Assert.False(decoded.IsAuthorized(IOPath.Combine(IOPath.GetDirectoryName(DesignPath)!, "Other.mfdesign")));
        var foreign = new DesignerCodeRequest(request.Id, "READ", DesignPath,
            IOPath.Combine(IOPath.GetDirectoryName(DesignPath)!, "Other.cs"), string.Empty, 0, string.Empty, request.ExpiresUtcTicks);
        Assert.False(foreign.IsAuthorized(DesignPath));
    }

    [Fact]
    public void ExpiredAndOversizedRequestsFailClosed()
    {
        var request = Request("READ");
        var expired = new DesignerCodeRequest(request.Id, "READ", DesignPath, SourcePath, string.Empty, 0, string.Empty, DateTime.UtcNow.AddMinutes(-1).Ticks);
        Assert.False(expired.IsAuthorized(DesignPath));
        Assert.False(DesignerCodeRequest.TryDecode(new string(' ', DesignerCodeRequest.MaximumFrameCharacters + 1), out _));
        var oversized = Request("INSERT", new string(' ', DesignerCodeRequest.MaximumTextCharacters + 1));
        Assert.False(DesignerCodeRequest.TryDecode(oversized.Encode(), out _));
    }

    [Fact]
    public async Task SaveCommandCarriesOnlyTheBoundDesignAndGetsOneAcknowledgement()
    {
        using var broker = new DesignerCodeRequestBroker();
        var operation = broker.RequestAsync(DesignerCodeRequest.SaveCommand, DesignPath,
            string.Empty, string.Empty, 0, string.Empty, CancellationToken.None);
        Assert.True(DesignerCodeRequest.TryDecode(broker.Poll(DesignPath), out var request));
        Assert.True(request.IsSaveCommand);
        Assert.True(request.IsAuthorized(DesignPath));
        Assert.False(request.IsAuthorized(IOPath.ChangeExtension(DesignPath, ".other")));

        var payload = new DesignerCodeRequest(request.Id, request.Operation, DesignPath, SourcePath,
            string.Empty, 0, string.Empty, request.ExpiresUtcTicks);
        Assert.False(DesignerCodeRequest.TryDecode(payload.Encode(), out _));
        Assert.False(payload.IsAuthorized(DesignPath));

        var reply = DesignerCodeRequest.Result(request.Id, true, null);
        Assert.True(broker.Complete(reply, DesignPath));
        Assert.False(broker.Complete(reply, DesignPath));
        Assert.Equal((true, string.Empty), await operation);
    }

    [Fact]
    public async Task BoundedReaderRetainsAFrameAndRejectsOversizedInput()
    {
        Assert.Equal("DIRTY\t0", await DesignerCodeRequest.ReadBoundedLineAsync(new StringReader("DIRTY\t0\r\n")));
        Assert.Null(await DesignerCodeRequest.ReadBoundedLineAsync(new StringReader(new string(' ', DesignerCodeRequest.MaximumFrameCharacters + 1) + "\n")));
    }

    [Fact]
    public async Task BrokerCorrelatesOneResultAndRejectsWrongDocumentAndDuplicateReply()
    {
        using var broker = new DesignerCodeRequestBroker();
        var operation = broker.RequestAsync("READ", DesignPath, SourcePath, string.Empty, 0, string.Empty, CancellationToken.None);
        Assert.True(DesignerCodeRequest.TryDecode(broker.Poll(DesignPath), out var request));
        Assert.False(broker.Complete(DesignerCodeRequest.Result(Guid.NewGuid().ToString("N"), true, "// current text"), DesignPath));
        var reply = DesignerCodeRequest.Result(request.Id, true, "// unsaved text");
        Assert.False(broker.Complete(reply, IOPath.ChangeExtension(DesignPath, ".other")));
        Assert.True(broker.Complete(reply, DesignPath));
        Assert.False(broker.Complete(reply, DesignPath));
        Assert.Equal((true, "// unsaved text"), await operation);
    }

    [Fact]
    public async Task CanceledQueuedRequestIsNeverDispatched()
    {
        using var broker = new DesignerCodeRequestBroker();
        using var cancellation = new CancellationTokenSource();
        var operation = broker.RequestAsync("INSERT", DesignPath, SourcePath, "// text", 0, "// new method", cancellation.Token);
        await cancellation.CancelAsync();
        // The already-started request is deliberately canceled while pending; this assertion
        // observes that task rather than starting a fresh request inside the assertion callback.
#pragma warning disable VSTHRD003
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
#pragma warning restore VSTHRD003
        Assert.Equal("CODE_NONE", broker.Poll(DesignPath));
    }

    [Fact]
    public async Task ContextChangeAndDisposeCompletePendingRequestsWithoutWrites()
    {
        using var broker = new DesignerCodeRequestBroker();
        var first = broker.RequestAsync("READ", DesignPath, SourcePath, string.Empty, 0, string.Empty, CancellationToken.None);
        broker.CancelAll();
        Assert.False((await first).Success);
        Assert.Equal("CODE_NONE", broker.Poll(DesignPath));
        var second = broker.RequestAsync("READ", DesignPath, SourcePath, string.Empty, 0, string.Empty, CancellationToken.None);
        broker.Dispose();
        Assert.False((await second).Success);
        Assert.False((await broker.RequestAsync("READ", DesignPath, SourcePath, string.Empty, 0, string.Empty, CancellationToken.None)).Success);
    }

    [Fact]
    public async Task QueueCapacityRemainsBounded()
    {
        using var broker = new DesignerCodeRequestBroker();
        var operations = Enumerable.Range(0, 8).Select(_ => broker.RequestAsync("READ", DesignPath, SourcePath,
            string.Empty, 0, string.Empty, CancellationToken.None)).ToArray();
        Assert.False((await broker.RequestAsync("READ", DesignPath, SourcePath, string.Empty, 0, string.Empty, CancellationToken.None)).Success);
        broker.CancelAll();
        Assert.All(await Task.WhenAll(operations), result => Assert.False(result.Success));
    }

    private static DesignerCodeRequest Request(string operation, string text = "", string insertion = "", int offset = 0)
        => new(Guid.NewGuid().ToString("N"), operation, DesignPath, SourcePath, text, offset, insertion, DateTime.UtcNow.AddSeconds(30).Ticks);
}
