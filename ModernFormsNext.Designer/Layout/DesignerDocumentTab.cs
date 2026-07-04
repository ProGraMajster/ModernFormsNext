using ModernFormsNext;
using ModernFormsNext.Designer.Services;
using SkiaSharp;

namespace ModernFormsNext.Designer.Layout;

internal sealed class DesignerDocumentTab : Panel
{
    private readonly DesignerSession state;
    private const int TabWidth = 220;
    private const int CloseButtonSize = 16;
    private const int CloseButtonRightPadding = 8;

    public DesignerDocumentTab(DesignerSession state)
    {
        this.state = state;
        Height = 32;
        Style.BackgroundColor = DesignerColors.Workspace;
        state.DocumentChanged += (_, _) => Invalidate();
        state.DocumentTabsChanged += (_, _) => Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        var index = Math.Max(0, e.X / TabWidth);

        if (IsCloseButtonHit(index, e.X, e.Y))
            state.CloseDocument(index);
        else
            state.SwitchDocument(index);

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Canvas.FillRectangle(ClientRectangle, DesignerColors.Workspace);

        for (var index = 0; index < state.OpenDocuments.Count; index++)
        {
            var document = state.OpenDocuments[index];
            var x = index * TabWidth;
            var active = index == state.ActiveDocumentIndex;

            if (x >= Width)
                break;

            e.Canvas.FillRectangle(x, 0, Math.Min(TabWidth, Width - x), Height, active ? new SKColor(42, 47, 54) : new SKColor(32, 37, 43));
            e.Canvas.DrawRectangle(x, 0, Math.Min(TabWidth, Width - x), Height, active ? new SKColor(92, 102, 114) : DesignerColors.PanelBorder);

            var dirtyMarker = document.IsDirty ? "*" : string.Empty;
            e.Canvas.DrawText(
                $"{document.DisplayName}{dirtyMarker} [Design]",
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(x + 10), 0, e.LogicalToDeviceUnits(TabWidth - 42), e.LogicalToDeviceUnits(Height)),
                active ? DesignerColors.Text : DesignerColors.MutedText,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);

            var closeBounds = GetCloseButtonBounds(index);
            var closeColor = active ? DesignerColors.Text : DesignerColors.MutedText;
            e.Canvas.DrawLine(closeBounds.Left + 4, closeBounds.Top + 4, closeBounds.Right - 4, closeBounds.Bottom - 4, closeColor);
            e.Canvas.DrawLine(closeBounds.Right - 4, closeBounds.Top + 4, closeBounds.Left + 4, closeBounds.Bottom - 4, closeColor);
        }
    }

    private bool IsCloseButtonHit(int index, int x, int y)
        => index >= 0
        && index < state.OpenDocuments.Count
        && GetCloseButtonBounds(index).Contains(x, y);

    private static System.Drawing.Rectangle GetCloseButtonBounds(int index)
    {
        var tabLeft = index * TabWidth;
        var left = tabLeft + TabWidth - CloseButtonRightPadding - CloseButtonSize;
        var top = (32 - CloseButtonSize) / 2;
        return new System.Drawing.Rectangle(left, top, CloseButtonSize, CloseButtonSize);
    }
}
