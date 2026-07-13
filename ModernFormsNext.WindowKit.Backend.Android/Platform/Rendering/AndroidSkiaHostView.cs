using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using SkiaSharp;
using SkiaSharp.Views.Android;
using ICharSequence = Java.Lang.ICharSequence;
using NativeKeyEvent = Android.Views.KeyEvent;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Hosts a density-aware Skia canvas in one Android view and translates lifecycle, pointer, resize,
/// hardware-key, and IME input into platform-neutral events.
/// </summary>
/// <remarks>
/// Create and use the view on the Android main thread. The owning activity must forward its
/// lifecycle and dispose the host from <c>OnDestroy</c>. Rendering occurs only after explicit
/// invalidation or a size change; there is no continuous render timer. The view is a custom Skia
/// surface and does not introduce an Android native-control UI tree for framework controls.
/// </remarks>
public sealed class AndroidSkiaHostView : SKCanvasView
{
    private readonly AndroidSurfaceHostState state = new();
    private readonly Action<string>? diagnosticSink;
    private readonly bool detailedDiagnostics;
    private bool disposed;

    /// <summary>Creates a Skia host using the supplied Android context.</summary>
    /// <param name="context">The current activity context.</param>
    /// <param name="diagnosticSink">An optional destination for backend diagnostics.</param>
    /// <param name="enableDetailedDiagnostics">Whether to report each render pass.</param>
    public AndroidSkiaHostView(
        Context context,
        Action<string>? diagnosticSink = null,
        bool enableDetailedDiagnostics = false)
        : base(context ?? throw new ArgumentNullException(nameof(context)))
    {
        this.diagnosticSink = diagnosticSink;
        detailedDiagnostics = enableDetailedDiagnostics;
        Focusable = true;
        FocusableInTouchMode = true;
    }

    /// <summary>Occurs when the shared renderer should paint the logical surface.</summary>
    public event EventHandler<AndroidSkiaRenderEventArgs>? Render;

    /// <summary>Occurs for pointer transitions expressed in logical pixels.</summary>
    public event EventHandler<AndroidPointerEvent>? Pointer;

    /// <summary>Occurs when the Android IME commits text to the focused shared editor.</summary>
    public event EventHandler<string>? TextCommitted;

    /// <summary>Occurs when the Android IME changes its composing text.</summary>
    public event EventHandler<string>? ComposingTextChanged;

    /// <summary>Occurs when the Android IME finishes its active composition.</summary>
    public event EventHandler? ComposingTextFinished;

    /// <summary>Occurs when the Android IME requests deletion around the shared caret.</summary>
    public event EventHandler<AndroidTextDeletionRequest>? DeleteSurroundingTextRequested;

    /// <summary>
    /// Occurs when an older host requests one backward-delete operation.
    /// </summary>
    /// <remarks>
    /// New integrations should use <see cref="DeleteSurroundingTextRequested"/> because an IME can
    /// request multiple UTF-16 units on either side of the selection.
    /// </remarks>
    public event EventHandler? DeleteBackwardRequested;

    /// <summary>Occurs when the IME or a hardware keyboard sends an editing-key transition.</summary>
    public event EventHandler<AndroidInputKeyEvent>? KeyInput;

    /// <summary>Occurs when the Android IME requests a new UTF-16 selection range.</summary>
    public event EventHandler<AndroidTextSelectionEvent>? TextSelectionRequested;

    /// <summary>
    /// Gets or sets the callback that supplies the current shared editor state to Android's IME.
    /// </summary>
    /// <remarks>
    /// The callback runs synchronously on the Android UI thread. It must not retain the activity,
    /// block, or mutate the control tree.
    /// </remarks>
    public Func<AndroidTextInputState>? TextInputStateProvider { get; set; }

    /// <summary>Gets the deterministic surface state exposed for diagnostics.</summary>
    public AndroidSurfaceHostState HostState => state;

    /// <summary>Gets the current Android density scale used to convert physical to logical pixels.</summary>
    public float Density => Resources?.DisplayMetrics?.Density is > 0 and var density ? density : 1f;

    /// <summary>Gets the current scaled density used by Android for font-related diagnostics.</summary>
    public float ScaledDensity => Density * (Resources?.Configuration?.FontScale is > 0 and var scale ? scale : 1f);

    /// <summary>Notifies the surface that its owning activity started.</summary>
    public void StartHost()
    {
        ThrowIfDisposed();
        state.Start();
        AndroidLogger.Write("Skia surface activity started.", diagnosticSink);
    }

