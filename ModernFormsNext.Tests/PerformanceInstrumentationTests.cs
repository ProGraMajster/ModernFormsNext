using System.Drawing;
using System.Reflection;
using ModernFormsNext.Animations;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class PerformanceInstrumentationTests
{
    [Fact]
    public void SuspendedLayoutIsNotAnExecutedPassAndExceptionRestoresTheNextPass()
    {
        using var control = new Control();
        using var profiler = PerformanceProfiler.Start();
        control.SuspendLayout();
        control.PerformLayout();
        Assert.Equal(0, profiler.Capture().UnframedWork.LayoutPasses);
        control.ResumeLayout(false);

        EventHandler<LayoutEventArgs> failing = (_, _) => throw new InvalidOperationException("layout test");
        control.Layout += failing;
        Assert.Throws<InvalidOperationException>(() => control.PerformLayout());
        control.Layout -= failing;
        control.PerformLayout();

        Assert.Equal(2, profiler.Capture().UnframedWork.LayoutPasses);
        Assert.True(profiler.Capture().UnframedWork.LayoutTime >= TimeSpan.Zero);
    }

    [Fact]
    public void PreferredSizeCountsTheExistingCoreAndCachePathsSeparately()
    {
        using var control = new PreferredControl();
        using var profiler = PerformanceProfiler.Start();
        Assert.Equal(new Size(30, 12), control.GetPreferredSize(Size.Empty));
        Assert.Equal(new Size(30, 12), control.GetPreferredSize(Size.Empty));

        PerformanceWorkMetrics work = profiler.Capture().UnframedWork;
        Assert.Equal(2, work.PreferredSizeQueries);
        Assert.Equal(1, work.PreferredSizeCoreCalls);
        Assert.Equal(1, work.PreferredSizeCacheHits);
        Assert.Equal(1, control.CoreCalls);
    }

    [Fact]
    public void DisabledPreferredSizeAndRecorderHooksAllocateNoAdditionalObjectsAfterWarmup()
    {
        using var control = new PreferredControl();
        for (int index = 0; index < 100; index++)
            control.GetPreferredSize(Size.Empty);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
        {
            control.GetPreferredSize(Size.Empty);
            using var scope = PerformanceRecorder.Measure(PerformanceActivityKind.Custom, control);
            PerformanceRecorder.Count(PerformanceCounterKind.ControlsVisited, control: control);
            control.RecordPerformanceRegion(PerformanceRegionKind.ControlBounds);
            scope.Complete();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.False(PerformanceRecorder.IsEnabled);
    }

    [Fact]
    public void PaintingDistinguishesChildTraversalRepaintAndCachedComposition()
    {
        using var root = new Control();
        root.Controls.Add(new Control { Bounds = new Rectangle(2, 3, 20, 12) });
        root.Controls.Add(new Control { Bounds = new Rectangle(2, 20, 20, 12), Visible = false });
        root.Controls.Add(new Control { Bounds = new Rectangle(2, 35, 0, 12) });
        using var host = new SkiaControlSurface(root);
        host.Resize(80, 60);
        using var surface = SKSurface.Create(new SKImageInfo(80, 60));
        using var profiler = PerformanceProfiler.Start();

        host.Render(surface.Canvas);
        PerformanceWorkMetrics first = profiler.Capture().LatestFrame!.Value.Work;
        Assert.Equal(4, first.ControlsVisited); // surface's content root plus three children
        Assert.Equal(2, first.ControlsRepainted);
        Assert.Equal(2, first.ControlsComposited);
        Assert.Equal(1, first.InvisibleControlsSkipped);
        Assert.Equal(1, first.ZeroSizeControlsSkipped);
        Assert.Equal(0, first.ControlCacheHits);

        // Unpaintable children retain their dirty state in the existing renderer and
        // therefore keep the ancestor dirty. Remove them before asserting cache reuse.
        var empty = root.Controls[2];
        var hidden = root.Controls[1];
        root.Controls.Remove(empty); root.Controls.Remove(hidden);
        empty.Dispose(); hidden.Dispose();
        host.Render(surface.Canvas);
        host.Render(surface.Canvas);
        PerformanceWorkMetrics second = profiler.Capture().LatestFrame!.Value.Work;
        Assert.Equal(1, second.ControlsVisited);
        Assert.Equal(0, second.ControlsRepainted);
        Assert.Equal(1, second.ControlsComposited);
        Assert.Equal(1, second.ControlCacheHits);
    }

    [Fact]
    public void PaintFailureLeavesTheBufferDirtyAndTheNextFrameCompletes()
    {
        using var root = new ThrowingPaintControl();
        using var host = new SkiaControlSurface(root);
        host.Resize(60, 40);
        using var surface = SKSurface.Create(new SKImageInfo(60, 40));
        using var profiler = PerformanceProfiler.Start();
        root.ThrowOnPaint = true;

        Assert.Throws<InvalidOperationException>(() => host.Render(surface.Canvas));
        PerformanceFrameMetrics failed = profiler.Capture().LatestFrame!.Value;
        Assert.False(failed.Completed);
        Assert.Equal(1, failed.Work.ControlsRepainted);
        Assert.Equal(0, failed.Work.ControlsComposited);
        Assert.True(root.NeedsPaint);

        root.ThrowOnPaint = false;
        host.Render(surface.Canvas);
        PerformanceFrameMetrics recovered = profiler.Capture().LatestFrame!.Value;
        Assert.True(recovered.Completed);
        Assert.Equal(1, recovered.Work.ControlsRepainted);
        Assert.Equal(1, recovered.Work.ControlsComposited);
        Assert.False(root.NeedsPaint);
    }

    [Fact]
    public void RepaintRegionsUseCurrentRootGeometryAndAreAbsentByDefault()
    {
        using var root = new Control();
        var child = new Control { Bounds = new Rectangle(12, 8, 20, 10) };
        root.Controls.Add(child);
        using var host = new SkiaControlSurface(root);
        host.Resize(80, 60);
        using var surface = SKSurface.Create(new SKImageInfo(80, 60));
        using (var profiler = PerformanceProfiler.Start())
        {
            host.Render(surface.Canvas);
            Assert.Empty(profiler.Capture().Regions);
        }

        child.Invalidate();
        using var detailed = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { ShowRepaintRegions = true, ShowControlBounds = true, ShowClipBounds = true }
        });
        host.Render(surface.Canvas);
        Assert.Contains(detailed.Capture().Regions, region =>
            region.Kind == PerformanceRegionKind.Repaint && region.Bounds == new RectangleF(12, 8, 20, 10));
    }

    [Fact]
    public void BackBufferResizeDisposesOnlyResourcesOwnedByTheRecordingSession()
    {
        using var control = new Control { Size = new Size(20, 10) };
        using var profiler = PerformanceProfiler.Start();
        control.GetBackBuffer();
        control.Width = 30;
        control.GetBackBuffer();
        control.Dispose();
        PerformanceWorkMetrics work = profiler.Capture().UnframedWork;
        Assert.Equal(2, work.SurfaceAllocations);
        Assert.Equal(2, work.SurfaceReleases);
    }

    [Fact]
    public void InvalidationBatchCountsRequestsSeparatelyFromDeliveredWindowInvalidations()
    {
        using var window = new RecordingWindow(1);
        using var child = new Control { Size = new Size(20, 10) };
        window.Controls.Add(child);
        child.CreateControl();
        window.Proxy.Invalidations = 0;
        using var profiler = PerformanceProfiler.Start();

        using (Application.BeginVisualInvalidationBatch())
        {
            child.Invalidate();
            child.Invalidate();
            child.Invalidate();
            Assert.Equal(0, window.Proxy.Invalidations);
        }

        PerformanceWorkMetrics work = profiler.Capture().UnframedWork;
        Assert.Equal(3, work.InvalidationRequests);
        Assert.Equal(1, work.WindowInvalidationRequests);
        Assert.Equal(2, work.CoalescedWindowInvalidations);
        Assert.Equal(1, window.Proxy.Invalidations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void NativeWindowRegionUsesPaintPixelsConvertedBackToRootLogicalUnits(double scale)
    {
        using var window = new RecordingWindow(scale);
        window.Style.Border.Width = 0;
        var child = new Control { Bounds = new Rectangle(12, 8, 20, 10) };
        window.Controls.Add(child);
        using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { ShowControlBounds = true }
        });
        using (var frame = PerformanceRecorder.BeginRender(window.adapter,
            new PerformanceRenderInfo { Scale = scale, LogicalWidth = 80, LogicalHeight = 60 }))
        {
            child.RecordPerformanceRegion(PerformanceRegionKind.ControlBounds);
            frame.Complete();
        }
        Assert.Contains(profiler.Capture().Regions, region => region.Bounds == new RectangleF(12, 8, 20, 10));
    }

    [Fact]
    public void DelayedAnimationPassDoesNotClaimAnUpdateTick()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var profiler = PerformanceProfiler.Start();
        harness.Scheduler.Start(new object(), "test", _ => { }, new AnimationOptions
        {
            Delay = TimeSpan.FromMilliseconds(20), Duration = TimeSpan.FromMilliseconds(40)
        });
        harness.TickSource.Fire();
        Assert.Equal(0, profiler.Capture().UnframedWork.AnimationTicks);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(40));
        Assert.Equal(2, profiler.Capture().UnframedWork.AnimationTicks);
        Assert.Equal(2, harness.Scheduler.GetDiagnostics().TickCount);
    }

    private sealed class PreferredControl : Control
    {
        internal PreferredControl() => SetExtendedState(ExtendedStates.UserPreferredSizeCache, true);
        internal int CoreCalls { get; private set; }
        internal override Size GetPreferredSizeCore(Size proposedSize)
        {
            CoreCalls++;
            return new Size(30, 12);
        }
    }

    private sealed class ThrowingPaintControl : Control
    {
        internal bool ThrowOnPaint { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (ThrowOnPaint)
                throw new InvalidOperationException("paint test");
            base.OnPaint(e);
        }
    }

    private sealed class RecordingWindow : WindowBase
    {
        internal RecordingWindow(double scale) : this(DispatchProxy.Create<IWindowBaseImpl, WindowProxy>(), scale) { }
        private RecordingWindow(IWindowBaseImpl implementation, double scale) : base(implementation)
        {
            Proxy = (WindowProxy)implementation;
            Proxy.Scale = scale;
            adapter.Bounds = new Rectangle(0, 0, 800, 600);
        }
        internal WindowProxy Proxy { get; }
    }

    private class WindowProxy : DispatchProxy
    {
        internal int Invalidations { get; set; }
        internal double Scale { get; set; } = 1;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IWindowBaseImpl.Invalidate)) Invalidations++;
            if (targetMethod?.Name is "get_RenderScaling" or "get_DesktopScaling") return Scale;
            if (targetMethod?.Name == "get_ClientSize") return new ModernFormsNext.WindowKit.Size(800, 600);
            if (targetMethod is null || targetMethod.ReturnType == typeof(void)) return null;
            return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }
}
