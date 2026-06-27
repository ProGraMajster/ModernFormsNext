using System.Drawing;
using ModernFormsNext;
using SkiaSharp;
using MfnDrawing = ModernFormsNext.Drawing;

namespace ControlGallery.Panels
{
    public class GroupBoxPanel : Panel
    {
        public GroupBoxPanel()
        {
            var options = Controls.Add(new GroupBox
            {
                Text = "Connection options",
                Left = 10,
                Top = 10,
                Width = 260,
                Height = 150
            });

            options.Controls.Add(new RadioButton
            {
                Text = "Use saved profile",
                Left = 14,
                Top = 30,
                Width = 190,
                Checked = true
            });

            options.Controls.Add(new RadioButton
            {
                Text = "Prompt every time",
                Left = 14,
                Top = 62,
                Width = 190
            });

            options.Controls.Add(new CheckBox
            {
                Text = "Remember this choice",
                Left = 14,
                Top = 98,
                Width = 190,
                Checked = true
            });

            var disabled = Controls.Add(new GroupBox
            {
                Text = "Disabled group",
                Left = 300,
                Top = 10,
                Width = 260,
                Height = 110,
                Enabled = false
            });

            disabled.Controls.Add(new CheckBox
            {
                Text = "Disabled option",
                Left = 14,
                Top = 32,
                Width = 180,
                Checked = true
            });

            var captionGradient = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 0)
            };

            captionGradient.GradientStops.Add(new MfnDrawing.GradientStop(Theme.AccentColor, 0f));
            captionGradient.GradientStops.Add(new MfnDrawing.GradientStop(Theme.AccentColor2, 1f));

            var styled = Controls.Add(new GroupBox
            {
                Text = "Styled group",
                CaptionFontSize = 18,
                CaptionForegroundColor = SKColors.White,
                CaptionBackgroundColor = Theme.AccentColor,
                CaptionBackgroundBrush = captionGradient,
                CaptionBorderColor = Theme.AccentColor2,
                CaptionBorderRadius = 3,
                CaptionBorderWidth = 1,
                ContentBackgroundColor = new SKColor(250, 252, 255),
                Left = 300,
                Top = 135,
                Width = 260,
                Height = 82
            });

            styled.Style.BackgroundColor = Theme.BackgroundColor;
            styled.Style.Border.Color = Theme.AccentColor2;
            styled.Style.Border.Radius = 8;
            styled.Style.Border.Width = 2;

            var contentGradient = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 1)
            };

            contentGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(255, 255, 255), 0f));
            contentGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(225, 242, 255), 0.55f));
            contentGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(191, 224, 246), 1f));

            var gradient = Controls.Add(new GroupBox
            {
                Text = "Gradient content",
                CaptionBackgroundColor = Theme.BackgroundColor,
                CaptionBorderColor = Theme.BorderLowColor,
                CaptionBorderRadius = 3,
                CaptionBorderWidth = 1,
                ContentBackgroundBrush = contentGradient,
                Left = 300,
                Top = 235,
                Width = 260,
                Height = 125,
                Padding = new Padding(10)
            });

            gradient.Style.BackgroundColor = Theme.BackgroundColor;
            gradient.Style.Border.Color = Theme.AccentColor2;
            gradient.Style.Border.Radius = 10;
            gradient.Style.Border.Width = 2;

            gradient.Controls.Add(new Label
            {
                Text = "Clipped fill",
                Left = 14,
                Top = 32,
                Width = 160,
                Height = 24
            });

            gradient.Controls.Add(new Button
            {
                Text = "Apply",
                Left = 14,
                Top = 66,
                Width = 90
            });

            var docked = Controls.Add(new GroupBox
            {
                Text = "Docked child layout",
                Left = 10,
                Top = 190,
                Width = 260,
                Height = 170,
                Padding = new Padding(8)
            });

            docked.Controls.Add(new Label
            {
                Text = "Top docked label",
                Dock = DockStyle.Top,
                Height = 28
            });

            var fill = docked.Controls.Add(new Panel
            {
                Dock = DockStyle.Fill
            });

            fill.Style.BackgroundColor = new SKColor(42, 138, 208, 45);
            fill.Style.Border.Width = 1;

            var autosized = Controls.Add(new GroupBox
            {
                Text = "AutoSize group",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Left = 300,
                Top = 380
            });

            autosized.Controls.Add(new Label
            {
                Text = "Content drives size",
                Left = 12,
                Top = 30,
                Width = 150,
                Height = 24
            });

            autosized.Controls.Add(new Button
            {
                Text = "Apply",
                Left = 12,
                Top = 62,
                Width = 90
            });
        }
    }
}
