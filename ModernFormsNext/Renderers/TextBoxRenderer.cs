using System;
using System.Drawing;
using Topten.RichTextKit;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a class that can render a TextBox.
    /// </summary>
    public class TextBoxRenderer : Renderer<TextBox>
    {
        /// <inheritdoc/>
        protected override void Render (TextBox control, PaintEventArgs e)
        {
            var text = control.document.DisplayText;

            // Bail early if we don't need to draw anything
            if (text.Length == 0 && !control.Selected)
                return;

            var block = GetTextBlock (control);

            UpdateScrollBars (control, block);
            var text_origin = control.GetTextOrigin (block);

            e.Canvas.Save ();
            e.Canvas.Clip (control.PaddedClientRectangle);

            if (text.Length > 0)
                e.Canvas.DrawTextBlock (block, text_origin, GetTextSelection (control));

            if (control.Selected) {
                var caret = TextMeasurer.GetCursorLocation (block, text_origin, GetCursorIndex (control), GetCurrentFontSize (control));
                DrawCaret (e, caret);
            }

            e.Canvas.Restore ();
        }

        private static void DrawCaret (PaintEventArgs e, Rectangle caret)
        {
            using var paint = new SkiaSharp.SKPaint {
                Color = Theme.ForegroundColor,
                IsAntialias = false,
                StrokeWidth = Math.Max (1, e.LogicalToDeviceUnits (1))
            };

            var x = caret.Left;
            var top = caret.Top;
            var bottom = Math.Max (caret.Top + 1, caret.Bottom);

            e.Canvas.DrawLine (x, top, x, bottom, paint);
        }

        /// <summary>
        /// Gets the TextBox's font size.
        /// </summary>
        protected int GetCurrentFontSize (TextBox control) => control.CurrentFontSize;

        /// <summary>
        /// Gets the current index of the TextBox cursor.
        /// </summary>
        protected int GetCursorIndex (TextBox control) => control.document.CursorIndex;

        /// <summary>
        /// Gets the TextBox's text block.
        /// </summary>
        protected TextBlock GetTextBlock (TextBox control) => control.document.GetTextBlock ();

        /// <summary>
        /// Gets the TextBox's text origin.
        /// </summary>
        protected Point GetTextOrigin (TextBox control) => control.TextOrigin;

        /// <summary>
        /// Gets the TextBox's text selection.
        /// </summary>
        protected TextSelection GetTextSelection (TextBox control) => control.document.GetTextSelection ();

        /// <summary>
        /// Updates the TextBox's scroll bars.
        /// </summary>
        protected void UpdateScrollBars (TextBox control, TextBlock block) => control.UpdateScrollBars (block);
    }
}
