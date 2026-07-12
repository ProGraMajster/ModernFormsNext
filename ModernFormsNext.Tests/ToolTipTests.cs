using System.Drawing;
using System.ComponentModel;
using System.Reflection;
using ModernFormsNext;
using SkiaSharp;
using Xunit;
using MfnContentAlignment = ModernFormsNext.ContentAlignment;
using MfnFont = ModernFormsNext.Font;
using MfnFontStyle = ModernFormsNext.FontStyle;

namespace ModernFormsNext.Tests;

public class ToolTipTests
{
    [Fact]
    public void DefaultDelaysMatchWinFormsStyleRatios()
    {
        using var toolTip = new ToolTip();

        Assert.Equal(500, toolTip.AutomaticDelay);
        Assert.Equal(500, toolTip.InitialDelay);
        Assert.Equal(100, toolTip.ReshowDelay);
        Assert.Equal(5000, toolTip.AutoPopDelay);
    }

    [Fact]
    public void AutomaticDelayUpdatesDerivedDelays()
    {
        using var toolTip = new ToolTip();

        toolTip.AutomaticDelay = 1000;

        Assert.Equal(1000, toolTip.AutomaticDelay);
        Assert.Equal(1000, toolTip.InitialDelay);
        Assert.Equal(200, toolTip.ReshowDelay);
        Assert.Equal(10000, toolTip.AutoPopDelay);
    }

    [Fact]
    public void SetToolTipStoresAndRemovesCaption()
    {
        using var toolTip = new ToolTip();
        using var control = new Control();

        toolTip.SetToolTip(control, "Save changes");

        Assert.Equal("Save changes", toolTip.GetToolTip(control));

        toolTip.SetToolTip(control, string.Empty);

        Assert.Equal(string.Empty, toolTip.GetToolTip(control));
    }

    [Fact]
    public void RemoveAllClearsAllCaptions()
    {
        using var toolTip = new ToolTip();
        using var first = new Control();
        using var second = new Control();

        toolTip.SetToolTip(first, "First");
        toolTip.SetToolTip(second, "Second");

        toolTip.RemoveAll();

        Assert.Equal(string.Empty, toolTip.GetToolTip(first));
        Assert.Equal(string.Empty, toolTip.GetToolTip(second));
    }

    [Fact]
    public void CanExtendOnlyAcceptsControls()
    {
        using var toolTip = new ToolTip();

        Assert.True(toolTip.CanExtend(new Control()));
        Assert.False(toolTip.CanExtend(new object()));
    }

    [Fact]
    public void PopupEventCanCancelAndResizeTooltip()
    {
        using var toolTip = new TestToolTip();
        var args = new PopupEventArgs(null, null, false, new Size(10, 20));

        toolTip.Popup += (_, e) =>
        {
            e.Cancel = true;
            e.ToolTipSize = new Size(50, 60);
        };

        toolTip.InvokePopup(args);

        Assert.True(args.Cancel);
        Assert.Equal(new Size(50, 60), args.ToolTipSize);
    }

    [Fact]
    public void DrawToolTipEventArgsHelperMethodsUseSkiaCanvas()
    {
        using var surface = SKSurface.Create(new SKImageInfo(120, 40));
        var args = new DrawToolTipEventArgs(
            surface.Canvas,
            null,
            null,
            new Rectangle(0, 0, 120, 40),
            "Tip",
            SKColors.Black,
            SKColors.White,
            Theme.UIFont,
            Theme.FontSize);

        args.DrawBackground();
        args.DrawText();
        args.DrawBorder();
    }