    /// <summary>Notifies the surface that its activity resumed.</summary>
    public void ResumeHost()
    {
        ThrowIfDisposed();
        if (state.LifecycleState == AndroidSurfaceLifecycleState.Uninitialized)
            state.Start();

        state.Resume();
        AndroidLogger.Write("Skia surface resumed.", diagnosticSink);
        RequestRender();
    }

    /// <summary>Notifies the surface that its activity paused and cancels active pointers.</summary>
    public void PauseHost()
    {
        ThrowIfDisposed();
        var primaryPointerId = state.PrimaryPointerId;
        EmitCancellations(state.Pause(), primaryPointerId);
        AndroidLogger.Write("Skia surface paused.", diagnosticSink);
    }

    /// <summary>Notifies the surface that its activity stopped.</summary>
    public void StopHost()
    {
        ThrowIfDisposed();
        var primaryPointerId = state.PrimaryPointerId;
        EmitCancellations(state.Stop(), primaryPointerId);
        AndroidLogger.Write("Skia surface stopped.", diagnosticSink);
    }

    /// <summary>
    /// Refreshes density, logical size, IME state, and rendering after an Android configuration change.
    /// </summary>
    public void RefreshConfiguration()
    {
        ThrowIfDisposed();
        ResizeFromPhysicalPixels(Width, Height);
        RestartInput();
        RequestRender();
        AndroidLogger.Write($"Android configuration refreshed at density {Density:0.##}.", diagnosticSink);
    }

    /// <summary>Requests a coalesced render on the Android UI thread.</summary>
    public void RequestRender()
    {
        ThrowIfDisposed();
        var shouldInvalidate = state.RequestInvalidation();
        if ((shouldInvalidate || state.IsInvalidationPending) && state.CanRender)
            PostInvalidateOnAnimation();
    }

    /// <summary>Shows Android's soft keyboard for this view and synchronizes its editor snapshot.</summary>
    public void ShowSoftKeyboard()
    {
        ThrowIfDisposed();
        RequestFocus();
        var manager = GetInputMethodManager();
        manager?.RestartInput(this);
        NotifyTextStateChanged();
        manager?.ShowSoftInput(this, ShowFlags.Implicit);
    }

    /// <summary>Hides Android's soft keyboard for this view.</summary>
    public void HideSoftKeyboard()
    {
        ThrowIfDisposed();
        GetInputMethodManager()?.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
        ClearFocus();
    }

    /// <summary>Synchronizes Android's IME selection and composition with the shared editor.</summary>
    public void NotifyTextStateChanged()
    {
        if (disposed)
            return;

        var inputState = GetTextInputState();
        GetInputMethodManager()?.UpdateSelection(
            this,
            inputState.SelectionStart,
            inputState.SelectionEnd,
            inputState.CompositionStart,
            inputState.CompositionEnd);
    }

    /// <inheritdoc/>
    public override bool OnCheckIsTextEditor() => true;

    /// <inheritdoc/>
    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
    {
        if (outAttrs is null)
            return null;

        var inputState = GetTextInputState();
        outAttrs.InputType = global::Android.Text.InputTypes.ClassText |
            global::Android.Text.InputTypes.TextFlagCapSentences |
            global::Android.Text.InputTypes.TextFlagMultiLine;
        outAttrs.ImeOptions = ImeFlags.NoExtractUi;
        outAttrs.InitialSelStart = inputState.SelectionStart;
        outAttrs.InitialSelEnd = inputState.SelectionEnd;
        return new SharedInputConnection(this);
    }

    /// <inheritdoc/>
    public override bool OnKeyDown(Keycode keyCode, NativeKeyEvent? e)
        => PublishKey(keyCode, isDown: true) || base.OnKeyDown(keyCode, e);

    /// <inheritdoc/>
    public override bool OnKeyUp(Keycode keyCode, NativeKeyEvent? e)
        => PublishKey(keyCode, isDown: false) || base.OnKeyUp(keyCode, e);

