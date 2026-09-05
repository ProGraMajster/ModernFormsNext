using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using ModernFormsNext.WindowKit.Backend;
using SkiaSharp;
using SkiaSharp.Views.Android;
using System.Text.Json;
using ICharSequence = Java.Lang.ICharSequence;
using NativeKeyEvent = Android.Views.KeyEvent;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Hosts a density-aware Skia canvas in one Android view and translates lifecycle, pointer, resize,
/// hardware-key, and IME input into platform-neutral events.
/// </summary>
/// <remarks>
/// Create and use the view on the Android main thread. The owning activity must forward its
/// lifecycle and dispose the host from <c>OnDestroy</c>. Rendering occurs only after explicit
/// invalidation or a size change. Shared animation work uses the backend's idle-aware Choreographer
/// source; there is no continuous or per-control render timer. The view is a custom Skia surface
/// and does not introduce an Android native-control UI tree for framework controls.
/// </remarks>
public sealed class AndroidSkiaHostView : SKCanvasView
{
    private readonly AndroidSurfaceHostState state = new();
    private readonly Action<string>? diagnosticSink;
    private readonly bool detailedDiagnostics;
    private readonly AndroidAnimationSurfaceRegistration? animationSurfaceRegistration;
    private SharedInputConnection? activeInputConnection;
    private KeyEventObservation? lastKeyEvent;
    private long inputDiagnosticSequence;
    private bool inputStateNotificationPending;
    private bool disposed;
    private IPlatformAccessibilityHost? accessibilityHost;
    private AndroidAccessibilityNodeProvider? accessibilityProvider;

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
        animationSurfaceRegistration =
            (PlatformServiceRegistry.GetService<IPlatformAnimationFrameSource>() as
                AndroidChoreographerAnimationFrameSource)?.CreateSurfaceRegistration();
        Focusable = true;
        FocusableInTouchMode = true;
    }

    /// <summary>Gets or sets the borrowed canonical accessibility host for this Skia surface.</summary>
    /// <remarks>
    /// Set this on the Android main thread, normally to the existing SkiaControlSurface. The host
    /// remains owned by the application. Replacing it disconnects all previous virtual node IDs.
    /// The view supplies physical screen coordinates; the windowless host supplies surface-relative
    /// logical bounds. No native child View is required for a semantic child.
    /// </remarks>
    public IPlatformAccessibilityHost? AccessibilityHost
    {
        get => accessibilityHost;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(accessibilityHost, value)) return;
            accessibilityProvider?.Dispose();
            accessibilityHost = value;
            accessibilityProvider = value is null ? null : new AndroidAccessibilityNodeProvider(this, value);
            ImportantForAccessibility = value is null ? ImportantForAccessibility.Auto : ImportantForAccessibility.Yes;
            if (IsAttachedToWindow) accessibilityProvider?.Attach();
        }
    }

    /// <inheritdoc/>
    public override global::Android.Views.Accessibility.AccessibilityNodeProvider? AccessibilityNodeProvider
        => accessibilityProvider ?? base.AccessibilityNodeProvider;

    internal void InitializeAccessibilityHostNode(global::Android.Views.Accessibility.AccessibilityNodeInfo info)
        => base.OnInitializeAccessibilityNodeInfo(info);

    /// <inheritdoc/>
    protected override bool DispatchHoverEvent(MotionEvent? e)
        => accessibilityProvider?.DispatchHover(e) == true || base.DispatchHoverEvent(e);

    /// <summary>Occurs when the shared renderer should paint the logical surface.</summary>
    public event EventHandler<AndroidSkiaRenderEventArgs>? Render;

    /// <summary>Occurs for pointer transitions expressed in logical pixels.</summary>
    public event EventHandler<AndroidPointerEvent>? Pointer;

    /// <summary>Occurs when the Android IME commits text to the focused shared editor.</summary>
    /// <remarks>
    /// This compatibility event is raised together with <see cref="TextCommitRequested"/>. A host
    /// should subscribe to only one of them.
    /// </remarks>
    public event EventHandler<string>? TextCommitted;

    /// <summary>Occurs when the Android IME commits text with an explicit caret position.</summary>
    /// <remarks>
    /// New hosts should use this event. <see cref="TextCommitted"/> remains available for hosts
    /// that predate cursor-position forwarding.
    /// </remarks>
    public event EventHandler<AndroidTextEditEvent>? TextCommitRequested;

    /// <summary>Occurs when the Android IME changes its composing text.</summary>
    /// <remarks>
    /// This compatibility event is raised together with <see cref="ComposingTextUpdateRequested"/>.
    /// A host should subscribe to only one of them.
    /// </remarks>
    public event EventHandler<string>? ComposingTextChanged;

    /// <summary>Occurs when the Android IME changes composing text with an explicit caret position.</summary>
    public event EventHandler<AndroidTextEditEvent>? ComposingTextUpdateRequested;

    /// <summary>Occurs when the Android IME finishes its active composition.</summary>
    public event EventHandler? ComposingTextFinished;

    /// <summary>Occurs when the Android IME marks an existing UTF-16 range as composing.</summary>
    public event EventHandler<AndroidTextSelectionEvent>? ComposingRegionRequested;

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

    /// <summary>
    /// Gets or sets whether detailed Android input-connection diagnostics are emitted.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. Enable this only for a short diagnostic session:
    /// every IME query and mutation includes the complete editor text and can contain user input.
    /// Records are written under the <c>MFN.InputConnection</c> logcat tag.
    /// </remarks>
    public bool EnableInputConnectionDiagnostics { get; set; }

    /// <summary>Gets or sets an optional destination for enabled input-connection records.</summary>
    /// <remarks>
    /// The sink is not called unless <see cref="EnableInputConnectionDiagnostics"/> is enabled.
    /// Android logcat remains the primary destination when no sink is supplied.
    /// </remarks>
    public Action<string>? InputConnectionDiagnosticSink { get; set; }

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
        UpdateAnimationSurfaceRegistration();
        AndroidLogger.Write("Skia surface activity started.", diagnosticSink);
    }

    /// <summary>Notifies the surface that its activity resumed.</summary>
    public void ResumeHost()
    {
        ThrowIfDisposed();
        if (state.LifecycleState == AndroidSurfaceLifecycleState.Uninitialized)
            state.Start();

        state.Resume();
        UpdateAnimationSurfaceRegistration();
        AndroidLogger.Write("Skia surface resumed.", diagnosticSink);
        RequestRender();
    }

    /// <summary>Notifies the surface that its activity paused and cancels active pointers.</summary>
    public void PauseHost()
    {
        ThrowIfDisposed();
        var primaryPointerId = state.PrimaryPointerId;
        EmitCancellations(state.Pause(), primaryPointerId);
        UpdateAnimationSurfaceRegistration();
        AndroidLogger.Write("Skia surface paused.", diagnosticSink);
    }

    /// <summary>Notifies the surface that its activity stopped.</summary>
    public void StopHost()
    {
        ThrowIfDisposed();
        var primaryPointerId = state.PrimaryPointerId;
        EmitCancellations(state.Stop(), primaryPointerId);
        UpdateAnimationSurfaceRegistration();
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
        var batchDepth = activeInputConnection?.BatchDepth ?? 0;
        if (batchDepth > 0)
        {
            inputStateNotificationPending = true;
            if (EnableInputConnectionDiagnostics)
            {
                WriteInputDiagnostic(
                    "UpdateSelection",
                    "HostToIme.DeferredBatch",
                    $"selection={inputState.SelectionStart}..{inputState.SelectionEnd}; " +
                    $"composition={inputState.CompositionStart}..{inputState.CompositionEnd}",
                    inputState,
                    inputState,
                    batchDepth,
                    batchDepth,
                    result: "deferred");
            }

            return;
        }

        inputStateNotificationPending = false;
        if (!EnableInputConnectionDiagnostics)
        {
            GetInputMethodManager()?.UpdateSelection(
                this,
                inputState.SelectionStart,
                inputState.SelectionEnd,
                inputState.CompositionStart,
                inputState.CompositionEnd);
            return;
        }

        GetInputMethodManager()?.UpdateSelection(
            this,
            inputState.SelectionStart,
            inputState.SelectionEnd,
            inputState.CompositionStart,
            inputState.CompositionEnd);
        WriteInputDiagnostic(
            "UpdateSelection",
            "HostToIme",
            $"selection={inputState.SelectionStart}..{inputState.SelectionEnd}; " +
            $"composition={inputState.CompositionStart}..{inputState.CompositionEnd}",
            inputState,
            GetTextInputState(),
            batchDepth,
            batchDepth,
            result: "void");
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
        inputStateNotificationPending = false;
        activeInputConnection = new SharedInputConnection(this);
        return activeInputConnection;
    }

    /// <inheritdoc/>
    public override bool OnKeyDown(Keycode keyCode, NativeKeyEvent? e)
        => ProcessViewKeyEvent(keyCode, e, isDown: true, () => base.OnKeyDown(keyCode, e));

    /// <inheritdoc/>
    public override bool OnKeyUp(Keycode keyCode, NativeKeyEvent? e)
        => ProcessViewKeyEvent(keyCode, e, isDown: false, () => base.OnKeyUp(keyCode, e));

    /// <inheritdoc/>
    public override bool OnTouchEvent(MotionEvent? motionEvent)
    {
        if (motionEvent is null || disposed || state.LifecycleState != AndroidSurfaceLifecycleState.Resumed)
            return false;

        AndroidMotionEventPlan plan = AndroidMotionEventPlan.Create(
            TranslateMotionEventAction(motionEvent.ActionMasked),
            motionEvent.PointerCount,
            motionEvent.ActionIndex);
        if (plan.CancelAll)
        {
            var primaryPointerId = state.PrimaryPointerId;
            EmitCancellations(state.CancelActivePointers(), primaryPointerId);
            return true;
        }

        if (plan.EventCount == 0 || plan.PointerAction is not { } pointerAction)
            return base.OnTouchEvent(motionEvent);

        for (var translatedIndex = 0; translatedIndex < plan.EventCount; translatedIndex++)
        {
            int pointerIndex = plan.GetPointerIndex(translatedIndex);
            EmitPointer(motionEvent, pointerIndex, pointerAction);
        }

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
        accessibilityProvider?.Attach();
        UpdateAnimationSurfaceRegistration();
        AndroidLogger.Write("Native Skia surface attached.", diagnosticSink);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromWindow()
    {
        if (!disposed)
        {
            accessibilityProvider?.Detach();
            var primaryPointerId = state.PrimaryPointerId;
            EmitCancellations(state.DetachSurface(), primaryPointerId);
            UpdateAnimationSurfaceRegistration();
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
            accessibilityProvider?.Dispose();
            accessibilityProvider = null;
            accessibilityHost = null;
            var primaryPointerId = state.PrimaryPointerId;
            EmitCancellations(state.Dispose(), primaryPointerId);
            animationSurfaceRegistration?.Dispose();
            Render = null;
            Pointer = null;
            TextCommitted = null;
            TextCommitRequested = null;
            ComposingTextChanged = null;
            ComposingTextUpdateRequested = null;
            ComposingTextFinished = null;
            ComposingRegionRequested = null;
            DeleteSurroundingTextRequested = null;
            DeleteBackwardRequested = null;
            KeyInput = null;
            TextSelectionRequested = null;
            TextInputStateProvider = null;
            activeInputConnection = null;
            InputConnectionDiagnosticSink = null;
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

    private void UpdateAnimationSurfaceRegistration()
        => animationSurfaceRegistration?.SetActive(state.CanRender);

    private void EmitPointer(MotionEvent motionEvent, int index, AndroidPointerAction action)
    {
        var pointerId = motionEvent.GetPointerId(index);
        var isPrimary = state.PrimaryPointerId == pointerId || (action == AndroidPointerAction.Down && index == 0);
        if (!state.TrackPointer(pointerId, action, isPrimary))
            return;

        var density = Density;
        var rawX = motionEvent.GetX(index);
        var rawY = motionEvent.GetY(index);
        var logicalX = AndroidDensityConverter.ToLogical(rawX, density);
        var logicalY = AndroidDensityConverter.ToLogical(rawY, density);
        if (detailedDiagnostics)
        {
            AndroidLogger.Write(
                $"Pointer {pointerId}: {action}; raw=({rawX:0.#},{rawY:0.#}); " +
                $"logical=({logicalX:0.#},{logicalY:0.#}); primary={isPrimary}.",
                diagnosticSink);
        }

        Pointer?.Invoke(this, new AndroidPointerEvent(
            pointerId,
            action,
            logicalX,
            logicalY,
            isPrimary));
    }

    private static AndroidMotionEventAction TranslateMotionEventAction(MotionEventActions action)
        => action switch
        {
            MotionEventActions.Down => AndroidMotionEventAction.Down,
            MotionEventActions.PointerDown => AndroidMotionEventAction.PointerDown,
            MotionEventActions.Move => AndroidMotionEventAction.Move,
            MotionEventActions.PointerUp => AndroidMotionEventAction.PointerUp,
            MotionEventActions.Up => AndroidMotionEventAction.Up,
            MotionEventActions.Cancel => AndroidMotionEventAction.Cancel,
            _ => AndroidMotionEventAction.Other
        };

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

    private void PublishCommittedText(string text, int newCursorPosition)
    {
        TextCommitRequested?.Invoke(this, new AndroidTextEditEvent(text, newCursorPosition));
        TextCommitted?.Invoke(this, text);
        NotifyTextStateChanged();
    }

    private void PublishComposingText(string text, int newCursorPosition)
    {
        ComposingTextUpdateRequested?.Invoke(this, new AndroidTextEditEvent(text, newCursorPosition));
        ComposingTextChanged?.Invoke(this, text);
        NotifyTextStateChanged();
    }

    private void PublishComposingRegion(int start, int end)
        => ComposingRegionRequested?.Invoke(this, new AndroidTextSelectionEvent(start, end));

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

    private void FlushTextStateNotification()
    {
        if (inputStateNotificationPending && activeInputConnection?.BatchDepth == 0)
            NotifyTextStateChanged();
    }

    private bool ProcessViewKeyEvent(
        Keycode keyCode,
        NativeKeyEvent? keyEvent,
        bool isDown,
        Func<bool> baseHandler)
    {
        if (!EnableInputConnectionDiagnostics)
            return PublishKey(keyCode, isDown) || baseHandler();

        var before = GetTextInputState();
        var batchDepth = activeInputConnection?.BatchDepth ?? 0;
        var observation = ObserveKeyEvent(keyCode, isDown, "ViewKeyEvent");
        var result = PublishKey(keyCode, isDown) || baseHandler();
        WriteInputDiagnostic(
            isDown ? "OnKeyDown" : "OnKeyUp",
            "ViewKeyEvent",
            $"keyCode={keyCode}; action={keyEvent?.Action.ToString() ?? (isDown ? "Down" : "Up")}; " +
            $"unicodeChar={keyEvent?.UnicodeChar ?? 0}; deviceId={keyEvent?.DeviceId ?? -1}",
            before,
            GetTextInputState(),
            batchDepth,
            batchDepth,
            result.ToString(),
            operationKeyEvent: observation);
        return result;
    }

    private KeyEventObservation ObserveKeyEvent(Keycode keyCode, bool isDown, string source)
    {
        var observation = new KeyEventObservation(
            DateTimeOffset.UtcNow,
            keyCode,
            isDown ? KeyEventActions.Down : KeyEventActions.Up,
            source);
        lastKeyEvent = observation;
        return observation;
    }

    private KeyEventObservation? GetRecentKeyEvent(DateTimeOffset timestamp)
        => lastKeyEvent is { } value && timestamp - value.Timestamp <= TimeSpan.FromMilliseconds(250)
            ? value
            : null;

    private void WriteInputDiagnostic(
        string method,
        string source,
        string arguments,
        AndroidTextInputState before,
        AndroidTextInputState after,
        int batchDepthBefore,
        int batchDepthAfter,
        string? result = null,
        string? argumentText = null,
        int? newCursorPosition = null,
        bool sameTextArgumentAsPreviousOperation = false,
        KeyEventObservation? operationKeyEvent = null,
        string? exception = null)
    {
        if (!EnableInputConnectionDiagnostics)
            return;

        var timestamp = DateTimeOffset.UtcNow;
        var recentKeyEvent = operationKeyEvent ?? GetRecentKeyEvent(timestamp);
        var noOp = SameState(before, after);
        var sameTextReinserted = DetectSameTextReinsertion(argumentText, before, after);
        var record = new
        {
            sequence = Interlocked.Increment(ref inputDiagnosticSequence),
            timestamp,
            managedThreadId = Environment.CurrentManagedThreadId,
            nativeThreadId = global::Android.OS.Process.MyTid(),
            batchDepthBefore,
            batchDepthAfter,
            method,
            source,
            arguments,
            argumentText,
            newCursorPosition,
            before = DescribeState(before),
            after = DescribeState(after),
            result,
            exception,
            parallelKeyEvent = recentKeyEvent is not null,
            recentKeyEvent = recentKeyEvent is null ? null : new
            {
                timestamp = recentKeyEvent.Value.Timestamp,
                keyCode = recentKeyEvent.Value.KeyCode.ToString(),
                action = recentKeyEvent.Value.Action.ToString(),
                recentKeyEvent.Value.Source,
                ageMilliseconds = Math.Max(0, (timestamp - recentKeyEvent.Value.Timestamp).TotalMilliseconds)
            },
            noOp,
            sameTextArgumentAsPreviousOperation,
            sameTextReinserted
        };
        var message = JsonSerializer.Serialize(record);
        global::Android.Util.Log.Info("MFN.InputConnection", message);
        InputConnectionDiagnosticSink?.Invoke(message);
    }

    private static object DescribeState(AndroidTextInputState state) => new
    {
        document = state.Text,
        selectionStart = state.SelectionStart,
        selectionEnd = state.SelectionEnd,
        selectionLength = Math.Abs(state.SelectionEnd - state.SelectionStart),
        compositionStart = state.CompositionStart,
        compositionEnd = state.CompositionEnd,
        compositionLength = state.CompositionStart < 0 ? 0 : state.CompositionEnd - state.CompositionStart,
        state.Revision
    };

    private static bool SameState(AndroidTextInputState left, AndroidTextInputState right)
        => left.Text == right.Text &&
           left.SelectionStart == right.SelectionStart &&
           left.SelectionEnd == right.SelectionEnd &&
           left.CompositionStart == right.CompositionStart &&
           left.CompositionEnd == right.CompositionEnd;

    private static bool DetectSameTextReinsertion(
        string? argumentText,
        AndroidTextInputState before,
        AndroidTextInputState after)
    {
        if (string.IsNullOrEmpty(argumentText) || after.Text.Length <= before.Text.Length)
            return false;

        var insertionStart = before.CompositionStart >= 0
            ? before.CompositionStart
            : Math.Min(before.SelectionStart, before.SelectionEnd);
        return insertionStart >= argumentText.Length &&
            before.Text.AsSpan(insertionStart - argumentText.Length, argumentText.Length)
                .SequenceEqual(argumentText.AsSpan());
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class SharedInputConnection(AndroidSkiaHostView owner) : BaseInputConnection(owner, false)
    {
        private string? lastTextArgument;

        // ModernFormsNext owns all editable state in TextBoxDocument. Returning null prevents
        // inherited helpers from silently creating BaseInputConnection's private fake Editable.
        public override global::Android.Text.IEditable? Editable => null;

        internal int BatchDepth { get; private set; }

        public override bool BeginBatchEdit()
            => Trace("BeginBatchEdit", "ImeInputConnection", string.Empty, () =>
            {
                BatchDepth++;
                return true;
            });

        public override bool EndBatchEdit()
            => Trace("EndBatchEdit", "ImeInputConnection", string.Empty, () =>
            {
                if (BatchDepth == 0)
                    return false;

                BatchDepth--;
                owner.FlushTextStateNotification();
                return BatchDepth > 0;
            });

        public override bool CommitText(ICharSequence? text, int newCursorPosition)
        {
            var value = text?.ToString() ?? string.Empty;
            return Trace(
                "CommitText",
                "ImeInputConnection",
                $"newCursorPosition={newCursorPosition}",
                () =>
                {
                    owner.PublishCommittedText(value, newCursorPosition);
                    return true;
                },
                value,
                newCursorPosition);
        }

        public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
        {
            var value = text?.ToString() ?? string.Empty;
            return Trace(
                "SetComposingText",
                "ImeInputConnection",
                $"newCursorPosition={newCursorPosition}",
                () =>
                {
                    owner.PublishComposingText(value, newCursorPosition);
                    return true;
                },
                value,
                newCursorPosition);
        }

        public override bool SetComposingRegion(int start, int end)
            => Trace(
                "SetComposingRegion",
                "ImeInputConnection",
                $"start={start}; end={end}",
                () =>
                {
                    owner.PublishComposingRegion(start, end);
                    return true;
                });

        public override bool FinishComposingText()
            => Trace("FinishComposingText", "ImeInputConnection", string.Empty, () =>
            {
                owner.PublishCompositionFinished();
                return true;
            });

        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
            => Trace(
                "DeleteSurroundingText",
                "ImeInputConnection",
                $"beforeLength={beforeLength}; afterLength={afterLength}",
                () =>
                {
                    if (beforeLength < 0 || afterLength < 0)
                        return false;

                    owner.PublishDeletion(new AndroidTextDeletionRequest(beforeLength, afterLength));
                    return true;
                });

        public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
            => Trace(
                "DeleteSurroundingTextInCodePoints",
                "ImeInputConnection",
                $"beforeLength={beforeLength}; afterLength={afterLength}",
                () =>
                {
                    if (beforeLength < 0 || afterLength < 0)
                        return false;

                    owner.PublishDeletion(owner.GetTextInputState().GetUtf16DeletionForCodePoints(beforeLength, afterLength));
                    return true;
                });

        public override ICharSequence? GetTextBeforeCursorFormatted(int length, GetTextFlags flags)
            => Trace(
                "GetTextBeforeCursor",
                "ImeInputConnection",
                $"length={length}; flags={flags}",
                () => new Java.Lang.String(owner.GetTextInputState().GetTextBeforeCursor(Math.Max(0, length))),
                resultFormatter: value => value.ToString());

        public override ICharSequence? GetTextAfterCursorFormatted(int length, GetTextFlags flags)
            => Trace(
                "GetTextAfterCursor",
                "ImeInputConnection",
                $"length={length}; flags={flags}",
                () => new Java.Lang.String(owner.GetTextInputState().GetTextAfterCursor(Math.Max(0, length))),
                resultFormatter: value => value.ToString());

        public override ICharSequence? GetSelectedTextFormatted(GetTextFlags flags)
            => Trace(
                "GetSelectedText",
                "ImeInputConnection",
                $"flags={flags}",
                () => new Java.Lang.String(owner.GetTextInputState().GetSelectedText()),
                resultFormatter: value => value.ToString());

        public override ExtractedText? GetExtractedText(ExtractedTextRequest? request, GetTextFlags flags)
            => Trace(
                "GetExtractedText",
                "ImeInputConnection",
                $"token={request?.Token ?? 0}; flags={flags}; hintMaxChars={request?.HintMaxChars ?? 0}; " +
                  $"hintMaxLines={request?.HintMaxLines ?? 0}",
                () =>
                {
                    var inputState = owner.GetTextInputState();
                    return new ExtractedText
                    {
                        Text = new Java.Lang.String(inputState.Text),
                        StartOffset = 0,
                        PartialStartOffset = -1,
                        PartialEndOffset = -1,
                        SelectionStart = inputState.SelectionStart,
                        SelectionEnd = inputState.SelectionEnd
                    };
                },
                resultFormatter: value => value is null
                    ? "null"
                    : $"text={value.Text}; selection={value.SelectionStart}..{value.SelectionEnd}; " +
                      $"partial={value.PartialStartOffset}..{value.PartialEndOffset}");

        public override bool RequestCursorUpdates(int cursorUpdateMode)
            => Trace(
                "RequestCursorUpdates",
                "ImeInputConnection",
                $"cursorUpdateMode={cursorUpdateMode}",
                () => base.RequestCursorUpdates(cursorUpdateMode));

        public override bool SetSelection(int start, int end)
            => Trace("SetSelection", "ImeInputConnection", $"start={start}; end={end}", () =>
            {
                var inputState = owner.GetTextInputState();
                if (start < 0 || end < 0 || start > inputState.Text.Length || end > inputState.Text.Length)
                    return false;

                owner.PublishSelection(start, end);
                return true;
            });

        public override bool SendKeyEvent(NativeKeyEvent? e)
        {
            var observation = e is null
                ? (KeyEventObservation?)null
                : owner.ObserveKeyEvent(e.KeyCode, e.Action == KeyEventActions.Down, "InputConnectionKeyEvent");
            return Trace(
                "SendKeyEvent",
                "ImeInputConnectionKeyEvent",
                e is null
                    ? "event=null"
                    : $"keyCode={e.KeyCode}; action={e.Action}; unicodeChar={e.UnicodeChar}; deviceId={e.DeviceId}",
                () =>
                {
                    if (e is not null && owner.PublishKey(e.KeyCode, e.Action == KeyEventActions.Down))
                        return true;

                    return base.SendKeyEvent(e);
                },
                operationKeyEvent: observation);
        }

        private T Trace<T>(
            string method,
            string source,
            string arguments,
            Func<T> operation,
            string? argumentText = null,
            int? newCursorPosition = null,
            Func<T, string?>? resultFormatter = null,
            KeyEventObservation? operationKeyEvent = null)
        {
            if (!owner.EnableInputConnectionDiagnostics)
                return operation();

            var before = owner.GetTextInputState();
            var batchDepthBefore = BatchDepth;
            var sameTextArgument = argumentText is not null && argumentText == lastTextArgument;
            T result = default!;
            string? exception = null;
            try
            {
                result = operation();
                return result;
            }
            catch (System.Exception error)
            {
                exception = error.ToString();
                throw;
            }
            finally
            {
                var after = owner.GetTextInputState();
                owner.WriteInputDiagnostic(
                    method,
                    source,
                    arguments,
                    before,
                    after,
                    batchDepthBefore,
                    BatchDepth,
                    exception is null ? resultFormatter?.Invoke(result) ?? result?.ToString() : null,
                    argumentText,
                    newCursorPosition,
                    sameTextArgument,
                    operationKeyEvent,
                    exception);
                if (argumentText is not null)
                    lastTextArgument = argumentText;
            }
        }
    }

    private readonly record struct KeyEventObservation(
        DateTimeOffset Timestamp,
        Keycode KeyCode,
        KeyEventActions Action,
        string Source);
}
