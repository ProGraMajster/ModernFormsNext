using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

/// <summary>Edits only the dialog's borrowed draft using the canonical vector converter.</summary>
internal sealed class DesignerPathCanvas : Control
{
    private readonly PathGeometry geometry;
    private readonly List<Handle> handles = [];
    private readonly SKPaint stroke = new() { IsAntialias = true, Color = SKColors.CornflowerBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
    private readonly SKPaint nodePaint = new() { IsAntialias = true, Color = SKColors.Navy };
    private SKPath? path;
    private int version = -1;
    private Size viewportSize;
    private Matrix3x2 view = Matrix3x2.Identity;
    private Matrix3x2 inverse;
    private bool canEdit;
    private Handle? dragged;
    private PointF dragOrigin;
    private int pointerId;
    private int selectedFigure;
    private int selectedSegment;

    internal DesignerPathCanvas(PathGeometry geometry)
    {
        this.geometry = geometry;
        geometry.Changed += GeometryChanged;
        Name = "PathCanvas";
        TabStop = true;
        AccessibleName = "Path nodes and Bezier handles";
    }

    internal event Action<int, int>? SelectionChanged;
    internal event Action? DraftChanged;
    internal event Action<string?>? DiagnosticChanged;
    internal string? Diagnostic { get; private set; }

    internal void SelectSegment(int figure, int segment)
    {
        selectedFigure = figure;
        selectedSegment = segment;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsurePath();
        e.Canvas.Save();
        try
        {
            e.Canvas.Scale(ScaleFactor.Width, ScaleFactor.Height);
            nodePaint.Color = SKColors.White;
            e.Canvas.DrawRect(new SKRect(0, 0, Width, Height), nodePaint);
            if (path is not null) e.Canvas.DrawPath(path, stroke);
            BuildHandles();
            foreach (var handle in handles)
            {
                PointF point = ToView(handle.Get());
                nodePaint.Color = handle.IsControl ? SKColors.DarkOrange : SKColors.Navy;
                if (handle.Anchor is { } anchor)
                {
                    PointF origin = ToView(anchor());
                    e.Canvas.DrawLine(origin.X, origin.Y, point.X, point.Y, nodePaint);
                }
                e.Canvas.DrawCircle(point.X, point.Y, handle.IsControl ? 5 : 4, nodePaint);
            }
        }
        finally { e.Canvas.Restore(); }
        base.OnPaint(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || dragged is not null) return;
        EnsurePath();
        if (!canEdit) return;
        BuildHandles();
        PointF pointer = Logical(e);
        foreach (var handle in handles.AsEnumerable().Reverse())
        {
            PointF point = ToView(handle.Get());
            if (MathF.Abs(point.X - pointer.X) > 8 || MathF.Abs(point.Y - pointer.Y) > 8) continue;
            dragged = handle;
            dragOrigin = handle.Get();
            pointerId = e.PointerId;
            SelectionChanged?.Invoke(handle.Figure, handle.Segment);
            e.Handled = true;
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragged is not { } handle || e.PointerId != pointerId) return;
        Vector2 point = Vector2.Transform(new Vector2(Logical(e).X, Logical(e).Y), inverse);
        // The view stays fixed during a gesture; moving an extremum cannot move the target beneath
        // the pointer. A cancelled capture restores exactly the coordinate present at mouse-down.
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || Math.Abs(point.X) > 1_000_000 || Math.Abs(point.Y) > 1_000_000) return;
        handle.Set(new PointF(point.X, point.Y));
        DraftChanged?.Invoke();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (dragged is not null && e.PointerId == pointerId && e.Button == MouseButtons.Left)
        {
            dragged = null;
            version = -1;
            DraftChanged?.Invoke();
            Invalidate();
            e.Handled = true;
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && dragged is not null)
        {
            CancelDrag();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(EventArgs e) { CancelDrag(); base.OnLostFocus(e); }
    internal override void CancelPointerInteraction(int? pointerId = null)
    {
        if (dragged is not null && pointerId is not null && pointerId != this.pointerId) return;
        if (pointerId is null || pointerId == this.pointerId) CancelDrag();
        base.CancelPointerInteraction(pointerId);
    }
    private void CancelDrag()
    {
        if (dragged is not { } handle) return;
        dragged = null;
        handle.Set(dragOrigin);
        version = -1;
        Capture = false;
        DraftChanged?.Invoke();
        Invalidate();
    }
    private void GeometryChanged(object? sender, EventArgs e) => Invalidate();
    private PointF Logical(MouseEventArgs e) => new(e.X / ScaleFactor.Width, e.Y / ScaleFactor.Height);
    private PointF ToView(PointF point)
    {
        Vector2 value = Vector2.Transform(new Vector2(point.X, point.Y), geometry.Transform * view);
        return new(value.X, value.Y);
    }

    private void EnsurePath()
    {
        if (version == geometry.Version && viewportSize == ClientSize) return;
        path?.Dispose();
        path = null;
        canEdit = false;
        Diagnostic = null;
        try
        {
            // Validate neutral expanded data and Boolean work before entering native conversion.
            _ = DesignerPropertyValueEditor.ToDesignPropertyValue(geometry, typeof(Geometry));
            path = SkiaGeometryConverter.CreatePath(geometry, new SizeF(1, 1));
            if (dragged is null)
            {
                SKRect bounds = path.Bounds;
                float scale = Math.Min(Math.Max(1, Width - 40) / Math.Max(1, bounds.Width), Math.Max(1, Height - 40) / Math.Max(1, bounds.Height));
                view = Matrix3x2.CreateTranslation(-bounds.MidX, -bounds.MidY) * Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(Width / 2f, Height / 2f);
            }
            var mapping = geometry.Transform * view;
            canEdit = Matrix3x2.Invert(mapping, out inverse) && IsFinite(inverse);
            path.Transform(SkiaGeometryConverter.ToSkiaMatrix(view));
            if (!canEdit) Diagnostic = "The geometry transform is singular; use the coordinate fields.";
        }
        catch (Exception failure) when (failure is FormatException or ArgumentException or InvalidOperationException)
        {
            path?.Dispose(); path = null;
            Diagnostic = failure.Message;
        }
        version = geometry.Version;
        viewportSize = ClientSize;
        DiagnosticChanged?.Invoke(Diagnostic);
    }
    private static bool IsFinite(Matrix3x2 matrix) => float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) && float.IsFinite(matrix.M21) &&
        float.IsFinite(matrix.M22) && float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);

