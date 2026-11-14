using System.Drawing;
using System.Reflection;
using ModernFormsNext.Accessibility;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TitleBarIconTests
{
    public static IEnumerable<object[]> ImageAndDpiSizes()
    {
        foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2 })
            foreach (var size in new[] { new Size(16, 16), new Size(64, 64), new Size(64, 32), new Size(32, 64) })
                yield return [scale, size.Width, size.Height];
    }

    [Theory]
    [MemberData(nameof(ImageAndDpiSizes))]
    public void FormIconUsesSixteenLogicalPixelsAndPreservesAspectAndMargins(double scale, int width, int height)
    {
        using var host = ModernFormsTestHost.Create();
        using var image = Solid(width, height, SKColors.Magenta);
        using var form = new Form { Image = image };
        var window = host.Show(form, new TestViewport(400, 150, scale));
        using var pixels = window.CaptureRenderedSnapshot();
        var actual = ColoredBounds(pixels, SKColors.Magenta);
        var expectedSize = width >= height ? new Size((int)(16 * scale), (int)(16 * scale * height / width))
            : new Size((int)(16 * scale * width / height), (int)(16 * scale));
        Assert.Equal(expectedSize, actual.Size);
        AssertCentered(form, actual);
        Assert.Same(image, form.TitleBar.Image);
    }

    [Fact]
    public void ImplicitIconPreservesImageAccessibilityAndStaysOutsideKeyboardFocus()
    {
        using var host = ModernFormsTestHost.Create();
        using var image = Solid(16, 16, SKColors.Magenta);
        using var form = new Form { Image = image };
        var window = host.Show(form, 400, 150);
        var slot = ImageControl(form.TitleBar);
        Assert.Equal(AccessibleRole.Graphic, slot.AccessibilityObject.Role);
        Assert.Equal(AccessibleControlType.Image, slot.AccessibilityObject.ControlType);
        Assert.Equal(AccessibilityView.Content, slot.AccessibilityObject.View);
        Assert.False(slot.CanSelect);
        // TestInput intentionally admits the public hosted tree, not implicit Form chrome.
        // Select is the same canonical focus operation and ignores this ineligible child.
        slot.Select();
        Assert.False(slot.Focused);
        Assert.NotSame(slot, window.FocusedControl);
        form.TitleBar.ShowImage = false;
        Assert.Equal(AccessibilityView.Hidden, slot.AccessibilityObject.View);
    }

    [Fact]
    public void RuntimeDpiChangesRepaintBothDirectionsWithoutChangingLogicalSlot()
    {
        using var host = ModernFormsTestHost.Create();
        using var image = Solid(16, 16, SKColors.Magenta);
        using var form = new Form { Image = image };
        var window = host.Show(form, 400, 150);
        foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2, 1.5, 1 }) {
            window.SetRenderScale(scale);
            using var pixels = window.CaptureRenderedSnapshot();
            var bounds = ColoredBounds(pixels, SKColors.Magenta);
            Assert.Equal(new Size((int)(16 * scale), (int)(16 * scale)), bounds.Size);
            Assert.Equal(34, ImageControl(form.TitleBar).Width);
            AssertCentered(form, bounds);
        }
    }

    [Fact]
    public void NullVisibilityReplacementAndDisposalKeepBorrowedBitmapOwnership()
    {
        using var host = ModernFormsTestHost.Create();
        using var first = Solid(64, 64, SKColors.Magenta);
        using var second = Solid(32, 64, SKColors.Lime);
        using var form = new Form();
        var window = host.Show(form, new TestViewport(400, 150, 1.5));
        Assert.False(ImageControl(form.TitleBar).Visible);
        form.Image = first;
        using (var pixels = window.CaptureRenderedSnapshot())
            Assert.Equal(new Size(24, 24), ColoredBounds(pixels, SKColors.Magenta).Size);
        form.TitleBar.ShowImage = false;
        using (var pixels = window.CaptureRenderedSnapshot())
            Assert.True(ColoredBounds(pixels, SKColors.Magenta).IsEmpty);
        form.Image = second;
        Assert.False(ImageControl(form.TitleBar).Visible);
        form.TitleBar.ShowImage = true;
        using (var pixels = window.CaptureRenderedSnapshot()) {
            Assert.True(ColoredBounds(pixels, SKColors.Magenta).IsEmpty);
            Assert.Equal(new Size(12, 24), ColoredBounds(pixels, SKColors.Lime).Size);
        }
        form.Image = null;
        using (var pixels = window.CaptureRenderedSnapshot())
            Assert.True(ColoredBounds(pixels, SKColors.Lime).IsEmpty);
        form.Image = first;
        window.Close();
        form.Dispose();
        Assert.Equal(SKColors.Magenta, first.GetPixel(0, 0));
        Assert.Equal(SKColors.Lime, second.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThemesAndDecorationModesKeepManagedIconSeparateFromSystemChrome(bool dark)
    {
        using var host = ModernFormsTestHost.Create();
        using var image = Solid(64, 64, SKColors.Magenta);
        Assert.True(ThemeManager.Current.Apply(dark ? BuiltInThemes.Dark : BuiltInThemes.Light,
            new ThemeApplyOptions { TextScale = 2 }).Success);
        // Decoration mode is a pre-show setting, not a supported runtime mutation.
        foreach (bool systemChrome in new[] { false, true, false }) {
            using var form = new Form { Image = image, UseSystemDecorations = systemChrome };
            var window = host.Show(form, new TestViewport(400, 150, 1.75));
            using var pixels = window.CaptureRenderedSnapshot();
            if (systemChrome) Assert.True(ColoredBounds(pixels, SKColors.Magenta).IsEmpty);
            else Assert.Equal(new Size(28, 28), ColoredBounds(pixels, SKColors.Magenta).Size);
            window.Close();
        }
    }

    [Fact]
    public void SmallAuthoredTitleBarContainsTheWholeImageWithoutStretchingItsAspect()
    {
        using var host = ModernFormsTestHost.Create();
        using var image = Solid(64, 32, SKColors.Magenta);
        using var form = new Form { Image = image };
        form.TitleBar.Height = 10;
        var window = host.Show(form, new TestViewport(400, 150, 1.5));
        using var pixels = window.CaptureRenderedSnapshot();
        Rectangle actual = ColoredBounds(pixels, SKColors.Magenta);
        Assert.Equal(new Size(15, 7), actual.Size);
        AssertCentered(form, actual);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(1.75d)]
    [InlineData(2d)]
    public void OrdinaryPictureBoxCenterImageKeepsItsExistingDevicePixelSize(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        using var image = Solid(16, 16, SKColors.Magenta);
        var picture = new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.CenterImage };
        var window = host.Show(picture, new TestViewport(100, 80, scale));
        using var pixels = window.CaptureRenderedSnapshot();
        Assert.Equal(new Size(16, 16), ColoredBounds(pixels, SKColors.Magenta).Size);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(1.75d)]
    [InlineData(2d)]
    public void HighResolutionSourceRetainsDistinctQuadrantsAtEachScale(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        using var image = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(image)) {
            canvas.Clear(SKColors.Red);
            using var paint = new SKPaint { Color = SKColors.Lime };
            canvas.DrawRect(32, 0, 32, 32, paint);
            paint.Color = SKColors.Blue; canvas.DrawRect(0, 32, 32, 32, paint);
            paint.Color = SKColors.White; canvas.DrawRect(32, 32, 32, 32, paint);
        }
        using var form = new Form { Image = image };
        var window = host.Show(form, new TestViewport(400, 150, scale));
        using var pixels = window.CaptureRenderedSnapshot();
        var slot = ImageControl(form.TitleBar);
        Point origin = slot.PointToScreen(Point.Empty), windowOrigin = form.PointToScreen(Point.Empty);
        int edge = (int)(16 * scale);
        int left = origin.X - windowOrigin.X + (slot.ScaledWidth - edge) / 2;
        int top = origin.Y - windowOrigin.Y + (slot.ScaledHeight - edge) / 2;
        Assert.Equal(SKColors.Red, pixels.GetPixel(left + edge / 4, top + edge / 4));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(left + 3 * edge / 4, top + edge / 4));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(left + edge / 4, top + 3 * edge / 4));
        Assert.Equal(SKColors.White, pixels.GetPixel(left + 3 * edge / 4, top + 3 * edge / 4));
        if (Environment.GetEnvironmentVariable("MFN_TITLEBAR_ICON_OUTPUT") is { Length: > 0 } directory) {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(System.IO.Path.Combine(directory, $"icon-{(int)(scale * 100)}.png"), pixels.EncodePng());
        }
    }

    private static SKBitmap Solid(int width, int height, SKColor color)
    {
        var image = new SKBitmap(width, height);
        image.Erase(color);
        return image;
    }

    // Inspect only the existing implicit slot; consumers use Form.Image/TitleBar.Image.
    private static Control ImageControl(FormTitleBar titlebar) =>
        (Control)typeof(FormTitleBar).GetField("form_image", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(titlebar)!;

    private static void AssertCentered(Form form, Rectangle bounds)
    {
        var slot = ImageControl(form.TitleBar);
        Point origin = slot.PointToScreen(Point.Empty), windowOrigin = form.PointToScreen(Point.Empty);
        var client = new Rectangle(origin.X - windowOrigin.X, origin.Y - windowOrigin.Y, slot.ScaledWidth, slot.ScaledHeight);
        Assert.True(client.Contains(bounds));
        Assert.InRange(Math.Abs((bounds.Left - client.Left) - (client.Right - bounds.Right)), 0, 1);
        Assert.InRange(Math.Abs((bounds.Top - client.Top) - (client.Bottom - bounds.Bottom)), 0, 1);
    }

    private static Rectangle ColoredBounds(RenderedSnapshot pixels, SKColor color)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < pixels.PixelHeight; y++)
            for (int x = 0; x < pixels.PixelWidth; x++)
                if (pixels.GetPixel(x, y) == color) {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
