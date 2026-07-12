using System.Drawing;
using ModernFormsNext.Documents;
using SkiaSharp;

namespace ModernFormsNext.Renderers;

/// <summary>
/// Renders a <see cref="DocumentViewer"/> using SkiaSharp.
/// </summary>
public class DocumentViewerRenderer : Renderer<DocumentViewer>
{
    /// <inheritdoc/>
    protected override void Render(DocumentViewer control, PaintEventArgs e)
    {
        var layout = control.GetDocumentLayout();
        var clip = control.PaddedClientRectangle;

        e.Canvas.Save();
        e.Canvas.Clip(clip);

        foreach (var element in layout.Elements)
        {
            if (IsElementVisible(element.Bounds, control.ScrollOffset, clip))
                RenderElement(control, e, element);
        }

        e.Canvas.Restore();
    }

    internal static bool IsElementVisible(Rectangle documentBounds, int scrollOffset, Rectangle viewport)
    {
        documentBounds.Offset(0, -scrollOffset);
        return documentBounds.IntersectsWith(viewport);
    }

    private static void RenderElement(DocumentViewer control, PaintEventArgs e, DocumentLayoutElement element)
    {
        var bounds = element.Bounds;
        bounds.Offset(0, -control.ScrollOffset);

        switch (element)
        {
            case DocumentCodeBlockLayoutElement codeBlock:
                e.Canvas.FillRectangle(bounds, codeBlock.BackgroundColor);
                DrawCodeBlockHeader(control, e, codeBlock.Header, bounds);
                DrawTextElement(control, e, codeBlock, bounds);
                break;
            case DocumentLoadedImageLayoutElement image:
                e.Canvas.DrawBitmap(image.Bitmap, bounds, !control.Enabled);
                break;
            case DocumentImagePlaceholderLayoutElement placeholder:
                DrawImagePlaceholder(control, e, placeholder, bounds);
                break;
            case DocumentTaskCheckBoxLayoutElement task:
                ControlPaint.DrawCheckBox(e, bounds, task.CheckState, !control.Enabled);
                break;
            case DocumentTableCellLayoutElement cell:
                DrawTableCellElement(e, cell, bounds);
                break;
            case DocumentHorizontalRuleLayoutElement rule:
                e.Canvas.FillRectangle(bounds, rule.Color);
                break;
            case DocumentQuoteBorderLayoutElement quote:
                e.Canvas.FillRectangle(bounds, quote.Color);
                break;
            case DocumentTextLayoutElement text:
                DrawTextElement(control, e, text, bounds);
                break;
        }
    }

    private static void DrawTextElement(
        DocumentViewer control,
        PaintEventArgs e,
        DocumentTextLayoutElement element,
        Rectangle clipBounds)
    {
        var origin = element.TextOrigin;
        origin.Offset(0, -control.ScrollOffset);

        e.Canvas.Save();
        e.Canvas.Clip(clipBounds);
        e.Canvas.DrawTextBlock(element.TextBlock, origin, control.GetTextSelection(element));
        e.Canvas.Restore();
    }

    private static void DrawImagePlaceholder(
        DocumentViewer control,
        PaintEventArgs e,
        DocumentImagePlaceholderLayoutElement element,
        Rectangle bounds)
    {
        e.Canvas.FillRectangle(bounds, Theme.ControlLowColor);
        e.Canvas.DrawRectangle(
            bounds,
            element.Failed ? Theme.WarningHighlightColor : element.BorderColor,
            control.LogicalToDeviceUnits(1));

        var origin = element.TextOrigin;
        origin.Offset(0, -control.ScrollOffset);

        e.Canvas.Save();
        e.Canvas.Clip(bounds);
        e.Canvas.DrawTextBlock(element.TextBlock, origin, TextSelection.Empty);
        e.Canvas.Restore();
    }

    private static void DrawCodeBlockHeader(
        DocumentViewer control,
        PaintEventArgs e,
        DocumentCodeBlockHeaderLayout? header,
        Rectangle clipBounds)
    {
        if (header is null)
            return;

        var origin = header.TextOrigin;
        origin.Offset(0, -control.ScrollOffset);
        var separator = header.SeparatorBounds;
        separator.Offset(0, -control.ScrollOffset);

        e.Canvas.Save();
        e.Canvas.Clip(clipBounds);
        e.Canvas.DrawTextBlock(header.TextBlock, origin, TextSelection.Empty);
        e.Canvas.FillRectangle(separator, header.SeparatorColor);
        e.Canvas.Restore();
    }

    private static void DrawTableCellElement(PaintEventArgs e, DocumentTableCellLayoutElement element, Rectangle bounds)
    {
        if (element.BackgroundColor.Alpha > 0)
            e.Canvas.FillRectangle(bounds, element.BackgroundColor);

        if (element.BorderThickness > 0)
            e.Canvas.DrawRectangle(bounds, element.BorderColor, element.BorderThickness);
    }
}
