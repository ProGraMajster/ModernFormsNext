using ModernFormsNext;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class ControlCompatibilityColorTests
{
    [Fact]
    public void BackColorSetterUpdatesStyleBackgroundColor()
    {
        var control = new Control();

        control.BackColor = SKColors.Red;

        Assert.Equal((SKColor?)SKColors.Red, control.Style.BackgroundColor);
    }

    [Fact]
    public void ForeColorSetterUpdatesStyleForegroundColor()
    {
        var control = new Control();

        control.ForeColor = SKColors.White;

        Assert.Equal((SKColor?)SKColors.White, control.Style.ForegroundColor);
    }

    [Fact]
    public void StyleBackgroundColorSetterIsVisibleThroughBackColor()
    {
        var control = new Control();

        control.Style.BackgroundColor = SKColors.Navy;

        Assert.Equal(SKColors.Navy, control.BackColor);
    }

    [Fact]
    public void StyleForegroundColorSetterIsVisibleThroughForeColor()
    {
        var control = new Control();

        control.Style.ForegroundColor = SKColors.Yellow;

        Assert.Equal(SKColors.Yellow, control.ForeColor);
    }

    [Fact]
    public void BackColorDoesNotModifyStyleHoverBackgroundColor()
    {
        var control = new Control();
        control.StyleHover.BackgroundColor = SKColors.Green;

        control.BackColor = SKColors.Red;

        Assert.Equal((SKColor?)SKColors.Green, control.StyleHover.BackgroundColor);
    }

    [Fact]
    public void ForeColorDoesNotModifyStyleHoverForegroundColor()
    {
        var control = new Control();
        control.StyleHover.ForegroundColor = SKColors.Green;

        control.ForeColor = SKColors.Red;

        Assert.Equal((SKColor?)SKColors.Green, control.StyleHover.ForegroundColor);
    }

    [Fact]
    public void BackColorUsesEffectiveStyleResolution()
    {
        var parentStyle = new ControlStyle(null, style => style.BackgroundColor = SKColors.AliceBlue);
        var controlStyle = new ControlStyle(parentStyle);
        var control = new StyleBackedControl(controlStyle);

        Assert.Null(control.Style.BackgroundColor);
        Assert.Equal(SKColors.AliceBlue, control.BackColor);
        Assert.Equal(control.Style.GetBackgroundColor(), control.BackColor);
    }

    [Fact]
    public void ForeColorUsesEffectiveStyleResolution()
    {
        var parentStyle = new ControlStyle(null, style => style.ForegroundColor = SKColors.DarkOrange);
        var controlStyle = new ControlStyle(parentStyle);
        var control = new StyleBackedControl(controlStyle);

        Assert.Null(control.Style.ForegroundColor);
        Assert.Equal(SKColors.DarkOrange, control.ForeColor);
        Assert.Equal(control.Style.GetForegroundColor(), control.ForeColor);
    }

    private sealed class StyleBackedControl : Control
    {
        private readonly ControlStyle style;
        private readonly ControlStyle styleHover;

        public StyleBackedControl(ControlStyle style)
        {
            this.style = style;
            styleHover = new ControlStyle(style);
        }

        public override ControlStyle Style => style;

        public override ControlStyle StyleHover => styleHover;
    }
}