    /// <inheritdoc/>
    public override bool OnTouchEvent(MotionEvent? motionEvent)
    {
        if (motionEvent is null || disposed || state.LifecycleState != AndroidSurfaceLifecycleState.Resumed)
            return false;

        var action = motionEvent.ActionMasked;
        if (action == MotionEventActions.Cancel)
        {
            var primaryPointerId = state.PrimaryPointerId;
            EmitCancellations(state.CancelActivePointers(), primaryPointerId);
            return true;
        }

        if (action == MotionEventActions.Move)
        {
            for (var index = 0; index < motionEvent.PointerCount; index++)
                EmitPointer(motionEvent, index, AndroidPointerAction.Move);
            return true;
        }

        var changedIndex = motionEvent.ActionIndex;
        var translatedAction = action switch
        {
            MotionEventActions.Down or MotionEventActions.PointerDown => AndroidPointerAction.Down,
            MotionEventActions.Up or MotionEventActions.PointerUp => AndroidPointerAction.Up,
            _ => (AndroidPointerAction?)null
        };

        if (translatedAction is null)
            return base.OnTouchEvent(motionEvent);

        EmitPointer(motionEvent, changedIndex, translatedAction.Value);
        return true;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        if (disposed)
            return;

        if (state.AttachSurface())
            PostInvalidateOnAnimation();
        AndroidLogger.Write("Native Skia surface attached.", diagnosticSink);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromWindow()
    {
        if (!disposed)
        {
            var primaryPointerId = state.PrimaryPointerId;
            EmitCancellations(state.DetachSurface(), primaryPointerId);
            AndroidLogger.Write("Native Skia surface detached.", diagnosticSink);
        }

        base.OnDetachedFromWindow();
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(int width, int height, int oldWidth, int oldHeight)
    {
        base.OnSizeChanged(width, height, oldWidth, oldHeight);
        if (disposed)
            return;

        if (ResizeFromPhysicalPixels(width, height) && state.CanRender)
            PostInvalidateOnAnimation();
    }

    /// <inheritdoc/>
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        if (disposed || !state.CanRender)
            return;

        var density = Density;
        if (state.LogicalWidth == 0 && state.LogicalHeight == 0)
        {
            state.Resize(
                AndroidDensityConverter.ToLogical(e.Info.Width, density),
                AndroidDensityConverter.ToLogical(e.Info.Height, density));
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);
        e.Surface.Canvas.Save();
        e.Surface.Canvas.Scale(density);
        try
        {
            Render?.Invoke(this, new AndroidSkiaRenderEventArgs(
                e.Surface.Canvas,
                state.LogicalWidth,
                state.LogicalHeight,
                density,
                state.RenderCount + 1));
            state.CompleteRender();
        }
        catch (System.Exception exception)
        {
            AndroidLogger.Error("Shared renderer failed.", exception, diagnosticSink);
            throw;
        }
        finally
        {
            e.Surface.Canvas.Restore();
        }

        if (detailedDiagnostics)
            AndroidLogger.Write($"Skia render #{state.RenderCount} completed.", diagnosticSink);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            disposed = true;
            var primaryPointerId = state.PrimaryPointerId;
            EmitCancellations(state.Dispose(), primaryPointerId);
            Render = null;
            Pointer = null;
            TextCommitted = null;
            ComposingTextChanged = null;
            ComposingTextFinished = null;
            DeleteSurroundingTextRequested = null;
            DeleteBackwardRequested = null;
            KeyInput = null;
            TextSelectionRequested = null;
            TextInputStateProvider = null;
            AndroidLogger.Write("Skia surface disposed.", diagnosticSink);
        }

        base.Dispose(disposing);
    }

    private bool ResizeFromPhysicalPixels(int width, int height)
    {
        var density = Density;
        var changed = state.Resize(
            AndroidDensityConverter.ToLogical(width, density),
            AndroidDensityConverter.ToLogical(height, density));
        if (changed)
        {
            AndroidLogger.Write(
                $"Skia surface resized to {state.LogicalWidth:0.#} x {state.LogicalHeight:0.#} logical pixels.",
                diagnosticSink);
        }

        return changed;
    }

    private void EmitPointer(MotionEvent motionEvent, int index, AndroidPointerAction action)
    {
        var pointerId = motionEvent.GetPointerId(index);
        var isPrimary = state.PrimaryPointerId == pointerId || (action == AndroidPointerAction.Down && index == 0);
        if (!state.TrackPointer(pointerId, action, isPrimary))
            return;

        var density = Density;
        Pointer?.Invoke(this, new AndroidPointerEvent(
            pointerId,
            action,
            AndroidDensityConverter.ToLogical(motionEvent.GetX(index), density),
            AndroidDensityConverter.ToLogical(motionEvent.GetY(index), density),
            isPrimary));
    }

