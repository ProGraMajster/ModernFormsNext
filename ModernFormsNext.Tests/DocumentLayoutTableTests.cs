using ModernFormsNext.Documents;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentLayoutTableTests
{
    [Fact]
    public void EqualContentProducesEqualColumns()
    {
        var widths = DocumentTableLayoutCalculator.Calculate(200, new[] { 20, 20 }, new[] { 60, 60 });

        Assert.Equal(new[] { 100, 100 }, widths);
    }

    [Fact]
    public void LongColumnReceivesMoreWidthThanShortColumn()
    {
        var widths = DocumentTableLayoutCalculator.Calculate(300, new[] { 80, 20 }, new[] { 240, 40 });

        Assert.True(widths[0] > widths[1]);
        Assert.Equal(300, widths.Sum());
    }

    [Fact]
    public void ShrinkingRespectsMinimumWidthsWhenTheyFit()
    {
        var widths = DocumentTableLayoutCalculator.Calculate(180, new[] { 80, 40, 20 }, new[] { 200, 120, 80 });

        Assert.True(widths[0] >= 80);
        Assert.True(widths[1] >= 40);
        Assert.True(widths[2] >= 20);
        Assert.Equal(180, widths.Sum());
    }

    [Fact]
    public void VerySmallWidthKeepsEveryColumnPositive()
    {
        var widths = DocumentTableLayoutCalculator.Calculate(2, new[] { 100, 50, 25 }, new[] { 200, 100, 50 });

        Assert.All(widths, width => Assert.True(width > 0));
        Assert.Equal(3, widths.Sum());
    }

    [Fact]
    public void NativeTableLayoutUsesContentAwareWidthsAndExactAvailableWidth()
    {
        var document = new MarkdownParser().Parse("""
            | Very long description | X |
            | --- | --- |
            | Another significantly longer value that needs room | OK |
            """);
        var layout = DocumentTestHelpers.LayoutDocument(document, 320);
        var firstRow = layout.Elements.OfType<DocumentTableCellLayoutElement>().Take(2).ToArray();

        Assert.True(firstRow[0].Bounds.Width > firstRow[1].Bounds.Width);
        Assert.Equal(320, firstRow.Sum(cell => cell.Bounds.Width));
    }

    [Fact]
    public void TableAlignmentIsPreservedWithContentAwareWidths()
    {
        var document = new MarkdownParser().Parse("""
            | Left | Center | Right |
            | :--- | :----: | ----: |
            | wrapped left content | wrapped center content | wrapped right content |
            """);
        var layout = DocumentTestHelpers.LayoutDocument(document, 240);
        var text = layout.Elements.OfType<DocumentTextLayoutElement>().Take(3).ToArray();

        Assert.Equal(TextAlignment.Left, text[0].TextBlock.Alignment);
        Assert.Equal(TextAlignment.Center, text[1].TextBlock.Alignment);
        Assert.Equal(TextAlignment.Right, text[2].TextBlock.Alignment);
    }

    [Fact]
    public void LongTokensDoNotCreateZeroOrNegativeWidths()
    {
        var longValue = new string('x', 120);
        var document = new MarkdownParser().Parse($"| Description | State |\n| --- | --- |\n| {longValue} | OK |");
        var layout = DocumentTestHelpers.LayoutDocument(document, 64);
        var cells = layout.Elements.OfType<DocumentTableCellLayoutElement>().Take(2).ToArray();

        Assert.All(cells, cell => Assert.True(cell.Bounds.Width > 0));
        Assert.Equal(64, cells.Sum(cell => cell.Bounds.Width));
    }
}
