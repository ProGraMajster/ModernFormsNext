using System.Drawing;
using System.Reflection;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TabTypographyTests
{
    public static IEnumerable<object[]> TextAndDpiScales()
    {
        foreach (double dpi in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d })
            foreach (double text in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d })
                yield return [dpi, text];
    }

    [Theory]
    [MemberData(nameof(TextAndDpiScales))]
    public void ImplicitHeadersMatchAnOrdinaryLabelAtIndependentTextAndDpiScales(double dpi, double textScale)
        => CompareCaption(dpi, new Font(Theme.UIFont.FamilyName, (float)(Theme.FontSize * textScale)), selected: false);

    [Theory]
    [InlineData(FontStyle.Regular)]
    [InlineData(FontStyle.Italic)]
    [InlineData(FontStyle.Underline)]
    [InlineData(FontStyle.Strikeout)]
    [InlineData(FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout)]
    public void SelectedCaptionAddsBoldAndPreservesTheOwnersOtherFontFlags(FontStyle flags)
        => CompareCaption(1.25, new Font(Theme.UIFont.FamilyName, 28, flags), selected: true);

    private static void CompareCaption(double dpi, Font font, bool selected)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var tabs = new TabControl { Bounds = new Rectangle(0, 0, 640, 120), Font = font };
        var page = tabs.AddTab("AgjpQ");
        var strip = ImplicitStrip(tabs);
        strip.SelectedIndex = selected ? 0 : -1;
        MakeWhite(strip);
        var reference = new Label {
            Bounds = new Rectangle(0, 140, 500, 80), Text = "AgjpQ", Font = new Font(font.FamilyName, font.SizeInPoints, font.Style | (selected ? FontStyle.Bold : FontStyle.Regular)),
            ForeColor = SKColors.Black, BackColor = SKColors.White
        };
        root.Controls.Add(tabs);
        root.Controls.Add(reference);
        var window = host.Show(root, new TestViewport(640, 240, dpi));

        using var pixels = window.CaptureRenderedSnapshot();
        Rectangle actual = Ink(pixels, strip.Tabs[0].TextBounds);
        Rectangle expected = Ink(pixels, new Rectangle(0, reference.LogicalToDeviceUnits(140),
            reference.LogicalToDeviceUnits(500), reference.LogicalToDeviceUnits(80)));

        Assert.False(expected.IsEmpty);
        Assert.False(actual.IsEmpty, $"Strip={strip.Bounds}, preferred={strip.GetPreferredSize(Size.Empty)}, text={strip.Tabs[0].TextBounds}, font={tabs.Font.SizeInPoints}");
        Assert.InRange(actual.Height, expected.Height - 1, expected.Height + 1);
        Assert.InRange(actual.Width, expected.Width - 1, expected.Width + 1);
        Assert.True(strip.Tabs[0].TextBounds.Height >= expected.Height + strip.LogicalToDeviceUnits(4));
    }

    [Fact]
    public void RuntimeFontChangesRelayoutTheImplicitStripAndRestoreItsDefaultHeight()
    {
        using var host = ModernFormsTestHost.Create();
        var tabs = new TabControl();
        var page = tabs.AddTab("Runtime caption");
        var strip = ImplicitStrip(tabs);
        tabs.TabMaxWidth = 0;
        var window = host.Show(tabs, 700, 240);
        using var before = window.CaptureRenderedSnapshot();
        int oldWidth = strip.Tabs[0].Bounds.Width;
        int oldHeight = strip.Height;
        Font original = tabs.Font;
        int layouts = 0;
        strip.Layout += (_, _) => layouts++;

        tabs.Font = new Font(original.FamilyName, original.SizeInPoints * 2);

        Assert.True(layouts > 0);
        Assert.True(strip.Height > oldHeight, $"Old={oldHeight}, new={strip.Height}, preferred={strip.GetPreferredSize(Size.Empty)}");
        using var enlarged = window.CaptureRenderedSnapshot();
        Assert.True(strip.Tabs[0].Bounds.Width > oldWidth * 1.5);
        tabs.Font = original;
        Assert.Equal(oldHeight, strip.Height);
        using var restored = window.CaptureRenderedSnapshot();
        Assert.Equal(oldWidth, strip.Tabs[0].Bounds.Width);
    }

    [Fact]
    public void StandaloneStripResolvesInheritedStyleAndPreservesAnAuthoredHeight()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var inherited = new ControlStyle(TabStrip.DefaultStyle);
        var strip = new StyledStrip(inherited) { Width = 650, Height = 75, TabMaxWidth = 0 };
        strip.Tabs.Add("Inherited caption");
        strip.SelectedIndex = -1;
        root.Controls.Add(strip);
        var window = host.Show(root, 700, 200);
        using var before = window.CaptureRenderedSnapshot();
        int original = strip.Tabs[0].Bounds.Width;
        inherited.FontSize = Theme.FontSize * 2;

        strip.PerformLayout();
        strip.Invalidate();

        using var after = window.CaptureRenderedSnapshot();
        Assert.Equal(75, strip.Height);
        Assert.True(strip.Tabs[0].Bounds.Width > original * 1.5);
    }

    [Fact]
    public void OwnerStyleInheritanceAndLocalOverridesControlTheImplicitHeaders()
    {
        using var host = ModernFormsTestHost.Create();
        var inherited = new ControlStyle(Control.DefaultStyle);
        var tabs = new StyledTabs(inherited) { TabMaxWidth = 0 };
        tabs.AddTab("Inherited owner");
        var strip = ImplicitStrip(tabs);
        var window = host.Show(tabs, 700, 200);
        using var first = window.CaptureRenderedSnapshot();
        int initialWidth = strip.Tabs[0].Bounds.Width;
        inherited.FontSize = Theme.FontSize * 2;
        tabs.PerformLayout();
        tabs.Invalidate();
        using var changed = window.CaptureRenderedSnapshot();
        Assert.True(strip.Tabs[0].Bounds.Width > initialWidth * 1.5);

        tabs.Font = new Font(Theme.UIFont.FamilyName, Theme.FontSize);
        using var local = window.CaptureRenderedSnapshot();
        Assert.Equal(initialWidth, strip.Tabs[0].Bounds.Width);
    }

    [Fact]
    public void StandaloneFontAndLiveMetricsResizeTheDefaultHeaderAndRespectMaximumSize()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var strip = new TabStrip { Width = 650, TabMaxWidth = 0 };
        strip.Tabs.Add("Metric caption");
        root.Controls.Add(strip);
        var window = host.Show(root, 700, 240);
        using var first = window.CaptureRenderedSnapshot();
        int width = strip.Tabs[0].Bounds.Width;
        strip.Font = new Font(Theme.UIFont.FamilyName, Theme.FontSize * 2, FontStyle.Italic);
        Assert.True(strip.Height > 31);
        int height = strip.Height;
        strip.LineHeight = 1.6;
        strip.LetterSpacing = 2.5;
        Assert.True(strip.Height > height);
        using var enlarged = window.CaptureRenderedSnapshot();
        Assert.True(strip.Tabs[0].Bounds.Width > width * 1.5);

        strip.MaximumSize = new Size(0, 40);
        strip.PerformLayout();
        Assert.Equal(40, strip.Height);
        strip.PerformLayout();
        Assert.Equal(40, strip.Height);
    }

    [Fact]
    public void RuntimeDpiChangeRemeasuresWithoutMultiplyingTheLogicalFont()
    {
        using var host = ModernFormsTestHost.Create();
        var tabs = new TabControl { Font = new Font(Theme.UIFont.FamilyName, 28), TabMaxWidth = 0 };
        tabs.AddTab("DPI caption");
        var strip = ImplicitStrip(tabs);
        var window = host.Show(tabs, 700, 240);
        using var first = window.CaptureRenderedSnapshot();
        int width = strip.Tabs[0].Bounds.Width;
        int height = strip.Height;

        window.SetRenderScale(2);
        using var scaled = window.CaptureRenderedSnapshot();
        Assert.Equal(28, tabs.Font.SizeInPoints);
        Assert.InRange(strip.Tabs[0].Bounds.Width, width * 2 - 2, width * 2 + 2);
        Assert.InRange(strip.Height, height - 1, height + 1);
        window.SetRenderScale(1);
        using var restored = window.CaptureRenderedSnapshot();
        Assert.Equal(width, strip.Tabs[0].Bounds.Width);
        Assert.Equal(height, strip.Height);
    }

    [Theory]
    [InlineData(DockStyle.Fill)]
    [InlineData(DockStyle.Left)]
    [InlineData(DockStyle.Right)]
    public void VerticallyStretchedDockingOwnsHeightWithoutACompetingNaturalSizeLoop(DockStyle dock)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        int layouts = 0;
        root.Layout += (_, _) => {
            if (++layouts > 20) throw new InvalidOperationException("Header natural height fights parent docking.");
        };
        var strip = new TabStrip { Dock = dock, Font = new Font(Theme.UIFont.FamilyName, 28) };
        strip.Tabs.Add("Docked caption");
        root.Controls.Add(strip);
        var window = host.Show(root, 700, 240);
        Assert.Equal(root.ClientSize.Height, strip.Height);
        strip.Font = new Font(Theme.UIFont.FamilyName, 32);
        Assert.Equal(root.ClientSize.Height, strip.Height);
        using var frame = window.CaptureRenderedSnapshot();
        Assert.InRange(layouts, 1, 20);
    }

    [Fact]
    public void VerticalAnchoringKeepsTheParentsResizeDelta()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var strip = new TabStrip { Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
        strip.Tabs.Add("Anchored caption");
        root.Controls.Add(strip);
        var window = host.Show(root, 700, 240);
        int original = strip.Height;
        window.Resize(700, 340);
        Assert.Equal(original + 100, strip.Height);
        strip.Font = new Font(Theme.UIFont.FamilyName, 28);
        Assert.Equal(original + 100, strip.Height);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(1.75d)]
    [InlineData(2d)]
    public void IconsCloseTargetsPinnedTabsAndKeyboardRemainAligned(double dpi)
    {
        using var host = ModernFormsTestHost.Create();
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(SKColors.Red);
        using var icon = SKImage.FromBitmap(bitmap);
        var tabs = new TabControl { Font = new Font(Theme.UIFont.FamilyName, 28), ShowCloseButtons = true, TabMaxWidth = 0 };
        var pinned = tabs.AddPinnedTab("Pinned");
        var closable = tabs.AddClosableTab("Closable");
        closable.Icon = icon;
        closable.DisplayMode = TabStripItem.TabDisplayMode.IconAndText;
        var last = tabs.AddTab("Last");
        var strip = ImplicitStrip(tabs);
        var window = host.Show(tabs, new TestViewport(1000, 220, dpi));
        using var frame = window.CaptureRenderedSnapshot();
        var item = strip.Tabs[1];
        Assert.Equal(Size.Empty, strip.Tabs[0].CloseButtonBounds.Size);
        Assert.Equal(new Size(strip.LogicalToDeviceUnits(16), strip.LogicalToDeviceUnits(16)), item.IconBounds.Size);
        Assert.True(item.Bounds.Contains(item.IconBounds));
        Assert.True(item.Bounds.Contains(item.CloseButtonBounds));
        Assert.True(item.TextBounds.Left >= item.IconBounds.Right);
        Assert.True(item.TextBounds.Right < item.CloseButtonBounds.Left);
        Assert.Equal(SKColors.Red, frame.GetPixel(item.IconBounds.Right - 1, item.IconBounds.Bottom - 1));

        Assert.True(window.Input.Focus(strip));
        tabs.SelectedTabPage = pinned;
        window.Input.KeyDown(Keys.Right);
        window.Input.KeyUp(Keys.Right);
        Assert.Same(closable, tabs.SelectedTabPage);
        using var selected = window.CaptureRenderedSnapshot();
        Point origin = strip.PointToScreen(Point.Empty);
        Point windowOrigin = tabs.PointToScreen(Point.Empty);
        var close = item.CloseButtonBounds;
        window.Input.Click(new Point((int)Math.Round((origin.X - windowOrigin.X + close.Left + close.Width / 2) / dpi),
            (int)Math.Round((origin.Y - windowOrigin.Y + close.Top + close.Height / 2) / dpi)));
        Assert.DoesNotContain(closable, tabs.TabPages);
        Assert.Contains(pinned, tabs.TabPages);
        Assert.Contains(last, tabs.TabPages);
    }

    // Inspect the implicit child's geometry only in the test; consumers configure TabControl.Font.
    private static TabStrip ImplicitStrip(TabControl tabs) =>
        (TabStrip)typeof(TabControl).GetField("tab_strip", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tabs)!;

    private sealed class StyledStrip(ControlStyle inherited) : TabStrip
    {
        public override ControlStyle Style { get; } = new(inherited);
    }

    private sealed class StyledTabs(ControlStyle inherited) : TabControl
    {
        public override ControlStyle Style { get; } = new(inherited);
    }

    private static void MakeWhite(TabStrip strip)
    {
        strip.ForeColor = SKColors.Black;
        strip.TabBackgroundColor = strip.SelectedTabBackgroundColor = strip.HoveredTabBackgroundColor = SKColors.White;
        strip.TabStripBackgroundColor = SKColors.White;
    }

    private static Rectangle Ink(RenderedSnapshot snapshot, Rectangle region)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        region.Intersect(new Rectangle(0, 0, snapshot.PixelWidth, snapshot.PixelHeight));
        for (int y = region.Top; y < region.Bottom; y++)
            for (int x = region.Left; x < region.Right; x++) {
                SKColor pixel = snapshot.GetPixel(x, y);
                if (pixel.Alpha > 200 && pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128) {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}


