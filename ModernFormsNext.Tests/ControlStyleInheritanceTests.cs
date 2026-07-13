using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ControlStyleInheritanceTests
{
    [Fact]
    public void TextBoxWithoutParentCanSetText()
    {
        var textBox = new TextBox();

        textBox.Text = "test";

        Assert.Null(textBox.Parent);
        Assert.Equal("test", textBox.Text);
    }

    [Fact]
    public void TextBoxDocumentCanBuildTextBlockWithoutVisualTree()
    {
        var textBox = new TextBox();
        textBox.document.Text = "detached document";

        var textBlock = textBox.document.GetTextBlock();

        Assert.Null(textBox.Parent);
        Assert.NotNull(textBlock);
    }

    [Fact]
    public void OwnerlessStyleCanResolveExplicitValues()
    {
        var style = new ControlStyle(null)
        {
            BackgroundColor = SKColors.CornflowerBlue,
            ForegroundColor = SKColors.DarkSlateGray,
            FontSize = 19,
            TextFont = new Font("sans-serif", 19, FontStyle.Bold | FontStyle.Italic)
        };

        Assert.Null(style.ParentStyle);
        Assert.Equal(SKColors.CornflowerBlue, style.GetBackgroundColor());
        Assert.Equal(SKColors.DarkSlateGray, style.GetForegroundColor());
        Assert.Equal(19, style.GetFontSize());
        Assert.Equal(FontStyle.Bold | FontStyle.Italic, style.GetFontStyle());
        Assert.False(string.IsNullOrWhiteSpace(style.GetFontFamily()));
        Assert.True(style.GetFontWeight() > 0);
    }

    [Fact]
    public void MissingParentStyleUsesFrameworkFallbacks()
    {
        var style = new ControlStyle(null);

        Assert.Null(style.ParentStyle);
        Assert.Equal(Theme.ControlMidColor, style.GetBackgroundColor());
        Assert.Equal(Theme.ForegroundColor, style.GetForegroundColor());
        Assert.Equal(Math.Max(1, Theme.FontSize), style.GetFontSize());
        Assert.Same(Theme.UIFont, style.GetFont());
    }

    [Fact]
    public void SelfReferencingStyleTerminatesWithFrameworkFallbacks()
    {
        var style = new ControlStyle(null);
        style.ParentStyle = style;

        Assert.Equal(Theme.ControlMidColor, style.GetBackgroundColor());
        Assert.Equal(Theme.ForegroundColor, style.GetForegroundColor());
        Assert.Equal(Math.Max(1, Theme.FontSize), style.GetFontSize());
        Assert.Same(Theme.UIFont, style.GetFont());
        Assert.Equal(ControlStyle.GetFontStyle(Theme.UIFont), style.GetFontStyle());
        Assert.Equal(Theme.UIFont.FamilyName, style.GetFontFamily());
        Assert.Equal(Theme.UIFont.FontWeight, style.GetFontWeight());
    }

    [Fact]
    public void TwoStyleCycleVisitsEachStyleOnceAndTerminates()
    {
        var first = new ControlStyle(null);
        var second = new ControlStyle(null)
        {
            BackgroundColor = SKColors.Goldenrod,
            ForegroundColor = SKColors.MidnightBlue,
            FontSize = 23,
            TextFont = new Font("sans-serif", 23, FontStyle.Bold | FontStyle.Underline)
        };

        first.ParentStyle = second;
        second.ParentStyle = first;

        Assert.Equal(SKColors.Goldenrod, first.GetBackgroundColor());
        Assert.Equal(SKColors.MidnightBlue, first.GetForegroundColor());
        Assert.Equal(23, first.GetFontSize());
        Assert.Same(second.Font, first.GetFont());
        Assert.Equal(FontStyle.Bold | FontStyle.Underline, first.GetFontStyle());
    }

    [Fact]
    public void ValuesAreInheritedFromNearestValidParent()
    {
        var root = new ControlStyle(null)
        {
            BackgroundColor = SKColors.AliceBlue,
            ForegroundColor = SKColors.DarkOrange,
            FontSize = 17,
            TextFont = new Font("sans-serif", 17, FontStyle.Bold | FontStyle.Italic | FontStyle.Strikeout)
        };
        var parent = new ControlStyle(root);
        var child = new ControlStyle(parent);

        Assert.Equal(SKColors.AliceBlue, child.GetBackgroundColor());
        Assert.Equal(SKColors.DarkOrange, child.GetForegroundColor());
        Assert.Equal(17, child.GetFontSize());
        Assert.Same(root.Font, child.GetFont());
        Assert.Equal(FontStyle.Bold | FontStyle.Italic | FontStyle.Strikeout, child.GetFontStyle());
        Assert.Equal(root.GetFontFamily(), child.GetFontFamily());
        Assert.Equal(root.GetFontWeight(), child.GetFontWeight());
    }

    [Fact]
    public void DefaultFontIsStableForDetachedStyle()
    {
        var style = new ControlStyle(null);

        var font = style.GetFont();

        Assert.NotNull(font);
        Assert.Same(Theme.UIFont, font);
        Assert.False(string.IsNullOrWhiteSpace(style.GetFontFamily()));
        Assert.Equal(font.FontWeight, style.GetFontWeight());
        Assert.Equal(ControlStyle.GetFontStyle(font), style.GetFontStyle());
        Assert.NotNull(Theme.CreateTypefaceOrDefault(
            "family-unavailable-on-all-supported-platforms",
            SKFontStyleWeight.Normal,
            SKFontStyleSlant.Upright));
    }

    [Fact]
    public void MissingTypefaceHasRegularFontStyleFallback()
    {
        Assert.Equal(FontStyle.Regular, ControlStyle.GetFontStyle(null));
    }
}
