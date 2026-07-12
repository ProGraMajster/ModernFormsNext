using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorLifecycleTests
{
    [Fact]
    public void RapidViewModeChangesReuseSurfacesAndSynchronizeOnlyWhenVisible()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = "first",
            PreviewUpdateDelayMilliseconds = 60_000
        };
        var preview = editor.PreviewViewer;
        var surface = editor.EditorSurface;

        editor.ViewMode = MarkdownEditorViewMode.Split;
        editor.ViewMode = MarkdownEditorViewMode.Preview;
        editor.ViewMode = MarkdownEditorViewMode.Editor;
        editor.Markdown = "second";

        Assert.Equal("first", preview.Markdown);
        editor.ViewMode = MarkdownEditorViewMode.Split;
        Assert.Equal("second", preview.Markdown);
        Assert.Same(preview, editor.PreviewViewer);
        Assert.Same(surface, editor.EditorSurface);
    }

    [Fact]
    public void DisposingDuringPendingDebouncePreventsLaterSynchronization()
    {
        var editor = new MarkdownEditor
        {
            Markdown = "before",
            ViewMode = MarkdownEditorViewMode.Split,
            PreviewUpdateDelayMilliseconds = 60_000
        };
        editor.SelectAll();
        editor.SelectedText = "after";

        editor.Dispose();
        editor.FlushPreviewUpdate();
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void HighlightingThresholdIsInclusiveAndReversible(int offset, bool expectedDetailed)
    {
        using var editor = new MarkdownEditor();
        var length = MarkdownEditorTextBox.MaximumHighlightedSourceLength + offset;
        editor.Markdown = new string('a', length);
        editor.Select(length, 0);

        Assert.Equal(expectedDetailed, editor.EditorSurface.IsDetailedSyntaxHighlightingActive);
        Assert.Equal(length, editor.Markdown.Length);
        Assert.Equal(length, editor.SelectionStart);
        Assert.False(editor.Modified);
    }

    [Fact]
    public void EditingAcrossHighlightingThresholdPreservesSourceSelectionAndModifiedState()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = new string('a', MarkdownEditorTextBox.MaximumHighlightedSourceLength)
        };
        editor.Select(editor.Markdown.Length, 0);

        editor.SelectedText = "b";
        Assert.False(editor.EditorSurface.IsDetailedSyntaxHighlightingActive);
        Assert.Equal(MarkdownEditorTextBox.MaximumHighlightedSourceLength + 1, editor.Markdown.Length);
        Assert.Equal(editor.Markdown.Length, editor.SelectionStart);
        Assert.True(editor.Modified);

        editor.Undo();
        Assert.True(editor.EditorSurface.IsDetailedSyntaxHighlightingActive);
        Assert.Equal(MarkdownEditorTextBox.MaximumHighlightedSourceLength, editor.Markdown.Length);
        Assert.False(editor.Modified);
    }

    [Fact]
    public void LargeSourceStillSynchronizesToPreview()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = new string('a', MarkdownEditorTextBox.MaximumHighlightedSourceLength + 1),
            ViewMode = MarkdownEditorViewMode.Preview
        };

        Assert.Equal(editor.Markdown, editor.PreviewViewer.Markdown);
    }
}
