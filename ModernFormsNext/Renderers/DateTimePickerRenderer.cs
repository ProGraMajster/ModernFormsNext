using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Provides rendering logic for <see cref="DateTimePicker"/>.
    /// </summary>
    /// <remarks>
    /// This renderer is responsible only for visual output.
    /// Control state, interaction, validation, and layout calculations
    /// are handled by <see cref="DateTimePicker"/>.
    /// </remarks>
    internal class DateTimePickerRenderer : Renderer<DateTimePicker>
    {
        private const float CheckStrokeWidth = 2f;
        private const float ArrowInset = 6f;
        private const float DividerPadding = 3f;
        private const float TextHorizontalPadding = 2f;

        /// <summary>
        /// Renders the specified <see cref="DateTimePicker"/>.
        /// </summary>
        /// <param name="control">The control to render.</param>
        /// <param name="e">The paint event arguments.</param>
        protected override void Render (DateTimePicker control, PaintEventArgs e)
        {
            var canvas = e.Canvas;

            DrawCheckBox (control, canvas);
            DrawButton (control, canvas);
            DrawText (control, canvas);
            DrawFocus (control, canvas);
        }

        /// <summary>
        /// Draws the left-side checkbox if enabled.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <param name="canvas">The target canvas.</param>
        private static void DrawCheckBox (DateTimePicker control, SKCanvas canvas)
        {
            if (!control.ShowCheckBox)
                return;

            var rect = ToSKRect (control.CheckBoxRectangle);

            using var borderPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = GetBorderColor (control)
            };

            using var fillPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = GetBackgroundColor (control)
            };

            canvas.DrawRect (rect, fillPaint);
            canvas.DrawRect (rect, borderPaint);

            if (!control.Checked)
                return;

            using var checkPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = CheckStrokeWidth,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                Color = GetForegroundColor (control)
            };

            float left = rect.Left + 3;
            float midX = rect.MidX - 1;
            float right = rect.Right - 3;
            float top = rect.Top + 4;
            float midY = rect.MidY + 1;
            float bottom = rect.Bottom - 4;

            using var path = new SKPath ();
            path.MoveTo (left, midY);
            path.LineTo (midX, bottom);
            path.LineTo (right, top);

            canvas.DrawPath (path, checkPaint);
        }

        /// <summary>
        /// Draws the right-side button area.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <param name="canvas">The target canvas.</param>
        private static void DrawButton (DateTimePicker control, SKCanvas canvas)
        {
            var rect = ToSKRect (control.ButtonRectangle);

            using var fillPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = GetButtonBackgroundColor (control)
            };

            using var borderPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = GetBorderColor (control)
            };

            canvas.DrawRect (rect, fillPaint);
            canvas.DrawRect (rect, borderPaint);

            if (control.ShowUpDown)
                DrawUpDownGlyph (control, canvas, rect);
            else
                DrawDropDownGlyph (control, canvas, rect);
        }

        /// <summary>
        /// Draws the up/down glyph when spinner mode is enabled.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <param name="canvas">The target canvas.</param>
        /// <param name="rect">The button rectangle.</param>
        private static void DrawUpDownGlyph (DateTimePicker control, SKCanvas canvas, SKRect rect)
        {
            float midY = rect.MidY;

            using var dividerPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = GetBorderColor (control)
            };

            canvas.DrawLine (rect.Left + DividerPadding, midY, rect.Right - DividerPadding, midY, dividerPaint);

            using var glyphPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = GetForegroundColor (control)
            };

            using (var up = new SKPath ()) {
                up.MoveTo (rect.MidX, midY - 5);
                up.LineTo (rect.Left + ArrowInset, midY - 1);
                up.LineTo (rect.Right - ArrowInset, midY - 1);
                up.Close ();
                canvas.DrawPath (up, glyphPaint);
            }

            using (var down = new SKPath ()) {
                down.MoveTo (rect.Left + ArrowInset, midY + 1);
                down.LineTo (rect.Right - ArrowInset, midY + 1);
                down.LineTo (rect.MidX, midY + 5);
                down.Close ();
                canvas.DrawPath (down, glyphPaint);
            }
        }

        /// <summary>
        /// Draws the drop-down arrow glyph.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <param name="canvas">The target canvas.</param>
        /// <param name="rect">The button rectangle.</param>
        private static void DrawDropDownGlyph (DateTimePicker control, SKCanvas canvas, SKRect rect)
        {
            using var glyphPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = GetForegroundColor (control)
            };

            using var path = new SKPath ();
            path.MoveTo (rect.MidX - 4, rect.MidY - 1);
            path.LineTo (rect.MidX + 4, rect.MidY - 1);
            path.LineTo (rect.MidX, rect.MidY + 4);
            path.Close ();

            canvas.DrawPath (path, glyphPaint);
        }

        /// <summary>
        /// Draws the formatted text of the control.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <param name="canvas">The target canvas.</param>
        private static void DrawText (DateTimePicker control, SKCanvas canvas)
        {
            if (string.IsNullOrEmpty (control.DisplayText))
                return;

            var rect = control.TextRectangle;

            using var paint = new SKPaint {
                IsAntialias = true,
                Color = GetForegroundColor (control),
                TextSize = GetTextSize (control),
                Typeface = GetTypeface (control)
            };

            var metrics = paint.FontMetrics;
            float baseline = rect.Top + ((rect.Height - (metrics.Descent - metrics.Ascent)) / 2f) - metrics.Ascent;

            canvas.DrawText (
                control.DisplayText,
                rect.Left + TextHorizontalPadding,
                baseline,
                paint);
        }

        /// <summary>
        /// Draws the focus cue around the text area when appropriate.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <param name="canvas">The target canvas.</param>
        private static void DrawFocus (DateTimePicker control, SKCanvas canvas)
        {
            if (!control.Focused || !control.ShowFocusCues)
                return;

            var rect = ToSKRect (control.TextRectangle);
            rect.Inflate (-1, -1);

            using var focusPaint = new SKPaint {
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = Theme.AccentColor,
                PathEffect = SKPathEffect.CreateDash (new float[] { 2, 2 }, 0)
            };

            canvas.DrawRect (rect, focusPaint);
        }

        /// <summary>
        /// Gets the control background color.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <returns>The resolved background color.</returns>
        private static SKColor GetBackgroundColor (DateTimePicker control)
        {
            return control.CurrentStyle.BackgroundColor ?? SKColors.White;
        }

        /// <summary>
        /// Gets the border color for the control.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <returns>The resolved border color.</returns>
        private static SKColor GetBorderColor (DateTimePicker control)
        {
            return control.CurrentStyle.Border.Color ?? SKColors.Gray;
        }

        /// <summary>
        /// Gets the foreground color for text and glyphs.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <returns>The resolved foreground color.</returns>
        private static SKColor GetForegroundColor (DateTimePicker control)
        {
            var color = control.CurrentStyle.ForegroundColor ?? SKColors.Black;

            if (!control.Enabled)
                color = color.WithAlpha ((byte)(color.Alpha * 0.55f));

            return color;
        }

        /// <summary>
        /// Gets the background color for the right-side button based on its current state.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <returns>The resolved button background color.</returns>
        private static SKColor GetButtonBackgroundColor (DateTimePicker control)
        {
            var baseColor = GetBackgroundColor (control);

            if (!control.Enabled)
                return baseColor.WithAlpha ((byte)(baseColor.Alpha * 0.75f));

            if (control.IsDropDownOpen || control.IsButtonPressed)
                return Blend (baseColor, SKColors.Black, 0.10f);

            if (control.IsButtonHovered)
                return Blend (baseColor, SKColors.White, 0.08f);

            return baseColor;
        }

        /// <summary>
        /// Gets the text size to use when drawing the control.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <returns>The resolved text size.</returns>
        private static float GetTextSize (DateTimePicker control)
        {
            if (control.CurrentStyle.FontSize.HasValue && control.CurrentStyle.FontSize.Value > 0)
                return control.CurrentStyle.FontSize.Value;

            return Theme.FontSize;
        }

        /// <summary>
        /// Gets the typeface used for text rendering.
        /// </summary>
        /// <param name="control">The owning control.</param>
        /// <returns>The resolved typeface.</returns>
        private static SKTypeface? GetTypeface (DateTimePicker control)
        {
            return control.CurrentStyle.Font ?? Theme.UIFont;
        }

        /// <summary>
        /// Blends two colors together.
        /// </summary>
        /// <param name="from">The base color.</param>
        /// <param name="to">The overlay color.</param>
        /// <param name="amount">Blend amount in range 0..1.</param>
        /// <returns>The blended color.</returns>
        private static SKColor Blend (SKColor from, SKColor to, float amount)
        {
            amount = amount < 0f ? 0f : (amount > 1f ? 1f : amount);

            byte r = (byte)(from.Red + ((to.Red - from.Red) * amount));
            byte g = (byte)(from.Green + ((to.Green - from.Green) * amount));
            byte b = (byte)(from.Blue + ((to.Blue - from.Blue) * amount));
            byte a = (byte)(from.Alpha + ((to.Alpha - from.Alpha) * amount));

            return new SKColor (r, g, b, a);
        }

        /// <summary>
        /// Converts a <see cref="System.Drawing.Rectangle"/> to <see cref="SKRect"/>.
        /// </summary>
        /// <param name="rect">The source rectangle.</param>
        /// <returns>The converted rectangle.</returns>
        private static SKRect ToSKRect (System.Drawing.Rectangle rect)
            => new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}
