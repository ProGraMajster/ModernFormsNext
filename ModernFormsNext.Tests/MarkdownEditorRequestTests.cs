using System.ComponentModel;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorRequestTests
{
    [Fact]
    public async Task SelectionSnapshotProvidesContextAndAsyncDeferralAppliesOnce()
    {
        using var editor = new MarkdownEditor { Markdown = "Hello world" };
        editor.Select(6, 5);
        InsertLinkRequestEventArgs? captured = null;
        IDisposable? deferral = null;
        editor.InsertLinkRequested += (_, e) =>
        {
            captured = e;
            deferral = e.GetDeferral();
        };

        var request = editor.RequestInsertLinkAsync();

        Assert.False(request.IsCompleted);
        Assert.NotNull(captured);
        Assert.Equal("world", captured.SelectedText);
        Assert.Equal(6, captured.SelectionStart);
        Assert.Equal(5, captured.SelectionLength);
        captured.Text = "World";
        captured.Url = "https://example.com";
        captured.Handled = true;
        deferral!.Dispose();

        Assert.True(await request);
        Assert.Equal("Hello [World](https://example.com)", editor.Markdown);
        editor.Undo();
        Assert.Equal("Hello world", editor.Markdown);
    }

    [Fact]
    public async Task SourceChangeDuringDeferredRequestRejectsStaleSelection()
    {
        using var editor = new MarkdownEditor { Markdown = "original" };
        InsertLinkRequestEventArgs? captured = null;
        IDisposable? deferral = null;
        editor.InsertLinkRequested += (_, e) =>
        {
            captured = e;
            deferral = e.GetDeferral();
        };

        var request = editor.RequestInsertLinkAsync();
        editor.Markdown = "replacement";
        captured!.Text = "stale";
        captured.Url = "https://example.com";
        captured.Handled = true;
        deferral!.Dispose();

        Assert.False(await request);
        Assert.Equal("replacement", editor.Markdown);
        Assert.False(editor.Modified);
    }

    [Fact]
    public async Task DisposeDuringDeferredImageRequestDoesNotApplyResult()
    {
        var editor = new MarkdownEditor { Markdown = "original" };
        InsertImageRequestEventArgs? captured = null;
        IDisposable? deferral = null;
        editor.InsertImageRequested += (_, e) =>
        {
            captured = e;
            deferral = e.GetDeferral();
        };

        var request = editor.RequestInsertImageAsync();
        editor.Dispose();
        captured!.Source = "image.png";
        captured.Handled = true;
        deferral!.Dispose();

        Assert.False(await request);
    }

    [Fact]
    public void CtrlKAndToolbarUseTheSameHostedRequestPath()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        var requests = 0;
        editor.InsertLinkRequested += (_, e) =>
        {
            requests++;
            e.Cancel = true;
        };

        editor.EditorSurface.RaiseKeyDown(new KeyEventArgs(Keys.Control | Keys.K));
        var link = editor.CommandToolbar.Items.Single(item => item.ToolTipText.StartsWith("Insert or edit link", StringComparison.Ordinal));
        link.OnClick(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        Assert.Equal(2, requests);
        Assert.Equal("text", editor.Markdown);
    }

    [Fact]
    public void ImageToolbarUsesTheHostedRequestPath()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        var requests = 0;
        editor.InsertImageRequested += (_, e) =>
        {
            requests++;
            e.Cancel = true;
        };

        var image = editor.CommandToolbar.Items.Single(item => item.ToolTipText.StartsWith("Insert or edit image", StringComparison.Ordinal));
        image.OnClick(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        Assert.Equal(1, requests);
        Assert.Equal("text", editor.Markdown);
    }

    [Fact]
    public async Task SourceChangeDuringDeferredImageRequestRejectsStaleSelection()
    {
        using var editor = new MarkdownEditor { Markdown = "original" };
        InsertImageRequestEventArgs? captured = null;
        IDisposable? deferral = null;
        editor.InsertImageRequested += (_, e) =>
        {
            captured = e;
            deferral = e.GetDeferral();
        };

        var request = editor.RequestInsertImageAsync();
        editor.Markdown = "replacement";
        captured!.Source = "image.png";
        captured.Handled = true;
        deferral!.Dispose();

        Assert.False(await request);
        Assert.Equal("replacement", editor.Markdown);
    }

    [Fact]
    public async Task DisposeDuringDeferredLinkRequestDoesNotApplyResult()
    {
        var editor = new MarkdownEditor { Markdown = "original" };
        InsertLinkRequestEventArgs? captured = null;
        IDisposable? deferral = null;
        editor.InsertLinkRequested += (_, e) =>
        {
            captured = e;
            deferral = e.GetDeferral();
        };

        var request = editor.RequestInsertLinkAsync();
        editor.Dispose();
        captured!.Text = "link";
        captured.Url = "https://example.com";
        captured.Handled = true;
        deferral!.Dispose();

        Assert.False(await request);
    }

    [Fact]
    public void NewDesignerMetadataUsesStableDefaultsAndEventCategories()
    {
        var property = TypeDescriptor.GetProperties(typeof(MarkdownEditor))[nameof(MarkdownEditor.SynchronizeScrolling)];
        var defaultValue = Assert.IsType<DefaultValueAttribute>(property!.Attributes[typeof(DefaultValueAttribute)]);
        Assert.Equal(true, defaultValue.Value);

        var events = TypeDescriptor.GetEvents(typeof(MarkdownEditor));
        Assert.Equal("Action", events[nameof(MarkdownEditor.InsertLinkRequested)]!.Category);
        Assert.Equal("Action", events[nameof(MarkdownEditor.InsertImageRequested)]!.Category);
        Assert.Equal("Action", events[nameof(MarkdownEditor.PreviewLinkClicked)]!.Category);
    }

    [Fact]
    public void AltGraphKAndReadOnlyCtrlKDoNotRaiseRequest()
    {
        using var editor = new MarkdownEditor();
        var requests = 0;
        editor.InsertLinkRequested += (_, _) => requests++;

        editor.EditorSurface.RaiseKeyDown(new KeyEventArgs(Keys.Control | Keys.Alt | Keys.AltGraph | Keys.K));
        editor.ReadOnly = true;
        editor.EditorSurface.RaiseKeyDown(new KeyEventArgs(Keys.Control | Keys.K));

        Assert.Equal(0, requests);
    }
}
