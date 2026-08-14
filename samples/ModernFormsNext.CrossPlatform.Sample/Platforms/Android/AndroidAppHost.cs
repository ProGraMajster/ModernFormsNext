using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Connects one Android Skia view to the shared ModernFormsNext control tree.
/// </summary>
/// <remarks>
/// This class contains only surface and input adaptation. It does not construct a second page or
/// use native Android controls to imitate ModernFormsNext widgets.
/// </remarks>
public sealed class AndroidAppHost : IDisposable
{
    /// <summary>
    /// Names the optional boolean activity intent extra that enables sensitive IME diagnostics.
    /// </summary>
    /// <remarks>
    /// Normal launches omit the extra, so input text is never logged by default. The switch exists
    /// only to capture a short, explicitly initiated diagnostic session.
    /// </remarks>
    public const string EnableInputDiagnosticsIntentExtra =
        "com.programajster.modernformsnext.sample.ENABLE_INPUT_DIAGNOSTICS";

    private readonly App app;
    private readonly SkiaControlSurface controlSurface;
    private readonly AndroidSkiaHostView nativeSurface;
    private bool disposed;

    /// <summary>Creates an Android adapter for a shared application.</summary>
    /// <param name="activity">The current native activity.</param>
    /// <param name="app">The process-owned shared application.</param>
    /// <param name="enableInputConnectionDiagnostics">
    /// Whether this host should emit sensitive, full-text IME diagnostics. The default is
    /// <see langword="false"/>.
    /// </param>
    public AndroidAppHost(Activity activity, App app, bool enableInputConnectionDiagnostics = false)
    {
        ArgumentNullException.ThrowIfNull(activity);
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        controlSurface = new SkiaControlSurface(app.Root);
        nativeSurface = new AndroidSkiaHostView(activity)
        {
            EnableInputConnectionDiagnostics = enableInputConnectionDiagnostics
        };
        nativeSurface.TextInputStateProvider = GetTextInputState;

        controlSurface.Invalidated += OnControlSurfaceInvalidated;
        nativeSurface.Render += OnRender;
        nativeSurface.Pointer += OnPointer;
        nativeSurface.TextCommitRequested += OnTextCommitRequested;
        nativeSurface.ComposingTextUpdateRequested += OnComposingTextUpdateRequested;
        nativeSurface.ComposingTextFinished += OnComposingTextFinished;
        nativeSurface.ComposingRegionRequested += OnComposingRegionRequested;
        nativeSurface.DeleteSurroundingTextRequested += OnDeleteSurroundingTextRequested;
        nativeSurface.KeyInput += OnKeyInput;
        nativeSurface.TextSelectionRequested += OnTextSelectionRequested;
    }

    /// <summary>Gets the single native view that the activity should display.</summary>
    public AndroidSkiaHostView View => nativeSurface;

    /// <summary>Forwards activity start to the render surface.</summary>
    public void Start()
    {
        ThrowIfDisposed();
        nativeSurface.StartHost();
        UpdateDiagnostics();
    }

    /// <summary>Forwards activity resume to the render surface.</summary>
    public void Resume()
    {
        ThrowIfDisposed();
        nativeSurface.ResumeHost();
        UpdateDiagnostics();
        app.RefreshPlatformStatus();
    }

