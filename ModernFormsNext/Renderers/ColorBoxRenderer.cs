using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    public class ColorBoxRenderer : Renderer<ColorBox>
    {
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

            // base hue color
            huePaint.Color = ColorHelper.FromHsv (control.Hue, 1f, 1f);
            canvas.DrawRect (rect, huePaint);

            // saturation gradient (white → transparent)
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

            // value gradient (transparent → black)
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

            canvas.DrawRect (rect, border);

            DrawSelector (control, e, bounds);
        }

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

        private static SKRect ToRect (Rectangle rect)
            => new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}
