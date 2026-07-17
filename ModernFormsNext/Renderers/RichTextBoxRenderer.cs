using System;
using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Renders a <see cref="RichTextBox"/> using the shared SkiaSharp text pipeline.
    /// </summary>
    /// <remarks>
    /// The renderer intentionally follows <see cref="TextBoxRenderer"/> for caret, selection,
    /// clipping, and scrollbar behavior. Only the text block source differs: rich text blocks are
    /// built from the RichTextBox formatting ranges rather than from the plain TextBox document.
    /// </remarks>
    public class RichTextBoxRenderer : Renderer<RichTextBox>
    {
        /// <inheritdoc/>
        protected override void Render(RichTextBox control, PaintEventArgs e)
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

            if (control.Selected) {
                var caret = TextMeasurer.GetCursorLocation(
                    block,
                    textOrigin,
                    control.document.CursorLayoutCodePointIndex,
                    control.CurrentFontSize);
                DrawCaret(e, caret);
            }

            e.Canvas.Restore();
        }

        private static void DrawCaret(PaintEventArgs e, Rectangle caret)
        {
            using var paint = new SKPaint {
                Color = Theme.ForegroundColor,
                IsAntialias = false,
                StrokeWidth = Math.Max(1, e.LogicalToDeviceUnits(1))
            };

            var x = caret.Left;
            var top = caret.Top;
            var bottom = Math.Max(caret.Top + 1, caret.Bottom);

            e.Canvas.DrawLine(x, top, x, bottom, paint);
        }
    }
}
