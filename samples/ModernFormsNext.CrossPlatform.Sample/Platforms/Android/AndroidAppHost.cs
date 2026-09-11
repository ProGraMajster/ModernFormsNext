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
    /// only to capture a short, explicitly initiated diagnostic session. It also emits numeric
    /// inset/view snapshots when their values change, to diagnose keyboard occlusion.
    /// </remarks>
    public const string EnableInputDiagnosticsIntentExtra =
        "com.programajster.modernformsnext.sample.ENABLE_INPUT_DIAGNOSTICS";

    private readonly App app;
    private readonly SkiaControlSurface controlSurface;
    private readonly AndroidSkiaHostView nativeSurface;
    private readonly Func<WindowKit.Input.ITextInputClient?> textInputClientProvider;
    private string? lastInputInsetDiagnostic;
    private bool caretScrollPending;
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
        nativeSurface.AccessibilityHost = controlSurface;
        controlSurface.SetTextInputActive(false);
        controlSurface.AttachTextInputMethod(nativeSurface);
        textInputClientProvider = () => disposed ? null : controlSurface.TextInputClient;
        app.Root.TextInputClientProvider = textInputClientProvider;
        app.Root.UpdateKeyboardOcclusion(nativeSurface.CurrentInsets);

        controlSurface.Invalidated += OnControlSurfaceInvalidated;
        nativeSurface.Render += OnRender;
        nativeSurface.Pointer += OnPointer;
        nativeSurface.KeyInput += OnKeyInput;
        nativeSurface.InsetsChanged += OnInsetsChanged;
        controlSurface.Insets = nativeSurface.CurrentInsets;
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
        controlSurface.SetTextInputActive(true);
        UpdateDiagnostics();
        app.RefreshPlatformStatus();
    }

    /// <summary>Forwards activity pause and cancels framework pointer capture.</summary>
    public void Pause()
    {
        ThrowIfDisposed();
        List<Exception> failures = [];
        Cleanup(() => controlSurface.SetTextInputActive(false), failures);
        Cleanup(nativeSurface.PauseHost, failures);
        Cleanup(() => controlSurface.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0), failures);
        Cleanup(UpdateDiagnostics, failures);
        Cleanup(app.RefreshPlatformStatus, failures);
        ThrowCleanupFailures(failures);
    }

    /// <summary>Forwards activity stop to the render surface.</summary>
    public void Stop()
    {
        ThrowIfDisposed();
        List<Exception> failures = [];
        Cleanup(() => controlSurface.SetTextInputActive(false), failures);
        Cleanup(nativeSurface.StopHost, failures);
        Cleanup(() => controlSurface.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0), failures);
        Cleanup(UpdateDiagnostics, failures);
        Cleanup(app.RefreshPlatformStatus, failures);
        ThrowCleanupFailures(failures);
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
        List<Exception> failures = [];
        if (ReferenceEquals(app.Root.TextInputClientProvider, textInputClientProvider))
        {
            app.Root.TextInputClientProvider = null;
            Cleanup(() => app.Root.UpdateKeyboardOcclusion(default), failures);
        }
        caretScrollPending = false;
        Cleanup(() => controlSurface.AttachTextInputMethod(null), failures);
        Cleanup(() => controlSurface.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0), failures);
        controlSurface.Invalidated -= OnControlSurfaceInvalidated;
        nativeSurface.Render -= OnRender;
        nativeSurface.Pointer -= OnPointer;
        nativeSurface.KeyInput -= OnKeyInput;
        nativeSurface.InsetsChanged -= OnInsetsChanged;
        Cleanup(() => nativeSurface.AccessibilityHost = null, failures);
        // This sample owns one process-wide surface. Snap its global theme transition before
        // detaching so Activity recreation cannot leave non-control scheduler work waiting for a
        // surface that no longer exists.
        Cleanup(() => ThemeManager.Current.CancelTransition(), failures);
        Cleanup(controlSurface.Dispose, failures);
        Cleanup(nativeSurface.Dispose, failures);
        ThrowCleanupFailures(failures);
    }

    // Input/composition observers belong to application code. A failed observer must not retain
    // an obsolete native view or detach the shared tree from its next Activity's ownership.
    private static void Cleanup(Action action, List<Exception> failures)
    {
        try { action(); }
        catch (Exception exception) { failures.Add(exception); }
    }

    private static void ThrowCleanupFailures(List<Exception> failures)
    {
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Android host lifecycle cleanup failed.", failures);
    }

    private void OnRender(object? sender, AndroidSkiaRenderEventArgs e)
    {
        var width = Math.Max(0, (int)MathF.Round(e.LogicalWidth));
        var height = Math.Max(0, (int)MathF.Round(e.LogicalHeight));
        controlSurface.Resize(width, height);
        controlSurface.Render(e.Canvas);
        TraceInputInsets("render");
        UpdateDiagnostics();
    }

    private void OnInsetsChanged(object? sender, WindowKit.WindowInsetsChangedEventArgs e)
    {
        if (!disposed)
        {
            controlSurface.Insets = e.Insets;
            app.Root.UpdateKeyboardOcclusion(e.Insets);
            ScheduleCaretScroll();
            TraceInputInsets("insets-changed");
        }
    }

    private void TraceInputInsets(string source)
    {
        if (disposed || !nativeSurface.EnableInputConnectionDiagnostics ||
            !OperatingSystem.IsAndroidVersionAtLeast(30) || nativeSurface.RootView is not { } root ||
            nativeSurface.RootWindowInsets is not { } nativeInsets) return;

        // Diagnostic-only reads: never request another inset pass or change layout from here.
        // Coalesce identical numeric snapshots so an animated surface does not flood logcat.
        using var keyboard = nativeInsets.GetInsets(global::Android.Views.WindowInsets.Type.Ime());
        int[] location = new int[2];
        int[] rootLocation = new int[2];
        nativeSurface.GetLocationInWindow(location);
        root.GetLocationInWindow(rootLocation);
        var current = nativeSurface.CurrentInsets;
        var values = FormattableString.Invariant($"rootIme={keyboard.Left},{keyboard.Top},{keyboard.Right},{keyboard.Bottom}; currentIme={current.Ime.Left},{current.Ime.Top},{current.Ime.Right},{current.Ime.Bottom}; safe={current.SafeArea.Left},{current.SafeArea.Top},{current.SafeArea.Right},{current.SafeArea.Bottom}; view={location[0]},{location[1]},{nativeSurface.Width},{nativeSurface.Height}; root={rootLocation[0]},{rootLocation[1]},{root.Width},{root.Height}; density={nativeSurface.Density}");
        if (values == lastInputInsetDiagnostic) return;
        lastInputInsetDiagnostic = values;
        global::Android.Util.Log.Info("MFN.IME.Insets", $"source={source}; {values}");
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
            controlSurface.RequestSoftwareKeyboard(controlSurface.TextInputClient is not null);
            ScheduleCaretScroll();
        }

        UpdateDiagnostics();
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

        if ((e.Modifiers & ModernFormsNext.WindowKit.Input.KeyModifiers.Control) != 0) key |= Keys.Control;
        if ((e.Modifiers & ModernFormsNext.WindowKit.Input.KeyModifiers.Shift) != 0) key |= Keys.Shift;
        if ((e.Modifiers & ModernFormsNext.WindowKit.Input.KeyModifiers.Alt) != 0) key |= Keys.Alt;
        if ((e.Modifiers & ModernFormsNext.WindowKit.Input.KeyModifiers.Meta) != 0) key |= Keys.Meta;
        if ((e.Modifiers & ModernFormsNext.WindowKit.Input.KeyModifiers.AltGraph) != 0) key |= Keys.AltGraph;

        if (e.IsDown)
            controlSurface.ProcessKeyDown(key, isTextInput: !e.IsHardwareKey);
        else
            controlSurface.ProcessKeyUp(key, isTextInput: !e.IsHardwareKey);
        app.UpdateLastInput($"Key {e.Key} {(e.IsDown ? "down" : "up")}");
        ScheduleCaretScroll();
    }

    private void OnControlSurfaceInvalidated(object? sender, EventArgs e)
    {
        if (!disposed && nativeSurface.HostState.LifecycleState is not
            (AndroidSurfaceLifecycleState.Uninitialized or AndroidSurfaceLifecycleState.Disposed))
            nativeSurface.RequestRender();
    }

    private void ScheduleCaretScroll()
    {
        if (disposed || caretScrollPending) return;
        caretScrollPending = true;
        try
        {
            app.PlatformServices.Dispatcher.Post(() =>
            {
                caretScrollPending = false;
                if (!disposed) app.Root.ScrollCurrentCaretIntoView();
            });
        }
        catch { caretScrollPending = false; throw; }
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
