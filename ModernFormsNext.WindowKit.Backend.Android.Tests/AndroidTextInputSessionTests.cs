using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

[Collection(AndroidTextInputUiCollection.Name)]
public sealed class AndroidTextInputSessionTests
{
    [Fact]
    public void RetiredConnectionCannotReadOrEditEitherOldOrNewClient()
    {
        var first = new Client("first", 5, 5);
        var second = new Client("second", 6, 6);
        var oldSession = new AndroidTextInputSession(first);
        var newSession = new AndroidTextInputSession(second);
        Assert.True(oldSession.BeginBatch());
        oldSession.MonitorExtractedText(17, 200);
        Assert.True(oldSession.RequestCursorUpdates(3));

        oldSession.Revoke();

        Assert.Null(oldSession.GetState());
        Assert.Null(oldSession.GetBefore(20));
        Assert.Null(oldSession.GetSurrounding(10, 10));
        Assert.False(oldSession.Edit(client => client.CommitText("late")));
        Assert.False(oldSession.BeginBatch());
        Assert.False(oldSession.EndBatch());
        Assert.False(oldSession.RequestCursorUpdates(3));
        Assert.Null(oldSession.ExtractedTextToken);
        Assert.False(oldSession.MonitorCursor);
        Assert.False(oldSession.CursorUpdatePending);
        Assert.Equal(0, oldSession.BatchDepth);
        Assert.Equal(0, first.Edits);
        Assert.Equal(0, second.Edits);
        Assert.True(newSession.Edit(client => client.CommitText("current")));
        Assert.Equal(1, second.Edits);
    }

    [Fact]
    public void RevocationDuringSnapshotDoesNotReturnAStaleResultOrRunEdit()
    {
        var client = new Client("text", 2, 2);
        var session = new AndroidTextInputSession(client);
        client.OnRead = session.Revoke;

        Assert.Null(session.GetState());
        Assert.False(session.Edit(target => target.CommitText("late")));
        Assert.Equal(0, client.Edits);
    }

    [Fact]
    public void SurroundingTextIsBoundedAndKeepsAbsoluteOriginAndReverseSelection()
    {
        var client = new Client(new string('x', 200_000), 100_002, 100_000);
        var session = new AndroidTextInputSession(client);

        var state = Assert.IsType<TextInputState>(session.GetSurrounding(int.MaxValue, int.MaxValue));

        Assert.Equal(TextInputState.MaximumTextLength, state.Text.Length);
        Assert.True(state.TextStart > 0);
        Assert.Equal(200_000, state.DocumentLength);
        Assert.Equal(100_002, state.SelectionStart);
        Assert.Equal(100_000, state.SelectionEnd);
        Assert.InRange(client.LargestRead, 0, TextInputState.MaximumTextLength);
        Assert.Equal("xx", session.GetSelected());
    }

    [Fact]
    public void OversizedSelectionIsUnavailableRatherThanMisreportedAsClipped()
    {
        var client = new Client(new string('x', 80_000), 0, 70_000);
        var session = new AndroidTextInputSession(client);

        Assert.Null(session.GetSelected());
        Assert.Null(session.GetSurrounding(10, 10));
        Assert.Null(session.GetExtracted(4096));
        Assert.InRange(client.LargestRead, 0, 4096);
    }

    [Fact]
    public void MaximumIntegerQueriesDoNotOverflowAndNegativeRequestsFail()
    {
        var session = new AndroidTextInputSession(new Client("A😀BC", 3, 3));

        Assert.Equal("A😀", session.GetBefore(int.MaxValue));
        Assert.Equal("BC", session.GetAfter(int.MaxValue));
        Assert.Equal(string.Empty, session.GetBefore(0));
        Assert.Equal(string.Empty, session.GetAfter(0));
        Assert.Null(session.GetBefore(-1));
        Assert.Null(session.GetAfter(-1));
        Assert.Null(session.GetSurrounding(-1, 0));
        Assert.Equal("bc", new AndroidTextInputState("abc", 1, 1).GetTextAfterCursor(int.MaxValue));
    }

    [Fact]
    public void OneUnitQueriesDoNotSplitSupplementaryCharacters()
    {
        var before = new AndroidTextInputSession(new Client("A😀B", 3, 3));
        var after = new AndroidTextInputSession(new Client("A😀B", 1, 1));

        Assert.Equal(string.Empty, before.GetBefore(1));
        Assert.Equal(string.Empty, after.GetAfter(1));
        Assert.Equal("😀", before.GetBefore(2));
        Assert.Equal("😀", after.GetAfter(2));
    }

    [Fact]
    public void ExtractedHintUsesBoundedWindowContainingSelection()
    {
        var session = new AndroidTextInputSession(new Client(new string('x', 200_000), 100_000, 100_000));

        var extracted = Assert.IsType<TextInputState>(session.GetExtracted(64));

        Assert.Equal(64, extracted.Text.Length);
        Assert.Equal(99_968, extracted.TextStart);
        Assert.Equal(100_000, extracted.SelectionStart);
        session.MonitorExtractedText(81, int.MaxValue);
        Assert.Equal(81, session.ExtractedTextToken);
        Assert.Equal(TextInputState.MaximumTextLength, session.ExtractedTextLimit);
    }

    [Fact]
    public void ChangedRevisionBetweenMetadataAndTextQueryReturnsNoMixedSnapshot()
    {
        var client = new Client("abcdef", 3, 3);
        var session = new AndroidTextInputSession(client);
        var reads = 0;
        client.OnRead = () => { if (++reads == 2) client.Revision++; };

        Assert.Null(session.GetBefore(3));
    }

