using System;
using System.Drawing;
using SkiaSharp;
using static ModernFormsNext.TabStripItem;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a class that can render a TabStrip.
    /// </summary>
    public class TabStripRenderer : Renderer<TabStrip>
    {
        /// <inheritdoc/>
        protected override void Render (TabStrip control, PaintEventArgs e)
        {
            var background = control.TabStripBackgroundColor
        ?? control.Style.BackgroundColor
        ?? Theme.BackgroundColor;

            e.Canvas.FillRectangle (control.ClientRectangle, background);

            foreach (var item in control.Tabs)
                RenderItem (control, item, e);
        }


        /// <summary>
        /// Renders a TabStripItem.
        /// </summary>
        protected virtual void RenderItem (TabStrip control, TabStripItem item, PaintEventArgs e)
        {
            var radius = e.LogicalToDeviceUnits (control.TabCornerRadius);
            var bounds = item.Bounds;

            var backgroundColor = (item.Selected
    ? control.SelectedTabBackgroundColor ?? Theme.ControlMidColor
    : item.Hovered
        ? control.HoveredTabBackgroundColor ?? Theme.ControlLowColor
        : control.TabBackgroundColor ?? control.Style.BackgroundColor) ?? Theme.BackgroundColor;

            e.Canvas.FillRoundedRectangle (
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                backgroundColor,
                radius,
                radius,
                1f);

            var left = bounds.Left + e.LogicalToDeviceUnits (item.Padding.Left);
            var top = bounds.Top + e.LogicalToDeviceUnits (item.Padding.Top);
            var bottom = Math.Max(top, bounds.Bottom - e.LogicalToDeviceUnits (item.Padding.Bottom));
            var centerY = top + (bottom - top) / 2;

            if (item.Icon != null && item.DisplayMode != TabDisplayMode.TextOnly) {
                var iconSize = e.LogicalToDeviceUnits (16);
                item.IconBounds = new Rectangle (left, centerY - iconSize / 2, iconSize, iconSize);
                e.Canvas.DrawImage (item.Icon, item.IconBounds.ToSKRect());
                left += iconSize + e.LogicalToDeviceUnits (8);
            } else {
                item.IconBounds = Rectangle.Empty;
            }

            var right = bounds.Right - e.LogicalToDeviceUnits (item.Padding.Right);

            if (control.ShowCloseButtons && item.Closable && !item.Pinned) {
                var closeSize = e.LogicalToDeviceUnits (14);
                item.CloseButtonBounds = new Rectangle (
                    right - closeSize,
                    centerY - closeSize / 2,
                    closeSize,
                    closeSize);

                right -= closeSize + e.LogicalToDeviceUnits (8);

                DrawCloseGlyph (e, item.CloseButtonBounds, item.CloseButtonHovered);
            } else {
                item.CloseButtonBounds = Rectangle.Empty;
            }

            item.TextBounds = Rectangle.FromLTRB (left, top, Math.Max(left, right), bottom);

            if (item.DisplayMode != TabDisplayMode.IconOnly) {
                var style = control.HeaderStyle;
                var fontColor = !item.Enabled ? Theme.ForegroundDisabledColor : control.CurrentStyle.GetForegroundColor();
                // Resolve the same borrowed typeface, flags and fractional metrics used for
                // preferred size. Selected emphasis adds Bold without dropping other flags.
                e.Canvas.DrawTextCore(item.Text, style.GetFont(), e.LogicalToDeviceUnits(style.GetFontSize()),
                    item.TextBounds, fontColor, ContentAlignment.MiddleCenter, maxLines: 1, ellipsis: true,
                    fontStyle: control.GetHeaderFontStyle(item.Enabled && item.Selected),
                    lineHeight: style.GetLineHeight(), letterSpacing: style.GetLetterSpacing() * control.Scaling);
            }
        }

        private static void DrawCloseGlyph (PaintEventArgs e, Rectangle bounds, bool hovered)
        {
            var color = hovered ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;

            using var paint = new SKPaint {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.6f,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            var canvas = e.Canvas;

            canvas.DrawLine (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, paint);
            canvas.DrawLine (bounds.Right, bounds.Top, bounds.Left, bounds.Bottom, paint);
        }
    }
}
