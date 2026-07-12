using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorScrollSyncTests
{
    [Fact]
    public void SourceAndPreviewSynchronizeProportionallyWithoutRecursion()
    {
        using var editor = CreateSplitEditor();
        var source = editor.EditorSurface.VerticalScrollBar;
        var preview = editor.PreviewViewer.VerticalScrollBar;
        SetRange(source, 100);
        SetRange(preview, 400);
        var sourceEvents = 0;
        var previewEvents = 0;
        source.ValueChanged += (_, _) => sourceEvents++;
        preview.ValueChanged += (_, _) => previewEvents++;

        source.Value = 25;
        Assert.Equal(100, preview.Value);
        Assert.Equal(1, sourceEvents);
        Assert.Equal(1, previewEvents);

        preview.Value = 300;
        Assert.Equal(75, source.Value);
        Assert.Equal(2, sourceEvents);
        Assert.Equal(2, previewEvents);
    }

    [Fact]
    public void DisabledOrNonSplitModesDoNotSynchronize()
    {
        using var editor = CreateSplitEditor();
        var source = editor.EditorSurface.VerticalScrollBar;
        var preview = editor.PreviewViewer.VerticalScrollBar;
        SetRange(source, 100);
        SetRange(preview, 200);
        editor.SynchronizeScrolling = false;

        source.Value = 50;
        Assert.Equal(0, preview.Value);

        editor.SynchronizeScrolling = true;
        Assert.Equal(100, preview.Value);
        editor.ViewMode = MarkdownEditorViewMode.Editor;
        source.Value = 75;
        Assert.Equal(100, preview.Value);
    }

    [Fact]
    public void ZeroRangesClampAndViewTransitionsRemainStable()
    {
        using var editor = CreateSplitEditor();
        var source = editor.EditorSurface.VerticalScrollBar;
        var preview = editor.PreviewViewer.VerticalScrollBar;
        SetRange(source, 0);
        SetRange(preview, 200);

        editor.SynchronizeScrollFromEditor();
        Assert.Equal(0, preview.Value);

        SetRange(source, 3);
        source.Value = 3;
        Assert.Equal(200, preview.Value);
        editor.ViewMode = MarkdownEditorViewMode.Preview;
        editor.ViewMode = MarkdownEditorViewMode.Split;
        editor.Size = new System.Drawing.Size(820, 420);
        editor.PerformLayout();
        Assert.InRange(preview.Value, preview.Minimum, preview.Maximum);
    }

    [Fact]
    public void DisposeAfterRangeChangesIsSafe()
    {
        var editor = CreateSplitEditor();
        SetRange(editor.EditorSurface.VerticalScrollBar, 100);
        SetRange(editor.PreviewViewer.VerticalScrollBar, 100);
        editor.EditorSurface.VerticalScrollBar.Value = 50;

        editor.Dispose();
    }

    [Fact]
    public void TransientZeroSizedSplitViewportDoesNotRefreshInvalidScrollRanges()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = "content",
            ViewMode = MarkdownEditorViewMode.Split,
            Size = System.Drawing.Size.Empty
        };

        editor.PerformLayout();
        editor.RefreshScrollSynchronization();

        Assert.Equal(0, editor.EditorSurface.VerticalScrollBar.Value);
        Assert.Equal(0, editor.PreviewViewer.VerticalScrollBar.Value);
    }

    private static MarkdownEditor CreateSplitEditor()
        => new()
        {
            Markdown = string.Join('\n', Enumerable.Range(1, 80).Select(i => $"Line {i}")),
            ViewMode = MarkdownEditorViewMode.Split,
            PreviewUpdateDelayMilliseconds = 0
        };

    private static void SetRange(ScrollBar scrollBar, int maximum)
    {
        scrollBar.Minimum = 0;
        scrollBar.Maximum = maximum;
        scrollBar.Enabled = maximum > 0;
        scrollBar.Value = 0;
    }
}
