using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorLinkCommandTests
{
    [Fact]
    public void InsertsWithAndWithoutSelectionAsOneUndoOperation()
    {
        using var editor = new MarkdownEditor { Markdown = "Hello " };
        editor.Select(editor.Markdown.Length, 0);

        editor.InsertLink("https://example.com", "Example");

        Assert.Equal("Hello [Example](https://example.com)", editor.Markdown);
        Assert.True(editor.Modified);
        editor.Undo();
        Assert.Equal("Hello ", editor.Markdown);
        Assert.False(editor.Modified);
        editor.Redo();
        Assert.Equal("Hello [Example](https://example.com)", editor.Markdown);

        editor.Markdown = "Hello world";
        editor.Select(6, 5);
        editor.InsertLink("mailto:test@example.com");
        Assert.Equal("Hello [world](mailto:test@example.com)", editor.Markdown);
    }

    [Fact]
    public async Task RequestEditsExistingLinkAtCaret()
    {
        using var editor = new MarkdownEditor { Markdown = "Before [world](https://old.example.com) after" };
        editor.Select(editor.Markdown.IndexOf("world", StringComparison.Ordinal) + 2, 0);
        editor.InsertLinkRequested += (_, e) =>
        {
            Assert.Equal("world", e.SuggestedText);
            Assert.Equal("https://old.example.com", e.SuggestedUrl);
            e.Text = "new label";
            e.Url = "https://example.com/a(b)";
            e.Handled = true;
        };

        Assert.True(await editor.RequestInsertLinkAsync());

        Assert.Equal("Before [new label](<https://example.com/a(b)>) after", editor.Markdown);
        Assert.Equal(editor.Markdown.IndexOf(" after", StringComparison.Ordinal), editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
        editor.Undo();
        Assert.Equal("Before [world](https://old.example.com) after", editor.Markdown);
    }

    [Fact]
    public async Task CancelReadOnlyAndEmptyUrlDoNotMutateHistory()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.Select(1, 2);
        editor.InsertLinkRequested += (_, e) => e.Cancel = true;

        Assert.False(await editor.RequestInsertLinkAsync());
        Assert.Equal("text", editor.Markdown);
        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(2, editor.SelectionLength);
        Assert.False(editor.Modified);
        Assert.False(editor.CanUndo);

        var requests = 0;
        editor.InsertLinkRequested += (_, _) => requests++;
        editor.ReadOnly = true;
        editor.RequestInsertLink();
        Assert.Equal(0, requests);
    }
}
