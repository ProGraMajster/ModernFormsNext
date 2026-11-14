using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class NumericTypographyTests
{
    public static IEnumerable<object[]> Scales()
    {
        foreach (double dpi in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d })
            foreach (double text in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d })
                yield return [dpi, text];
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ConstructedFontMatchesVisibleReferenceAndKeepsSpinnerGeometry(double dpi, double textScale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var font = new Font(Theme.UIFont.FamilyName, (float)(Theme.FontSize * textScale));
        var numeric = new NumericUpDown { Width = 400, Value = 50, Font = font };
        var label = new Label { Bounds = new Rectangle(0, 120, 400, 90), Text = "50", Font = font,
            ForeColor = SKColors.Black, BackColor = SKColors.White };
        root.Controls.Add(numeric);
        root.Controls.Add(label);
        var window = host.Show(root, new TestViewport(640, 240, dpi));
        var editor = Editor(numeric);
        editor.ForeColor = SKColors.Black;
        editor.BackColor = SKColors.White;
        editor.Style.Border.Color = SKColors.White;
        using var pixels = window.CaptureRenderedSnapshot();
        Assert.Equal(numeric.CurrentStyle.GetFontSize(), editor.CurrentStyle.GetFontSize());
        Point origin = editor.PointToScreen(Point.Empty), windowOrigin = root.PointToScreen(Point.Empty);
        Rectangle region = editor.PaddedClientRectangle;
        region.Offset(origin.X - windowOrigin.X, origin.Y - windowOrigin.Y);
        Rectangle actual = Ink(pixels, region);
        Rectangle expected = Ink(pixels, new Rectangle(0, label.LogicalToDeviceUnits(120),
            label.LogicalToDeviceUnits(400), label.LogicalToDeviceUnits(90)));
        Assert.False(expected.IsEmpty);
        Assert.InRange(actual.Width, expected.Width - 1, expected.Width + 1);
        Assert.InRange(actual.Height, expected.Height - 1, expected.Height + 1);
        Assert.True(region.Height >= expected.Height + numeric.LogicalToDeviceUnits(4));
        var peer = numeric.AccessibilityObject;
        var editBounds = peer.GetChild(0)!.Bounds;
        var up = peer.GetChild(1)!.Bounds;
        var down = peer.GetChild(2)!.Bounds;
        Assert.Equal(editBounds.Top, up.Top);
        Assert.Equal(editBounds.Bottom, down.Bottom);
        Assert.InRange(Math.Abs(editBounds.Right - up.Left), 0, 1);
        Assert.Equal(up.Bottom, down.Top);
        // Use actual pointer hit testing, not the semantic action, for both button halves.
        window.Input.Click(new Point((int)Math.Round((up.Left - windowOrigin.X + up.Width / 2d) / dpi),
            (int)Math.Round((up.Top - windowOrigin.Y + up.Height / 2d) / dpi)));
        Assert.Equal(51m, numeric.Value);
        window.Input.Click(new Point((int)Math.Round((down.Left - windowOrigin.X + down.Width / 2d) / dpi),
            (int)Math.Round((down.Top - windowOrigin.Y + down.Height / 2d) / dpi)));
        Assert.Equal(50m, numeric.Value);
    }

    [Fact]
    public void RuntimeTypographyPreservesActiveCompositionSelectionValueAndAccessibleRange()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var numeric = root.Controls.Add(new NumericUpDown { Width = 400, Maximum = 100000, Value = 1234 });
        var window = host.Show(root, 640, 240);
        var editor = Editor(numeric);
        window.Input.Focus(editor);
        var client = window.Input.TextInputClient!;
        Assert.True(client.SetSelection(1, 3));
        Assert.True(client.SetComposingText("56"));
        var before = client.GetState()!;
        decimal value = numeric.Value;
        int changes = 0;
        numeric.ValueChanged += (_, _) => changes++;
        var range = editor.AccessibilityObject.TextProvider!.RangeFromOffsets(0, 1);
        var oldBounds = Assert.Single(range.GetBoundingRectangles());
        int oldHeight = numeric.Height;
        Font original = numeric.Font;

        numeric.Font = new Font(original.FamilyName, original.SizeInPoints * 2, FontStyle.Bold | FontStyle.Italic | FontStyle.Underline);

        Assert.Same(client, window.Input.TextInputClient);
        var after = client.GetState()!;
        Assert.Equal(before.Text, after.Text);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.SelectionStart, after.SelectionStart);
        Assert.Equal(before.SelectionEnd, after.SelectionEnd);
        Assert.Equal(before.CompositionStart, after.CompositionStart);
        Assert.Equal(before.CompositionEnd, after.CompositionEnd);
        Assert.Equal(value, numeric.Value);
        Assert.Equal(0, changes);
        Assert.True(numeric.Height > oldHeight);
        Assert.True(Assert.Single(range.GetBoundingRectangles()).Height > oldBounds.Height);
        Assert.Equal(numeric.CurrentStyle.GetFontStyle(), editor.CurrentStyle.GetFontStyle());
        numeric.Font = original;
        Assert.Equal(oldHeight, numeric.Height);
        Assert.True(client.CancelComposition());
        Assert.Equal("1234", editor.Text);
    }

    [Fact]
    public void InheritedStyleLowLevelTypefaceAndMetricsFollowTheOwner()
    {
        using var host = ModernFormsTestHost.Create();
        var inherited = new ControlStyle(NumericUpDown.DefaultStyle);
        var numeric = new StyledNumeric(inherited) { Width = 400, Value = 25 };
        var root = new Panel();
        root.Controls.Add(numeric);
        host.Show(root, 640, 240);
        inherited.Font = Theme.UIFontBold;
        inherited.FontSize = 28;
        inherited.LineHeight = 1.4;
        inherited.LetterSpacing = 2.5;
        numeric.PerformLayout();
        numeric.Invalidate();
        var editor = Editor(numeric);
        Assert.Same(numeric.CurrentStyle.GetFont(), editor.CurrentStyle.GetFont());
        Assert.Equal(28, editor.CurrentStyle.GetFontSize());
        Assert.Equal(1.4, editor.CurrentStyle.GetLineHeight());
        Assert.Equal(2.5, editor.CurrentStyle.GetLetterSpacing());
        int height = numeric.Height;
        numeric.LineHeight = 2;
        Assert.True(numeric.Height > height);
        numeric.Font = new Font(Theme.UIFont.FamilyName, 18, FontStyle.Strikeout);
        Assert.Equal(FontStyle.Strikeout, editor.CurrentStyle.GetFontStyle());
        Assert.Equal(18, editor.CurrentStyle.GetFontSize());
    }

    [Fact]
    public void DecimalFormattingManualEditingReadOnlyAndAccessibleActionsSurviveFontChanges()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown { Width = 350, Minimum = -10000, Maximum = 10000,
            DecimalPlaces = 2, ThousandsSeparator = true, Value = 1234.5m, Font = new Font(Theme.UIFont.FamilyName, 28) };
        var root = new Panel();
        root.Controls.Add(numeric);
        var window = host.Show(root, 640, 240);
        var editor = Editor(numeric);
        Assert.Equal(1234.5m.ToString("N2", CultureInfo.CurrentCulture), editor.Text);
        window.Input.Focus(editor);
        editor.SelectAll();
        Assert.True(window.Input.CommitComposition(56.75m.ToString(CultureInfo.CurrentCulture)));
        window.Input.PressKey(Keys.Enter);
        Assert.Equal(56.75m, numeric.Value);
        numeric.ReadOnly = true;
        numeric.Font = new Font(Theme.UIFont.FamilyName, 35);
        Assert.Equal(35, editor.CurrentStyle.GetFontSize());
        Assert.True(editor.ReadOnly);
        Assert.True(editor.AccessibilityObject.State.HasFlag(AccessibleStates.ReadOnly));
        Assert.False(editor.AccessibilityObject.PerformAction(AccessibleActions.SetValue, "5"));
        Assert.True(numeric.AccessibilityObject.GetChild(1)!.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(56.76m, numeric.Value);
        numeric.AllowManualEdit = false;
        numeric.ReadOnly = false;
        Assert.True(editor.ReadOnly);
        Assert.True(numeric.AccessibilityObject.PerformAction(AccessibleActions.Decrement));
        Assert.Equal(56.75m, numeric.Value);
    }

    [Fact]
    public void AuthoredHeightConstraintsAndAutoSizeRemainInControl()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var numeric = root.Controls.Add(new NumericUpDown { Width = 300, Height = 75, Font = new Font(Theme.UIFont.FamilyName, 28) });
        host.Show(root, 640, 240);
        Assert.Equal(75, numeric.Height);
        numeric.AutoSize = true;
        numeric.PerformLayout();
        Assert.InRange(numeric.Height, 35, 74);
        numeric.MaximumSize = new Size(0, 32);
        numeric.PerformLayout();
        Assert.Equal(32, numeric.Height);
        numeric.PerformLayout();
        Assert.Equal(32, numeric.Height);
    }

    [Theory]
    [InlineData(DockStyle.Fill)]
    [InlineData(DockStyle.Left)]
    [InlineData(DockStyle.Right)]
    public void VerticalDockingOwnsHeight(DockStyle dock)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var numeric = root.Controls.Add(new NumericUpDown { Dock = dock, Font = new Font(Theme.UIFont.FamilyName, 28) });
        host.Show(root, 640, 240);
        Assert.Equal(root.ClientSize.Height, numeric.Height);
        numeric.Font = new Font(Theme.UIFont.FamilyName, 36);
        Assert.Equal(root.ClientSize.Height, numeric.Height);
        Assert.Equal(36, Editor(numeric).CurrentStyle.GetFontSize());
    }

    [Fact]
    public void RuntimeDpiDoesNotChangeLogicalFontOrDoubleScaleEditor()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var numeric = root.Controls.Add(new NumericUpDown { Width = 300, Value = 50, Font = new Font(Theme.UIFont.FamilyName, 28) });
        var window = host.Show(root, 640, 240);
        int height = numeric.Height;
        window.SetRenderScale(1.5);
        using var frame = window.CaptureRenderedSnapshot();
        Assert.Equal(28, Editor(numeric).CurrentStyle.GetFontSize());
        Assert.InRange(numeric.Height, height - 2, height + 2);
        window.SetRenderScale(1);
        using var restored = window.CaptureRenderedSnapshot();
        Assert.Equal(height, numeric.Height);
    }

    private sealed class StyledNumeric(ControlStyle inherited) : NumericUpDown
    {
        public override ControlStyle Style { get; } = new(inherited);
    }

    [Fact]
    public void FontObserverReentryPublishesTheNewerOwnerTypography()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown { Width = 400 };
        host.Show(numeric, 640, 240);
        var editor = Editor(numeric);
        bool replaced = false;
        editor.FontChanged += (_, _) => {
            if (!replaced) {
                replaced = true;
                numeric.Font = new Font(Theme.UIFont.FamilyName, 35, FontStyle.Bold | FontStyle.Strikeout);
            }
        };
        numeric.Font = new Font(Theme.UIFont.FamilyName, 28);
        Assert.True(replaced);
        Assert.Equal(35, editor.CurrentStyle.GetFontSize());
        Assert.Equal(numeric.CurrentStyle.GetFontStyle(), editor.CurrentStyle.GetFontStyle());
    }

    [Fact]
    public void FontObserverCanDisposeTheOwnerWithoutReusingItsEditor()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown();
        host.Show(numeric);
        var editor = Editor(numeric);
        editor.FontChanged += (_, _) => numeric.Dispose();
        numeric.Font = new Font(Theme.UIFont.FamilyName, 28);
        Assert.True(numeric.AccessibilityObject.State.HasFlag(AccessibleStates.Unavailable));
        Assert.True(editor.AccessibilityObject.State.HasFlag(AccessibleStates.Unavailable));
    }

    [Fact]
    public void VerticalAnchorRetainsResizeDeltaWhileEditorTypographyChanges()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var numeric = root.Controls.Add(new NumericUpDown { Width = 300,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left });
        var window = host.Show(root, 640, 240);
        int height = numeric.Height;
        window.Resize(640, 340);
        Assert.Equal(height + 100, numeric.Height);
        numeric.Font = new Font(Theme.UIFont.FamilyName, 28);
        Assert.Equal(height + 100, numeric.Height);
        Assert.Equal(28, Editor(numeric).CurrentStyle.GetFontSize());
    }

    [Fact]
    public void ThemeTextScaleAndExplicitStateTypographyRefreshBeforePainting()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var numeric = root.Controls.Add(new NumericUpDown { Width = 400, Value = 50 });
        var window = host.Show(root, 640, 240);
        var editor = Editor(numeric);
        int height = numeric.Height;
        Assert.True(ThemeManager.Current.Apply(BuiltInThemes.Light, new ThemeApplyOptions { TextScale = 2 }).Success);
        Assert.Equal(numeric.CurrentStyle.GetFontSize(), editor.CurrentStyle.GetFontSize());
        Assert.True(numeric.Height > height);
        numeric.StyleHover.FontSize = 40;
        window.Input.Move(new Point(numeric.Width - 8, 8));
        Assert.Equal(40, editor.CurrentStyle.GetFontSize());
        Assert.Equal(numeric.CurrentStyle.GetFontStyle(), editor.CurrentStyle.GetFontStyle());
        numeric.Enabled = false;
        Assert.Equal(numeric.CurrentStyle.GetFontSize(), editor.CurrentStyle.GetFontSize());
        Assert.False(editor.Enabled);
    }

    private static TextBox Editor(NumericUpDown numeric) =>
        (TextBox)((Control.ControlAccessibleObject)numeric.AccessibilityObject.GetChild(0)!).Owner!;

    private static Rectangle Ink(RenderedSnapshot pixels, Rectangle region)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        region.Intersect(new Rectangle(0, 0, pixels.PixelWidth, pixels.PixelHeight));
        for (int y = region.Top; y < region.Bottom; y++)
            for (int x = region.Left; x < region.Right; x++) {
                var pixel = pixels.GetPixel(x, y);
                if (pixel.Alpha > 200 && pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128) {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
