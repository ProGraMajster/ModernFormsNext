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

    public DesignerSurface(DesignerSession state)
    {
        this.state = state;
        renderer = new DesignerSurfaceRenderer();
        mouseController = new DesignerMouseController(state);
        TabStop = true;
        Style.BackgroundColor = DesignerColors.Workspace;
        Style.Border.Width = 1;
        Style.Border.Color = DesignerColors.PanelBorder;

        state.SelectionChanged += (_, _) => Invalidate();
        state.DocumentChanged += (_, _) => Invalidate();
        state.SettingsChanged += (_, _) => Invalidate();
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

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        mouseController.HandleMouseUp(this, e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        renderer.Render(e, state, Width, Height);
    }
}
