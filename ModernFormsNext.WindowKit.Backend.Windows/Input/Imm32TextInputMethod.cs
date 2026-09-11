using System.Runtime.ExceptionServices;
using System.Text;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32.Input;

/// <summary>Adapts one existing HWND's IMM32 messages to its borrowed text session.</summary>
/// <remarks>
/// The HWND/UI thread owns this adapter. It borrows the OS context and never owns an editor.
/// Password/read-only clients have IME association disabled; ordinary WM_CHAR remains available.
/// This is IMM32 composition support, not a TSF text store or software-keyboard controller.
/// </remarks>
internal sealed class Imm32TextInputMethod : ITextInputMethod, IDisposable
{
    private const GCS ResultFlags = GCS.GCS_RESULTSTR | GCS.GCS_RESULTREADSTR |
        GCS.GCS_RESULTCLAUSE | GCS.GCS_RESULTREADCLAUSE;
    private const GCS PreeditFlags = GCS.GCS_COMPSTR | GCS.GCS_COMPATTR | GCS.GCS_COMPCLAUSE |
        GCS.GCS_COMPREADSTR | GCS.GCS_COMPREADATTR | GCS.GCS_COMPREADCLAUSE |
        GCS.GCS_CURSORPOS | GCS.GCS_DELTASTART;
    private const int InsertCharacter = 0x2000;
    private const int DoNotMoveCaret = 0x4000;
    private static readonly UnicodeEncoding StrictUnicode = new(false, false, true);
    private readonly IImm32NativeApi native;
    private Func<double>? scaling;
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private IntPtr hwnd;
    private ITextInputClient? client;
    private ITextInputClient? compositionOwner;
    private EventHandler? stateHandler;
    private EventHandler<TextCompositionEventArgs>? compositionHandler;
    private IntPtr previousContext;
    private long generation;
    private bool associationDisabled;
    private bool refreshing;
    private bool refreshPending;
    private bool applying;
    private bool requireStart;
    private bool disposed;
    private bool ownsCaret;
    private int caretWidth;
    private int caretHeight;
    private uint candidateMask = 1;

    internal Imm32TextInputMethod(IntPtr hwnd, Func<double> scaling, IImm32NativeApi? native = null)
    {
        if (hwnd == IntPtr.Zero) throw new ArgumentException("A live window is required.", nameof(hwnd));
        this.hwnd = hwnd;
        this.scaling = scaling ?? throw new ArgumentNullException(nameof(scaling));
        this.native = native ?? Imm32NativeApi.Instance;
    }

    /// <inheritdoc />
    public void SetClient(ITextInputClient? value)
    {
        VerifyAccess();
        if (disposed)
        {
            if (value is not null) throw new ObjectDisposedException(nameof(Imm32TextInputMethod));
            return;
        }
        if (ReferenceEquals(client, value)) { Refresh(); return; }

        long currentGeneration = ++generation;
        ITextInputClient? old = client;
        EventHandler? oldStateHandler = stateHandler;
        EventHandler<TextCompositionEventArgs>? oldCompositionHandler = compositionHandler;
        client = null;
        stateHandler = null;
        compositionHandler = null;
        // A native message carries no managed session ID. Retired composition messages
        // cannot attach to a replacement client until the IME announces a fresh START.
        if (old is not null) requireStart = true;
        List<Exception>? errors = null;
        Attempt(ref errors, RetireComposition);
        if (old is not null)
        {
            Attempt(ref errors, () => old.StateChanged -= oldStateHandler);
            Attempt(ref errors, () => old.CompositionChanged -= oldCompositionHandler);
        }
        if (!disposed && generation == currentGeneration)
        {
            client = value;
            if (value is not null)
            {
                // Capture the offered identity instead of relying on the sender of a
                // forwarding client's event. Previously captured delegates stay stale.
                EventHandler addedState = (_, _) => { if (ReferenceEquals(value, client)) Refresh(); };
                EventHandler<TextCompositionEventArgs> addedComposition = (_, args) => ClientCompositionChanged(value, args);
                stateHandler = addedState;
                compositionHandler = addedComposition;
                bool subscribed = false;
                try
                {
                    value.StateChanged += addedState;
                    if (!disposed && generation == currentGeneration)
                    {
                        value.CompositionChanged += addedComposition;
                        subscribed = !disposed && generation == currentGeneration;
                    }
                }
                catch (Exception exception) { (errors ??= new()).Add(exception); }
                if (!subscribed)
                {
                    // Custom accessors can attach then throw or replace the current
                    // client. Undo only these exact delegates, never a successor's.
                    if (generation == currentGeneration)
                    {
                        ++generation;
                        client = null;
                        stateHandler = null;
                        compositionHandler = null;
                        Attempt(ref errors, RetireComposition);
                    }
                    Attempt(ref errors, () => value.StateChanged -= addedState);
                    Attempt(ref errors, () => value.CompositionChanged -= addedComposition);
                }
            }
            Attempt(ref errors, Refresh);
        }
        Throw(errors);
    }

