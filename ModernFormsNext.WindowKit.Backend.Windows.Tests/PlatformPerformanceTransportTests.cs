using ModernFormsNext.WindowKit.Diagnostics;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class PlatformPerformanceTransportTests
{
    private static readonly PlatformRenderInfo Info = new(PlatformRenderBoundary.WindowPaint,
        PlatformRenderBackend.Windows, PlatformRenderMode.Software, 100, 80, 150, 120,
        600, "BGRA8888", 1.5, 72000, HostGeneration: 1, BackingGeneration: 3);

    [Fact]
    public void DisabledIngressDoesNotAllocateOrRetainAFrame()
    {
        Assert.False(PlatformPerformanceDiagnostics.IsEnabled);
        var source = new object();
        for (int i = 0; i < 20; i++) PlatformPerformanceDiagnostics.BeginFrame(source, Info).Dispose();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            var scope = PlatformPerformanceDiagnostics.BeginFrame(source, Info);
            scope.UpdateInfo(Info);
            scope.Complete();
            scope.Dispose();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ScopeReportsActualMetadataAndCompletesExactlyOnce()
    {
        var sink = new Sink();
        using var registration = PlatformPerformanceDiagnostics.Register(sink);
        var scope = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        var copy = scope;
        var actual = Info with { PixelWidth = 180, RowBytes = 720, BackingGeneration = 4 };
        scope.UpdateInfo(actual);
        copy.Complete();
        scope.Dispose();
        copy.Dispose();
        copy.UpdateInfo(Info);
        Assert.Equal(actual, Assert.Single(sink.Updates).Info);
        Assert.Equal((1L, true), Assert.Single(sink.Ends));
        Assert.Equal(Info, Assert.Single(sink.Starts));
    }

    [Fact]
    public void OutOfOrderAndReusedSlotsCannotCloseANewerFrame()
    {
        var sink = new Sink();
        using var registration = PlatformPerformanceDiagnostics.Register(sink);
        var parent = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        var child = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        parent.Complete();
        parent.Dispose();
        Assert.Empty(sink.Ends);
        child.Dispose();
        Assert.Equal(new[] { (2L, false), (1L, true) }, sink.Ends);
        var next = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        parent.Complete();
        parent.Dispose();
        Assert.Equal(2, sink.Ends.Count);
        next.Dispose();
        Assert.Equal((3L, false), sink.Ends[2]);
    }

    [Fact]
    public void RevocationClosesOutstandingFramesAndDoesNotDetachReplacement()
    {
        var old = new Sink();
        var registration = PlatformPerformanceDiagnostics.Register(old);
        var scope = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        scope.Complete();
        registration.Dispose();
        Assert.Equal((1L, false), Assert.Single(old.Ends));
        var replacement = new Sink();
        using var next = PlatformPerformanceDiagnostics.Register(replacement);
        registration.Dispose();
        scope.Dispose();
        using (var active = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info)) active.Complete();
        Assert.True(PlatformPerformanceDiagnostics.IsEnabled);
        Assert.Equal((1L, true), Assert.Single(replacement.Ends));
    }

    [Fact]
    public void SinkFailuresCannotEscapeNativeCallbacksOrLeaveRegistrationActive()
    {
        var sink = new Sink { ThrowBegin = true };
        var registration = PlatformPerformanceDiagnostics.Register(sink);
        try
        {
            PlatformPerformanceDiagnostics.BeginFrame(new object(), Info).Dispose();
            sink.ThrowBegin = false;
            sink.ThrowUpdate = sink.ThrowEnd = true;
            using var scope = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
            scope.UpdateInfo(Info);
            scope.Complete();
        }
        finally { registration.Dispose(); }
        Assert.False(PlatformPerformanceDiagnostics.IsEnabled);
        Assert.Equal(3, registration.RecorderFailureCount);
    }

    [Fact]
    public void NativePaintExceptionRemainsTheOriginalException()
    {
        var sink = new Sink { ThrowEnd = true };
        using var registration = PlatformPerformanceDiagnostics.Register(sink);
        var expected = new InvalidOperationException("paint");
        var actual = Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var scope = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.Equal((1L, false), Assert.Single(sink.Ends));
    }

    [Fact]
    public void ReentrantBeginIsOmittedAndRevokedOpeningTokenIsClosed()
    {
        var sink = new Sink();
        using var registration = PlatformPerformanceDiagnostics.Register(sink);
        sink.OnBegin = () =>
        {
            PlatformPerformanceDiagnostics.BeginFrame(new object(), Info).Dispose();
            registration.Dispose();
        };
        PlatformPerformanceDiagnostics.BeginFrame(new object(), Info).Dispose();
        Assert.Single(sink.Starts);
        Assert.Equal(1, registration.OmittedScopeCount);
        Assert.Equal((1L, false), Assert.Single(sink.Ends));
        Assert.False(PlatformPerformanceDiagnostics.IsEnabled);
    }

    [Fact]
    public void ExcessiveNestingIsBoundedWithoutDiscardingAcceptedParents()
    {
        var sink = new Sink();
        using var registration = PlatformPerformanceDiagnostics.Register(sink);
        var scopes = new PlatformPerformanceFrameScope[PlatformPerformanceRegistration.MaximumScopeDepth + 1];
        for (int i = 0; i < scopes.Length; i++) scopes[i] = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        for (int i = scopes.Length - 1; i >= 0; i--) scopes[i].Dispose();
        Assert.Equal(PlatformPerformanceRegistration.MaximumScopeDepth, sink.Ends.Count);
        Assert.Equal(1, registration.OmittedScopeCount);
        Assert.Equal(0, registration.RecorderFailureCount);
    }

    [Fact]
    public void RegistrationIsThreadLocalAndForeignCopiesCannotFinishOwnerWork()
    {
        var sink = new Sink();
        using var registration = PlatformPerformanceDiagnostics.Register(sink);
        var scope = PlatformPerformanceDiagnostics.BeginFrame(new object(), Info);
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                Assert.False(PlatformPerformanceDiagnostics.IsEnabled);
                scope.Complete();
                scope.Dispose();
                Assert.Throws<InvalidOperationException>(registration.Dispose);
                using var own = PlatformPerformanceDiagnostics.Register(new Sink());
                Assert.True(PlatformPerformanceDiagnostics.IsEnabled);
            }
            catch (Exception error) { failure = error; }
        });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        Assert.Empty(sink.Ends);
        scope.Dispose();
        Assert.Equal((1L, false), Assert.Single(sink.Ends));
    }

    [Fact]
    public void ConcurrentRecorderOnSameThreadIsRejectedWithoutRevokingFirst()
    {
        using var registration = PlatformPerformanceDiagnostics.Register(new Sink());
        Assert.Throws<InvalidOperationException>(() => PlatformPerformanceDiagnostics.Register(new Sink()));
        Assert.True(PlatformPerformanceDiagnostics.IsEnabled);
    }

    private sealed class Sink : IPlatformPerformanceSink
    {
        internal readonly List<PlatformRenderInfo> Starts = [];
        internal readonly List<(long Token, PlatformRenderInfo Info)> Updates = [];
        internal readonly List<(long Token, bool Completed)> Ends = [];
        internal bool ThrowBegin, ThrowUpdate, ThrowEnd;
        internal Action? OnBegin;
        public long BeginFrame(object source, in PlatformRenderInfo info)
        {
            if (ThrowBegin) throw new InvalidOperationException("recorder begin");
            Starts.Add(info);
            OnBegin?.Invoke();
            return Starts.Count;
        }
        public void UpdateFrame(long token, in PlatformRenderInfo info)
        {
            Updates.Add((token, info));
            if (ThrowUpdate) throw new InvalidOperationException("recorder update");
        }
        public void EndFrame(long token, bool completed)
        {
            Ends.Add((token, completed));
            if (ThrowEnd) throw new InvalidOperationException("recorder end");
        }
    }
}
