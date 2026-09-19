using System.Drawing;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Layout;
using SkiaSharp;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_performanceBackBufferProperty = PropertyStore.CreateKey();

    private void CapturePerformanceBackBuffer()
    {
        if (!PerformanceRecorder.IsEnabled)
            return;

        // No field is added to every Control. Only a buffer allocated during recording
        // stores a boxed lease; it contains no control or strong profiler reference.
        Properties.AddValue(s_performanceBackBufferProperty, PerformanceRecorder.CaptureResource(
            PerformanceCounterKind.SurfaceAllocations, PerformanceCounterKind.SurfaceReleases, this));
    }

    private void ReleasePerformanceBackBuffer()
    {
        if (!Properties.TryGetValue(s_performanceBackBufferProperty, out PerformanceResourceToken lease))
            return;
        Properties.RemoveObject(s_performanceBackBufferProperty);
        lease.Dispose();
    }

    internal void RecordPerformanceRegion(PerformanceRegionKind kind)
    {
        if (!PerformanceRecorder.ShouldRecordRegions)
            return;
        PerformanceRecorder.RecordRegion(this,
            PerformanceRootBounds(new RectangleF(0, 0, ScaledWidth, ScaledHeight)), kind);
    }

    internal void RecordPerformancePaintRegions(Control parent, SKRect parentClip)
    {
        if (!PerformanceRecorder.ShouldRecordRegions)
            return;

        RectangleF bounds = PerformanceRootBounds(new RectangleF(0, 0, ScaledWidth, ScaledHeight));
        PerformanceRecorder.RecordRegion(this, bounds, PerformanceRegionKind.ControlBounds);

        RectangleF clip = parent.PerformanceRootBounds(new RectangleF(
            parentClip.Left, parentClip.Top, parentClip.Width, parentClip.Height));
        clip.Intersect(bounds);
        // Cached child buffers have rectangular extents. Their parent buffers and the
        // root surface also clip composition; transformed AABBs are approximations,
        // not an assertion about rounded paths or native partial presentation.
        for (Control? ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            clip.Intersect(ancestor.PerformanceRootBounds(
                new RectangleF(0, 0, ancestor.ScaledWidth, ancestor.ScaledHeight)));
        }
        PerformanceRecorder.RecordRegion(this, clip, PerformanceRegionKind.ClipBounds);
    }

    private RectangleF PerformanceRootBounds(RectangleF localDeviceBounds)
    {
        PointF first = new(localDeviceBounds.Left, localDeviceBounds.Top);
        PointF second = new(localDeviceBounds.Right, localDeviceBounds.Top);
        PointF third = new(localDeviceBounds.Right, localDeviceBounds.Bottom);
        PointF fourth = new(localDeviceBounds.Left, localDeviceBounds.Bottom);
        Control current = this;
        while (current.Parent is { } parent)
        {
            first = current.ClientPointToParentPresentation(first);
            second = current.ClientPointToParentPresentation(second);
            third = current.ClientPointToParentPresentation(third);
            fourth = current.ClientPointToParentPresentation(fourth);
            if (parent is ControlAdapter adapter)
            {
                // ControlAdapter.OnPaint adds the managed form-border offset separately
                // from each child's presentation transform. Mirror the scaled border offset.
                var border = adapter.ParentForm.CurrentStyle.Border;
                float x = adapter.LogicalToDeviceUnits(border.Left.GetWidth());
                float y = adapter.LogicalToDeviceUnits(border.Top.GetWidth());
                first = new(first.X + x, first.Y + y);
                second = new(second.X + x, second.Y + y);
                third = new(third.X + x, third.Y + y);
                fourth = new(fourth.X + x, fourth.Y + y);
            }
            current = parent;
        }

        float scale = current.ScaleFactor.Width;
        float left = MathF.Min(MathF.Min(first.X, second.X), MathF.Min(third.X, fourth.X));
        float top = MathF.Min(MathF.Min(first.Y, second.Y), MathF.Min(third.Y, fourth.Y));
        float right = MathF.Max(MathF.Max(first.X, second.X), MathF.Max(third.X, fourth.X));
        float bottom = MathF.Max(MathF.Max(first.Y, second.Y), MathF.Max(third.Y, fourth.Y));
        return RectangleF.FromLTRB(left / scale, top / scale, right / scale, bottom / scale);
    }
}