    /// <inheritdoc />
    public bool SetKeyboardVisible(bool visible)
    {
        VerifyAccess();
        // ImmSetOpenStatus controls conversion, not Windows touch-keyboard visibility.
        return false;
    }

    internal void ReleaseClient(ITextInputClient expected)
    {
        VerifyAccess();
        // A hidden/disposed old popup cannot clear a newer popup or restored owner.
        if (ReferenceEquals(client, expected)) SetClient(null);
    }

    internal void Refresh()
    {
        VerifyAccess();
        if (disposed) return;
        if (refreshing) { refreshPending = true; return; }
        refreshing = true;
        try
        {
            int attempts = 0;
            do
            {
                refreshPending = false;
                RefreshCurrent();
                if (++attempts == 32 && refreshPending)
                    throw new InvalidOperationException("Native text input state did not settle after callbacks.");
            }
            while (!disposed && refreshPending);
        }
        finally { refreshing = false; }
    }

    private void RefreshCurrent()
    {
        long observed = generation;
        ITextInputClient? offered = client;
        TextInputState? state = offered?.GetState(0);
        if (disposed || observed != generation || !ReferenceEquals(offered, client)) return;
        if (!Editable(state))
        {
            try { RetireComposition(); }
            finally
            {
                if (!disposed && observed == generation)
                {
                    try { DisableAssociation(); }
                    finally { ReleaseCaret(); }
                }
            }
            return;
        }
        RestoreAssociation();
        if (!disposed && observed == generation && native.FocusedWindow == hwnd) UpdateGeometry(state!);
        else ReleaseCaret();
    }

    internal void LoseFocus()
    {
        VerifyAccess();
        if (disposed) return;
        try { RetireComposition(); }
        finally { ReleaseCaret(); }
    }

    // Returning false permits default processing with only the unconsumed flags.
    internal bool HandleMessage(WindowsMessage message, IntPtr wParam, ref IntPtr lParam)
    {
        VerifyAccess();
        if (disposed) return false;
        switch (message)
        {
            case WindowsMessage.WM_IME_SETCONTEXT:
                if (wParam != IntPtr.Zero && EligibleClient() is not null)
                    lParam = (IntPtr)(lParam.ToInt64() & ~ISC_SHOWUICOMPOSITIONWINDOW);
                return false;
            case WindowsMessage.WM_IME_STARTCOMPOSITION:
                ITextInputClient? started = EligibleClient();
                if (native.FocusedWindow != hwnd || started is null) return false;
                requireStart = false;
                compositionOwner = started;
                Refresh();
                return true;
            case WindowsMessage.WM_IME_COMPOSITION:
                return HandleComposition(wParam, ref lParam);
            case WindowsMessage.WM_IME_ENDCOMPOSITION:
                if (compositionOwner is null) return false;
                RetireComposition(notifyNative: false);
                return true;
            case WindowsMessage.WM_IME_NOTIFY:
                // IMN_OPENCANDIDATE / IMN_CHANGECANDIDATE specify candidate list bits.
                if (wParam.ToInt64() is 3 or 5)
                {
                    candidateMask = (uint)(lParam.ToInt64() & 15);
                    if (candidateMask == 0) candidateMask = 1;
                    Refresh();
                }
                return false;
            default:
                return false;
        }
    }

