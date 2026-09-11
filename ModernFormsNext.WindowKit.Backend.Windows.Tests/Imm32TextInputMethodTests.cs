using System.Text;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Input;
using ModernFormsNext.WindowKit.Input;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;
using TextRect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsUiCollection.Name)]
public sealed class Imm32TextInputMethodTests
{
    [Fact]
    public void ResultAndNextPreeditUseTheRealEditorAndConsumeDefaultInsertionFlags()
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        native.Text[GCS.GCS_COMPSTR] = "nihon";
        Send(method, GCS.GCS_COMPSTR);
        Assert.Equal("nihon", editor.Text);
        Assert.True(editor.Client.GetState()!.HasComposition);

        native.Text[GCS.GCS_RESULTSTR] = "日本";
        native.Text[GCS.GCS_COMPSTR] = "next";
        native.Cursor = 2;
        nint flags = (nint)(GCS.GCS_RESULTSTR | GCS.GCS_RESULTREADSTR | GCS.GCS_RESULTCLAUSE |
            GCS.GCS_RESULTREADCLAUSE | GCS.GCS_COMPSTR | GCS.GCS_CURSORPOS);
        Assert.True(method.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref flags));
        Assert.Equal(0, flags);
        Assert.Equal("日本next", editor.Text);
        TextInputState state = editor.Client.GetState()!;
        Assert.Equal(2, state.CompositionStart);
        Assert.Equal(4, state.SelectionEnd);
        Assert.Equal(native.Acquired, native.Released);

        native.Text[GCS.GCS_RESULTSTR] = "次";
        Send(method, GCS.GCS_RESULTSTR);
        nint end = 0;
        method.HandleMessage(WindowsMessage.WM_IME_ENDCOMPOSITION, 0, ref end);
        Assert.Equal("日本次", editor.Text);
        Assert.False(editor.Client.GetState()!.HasComposition);
    }

    [Fact]
    public void ExplicitEmptyResultDeletesSelectionAndDiffersFromNativeNoData()
    {
        using var editor = new Editor { Visible = true, Text = "before" };
        editor.Client.SetSelection(0, 6);
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        native.Text[GCS.GCS_RESULTSTR] = "";
        Send(method, GCS.GCS_RESULTSTR);
        Assert.Equal("", editor.Text);
        Assert.False(editor.Client.GetState()!.HasComposition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitCancellationOrUncommittedEndRestoresTheOriginalSelection(bool end)
    {
        using var editor = new Editor { Visible = true, Text = "left original right" };
        editor.Client.SetSelection(13, 5);
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        native.Text[GCS.GCS_COMPSTR] = "変更";
        Send(method, GCS.GCS_COMPSTR);
        Assert.Equal("left 変更 right", editor.Text);
        nint flags = 0;
        Assert.True(method.HandleMessage(end ? WindowsMessage.WM_IME_ENDCOMPOSITION : WindowsMessage.WM_IME_COMPOSITION,
            0, ref flags));
        Assert.Equal("left original right", editor.Text);
        TextInputState state = editor.Client.GetState()!;
        Assert.False(state.HasComposition);
        Assert.Equal(13, state.SelectionStart);
        Assert.Equal(5, state.SelectionEnd);
    }

    [Fact]
    public void FocusHandoffFinishesBeforeReentrantNativeCancellationAndRejectsLateResult()
    {
        using var first = new Editor { Visible = true };
        using var second = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(first.Client);
        Start(method);
        native.Text[GCS.GCS_COMPSTR] = "visible";
        Send(method, GCS.GCS_COMPSTR);
        native.OnCancel = () => {
            Assert.Equal("visible", first.Text);
            Assert.False(first.Client.GetState()!.HasComposition);
            nint canceled = 0;
            method.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref canceled);
        };
        method.SetClient(second.Client);
        native.Text[GCS.GCS_RESULTSTR] = "late";
        Send(method, GCS.GCS_RESULTSTR);
        Assert.Equal("visible", first.Text);
        Assert.Empty(second.Text);
        Start(method);
        Send(method, GCS.GCS_RESULTSTR);
        Assert.Equal("late", second.Text);
        Assert.Equal(native.Acquired, native.Released);
    }

    [Fact]
    public void PasswordAndReadOnlyDisableOnlyTheWindowAssociationAndRestoreTheBorrowedContext()
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Assert.Equal((nint)42, native.Context);
        editor.ReadOnly = true;
        method.Refresh();
        Assert.Equal(0, native.Context);
        editor.ReadOnly = false;
        editor.PasswordCharacter = '*';
        method.Refresh();
        Assert.Equal(0, native.Context);
        editor.PasswordCharacter = null;
        method.Refresh();
        Assert.Equal((nint)42, native.Context);
        Assert.False(method.SetKeyboardVisible(true));
        Assert.False(method.SetKeyboardVisible(false));
        method.SetClient(null);
        Assert.Equal(0, native.Context);
        method.Dispose();
        Assert.Equal((nint)42, native.Context);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(-2, 0)]
    [InlineData(3, 0)]
    [InlineData(131074, 0)]
    [InlineData(2, 4)]
    [InlineData(2, 1)]
    public void MalformedNativeLengthsNeverMutateOrLeakTheBorrowedContext(int requested, int copied)
    {
        using var editor = new Editor { Visible = true, Text = "safe" };
        var native = new Native { RequestedLength = requested, CopiedLength = copied };
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        Send(method, GCS.GCS_RESULTSTR);
        Assert.Equal("safe", editor.Text);
        Assert.Equal(native.Acquired, native.Released);
        Assert.Equal(requested is > 0 and <= 131072 && requested % 2 == 0 ? 1 : 0, native.BufferReads);
    }

    [Fact]
    public void NativeResultContainingAnUnpairedSurrogateIsRejected()
    {
        using var editor = new Editor { Visible = true, Text = "safe" };
        var native = new Native { RawBytes = [0x00, 0xD8] };
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        Send(method, GCS.GCS_RESULTSTR);
        Assert.Equal("safe", editor.Text);
        Assert.Equal(native.Acquired, native.Released);
    }

    [Fact]
    public void CommitCallbackFailureHasAlreadyConsumedNativeResultAndReleasedContext()
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Committed) throw new InvalidOperationException("observer");
        };
        native.Text[GCS.GCS_RESULTSTR] = "once";
        nint flags = (nint)(GCS.GCS_RESULTSTR | GCS.GCS_RESULTCLAUSE);
        Assert.ThrowsAny<Exception>(() => method.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref flags));
        Assert.Equal(0, flags);
        Assert.Equal("once", editor.Text);
        Assert.Equal(native.Acquired, native.Released);
    }

    [Fact]
    public void DisposeFinishesAndRestoresOwnershipDespiteAThrowingCompositionObserver()
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        var method = new Imm32TextInputMethod(1, () => 1, native);
        method.SetClient(editor.Client);
        Start(method);
        native.Text[GCS.GCS_COMPSTR] = "visible";
        Send(method, GCS.GCS_COMPSTR);
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Finished) throw new InvalidOperationException("observer");
        };
        Assert.ThrowsAny<Exception>(method.Dispose);
        Assert.Equal("visible", editor.Text);
        Assert.False(editor.Client.GetState()!.HasComposition);
        Assert.Equal(native.Acquired, native.Released);
        Assert.Equal(0, native.CaretWindow);
        int positioned = native.Candidates.Count;
        editor.Text = "after";
        Assert.Equal(positioned, native.Candidates.Count);
        method.Dispose();
        method.SetClient(null);
        Assert.Throws<ObjectDisposedException>(() => method.SetClient(editor.Client));
    }

    [Fact]
    public void GeometryUsesWindowScalingAndOldWindowDoesNotDestroySuccessorCaret()
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 2, native);
        var geometry = new GeometryClient(editor.Client, new TextRect(10.25, 20.5, 1, 15));
        method.SetClient(geometry);
        CANDIDATEFORM candidate = Assert.Single(native.Candidates);
        Assert.Equal(CFS_EXCLUDE, candidate.dwStyle);
        Assert.Equal(20, candidate.rcArea.left);
        Assert.Equal(41, candidate.rcArea.top);
        Assert.Equal(23, candidate.rcArea.right);
        Assert.Equal(71, candidate.rcArea.bottom);
        Assert.Equal((20, 41), native.CaretPosition);
        native.FocusedWindow = 2;
        native.CaretWindow = 2;
        method.LoseFocus();
        Assert.Equal((nint)2, native.CaretWindow);
    }

    [Fact]
    public void DefaultCompositionUiIsSuppressedOnlyForAnEligibleInlineClient()
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        nint flags = unchecked((nint)(ISC_SHOWUICOMPOSITIONWINDOW | ISC_SHOWUICANDIDATEWINDOW));
        Assert.False(method.HandleMessage(WindowsMessage.WM_IME_SETCONTEXT, 1, ref flags));
        Assert.NotEqual(0, flags.ToInt64() & ISC_SHOWUICOMPOSITIONWINDOW);
        method.SetClient(editor.Client);
        method.HandleMessage(WindowsMessage.WM_IME_SETCONTEXT, 1, ref flags);
        Assert.Equal(ISC_SHOWUICANDIDATEWINDOW, flags.ToInt64());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartiallySubscribedCustomClientIsFullyDetached(bool popupLease)
    {
        using var editor = new Editor { Visible = true };
        var native = new Native();
        using var method = new Imm32TextInputMethod(1, () => 1, native);
        using var popup = new PopupTextInputMethod(method, rectangle => rectangle);
        var forwarding = new GeometryClient(editor.Client, new TextRect(1, 2, 1, 10), failSubscription: true);
        Assert.ThrowsAny<Exception>(() => {
            if (popupLease) popup.SetClient(forwarding);
            else method.SetClient(forwarding);
        });
        Assert.Equal(0, forwarding.SubscriptionCount);
        method.SetClient(editor.Client);
        Start(method);
        native.Text[GCS.GCS_RESULTSTR] = "still editable";
        Send(method, GCS.GCS_RESULTSTR);
        Assert.Equal("still editable", editor.Text);
    }

    private static void Start(Imm32TextInputMethod method)
    {
        nint flags = 0;
        Assert.True(method.HandleMessage(WindowsMessage.WM_IME_STARTCOMPOSITION, 0, ref flags));
    }

    private static void Send(Imm32TextInputMethod method, GCS flags)
    {
        nint value = (nint)flags;
        Assert.True(method.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref value));
    }

    private sealed class Editor : TextBox
    {
        private readonly SkiaControlSurface surface;
        public Editor()
        {
            // A locally visible unparented Control is still effectively invisible. Use
            // the existing real surface/focus host rather than overriding that contract.
            surface = new(this);
            surface.Resize(400, 120);
            Select();
        }
        public ITextInputClient Client => surface.TextInputClient ?? throw new InvalidOperationException("The editor is not focused.");
        protected override void Dispose(bool disposing)
        {
            try { if (disposing) surface.Dispose(); }
            finally { base.Dispose(disposing); }
        }
    }

    // Real TextBox editing plus a deterministic host-coordinate transform, not a fake editor.
    private sealed class GeometryClient(ITextInputClient inner, TextRect rectangle, bool failSubscription = false) : ITextInputClient
    {
        private readonly HashSet<Delegate> subscriptions = [];
        public int SubscriptionCount => subscriptions.Count;
        public event EventHandler? StateChanged
        {
            add { inner.StateChanged += value; if (value is not null) subscriptions.Add(value); }
            remove { inner.StateChanged -= value; if (value is not null) subscriptions.Remove(value); }
        }
        public event EventHandler<TextCompositionEventArgs>? CompositionChanged
        {
            add
            {
                inner.CompositionChanged += value;
                if (value is not null) subscriptions.Add(value);
                if (failSubscription) throw new InvalidOperationException("Custom event accessor failed after attachment.");
            }
            remove { inner.CompositionChanged -= value; if (value is not null) subscriptions.Remove(value); }
        }
        public TextInputState? GetState(int maximumTextLength = 4096, int? textStart = null) => inner.GetState(maximumTextLength, textStart)?.WithCaretRectangle(rectangle);
        public bool CommitText(string text, int newCursorPosition = 1) => inner.CommitText(text, newCursorPosition);
        public bool SetComposingText(string text, int newCursorPosition = 1) => inner.SetComposingText(text, newCursorPosition);
        public bool SetComposingRegion(int start, int end) => inner.SetComposingRegion(start, end);
        public bool SetSelection(int start, int end) => inner.SetSelection(start, end);
        public bool FinishComposition() => inner.FinishComposition();
        public bool CancelComposition() => inner.CancelComposition();
        public bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false) => inner.DeleteSurroundingText(beforeLength, afterLength, inCodePoints);
        public bool PerformEditorAction(TextInputAction action) => inner.PerformEditorAction(action);
    }

    internal sealed class Native : IImm32NativeApi
    {
        public Dictionary<GCS, string> Text { get; } = [];
        public List<CANDIDATEFORM> Candidates { get; } = [];
        public nint Context { get; set; } = 42;
        public nint FocusedWindow { get; set; } = 1;
        public nint CaretWindow { get; set; }
        public (int X, int Y) CaretPosition { get; private set; }
        public int Acquired { get; private set; }
        public int Released { get; private set; }
        public int BufferReads { get; private set; }
        public int Cursor { get; set; }
        public int? RequestedLength { get; set; }
        public int? CopiedLength { get; set; }
        public byte[]? RawBytes { get; set; }
        public Action? OnCancel { get; set; }
        public nint GetContext(nint hwnd) { if (Context != 0) Acquired++; return Context; }
        public void ReleaseContext(nint hwnd, nint context) { Assert.NotEqual(0, hwnd); Assert.NotEqual(0, context); Released++; }
        public nint AssociateContext(nint hwnd, nint context) { var old = Context; Context = context; return old; }
        public int ReadComposition(nint context, GCS index, Span<byte> buffer)
        {
            if (index == GCS.GCS_CURSORPOS) return Cursor;
            byte[] bytes = RawBytes ?? Encoding.Unicode.GetBytes(Text.GetValueOrDefault(index, ""));
            if (buffer.IsEmpty) return RequestedLength ?? bytes.Length;
            BufferReads++;
            bytes.AsSpan(0, Math.Min(bytes.Length, buffer.Length)).CopyTo(buffer);
            return CopiedLength ?? bytes.Length;
        }
        public void CancelComposition(nint context) => OnCancel?.Invoke();
        public void PositionComposition(nint context, COMPOSITIONFORM form) { }
        public void PositionCandidates(nint context, CANDIDATEFORM form) => Candidates.Add(form);
        public bool CreateCaret(nint hwnd, int width, int height) { CaretWindow = hwnd; return true; }
        public void PositionCaret(int x, int y) => CaretPosition = (x, y);
        public void DestroyCaret() => CaretWindow = 0;
    }
}
