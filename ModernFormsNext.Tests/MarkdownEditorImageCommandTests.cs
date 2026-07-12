using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorImageCommandTests
{
    [Theory]
    [InlineData("Images/example.png")]
    [InlineData("https://example.com/image.png")]
    [InlineData("data:image/png;base64,AA==")]
    public void InsertsSupportedSourceFormsAndUndoRedoRoundTrips(string source)
    {
        using var editor = new MarkdownEditor { Markdown = "Alt text" };
        editor.SelectAll();

        editor.InsertImage(source, null, "Optional title");

        Assert.Equal($"![Alt text]({source} \"Optional title\")", editor.Markdown);
        editor.Undo();
        Assert.Equal("Alt text", editor.Markdown);
        editor.Redo();
        Assert.Equal($"![Alt text]({source} \"Optional title\")", editor.Markdown);
    }

    [Fact]
    public void EmptyTitleIsOmitted()
    {
        using var editor = new MarkdownEditor();

        editor.InsertImage("image.png", "Alt", string.Empty);

        Assert.Equal("![Alt](image.png)", editor.Markdown);
    }

    [Fact]
    public async Task RequestEditsExistingImageAtCaret()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = "Before ![Old alt](Images/old.png \"Old title\") after"
        };
        editor.Select(editor.Markdown.IndexOf("Old alt", StringComparison.Ordinal) + 2, 0);
        editor.InsertImageRequested += (_, e) =>
        {
            Assert.Equal("Old alt", e.AltText);
            Assert.Equal("Images/old.png", e.Source);
            Assert.Equal("Old title", e.Title);
            e.AltText = "New [alt]";
            e.Source = "Images/new image.png";
            e.Title = "New \"title\"";
            e.Handled = true;
        };

        Assert.True(await editor.RequestInsertImageAsync());

        Assert.Equal(
            "Before ![New \\[alt\\]](<Images/new image.png> \"New \\\"title\\\"\") after",
            editor.Markdown);
        editor.Undo();
        Assert.Equal("Before ![Old alt](Images/old.png \"Old title\") after", editor.Markdown);
    }

    [Fact]
    public async Task CancelAndReadOnlyDoNotInsertImages()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.InsertImageRequested += (_, e) => e.Cancel = true;

        Assert.False(await editor.RequestInsertImageAsync());
        Assert.Equal("text", editor.Markdown);
        Assert.False(editor.CanUndo);

        var requests = 0;
        editor.InsertImageRequested += (_, _) => requests++;
        editor.ReadOnly = true;
        editor.RequestInsertImage();
        Assert.Equal(0, requests);
    }
}