    /// <summary>Forwards activity pause and cancels framework pointer capture.</summary>
    public void Pause()
    {
        ThrowIfDisposed();
        nativeSurface.PauseHost();
        controlSurface.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0);
        UpdateDiagnostics();
        app.RefreshPlatformStatus();
    }

    /// <summary>Forwards activity stop to the render surface.</summary>
    public void Stop()
    {
        ThrowIfDisposed();
        nativeSurface.StopHost();
        UpdateDiagnostics();
        app.RefreshPlatformStatus();
    }

    /// <summary>Refreshes density and size after an Android configuration transition.</summary>
    public void ConfigurationChanged()
    {
        ThrowIfDisposed();
        nativeSurface.RefreshConfiguration();
        UpdateDiagnostics();
        app.RefreshPlatformStatus();
    }

    /// <summary>Detaches this activity while preserving the shared <see cref="App"/> tree.</summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        controlSurface.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0);
        controlSurface.Invalidated -= OnControlSurfaceInvalidated;
        nativeSurface.Render -= OnRender;
        nativeSurface.Pointer -= OnPointer;
        nativeSurface.TextCommitRequested -= OnTextCommitRequested;
        nativeSurface.ComposingTextUpdateRequested -= OnComposingTextUpdateRequested;
        nativeSurface.ComposingTextFinished -= OnComposingTextFinished;
        nativeSurface.ComposingRegionRequested -= OnComposingRegionRequested;
        nativeSurface.DeleteSurroundingTextRequested -= OnDeleteSurroundingTextRequested;
        nativeSurface.KeyInput -= OnKeyInput;
        nativeSurface.TextSelectionRequested -= OnTextSelectionRequested;
        nativeSurface.TextInputStateProvider = null;
        // This sample owns one process-wide surface. Snap its global theme transition before
        // detaching so Activity recreation cannot leave non-control scheduler work waiting for a
        // surface that no longer exists.
        ThemeManager.Current.CancelTransition();
        controlSurface.Dispose();
        nativeSurface.Dispose();
    }

    private void OnRender(object? sender, AndroidSkiaRenderEventArgs e)
    {
        var width = Math.Max(0, (int)MathF.Round(e.LogicalWidth));
        var height = Math.Max(0, (int)MathF.Round(e.LogicalHeight));
        controlSurface.Resize(width, height);
        controlSurface.Render(e.Canvas);
        UpdateDiagnostics();
    }

    private void OnPointer(object? sender, AndroidPointerEvent e)
    {
        app.State.ActivePointerCount = nativeSurface.HostState.ActivePointerCount;
        if (e.Action != AndroidPointerAction.Move)
            app.State.LastInput = $"Pointer {e.PointerId}: {e.Action} at {e.X:0.#}, {e.Y:0.#}";

        var action = e.Action switch
        {
            AndroidPointerAction.Down => ControlSurfacePointerAction.Down,
            AndroidPointerAction.Move => ControlSurfacePointerAction.Move,
            AndroidPointerAction.Up => ControlSurfacePointerAction.Up,
            AndroidPointerAction.Cancel => ControlSurfacePointerAction.Cancel,
            _ => throw new ArgumentOutOfRangeException(nameof(e))
        };
        controlSurface.ProcessPointer(e.PointerId, action, (int)MathF.Round(e.X), (int)MathF.Round(e.Y));

        if (action == ControlSurfacePointerAction.Up)
        {
            if (controlSurface.SelectedControl is TextBox)
                nativeSurface.ShowSoftKeyboard();
            else
                nativeSurface.HideSoftKeyboard();
        }

        UpdateDiagnostics();
    }

    private void OnTextCommitRequested(object? sender, AndroidTextEditEvent e)
    {
        controlSurface.CommitText(e.Text, e.NewCursorPosition);
        app.UpdateLastInput($"IME committed {e.Text.Length} UTF-16 unit(s)");
    }

    private void OnComposingTextUpdateRequested(object? sender, AndroidTextEditEvent e)
    {
        controlSurface.SetComposingText(e.Text, e.NewCursorPosition);
        app.UpdateLastInput($"IME composition changed ({e.Text.Length} UTF-16 unit(s))");
    }

    private void OnComposingTextFinished(object? sender, EventArgs e)
    {
        controlSurface.FinishComposingText();
        app.UpdateLastInput("IME composition finished");
    }

    private void OnComposingRegionRequested(object? sender, AndroidTextSelectionEvent e)
    {
        controlSurface.SetComposingRegion(e.Start, e.End);
        app.UpdateLastInput($"IME composition region: {e.Start}..{e.End}");
    }

    private void OnDeleteSurroundingTextRequested(object? sender, AndroidTextDeletionRequest e)
    {
        controlSurface.DeleteSurroundingText(e.BeforeLength, e.AfterLength);
        app.UpdateLastInput($"IME deletion: before {e.BeforeLength}, after {e.AfterLength}");
    }

    private void OnTextSelectionRequested(object? sender, AndroidTextSelectionEvent e)
    {
        controlSurface.SetTextSelection(e.Start, e.End);
        app.UpdateLastInput($"IME selection: {e.Start}..{e.End}");
    }

    private void OnKeyInput(object? sender, AndroidInputKeyEvent e)
    {
        var key = e.Key switch
        {
            AndroidInputKey.Backspace => Keys.Back,
            AndroidInputKey.Delete => Keys.Delete,
            AndroidInputKey.Enter => Keys.Enter,
            AndroidInputKey.Left => Keys.Left,
            AndroidInputKey.Up => Keys.Up,
            AndroidInputKey.Right => Keys.Right,
            AndroidInputKey.Down => Keys.Down,
            _ => throw new ArgumentOutOfRangeException(nameof(e))
        };

        if (e.IsDown)
            controlSurface.ProcessKeyDown(key);
        else
            controlSurface.ProcessKeyUp(key);
        app.UpdateLastInput($"Key {e.Key} {(e.IsDown ? "down" : "up")}");
    }

    private void OnControlSurfaceInvalidated(object? sender, EventArgs e)
    {
        if (!disposed && nativeSurface.HostState.LifecycleState is not
            (AndroidSurfaceLifecycleState.Uninitialized or AndroidSurfaceLifecycleState.Disposed))
            nativeSurface.RequestRender();
    }

    private AndroidTextInputState GetTextInputState()
    {
        var state = controlSurface.GetTextInputState();
        return state is null
            ? new AndroidTextInputState(string.Empty, 0, 0)
            : new AndroidTextInputState(
                state.Value.Text,
                state.Value.SelectionStart,
                state.Value.SelectionEnd,
                state.Value.CompositionStart,
                state.Value.CompositionEnd,
                state.Value.Revision);
    }

    private void UpdateDiagnostics()
    {
        app.UpdateSurfaceDiagnostics(
            nativeSurface.Density,
            nativeSurface.ScaledDensity,
            nativeSurface.HostState.IsSurfaceAttached,
            nativeSurface.HostState.ActivePointerCount,
            nativeSurface.HostState.RenderCount);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
