using System.Drawing;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorPreviewInteractionTests
{
    [Theory]
    [InlineData("😀 [link](https://example.com)")]
    [InlineData("- [link](https://example.com)")]
    [InlineData("> [link](https://example.com)")]
    [InlineData("| Column |\n| --- |\n| [link](https://example.com) |")]
    public void PreviewLinkClickIsForwardedWithoutChangingEditorSelection(string markdown)
    {
        using var editor = new MarkdownEditor
        {
            Markdown = markdown,
            ViewMode = MarkdownEditorViewMode.Split,
            ShowToolbar = false,
            Size = new Size(620, 260)
        };
        editor.Select(0, Math.Min(2, markdown.Length));
        editor.PerformLayout();
        editor.PreviewViewer.PerformLayout();
        var link = Assert.Single(editor.PreviewViewer.GetDocumentLayout().Links);
        var point = GetPointInsideCodePoint(link.Element, link.Start);
        Documents.DocumentLinkClickedEventArgs? forwarded = null;
        editor.PreviewLinkClicked += (_, e) => forwarded = e;

        editor.PreviewViewer.RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));
        editor.PreviewViewer.RaiseMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));

        Assert.NotNull(forwarded);
        Assert.Equal("https://example.com", forwarded.Destination);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(Math.Min(2, markdown.Length), editor.SelectionLength);
    }

    [Fact]
    public void WrappedPreviewLinkRetainsNativeHitTestingAndForwarding()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = "[a long wrapped link label that needs more than one visual line](https://example.com/wrapped)",
            ViewMode = MarkdownEditorViewMode.Preview,
            ShowToolbar = false,
            Size = new Size(190, 220)
        };
        editor.PerformLayout();
        editor.PreviewViewer.PerformLayout();
        var link = Assert.Single(editor.PreviewViewer.GetDocumentLayout().Links);
        var firstTop = link.Element.TextBlock.GetCaretInfo(new CaretPosition(link.Start)).CaretRectangle.Top;
        var later = Enumerable.Range(link.Start + 1, link.End - link.Start - 1)
            .First(index => link.Element.TextBlock.GetCaretInfo(new CaretPosition(index)).CaretRectangle.Top > firstTop);
        var point = GetPointInsideCodePoint(link.Element, later);
        var clicks = 0;
        editor.PreviewLinkClicked += (_, _) => clicks++;

        editor.PreviewViewer.RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));
        editor.PreviewViewer.RaiseMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));

        Assert.Equal(1, clicks);
    }

    private static Point GetPointInsideCodePoint(Documents.DocumentTextLayoutElement element, int index)
    {
        var start = element.TextBlock.GetCaretInfo(new CaretPosition(index)).CaretRectangle;
        var end = element.TextBlock.GetCaretInfo(new CaretPosition(index + 1)).CaretRectangle;
        var glyphWidth = end.Top == start.Top ? end.Left - start.Left : Math.Max(2f, start.Height * 0.5f);
        return new Point(
            element.TextOrigin.X + (int)Math.Round(start.Left + Math.Max(2f, glyphWidth) * 0.35f),
            element.TextOrigin.Y + (int)Math.Round(start.Top + start.Height / 2f));
    }
}
