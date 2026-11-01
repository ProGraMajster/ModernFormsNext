using System.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ToolTipTextClippingTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(1.75d)]
    [InlineData(2d)]
    [InlineData(2.625d)]
    [InlineData(3.5d)]
    public void IncreasingOnlyHeightCannotRevealPreviouslyClippedTooltipInk(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form, new TestViewport(400, 300, scale));
        using var tip = new ToolTip
        {
            UseAnimation = false, UseFading = false, BorderWidth = 0, BorderRadius = 0,
            BackColor = SKColors.White, ForeColor = SKColors.Black,
            TextAlign = ContentAlignment.TopLeft, TitleAlign = ContentAlignment.TopLeft
        };
        foreach (string title in new[] { "", "Passive help" })
        foreach (string caption in new[] { "Explanation without activation", "Save the current document and close this view.", "Editor help with two short lines\nReady" })
        {
            tip.ToolTipTitle = title;
            tip.Show(caption, anchor);
            var popup = Assert.Single(window.Popups);
            var control = Assert.Single(popup.Window.Controls);
            using var original = popup.CaptureRenderedSnapshot();
            int originalBodyBottom = control.PaddedClientRectangle.Bottom;
            // Top alignment and identical width keep glyph positions/wrapping unchanged. An
            // expanded real production paint must not reveal ink hidden by the measured height.
            var expanded = new Size(popup.Window.Size.Width, popup.Window.Size.Height + 100);
            popup.Window.Size = control.Size = expanded;
            using var taller = popup.CaptureRenderedSnapshot();
            Assert.Equal(original.PixelWidth, taller.PixelWidth);
            int hiddenInk = 0;
            for (int y = originalBodyBottom; y < taller.PixelHeight; y++)
                for (int x = 0; x < taller.PixelWidth; x++)
                {
                    SKColor pixel = taller.GetPixel(x, y);
                    if (pixel.Alpha > 200 && pixel.Red < 96 && pixel.Green < 96 && pixel.Blue < 96) hiddenInk++;
                }
            Assert.True(hiddenInk == 0, $"Tooltip clipped {hiddenInk} ink pixels at scale {scale}; title={title}; caption={caption}");
            tip.Hide(anchor);
        }
    }
}
