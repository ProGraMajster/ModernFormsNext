using Android.Views;
using NativeKeyEvent = Android.Views.KeyEvent;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

public sealed partial class AndroidSkiaHostView
{
    private Func<AndroidInputKeyEvent, bool>? keyInputHandler;
    private readonly AndroidKeyboardPressState keyboardPressState = new();
    private long keyboardGeneration;
    private bool resettingKeyboard;

    /// <summary>Gets or sets the synchronous primary key sink on the Android main thread.</summary>
    /// <remarks>
    /// When supplied, this handler receives all supported key identities and replaces the legacy
    /// <see cref="KeyInput"/> event. Return true to consume native View input, including suppressed
    /// key releases; false permits native fallback while this view, focus and client remain current.
    /// Exceptions propagate without fallback or automatic retry. The handler must not block.
    /// InputConnection-origin transitions remain editing input: once delivered they are accepted
    /// exactly once regardless of this result, avoiding BaseInputConnection redispatch to the View.
    /// This API does not convert letters to text; committed text remains on the IME client path.
    /// Replacing the handler resets shared keyboard state through <see cref="KeyboardStateReset"/>.
    /// Hardware releases lacking a matching key/device press in this active lifetime are canceled;
    /// orphan repeats are ignored until a new initial press. The bounded native pairing capacity
    /// is 256 simultaneous pairs; saturation resets shared state and vetoes that event without retry.
    /// Compatibility events and IME transitions retain their existing unpaired delivery semantics.
    /// </remarks>
    public Func<AndroidInputKeyEvent, bool>? KeyInputHandler
    {
        get => keyInputHandler;
        set
        {
            VerifyTextInputThread();
            ThrowIfDisposed();
            if (ReferenceEquals(keyInputHandler, value)) return;
            keyInputHandler = value;
            ResetKeyboardState();
        }
    }

    /// <summary>Occurs when the shared host must discard consumed presses and cancel keyboard interaction.</summary>
    /// <remarks>
    /// Raised on the Android main thread after pause/stop, native View or window focus loss,
    /// detach, handler replacement, and managed disposal commit their state. Subscribe the
    /// existing surface's keyboard-reset operation; do not synthesize ordinary key-up activation.
    /// All captured subscribers run even when an earlier subscriber throws. Reentrant resets
    /// invalidate pending routes without recursively notifying subscribers. Finalization raises
    /// no application callbacks. Subscribe and unsubscribe on the Android main thread.
    /// </remarks>
    public event EventHandler? KeyboardStateReset;

    private bool CanRouteKeyboard => !disposed && !resettingKeyboard &&
        state.LifecycleState == AndroidSurfaceLifecycleState.Resumed &&
        state.IsSurfaceAttached && IsAttachedToWindow && IsFocused && HasWindowFocus;

    private void InvalidateKeyboardRoute()
    {
        keyboardGeneration++;
        keyboardPressState.Reset();
    }

    private void ResetKeyboardState()
    {
        InvalidateKeyboardRoute();
        if (resettingKeyboard) return;
        resettingKeyboard = true;
        try { AndroidSurfaceCleanup.ResetKeyboard(this, KeyboardStateReset); }
        finally { resettingKeyboard = false; }
    }

    private void ResetKeyboardFromNativeCallback()
    {
        if (disposed) return;
        try { ResetKeyboardState(); }
        catch (Exception exception)
        {
            // The native focus transaction already completed. One failed subscriber cannot
            // interrupt Android's remaining focus/teardown bookkeeping.
            try { AndroidLogger.Write($"Keyboard reset failed ({exception.GetType().Name}).", diagnosticSink); }
            catch { /* Diagnostics cannot interrupt native focus changes. */ }
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool gainFocus, FocusSearchDirection direction,
        global::Android.Graphics.Rect? previouslyFocusedRect)
    {
        if (!gainFocus) InvalidateKeyboardRoute();
        try { base.OnFocusChanged(gainFocus, direction, previouslyFocusedRect); }
        finally { if (!gainFocus) ResetKeyboardFromNativeCallback(); }
    }

    /// <inheritdoc/>
    public override void OnWindowFocusChanged(bool hasWindowFocus)
    {
        if (!hasWindowFocus) InvalidateKeyboardRoute();
        try { base.OnWindowFocusChanged(hasWindowFocus); }
        finally { if (!hasWindowFocus) ResetKeyboardFromNativeCallback(); }
    }

    private bool PublishKey(Keycode keyCode, bool isDown, NativeKeyEvent? nativeEvent,
        bool fromInputConnection = false)
    {
        var generation = keyboardGeneration;
        var clientGeneration = textInputGeneration;
        bool IsCurrent() => generation == keyboardGeneration &&
            clientGeneration == textInputGeneration && CanRouteKeyboard;
        var translated = AndroidInputKeyMapper.FromNative((int)keyCode, isDown,
            nativeEvent is null ? 0 : (int)NativeKeyEvent.NormalizeMetaState(nativeEvent.MetaState),
            nativeEvent?.DeviceId ?? -1,
            nativeEvent is not null && (nativeEvent.Flags & KeyEventFlags.SoftKeyboard) != 0,
            fromInputConnection, nativeEvent?.RepeatCount ?? 0,
            nativeEvent is not null && (nativeEvent.Flags & KeyEventFlags.Canceled) != 0,
            nativeEvent is not null && (nativeEvent.UnicodeChar & unchecked((int)0x80000000)) != 0);
        return AndroidKeyboardEventDispatch.Publish(this, translated, keyInputHandler, KeyInput,
            IsCurrent, NotifyTextStateChanged, fromInputConnection, keyboardPressState, ResetKeyboardState);
    }

    private bool ProcessViewKeyEvent(Keycode keyCode, NativeKeyEvent? keyEvent, bool isDown,
        Func<bool> baseHandler)
    {
        if (!CanRouteKeyboard) return false;
        var generation = keyboardGeneration;
        var clientGeneration = textInputGeneration;
        bool IsCurrent() => generation == keyboardGeneration &&
            clientGeneration == textInputGeneration && CanRouteKeyboard;
        bool Dispatch()
        {
            if (!IsCurrent()) return true;
            var result = PublishKey(keyCode, isDown, keyEvent);
            // Native fallback is part of this captured route. An unhandled callback that
            // changes native focus/client/lifetime cannot continue against a different owner.
            return result || !IsCurrent() || baseHandler();
        }
        if (!EnableInputConnectionDiagnostics) return Dispatch();

        var before = GetTextInputState();
        var batchDepth = activeInputConnection?.BatchDepth ?? 0;
        var observation = ObserveKeyEvent(keyCode, isDown, "ViewKeyEvent");
        var result = Dispatch();
        if (!IsCurrent()) return result;
        WriteInputDiagnostic(isDown ? "OnKeyDown" : "OnKeyUp", "ViewKeyEvent",
            $"keyCode={keyCode}; deviceId={keyEvent?.DeviceId ?? -1}; " +
            $"source={keyEvent?.Source}; flags={keyEvent?.Flags}; metaState={keyEvent?.MetaState}; " +
            $"repeatCount={keyEvent?.RepeatCount ?? 0}",
            before, GetTextInputState(), batchDepth, batchDepth, result.ToString(),
            operationKeyEvent: observation);
        return result;
    }
}
