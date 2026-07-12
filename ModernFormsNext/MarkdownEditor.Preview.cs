using System;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    private bool previewImagesAreDirty;

    internal void FlushPreviewUpdate() => UpdatePreviewNow();

    /// <summary>
    /// Invalidates image resources in the native preview without changing Markdown or undo history.
    /// </summary>
    /// <remarks>
    /// Call this after a host replaces a file referenced by unchanged Markdown. Hidden previews
    /// defer reloading until Preview or Split mode becomes visible.
    /// </remarks>
    public void RefreshPreviewImages()
    {
        if (disposed)
            return;

        previewImagesAreDirty = true;
        if (IsPreviewVisible)
            UpdatePreviewNow();
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        previewTimer.Stop();
        UpdatePreviewNow();
    }

    private void MarkPreviewDirty()
    {
        previewIsDirty = true;
        SchedulePreviewUpdate();
    }

    private void SchedulePreviewUpdate()
    {
        if (!IsPreviewVisible || disposed || DesignMode)
            return;

        previewTimer.Stop();
        if (previewDelayIsImmediate)
            UpdatePreviewNow();
        else
            previewTimer.Start();
    }

    private void UpdatePreviewNow()
    {
        previewTimer.Stop();
        if (disposed || !IsPreviewVisible || DesignMode)
            return;

        var sourceChanged = previewViewer.Markdown != Markdown;
        if (previewIsDirty || sourceChanged)
            previewViewer.Markdown = Markdown;
        if (previewImagesAreDirty && !sourceChanged)
            previewViewer.ReloadDocumentImages();
        previewIsDirty = false;
        previewImagesAreDirty = false;

        if (ViewMode == MarkdownEditorViewMode.Split)
            RefreshScrollSynchronization();
    }

    private void PreviewViewer_LinkClicked(object? sender, Documents.DocumentLinkClickedEventArgs e)
        => OnPreviewLinkClicked(e);
}
