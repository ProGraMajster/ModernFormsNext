using System;
using System.Drawing;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        var bounds = PaddedClientRectangle;
        var toolbarHeight = ShowToolbar ? Math.Min(34, Math.Max(0, bounds.Height)) : 0;
        toolbar.SetBounds(bounds.Left, bounds.Top, bounds.Width, toolbarHeight);
        toolbar.Visible = ShowToolbar;

        var content = new Rectangle(bounds.Left, bounds.Top + toolbarHeight, bounds.Width, Math.Max(0, bounds.Height - toolbarHeight));
        if (ViewMode == MarkdownEditorViewMode.Split)
        {
            splitContainer.Bounds = content;
            var minimumSplitWidth = splitContainer.Panel1MinimumSize
                + splitContainer.Panel2MinimumSize
                + splitContainer.SplitterWidth;
            if (content.Width >= minimumSplitWidth)
            {
                splitContainer.SplitterDistance = Math.Max(
                    splitContainer.Panel1MinimumSize,
                    (int)Math.Round(content.Width * SplitRatio));
            }
        }
        else if (ViewMode == MarkdownEditorViewMode.Preview)
        {
            previewViewer.Bounds = content;
        }
        else
        {
            editorSurface.Bounds = content;
        }

        if (ViewMode == MarkdownEditorViewMode.Split)
            RefreshScrollSynchronization();
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        editorSurface.RefreshSyntaxHighlighting();
    }

    private void AttachViewControls()
    {
        RemoveFromParent(editorSurface);
        RemoveFromParent(previewViewer);
        RemoveFromParent(splitContainer);

        switch (ViewMode)
        {
            case MarkdownEditorViewMode.Preview:
                Controls.Add(previewViewer);
                break;
            case MarkdownEditorViewMode.Split:
                splitContainer.Panel1.Controls.Add(editorSurface);
                splitContainer.Panel2.Controls.Add(previewViewer);
                Controls.Add(splitContainer);
                break;
            default:
                Controls.Add(editorSurface);
                break;
        }

        // Keep the toolbar last in z-order so it remains above a fill-sized content surface.
        RemoveFromParent(toolbar);
        Controls.Add(toolbar);
    }

    private static void RemoveFromParent(Control control)
        => control.Parent?.Controls.Remove(control);
}
