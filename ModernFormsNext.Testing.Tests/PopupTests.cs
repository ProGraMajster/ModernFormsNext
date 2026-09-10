using System.Drawing;
using System.Runtime.CompilerServices;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class PopupTests
{
    [Fact]
    public void ComboBoxOpensRealPopupAndPointerSelectionClosesIt()
    {
        using var host = ModernFormsTestHost.Create();
        var combo = new ComboBox();
        combo.Items.Add("Alpha");
        combo.Items.Add("Beta");
        TestWindowHost window = host.Show(combo, 180, 32);
        int changed = 0;
        combo.SelectedIndexChanged += (_, _) => changed++;

        window.Input.Click(combo);
        TestPopupHost popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        popup.LayoutUntilStable();

        Assert.True(combo.DroppedDown);
        Assert.Single(window.Popups);
        Assert.Equal(IntPtr.Zero, popup.Window.PlatformHandle.Handle);
        ListBox list = Assert.IsType<ListBox>(Assert.Single(popup.Window.Controls));
        Assert.Equal(2, list.Items.Count);
        Assert.Contains("ListBox", popup.CaptureTree().Dump());

        popup.Input.Click(new Point(12, 12));

        Assert.Equal(0, combo.SelectedIndex);
        Assert.Equal(1, changed);
        Assert.False(combo.DroppedDown);
        Assert.False(popup.IsVisible);
        Assert.Null(window.ActivePopup);
        Assert.False(popup.IsClosed);
    }

    [Fact]
    public void PopupHideCanReuseTheSameCanonicalWindowAndOutsideClickClosesIt()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var combo = root.Controls.Add(new ComboBox { Bounds = new Rectangle(0, 0, 150, 32) });
        combo.Items.Add("Alpha");
        var outside = root.Controls.Add(new Button { Bounds = new Rectangle(0, 80, 150, 32) });
        TestWindowHost window = host.Show(root, 300, 220);
        window.Input.Click(combo);
        TestPopupHost popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        PopupWindow canonical = popup.Window;
        popup.Hide();

        combo.DroppedDown = true;

        Assert.Same(popup, window.ActivePopup);
        Assert.Same(canonical, popup.Window);
        window.Input.Click(outside);
        Assert.False(combo.DroppedDown);
        Assert.Null(window.ActivePopup);
        Assert.Single(window.Popups);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void PopupUsesManagedPlacementAndTheProductionScaledFramebuffer(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true, StartPosition = FormStartPosition.Manual };
        TestWindowHost window = host.Show(form, new TestViewport(300, 240, scale));
        var canonical = new PopupWindow(form) { Size = new Size(60, 40) };
        var color = canonical.Controls.Add(new ColorControl { Dock = DockStyle.Fill });
        canonical.Show(form.PointToScreen(new Point(20, 30)));
        TestPopupHost popup = Assert.IsType<TestPopupHost>(window.ActivePopup);

        using RenderedSnapshot first = popup.CaptureRenderedSnapshot();

        Assert.Equal(new Point((int)(20 * scale), (int)(30 * scale)), canonical.Location);
        Assert.Equal((int)(60 * scale), first.PixelWidth);
        Assert.Equal((int)(40 * scale), first.PixelHeight);
        Assert.Equal(scale, first.RenderScale);
        Assert.Equal(SKColors.Crimson, first.GetPixel(first.PixelWidth / 2, first.PixelHeight / 2));
        Assert.Same(color, Assert.Single(canonical.Controls));

        color.Color = SKColors.Blue;
        canonical.Size = new Size(80, 50);
        canonical.Show(form.PointToScreen(new Point(30, 40)));
        using RenderedSnapshot second = popup.CaptureRenderedSnapshot();
        window.Close();

        Assert.True(popup.IsClosed);
        Assert.Equal(SKColors.Crimson, first.GetPixel(first.PixelWidth / 2, first.PixelHeight / 2));
        Assert.Equal(SKColors.Blue, second.GetPixel(second.PixelWidth / 2, second.PixelHeight / 2));
        Assert.Equal((int)(80 * scale), second.PixelWidth);
        Assert.Throws<ObjectDisposedException>(() => popup.Input.KeyDown(Keys.A));
        Assert.Throws<ObjectDisposedException>(() => popup.CaptureRenderedSnapshot());
    }

    [Fact]
    public void PopupFocusAndTextUseItsOwnCanonicalInputRoot()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var parentText = form.Controls.Add(new TextBox());
        TestWindowHost window = host.Show(form, 300, 240);
        Assert.True(window.Input.Focus(parentText));
        var canonical = new PopupWindow(form) { Size = new Size(180, 60) };
        var popupText = canonical.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        canonical.Show(form.PointToScreen(new Point(30, 30)));
        TestPopupHost popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        popup.LayoutUntilStable();

        Assert.True(popup.Input.Focus(popupText));
        popup.Input.TextInput("Popup text");

        Assert.Equal("Popup text", popupText.Text);
        Assert.Equal(string.Empty, parentText.Text);
        Assert.Same(popupText, popup.FocusedControl);
        Assert.Same(parentText, window.FocusedControl);
        Assert.Throws<ArgumentException>(() => window.Input.Click(popupText));
        Assert.Throws<ArgumentException>(() => popup.Input.Click(parentText));
    }

    [Fact]
    public void ParentCleanupClosesUndiscoveredPopupsAndContinuesAfterThrowingClosedCallback()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        TestWindowHost window = host.Show(form, 300, 240);
        var first = new PopupWindow(form) { Size = new Size(80, 60) };
        first.Show(0, 0);
        var second = new PopupWindow(form) { Size = new Size(80, 60) };
        second.Show(0, 0);
        int closed = 0;
        first.Closed += (_, _) => throw new InvalidOperationException("Popup closure failed.");
        second.Closed += (_, _) => closed++;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(window.Close);

        Assert.Equal("Popup closure failed.", failure.Message);
        Assert.Equal(1, closed);
        Assert.False(first.Visible);
        Assert.False(second.Visible);
        Assert.True(window.IsClosed);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public void PopupCaptureRejectsHiddenAndOversizedFramesAndRecoversAfterPaintFailure()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        TestWindowHost window = host.Show(form, 300, 240);
        var canonical = new PopupWindow(form) { Size = new Size(60, 40) };
        var color = canonical.Controls.Add(new ColorControl { Dock = DockStyle.Fill });
        canonical.Show(0, 0);
        TestPopupHost popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        Assert.Throws<InvalidOperationException>(() => popup.CaptureRenderedSnapshot(10));
        color.ThrowDuringPaint = true;
        Assert.Throws<InvalidOperationException>(() => popup.CaptureRenderedSnapshot());
        color.ThrowDuringPaint = false;
        color.Invalidate();
        using RenderedSnapshot image = popup.CaptureRenderedSnapshot();
        Assert.Equal(SKColors.Crimson, image.GetPixel(20, 20));
        popup.Hide();
        Assert.Throws<InvalidOperationException>(() => popup.CaptureRenderedSnapshot());
    }

    [Fact]
    public void ClosedPopupIsReleasedWhileParentRemainsAliveAndCanDeactivate()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        TestWindowHost window = host.Show(form, 300, 240);
        WeakReference closedPopup = CreateAndClosePopup(form);

        window.Backend.Deactivated?.Invoke();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(closedPopup.IsAlive);
        Assert.False(window.IsClosed);
        GC.KeepAlive(form);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndClosePopup(Form parent)
    {
        var popup = new PopupWindow(parent) { Size = new Size(80, 60) };
        popup.Show(0, 0);
        var reference = new WeakReference(popup);
        popup.Close();
        return reference;
    }

    private sealed class ColorControl : Control
    {
        private SKColor color = SKColors.Crimson;
        internal bool ThrowDuringPaint { get; set; }
        internal SKColor Color { get => color; set { color = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (ThrowDuringPaint) throw new InvalidOperationException("Popup paint failed.");
            e.Canvas.Clear(color);
        }
    }
}
