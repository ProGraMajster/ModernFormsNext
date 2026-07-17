using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;

namespace ModernFormsNext.Designer.Panels;

internal sealed class OutputPanel : DesignerPanelBase
{
    private const int ContentPadding = 8;
    private const int LineHeight = 20;

    private readonly DesignerSession state;

    public OutputPanel(DesignerSession state, string title = "Output")
        : base(title)
    {
        this.state = state;
        state.OutputChanged += (_, _) => Invalidate();
        SizeChanged += (_, _) => Invalidate();
    }

    protected override void OnPaintContent(PaintEventArgs e)
    {
        var availableHeight = Math.Max(1, Height - HeaderHeight - (ContentPadding * 2));
        var maxLines = Math.Max(1, availableHeight / LineHeight);
        var lines = state.OutputLines.TakeLast(maxLines).ToArray();
        var y = HeaderHeight + ContentPadding;

        foreach (var line in lines)
        {
            e.Canvas.DrawText(
                line,
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(ContentPadding), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(Math.Max(1, Width - (ContentPadding * 2))), e.LogicalToDeviceUnits(LineHeight)),
                DesignerColors.Text,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);
            y += LineHeight;
        }
    }
}
