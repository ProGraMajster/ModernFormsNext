using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class AccessibleTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullDocumentUsesActualTextAndNoInputSnapshotLimit(bool rich)
    {
        using var f = new Fixture(rich ? new RichTextBox() : new TextBox());
        f.Editor.Text = new string('a', 70000) + "😀\r\n";
        var range = f.Provider.DocumentRange;
        Assert.Equal(f.Editor.Text, range.GetText());
        Assert.Equal(70004, range.End);
        Assert.Equal(new string('a', 70000), range.GetText(70001));
        Assert.Throws<ArgumentOutOfRangeException>(() => range.GetText(-2));
        Assert.Empty(range.GetChildren());
        Assert.Same(f.Editor.AccessibilityObject, range.GetEnclosingElement());
    }

    [Fact]
    public void PlaceholderAndRichTrailingCaretMarkerAreNotDocumentText()
    {
        using var f = new Fixture(new RichTextBox());
        Assert.Empty(f.Provider.DocumentRange.GetText());
        f.Editor.Text = "line\n";
        Assert.Equal("line\n", f.Provider.DocumentRange.GetText());
        Assert.Equal(5, f.Provider.DocumentRange.End);
    }

    [Fact]
    public void RetainedRangesRebaseBoundariesAndCollapsedAnchorAcrossActualEdits()
    {
        using var f = new Fixture(new TextBox { Text = "abcd" });
        var retained = f.Provider.RangeFromOffsets(1, 3);
        var caret = f.Provider.RangeFromOffsets(1, 1);
        Assert.True(f.Client.SetSelection(1, 1));
        Assert.True(f.Client.CommitText("X"));
        Assert.Equal("Xbc", retained.GetText());
        Assert.Equal((2, 2), (caret.Start, caret.End));
        Assert.True(f.Client.SetSelection(0, 1));
        Assert.True(f.Client.CommitText(""));
        Assert.Equal("Xbc", retained.GetText());
        Assert.Equal((0, 3), (retained.Start, retained.End));
    }

    [Fact]
    public void JournalExpiryIsExplicitAndFreshRangesStillWork()
    {
        using var f = new Fixture(new TextBox());
        var retained = f.Provider.DocumentRange;
        for (int i = 0; i < 1025; i++) f.Editor.Text = i % 2 == 0 ? "x" : "y";
        Assert.Throws<InvalidOperationException>(() => retained.GetText());
        Assert.Equal(f.Editor.Text, f.Provider.DocumentRange.GetText());
    }

    [Fact]
    public void ReplacementPreservesTheFollowingBoundaryAndCollapsesDeletedInterior()
    {
        using var f = new Fixture(new TextBox { Text = "aOLDz" });
        var following = f.Provider.RangeFromOffsets(4, 5);
        var inside = f.Provider.RangeFromOffsets(2, 2);
        f.Editor.Text = "aNEWLONGz";
        Assert.Equal("z", following.GetText());
        Assert.Equal((8, 9), (following.Start, following.End));
        Assert.Equal((8, 8), (inside.Start, inside.End));
    }

    [Fact]
    public void CloneSearchEndpointComparisonAndCrossDocumentRejection()
    {
        using var f = new Fixture(new TextBox { Text = "one TWO one" });
        var range = f.Provider.DocumentRange;
        Assert.True(range.Compare(range.Clone()));
        Assert.Equal(8, range.FindText("one", backward: true)!.Start);
        Assert.Equal("TWO", range.FindText("two", ignoreCase: true)!.GetText());
        Assert.Null(range.FindText("absent"));
        Assert.Throws<ArgumentException>(() => range.FindText(""));
        var second = f.Provider.RangeFromOffsets(4, 7);
        range.MoveEndpointByRange(AccessibleTextEndpoint.Start, second, AccessibleTextEndpoint.Start);
        Assert.Equal("TWO one", range.GetText());
        Assert.True(range.CompareEndpoints(AccessibleTextEndpoint.End, second, AccessibleTextEndpoint.End) > 0);
        using var other = new Fixture(new TextBox());
        Assert.Throws<ArgumentException>(() => range.Compare(other.Provider.DocumentRange));
    }

    [Fact]
    public void GraphemeWordParagraphAndPageMovementUseDocumentBoundaries()
    {
        using var f = new Fixture(new TextBox { Text = "a😀e\u0301 word\r\nlast", MultiLine = true });
        var range = f.Provider.RangeFromOffsets(1, 1);
        Assert.Equal(1, range.Move(AccessibleTextUnit.Character, 1));
        Assert.Equal(3, range.Start);
        Assert.Equal(1, range.Move(AccessibleTextUnit.Character, 1));
        Assert.Equal(5, range.Start);
        range.ExpandToEnclosingUnit(AccessibleTextUnit.Paragraph);
        Assert.Equal("a😀e\u0301 word\r\n", range.GetText());
        range.ExpandToEnclosingUnit(AccessibleTextUnit.Page);
        Assert.Equal(f.Editor.Text, range.GetText());
    }

    [Fact]
    public void ReadOnlySupportsSelectionAndContiguousOperationsWithoutTextMutation()
    {
        using var f = new Fixture(new TextBox { Text = "0123456789", ReadOnly = true });
        Assert.True(f.Provider.RangeFromOffsets(2, 5).Select());
        Assert.True(f.Provider.RangeFromOffsets(4, 7).AddToSelection());
        Assert.Equal("23456", Assert.Single(f.Provider.GetSelection()).GetText());
        Assert.False(f.Provider.RangeFromOffsets(8, 9).AddToSelection());
        Assert.False(f.Provider.RangeFromOffsets(3, 5).RemoveFromSelection());
        Assert.True(f.Provider.RangeFromOffsets(2, 4).RemoveFromSelection());
        Assert.Equal("456", Assert.Single(f.Provider.GetSelection()).GetText());
        Assert.Equal("0123456789", f.Editor.Text);
        Assert.Equal(true, f.Provider.DocumentRange.GetAttributeValue(AccessibleTextAttribute.IsReadOnly));
        f.Editor.Enabled = false;
        Assert.False(f.Provider.DocumentRange.Select());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SensitiveTransitionRevokesExistingRangeAndRestoringPrivacyNeedsFreshRange(bool scope)
    {
        using var f = new Fixture(new TextBox { Text = "secret" });
        var range = f.Provider.DocumentRange;
        if (scope) f.Editor.TextInputOptions = new() { Scope = TextInputScope.Password };
        else f.Editor.PasswordCharacter = '*';
        Assert.True(f.Editor.AccessibilityObject.IsSensitive);
        Assert.Null(f.Editor.AccessibilityObject.TextProvider);
        Assert.Throws<UnauthorizedAccessException>(() => range.GetText());
        if (scope) f.Editor.TextInputOptions = new(); else f.Editor.PasswordCharacter = null;
        Assert.Throws<InvalidOperationException>(() => range.GetText());
        Assert.Equal("secret", f.Editor.AccessibilityObject.TextProvider!.DocumentRange.GetText());
    }

    [Fact]
    public void ReadsPreserveCompositionButExternalSelectionAcceptsVisiblePreedit()
    {
        using var f = new Fixture(new TextBox { Text = "old" });
        Assert.True(f.Client.SetSelection(0, 3));
        Assert.True(f.Client.SetComposingText("新"));
        Assert.Equal("新", f.Provider.DocumentRange.GetText());
        Assert.True(f.Client.GetState()!.HasComposition);
        Assert.True(f.Provider.RangeFromOffsets(0, 1).Select());
        Assert.False(f.Client.GetState()!.HasComposition);
        Assert.Equal("新", f.Editor.Text);
    }

    [Fact]
    public void RichAttributesFindMixedAndGeometryUseStyledLayout()
    {
        using var f = new Fixture(new RichTextBox { Text = "small BIG", MultiLine = true });
        var rich = (RichTextBox)f.Editor;
        rich.Select(6, 3);
        rich.SelectionFont = new Font("Segoe UI", 30, FontStyle.Bold);
        rich.SelectionColor = SKColors.Red;
        Assert.Same(AccessibleTextAttributeValues.Mixed, f.Provider.DocumentRange.GetAttributeValue(AccessibleTextAttribute.FontSize));
        var red = f.Provider.DocumentRange.FindAttribute(AccessibleTextAttribute.ForegroundColor, unchecked((int)(uint)SKColors.Red));
        Assert.NotNull(red);
        Assert.Equal("BIG", red.GetText());
        Assert.Equal(700, red.GetAttributeValue(AccessibleTextAttribute.FontWeight));
        Assert.NotEmpty(red.GetBoundingRectangles());
        var rectangle = red.GetBoundingRectangles()[0];
        var hit = f.Provider.RangeFromPoint(new PointF(rectangle.Left + 1, rectangle.Top + rectangle.Height / 2));
        Assert.InRange(hit.Start, red.Start, red.End);
    }

    [Fact]
    public void ScrollRangeLeavesSelectionUnchangedAndVisibleGeometryClips()
    {
        using var f = new Fixture(new TextBox { MultiLine = true, Text = string.Join("\n", Enumerable.Range(0, 30).Select(i => "line " + i)) });
        Assert.True(f.Provider.RangeFromOffsets(0, 0).Select());
        var selection = Assert.Single(f.Provider.GetSelection()).Clone();
        var end = f.Provider.RangeFromOffsets(f.Editor.Text.Length - 7, f.Editor.Text.Length);
        Assert.True(end.ScrollIntoView(false));
        Assert.True(selection.Compare(Assert.Single(f.Provider.GetSelection())));
        Assert.NotEmpty(end.GetBoundingRectangles());
        Assert.All(f.Provider.GetVisibleRanges(), r => Assert.NotEmpty(r.GetBoundingRectangles()));
    }

    [Fact]
    public void RangeRevealUsesExistingOuterViewportWithoutMovingFocusOrSelection()
    {
        using var root = new Panel();
        var viewport = root.Controls.Add(new ScrollableControl { Bounds = new(0, 0, 200, 120), AutoScroll = true });
        var editor = viewport.Controls.Add(new TextBox { Bounds = new(0, 240, 180, 100), MultiLine = true,
            Text = string.Join("\n", Enumerable.Range(0, 30).Select(i => "line " + i)) });
        var focus = root.Controls.Add(new Button { Bounds = new(240, 0, 100, 30) });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(400, 200);
        focus.Select();
        var provider = editor.AccessibilityObject.TextProvider!;
        var selection = Assert.Single(provider.GetSelection()).Clone();
        var end = provider.RangeFromOffsets(editor.Text.Length - 7, editor.Text.Length);
        Assert.Empty(end.GetBoundingRectangles());
        Assert.True(end.ScrollIntoView(false));
        Assert.True(viewport.VerticalScrollProperties.Value > 0);
        Assert.NotEmpty(end.GetBoundingRectangles());
        Assert.True(selection.Compare(Assert.Single(provider.GetSelection())));
        Assert.True(focus.Focused);
    }

    [Fact]
    public void RangeRevealStopsBeforeOuterScrollAfterInnerCallbackReparentsEditor()
    {
        using var root = new Panel();
        var viewport = root.Controls.Add(new ScrollableControl { Bounds = new(0, 0, 200, 120), AutoScroll = true });
        var editor = viewport.Controls.Add(new TextBox { Bounds = new(0, 240, 180, 100), MultiLine = true,
            Text = string.Join("\n", Enumerable.Range(0, 30).Select(i => "line " + i)) });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(400, 200);
        var provider = editor.AccessibilityObject.TextProvider!;
        var end = provider.RangeFromOffsets(editor.Text.Length - 7, editor.Text.Length);
        bool once = false;
        editor.Invalidated += (_, _) => {
            if (once) return;
            once = true;
            viewport.Controls.Remove(editor);
            viewport.Controls.Add(editor);
        };
        Assert.False(end.ScrollIntoView(false));
        Assert.True(once);
        Assert.Equal(0, viewport.VerticalScrollProperties.Value);
    }

    [Fact]
    public void SameContentReplacementPublishesCommittedMetadataAndSelection()
    {
        using var f = new Fixture(new TextBox { Text = "same" });
        var events = new List<AccessibleEvents>();
        f.Editor.AccessibilityObject.ClientNotification += (_, e) => {
            events.Add(e.EventId);
            if (e.EventId == AccessibleEvents.TextChanged) Assert.Equal("same", f.Provider.DocumentRange.GetText());
        };
        Assert.True(f.Client.SetSelection(0, 4));
        events.Clear();
        Assert.True(f.Client.CommitText("same"));
        Assert.Single(events, e => e == AccessibleEvents.TextChanged);
        Assert.Single(events, e => e == AccessibleEvents.TextSelectionChanged);
    }

    [Fact]
    public void DisposedDocumentRejectsRetainedRanges()
    {
        using var f = new Fixture(new TextBox { Text = "gone" });
        var range = f.Provider.DocumentRange;
        f.Editor.Dispose();
        Assert.Throws<ObjectDisposedException>(() => range.GetText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThemeChangeInvalidatesActualPlainAndStyledLayout(bool rich)
    {
        using var f = new Fixture(rich ? new RichTextBox { Text = "theme" } : new TextBox { Text = "theme" });
        var before = f.Editor.AccessibleTextLayout;
        var notifications = new List<AccessibleEvents>();
        f.Editor.AccessibilityObject.ClientNotification += (_, e) => notifications.Add(e.EventId);
        f.Editor.OnThemeChanged(EventArgs.Empty);
        Assert.NotSame(before, f.Editor.AccessibleTextLayout);
        Assert.Contains(AccessibleEvents.TextAttributesChanged, notifications);
        Assert.Equal("theme", f.Provider.DocumentRange.GetText());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ThemeSelectionUsesLiveDefaultsWithoutOverwritingExplicitColors(bool rich, bool authored)
    {
        using var f = new Fixture(rich ? new RichTextBox { Text = "theme" } : new TextBox { Text = "theme" });
        f.Editor.SelectAll();
        var original = Theme.TextSelectionBackgroundColor;
        if (authored) f.Editor.document.SelectionColor = original;
        try {
            Theme.TextSelectionBackgroundColor = SKColors.Yellow;
            f.Editor.OnThemeChanged(EventArgs.Empty);
            Assert.Equal(authored ? original : SKColors.Yellow, f.Editor.document.GetTextSelection().Color);
            Assert.Equal("theme", Assert.Single(f.Provider.GetSelection()).GetText());
        }
        finally { Theme.TextSelectionBackgroundColor = original; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThemePlaceholderColorUpdatesTheActualPrepaintLayoutUnlessAuthored(bool authored)
    {
        using var f = new Fixture(new TextBox { Placeholder = "placeholder" });
        var original = Theme.ForegroundDisabledColor;
        if (authored) f.Editor.document.PlaceholderFontColor = original;
        _ = f.Editor.AccessibleTextLayout.MeasuredWidth;
        try {
            Theme.ForegroundDisabledColor = SKColors.Cyan;
            f.Editor.OnThemeChanged(EventArgs.Empty);
            var block = f.Editor.AccessibleTextLayout;
            _ = block.MeasuredWidth;
            Assert.Equal(authored ? original : SKColors.Cyan, block.FontRuns[0].Style.TextColor);
            Assert.Equal("", f.Provider.DocumentRange.GetText());
        }
        finally { Theme.ForegroundDisabledColor = original; }
    }

    [Fact]
    public void CompositionFinishCallbackCannotRetargetAccessibleSelectionAfterDisposal()
    {
        using var f = new Fixture(new TextBox { Text = "old" });
        Assert.True(f.Client.SetComposingText("visible"));
        f.Editor.TextCompositionChanged += (_, e) => {
            if (e.Stage == TextCompositionStage.Finished) f.Editor.Dispose();
        };
        Assert.False(f.Provider.SetSelection(0, 1));
        Assert.True(f.Editor.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositionFinishProtectingAncestorStopsLaterAccessibleSelection(bool rich)
    {
        using var parent = new SensitivePanel();
        TextBox editor = parent.Controls.Add(rich ? new RichTextBox { Text = "old" } : new TextBox { Text = "old" });
        using var surface = new SkiaControlSurface(parent);
        surface.Resize(400, 150);
        editor.Select();
        var client = surface.TextInputClient!;
        var provider = editor.AccessibilityObject.TextProvider!;
        Assert.True(client.SetComposingText("visible"));
        string text = editor.Text;
        var selection = (editor.document.SelectionStart, editor.document.SelectionEnd, editor.document.CursorIndex);
        int finished = 0;
        editor.TextCompositionChanged += (_, e) => {
            if (e.Stage == TextCompositionStage.Finished) { finished++; parent.Protected = true; }
        };
        Assert.False(provider.SetSelection(0, 1));
        Assert.Equal(1, finished);
        Assert.False(client.GetState()!.HasComposition);
        Assert.Equal(text, editor.Text);
        Assert.Equal(selection, (editor.document.SelectionStart, editor.document.SelectionEnd, editor.document.CursorIndex));
        Assert.Null(editor.AccessibilityObject.TextProvider);
    }

    [Fact]
    public void MarkdownTextProviderUsesTheSourceDocumentAndExistingHistory()
    {
        using var editor = new MarkdownEditor { Markdown = "# source" };
        using var surface = new SkiaControlSurface(editor);
        surface.Resize(600, 300);
        var source = editor.EditorSurface;
        var provider = Assert.IsAssignableFrom<AccessibleTextProvider>(source.AccessibilityObject.TextProvider);
        Assert.Equal("# source", provider.DocumentRange.GetText());
        Assert.True(provider.SetSelection(2, 8));
        Assert.Equal("source", Assert.Single(provider.GetSelection()).GetText());
        Assert.False(editor.CanUndo);
        Assert.Equal("# source", editor.Markdown);
    }

    [Fact]
    public void EmptyAndFullyClippedTextExposeDegenerateVisibleRangeWithoutRectangles()
    {
        using var f = new Fixture(new TextBox());
        var empty = Assert.Single(f.Provider.GetVisibleRanges());
        Assert.Equal(empty.Start, empty.End);
        Assert.Empty(empty.GetBoundingRectangles());
        foreach (var unit in Enum.GetValues<AccessibleTextUnit>()) {
            empty.ExpandToEnclosingUnit(unit);
            Assert.Equal(0, empty.Move(unit, 1));
            Assert.Equal(0, empty.MoveEndpointByUnit(AccessibleTextEndpoint.End, unit, -1));
        }
        f.Editor.Text = "outside";
        f.Editor.Top = 500;
        var clipped = Assert.Single(f.Provider.GetVisibleRanges());
        Assert.Equal(clipped.Start, clipped.End);
        Assert.Empty(f.Provider.DocumentRange.GetBoundingRectangles());
    }

    [Fact]
    public void WrappedBidirectionalRangesStayWithinTheActualViewport()
    {
        using var f = new Fixture(new RichTextBox { Text = "abc שלום def\nمرحبا world\nthird", MultiLine = true });
        f.Editor.Width = 100;
        var range = f.Provider.DocumentRange;
        var rectangles = range.GetBoundingRectangles();
        Assert.NotEmpty(rectangles);
        var bounds = f.Editor.AccessibilityObject.Bounds;
        Assert.All(rectangles, r => {
            Assert.True(r.Width > 0 && r.Height > 0);
            Assert.True(r.Left >= bounds.Left && r.Right <= bounds.Right);
            Assert.True(r.Top >= bounds.Top && r.Bottom <= bounds.Bottom);
        });
        foreach (var visible in f.Provider.GetVisibleRanges()) {
            Assert.InRange(visible.Start, 0, f.Editor.Text.Length);
            Assert.InRange(visible.End, visible.Start, f.Editor.Text.Length);
        }
    }

    [Fact]
    public void MaskedEditorRangesFollowTheRenderedDocumentRatherThanPublicExportFormat()
    {
        using var f = new Fixture(new MaskedTextBox { Mask = "00-00", TextMaskFormat = MaskFormat.ExcludePromptAndLiterals, Text = "1234" });
        Assert.Equal("1234", f.Editor.Text);
        Assert.Equal("12-34", f.Provider.DocumentRange.GetText());
        Assert.Equal(5, f.Provider.DocumentRange.End);
        Assert.True(f.Provider.SetSelection(3, 5));
        Assert.Equal("34", Assert.Single(f.Provider.GetSelection()).GetText());
        Assert.NotEmpty(f.Provider.DocumentRange.GetBoundingRectangles());
    }

    [Fact]
    public void CaretActivityFollowsExistingHostActivationWithoutReacquiringInput()
    {
        using var f = new Fixture(new TextBox { Text = "caret" });
        _ = f.Provider.GetCaretRange(out bool active);
        Assert.True(active);
        f.Surface.SetTextInputActive(false);
        var before = f.Surface.TextInputDiagnostics.Generation;
        _ = f.Provider.GetCaretRange(out active);
        Assert.False(active);
        Assert.True(f.Editor.Selected);
        Assert.Null(f.Surface.TextInputClient);
        Assert.Equal(before, f.Surface.TextInputDiagnostics.Generation);
        f.Surface.SetTextInputActive(true);
        _ = f.Provider.GetCaretRange(out active);
        Assert.True(active);
    }

    [Fact]
    public void KnownPasswordOwnerPreventsCustomPrivacyAndTextProviderGetters()
    {
        using var editor = new LyingPasswordEditor();
        editor.TextInputOptions = new() { Scope = TextInputScope.Password };
        var node = Assert.IsType<PlatformAccessibleObjectAdapter>(PlatformAccessibleObjectAdapter.From(editor.AccessibilityObject));
        Assert.True(node.IsSensitive);
        Assert.Null(ReadNativeTextCapability(node));
        Assert.Equal(0, editor.PrivacyReads);
        Assert.Equal(0, editor.ProviderReads);
    }

    [Fact]
    public void ProtectedAncestorRevokesPublicAndNativeTextCapabilitiesAndRetainedReads()
    {
        using var parent = new SensitivePanel();
        var editor = parent.Controls.Add(new TextBox { Text = "confidential" });
        using var surface = new SkiaControlSurface(parent);
        surface.Resize(400, 150);
        var provider = editor.AccessibilityObject.TextProvider!;
        var range = provider.DocumentRange;
        var native = PlatformAccessibleObjectAdapter.From(editor.AccessibilityObject)!;
        Assert.NotNull(ReadNativeTextCapability(native));
        parent.Sensitive = true;
        Assert.Null(editor.AccessibilityObject.TextProvider);
        Assert.Null(ReadNativeTextCapability(native));
        Assert.Throws<UnauthorizedAccessException>(() => range.GetText());
        Assert.Throws<UnauthorizedAccessException>(() => range.GetBoundingRectangles());
        Assert.Throws<UnauthorizedAccessException>(() => provider.GetSelection());
    }

    // Core.Tests does not expose WindowKit internals. Native backend projects separately test
    // the optional extension; this probes the real Core adapter without expanding friend access.
    private static object? ReadNativeTextCapability(IPlatformAccessibleObject node)
        => node.GetType().GetProperty("TextProvider")!.GetValue(node);

    private sealed class LyingPasswordEditor : TextBox
    {
        internal int PrivacyReads, ProviderReads;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(LyingPasswordEditor editor) : ControlAccessibleObject(editor)
        {
            public override bool IsSensitive { get { editor.PrivacyReads++; return false; } }
            public override AccessibleStates State => AccessibleStates.None;
            public override AccessibleTextProvider? TextProvider { get { editor.ProviderReads++; throw new InvalidOperationException("Protected provider must not be read."); } }
        }
    }

    private sealed class SensitivePanel : Panel
    {
        internal bool Sensitive, Protected;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(SensitivePanel panel) : ControlAccessibleObject(panel)
        {
            public override bool IsSensitive => panel.Sensitive;
            public override AccessibleStates State => base.State | (panel.Protected ? AccessibleStates.Protected : 0);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Panel root = new();
        private readonly SkiaControlSurface surface;
        internal TextBox Editor { get; }
        internal AccessibleTextProvider Provider { get; }
        internal ITextInputClient Client => surface.TextInputClient!;
        internal SkiaControlSurface Surface => surface;
        internal Fixture(TextBox editor)
        {
            Editor = root.Controls.Add(editor);
            editor.Bounds = new Rectangle(10, 10, 460, 140);
            surface = new(root);
            surface.Resize(500, 180);
            editor.Select();
            Provider = editor.AccessibilityObject.TextProvider!;
            Assert.NotNull(Provider);
        }
        public void Dispose() { surface.Dispose(); root.Dispose(); }
    }
}