    private void BuildHandles()
    {
        handles.Clear();
        if (!canEdit || selectedFigure < 0 || selectedFigure >= geometry.Figures.Count) return;
        var figure = geometry.Figures[selectedFigure];
        handles.Add(new(() => figure.StartPoint, point => figure.StartPoint = point, selectedFigure, -1));
        for (int i = 0; i < figure.Segments.Count; i++)
        {
            var segment = figure.Segments[i];
            int index = i;
            switch (segment)
            {
                case LineSegment line: handles.Add(new(() => line.Point, point => line.Point = point, selectedFigure, index)); break;
                case QuadraticBezierSegment quadratic:
                    handles.Add(new(() => quadratic.Point, point => quadratic.Point = point, selectedFigure, index));
                    if (i == selectedSegment) handles.Add(new(() => quadratic.ControlPoint, point => quadratic.ControlPoint = point, selectedFigure, index, true, () => quadratic.Point));
                    break;
                case BezierSegment cubic:
                    handles.Add(new(() => cubic.Point, point => cubic.Point = point, selectedFigure, index));
                    if (i == selectedSegment)
                    {
                        PointF start = i == 0 ? figure.StartPoint : EndPoint(figure.Segments[i - 1]);
                        handles.Add(new(() => cubic.ControlPoint1, point => cubic.ControlPoint1 = point, selectedFigure, index, true, () => start));
                        handles.Add(new(() => cubic.ControlPoint2, point => cubic.ControlPoint2 = point, selectedFigure, index, true, () => cubic.Point));
                    }
                    break;
                case ArcSegment arc: handles.Add(new(() => arc.Point, point => arc.Point = point, selectedFigure, index)); break;
            }
        }
    }
    private static PointF EndPoint(PathSegment segment) => segment switch
    {
        LineSegment line => line.Point, QuadraticBezierSegment quadratic => quadratic.Point,
        BezierSegment cubic => cubic.Point, ArcSegment arc => arc.Point, _ => default
    };
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelDrag();
            geometry.Changed -= GeometryChanged;
            path?.Dispose(); path = null;
            stroke.Dispose(); nodePaint.Dispose();
        }
        base.Dispose(disposing);
    }
    private sealed record Handle(Func<PointF> Get, Action<PointF> Set, int Figure, int Segment, bool IsControl = false, Func<PointF>? Anchor = null);
}
