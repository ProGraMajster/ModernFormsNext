using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    internal sealed class ToolTipPopupControlRenderer : Renderer<ToolTipPopupControl>
    {
        protected override void Render(ToolTipPopupControl control, PaintEventArgs e)
        {
            if (control.OwnerToolTip is not { } owner)
                return;

            if (owner.OwnerDraw)
            {
                owner.RaiseDraw(CreateDrawArgs(control, e));
                return;
            }

            DrawDefault(control, e);
        }

        private static DrawToolTipEventArgs CreateDrawArgs(ToolTipPopupControl control, PaintEventArgs e)
            => new(
                e.Canvas,
                control.AssociatedWindow,
                control.AssociatedControl,
                control.ClientRectangle,
                control.TextToDisplay,
                control.CurrentStyle.GetBackgroundColor(),
                control.CurrentStyle.GetForegroundColor(),
                control.CurrentStyle.GetFont(),
                control.LogicalToDeviceUnits(control.CurrentStyle.GetFontSize()));

        private static void DrawDefault(ToolTipPopupControl control, PaintEventArgs e)
        {
            var owner = control.OwnerToolTip!;
            var bounds = control.PaddedClientRectangle;
            var iconBounds = Rectangle.Empty;
            var iconGap = control.Icon == ToolTipIcon.None ? 0 : control.LogicalToDeviceUnits(owner.IconSpacing);

            if (control.Icon != ToolTipIcon.None)
            {
                var iconSize = control.LogicalToDeviceUnits(owner.IconSize);
                iconBounds = new Rectangle(bounds.X, bounds.Y + 1, iconSize, iconSize);
                DrawIcon(e.Canvas, owner, control.Icon, iconBounds);
                bounds.X += iconSize + iconGap;
                bounds.Width -= iconSize + iconGap;
            }

            var hasTitle = !string.IsNullOrWhiteSpace(control.TitleToDisplay);
            var hasText = !string.IsNullOrWhiteSpace(control.TextToDisplay);

            if (hasTitle)
            {
                var titleHeight = MeasureTitleHeight(control, bounds.Width);
                var titleBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, titleHeight);

                e.Canvas.DrawText(
                    control.TitleToDisplay,
                    owner.EffectiveTitleTypeface,
                    control.LogicalToDeviceUnits(owner.EffectiveTitleFontSize),
                    titleBounds,
                    owner.EffectiveTitleForeColor,
                    owner.TitleAlign,
                    maxLines: 1,
                    ellipsis: true,
                    fontStyle: owner.EffectiveTitleFontStyle);

                var titleGap = hasText ? control.LogicalToDeviceUnits(owner.TitleSpacing) : 0;
                bounds.Y += titleHeight + titleGap;
                bounds.Height -= titleHeight + titleGap;
            }

            if (hasText && bounds.Height > 0)
            {
                e.Canvas.DrawText(
                    control.TextToDisplay,
                    owner.EffectiveTextTypeface,
                    control.LogicalToDeviceUnits(owner.EffectiveTextFontSize),
                    bounds,
                    control.CurrentStyle.GetForegroundColor(),
                    owner.TextAlign,
                    fontStyle: owner.EffectiveTextFontStyle);
            }
        }

        private static int MeasureTitleHeight(ToolTipPopupControl control, int width)
        {
            var owner = control.OwnerToolTip!;
            var size = TextMeasurer.MeasureText(
                control.TitleToDisplay,
                owner.EffectiveTitleTypeface,
                control.LogicalToDeviceUnits(owner.EffectiveTitleFontSize),
                new Size(Math.Max(1, width), int.MaxValue),
                owner.EffectiveTitleFontStyle);

            return Math.Max(control.LogicalToDeviceUnits(owner.MinimumTextLineHeight), (int)Math.Ceiling(size.Height));
        }

        private static void DrawIcon(SKCanvas canvas, ToolTip owner, ToolTipIcon icon, Rectangle bounds)
        {
            var rect = bounds.ToSKRect();
            using var fill = new SKPaint { IsAntialias = true };
            using var stroke = new SKPaint { Color = owner.IconForegroundColor, IsAntialias = true, IsStroke = true, StrokeWidth = 2 };
            using var textFont = new SKFont(Theme.UIFontBold, bounds.Height * 0.72f);
            using var textPaint = new SKPaint { Color = owner.IconForegroundColor, IsAntialias = true };

            switch (icon)
            {
                case ToolTipIcon.Info:
                    fill.Color = owner.ResolveIconBackColor(icon);
                    canvas.DrawOval(rect, fill);
                    canvas.DrawText("i", rect.MidX, rect.Bottom - (bounds.Height * 0.22f), SKTextAlign.Center, textFont, textPaint);
                    break;
                case ToolTipIcon.Warning:
                    fill.Color = owner.ResolveIconBackColor(icon);
                    using (var path = new SKPath())
                    {
                        path.MoveTo(rect.MidX, rect.Top);
                        path.LineTo(rect.Right, rect.Bottom);
                        path.LineTo(rect.Left, rect.Bottom);
                        path.Close();
                        canvas.DrawPath(path, fill);
                    }
                    canvas.DrawText("!", rect.MidX, rect.Bottom - (bounds.Height * 0.16f), SKTextAlign.Center, textFont, textPaint);
                    break;
                case ToolTipIcon.Error:
                    fill.Color = owner.ResolveIconBackColor(icon);
                    canvas.DrawOval(rect, fill);
                    canvas.DrawLine(rect.Left + 5, rect.Top + 5, rect.Right - 5, rect.Bottom - 5, stroke);
                    canvas.DrawLine(rect.Right - 5, rect.Top + 5, rect.Left + 5, rect.Bottom - 5, stroke);
                    break;
            }
        }
    }
}
