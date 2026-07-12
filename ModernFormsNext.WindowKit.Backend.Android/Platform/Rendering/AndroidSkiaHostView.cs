using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Views;
using Android.Views.InputMethods;
using SkiaSharp.Views.Android;
using ICharSequence = Java.Lang.ICharSequence;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Hosts a density-aware Skia canvas in one Android view and translates lifecycle, pointer, resize,
/// and IME input into platform-neutral events.
/// </summary>
/// <remarks>
/// The view is a transition surface for bringing the shared ModernFormsNext renderer to Android;
/// it is not a complete Android implementation of the framework window/control stack. Create and
/// use it on the Android main thread. The host owns the view and must dispose it from the activity.
/// Rendering occurs only after explicit invalidation or a size change, not on a continuous timer.
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

    /// <summary>Occurs when the Android IME requests deletion around the cursor.</summary>
    public event EventHandler? DeleteBackwardRequested;

    /// <summary>Gets the deterministic state exposed for diagnostics.</summary>
    public AndroidSurfaceHostState HostState => state;

    /// <summary>Notifies the surface that its activity resumed.</summary>
    public void ResumeHost()
    {
        ThrowIfDisposed();
        state.Resume();
        AndroidLogger.Write("Skia surface resumed.", diagnosticSink);
        if (state.IsInvalidationPending)
            PostInvalidateOnAnimation();
        else
            RequestRender();
    }

    /// <summary>Notifies the surface that its activity paused and cancels active pointers.</summary>
    public void PauseHost()
    {
        ThrowIfDisposed();
        EmitCancellations(state.Pause());
        AndroidLogger.Write("Skia surface paused.", diagnosticSink);
    }

    /// <summary>Notifies the surface that its activity stopped.</summary>
    public void StopHost()
    {
        ThrowIfDisposed();
        EmitCancellations(state.Stop());
        AndroidLogger.Write("Skia surface stopped.", diagnosticSink);
    }

    /// <summary>Requests a coalesced render on the Android UI thread.</summary>
    public void RequestRender()
    {
        ThrowIfDisposed();
        if (state.RequestInvalidation())
            PostInvalidateOnAnimation();
    }

    /// <summary>Shows Android's soft keyboard for this view.</summary>
    public void ShowSoftKeyboard()
    {
        ThrowIfDisposed();
        RequestFocus();
        var manager = Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
        manager?.ShowSoftInput(this, ShowFlags.Implicit);
    }

    /// <summary>Hides Android's soft keyboard for this view.</summary>
    public void HideSoftKeyboard()
    {
        ThrowIfDisposed();
        var manager = Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
        manager?.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
        ClearFocus();
    }

    /// <inheritdoc/>
    public override bool OnCheckIsTextEditor() => true;

    /// <inheritdoc/>
    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
    {
        if (outAttrs is null)
            return null;

        outAttrs.InputType = global::Android.Text.InputTypes.ClassText |
            global::Android.Text.InputTypes.TextFlagCapSentences |
            global::Android.Text.InputTypes.TextFlagMultiLine;
        outAttrs.ImeOptions = ImeFlags.NoExtractUi;
        return new SharedInputConnection(this);
    }

    /// <inheritdoc/>
    public override bool OnTouchEvent(MotionEvent? motionEvent)
    {
        if (motionEvent is null || disposed || state.LifecycleState != AndroidSurfaceLifecycleState.Resumed)
            return false;

        var action = motionEvent.ActionMasked;
        if (action == MotionEventActions.Cancel)
        {
            EmitCancellations(state.Pause());
            state.Resume();
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
    protected override void OnSizeChanged(int width, int height, int oldWidth, int oldHeight)
    {
        base.OnSizeChanged(width, height, oldWidth, oldHeight);
        if (disposed || state.LifecycleState == AndroidSurfaceLifecycleState.Uninitialized)
            return;

        var density = GetDensity();
        var changed = state.Resize(
            AndroidDensityConverter.ToLogical(width, density),
            AndroidDensityConverter.ToLogical(height, density));
        if (changed)
            PostInvalidateOnAnimation();
        AndroidLogger.Write($"Skia surface resized to {state.LogicalWidth:0.#} x {state.LogicalHeight:0.#} logical pixels.", diagnosticSink);
    }

    /// <inheritdoc/>
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        if (disposed || state.LifecycleState == AndroidSurfaceLifecycleState.Uninitialized)
            return;

        var density = GetDensity();
        if (state.LogicalWidth == 0 && state.LogicalHeight == 0)
        {
            state.Resize(
                AndroidDensityConverter.ToLogical(e.Info.Width, density),
                AndroidDensityConverter.ToLogical(e.Info.Height, density));
        }

        state.CompleteRender();
        e.Surface.Canvas.Save();
        e.Surface.Canvas.Scale(density);
        try
        {
            Render?.Invoke(this, new AndroidSkiaRenderEventArgs(
                e.Surface.Canvas,
                state.LogicalWidth,
                state.LogicalHeight,
                density,
                state.RenderCount));
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
            EmitCancellations(state.Dispose());
            Render = null;
            Pointer = null;
            TextCommitted = null;
            ComposingTextChanged = null;
            DeleteBackwardRequested = null;
            AndroidLogger.Write("Skia surface disposed.", diagnosticSink);
        }

        base.Dispose(disposing);
    }

    private float GetDensity()
        => Resources?.DisplayMetrics?.Density is > 0 and var density ? density : 1f;

    private void EmitPointer(MotionEvent motionEvent, int index, AndroidPointerAction action)
    {
        var pointerId = motionEvent.GetPointerId(index);
        if (!state.TrackPointer(pointerId, action))
            return;

        var density = GetDensity();
        Pointer?.Invoke(this, new AndroidPointerEvent(
            pointerId,
            action,
            AndroidDensityConverter.ToLogical(motionEvent.GetX(index), density),
            AndroidDensityConverter.ToLogical(motionEvent.GetY(index), density),
            index == 0));
    }

    private void EmitCancellations(IReadOnlyList<int> pointerIds)
    {
        foreach (var pointerId in pointerIds)
            Pointer?.Invoke(this, new AndroidPointerEvent(pointerId, AndroidPointerAction.Cancel, 0, 0, pointerId == 0));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class SharedInputConnection(AndroidSkiaHostView owner) : BaseInputConnection(owner, false)
    {
        public override bool CommitText(ICharSequence? text, int newCursorPosition)
        {
            owner.TextCommitted?.Invoke(owner, text?.ToString() ?? string.Empty);
            return true;
        }

        public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
        {
            owner.ComposingTextChanged?.Invoke(owner, text?.ToString() ?? string.Empty);
            return true;
        }

        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
        {
            if (beforeLength > 0)
                owner.DeleteBackwardRequested?.Invoke(owner, EventArgs.Empty);
            return true;
        }
    }
}
