using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerSurface : Panel
{
    private readonly DesignerSession state;
    private readonly DesignerSurfaceRenderer renderer;
    private readonly DesignerMouseController mouseController;

    internal DesignerMouseController MouseController => mouseController;

    internal event EventHandler? DefaultEventRequested;

    public DesignerSurface(DesignerSession state)
    {
        this.state = state;
        renderer = new DesignerSurfaceRenderer();
        mouseController = new DesignerMouseController(state);
        // Panel is normally not selectable. The design surface owns keyboard gestures such
        // as Escape, so admit it to the existing focus scope rather than intercepting keys
        // through a separate host or global keyboard path.
        SetControlBehavior(ControlBehaviors.Selectable, true);
        TabStop = true;
        Style.BackgroundColor = DesignerColors.Workspace;
        Style.Border.Width = 1;
        Style.Border.Color = DesignerColors.PanelBorder;

        state.SelectionChanged += (_, _) => Invalidate();
        state.DocumentChanged += (_, _) => { mouseController.InvalidateGuides(); Invalidate(); };
        state.SettingsChanged += (_, _) => { mouseController.InvalidateGuides(); Invalidate(); };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Select();
        mouseController.HandleMouseDown(this, e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape && mouseController.CancelOperation(this, "Escape"))
        {
            e.Handled = true;
            Invalidate();
        }
    }

    internal override void CancelPointerInteraction(int? pointerId = null)
    {
        // The shell owns edit commands. The surface owns only its in-flight pointer gesture, so a
        // focus or native-capture transition can roll back that gesture without touching history.
        mouseController.CancelOperation(this, pointerId is null ? "FocusOrCaptureLost" : $"PointerCanceled:{pointerId}");
        base.CancelPointerInteraction(pointerId);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        mouseController.HandleMouseMove(this, e);
        Invalidate();
    }

    protected override void OnDoubleClick(MouseEventArgs e)
    {
        base.OnDoubleClick(e);
        if (IsDisposed || Disposing) return;
        if (e.Button != MouseButtons.Left || e.Control || e.Shift || e.Alt || state.SelectedNodes.Count > 1) return;
        var point = DesignerDpiCoordinateConverter.DeviceToLogicalPoint(e.X, e.Y, Scaling);
        var mapper = new DesignerCoordinateMapper();
        if (!mapper.TryMapToDocument(state, Width, Height, point.X, point.Y, out var documentPoint)) return;
        var hit = new DesignerHitTestService(mapper).HitTestControl(state, documentPoint);
        if (!ReferenceEquals(hit.Node, state.SelectedNode)) return;
        // Resolve the canonical pointer transaction before requesting a code edit. A double click
        // must not leave a drag/capture open across an asynchronous editor request.
        mouseController.CancelOperation(this, "DefaultEvent");
        DefaultEventRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        mouseController.HandleMouseUp(this, e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        renderer.Render(e, state, Width, Height, mouseController.MarqueeBounds, mouseController.Guides);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            renderer.Dispose();
        base.Dispose(disposing);
    }
}
