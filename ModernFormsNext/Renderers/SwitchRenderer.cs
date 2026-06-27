using System;
using System.Drawing;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Renders the track, thumb, state transitions, and optional icons for a <see cref="Switch"/>.
    /// </summary>
    public class SwitchRenderer : Renderer<Switch>
    {
        private readonly struct FillSpec
        {
            public FillSpec(SKColor color, MfnBrush? brush)
            {
                Color = color;
                Brush = brush;
            }

            public SKColor Color { get; }

            public MfnBrush? Brush { get; }
        }

        private readonly struct TransitionSpec
        {
            public TransitionSpec(int fromValue, int toValue, float progress)
            {
                FromValue = fromValue;
                ToValue = toValue;
                Progress = Math.Clamp(progress, 0f, 1f);
            }

            public int FromValue { get; }

            public int ToValue { get; }

            public float Progress { get; }
        }

        /// <inheritdoc/>
        protected override void Render(Switch control, PaintEventArgs e)
        {
            var trackBounds = GetTrackBounds(control, e);
            var thumbBounds = GetThumbBounds(control, e);

            DrawTrack(control, e.Canvas, trackBounds, e);
            DrawTrackIcons(control, e.Canvas, trackBounds, e);
            DrawThumb(control, e.Canvas, thumbBounds, e);

            if (control.Selected && control.ShowFocusCues)
                e.Canvas.DrawFocusRectangle(control.ClientRectangle, 0);
        }

        /// <summary>
        /// Gets the bounds of the switch track.
        /// </summary>
        /// <param name="control">The switch to inspect.</param>
        /// <returns>The track bounds in control coordinates.</returns>
        public Rectangle GetTrackBounds(Switch control) => GetTrackBounds(control, null);

        /// <summary>
        /// Gets the bounds of the switch thumb.
        /// </summary>
        /// <param name="control">The switch to inspect.</param>
        /// <returns>The thumb bounds in control coordinates.</returns>
        public Rectangle GetThumbBounds(Switch control) => GetThumbBounds(control, null);

        /// <summary>
        /// Converts a pointer location to the nearest logical switch value.
        /// </summary>
        /// <param name="control">The switch that owns the coordinate system.</param>
        /// <param name="location">The pointer location in control coordinates.</param>
        /// <returns>The nearest switch value.</returns>
        public int PositionToValue(Switch control, Point location)
            => VisualPositionToValue(control, PositionToVisualPosition(control, location));

        /// <summary>
        /// Converts a pointer location to an animated visual position from 0 to 1.
        /// </summary>
        /// <param name="control">The switch that owns the coordinate system.</param>
        /// <param name="location">The pointer location in control coordinates.</param>
        /// <returns>The visual position in the range from 0 to 1.</returns>
        public float PositionToVisualPosition(Switch control, Point location)
        {
            var trackBounds = GetTrackBounds(control);
            var thumbSize = GetThumbSize(control, null);
            var inset = Scale(control, null, control.ThumbInset);
            var left = trackBounds.Left + inset + (thumbSize / 2f);
            var right = trackBounds.Right - inset - (thumbSize / 2f);
            var usable = Math.Max(1f, right - left);

            return Math.Clamp((location.X - left) / usable, 0f, 1f);
        }

        /// <summary>
        /// Converts an animated visual position to the nearest logical switch value.
        /// </summary>
        /// <param name="control">The switch that defines the available values.</param>
        /// <param name="position">The visual position in the range from 0 to 1.</param>
        /// <returns>The nearest switch value.</returns>
        public int VisualPositionToValue(Switch control, float position)
        {
            position = Math.Clamp(position, 0f, 1f);

            if (control.Mode == SwitchMode.ThreeState) {
                if (position < 1f / 3f)
                    return -1;

                if (position > 2f / 3f)
                    return 1;

                return 0;
            }

            return position >= 0.5f ? 1 : 0;
        }

        private static int Scale(Switch control, PaintEventArgs? e, int value)
            => e?.LogicalToDeviceUnits(value) ?? control.LogicalToDeviceUnits(value);

        private static SKColor WithAlpha(SKColor color, byte alpha)
            => new(color.Red, color.Green, color.Blue, alpha);

        private static SKColor Blend(SKColor from, SKColor to, float progress)
        {
            progress = Math.Clamp(progress, 0f, 1f);

            return new SKColor(
                (byte)Math.Round(from.Red + ((to.Red - from.Red) * progress)),
                (byte)Math.Round(from.Green + ((to.Green - from.Green) * progress)),
                (byte)Math.Round(from.Blue + ((to.Blue - from.Blue) * progress)),
                (byte)Math.Round(from.Alpha + ((to.Alpha - from.Alpha) * progress)));
        }

        private static Rectangle GetTrackBounds(Switch control, PaintEventArgs? e)
        {
            var client = control.ClientRectangle;
            var inset = Scale(control, e, 1);

            return new Rectangle(
                client.Left + inset,
                client.Top + inset,
                Math.Max(1, client.Width - (inset * 2)),
                Math.Max(1, client.Height - (inset * 2)));
        }

        private static int GetThumbSize(Switch control, PaintEventArgs? e)
        {
            if (control.ThumbSize > 0)
                return Math.Max(1, Scale(control, e, control.ThumbSize));

            var trackBounds = GetTrackBounds(control, e);
            var inset = Scale(control, e, control.ThumbInset);

            return Math.Max(2, trackBounds.Height - (inset * 2));
        }

        private static Rectangle GetThumbBounds(Switch control, PaintEventArgs? e)
        {
            var trackBounds = GetTrackBounds(control, e);
            var thumbSize = GetThumbSize(control, e);
            var inset = Scale(control, e, control.ThumbInset);
            var minX = trackBounds.Left + inset;
            var maxX = trackBounds.Right - inset - thumbSize;
            var x = minX + (int)Math.Round(Math.Max(0, maxX - minX) * control.VisualPosition);
            var y = trackBounds.Top + ((trackBounds.Height - thumbSize) / 2);

            return new Rectangle(x, y, thumbSize, thumbSize);
        }

        private static float GetTrackRadius(Switch control, PaintEventArgs e, SKRect bounds)
        {
            if (control.TrackCornerRadius >= 0)
                return Scale(control, e, control.TrackCornerRadius);

            return bounds.Height / 2f;
        }

        private static float GetThumbRadius(Switch control, PaintEventArgs e, SKRect bounds)
        {
            if (control.ThumbCornerRadius >= 0)
                return Scale(control, e, control.ThumbCornerRadius);

            return Math.Min(bounds.Width, bounds.Height) / 2f;
        }

        private static TransitionSpec GetTransition(Switch control)
        {
            if (control.TryGetVisualTransition(out var fromValue, out var toValue, out var progress))
                return new TransitionSpec(fromValue, toValue, progress);

            var position = control.VisualPosition;

            if (control.Mode == SwitchMode.ThreeState) {
                if (position <= 0.5f)
                    return new TransitionSpec(-1, 0, position * 2f);

                return new TransitionSpec(0, 1, (position - 0.5f) * 2f);
            }

            return new TransitionSpec(0, 1, position);
        }

        private static FillSpec GetTrackFill(Switch control, int value)
        {
            if (!control.Enabled)
                return new FillSpec(Theme.ControlMidColor, null);

            return value switch
            {
                -1 => new FillSpec(control.NegativeTrackColor ?? Theme.ControlMidHighColor, control.NegativeTrackBrush),
                1 => new FillSpec(control.OnTrackColor ?? Theme.AccentColor2, control.OnTrackBrush),
                _ => new FillSpec(control.OffTrackColor ?? Theme.ControlMidColor, control.OffTrackBrush)
            };
        }

        private static FillSpec GetThumbFill(Switch control, int value)
        {
            if (!control.Enabled)
                return new FillSpec(Theme.ControlMidHighColor, null);

            var fill = value switch
            {
                -1 => new FillSpec(control.NegativeThumbColor ?? control.ThumbColor ?? Theme.ControlVeryHighColor, control.NegativeThumbBrush ?? control.ThumbBrush),
                1 => new FillSpec(control.OnThumbColor ?? control.ThumbColor ?? Theme.ControlVeryHighColor, control.OnThumbBrush ?? control.ThumbBrush),
                _ => new FillSpec(control.OffThumbColor ?? control.ThumbColor ?? Theme.ControlVeryHighColor, control.OffThumbBrush ?? control.ThumbBrush)
            };

            if (fill.Brush is not null)
                return fill;

            if (control.ThumbPressed)
                return new FillSpec(Blend(fill.Color, Theme.AccentColor2, 0.18f), null);

            if (control.ThumbHovered)
                return new FillSpec(Blend(fill.Color, Theme.AccentColor, 0.10f), null);

            return fill;
        }

        private static void DrawTransitionFill(SKCanvas canvas, SKRect bounds, float radius, FillSpec from, FillSpec to, float progress)
        {
            if (from.Brush is null && to.Brush is null) {
                DrawRoundedFill(canvas, bounds, radius, Blend(from.Color, to.Color, progress), 1f);
            } else {
                DrawFillLayer(canvas, bounds, radius, from, 1f);
                DrawFillLayer(canvas, bounds, radius, to, progress);
            }
        }

        private static void DrawFillLayer(SKCanvas canvas, SKRect bounds, float radius, FillSpec fill, float opacity)
        {
            opacity = Math.Clamp(opacity, 0f, 1f);

            if (opacity <= 0f)
                return;

            if (fill.Brush is null) {
                DrawRoundedFill(canvas, bounds, radius, fill.Color, opacity);
                return;
            }

            using var path = new SKPath();
            path.AddRoundRect(bounds, radius, radius);

            canvas.Save();
            canvas.ClipPath(path, SKClipOperation.Intersect, true);

            if (opacity >= 0.999f) {
                SkiaExtensions.RenderBrushBackground(canvas, bounds, fill.Brush, fill.Color);
                canvas.Restore();
                return;
            }

            using var opacityPaint = new SKPaint {
                Color = new SKColor(255, 255, 255, (byte)Math.Round(255 * opacity)),
                IsAntialias = true
            };

            canvas.SaveLayer(bounds, opacityPaint);
            SkiaExtensions.RenderBrushBackground(canvas, bounds, fill.Brush, fill.Color);
            canvas.Restore();
            canvas.Restore();
        }

        private static void DrawRoundedFill(SKCanvas canvas, SKRect bounds, float radius, SKColor color, float opacity)
        {
            opacity = Math.Clamp(opacity, 0f, 1f);

            if (opacity <= 0f)
                return;

            var alpha = (byte)Math.Round(color.Alpha * opacity);

            using var paint = new SKPaint {
                Color = WithAlpha(color, alpha),
                IsAntialias = true,
                IsStroke = false
            };

            canvas.DrawRoundRect(bounds, radius, radius, paint);
        }

        private static void DrawRoundedBorder(SKCanvas canvas, SKRect bounds, float radius, SKColor color, float width)
        {
            if (width <= 0)
                return;

            using var paint = new SKPaint {
                Color = color,
                IsAntialias = true,
                IsStroke = true,
                StrokeWidth = width,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            var half = width / 2f;
            var borderBounds = new SKRect(
                bounds.Left + half,
                bounds.Top + half,
                bounds.Right - half,
                bounds.Bottom - half);
            var borderRadius = Math.Max(0f, radius - half);

            canvas.DrawRoundRect(borderBounds, borderRadius, borderRadius, paint);
        }

        private static void DrawTrack(Switch control, SKCanvas canvas, Rectangle trackBounds, PaintEventArgs e)
        {
            var rect = trackBounds.ToSKRect();
            var radius = GetTrackRadius(control, e, rect);
            var transition = GetTransition(control);
            var fromFill = GetTrackFill(control, transition.FromValue);
            var toFill = GetTrackFill(control, transition.ToValue);

            DrawTransitionFill(canvas, rect, radius, fromFill, toFill, transition.Progress);

            var borderWidth = Scale(control, e, control.TrackBorderWidth);
            var borderColor = control.Enabled
                ? control.TrackBorderColor ?? Theme.BorderLowColor
                : Theme.ForegroundDisabledColor;

            DrawRoundedBorder(canvas, rect, radius, borderColor, borderWidth);
        }

        private static void DrawThumb(Switch control, SKCanvas canvas, Rectangle thumbBounds, PaintEventArgs e)
        {
            var rect = thumbBounds.ToSKRect();
            var radius = GetThumbRadius(control, e, rect);
            var transition = GetTransition(control);
            var fromFill = GetThumbFill(control, transition.FromValue);
            var toFill = GetThumbFill(control, transition.ToValue);

            DrawTransitionFill(canvas, rect, radius, fromFill, toFill, transition.Progress);

            var borderWidth = Scale(control, e, control.ThumbBorderWidth);
            var borderColor = control.Enabled
                ? control.ThumbBorderColor ?? Theme.BorderLowColor
                : Theme.ForegroundDisabledColor;

            DrawRoundedBorder(canvas, rect, radius, borderColor, borderWidth);

            if (control.ThumbIconImage is not null || control.ThumbIcon != SwitchIconKind.None) {
                var iconRect = GetIconRect(control, e, rect);
                var color = control.Enabled
                    ? control.ThumbIconColor ?? Theme.ForegroundColor
                    : Theme.ForegroundDisabledColor;

                DrawIcon(canvas, iconRect, control.ThumbIcon, control.ThumbIconImage, color, !control.Enabled);
            }
        }

        private static void DrawTrackIcons(Switch control, SKCanvas canvas, Rectangle trackBounds, PaintEventArgs e)
        {
            if (control.Mode == SwitchMode.ThreeState)
                DrawPositionIcon(control, canvas, trackBounds, e, -1);

            DrawPositionIcon(control, canvas, trackBounds, e, 0);
            DrawPositionIcon(control, canvas, trackBounds, e, 1);
        }

        private static void DrawPositionIcon(Switch control, SKCanvas canvas, Rectangle trackBounds, PaintEventArgs e, int value)
        {
            var iconKind = value switch
            {
                -1 => control.NegativeIcon,
                1 => control.OnIcon,
                _ => control.OffIcon
            };

            var image = value switch
            {
                -1 => control.NegativeIconImage,
                1 => control.OnIconImage,
                _ => control.OffIconImage
            };

            if (iconKind == SwitchIconKind.None && image is null)
                return;

            var center = GetIconCenter(control, trackBounds, value);
            var slot = GetTrackIconSlot(control, trackBounds, center);
            var color = GetTrackIconColor(control, value);
            var iconRect = GetIconRect(control, e, slot);

            DrawIcon(canvas, iconRect, iconKind, image, color, !control.Enabled);
        }

        private static SKPoint GetIconCenter(Switch control, Rectangle trackBounds, int value)
        {
            var y = trackBounds.Top + (trackBounds.Height / 2f);

            if (control.Mode == SwitchMode.ThreeState) {
                var third = trackBounds.Width / 3f;

                return value switch
                {
                    -1 => new SKPoint(trackBounds.Left + (third / 2f), y),
                    1 => new SKPoint(trackBounds.Right - (third / 2f), y),
                    _ => new SKPoint(trackBounds.Left + third + (third / 2f), y)
                };
            }

            var halfHeight = trackBounds.Height / 2f;
            return value > 0
                ? new SKPoint(trackBounds.Right - halfHeight, y)
                : new SKPoint(trackBounds.Left + halfHeight, y);
        }

        private static SKRect GetTrackIconSlot(Switch control, Rectangle trackBounds, SKPoint center)
        {
            var slotWidth = control.Mode == SwitchMode.ThreeState
                ? trackBounds.Width / 3f
                : trackBounds.Height;

            var slotHeight = trackBounds.Height;
            return new SKRect(
                center.X - (slotWidth / 2f),
                center.Y - (slotHeight / 2f),
                center.X + (slotWidth / 2f),
                center.Y + (slotHeight / 2f));
        }

        private static SKRect GetIconRect(Switch control, PaintEventArgs e, SKRect slot)
        {
            var requested = control.IconSize > 0
                ? Scale(control, e, control.IconSize)
                : (int)Math.Round(Math.Min(slot.Width, slot.Height) * 0.48f);

            var size = Math.Max(1, Math.Min(requested, (int)Math.Floor(Math.Min(slot.Width, slot.Height) - 2)));
            var x = slot.Left + ((slot.Width - size) / 2f);
            var y = slot.Top + ((slot.Height - size) / 2f);

            return new SKRect(x, y, x + size, y + size);
        }

        private static SKColor GetTrackIconColor(Switch control, int value)
        {
            if (!control.Enabled)
                return Theme.ForegroundDisabledColor;

            return value switch
            {
                -1 => control.NegativeIconColor ?? WithAlpha(Theme.ForegroundColor, 190),
                1 => control.OnIconColor ?? WithAlpha(Theme.ForegroundColorOnAccent, 220),
                _ => control.OffIconColor ?? WithAlpha(Theme.ForegroundColor, 180)
            };
        }

        private static void DrawIcon(SKCanvas canvas, SKRect rect, SwitchIconKind icon, SKBitmap? image, SKColor color, bool disabled)
        {
            if (image is not null) {
                canvas.DrawBitmap(image, Rectangle.Round(new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height)), disabled);
                return;
            }

            if (icon == SwitchIconKind.None)
                return;

            DrawBuiltInIcon(canvas, rect, icon, color);
        }

        private static void DrawBuiltInIcon(SKCanvas canvas, SKRect rect, SwitchIconKind icon, SKColor color)
        {
            using var paint = new SKPaint {
                Color = color,
                IsAntialias = true,
                IsStroke = true,
                StrokeWidth = Math.Max(1.4f, rect.Width * 0.12f),
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            var cx = rect.MidX;
            var cy = rect.MidY;
            var size = Math.Min(rect.Width, rect.Height);

            switch (icon) {
                case SwitchIconKind.Check:
                    canvas.DrawLine(rect.Left + (size * 0.18f), cy, rect.Left + (size * 0.42f), rect.Bottom - (size * 0.22f), paint);
                    canvas.DrawLine(rect.Left + (size * 0.42f), rect.Bottom - (size * 0.22f), rect.Right - (size * 0.14f), rect.Top + (size * 0.22f), paint);
                    break;

                case SwitchIconKind.Cross:
                    canvas.DrawLine(rect.Left + (size * 0.22f), rect.Top + (size * 0.22f), rect.Right - (size * 0.22f), rect.Bottom - (size * 0.22f), paint);
                    canvas.DrawLine(rect.Right - (size * 0.22f), rect.Top + (size * 0.22f), rect.Left + (size * 0.22f), rect.Bottom - (size * 0.22f), paint);
                    break;

                case SwitchIconKind.Minus:
                    canvas.DrawLine(rect.Left + (size * 0.20f), cy, rect.Right - (size * 0.20f), cy, paint);
                    break;

                case SwitchIconKind.Dot:
                    paint.IsStroke = false;
                    canvas.DrawCircle(cx, cy, size * 0.24f, paint);
                    break;

                case SwitchIconKind.Sun:
                    DrawSun(canvas, rect, paint, color);
                    break;

                case SwitchIconKind.Moon:
                    DrawMoon(canvas, rect, color);
                    break;
            }
        }

        private static void DrawSun(SKCanvas canvas, SKRect rect, SKPaint paint, SKColor color)
        {
            var cx = rect.MidX;
            var cy = rect.MidY;
            var size = Math.Min(rect.Width, rect.Height);
            var inner = size * 0.19f;
            var rayStart = size * 0.34f;
            var rayEnd = size * 0.46f;

            paint.IsStroke = false;
            paint.Color = color;
            canvas.DrawCircle(cx, cy, inner, paint);

            paint.IsStroke = true;
            paint.StrokeWidth = Math.Max(1.1f, size * 0.09f);

            for (var i = 0; i < 8; i++) {
                var angle = (float)(Math.PI * 2 * i / 8);
                var sx = cx + (MathF.Cos(angle) * rayStart);
                var sy = cy + (MathF.Sin(angle) * rayStart);
                var ex = cx + (MathF.Cos(angle) * rayEnd);
                var ey = cy + (MathF.Sin(angle) * rayEnd);
                canvas.DrawLine(sx, sy, ex, ey, paint);
            }
        }

        private static void DrawMoon(SKCanvas canvas, SKRect rect, SKColor color)
        {
            var size = Math.Min(rect.Width, rect.Height);

            using var path = new SKPath {
                FillType = SKPathFillType.EvenOdd
            };

            path.AddCircle(rect.MidX - (size * 0.07f), rect.MidY, size * 0.36f);
            path.AddCircle(rect.MidX + (size * 0.13f), rect.MidY - (size * 0.04f), size * 0.34f);

            using var paint = new SKPaint {
                Color = color,
                IsAntialias = true,
                IsStroke = false
            };

            canvas.DrawPath(path, paint);
        }
    }
}
