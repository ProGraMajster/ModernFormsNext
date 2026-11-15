using System.Buffers.Binary;
using System.Collections.Concurrent;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using System.Text.Json;
using ModernFormsNext.Designer.ExecutablePreview;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class ExecutablePreviewTests
{
    [Fact]
    public async Task WireRoundTripsOnlyTheExpectedSessionRequestRevisionAndNode()
    {
        var key = new PreviewKey(Guid.NewGuid(), 10, 5, 2);
        using var stream = new MemoryStream();
        var request = Request();
        await PreviewWire.WriteAsync(stream, key, PreviewMessage.Request, PreviewWire.Encode(request), default);
        var bytes = stream.ToArray();
        foreach (var stale in new[] { key with { Session = Guid.NewGuid() }, key with { Request = 9 },
            key with { Revision = 4 }, key with { Node = 1 } })
        {
            using var input = new MemoryStream(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => PreviewWire.ReadAsync(input, stale, PreviewMessage.Request, default).AsTask());
        }
        stream.Position = 0;
        var decoded = PreviewWire.Decode((await PreviewWire.ReadAsync(stream, key, PreviewMessage.Request, default)).Payload);
        Assert.Equal(request.Digest, decoded.Digest);
        Assert.Equal(request.Type, decoded.Type);
        Assert.Equal("healthy", Assert.Single(decoded.Properties).Value.GetString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4194305)]
    [InlineData(int.MaxValue)]
    public async Task HostRejectsOversizedRasterBeforeReadingItsPayload(int bytes)
    {
        var key = new PreviewKey(Guid.NewGuid(), 1, 0, 1);
        using var stream = new MemoryStream();
        await PreviewWire.WriteAsync(stream, key, PreviewMessage.Result, new byte[4], default);
        var forged = stream.ToArray()[..64];
        BinaryPrimitives.WriteInt32LittleEndian(forged.AsSpan(44), bytes);
        using var input = new MemoryStream(forged);
        await Assert.ThrowsAsync<InvalidDataException>(() => PreviewWire.ReadAsync(input, key, PreviewMessage.Result, default).AsTask());
    }

    [Fact]
    public async Task RejectsAnInRangeRasterWithTheWrongSizeBeforeReadingItsPayload()
    {
        var key = new PreviewKey(Guid.NewGuid(), 1, 0, 1);
        using var serialized = new MemoryStream();
        await PreviewWire.WriteAsync(serialized, key, PreviewMessage.Result, new byte[8], default);
        using var headerOnly = new MemoryStream(serialized.ToArray()[..64]);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            PreviewWire.ReadAsync(headerOnly, key, PreviewMessage.Result, default, expectedResultLength: 4).AsTask());
    }

    [Fact]
    public async Task RejectsAnAdditionalFrameAfterACompleteResult()
    {
        var key = new PreviewKey(Guid.NewGuid(), 1, 0, 1);
        using var stream = new MemoryStream();
        await PreviewWire.WriteAsync(stream, key, PreviewMessage.Result, new byte[4], default);
        await PreviewWire.WriteAsync(stream, key, PreviewMessage.Result, new byte[4], default);
        stream.Position = 0;
        await PreviewWire.ReadAsync(stream, key, PreviewMessage.Result, default, expectedResultLength: 4);
        await Assert.ThrowsAsync<InvalidDataException>(() => PreviewWire.RequireEndOfStreamAsync(stream, default).AsTask());
    }

    [Fact]
    public async Task HostRejectsARequestOrASecondReadyAsAReply()
    {
        var key = new PreviewKey(Guid.NewGuid(), 1, 0, 1);
        foreach (var message in new[] { PreviewMessage.Request, PreviewMessage.Ready })
        {
            using var stream = new MemoryStream();
            await PreviewWire.WriteAsync(stream, key, message, message == PreviewMessage.Request ? new byte[] { 1 } : ReadOnlyMemory<byte>.Empty, default);
            stream.Position = 0;
            await Assert.ThrowsAsync<InvalidDataException>(() => PreviewWire.ReadAsync(stream, key, PreviewMessage.Result, default).AsTask());
        }
    }

    [Fact]
    public void WorkerRejectsUnboundedOrUnrecognizedInputAndDuplicateProperties()
    {
        var request = Request();
        Assert.Throws<InvalidDataException>(() => PreviewWire.Encode(request with { Width = 0 }));
        Assert.Throws<InvalidDataException>(() => PreviewWire.Encode(request with { Scale = double.PositiveInfinity }));
        Assert.Throws<InvalidDataException>(() => PreviewWire.Encode(request with { Type = "Name, loader" }));
        Assert.Throws<InvalidDataException>(() => PreviewWire.Encode(request with { Properties = [request.Properties[0], request.Properties[0]] }));
        Assert.Throws<InvalidDataException>(() => PreviewWire.Decode(new byte[PreviewWire.MaxRequest + 1]));
        Assert.Throws<JsonException>(() => PreviewWire.Decode("""{"AssemblyPath":"outside.dll"}"""u8.ToArray()));
        Assert.Throws<JsonException>(() => PreviewWire.Decode("""{"Width":1,"Width":2}"""u8.ToArray()));
    }

    [Fact]
    public void RasterRequiresExactLengthAndPremultipliedChannels()
    {
        var request = Request();
        Assert.True(PreviewWire.IsRaster([0, 10, 10, 10], request));
        Assert.False(PreviewWire.IsRaster([0, 10, 11, 10], request));
        Assert.False(PreviewWire.IsRaster(new byte[5], request));
    }

    [Fact]
    public async Task FailureAndStaleFrameDoNotChangeDocumentHistorySelectionOrDirtyState()
        => await Task.Run(CheckDocumentOnOneThread);

    private static void CheckDocumentOnOneThread()
    {
        var dispatcher = new QueuedDispatcher();
        var environment = new ProjectEnvironment();
        using var session = new DesignerSession(environment);
        var node = new DesignControlNode { Name = "widget", TypeName = "Project.Control", Bounds = new(10, 10, 1, 1) };
        session.Document.Controls.Add(node);
        session.SelectNode(node);
        session.SetPropertyValue(node, "Caption", DesignPropertyValue.FromString("draft"));
        Assert.True(session.IsDirty);
        Assert.True(session.Transactions.CanUndo);
        var snapshot = new ControlledSnapshot();
        using var controller = new ExecutablePreviewController(session, new Backend(snapshot), dispatcher);

        Assert.Null(controller.GetRaster(node, 1, 1, 1));
        Assert.Equal(0, snapshot.RenderCount);
        // The test owns this thread and has no synchronization context. The modeled editor
        // callbacks below must run here to exercise the transaction manager's thread contract.
#pragma warning disable VSTHRD002
        var candidate = controller.CaptureAsync(environment.CurrentProjectPath, []).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        controller.EnableAsync(environment.CurrentProjectPath, Assert.IsType<ControlledSnapshot>(candidate))
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        Assert.Null(controller.GetRaster(node, 1, 1, 1));
        Assert.True(snapshot.Started.Wait(TimeSpan.FromSeconds(5)));
        snapshot.Result.SetResult(new(null, PreviewPhase.Paint, PreviewFault.Timeout));
        dispatcher.DrainOne();

        Assert.Same(node, session.SelectedNode);
        Assert.Equal("draft", node.Properties["Caption"].GetString());
        Assert.True(session.IsDirty);
        Assert.True(session.Transactions.CanUndo);
        Assert.Null(controller.GetRaster(node, 1, 1, 1));
        Assert.Equal(1, snapshot.RenderCount);

        snapshot.NextRequest();
        controller.Retry();
        Assert.Null(controller.GetRaster(node, 1, 1, 1));
        Assert.True(snapshot.Started.Wait(TimeSpan.FromSeconds(5)));
        session.SetPropertyValue(node, "Caption", DesignPropertyValue.FromString("new draft"));
        snapshot.Result.SetResult(new([0, 0, 0, 0], PreviewPhase.Dispose));
        dispatcher.DrainOne();
        Assert.DoesNotContain(environment.Output, line => line.Contains("Isolated preview rendered", StringComparison.Ordinal));
        Assert.Equal("new draft", node.Properties["Caption"].GetString());
        Assert.Same(node, session.SelectedNode);
        Assert.True(session.Transactions.Undo());
        Assert.True(session.Transactions.CanRedo);
        Assert.True(session.IsDirty);

        controller.Disable();
        Assert.False(controller.IsEnabled);
        Assert.Null(controller.GetRaster(node, 1, 1, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingRetiresAnUnapprovedSnapshotExactlyOnce(bool captureInFlight)
        => await Task.Run(() => CheckCaptureCleanup(captureInFlight));

    private static void CheckCaptureCleanup(bool captureInFlight)
    {
        var environment = new ProjectEnvironment();
        using var session = new DesignerSession(environment);
        var snapshot = new ControlledSnapshot();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim(!captureInFlight);
        var backend = new Backend(snapshot, () =>
        {
            started.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        });
        using var controller = new ExecutablePreviewController(session, backend, new QueuedDispatcher());
        var capture = controller.CaptureAsync(environment.CurrentProjectPath, []);
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));

        // Both the dialog and shutdown normally run on the same Designer thread.
#pragma warning disable VSTHRD002
        if (!captureInFlight)
            Assert.Same(snapshot, capture.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());

        controller.Dispose();
        release.Set();
        if (captureInFlight)
            Assert.Null(capture.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());

        // A dialog can also finish closing after the Designer has already taken ownership.
        controller.Discard(snapshot);
        controller.EnableAsync(environment.CurrentProjectPath, snapshot).GetAwaiter().GetResult();
        controller.Cleanup.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        Assert.False(controller.IsEnabled);
        Assert.Equal(1, snapshot.DisposeCount);
    }

    private sealed class QueuedDispatcher : IDesignerUiDispatcher
    {
        private readonly ConcurrentQueue<Action> callbacks = new();
        private readonly SemaphoreSlim ready = new(0);

        public void Post(Action callback)
        {
            callbacks.Enqueue(callback);
            ready.Release();
        }

        internal void DrainOne()
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(callbacks.TryDequeue(out var callback));
            callback();
        }
    }

    private sealed class ProjectEnvironment : IDesignerHostEnvironment
    {
        public string? CurrentDocumentPath => null;
        public string CurrentProjectPath => "C:/Designer/Fixture.csproj";
        public void ReportStatus(string message) { }
        internal List<string> Output { get; } = [];
        public void ReportOutput(string message) => Output.Add(message);
    }

    private sealed class Backend(ControlledSnapshot snapshot, Action? beforeCapture = null) : IExecutablePreviewBackend
    {
        public IExecutablePreviewSnapshot Capture(string project, IReadOnlyList<string> files)
        {
            beforeCapture?.Invoke();
            return snapshot;
        }
    }

    private sealed class ControlledSnapshot : IExecutablePreviewSnapshot
    {
        public string Digest => new('A', 64);
        public IReadOnlyList<string> Summary => [];
        public event Action? SourcesChanged { add { } remove { } }
        internal int RenderCount { get; private set; }
        private int disposeCount;
        internal int DisposeCount => Volatile.Read(ref disposeCount);
        internal ManualResetEventSlim Started { get; } = new();
        internal TaskCompletionSource<PreviewOutcome> Result { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task VerifyAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<PreviewOutcome> RenderAsync(PreviewKey key, PreviewRequest request, CancellationToken ct)
        {
            RenderCount++;
            Started.Set();
            // The test controls completion and deliberately ignores cancellation to inject a late result.
#pragma warning disable VSTHRD003
            return Result.Task;
#pragma warning restore VSTHRD003
        }
        internal void NextRequest()
        {
            Started.Reset();
            Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public void Dispose()
        {
            Interlocked.Increment(ref disposeCount);
            Started.Dispose();
        }
    }

    private static PreviewRequest Request()
        => new(new string('A', 64), "Project.Control", 1, 1, 1, [new("Caption", JsonSerializer.SerializeToElement("healthy"))]);
}
