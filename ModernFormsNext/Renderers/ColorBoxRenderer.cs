using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Provides rendering logic for the <see cref="ColorBox"/> control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This renderer draws a 2D HSV color selection surface composed of:
    /// <list type="bullet">
    /// <item><description>Base hue color</description></item>
    /// <item><description>White gradient (saturation)</description></item>
    /// <item><description>Black gradient (value/brightness)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// It also renders a circular selector indicating the currently selected color.
    /// </para>
    /// </remarks>
    public class ColorBoxRenderer : Renderer<ColorBox>
    {
        /// <summary>
        /// Renders the <see cref="ColorBox"/> control.
        /// </summary>
        /// <param name="control">The control to render.</param>
        /// <param name="e">The paint event data.</param>
        /// <remarks>
        /// The rendering process consists of multiple layered gradients:
        /// base hue, saturation overlay, value overlay, and border.
        /// </remarks>
        protected override void Render (ColorBox control, PaintEventArgs e)
        {
            var bounds = GetContentBounds (control, e);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var canvas = e.Canvas;
            var rect = new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

            using var huePaint = new SKPaint { IsAntialias = false };
            using var whitePaint = new SKPaint { IsAntialias = false };
            using var blackPaint = new SKPaint { IsAntialias = false };
            using var border = new SKPaint {
                Style = SKPaintStyle.Stroke,
                Color = Theme.BorderLowColor,
                IsAntialias = true
            };

            // Base hue layer
            huePaint.Color = ColorHelper.FromHsv (control.Hue, 1f, 1f);
            canvas.DrawRect (rect, huePaint);

            // Saturation gradient (white → transparent)
            whitePaint.Shader = SKShader.CreateLinearGradient (
                new SKPoint (rect.Left, rect.Top),
                new SKPoint (rect.Right, rect.Top),
                new[]
                {
            SKColors.White,
            new SKColor(255,255,255,0)
                },
                null,
                SKShaderTileMode.Clamp);

            canvas.DrawRect (rect, whitePaint);

            // Value gradient (transparent → black)
            blackPaint.Shader = SKShader.CreateLinearGradient (
                new SKPoint (rect.Left, rect.Top),
                new SKPoint (rect.Left, rect.Bottom),
                new[]
                {
            new SKColor(0,0,0,0),
            SKColors.Black
                },
                null,
                SKShaderTileMode.Clamp);

            canvas.DrawRect (rect, blackPaint);

            // Border
            canvas.DrawRect (rect, border);

            DrawSelector (control, e, bounds);
        }

        /// <summary>
        /// Calculates the inner drawable area of the control excluding borders.
        /// </summary>
        /// <param name="control">The control instance.</param>
        /// <param name="e">Optional paint event data.</param>
        /// <returns>A rectangle representing the drawable content area.</returns>
        /// <remarks>
        /// The returned bounds account for DPI scaling via logical-to-device unit conversion.
        /// </remarks>
        public Rectangle GetContentBounds (ColorBox control, PaintEventArgs? e)
        {
            int border = e?.LogicalToDeviceUnits (1) ?? control.LogicalToDeviceUnits (1);
            var rect = control.ClientRectangle;

            return new Rectangle (
                rect.Left + border,
                rect.Top + border,
                Math.Max (1, rect.Width - (border * 2)),
                Math.Max (1, rect.Height - (border * 2)));
        }

        /// <summary>
        /// Draws the selection indicator showing the current saturation and value.
        /// </summary>
        /// <param name="control">The control containing the current color state.</param>
        /// <param name="e">The paint event data.</param>
        /// <param name="bounds">The drawable bounds.</param>
        /// <remarks>
        /// The selector is drawn as two concentric circles (black outer ring, white inner ring)
        /// to ensure visibility against any background color.
        /// </remarks>
        private void DrawSelector (ColorBox control, PaintEventArgs e, Rectangle bounds)
        {
            float x = bounds.Left + control.Saturation * Math.Max (1, bounds.Width - 1);
            float y = bounds.Top + (1f - control.Value) * Math.Max (1, bounds.Height - 1);

            using var outer = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = SKColors.Black
            };

            using var inner = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = SKColors.White
            };

            e.Canvas.DrawCircle (x, y, 7, outer);
            e.Canvas.DrawCircle (x, y, 8.5f, inner);
        }

        /// <summary>
        /// Converts a <see cref="Rectangle"/> to an <see cref="SKRect"/>.
        /// </summary>
        /// <param name="rect">The rectangle to convert.</param>
        /// <returns>An equivalent <see cref="SKRect"/>.</returns>
        private static SKRect ToRect (Rectangle rect)
            => new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}
