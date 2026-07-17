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

    protected sealed override void OnPaint(PaintEventArgs e)
    {
        using (var logicalPaintScope = DesignerLogicalPaintScope.Begin(e))
        {
            var logicalPaintArgs = logicalPaintScope.PaintArgs;

            // Width, Height, and all panel metrics are logical values. The scope transforms the
            // device-pixel backing canvas once so derived panels cannot accidentally scale text but
            // leave backgrounds, clipping rectangles, or hit-test geometry unscaled.
            logicalPaintArgs.Canvas.FillRectangle(0, 0, Width, Height, DesignerColors.PanelBackground);
            logicalPaintArgs.Canvas.FillRectangle(0, 0, Width, HeaderHeight, DesignerColors.PanelHeader);
            logicalPaintArgs.Canvas.DrawText(
                Title,
                Theme.UIFont,
                logicalPaintArgs.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(
                    logicalPaintArgs.LogicalToDeviceUnits(10),
                    0,
                    logicalPaintArgs.LogicalToDeviceUnits(Math.Max(1, Width - 20)),
                    logicalPaintArgs.LogicalToDeviceUnits(HeaderHeight)),
                DesignerColors.Text,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);

            OnPaintContent(logicalPaintArgs);
        }

        // Child controls already own device-pixel backing bitmaps. Compose them only after the
        // logical transform has been restored; otherwise DPI would be applied for a second time.
        base.OnPaint(e);

        // Keep the border above child composition, matching the established designer chrome
        // order while still drawing it in the same logical coordinate system as the panel.
        using var borderPaintScope = DesignerLogicalPaintScope.Begin(e);
        borderPaintScope.PaintArgs.Canvas.DrawRectangle(0, 0, Width, Height, DesignerColors.PanelBorder);
    }

    /// <summary>
    /// Paints panel-specific content using logical designer pixels.
    /// </summary>
    /// <param name="e">
    /// Paint arguments whose canvas is already transformed from logical pixels to device pixels
    /// and whose <see cref="PaintEventArgs.Scaling"/> is 1.
    /// </param>
    protected virtual void OnPaintContent(PaintEventArgs e)
    {
    }
}
