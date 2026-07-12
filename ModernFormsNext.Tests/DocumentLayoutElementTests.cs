using System.Drawing;
using ModernFormsNext.Documents;
using SkiaSharp;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentLayoutElementTests
{
    [Fact]
    public void TextElementRequiresTextBlockAndText()
    {
        Assert.Throws<ArgumentNullException>(() => new DocumentTextLayoutElement(
            Rectangle.Empty,
            null!,
            Point.Empty,
            "text",
            0));
        Assert.Throws<ArgumentNullException>(() => new DocumentTextLayoutElement(
            Rectangle.Empty,
            new TextBlock(),
            Point.Empty,
            null!,
            0));
    }

    [Fact]
    public void TaskCheckboxHasRequiredNonNullableState()
    {
        var element = new DocumentTaskCheckBoxLayoutElement(new Rectangle(0, 0, 12, 12), CheckState.Checked);

        Assert.Equal(CheckState.Checked, element.CheckState);
    }

    [Fact]
    public void LoadedAndPlaceholderImagesHaveDistinctStates()
    {
        using var bitmap = new SKBitmap(2, 2);
        var loaded = new DocumentLoadedImageLayoutElement(new Rectangle(0, 0, 2, 2), bitmap);
        var placeholder = new DocumentImagePlaceholderLayoutElement(
            new Rectangle(0, 0, 20, 20),
            new TextBlock(),
            Point.Empty,
            "fallback",
            SKColors.Gray,
            failed: true);

        Assert.Same(bitmap, loaded.Bitmap);
        Assert.True(placeholder.Failed);
        Assert.Equal("fallback", placeholder.FallbackText);
    }
}
