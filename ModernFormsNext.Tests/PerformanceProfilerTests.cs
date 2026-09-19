using System.Text;
using ModernFormsNext.Diagnostics;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class PerformanceProfilerTests
{
    [Fact]
    public void NestedCategoriesAreUnionedAndWorkIsSplitAtFrameBoundaries()
    {
        long clock = 0;
        using var root = new Control();
        using var profiler = PerformanceProfiler.StartForTesting(null, () => clock, 1000);
        using var layout = profiler.Measure(PerformanceActivityKind.Layout);
        clock = 5;
        var frame = PerformanceRecorder.BeginRender(root, new());
        clock = 10;
        var nested = profiler.Measure(PerformanceActivityKind.Layout);
        clock = 15;
        nested.Dispose();
        clock = 20;
        frame.Complete();
        clock = 22;
        frame.Dispose();
        clock = 25;
        layout.Dispose();
        var snapshot = profiler.Capture();
        var result = Assert.Single(snapshot.Frames);
        Assert.Equal(TimeSpan.FromMilliseconds(17), result.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(17), result.Work.LayoutTime);
        Assert.Equal(TimeSpan.FromMilliseconds(15), result.Work.RenderTime);
        Assert.Equal(TimeSpan.FromMilliseconds(5), result.ThreadWorkSincePreviousFrame.LayoutTime);
        Assert.Equal(TimeSpan.FromMilliseconds(8), snapshot.UnframedWork.LayoutTime);
    }

    [Fact]
    public void SourceIntervalsIncludeIdleButSlowDetectionUsesCallbackDuration()
    {
        long clock = 0;
        using var a = new Control();
        using var b = new Control();
        using var profiler = PerformanceProfiler.StartForTesting(new() { SlowFrameThreshold = TimeSpan.FromMilliseconds(10) }, () => clock, 1000);
        Frame(a, 1); clock = 1000; Frame(b, 1); clock = 2000; Frame(a, 1);
        clock = 3000; Frame(a, 2);
        var frames = profiler.Capture().Frames;
        Assert.Null(frames[0].Interval);
        Assert.Null(frames[1].Interval);
        Assert.Equal(TimeSpan.FromSeconds(2), frames[2].Interval);
        Assert.Equal(0.5, frames[2].FramesPerSecond);
        Assert.Null(frames[3].Interval);
        Assert.All(frames, frame => { Assert.False(frame.IsSlow); Assert.Equal(TimeSpan.FromMilliseconds(2), frame.Duration); });

        void Frame(Control control, long generation)
        {
            using var frame = PerformanceRecorder.BeginRender(control, new() { HostGeneration = generation });
            clock += 2; frame.Complete();
        }
    }

    [Fact]
    public void HistoriesAreBoundedAndSnapshotsStayDetached()
    {
        long clock = 0;
        using var root = new Control();
        using var profiler = PerformanceProfiler.StartForTesting(new() {
            FrameCapacity = 2, SlowFrameCapacity = 1, SlowFrameThreshold = TimeSpan.FromMilliseconds(1)
        }, () => clock, 1000);
        Render(); var first = profiler.Capture(); Render(); Render();
        var final = profiler.Capture();
        Assert.Single(first.Frames);
        Assert.Equal(1, first.TotalFrames);
        Assert.Equal(3, final.TotalFrames);
        Assert.Equal(1, final.DroppedFrames);
        Assert.Equal(new long[] { 2, 3 }, final.Frames.Select(frame => frame.Sequence));
        Assert.Equal(3, Assert.Single(final.SlowFrames).Sequence);
        Assert.Equal(TimeSpan.FromMilliseconds(4), final.GetStatistics(final.Frames[0].SourceId).P95);
        Assert.Throws<NotSupportedException>(() => ((IList<PerformanceFrameMetrics>)final.Frames).Clear());
        void Render() { using var frame = PerformanceRecorder.BeginRender(root, new()); clock += 4; frame.Complete(); }
    }

    [Fact]
    public void CopiedScopesAndRetiredResourcesCannotAffectAnotherSession()
    {
        long clock = 0;
        using var root = new Control();
        var profiler = PerformanceProfiler.StartForTesting(null, () => clock, 1000);
        var scope = profiler.Measure(); var copy = scope;
        clock = 4; scope.Dispose(); clock = 8; copy.Dispose();
        Assert.Equal(TimeSpan.FromMilliseconds(4), profiler.Capture().UnframedWork.CustomTime);
        var resource = PerformanceRecorder.CaptureResource(PerformanceCounterKind.ShadersCreated, PerformanceCounterKind.ShadersDisposed);
        var frame = PerformanceRecorder.BeginRender(root, new());
        profiler.Dispose();
        using var next = PerformanceProfiler.StartForTesting(null, () => clock, 1000);
        resource.Dispose(); frame.Complete(); frame.Dispose(); copy.Complete();
        Assert.Empty(next.Capture().Frames);
        Assert.Equal(0, next.Capture().UnframedWork.ShadersDisposed);
        Assert.Throws<ObjectDisposedException>(() => profiler.Capture());
    }

    [Fact]
    public void DetailsAndExportDoNotHarvestNamesOrTextAndResourceOwnershipFollowsTheControlScope()
    {
        using var control = new Control { Text = "secret-text-58", Name = "secret-name-58" };
        using var profiler = PerformanceProfiler.Start(new() { DetailedControls = true });
        PerformanceResourceToken resource;
        using (PerformanceRecorder.Measure(PerformanceActivityKind.Render, control))
            resource = PerformanceRecorder.CaptureResource(PerformanceCounterKind.ShadersCreated, PerformanceCounterKind.ShadersDisposed);
        resource.Dispose(); resource.Dispose();
        var detail = Assert.Single(profiler.Capture().Controls);
        Assert.Equal(1, detail.Work.ShadersCreated); Assert.Equal(1, detail.Work.ShadersDisposed);
        using var stream = new MemoryStream(); profiler.WriteJson(stream);
        string json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.DoesNotContain("secret-text-58", json); Assert.DoesNotContain("secret-name-58", json);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void ExtensionValidationIsAtomicAndHandlesAreRevoked()
    {
        var profiler = PerformanceProfiler.Start(new() { CounterCapacity = 2 });
        var count = profiler.RegisterCounter("created", PerformanceExtensionKind.Counter);
        var gauge = profiler.RegisterCounter("realized", PerformanceExtensionKind.Gauge);
        try {
            Assert.All(profiler.Capture().Extensions, metric => Assert.Null(metric.Value));
            count.Add(2); gauge.Set(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => count.Add(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => count.Add(-1));
            Assert.Throws<InvalidOperationException>(() => gauge.Add(1));
            Assert.Throws<InvalidOperationException>(() => count.Set(1));
            Assert.Throws<ArgumentException>(() => profiler.RegisterCounter("created", PerformanceExtensionKind.Counter));
            Assert.Throws<InvalidOperationException>(() => profiler.RegisterCounter("overflow", PerformanceExtensionKind.Counter));
            Assert.Equal(new double?[] { 2, 3 }, profiler.Capture().Extensions.Select(metric => metric.Value));
        } finally { profiler.Dispose(); }
        Assert.Throws<ObjectDisposedException>(() => count.Add(1));
    }

    [Fact]
    public void ForeignThreadsCannotReadOrRetireTheOwnerSession()
    {
        using var profiler = PerformanceProfiler.Start();
        Exception? read = null, dispose = null;
        var thread = new Thread(() => {
            read = Record.Exception(() => profiler.Capture());
            dispose = Record.Exception(() => profiler.Dispose());
            Assert.False(PerformanceRecorder.IsEnabled);
        });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(read); Assert.IsType<InvalidOperationException>(dispose);
        Assert.True(PerformanceRecorder.IsEnabled);
    }

    [Fact]
    public void CapacityOverflowRemainsBoundedAndRecoversWhenScopesClose()
    {
        using var profiler = PerformanceProfiler.Start(new() { DetailedControls = true, DetailCapacity = 1 });
        var scopes = new PerformanceScope[PerformanceProfiler.ActivityCapacity + 1];
        for (int i = 0; i < scopes.Length; i++) scopes[i] = profiler.Measure();
        Assert.Equal(1, profiler.Capture().DroppedScopes);
        foreach (var scope in scopes) scope.Dispose();
        using (profiler.Measure()) { }
        using var a = new Control(); using var b = new Control();
        PerformanceRecorder.Count(PerformanceCounterKind.LayoutPasses, control: a);
        PerformanceRecorder.Count(PerformanceCounterKind.LayoutPasses, control: b);
        var result = profiler.Capture();
        Assert.Equal(1, result.DroppedScopes); Assert.Single(result.Controls); Assert.True(result.DroppedDetails > 0);
        Assert.True(result.UnframedWork.LayoutPasses >= 2);
    }

    [Fact]
    public void InvalidOptionsDoNotInstallACollectorAndLongTimesSaturate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceProfiler.Start(new() { FrameCapacity = 0 }));
        Assert.False(PerformanceRecorder.IsEnabled);
        Assert.Equal(TimeSpan.MaxValue, PerformanceWorkMetrics.ToTime(long.MaxValue, 1000));
        Assert.Equal(TimeSpan.Zero, PerformanceWorkMetrics.ToTime(-1, 1000));
    }

    [Fact]
    public void RecordingOnlyHotPathDoesNotAllocateAfterSourceWarmup()
    {
        using var root = new Control();
        using var profiler = PerformanceProfiler.Start();
        for (int i = 0; i < 100; i++) Render();
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Render();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(0, allocated);
        void Render() {
            using var frame = PerformanceRecorder.BeginRender(root, new());
            using var layout = profiler.Measure(PerformanceActivityKind.Layout);
            PerformanceRecorder.Count(PerformanceCounterKind.LayoutPasses);
            frame.Complete();
        }
    }
}
