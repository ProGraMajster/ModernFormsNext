using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorPreviewTests
{
    [Fact]
    public void ViewModesReuseOnePreviewAndOneEditingSurface()
    {
        using var editor = new MarkdownEditor { Markdown = "# Preview" };
        var preview = editor.PreviewViewer;
        var surface = editor.EditorSurface;

        Assert.Equal(MarkdownEditorViewMode.Editor, editor.ViewMode);
        Assert.False(editor.IsPreviewVisible);

        editor.ViewMode = MarkdownEditorViewMode.Preview;
        Assert.True(editor.IsPreviewVisible);
        Assert.Equal("# Preview", preview.Markdown);

        editor.ViewMode = MarkdownEditorViewMode.Split;
        Assert.Same(preview, editor.PreviewViewer);
        Assert.Same(surface, editor.EditorSurface);
        Assert.True(editor.IsPreviewVisible);
    }

    [Fact]
    public void VisiblePreviewUpdateIsDebouncedUntilFlushed()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = "old",
            ViewMode = MarkdownEditorViewMode.Split,
            PreviewUpdateDelayMilliseconds = 60_000
        };
        Assert.Equal("old", editor.PreviewViewer.Markdown);

        editor.SelectAll();
        editor.SelectedText = "new";

        Assert.Equal("old", editor.PreviewViewer.Markdown);
        editor.FlushPreviewUpdate();
        Assert.Equal("new", editor.PreviewViewer.Markdown);
    }

    [Fact]
    public void EditorModeDoesNotParsePreviewOnSourceChanges()
    {
        using var editor = new MarkdownEditor { Markdown = "first" };

        editor.SelectAll();
        editor.SelectedText = "second";

        Assert.Equal(string.Empty, editor.PreviewViewer.Markdown);
        editor.ViewMode = MarkdownEditorViewMode.Preview;
        Assert.Equal("second", editor.PreviewViewer.Markdown);
    }

    [Fact]
    public void PreviewStyleAndLinkEventRemainNativeViewerApi()
    {
        using var editor = new MarkdownEditor();
        var clicked = false;
        editor.PreviewStyle.LinkColor = SKColors.Red;
        editor.PreviewViewer.LinkClicked += (_, _) => clicked = true;

        Assert.Same(editor.PreviewStyle, editor.PreviewViewer.DocumentStyle);
        Assert.Equal(SKColors.Red, editor.PreviewViewer.DocumentStyle.LinkColor);
        Assert.False(clicked);
    }

    [Fact]
    public void PreviewDelayValidatesAndSupportsImmediateUpdates()
    {
        using var editor = new MarkdownEditor { ViewMode = MarkdownEditorViewMode.Preview };
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.PreviewUpdateDelayMilliseconds = -1);

        editor.PreviewUpdateDelayMilliseconds = 0;
        editor.SelectedText = "# Immediate";

        Assert.Equal("# Immediate", editor.PreviewViewer.Markdown);
    }

    [Fact]
    public void DisposeWhilePreviewUpdateIsPendingIsSafe()
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
}
