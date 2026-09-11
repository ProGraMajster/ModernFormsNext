using ModernFormsNext.WindowKit.Input;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TextInputClientTests
{
    [Fact]
    public void CompositionUsesExistingKeyPressAndPublishesFinalMetadata()
    {
        using var fixture = new EditorFixture(new TextBox());
        var stages = new List<TextCompositionStage>();
        int keyPresses = 0;
        fixture.Editor.KeyPress += (_, _) => keyPresses++;
        fixture.Editor.TextCompositionChanged += (_, args) => {
            var state = fixture.Client.GetState()!;
            Assert.Equal(state.Revision, args.Revision);
            Assert.Equal((state.CompositionStart, state.CompositionEnd), (args.Start, args.End));
            stages.Add(args.Stage);
        };

        Assert.True(fixture.Client.SetComposingText("zh"));
        Assert.True(fixture.Client.SetComposingText("中"));
        Assert.True(fixture.Client.CommitText("中文😀"));

        Assert.Equal("中文😀", fixture.Editor.Text);
        Assert.Equal(3, keyPresses);
        Assert.Equal(new[] { TextCompositionStage.Started, TextCompositionStage.Updated, TextCompositionStage.Committed }, stages);
        Assert.False(fixture.Client.GetState()!.HasComposition);
    }

    [Theory]
    [InlineData("plain", "\n")]
    [InlineData("plain", "\r\n")]
    [InlineData("plain", "\nż😀中")]
    [InlineData("plain", "\r\nż😀中")]
    [InlineData("rich", "\n")]
    [InlineData("rich", "\r\n")]
    [InlineData("rich", "\nż😀中")]
    [InlineData("rich", "\r\nż😀中")]
    [InlineData("markdown", "\n")]
    [InlineData("markdown", "\r\n")]
    [InlineData("markdown", "\nż😀中")]
    [InlineData("markdown", "\r\nż😀中")]
    public void NativeNewlinePayloadUsesTheExistingMultilineEditorForCommitCompositionAndCancel(string kind, string payload)
    {
        using var root = CreateNewlineEditor(kind, out var editor);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(640, 320);
        editor.Select();
        var client = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        var observedText = new List<string>();
        editor.KeyPress += (_, args) => observedText.Add(args.Text);
        string expected = "A" + payload + "Z";

        Assert.True(client.SetSelection(4, 1));
        Assert.True(client.SetComposingText(payload));
        Assert.Equal(expected, editor.Text);
        var composing = client.GetState()!;
        Assert.Equal((1, 1 + payload.Length), (composing.CompositionStart, composing.CompositionEnd));
        Assert.Equal(1 + payload.Length, composing.SelectionEnd);

        Assert.True(client.CancelComposition());
        Assert.Equal("AoldZ", editor.Text);
        Assert.Equal((4, 1), (client.GetState()!.SelectionStart, client.GetState()!.SelectionEnd));
        Assert.False(client.GetState()!.HasComposition);
        if (root is MarkdownEditor canceledMarkdown) {
            Assert.Equal("AoldZ", canceledMarkdown.Markdown);
            Assert.False(canceledMarkdown.CanUndo);
            Assert.False(canceledMarkdown.Modified);
        }

        Assert.True(client.CommitText(payload));
        Assert.Equal(expected, editor.Text);
        Assert.Equal(1 + payload.Length, client.GetState()!.SelectionEnd);
        Assert.False(client.GetState()!.HasComposition);
        Assert.Equal(new[] { payload, payload }, observedText);
        if (root is MarkdownEditor committedMarkdown) {
            Assert.Equal(expected, committedMarkdown.Markdown);
            committedMarkdown.Undo();
            Assert.Equal("AoldZ", committedMarkdown.Markdown);
            Assert.False(committedMarkdown.CanUndo);
            committedMarkdown.Redo();
            Assert.Equal(expected, committedMarkdown.Markdown);
        }
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("rich")]
    [InlineData("markdown")]
    public void NativeStandaloneCarriageReturnKeepsTheExistingSingleLineBreakConvention(string kind)
    {
        using var root = CreateNewlineEditor(kind, out var editor);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(640, 320);
        editor.Select();
        var client = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        Assert.True(client.SetSelection(4, 1));

        Assert.True(client.CommitText("\r"));

        Assert.Equal("A\nZ", editor.Text);
        Assert.Equal(2, client.GetState()!.SelectionEnd);
    }

    [Theory]
    [InlineData("plain", "\nż😀")]
    [InlineData("plain", "\r\nż😀")]
    [InlineData("rich", "\nż😀")]
    [InlineData("rich", "\r\nż😀")]
    public void SingleLineNativeNewlinePrefixKeepsPrintableTextThroughTheExistingDocumentFilter(string kind, string payload)
    {
        using var root = CreateNewlineEditor(kind, out var editor);
        editor.MultiLine = false;
        using var surface = new SkiaControlSurface(root);
        surface.Resize(640, 320);
        editor.Select();
        var client = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        var observedText = new List<string>();
        editor.KeyPress += (_, args) => observedText.Add(args.Text);
        Assert.True(client.SetSelection(4, 1));

        Assert.True(client.SetComposingText(payload));
        Assert.Equal("Aż😀Z", editor.Text);
        Assert.Equal((1, 4), (client.GetState()!.CompositionStart, client.GetState()!.CompositionEnd));
        Assert.True(client.CancelComposition());
        Assert.Equal("AoldZ", editor.Text);
        Assert.Equal((4, 1), (client.GetState()!.SelectionStart, client.GetState()!.SelectionEnd));

        Assert.True(client.CommitText(payload));
        Assert.Equal("Aż😀Z", editor.Text);
        Assert.Equal(4, client.GetState()!.SelectionEnd);
        Assert.Equal(new[] { payload, payload }, observedText);
    }

    [Theory]
    [InlineData("plain", "\n")]
    [InlineData("plain", "\r\n")]
    [InlineData("rich", "\n")]
    [InlineData("rich", "\r\n")]
    [InlineData("markdown", "\n")]
    [InlineData("markdown", "\r\n")]
    [InlineData("markdown", "\nż😀中")]
    public void NativeNewlineRespectsSingleLineAndMarkdownAcceptsReturnPolicies(string kind, string payload)
    {
        using var root = CreateNewlineEditor(kind, out var editor);
        if (root is MarkdownEditor markdown)
            markdown.AcceptsReturn = false;
        else
            editor.MultiLine = false;
        using var surface = new SkiaControlSurface(root);
        surface.Resize(640, 320);
        editor.Select();
        var client = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        Assert.True(client.SetSelection(4, 1));
        var observedText = new List<string>();
        editor.KeyPress += (_, args) => observedText.Add(args.Text);

        Assert.True(client.CommitText(payload));
        Assert.Equal("AoldZ", editor.Text);
        Assert.True(client.SetSelection(5, 5));
        Assert.True(client.SetComposingText(payload));
        Assert.Equal("AoldZ", editor.Text);
        Assert.True(client.CancelComposition());
        Assert.Equal("AoldZ", editor.Text);
        Assert.Equal(new[] { payload, payload }, observedText);
    }

    [Fact]
    public void CancelRestoresOriginalFragmentAndReverseSelectionButFinishKeepsText()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "AselectedZ" });
        fixture.Client.SetSelection(9, 1);
        fixture.Client.SetComposingText("試験");
        fixture.Client.SetComposingText("試験😀", 0);
        Assert.True(fixture.Client.CancelComposition());
        Assert.Equal("AselectedZ", fixture.Editor.Text);
        Assert.Equal((9, 1), (fixture.Client.GetState()!.SelectionStart, fixture.Client.GetState()!.SelectionEnd));

        fixture.Client.SetComposingText("keep");
        Assert.True(fixture.Client.FinishComposition());
        Assert.Equal("AkeepZ", fixture.Editor.Text);
        Assert.False(fixture.Client.GetState()!.HasComposition);
    }

    [Theory]
    [InlineData(1, "😀", "")]
    [InlineData(2, "😀x", "😀")]
    [InlineData(3, "A😀B", "A😀")]
    public void MaximumLengthNeverSplitsCommittedScalar(int limit, string text, string expected)
    {
        using var fixture = new EditorFixture(new TextBox { MaxLength = limit });
        Assert.True(fixture.Client.CommitText(text));
        Assert.Equal(expected, fixture.Editor.Text);
        Assert.Equal(expected.Length, fixture.Client.GetState()!.SelectionEnd);
    }

    [Fact]
    public void LoweredMaximumLengthDoesNotThrowOnNextInput()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "existing", MaxLength = 2 });
        fixture.Client.SetSelection(8, 8);
        fixture.Client.CommitText("😀");
        Assert.Equal("existing", fixture.Editor.Text);
    }

    [Fact]
    public void BoundedSnapshotKeepsAbsoluteRangesAndNeverSplitsSurrogates()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "AB😀CD中" });
        fixture.Client.SetSelection(7, 0);
        var state = fixture.Client.GetState(2, 3)!;
        Assert.Equal("😀", state.Text);
        Assert.Equal(2, state.TextStart);
        Assert.Equal(7, state.DocumentLength);
        Assert.Equal((7, 0), (state.SelectionStart, state.SelectionEnd));
        Assert.Empty(fixture.Client.GetState(1, 2)!.Text);
        Assert.Empty(fixture.Client.GetState(0)!.Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Client.GetState(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Client.GetState(65537));
    }

    [Fact]
    public void SelectionInsideSurrogateNormalizesBeforeDeletion()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "A😀B" });
        fixture.Client.SetSelection(1, 2);
        fixture.Client.CommitText("");
        Assert.Equal("AB", fixture.Editor.Text);
        Assert.Throws<ArgumentException>(() => fixture.Client.SetComposingText("\ud800"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SurroundingDeletionPreservesSelectionAndCompleteTextElements(bool codePoints)
    {
        using var fixture = new EditorFixture(new TextBox { Text = "A😀XYZn\u0301B" });
        fixture.Client.SetSelection(6, 3);
        Assert.True(fixture.Client.DeleteSurroundingText(1, 1, codePoints));
        Assert.Equal("AXYZB", fixture.Editor.Text);
        Assert.Equal((4, 1), (fixture.Client.GetState()!.SelectionStart, fixture.Client.GetState()!.SelectionEnd));
    }

    [Fact]
    public void ProgrammaticReplacementInsideTextChangedRevokesOldCheckpoint()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "old" });
        fixture.Client.SetSelection(3, 3);
        bool replaced = false;
        fixture.Editor.TextChanged += (_, _) => {
            if (replaced) return;
            replaced = true;
            fixture.Editor.Text = "";
        };
        Assert.False(fixture.Client.SetComposingText("new"));
        Assert.Equal("", fixture.Editor.Text);
        Assert.True(fixture.Client.CancelComposition());
        Assert.Equal("", fixture.Editor.Text);
    }

    [Fact]
    public void KeyPressMutationPreventsSubsequentInsertion()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "original" });
        fixture.Editor.KeyPress += (_, _) => fixture.Editor.Text = "application";
        Assert.False(fixture.Client.CommitText("stale"));
        Assert.Equal("application", fixture.Editor.Text);
    }

    [Fact]
    public void NestedNativeEditReturnsFalseAndDoesNotAppendText()
    {
        using var fixture = new EditorFixture(new TextBox());
        fixture.Editor.KeyPress += (_, _) => Assert.False(fixture.Client.CommitText("nested"));
        Assert.True(fixture.Client.CommitText("outer"));
        Assert.Equal("outer", fixture.Editor.Text);
    }

    [Fact]
    public void FocusRetirementInsideKeyPressStopsTheOldOperation()
    {
        using var fixture = new EditorFixture(new TextBox());
        var next = new TextBox();
        fixture.Root.Controls.Add(next);
        fixture.Editor.KeyPress += (_, _) => next.Select();
        Assert.False(fixture.Client.SetComposingText("stale"));
        Assert.Empty(fixture.Editor.Text);
        Assert.Empty(next.Text);
        Assert.False(fixture.Editor.document.HasComposition);
    }

    [Fact]
    public void FailureFinishesMarkersAndAllowsSubsequentInput()
    {
        using var fixture = new EditorFixture(new TextBox());
        fixture.Client.SetComposingText("accepted");
        var failure = new InvalidOperationException("observer");
        EventHandler<KeyPressEventArgs> handler = (_, _) => throw failure;
        fixture.Editor.KeyPress += handler;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => fixture.Client.SetComposingText("failed")));
        fixture.Editor.KeyPress -= handler;
        Assert.False(fixture.Editor.document.HasComposition);
        fixture.Client.CommitText("after");
        Assert.Equal("after", fixture.Editor.Text);
    }

    [Fact]
    public void RetainedAdapterCannotUseDisposedEditor()
    {
        using var fixture = new EditorFixture(new TextBox());
        var retained = fixture.Client;
        fixture.Editor.Dispose();
        Assert.Null(retained.GetState());
        Assert.False(retained.CommitText("late"));
    }

    [Fact]
    public void OptionsReflectControlStateAndPasswordOverridesPrediction()
    {
        using var fixture = new EditorFixture(new TextBox());
        int notifications = 0;
        fixture.Client.StateChanged += (_, _) => notifications++;
        fixture.Editor.TextInputOptions = new() { Scope = TextInputScope.Email, Capitalization = TextInputCapitalization.Words };
        fixture.Editor.MultiLine = true;
        fixture.Editor.PasswordCharacter = '*';
        fixture.Editor.ReadOnly = true;
        var options = fixture.Client.GetState()!.Options;
        Assert.True(options.ReadOnly);
        Assert.True(options.MultiLine);
        Assert.Equal(TextInputScope.Password, options.Scope);
        Assert.False(options.AutoCorrect);
        Assert.Equal(TextInputCapitalization.None, options.Capitalization);
        Assert.False(fixture.Client.CommitText("blocked"));
        Assert.True(fixture.Client.SetSelection(0, 0));
        Assert.True(notifications > 0);
    }

    [Fact]
    public void RichCancelRestoresMixedFormattingAndOriginalInsertionStyle()
    {
        var rich = new RichTextBox { Text = "ABCD" };
        rich.Select(1, 1);
        rich.SelectionColor = SKColors.Red;
        rich.Select(2, 1);
        rich.SelectionColor = SKColors.Blue;
        using var fixture = new EditorFixture(rich);
        fixture.Client.SetSelection(1, 3);
        fixture.Client.SetComposingText("replacement");
        fixture.Client.SetComposingText("中");
        Assert.True(fixture.Client.CancelComposition());
        Assert.Equal("ABCD", rich.Text);
        rich.Select(1, 1);
        Assert.Equal(SKColors.Red, rich.SelectionColor);
        rich.Select(2, 1);
        Assert.Equal(SKColors.Blue, rich.SelectionColor);
    }

    [Fact]
    public void RichCaretUsesStyledRenderedBlockAndSelectionNotifies()
    {
        var rich = new RichTextBox { Text = "abc\nDEF", Width = 400, Height = 160 };
        rich.SelectAll();
        rich.SelectionFont = new Font("Segoe UI", 28, FontStyle.Bold);
        using var fixture = new EditorFixture(rich);
        int selectionEvents = 0;
        rich.SelectionChanged += (_, _) => selectionEvents++;
        fixture.Client.SetSelection(6, 6);
        var block = rich.GetRichTextBlock();
        var expected = TextMeasurer.GetCursorLocation(block, rich.GetTextOrigin(block),
            rich.document.CursorLayoutCodePointIndex, rich.CurrentFontSize);
        var actual = fixture.Client.GetState()!.CaretRectangle;
        double scale = rich.DeviceDpi / (double)DpiHelper.LogicalDpi;
        Assert.Equal(expected.X / scale, actual.X);
        Assert.Equal(expected.Y / scale, actual.Y);
        Assert.Equal(Math.Max(1, expected.Height) / scale, actual.Height);
        Assert.True(selectionEvents > 0);
        var caret = block.GetCaretInfo(new Topten.RichTextKit.CaretPosition(rich.document.CursorLayoutCodePointIndex));
        var line = block.Lines[caret.LineIndex];
        Assert.Equal((rich.GetTextOrigin(block).Y + line.YCoord + line.BaseLine) / scale,
            fixture.Client.GetState()!.CaretBaseline);
    }

    [Fact]
    public void FirstClientAccessAndOptionsUseTheEditorsCreatingThread()
    {
        using var editor = new TextBox();
        Exception? clientFailure = null, optionFailure = null;
        var thread = new Thread(() => {
            clientFailure = Record.Exception(() => editor.QueryTextInputClient());
            optionFailure = Record.Exception(() => { editor.TextInputOptions = new(); });
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(clientFailure);
        Assert.IsType<InvalidOperationException>(optionFailure);
        Assert.NotNull(editor.QueryTextInputClient());
    }

    [Fact]
    public void ThrowingStateObserverDoesNotSkipLaterObserversOrLeaveComposition()
    {
        using var fixture = new EditorFixture(new TextBox());
        var failure = new InvalidOperationException("state observer");
        int following = 0;
        EventHandler throwing = (_, _) => throw failure;
        fixture.Client.StateChanged += throwing;
        fixture.Client.StateChanged += (_, _) => following++;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => fixture.Client.SetComposingText("accepted")));
        Assert.Equal(1, following);
        Assert.False(fixture.Editor.document.HasComposition);
        fixture.Client.StateChanged -= throwing;
        fixture.Client.CancelComposition();
        Assert.Equal("accepted", fixture.Editor.Text);
        fixture.Client.CommitText("!");
        Assert.Equal("accepted!", fixture.Editor.Text);
    }

    [Fact]
    public void ReentrantStateChangeIsReportedAfterTheCurrentNotification()
    {
        using var fixture = new EditorFixture(new TextBox());
        int depth = 0, maximumDepth = 0;
        var observed = new List<string>();
        fixture.Client.StateChanged += (_, _) => {
            depth++;
            maximumDepth = Math.Max(maximumDepth, depth);
            observed.Add(fixture.Client.GetState()!.Text);
            if (fixture.Editor.Text == "first") fixture.Editor.Text = "second";
            depth--;
        };
        fixture.Client.CommitText("first");
        Assert.Equal(new[] { "first", "second" }, observed);
        Assert.Equal(1, maximumDepth);
    }

    [Fact]
    public void CancelNormalizesAnExistingPublicSelectionInsideASurrogate()
    {
        using var fixture = new EditorFixture(new TextBox { Text = "A😀Z" });
        fixture.Editor.SelectionStart = 1;
        fixture.Editor.SelectionEnd = 2;
        fixture.Client.SetComposingText("X");
        fixture.Client.CancelComposition();
        Assert.Equal("A😀Z", fixture.Editor.Text);
        Assert.Equal((1, 3), (fixture.Client.GetState()!.SelectionStart, fixture.Client.GetState()!.SelectionEnd));
    }

    [Fact]
    public void RichReplacementAndCancelPreserveFormattingOutsideTheComposition()
    {
        var rich = new RichTextBox { Text = "prefix-MIDDLE-suffix" };
        rich.Select(14, 6);
        rich.SelectionColor = SKColors.Green;
        using var fixture = new EditorFixture(rich);
        fixture.Client.SetSelection(7, 13);
        fixture.Client.SetComposingText("短");
        rich.Select(9, 6);
        Assert.Equal(SKColors.Green, rich.SelectionColor);
        // Selecting through the ordinary control API accepts the old composition. Start another
        // region so rollback can verify suffix runs on both shrink and expansion paths.
        fixture.Client.SetSelection(7, 8);
        fixture.Client.SetComposingText("much-longer");
        fixture.Client.CancelComposition();
        Assert.Equal("prefix-短-suffix", rich.Text);
        rich.Select(9, 6);
        Assert.Equal(SKColors.Green, rich.SelectionColor);
    }

    [Fact]
    public void MarkdownProgrammaticReplacementDuringCompositionDoesNotReviveOldUndo()
    {
        using var markdown = new MarkdownEditor { Markdown = "old" };
        using var surface = new SkiaControlSurface(markdown);
        surface.Resize(600, 320);
        markdown.Select();
        var client = markdown.EditorSurface.QueryTextInputClient()!;
        client.SetComposingText("provisional");
        markdown.Markdown = "application";
        Assert.True(client.CancelComposition());
        Assert.Equal("application", markdown.Markdown);
        Assert.False(markdown.CanUndo);
        Assert.False(markdown.Modified);
    }

    [Fact]
    public void MarkdownCompositionUsesOneExistingHistoryRecordWithFinalCaret()
    {
        using var markdown = new MarkdownEditor { Markdown = "AB" };
        using var surface = new SkiaControlSurface(markdown);
        surface.Resize(600, 320);
        markdown.Select();
        var client = markdown.EditorSurface.QueryTextInputClient()!;
        client.SetSelection(1, 1);
        client.SetComposingText("試");
        client.SetComposingText("試験");
        client.CommitText("試験", 0);
        Assert.Equal("A試験B", markdown.Markdown);
        Assert.True(markdown.CanUndo);
        markdown.Undo();
        Assert.Equal("AB", markdown.Markdown);
        Assert.False(markdown.CanUndo);
        markdown.Redo();
        Assert.Equal("A試験B", markdown.Markdown);
        Assert.Equal(1, client.GetState()!.SelectionEnd);
    }

    [Fact]
    public void MarkdownCancelPreservesExistingRedoAndCleanState()
    {
        using var markdown = new MarkdownEditor { Markdown = "original" };
        using var surface = new SkiaControlSurface(markdown);
        surface.Resize(600, 320);
        markdown.Select();
        markdown.Select(8, 0);
        markdown.SelectedText = "!";
        markdown.Undo();
        Assert.True(markdown.CanRedo);
        var client = markdown.EditorSurface.QueryTextInputClient()!;
        client.SetSelection(0, 8);
        client.SetComposingText("temporary");
        Assert.True(client.CancelComposition());
        Assert.Equal("original", markdown.Markdown);
        Assert.True(markdown.CanRedo);
        Assert.False(markdown.Modified);
        markdown.Redo();
        Assert.Equal("original!", markdown.Markdown);
    }

    private static Control CreateNewlineEditor(string kind, out TextBox editor)
    {
        if (kind == "markdown") {
            var markdown = new MarkdownEditor { Markdown = "AoldZ" };
            editor = markdown.EditorSurface;
            return markdown;
        }

        editor = kind == "rich" ? new RichTextBox() : new TextBox { MultiLine = true };
        editor.Text = "AoldZ";
        return editor;
    }

    private sealed class EditorFixture : IDisposable
    {
        internal Panel Root { get; } = new();
        internal TextBox Editor { get; }
        internal ITextInputClient Client { get; }
        private readonly SkiaControlSurface surface;

        internal EditorFixture(TextBox editor)
        {
            Editor = editor;
            Root.Controls.Add(editor);
            surface = new SkiaControlSurface(Root);
            surface.Resize(640, 320);
            Editor.Select();
            Client = Editor.QueryTextInputClient()!;
        }

        public void Dispose()
        {
            surface.Dispose();
            Root.Dispose();
        }
    }
}