    [Fact]
    public void BatchNestingIsBoundedAndCursorFiltersAreExplicit()
    {
        var session = new AndroidTextInputSession(new Client("a", 1, 1));
        for (var count = 0; count < 256; count++) Assert.True(session.BeginBatch());
        Assert.False(session.BeginBatch());
        for (var count = 255; count > 0; count--) Assert.True(session.EndBatch());
        Assert.False(session.EndBatch());
        Assert.Equal(0, session.BatchDepth);
        Assert.False(session.EndBatch());
        Assert.True(session.RequestCursorUpdates(3 | 16));
        Assert.True(session.MonitorCursor);
        Assert.True(session.CursorUpdatePending);
        session.MarkCursorPublished();
        Assert.False(session.CursorUpdatePending);
        Assert.True(session.MonitorCursor);
        Assert.False(session.RequestCursorUpdates(8));
        Assert.True(session.RequestCursorUpdates(0));
        Assert.False(session.MonitorCursor);
    }

    [Fact]
    public void ExtractedMonitoringDoesNotRepeatForGeometryOnlyChanges()
    {
        var session = new AndroidTextInputSession(new Client("text", 2, 2));
        session.MonitorExtractedText(11, 4096);
        Assert.True(session.ShouldPublishExtractedText(5));
        session.MarkExtractedTextPublished(5);
        Assert.False(session.ShouldPublishExtractedText(5));
        Assert.True(session.ShouldPublishExtractedText(6));
        session.MonitorExtractedText(12, 4096);
        Assert.True(session.ShouldPublishExtractedText(5));
        session.Revoke();
        Assert.False(session.ShouldPublishExtractedText(6));
    }

    [Fact]
    public void SelectAllUsesDocumentLengthWithoutReadingTheSurroundingText()
    {
        var client = new Client(new string('x', 200_000), 100_002, 100_000);
        var session = new AndroidTextInputSession(client);

        Assert.True(session.SelectAll());

        Assert.Equal((0, 200_000), client.LastSelection);
        Assert.Equal(0, client.LargestRead);
        Assert.Equal(1, client.Edits);
    }

    [Fact]
    public void SelectAllCannotEditAfterRevocationDuringMetadataRead()
    {
        var client = new Client("text", 2, 2);
        var session = new AndroidTextInputSession(client);
        client.OnRead = session.Revoke;

        Assert.False(session.SelectAll());
        Assert.False(session.SelectAll());
        Assert.Equal(0, client.Edits);
        Assert.Null(client.LastSelection);
    }

    [Fact]
    public void SelectAllUsesTheCapturedRealEditorAndCannotRetargetAfterFocusChanges()
    {
        using var root = new Panel();
        var first = root.Controls.Add(new TextBox { Text = "A😀BC" });
        var second = root.Controls.Add(new TextBox { Text = "other" });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(480, 160);
        first.Select();
        var client = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        var session = new AndroidTextInputSession(client);

        Assert.True(session.SelectAll());
        Assert.Equal((0, 5), (client.GetState()!.SelectionStart, client.GetState()!.SelectionEnd));
        Assert.Equal("A😀BC", first.Text);

        second.Select();
        var current = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        Assert.True(current.SetSelection(2, 2));
        Assert.False(session.SelectAll());
        Assert.Equal((2, 2), (current.GetState()!.SelectionStart, current.GetState()!.SelectionEnd));
        Assert.Equal("other", second.Text);
    }

    private sealed class Client(string text, int selectionStart, int selectionEnd) : ITextInputClient
    {
        public Action? OnRead { get; set; }
        public int LargestRead { get; private set; }
        public long Revision { get; set; }
        public int Edits { get; private set; }
        public (int Start, int End)? LastSelection { get; private set; }
        public event EventHandler? StateChanged { add { } remove { } }
        public event EventHandler<TextCompositionEventArgs>? CompositionChanged { add { } remove { } }

        public TextInputState? GetState(int maximumTextLength = 4096, int? textStart = null)
        {
            Assert.InRange(maximumTextLength, 0, TextInputState.MaximumTextLength);
            LargestRead = Math.Max(LargestRead, maximumTextLength);
            OnRead?.Invoke();
            var start = textStart ?? Math.Max(0, Math.Min(selectionStart, selectionEnd) - maximumTextLength / 2);
            start = Math.Clamp(start, 0, text.Length);
            var end = start + Math.Min(maximumTextLength, text.Length - start);
            if (start < text.Length && char.IsLowSurrogate(text[start])) start++;
            if (end > start && char.IsHighSurrogate(text[end - 1])) end--;
            end = Math.Max(start, end);
            return new TextInputState(text[start..end], start, text.Length, selectionStart, selectionEnd,
                -1, -1, Revision, new Rect(4, 5, 1, 20), new TextInputOptions(), caretBaseline: 21);
        }
        public bool CommitText(string value, int newCursorPosition = 1) { Edits++; return true; }
        public bool SetComposingText(string value, int newCursorPosition = 1) { Edits++; return true; }
        public bool SetComposingRegion(int start, int end) { Edits++; return true; }
        public bool SetSelection(int start, int end) { Edits++; LastSelection = (start, end); return true; }
        public bool FinishComposition() { Edits++; return true; }
        public bool CancelComposition() { Edits++; return true; }
        public bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false) { Edits++; return true; }
        public bool PerformEditorAction(TextInputAction action) { Edits++; return true; }
    }
}