    private bool HandleComposition(IntPtr character, ref IntPtr lParam)
    {
        if (applying) return true; // Never recursively edit or replay nested native input.
        ITextInputClient? owner = compositionOwner;
        if (owner is null && !requireStart && native.FocusedWindow == hwnd)
            compositionOwner = owner = EligibleClient(); // Some IMM providers begin with their first update.
        if (owner is null)
        {
            // Never let a late result from a revoked composition fall through to WM_CHAR
            // and insert into a newly selected control. A fresh START lifts this barrier.
            return requireStart;
        }

        long observed = generation;
        int flags = unchecked((int)lParam.ToInt64());
        var dataFlags = (GCS)flags;
        // Consume before callbacks, including callbacks that throw after editing the text.
        lParam = (IntPtr)(flags & ~((int)(ResultFlags | PreeditFlags) | InsertCharacter | DoNotMoveCaret));
        if ((dataFlags & (ResultFlags | PreeditFlags)) == 0 && (flags & InsertCharacter) == 0)
        {
            RetireComposition(notifyNative: false);
            return lParam == IntPtr.Zero;
        }

        string? result = null;
        string? preedit = null;
        int cursor = -1;
        bool valid = true;
        IntPtr contextWindow = hwnd;
        IntPtr context = native.GetContext(contextWindow);
        if (context == IntPtr.Zero) return lParam == IntPtr.Zero;
        try
        {
            if ((dataFlags & GCS.GCS_RESULTSTR) != 0)
                valid = TryReadText(context, GCS.GCS_RESULTSTR, out result);
            if (valid && (dataFlags & GCS.GCS_COMPSTR) != 0)
                valid = TryReadText(context, GCS.GCS_COMPSTR, out preedit);
            if (valid && (dataFlags & GCS.GCS_CURSORPOS) != 0)
                cursor = native.ReadComposition(context, GCS.GCS_CURSORPOS, Span<byte>.Empty);
        }
        finally { native.ReleaseContext(contextWindow, context); }
        if (!valid || !Current(owner, observed)) return lParam == IntPtr.Zero;

        applying = true;
        try
        {
            if (result is not null)
            {
                if (!owner.CommitText(result) || !Current(owner, observed)) return lParam == IntPtr.Zero;
            }
            if (preedit is not null)
            {
                if (!owner.SetComposingText(preedit) || !Current(owner, observed)) return lParam == IntPtr.Zero;
                // Android's newCursorPosition cannot express an interior preedit offset.
                // Apply the native UTF-16 cursor to the resulting absolute range instead.
                if (cursor >= 0) SetCompositionCursor(owner, cursor);
            }
            else if ((flags & InsertCharacter) != 0 && result is null)
            {
                char value = (char)character.ToInt64();
                if (!char.IsSurrogate(value))
                    owner.SetComposingText(value.ToString(), (flags & DoNotMoveCaret) != 0 ? 0 : 1);
            }
            else if (cursor >= 0 && Current(owner, observed))
            {
                SetCompositionCursor(owner, cursor);
            }
        }
        finally { applying = false; }
        if (Current(owner, observed)) Refresh();
        return lParam == IntPtr.Zero;
    }

