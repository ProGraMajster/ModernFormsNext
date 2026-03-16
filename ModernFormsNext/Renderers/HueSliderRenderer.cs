using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    public class HueSliderRenderer : Renderer<HueSlider>
    {
        protected override void Render (HueSlider control, PaintEventArgs e)
        {
            var bounds = GetContentBounds (control, e);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var canvas = e.Canvas;
            var rect = new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

            using (var paint = new SKPaint { IsAntialias = false })
            using (var border = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = Theme.BorderLowColor
            }) {
                paint.Shader = SKShader.CreateLinearGradient (
                    new SKPoint (rect.Left, rect.Top),
                    new SKPoint (rect.Left, rect.Bottom),
                    new[]
                    {
                        new SKColor(255, 0, 0),     // 0   red
                        new SKColor(255, 255, 0),   // 60  yellow
                        new SKColor(0, 255, 0),     // 120 green
                        new SKColor(0, 255, 255),   // 180 cyan
                        new SKColor(0, 0, 255),     // 240 blue
                        new SKColor(255, 0, 255),   // 300 magenta
                        new SKColor(255, 0, 0)      // 360 red
                    },
                    new[] { 0f, 1f / 6f, 2f / 6f, 3f / 6f, 4f / 6f, 5f / 6f, 1f },
                    SKShaderTileMode.Clamp);

                canvas.DrawRect (rect, paint);
                canvas.DrawRect (rect, border);
            }

            DrawMarker (control, e, bounds);
        }

        public Rectangle GetContentBounds (HueSlider control, PaintEventArgs? e)
        {
            int border = e?.LogicalToDeviceUnits (1) ?? control.LogicalToDeviceUnits (1);
            var rect = control.ClientRectangle;

            return new Rectangle (
                rect.Left + border,
                rect.Top + border,
                System.Math.Max (1, rect.Width - (border * 2)),
                System.Math.Max (1, rect.Height - (border * 2)));
        }

        private void DrawMarker (HueSlider control, PaintEventArgs e, Rectangle bounds)
        {
            // Top = 0°, bottom = 360°.
            float percent = control.Hue / 360f;
            float y = bounds.Top + percent * System.Math.Max (1, bounds.Height - 1);

            using var outlinePaint = new SKPaint {
                IsAntialias = true,
                Color = SKColors.Black,
                StrokeWidth = 3
            };

            using var linePaint = new SKPaint {
                IsAntialias = true,
                Color = SKColors.White,
                StrokeWidth = 1.5f
            };

            e.Canvas.DrawLine (bounds.Left - 3, y, bounds.Right + 3, y, outlinePaint);
            e.Canvas.DrawLine (bounds.Left - 2, y, bounds.Right + 2, y, linePaint);
        }
    }
}