    [Fact]
    public void NegativeDelayValuesThrow()
    {
        using var toolTip = new ToolTip();

        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.AutomaticDelay = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.InitialDelay = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.ReshowDelay = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.AutoPopDelay = -1);
    }

    [Fact]
    public void InvalidToolTipIconThrows()
    {
        using var toolTip = new ToolTip();

        Assert.Throws<InvalidEnumArgumentException>(() => toolTip.ToolTipIcon = (ToolTipIcon)99);
    }

    [Fact]
    public void DefaultVisualOptionsLeaveEnoughRoomForTitlelessText()
    {
        using var toolTip = new ToolTip();

        var size = InvokeMeasureToolTip(toolTip, "Manual tooltip near the button.", string.Empty, ToolTipIcon.None);

        Assert.True(size.Width > toolTip.Padding.Horizontal);
        Assert.True(size.Height >= toolTip.MinimumTextLineHeight + toolTip.Padding.Vertical + (toolTip.BorderWidth * 2));
    }

    [Fact]
    public void VisualOptionsCanBeCustomized()
    {
        using var toolTip = new ToolTip
        {
            BackColor = SKColors.Black,
            ForeColor = SKColors.White,
            BorderColor = SKColors.Red,
            BorderRadius = 6,
            BalloonBorderRadius = 12,
            BorderWidth = 2,
            IconColor = SKColors.Green,
            IconForegroundColor = SKColors.Yellow,
            IconSize = 24,
            IconSpacing = 10,
            MaximumWidth = 220,
            MinimumSize = new Size(120, 48),
            MinimumTextLineHeight = 24,
            Padding = new Padding(12, 8, 12, 8),
            TextAlign = MfnContentAlignment.MiddleCenter,
            TextFont = new MfnFont("Segoe UI", 13, MfnFontStyle.Italic),
            TitleAlign = MfnContentAlignment.MiddleCenter,
            TitleForeColor = SKColors.Cyan,
            TitleFont = new MfnFont("Segoe UI", 14, MfnFontStyle.Bold),
            TitleSpacing = 6
        };

        Assert.Equal(SKColors.Black, toolTip.BackColor);
        Assert.Equal(SKColors.White, toolTip.ForeColor);
        Assert.Equal(SKColors.Red, toolTip.BorderColor);
        Assert.Equal(6, toolTip.BorderRadius);
        Assert.Equal(12, toolTip.BalloonBorderRadius);
        Assert.Equal(2, toolTip.BorderWidth);
        Assert.Equal(SKColors.Green, toolTip.IconColor);
        Assert.Equal(SKColors.Yellow, toolTip.IconForegroundColor);
        Assert.Equal(24, toolTip.IconSize);
        Assert.Equal(10, toolTip.IconSpacing);
        Assert.Equal(220, toolTip.MaximumWidth);
        Assert.Equal(new Size(120, 48), toolTip.MinimumSize);
        Assert.Equal(24, toolTip.MinimumTextLineHeight);
        Assert.Equal(new Padding(12, 8, 12, 8), toolTip.Padding);
        Assert.Equal(MfnContentAlignment.MiddleCenter, toolTip.TextAlign);
        Assert.Equal(MfnFontStyle.Italic, toolTip.TextFont!.Style);
        Assert.Equal(MfnContentAlignment.MiddleCenter, toolTip.TitleAlign);
        Assert.Equal(SKColors.Cyan, toolTip.TitleForeColor);
        Assert.Equal(MfnFontStyle.Bold, toolTip.TitleFont!.Style);
        Assert.Equal(6, toolTip.TitleSpacing);
    }

    [Fact]
    public void InvalidVisualOptionsThrow()
    {
        using var toolTip = new ToolTip();

        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.BorderRadius = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.BalloonBorderRadius = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.BorderWidth = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.IconSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.IconSpacing = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.MaximumWidth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.MinimumTextLineHeight = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.Padding = new Padding(-1, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => toolTip.TitleSpacing = -1);
    }

    [Fact]
    public void StripAmpersandsKeepsEscapedAmpersands()
    {
        using var toolTip = new ToolTip { StripAmpersands = true };

        var processed = InvokeProcessText(toolTip, "Save && Close removes &mnemonic markers.");

        Assert.Equal("Save & Close removes mnemonic markers.", processed);
    }

    private sealed class TestToolTip : ToolTip
    {
        public void InvokePopup(PopupEventArgs e)
        {
            OnPopup(e);
        }
    }

    private static Size InvokeMeasureToolTip(ToolTip toolTip, string text, string title, ToolTipIcon icon)
    {
        var method = typeof(ToolTip).GetMethod("MeasureToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (Size)method.Invoke(toolTip, [text, title, icon])!;
    }

    private static string InvokeProcessText(ToolTip toolTip, string text)
    {
        var method = typeof(ToolTip).GetMethod("ProcessText", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method.Invoke(toolTip, [text])!;
    }
}