    private void EmitCancellations(IReadOnlyList<int> pointerIds, int? primaryPointerId)
    {
        foreach (var pointerId in pointerIds)
        {
            Pointer?.Invoke(this, new AndroidPointerEvent(
                pointerId,
                AndroidPointerAction.Cancel,
                0,
                0,
                pointerId == primaryPointerId));
        }
    }

    private bool PublishKey(Keycode keyCode, bool isDown)
    {
        var translated = keyCode switch
        {
            Keycode.Del => AndroidInputKey.Backspace,
            Keycode.ForwardDel => AndroidInputKey.Delete,
            Keycode.Enter or Keycode.NumpadEnter => AndroidInputKey.Enter,
            Keycode.DpadLeft => AndroidInputKey.Left,
            Keycode.DpadUp => AndroidInputKey.Up,
            Keycode.DpadRight => AndroidInputKey.Right,
            Keycode.DpadDown => AndroidInputKey.Down,
            _ => (AndroidInputKey?)null
        };

        if (translated is null)
            return false;

        KeyInput?.Invoke(this, new AndroidInputKeyEvent(translated.Value, isDown));
        NotifyTextStateChanged();
        return true;
    }

    private void PublishCommittedText(string text)
    {
        TextCommitted?.Invoke(this, text);
        NotifyTextStateChanged();
    }

    private void PublishComposingText(string text)
    {
        ComposingTextChanged?.Invoke(this, text);
        NotifyTextStateChanged();
    }

    private void PublishCompositionFinished()
    {
        ComposingTextFinished?.Invoke(this, EventArgs.Empty);
        NotifyTextStateChanged();
    }

    private void PublishDeletion(AndroidTextDeletionRequest request)
    {
        DeleteSurroundingTextRequested?.Invoke(this, request);
        if (request.BeforeLength > 0 && request.AfterLength == 0)
            DeleteBackwardRequested?.Invoke(this, EventArgs.Empty);
        NotifyTextStateChanged();
    }

    private void PublishSelection(int start, int end)
    {
        TextSelectionRequested?.Invoke(this, new AndroidTextSelectionEvent(start, end));
        NotifyTextStateChanged();
    }

    private AndroidTextInputState GetTextInputState()
        => TextInputStateProvider?.Invoke() ?? new AndroidTextInputState(string.Empty, 0, 0);

    private InputMethodManager? GetInputMethodManager()
        => Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;

    private void RestartInput()
    {
        var manager = GetInputMethodManager();
        manager?.RestartInput(this);
        NotifyTextStateChanged();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class SharedInputConnection(AndroidSkiaHostView owner) : BaseInputConnection(owner, false)
    {
        public override bool CommitText(ICharSequence? text, int newCursorPosition)
        {
            owner.PublishCommittedText(text?.ToString() ?? string.Empty);
            return true;
        }

        public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
        {
            owner.PublishComposingText(text?.ToString() ?? string.Empty);
            return true;
        }

        public override bool FinishComposingText()
        {
            owner.PublishCompositionFinished();
            return true;
        }

        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
        {
            if (beforeLength < 0 || afterLength < 0)
                return false;

            owner.PublishDeletion(new AndroidTextDeletionRequest(beforeLength, afterLength));
            return true;
        }

        public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
        {
            if (beforeLength < 0 || afterLength < 0)
                return false;

            owner.PublishDeletion(owner.GetTextInputState().GetUtf16DeletionForCodePoints(beforeLength, afterLength));
            return true;
        }

        public override ICharSequence? GetTextBeforeCursorFormatted(int length, GetTextFlags flags)
            => new Java.Lang.String(owner.GetTextInputState().GetTextBeforeCursor(Math.Max(0, length)));

        public override ICharSequence? GetTextAfterCursorFormatted(int length, GetTextFlags flags)
            => new Java.Lang.String(owner.GetTextInputState().GetTextAfterCursor(Math.Max(0, length)));

        public override ICharSequence? GetSelectedTextFormatted(GetTextFlags flags)
            => new Java.Lang.String(owner.GetTextInputState().GetSelectedText());

        public override bool SetSelection(int start, int end)
        {
            var inputState = owner.GetTextInputState();
            if (start < 0 || end < 0 || start > inputState.Text.Length || end > inputState.Text.Length)
                return false;

            owner.PublishSelection(start, end);
            return true;
        }

        public override bool SendKeyEvent(NativeKeyEvent? e)
        {
            if (e is not null && owner.PublishKey(e.KeyCode, e.Action == KeyEventActions.Down))
                return true;

            return base.SendKeyEvent(e);
        }
    }
}
