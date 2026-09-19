using System.Drawing;
using ModernFormsNext.Diagnostics;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class PerformanceHostTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(2d)]
    public void LateProfilerObservesActualHeadlessFramebufferWithoutDuplicatingSharedRender(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var window = host.Show(new PaintControl(), new TestViewport(120, 80, scale));
        using var prior = window.CaptureRenderedSnapshot();
        using var profiler = PerformanceProfiler.Start();
        using var rendered = window.CaptureRenderedSnapshot();
        var frame = Assert.Single(profiler.Capture().Frames);
        Assert.True(frame.Completed);
        Assert.Equal("Headless", frame.RenderInfo.Backend);
        Assert.Equal(PerformanceFrameBoundary.OffscreenCapture, frame.RenderInfo.Boundary);
        Assert.True(frame.RenderInfo.IsOffscreen);
        Assert.Equal(PerformanceAcceleration.Software, frame.RenderInfo.Acceleration);
        Assert.Equal(scale, frame.RenderInfo.Scale);
        Assert.Equal(rendered.PixelWidth, frame.RenderInfo.PixelWidth);
        Assert.Equal(rendered.PixelHeight, frame.RenderInfo.PixelHeight);
        Assert.Equal(rendered.RowBytes, frame.RenderInfo.RowBytes);
        Assert.Equal((long)rendered.RowBytes * rendered.PixelHeight, frame.RenderInfo.BackingBytes);
        Assert.Equal(2, frame.RenderInfo.BackingGeneration);
        Assert.Equal(PerformanceRedraw.FullSurface, frame.RenderInfo.Redraw);
        Assert.Null(frame.RenderInfo.GpuDuration);
        Assert.Null(frame.RenderInfo.PresentationTimestamp);
        Assert.Null(frame.RenderInfo.GpuContextResetCount);
        Assert.Equal(prior.CopyPixels(), rendered.CopyPixels());
        Assert.Empty(window.Backend.Surfaces);
    }

    [Fact]
    public void EachWindowKeepsItsSourceAndActualResizeBackingFacts()
    {
        using var host = ModernFormsTestHost.Create();
        var first = host.Show(new PaintControl(), 100, 60);
        var second = host.Show(new PaintControl(), new TestViewport(50, 40, 2));
        using var profiler = PerformanceProfiler.Start();
        using var a = first.CaptureRenderedSnapshot();
        using var b = second.CaptureRenderedSnapshot();
        first.Resize(80, 70);
        using var c = first.CaptureRenderedSnapshot();
        var frames = profiler.Capture().Frames;
        Assert.Equal(3, frames.Count);
        Assert.NotEqual(frames[0].SourceId, frames[1].SourceId);
        Assert.Equal(frames[0].SourceId, frames[2].SourceId);
        Assert.Equal(2, frames[2].RenderInfo.BackingGeneration);
        Assert.Equal(80, frames[2].RenderInfo.PixelWidth);
        Assert.Equal(70, frames[2].RenderInfo.PixelHeight);
        Assert.Equal(1, frames[1].RenderInfo.BackingGeneration);
    }

    [Fact]
    public void FailedPaintUnwindsFrameAndFramebufferBeforeTheNextCapture()
    {
        using var host = ModernFormsTestHost.Create();
        var control = new PaintControl();
        var window = host.Show(control, 120, 80);
        using var profiler = PerformanceProfiler.Start();
        var expected = new InvalidOperationException("application paint");
        control.PaintFailure = expected;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => window.CaptureRenderedSnapshot()));
        Assert.False(Assert.Single(profiler.Capture().Frames).Completed);
        Assert.Empty(window.Backend.Surfaces);
        control.PaintFailure = null;
        control.Invalidate();
        using var next = window.CaptureRenderedSnapshot();
        Assert.True(profiler.Capture().LatestFrame!.Value.Completed);
        Assert.Equal(2, profiler.Capture().TotalFrames);
    }

    [Fact]
    public void BorrowedSurfaceReportsOnlyKnownFactsAndRestoresTheCallerCanvas()
    {
        using var host = ModernFormsTestHost.Create();
        using var root = new PaintControl();
        using var surface = new SkiaControlSurface(root);
        surface.Resize(100, 80);
        using var pixels = SKSurface.Create(new SKImageInfo(200, 160));
        pixels.Canvas.Scale(2);
        var matrix = pixels.Canvas.TotalMatrix;
        var clip = pixels.Canvas.DeviceClipBounds;
        using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { Visible = true }
        });
        surface.Render(pixels.Canvas, 2);
        var frame = Assert.Single(profiler.Capture().Frames);
        Assert.Equal(PerformanceFrameBoundary.SharedRender, frame.RenderInfo.Boundary);
        Assert.Equal(PerformanceAcceleration.Unknown, frame.RenderInfo.Acceleration);
        Assert.Equal(100, frame.RenderInfo.LogicalWidth);
        Assert.Equal(2, frame.RenderInfo.Scale);
        Assert.Null(frame.RenderInfo.PixelWidth);
        Assert.Null(frame.RenderInfo.RowBytes);
        Assert.Null(frame.RenderInfo.BackingGeneration);
        Assert.Equal(matrix, pixels.Canvas.TotalMatrix);
        Assert.Equal(clip, pixels.Canvas.DeviceClipBounds);
        Assert.True(frame.Duration >= frame.Work.RenderTime);
        Assert.True(frame.Duration >= frame.Work.OverlayTime);
    }

    [Fact]
    public void SurfaceInputAndDirectNativeImeProxyCountOnlyOutermostOperations()
    {
        using var host = ModernFormsTestHost.Create();
        using var root = new TextBox { Text = "seed" };
        using var surface = new SkiaControlSurface(root);
        surface.Resize(200, 100);
        root.Select();
        using var profiler = PerformanceProfiler.Start();
        surface.SetComposingText("a");
        Assert.Equal(1, profiler.Capture().UnframedWork.InputEvents);
        surface.CommitText("b");
        Assert.Equal(2, profiler.Capture().UnframedWork.InputEvents);
        Assert.NotNull(surface.TextInputClient);
        Assert.True(surface.TextInputClient!.SetSelection(0, 0));
        Assert.Equal(3, profiler.Capture().UnframedWork.InputEvents);
        surface.ProcessPointer(ControlSurfacePointerAction.Down, 10, 10);
        // PointerDown itself finishes composition through the same proxy. That nested call
        // must not count as another native input or inflate the inclusive category duration.
        Assert.Equal(4, profiler.Capture().UnframedWork.InputEvents);
        surface.ProcessPointer(ControlSurfacePointerAction.Up, 10, 10);
        surface.ProcessKeyDown(Keys.Left);
        surface.ProcessKeyUp(Keys.Left);
        Assert.Equal(7, profiler.Capture().UnframedWork.InputEvents);
    }

    [Fact]
    public void InputFailureDoesNotLeaveTheNextOperationNested()
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        var window = host.Show(button, 160, 80);
        using var profiler = PerformanceProfiler.Start();
        var expected = new InvalidOperationException("input callback");
        EventHandler<MouseEventArgs> handler = (_, _) => throw expected;
        button.MouseDown += handler;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => window.Input.PointerDown(new Point(10, 10))));
        button.MouseDown -= handler;
        window.Input.PointerUp(new Point(10, 10));
        Assert.Equal(2, profiler.Capture().UnframedWork.InputEvents);
    }

    [Fact]
    public void DisposedProfilerStopsCapturesWithoutOwningTheWindowOrPixels()
    {
        using var host = ModernFormsTestHost.Create();
        var window = host.Show(new PaintControl(), 100, 60);
        var profiler = PerformanceProfiler.Start();
        using var pixels = window.CaptureRenderedSnapshot();
        var detached = profiler.Capture();
        profiler.Dispose();
        using var after = window.CaptureRenderedSnapshot();
        Assert.Single(detached.Frames);
        Assert.Equal(pixels.CopyPixels(), after.CopyPixels());
        Assert.Empty(window.Backend.Surfaces);
    }

    private sealed class PaintControl : Control
    {
        internal Exception? PaintFailure;
        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(SKColors.CornflowerBlue);
        protected override void OnPaint(PaintEventArgs e)
        {
            if (PaintFailure is { } error) throw error;
            base.OnPaint(e);
        }
    }
}
