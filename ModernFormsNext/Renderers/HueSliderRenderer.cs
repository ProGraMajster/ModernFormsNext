using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Provides rendering logic for the <see cref="HueSlider"/> control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This renderer draws a vertical hue gradient covering the full HSV hue spectrum (0–360°).
    /// </para>
    /// <para>
    /// The gradient includes the following color stops:
    /// <list type="bullet">
    /// <item><description>Red → Yellow → Green → Cyan → Blue → Magenta → Red</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// A horizontal marker line is rendered to indicate the currently selected hue.
    /// </para>
    /// </remarks>
    public class HueSliderRenderer : Renderer<HueSlider>
    {
        /// <summary>
        /// Renders the <see cref="HueSlider"/> control.
        /// </summary>
        /// <param name="control">The control to render.</param>
        /// <param name="e">The paint event data.</param>
        /// <remarks>
        /// The rendering consists of:
        /// <list type="number">
        /// <item><description>Hue gradient background</description></item>
        /// <item><description>Border</description></item>
        /// <item><description>Selection marker</description></item>
        /// </list>
        /// </remarks>
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
            }) 
            {
                // Hue gradient
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

        /// <summary>
        /// Calculates the drawable content area of the control excluding borders.
        /// </summary>
        /// <param name="control">The control instance.</param>
        /// <param name="e">Optional paint event data.</param>
        /// <returns>A rectangle representing the content bounds.</returns>
        /// <remarks>
        /// The result accounts for DPI scaling using logical-to-device unit conversion.
        /// </remarks>
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

        /// <summary>
        /// Draws the selection marker indicating the current hue.
        /// </summary>
        /// <param name="control">The control containing the hue value.</param>
        /// <param name="e">The paint event data.</param>
        /// <param name="bounds">The drawable bounds.</param>
        /// <remarks>
        /// The marker is drawn as two horizontal lines (black outer line and white inner line)
        /// to ensure visibility against any background color.
        /// </remarks>
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
