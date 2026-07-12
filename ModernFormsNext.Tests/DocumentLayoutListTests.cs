using ModernFormsNext.Documents;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentLayoutListTests
{
    [Fact]
    public void OrderedListHangingIndentReservesMarkerColumnForWideNumbers()
    {
        var document = new MarkdownParser().Parse("""
            98. ninety eight
            99. ninety nine
            100. one hundred with enough text to wrap on a narrow document width and prove that wrapped lines stay under the content column
            """);

        var layout = DocumentTestHelpers.LayoutDocument(document, 160);
        var marker100 = Assert.Single(layout.Elements.OfType<DocumentTextLayoutElement>(), element => element.Text == "100.");
        var content = Assert.Single(layout.Elements.OfType<DocumentTextLayoutElement>(), element => element.Text.StartsWith("one hundred"));

        Assert.True(content.Bounds.X > marker100.Bounds.X + marker100.Bounds.Width);
        Assert.True(content.Bounds.Width < 160);
    }

    [Fact]
    public void TaskListItemsUseCheckboxElementsInsteadOfTextMarkers()
    {
        var document = new MarkdownParser().Parse("""
            - [x] Done
            - [ ] Todo
            """);

        var layout = DocumentTestHelpers.LayoutDocument(document, 240);
        var checkboxes = layout.Elements.OfType<DocumentTaskCheckBoxLayoutElement>().ToArray();
        var text = string.Join(" ", layout.Elements.OfType<DocumentTextLayoutElement>().Select(element => element.Text));

        Assert.Equal(2, checkboxes.Length);
        Assert.Contains(checkboxes, box => box.CheckState == CheckState.Checked);
        Assert.Contains(checkboxes, box => box.CheckState == CheckState.Unchecked);
        Assert.DoesNotContain("[x]", text);
        Assert.DoesNotContain("[ ]", text);
        Assert.DoesNotContain("\u2022", text);
    }
}
