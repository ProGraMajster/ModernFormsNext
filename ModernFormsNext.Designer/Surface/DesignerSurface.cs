using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerSurface : Panel
{
    private readonly DesignerSession state;
    private readonly DesignerSurfaceRenderer renderer;
    private readonly DesignerMouseController mouseController;

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

        if (e.KeyCode == Keys.Delete)
        {
            e.Handled = state.DeleteSelectedNode();
            Invalidate();
            return;
        }

        if (!e.Control)
            return;

        if (e.KeyCode == Keys.C)
        {
            e.Handled = state.CopySelectedNode();
            return;
        }

        if (e.KeyCode == Keys.V)
        {
            e.Handled = state.PasteCopiedNode();
            Invalidate();
            return;
        }

        if (e.KeyCode == Keys.D)
        {
            e.Handled = state.DuplicateSelectedNode();
            Invalidate();
        }
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
