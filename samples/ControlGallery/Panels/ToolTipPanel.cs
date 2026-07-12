using System;
using System.Drawing;
using ModernFormsNext;
using SkiaSharp;
using MfContentAlignment = ModernFormsNext.ContentAlignment;

namespace ControlGallery.Panels
{
    public class ToolTipPanel : BasePanel
    {
        private readonly ToolTip hoverToolTip;
        private readonly ToolTip ampersandToolTip;
        private readonly ToolTip manualToolTip;
        private readonly ToolTip ownerDrawToolTip;

        public ToolTipPanel()
        {
            hoverToolTip = new ToolTip
            {
                ToolTipTitle = "ModernFormsNext",
                ToolTipIcon = ToolTipIcon.Info,
                InitialDelay = 400,
                AutoPopDelay = 6000
            };

            ampersandToolTip = new ToolTip
            {
                InitialDelay = 400,
                AutoPopDelay = 6000,
                StripAmpersands = true
            };

            manualToolTip = new ToolTip
            {
                InitialDelay = 400,
                AutoPopDelay = 6000,
                BackColor = new SKColor(32, 36, 42),
                ForeColor = SKColors.White,
                BorderColor = Theme.AccentColor2,
                BorderRadius = 6,
                Padding = new Padding(12, 8, 12, 8),
                MaximumWidth = 260,
                MinimumTextLineHeight = 24
            };

            ownerDrawToolTip = new ToolTip
            {
                OwnerDraw = true,
                InitialDelay = 250,
                AutoPopDelay = 6000
            };

            ownerDrawToolTip.Popup += (_, e) => e.ToolTipSize = new Size(320, 72);
            ownerDrawToolTip.Draw += OwnerDrawToolTip_Draw;

            var automaticButton = Controls.Add(new Button
            {
                Text = "Hover",
                Left = 100,
                Top = 90,
                Width = 130,
                Height = 34
            });
            hoverToolTip.SetToolTip(automaticButton, "Automatic tooltip text is attached with SetToolTip(control, text).");

            var ampersandButton = Controls.Add(new Button
            {
                Text = "Ampersands",
                Left = 260,
                Top = 90,
                Width = 130,
                Height = 34
            });
            ampersandToolTip.SetToolTip(ampersandButton, "Save && Close removes &mnemonic markers.");

            var manualButton = Controls.Add(new Button
            {
                Text = "Show",
                Left = 420,
                Top = 90,
                Width = 130,
                Height = 34
            });
            manualToolTip.SetToolTip(manualButton, "Click to show a manually positioned tooltip.");
            manualButton.Click += (_, _) => manualToolTip.Show("Manual tooltip near the button.", manualButton, 0, manualButton.Height + 8, 2500);

            var ownerDrawButton = Controls.Add(new Button
            {
                Text = "Owner Draw",
                Left = 580,
                Top = 90,
                Width = 130,
                Height = 34
            });
            ownerDrawToolTip.SetToolTip(ownerDrawButton, "Owner-drawn tooltip rendered with SkiaSharp.");
        }

        public override void UnloadPanel()
        {
            hoverToolTip.Dispose();
            ampersandToolTip.Dispose();
            manualToolTip.Dispose();
            ownerDrawToolTip.Dispose();
        }

        private static void OwnerDrawToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            e.Canvas.FillRoundedRectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height, new SKColor(28, 31, 36), 8, 8);
            e.Canvas.DrawRoundedRectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height, Theme.AccentColor2, 8, 8);
            e.Canvas.DrawText(
                "Owner-drawn ToolTip",
                Theme.UIFontBold,
                e.FontSize,
                new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 8, e.Bounds.Width - 24, 22),
                SKColors.White,
                MfContentAlignment.MiddleLeft,
                maxLines: 1);
            e.Canvas.DrawText(
                "Rendered with SkiaSharp.",
                Theme.UIFont,
                Math.Max(1, e.FontSize - 1),
                new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 34, e.Bounds.Width - 24, 28),
                new SKColor(210, 222, 235),
                MfContentAlignment.MiddleLeft,
                maxLines: 1);
        }
    }
}
