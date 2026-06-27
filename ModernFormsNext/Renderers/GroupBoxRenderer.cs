using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a renderer that draws the frame and caption for a <see cref="GroupBox"/>.
    /// </summary>
    /// <remarks>
    /// The renderer draws only the group frame and caption. Child controls are rendered by the
    /// normal <see cref="Control"/> paint pipeline and are laid out using
    /// <see cref="GroupBox.DisplayRectangle"/>.
    /// </remarks>
    public class GroupBoxRenderer : Renderer<GroupBox>
    {
        private const int CaptionHorizontalOffset = 8;
        private const int CaptionGapPadding = 2;

        /// <inheritdoc/>
        protected override void Render(GroupBox control, PaintEventArgs e)
        {
            var bounds = control.ClientRectangle;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            int captionHeight = Math.Max(1, e.LogicalToDeviceUnits(control.CaptionHeight));
            float borderWidth = GetBorderWidth(control, e);
            int boxTop = Math.Min(bounds.Bottom - 1, bounds.Top + captionHeight / 2);
            var captionBounds = GetCaptionBounds(control, e, bounds, captionHeight);

            DrawFrame(control, e.Canvas, bounds, boxTop, borderWidth, e);

            if (!captionBounds.IsEmpty)
            {
                FillCaptionBackground(control, e, captionBounds);
                DrawCaptionText(control, e, captionBounds);
            }
        }

        /// <summary>
        /// Draws the group box background layers before child controls are rendered.
        /// </summary>
        /// <param name="control">The group box whose background should be rendered.</param>
        /// <param name="e">The paint event data.</param>
        /// <remarks>
        /// This method keeps the outer control surface transparent and paints only the framed
        /// content surface. The content background is clipped to the inside of the frame so solid
        /// fills and gradients cannot bleed outside rounded borders.
        /// </remarks>
        public virtual void RenderBackground(GroupBox control, PaintEventArgs e)
        {
            e.Canvas.Clear(SKColors.Transparent);

            var bounds = control.ClientRectangle;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            int captionHeight = Math.Max(1, e.LogicalToDeviceUnits(control.CaptionHeight));
            float borderWidth = GetBorderWidth(control, e);
            int boxTop = Math.Min(bounds.Bottom - 1, bounds.Top + captionHeight / 2);

            RenderShadow(control, e);
            DrawContentBackground(control, e, bounds, boxTop, borderWidth);
        }

        /// <summary>
        /// Draws the optional shadow behind the group box frame.
        /// </summary>
        /// <param name="control">The group box whose shadow should be rendered.</param>
        /// <param name="e">The paint event data.</param>
        /// <remarks>
        /// This method is called from <see cref="GroupBox.OnPaintBackground(PaintEventArgs)"/> so
        /// child controls are painted above the shadow.
        /// </remarks>
        public virtual void RenderShadow(GroupBox control, PaintEventArgs e)
        {
            if (!control.ShowShadow || control.ShadowColor.Alpha == 0)
                return;

            var bounds = control.ClientRectangle;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            int captionHeight = Math.Max(1, e.LogicalToDeviceUnits(control.CaptionHeight));
            float borderWidth = GetBorderWidth(control, e);
            int boxTop = Math.Min(bounds.Bottom - 1, bounds.Top + captionHeight / 2);
            var frame = GetFrameRect(bounds, boxTop, borderWidth);

            if (frame.Width <= 0 || frame.Height <= 0)
                return;

            var shadowFrame = frame;

            int offsetX = e.LogicalToDeviceUnits(control.ShadowOffset.X);
            int offsetY = e.LogicalToDeviceUnits(control.ShadowOffset.Y);
            shadowFrame.Offset(offsetX, offsetY);

            float blur = e.LogicalToDeviceUnits(control.ShadowBlur);
            using var paint = new SKPaint
            {
                Color = control.ShadowColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            SKMaskFilter? maskFilter = null;

            try
            {
                if (blur > 0)
                {
                    maskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur);
                    paint.MaskFilter = maskFilter;
                }

                using var clipPath = CreateFramePath(frame, GetFrameRadius(control, frame, e));
                // Draw the shadow as a blurred filled frame, but clip out the real frame so the
                // blur never appears as an accidental gradient inside the group content area. The
                // directional clip keeps a positive down/right shadow from blooming back over the
                // top and left edges.
                DrawDirectionalShadow(control, e, frame, shadowFrame, clipPath, offsetX, offsetY, paint);
            }
            finally
            {
                maskFilter?.Dispose();
            }
        }

        private static void DrawCaptionText(GroupBox control, PaintEventArgs e, Rectangle captionBounds)
        {
            e.Canvas.DrawTextCore(
                control.Text,
                control.CurrentStyle.GetFont(),
                e.LogicalToDeviceUnits(control.EffectiveCaptionFontSize),
                captionBounds,
                control.EffectiveCaptionForegroundColor,
                ContentAlignment.TopLeft,
                maxLines: 1,
                ellipsis: true,
                fontStyle: control.CurrentStyle.GetFontStyle(),
                brush: control.EffectiveCaptionForegroundBrush);
        }

        private static void DrawFrame(GroupBox control, SKCanvas canvas, Rectangle bounds, int boxTop, float borderWidth, PaintEventArgs e)
        {
            if (borderWidth <= 0 || bounds.Right <= bounds.Left || bounds.Bottom <= boxTop)
                return;

            using var paint = new SKPaint
            {
                Color = GetFrameColor(control),
                IsAntialias = true,
                StrokeWidth = borderWidth,
                Style = SKPaintStyle.Stroke
            };

            DrawFrameRect(control, canvas, GetFrameRect(bounds, boxTop, borderWidth), paint, e);
        }

        private static void DrawContentBackground(GroupBox control, PaintEventArgs e, Rectangle bounds, int boxTop, float borderWidth)
        {
            if (bounds.Right <= bounds.Left || bounds.Bottom <= boxTop)
                return;

            var frame = GetFrameRect(bounds, boxTop, borderWidth);
            float borderInset = borderWidth / 2f;

            if (borderInset > 0)
                frame.Inflate(-borderInset, -borderInset);

            if (frame.Width <= 0 || frame.Height <= 0)
                return;

            float radius = Math.Max(
                0,
                GetFrameRadius(control, GetFrameRect(bounds, boxTop, borderWidth), e) - borderInset);

            using var path = CreateFramePath(frame, radius);

            e.Canvas.Save();

            try
            {
                e.Canvas.ClipPath(path, SKClipOperation.Intersect, true);
                SkiaExtensions.RenderBrushBackground(
                    e.Canvas,
                    frame,
                    control.EffectiveContentBackgroundBrush,
                    control.EffectiveContentBackgroundColor);
            }
            finally
            {
                e.Canvas.Restore();
            }
        }

        private static SKPath CreateFramePath(SKRect frame, float radius)
        {
            var path = new SKPath();

            if (radius > 0)
                path.AddRoundRect(frame, radius, radius);
            else
                path.AddRect(frame);

            return path;
        }

        private static void DrawFrameRect(GroupBox control, SKCanvas canvas, SKRect frame, SKPaint paint, PaintEventArgs? e)
        {
            float radius = GetFrameRadius(control, frame, e);

            if (radius > 0)
                canvas.DrawRoundRect(frame, radius, radius, paint);
            else
                canvas.DrawRect(frame, paint);
        }

        private static void DrawDirectionalShadow(
            GroupBox control,
            PaintEventArgs e,
            SKRect frame,
            SKRect shadowFrame,
            SKPath frameClipPath,
            int offsetX,
            int offsetY,
            SKPaint paint)
        {
            bool drewDirectionalClip = false;

            if (offsetX != 0)
            {
                var sideClip = new SKRect(
                    offsetX > 0 ? frame.Right : 0,
                    offsetY > 0 ? frame.Top + offsetY : 0,
                    offsetX > 0 ? e.Info.Width : frame.Left,
                    offsetY < 0 ? frame.Bottom + offsetY : e.Info.Height);

                drewDirectionalClip |= DrawShadowInClip(control, e, shadowFrame, frameClipPath, sideClip, paint);
            }

            if (offsetY != 0)
            {
                var bottomClip = new SKRect(
                    offsetX > 0 ? frame.Left + offsetX : 0,
                    offsetY > 0 ? frame.Bottom : 0,
                    offsetX < 0 ? frame.Right + offsetX : e.Info.Width,
                    offsetY > 0 ? e.Info.Height : frame.Top);

                drewDirectionalClip |= DrawShadowInClip(control, e, shadowFrame, frameClipPath, bottomClip, paint);
            }

            if (!drewDirectionalClip)
            {
                var fullClip = new SKRect(0, 0, e.Info.Width, e.Info.Height);
                DrawShadowInClip(control, e, shadowFrame, frameClipPath, fullClip, paint);
            }
        }

        private static bool DrawShadowInClip(
            GroupBox control,
            PaintEventArgs e,
            SKRect shadowFrame,
            SKPath frameClipPath,
            SKRect clip,
            SKPaint paint)
        {
            if (clip.Width <= 0 || clip.Height <= 0)
                return false;

            e.Canvas.Save();

            try
            {
                e.Canvas.ClipRect(clip, SKClipOperation.Intersect, true);
                e.Canvas.ClipPath(frameClipPath, SKClipOperation.Difference, true);
                DrawFrameRect(control, e.Canvas, shadowFrame, paint, e);
                return true;
            }
            finally
            {
                e.Canvas.Restore();
            }
        }

        private static void FillCaptionBackground(GroupBox control, PaintEventArgs e, Rectangle captionBounds)
        {
            var gap = captionBounds;
            int borderWidth = Math.Max(0, e.LogicalToDeviceUnits(control.CaptionBorderWidth));
            int gapPadding = e.LogicalToDeviceUnits(CaptionGapPadding) + borderWidth;

            gap.Inflate(gapPadding, 0);

            var captionRect = new SKRect(gap.Left, gap.Top, gap.Right, gap.Bottom);
            float radius = Math.Min(
                e.LogicalToDeviceUnits(control.CaptionBorderRadius),
                Math.Min(captionRect.Width, captionRect.Height) / 2f);

            FillCaptionSurface(control, e, captionRect, radius);

            if (borderWidth <= 0)
                return;

            float halfBorder = borderWidth / 2f;
            captionRect.Inflate(-halfBorder, -halfBorder);

            if (captionRect.Width <= 0 || captionRect.Height <= 0)
                return;

            using var borderPaint = new SKPaint
            {
                Color = control.EffectiveCaptionBorderColor,
                IsAntialias = radius > 0,
                StrokeWidth = borderWidth,
                Style = SKPaintStyle.Stroke
            };

            float borderRadius = Math.Max(0, radius - halfBorder);

            if (borderRadius > 0)
                e.Canvas.DrawRoundRect(captionRect, borderRadius, borderRadius, borderPaint);
            else
                e.Canvas.DrawRect(captionRect, borderPaint);
        }

        private static void FillCaptionSurface(GroupBox control, PaintEventArgs e, SKRect captionRect, float radius)
        {
            if (control.EffectiveCaptionBackgroundBrush is null)
            {
                using var paint = new SKPaint
                {
                    Color = control.EffectiveCaptionBackgroundColor,
                    IsAntialias = radius > 0,
                    Style = SKPaintStyle.Fill
                };

                if (radius > 0)
                    e.Canvas.DrawRoundRect(captionRect, radius, radius, paint);
                else
                    e.Canvas.DrawRect(captionRect, paint);

                return;
            }

            using var path = CreateFramePath(captionRect, radius);

            e.Canvas.Save();

            try
            {
                e.Canvas.ClipPath(path, SKClipOperation.Intersect, true);
                SkiaExtensions.RenderBrushBackground(
                    e.Canvas,
                    captionRect,
                    control.EffectiveCaptionBackgroundBrush,
                    control.EffectiveCaptionBackgroundColor);
            }
            finally
            {
                e.Canvas.Restore();
            }
        }

        private static Rectangle GetCaptionBounds(GroupBox control, PaintEventArgs e, Rectangle bounds, int captionHeight)
        {
            if (!control.Text.HasValue())
                return Rectangle.Empty;

            int offset = e.LogicalToDeviceUnits(CaptionHorizontalOffset);
            int maxWidth = Math.Max(0, bounds.Width - (offset * 2));

            if (maxWidth == 0)
                return Rectangle.Empty;

            var measured = TextMeasurer.MeasureText(
                control.Text,
                control.CurrentStyle.GetFont(),
                e.LogicalToDeviceUnits(control.EffectiveCaptionFontSize),
                new Size(maxWidth, captionHeight),
                control.CurrentStyle.GetFontStyle());

            int width = Math.Min(maxWidth, Math.Max(1, (int)Math.Ceiling(measured.Width) + 1));

            return new Rectangle(bounds.Left + offset, bounds.Top, width, captionHeight);
        }

        private static float GetBorderWidth(GroupBox control, PaintEventArgs e)
            => Math.Max(0, e.LogicalToDeviceUnits(control.CurrentStyle.Border.GetWidth()));

        private static SKRect GetFrameRect(Rectangle bounds, int boxTop, float borderWidth)
        {
            float half = borderWidth / 2f;

            return new SKRect(
                bounds.Left + half,
                boxTop + half,
                bounds.Right - half,
                bounds.Bottom - half);
        }

        private static float GetFrameRadius(GroupBox control, SKRect frame, PaintEventArgs? e)
        {
            float radius = e is null
                ? control.CurrentStyle.Border.GetRadius()
                : e.LogicalToDeviceUnits(control.CurrentStyle.Border.GetRadius());

            return Math.Min(radius, Math.Min(frame.Width, frame.Height) / 2f);
        }

        private static SKColor GetFrameColor(GroupBox control)
            => control.Enabled ? control.CurrentStyle.Border.GetColor() : Theme.BorderLowColor;
    }
}
