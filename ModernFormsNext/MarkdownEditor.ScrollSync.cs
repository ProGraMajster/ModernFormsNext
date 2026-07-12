using System;
using System.ComponentModel;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    private bool synchronizeScrolling = true;
    private bool synchronizingScroll;

    /// <summary>
    /// Gets or sets a value indicating whether source and preview vertical scrolling is synchronized
    /// proportionally while <see cref="ViewMode"/> is <see cref="MarkdownEditorViewMode.Split"/>.
    /// </summary>
    /// <remarks>
    /// Synchronization maps each scrollbar's current value onto the other scrollbar's available
    /// range. It does not attempt line- or block-level semantic alignment and has no effect outside
    /// split mode. Changing this property does not modify Markdown or undo history.
    /// </remarks>
    [DefaultValue(true)]
    [Category("Preview")]
    [Description("Synchronizes source and preview scrolling proportionally in split mode.")]
    public bool SynchronizeScrolling
    {
        get => synchronizeScrolling;
        set
        {
            if (synchronizeScrolling == value)
                return;

            synchronizeScrolling = value;
            if (value && ViewMode == MarkdownEditorViewMode.Split)
                SynchronizeScrollFromEditor();
        }
    }

    internal void SynchronizeScrollFromEditor()
        => SynchronizeScrollBars(editorSurface.VerticalScrollBar, previewViewer.VerticalScrollBar);

    internal void SynchronizeScrollFromPreview()
        => SynchronizeScrollBars(previewViewer.VerticalScrollBar, editorSurface.VerticalScrollBar);

    internal void RefreshScrollSynchronization()
    {
        if (disposed || !SynchronizeScrolling || ViewMode != MarkdownEditorViewMode.Split)
            return;

        var sourceViewport = editorSurface.PaddedClientRectangle;
        var previewViewport = previewViewer.PaddedClientRectangle;
        if (sourceViewport.Width <= 0
            || sourceViewport.Height <= 0
            || previewViewport.Width <= 0
            || previewViewport.Height <= 0)
        {
            return;
        }

        // TextBox updates its range while rendering, whereas DocumentViewer updates it while
        // building layout. Refresh both here after a split resize or preview replacement so the
        // proportional mapping does not use ranges from the previous viewport.
        try
        {
            synchronizingScroll = true;
            editorSurface.UpdateScrollBars(editorSurface.GetRichTextBlock());
            previewViewer.GetDocumentLayout();
        }
        finally
        {
            synchronizingScroll = false;
        }

        SynchronizeScrollFromEditor();
    }

    private void EditorVerticalScrollBar_ValueChanged(object? sender, EventArgs e)
        => SynchronizeScrollFromEditor();

    private void PreviewVerticalScrollBar_ValueChanged(object? sender, EventArgs e)
        => SynchronizeScrollFromPreview();

    private void SynchronizeScrollBars(ScrollBar source, ScrollBar target)
    {
        if (disposed
            || synchronizingScroll
            || !SynchronizeScrolling
            || ViewMode != MarkdownEditorViewMode.Split)
        {
            return;
        }

        var sourceRange = source.Enabled ? Math.Max(0, source.Maximum - source.Minimum) : 0;
        var targetRange = target.Enabled ? Math.Max(0, target.Maximum - target.Minimum) : 0;
        if (sourceRange == 0 || targetRange == 0)
            return;

        var sourcePosition = Math.Clamp(source.Value - source.Minimum, 0, sourceRange);
        var ratio = sourcePosition / (double)sourceRange;
        var targetValue = target.Minimum + (int)Math.Round(ratio * targetRange);
        targetValue = Math.Clamp(targetValue, target.Minimum, target.Maximum);

        // Ignore a one-pixel difference to avoid visible oscillation caused by inverse rounding.
        if (Math.Abs(target.Value - targetValue) <= 1)
            return;

        try
        {
            synchronizingScroll = true;
            target.Value = targetValue;
        }
        finally
        {
            synchronizingScroll = false;
        }
    }
}
