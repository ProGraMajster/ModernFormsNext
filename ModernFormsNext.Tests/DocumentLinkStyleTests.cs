using ModernFormsNext.Documents;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentLinkStyleTests
{
    [Fact]
    public void NormalHoverAndPressedColorsResolveInPriorityOrder()
    {
        using var viewer = new DocumentViewer();
        var style = viewer.DocumentStyle;
        style.LinkColor = SKColors.Blue;
        style.HoveredLinkColor = SKColors.Green;
        style.PressedLinkColor = SKColors.Red;

        Assert.Equal(SKColors.Blue, style.ResolveLinkColor(viewer, hovered: false, pressed: false));
        Assert.Equal(SKColors.Green, style.ResolveLinkColor(viewer, hovered: true, pressed: false));
        Assert.Equal(SKColors.Red, style.ResolveLinkColor(viewer, hovered: true, pressed: true));
    }

    [Fact]
    public void ChangingPressedLinkColorRaisesChangedAndIncrementsVersion()
    {
        var style = new DocumentStyle();
        var changed = 0;
        var version = style.Version;
        style.Changed += (_, _) => changed++;

        style.PressedLinkColor = SKColors.Red;

        Assert.Equal(1, changed);
        Assert.Equal(version + 1, style.Version);
    }

    [Fact]
    public void DefaultPressedColorIsDistinctFromDefaultHoverColor()
    {
        using var viewer = new DocumentViewer();
        var style = viewer.DocumentStyle;

        var hover = style.ResolveLinkColor(viewer, hovered: true, pressed: false);
        var pressed = style.ResolveLinkColor(viewer, hovered: true, pressed: true);

        Assert.NotEqual(hover, pressed);
    }

    [Fact]
    public void LinkColorChangeInvalidatesCachedLayout()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("[link](https://example.com)")
        };
        var original = viewer.GetDocumentLayout();

        viewer.DocumentStyle.PressedLinkColor = SKColors.Red;
        var refreshed = viewer.GetDocumentLayout();

        Assert.NotSame(original, refreshed);
    }
}
