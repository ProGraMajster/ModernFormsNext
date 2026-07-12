using System.ComponentModel;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorQualityTests
{
    [Fact]
    public void MarkdownAndTextRemainOneSourceOfTruthWithoutDuplicateEvents()
    {
        using var editor = new MarkdownEditor();
        var markdownChanged = 0;
        var textChanged = 0;
        editor.MarkdownChanged += (_, _) => markdownChanged++;
        editor.TextChanged += (_, _) => textChanged++;

        editor.Markdown = "# first";
        Assert.Equal(editor.Markdown, editor.Text);
        Assert.Equal(1, markdownChanged);
        Assert.Equal(1, textChanged);

        editor.Text = "# second";
        Assert.Equal("# second", editor.Markdown);
        Assert.Equal(2, markdownChanged);
        Assert.Equal(2, textChanged);
    }

    [Fact]
    public void ProgrammaticSourceAndViewChangesDoNotCreateUndoRecords()
    {
        using var editor = new MarkdownEditor { Markdown = "source" };

        editor.ViewMode = MarkdownEditorViewMode.Split;
        editor.ViewMode = MarkdownEditorViewMode.Preview;
        editor.ViewMode = MarkdownEditorViewMode.Editor;

        Assert.False(editor.CanUndo);
        Assert.False(editor.Modified);
    }

    [Fact]
    public void SyntaxStyleRefreshDoesNotChangeTextSelectionOrHistory()
    {
        using var editor = new MarkdownEditor { Markdown = "**source**" };
        editor.Select(2, 6);

        editor.SyntaxStyle.HeadingMarkerColor = SKColors.Red;

        Assert.Equal("**source**", editor.Markdown);
        Assert.Equal("source", editor.SelectedText);
        Assert.False(editor.CanUndo);
        Assert.False(editor.Modified);
    }

    [Fact]
    public void DesignerFacingPropertiesRemainBrowsableAndEditable()
    {
        var properties = TypeDescriptor.GetProperties(typeof(MarkdownEditor));

        Assert.False(properties[nameof(MarkdownEditor.Markdown)]!.IsReadOnly);
        Assert.True(properties[nameof(MarkdownEditor.ViewMode)]!.IsBrowsable);
        Assert.True(properties[nameof(MarkdownEditor.ShowToolbar)]!.IsBrowsable);
        Assert.True(properties[nameof(MarkdownEditor.ReadOnly)]!.IsBrowsable);
        Assert.True(properties[nameof(MarkdownEditor.PreviewUpdateDelayMilliseconds)]!.IsBrowsable);
        Assert.True(properties[nameof(MarkdownEditor.SyntaxStyle)]!.IsBrowsable);
    }
}
