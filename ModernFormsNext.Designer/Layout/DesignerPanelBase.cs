using ModernFormsNext;
using SkiaSharp;

namespace ModernFormsNext.Designer.Layout;

internal abstract class DesignerPanelBase : Control
{
    protected const int HeaderHeight = 28;

    protected DesignerPanelBase(string title)
    {
        Title = title;
        TabStop = false;
        Style.BackgroundColor = DesignerColors.PanelBackground;
        Style.Border.Width = 1;
        Style.Border.Color = DesignerColors.PanelBorder;
    }

    protected string Title { get; private set; }

    protected static void ApplyPanelInputStyle(Control control)
    {
        control.Style.BackgroundColor = DesignerColors.PanelHeader;
        control.Style.ForegroundColor = DesignerColors.Text;
        control.Style.Border.Width = 1;
        control.Style.Border.Color = DesignerColors.PanelBorder;
    }

    public void SetTitle(string title)
    {
        Title = title;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Canvas.FillRectangle(ClientRectangle, DesignerColors.PanelBackground);
        e.Canvas.FillRectangle(0, 0, Width, HeaderHeight, DesignerColors.PanelHeader);
        e.Canvas.DrawText(
            Title,
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new System.Drawing.Rectangle(e.LogicalToDeviceUnits(10), 0, e.LogicalToDeviceUnits(Math.Max(1, Width - 20)), e.LogicalToDeviceUnits(HeaderHeight)),
            DesignerColors.Text,
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);

        // Child controls such as search boxes and toolbar buttons must be painted after the
        // panel chrome. Otherwise the background fill hides them completely.
        base.OnPaint(e);

        e.Canvas.DrawRectangle(0, 0, Width, Height, DesignerColors.PanelBorder);
    }
}
