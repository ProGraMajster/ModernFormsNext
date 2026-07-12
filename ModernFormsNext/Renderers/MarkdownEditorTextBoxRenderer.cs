using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers;

/// <summary>
/// Renders the Markdown source surface through the shared RichTextBox text block.
/// </summary>
internal sealed class MarkdownEditorTextBoxRenderer : Renderer<MarkdownEditorTextBox>
{
    protected override void Render(MarkdownEditorTextBox control, PaintEventArgs e)
    {
        var text = control.document.DisplayText;
        if (text.Length == 0 && !control.Selected)
            return;

        var block = control.GetRichTextBlock();
        control.UpdateScrollBars(block);
        var textOrigin = control.GetTextOrigin(block);

        e.Canvas.Save();
        e.Canvas.Clip(control.PaddedClientRectangle);

        if (text.Length > 0)
            e.Canvas.DrawTextBlock(block, textOrigin, control.document.GetTextSelection());

        if (control.Selected)
        {
            var caret = TextMeasurer.GetCursorLocation(
                block,
                textOrigin,
                control.document.CursorIndex,
                control.CurrentFontSize);
            DrawCaret(control, e, caret);
        }

        e.Canvas.Restore();
    }

    private static void DrawCaret(MarkdownEditorTextBox control, PaintEventArgs e, Rectangle caret)
    {
        using var paint = new SKPaint
        {
            Color = control.CaretColor,
            IsAntialias = false,
            StrokeWidth = Math.Max(1, e.LogicalToDeviceUnits(1))
        };

        e.Canvas.DrawLine(caret.Left, caret.Top, caret.Left, Math.Max(caret.Top + 1, caret.Bottom), paint);
    }
}