    private bool TryReadText(IntPtr context, GCS index, out string? text)
    {
        text = null;
        int length = native.ReadComposition(context, index, Span<byte>.Empty);
        if (length < 0 || length > TextInputState.MaximumTextLength * 2 || (length & 1) != 0) return false;
        if (length == 0) { text = string.Empty; return true; }
        byte[] bytes = new byte[length];
        int copied = native.ReadComposition(context, index, bytes);
        if (copied < 0 || copied > length || (copied & 1) != 0) return false;
        try { text = StrictUnicode.GetString(bytes, 0, copied); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    private bool Current(ITextInputClient owner, long observed) => !disposed && generation == observed &&
        ReferenceEquals(owner, client) && ReferenceEquals(owner, compositionOwner);

    private ITextInputClient? EligibleClient()
    {
        ITextInputClient? offered = client;
        long observed = generation;
        TextInputState? state = offered?.GetState(0);
        return !disposed && observed == generation && ReferenceEquals(offered, client) && Editable(state) ? offered : null;
    }

    private static void SetCompositionCursor(ITextInputClient owner, int cursor)
    {
        TextInputState? state = owner.GetState(0);
        if (state?.HasComposition == true)
        {
            int position = state.CompositionStart + Math.Clamp(cursor, 0, state.CompositionEnd - state.CompositionStart);
            owner.SetSelection(position, position);
        }
    }

    private static bool Editable(TextInputState? state) => state is not null && !state.Options.ReadOnly &&
        state.Options.Scope != TextInputScope.Password;

    private void ClientCompositionChanged(object? sender, TextCompositionEventArgs args)
    {
        if (!applying && ReferenceEquals(sender, compositionOwner) &&
            args.Stage is TextCompositionStage.Committed or TextCompositionStage.Finished or TextCompositionStage.Canceled)
            RetireComposition();
    }

    private void RetireComposition() => RetireComposition(notifyNative: true, finish: true);

    private void RetireComposition(bool notifyNative, bool finish = false)
    {
        ITextInputClient? owner = compositionOwner;
        compositionOwner = null;
        if (owner is null) return;
        requireStart = true;
        List<Exception>? errors = null;
        // Blur/handoff accepts the visible preedit. Explicit IME cancellation rolls
        // back instead. Revoke before either callback, then cancel the native context;
        // its synchronous cancellation messages cannot roll back an accepted edit.
        Attempt(ref errors, () => { if (finish) owner.FinishComposition(); else owner.CancelComposition(); });
        if (notifyNative && hwnd != IntPtr.Zero && compositionOwner is null)
        {
            Attempt(ref errors, () =>
            {
                IntPtr contextWindow = hwnd;
                IntPtr context = native.GetContext(contextWindow);
                if (context == IntPtr.Zero) return;
                try { native.CancelComposition(context); }
                finally { native.ReleaseContext(contextWindow, context); }
            });
        }
        Throw(errors);
    }

    private void DisableAssociation()
    {
        if (associationDisabled) return;
        associationDisabled = true;
        previousContext = native.AssociateContext(hwnd, IntPtr.Zero);
    }

    private void RestoreAssociation()
    {
        if (!associationDisabled) return;
        associationDisabled = false;
        IntPtr previous = previousContext;
        previousContext = IntPtr.Zero;
        if (previous != IntPtr.Zero) native.AssociateContext(hwnd, previous);
    }

    private void UpdateGeometry(TextInputState state)
    {
        double scale = scaling?.Invoke() ?? 1;
        if (!double.IsFinite(scale) || scale <= 0) return;
        Rect caret = state.CaretRectangle;
        double left = Math.Floor(caret.X * scale), top = Math.Floor(caret.Y * scale);
        double right = Math.Ceiling(caret.Right * scale), bottom = Math.Ceiling(caret.Bottom * scale);
        if (!Fits(left) || !Fits(top) || !Fits(right) || !Fits(bottom) ||
            right - left > int.MaxValue || bottom - top > int.MaxValue) return;
        var rectangle = new RECT { left = (int)left, top = (int)top, right = (int)right, bottom = (int)bottom };
        var point = new POINT { X = rectangle.left, Y = rectangle.top };
        IntPtr contextWindow = hwnd;
        IntPtr context = native.GetContext(contextWindow);
        if (context != IntPtr.Zero)
        {
            try
            {
                native.PositionComposition(context, new COMPOSITIONFORM { dwStyle = CFS_POINT, ptCurrentPos = point });
                for (int index = 0; index < 4; index++)
                    if ((candidateMask & (1u << index)) != 0)
                        native.PositionCandidates(context, new CANDIDATEFORM {
                            dwIndex = index, dwStyle = CFS_EXCLUDE, ptCurrentPos = point, rcArea = rectangle });
            }
            finally { native.ReleaseContext(contextWindow, context); }
        }
        int width = Math.Max(1, rectangle.Width), height = Math.Max(1, rectangle.Height);
        if (!ownsCaret || caretWidth != width || caretHeight != height)
        {
            ReleaseCaret();
            ownsCaret = native.CreateCaret(hwnd, width, height);
            caretWidth = width;
            caretHeight = height;
        }
        if (ownsCaret) native.PositionCaret(rectangle.left, rectangle.top);
    }

    private static bool Fits(double value) => double.IsFinite(value) && value >= int.MinValue && value <= int.MaxValue;

    private void ReleaseCaret()
    {
        if (!ownsCaret) return;
        ownsCaret = false;
        // The queue has one caret: an old HWND must not destroy its successor's caret.
        if (native.CaretWindow == hwnd) native.DestroyCaret();
    }

    /// <summary>Revokes callbacks and restores borrowed native ownership before HWND destruction.</summary>
    public void Dispose()
    {
        VerifyAccess();
        if (disposed) return;
        disposed = true;
        ++generation;
        ITextInputClient? old = client;
        EventHandler? oldStateHandler = stateHandler;
        EventHandler<TextCompositionEventArgs>? oldCompositionHandler = compositionHandler;
        client = null;
        stateHandler = null;
        compositionHandler = null;
        List<Exception>? errors = null;
        Attempt(ref errors, RetireComposition);
        if (old is not null)
        {
            Attempt(ref errors, () => old.StateChanged -= oldStateHandler);
            Attempt(ref errors, () => old.CompositionChanged -= oldCompositionHandler);
        }
        Attempt(ref errors, ReleaseCaret);
        Attempt(ref errors, RestoreAssociation);
        hwnd = IntPtr.Zero;
        scaling = null;
        Throw(errors);
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("Native text input requires its owning UI thread.");
    }

    private static void Attempt(ref List<Exception>? errors, Action action)
    {
        try { action(); }
        catch (Exception exception) { (errors ??= new()).Add(exception); }
    }

    private static void Throw(List<Exception>? errors)
    {
        if (errors is null) return;
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        throw new AggregateException(errors);
    }
}
