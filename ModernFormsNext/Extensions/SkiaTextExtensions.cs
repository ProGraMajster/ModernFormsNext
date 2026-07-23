using System;
using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext
{
    /// <summary>
    /// A collection of extension methods to facilitate text drawing operations.
    /// </summary>
    public static class SkiaTextExtensions
    {
        private static TextPaintOptions CreateOptions () => new TextPaintOptions { Edging = SKFontEdging.SubpixelAntialias };

        /// <summary>
        /// Draws a string of text.
        /// </summary>
        public static void DrawText (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false)
            => canvas.DrawTextCore (
                text,
                control.CurrentStyle.GetFont (),
                control.LogicalToDeviceUnits (control.CurrentStyle.GetFontSize ()),
                bounds,
                control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor,
                alignment,
                selectionStart,
                selectionEnd,
                selectionColor,
                maxLines,
                ellipsis,
                control.CurrentStyle.GetFontStyle (),
                control.Enabled ? control.EffectiveTextBrush : null);

        /// <summary>
        /// Draws a string of text.
        /// </summary>
        public static void DrawText (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false, FontStyle fontStyle = FontStyle.Regular)
            => canvas.DrawTextCore (text, font, fontSize, bounds, color, alignment, selectionStart, selectionEnd, selectionColor, maxLines, ellipsis, fontStyle, brush: null);

        internal static void DrawTextCore (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false, FontStyle fontStyle = FontStyle.Regular, Drawing.Brush? brush = null)
        {
            if (string.IsNullOrWhiteSpace (text))
                return;

            var tb = TextMeasurer.CreateTextBlock (text, font, fontSize, bounds.Size, TextMeasurer.GetTextAlign (alignment), brush is null ? color : SKColors.White, maxLines, ellipsis, fontStyle);
            var location = bounds.Location;
            var vertical = TextMeasurer.GetVerticalAlign (alignment);

            if (vertical == SKTextAlign.Right)
                location.Y = bounds.Bottom - (int)tb.MeasuredHeight;
            else if (vertical == SKTextAlign.Center)
                location.Y += (bounds.Height - (int)tb.MeasuredHeight) / 2;

            var options = CreateOptions ();

            if (selectionStart >= 0 && selectionEnd >= 0 && selectionStart != selectionEnd) {
                options.Selection = new TextRange (selectionStart, selectionEnd);
                options.SelectionColor = selectionColor ?? Theme.TextSelectionBackgroundColor;
            }

            canvas.Save ();
            canvas.Clip (bounds);

            if (brush is null) {
                tb.Paint (canvas, new SKPoint (location.X, location.Y), options);
            } else {
                var layerBounds = bounds.ToSKRect ();

                // Paint text into a temporary alpha mask, then fill that mask with the brush.
                // This keeps gradient text support in the shared text path without changing
                // individual renderers into shader-aware text renderers.
                canvas.SaveLayer (layerBounds, null);

                try {
                    tb.Paint (canvas, new SKPoint (location.X, location.Y), options);
                    SkiaExtensions.RenderBrushBackground (canvas, layerBounds, brush, color, SKBlendMode.SrcIn);
                } finally {
                    canvas.Restore ();
                }
            }

            canvas.Restore ();
        }

        /// <summary>
        /// Draws a block of text.
        /// </summary>
        public static void DrawTextBlock (this SKCanvas canvas, TextBlock block, Point location, TextSelection selection)
        {
            var options = CreateOptions ();

            if (!selection.IsEmpty ()) {
                options.Selection = new TextRange (selection.Start, selection.End);
                options.SelectionColor = selection.Color;
            }

            block.Paint (canvas, new SKPoint (location.X, location.Y), options);
        }

        /// <summary>
        /// Draws a single line of text.
        /// </summary>
        public static void DrawTextLine (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, bool ellipsis = false)
            => canvas.DrawTextCore (
                text,
                control.CurrentStyle.GetFont (),
                control.LogicalToDeviceUnits (control.CurrentStyle.GetFontSize ()),
                bounds,
                control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor,
                alignment,
                maxLines: 1,
                ellipsis: ellipsis,
                fontStyle: control.CurrentStyle.GetFontStyle (),
                brush: control.Enabled ? control.EffectiveTextBrush : null);
    }
}
