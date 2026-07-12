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
    private readonly App app;
    private readonly SkiaControlSurface controlSurface;
    private readonly AndroidSkiaHostView nativeSurface;
    private bool disposed;

    /// <summary>Creates an Android adapter for a shared application.</summary>
    /// <param name="activity">The current native activity.</param>
    /// <param name="app">The process-owned shared application.</param>
    public AndroidAppHost(Activity activity, App app)
    {
        ArgumentNullException.ThrowIfNull(activity);
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        controlSurface = new SkiaControlSurface(app.Root);
        nativeSurface = new AndroidSkiaHostView(activity);

        controlSurface.Invalidated += OnControlSurfaceInvalidated;
        nativeSurface.Render += OnRender;
        nativeSurface.Pointer += OnPointer;
        nativeSurface.TextCommitted += OnTextCommitted;
        nativeSurface.ComposingTextChanged += OnComposingTextChanged;
        nativeSurface.DeleteBackwardRequested += OnDeleteBackwardRequested;
    }

    /// <summary>Gets the single native view that the activity should display.</summary>
    public AndroidSkiaHostView View => nativeSurface;

    /// <summary>Forwards activity resume to the render surface.</summary>
    public void Resume()
    {
        ThrowIfDisposed();
        nativeSurface.ResumeHost();
        app.RefreshPlatformStatus();
    }

    /// <summary>Forwards activity pause and cancels framework pointer capture.</summary>
    public void Pause()
    {
        ThrowIfDisposed();
        nativeSurface.PauseHost();
        controlSurface.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0);
        app.RefreshPlatformStatus();
    }

    /// <summary>Forwards activity stop to the render surface.</summary>
    public void Stop()
    {
        ThrowIfDisposed();
        nativeSurface.StopHost();
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
        nativeSurface.TextCommitted -= OnTextCommitted;
        nativeSurface.ComposingTextChanged -= OnComposingTextChanged;
        nativeSurface.DeleteBackwardRequested -= OnDeleteBackwardRequested;
        controlSurface.Dispose();
        nativeSurface.Dispose();
    }

    private void OnRender(object? sender, AndroidSkiaRenderEventArgs e)
    {
        var width = Math.Max(0, (int)MathF.Round(e.LogicalWidth));
        var height = Math.Max(0, (int)MathF.Round(e.LogicalHeight));
        controlSurface.Resize(width, height);
        controlSurface.Render(e.Canvas);
    }

    private void OnPointer(object? sender, AndroidPointerEvent e)
    {
        if (!e.IsPrimary && e.Action != AndroidPointerAction.Cancel)
            return;

        var action = e.Action switch
        {
            AndroidPointerAction.Down => ControlSurfacePointerAction.Down,
            AndroidPointerAction.Move => ControlSurfacePointerAction.Move,
            AndroidPointerAction.Up => ControlSurfacePointerAction.Up,
            AndroidPointerAction.Cancel => ControlSurfacePointerAction.Cancel,
            _ => throw new ArgumentOutOfRangeException(nameof(e))
        };
        controlSurface.ProcessPointer(action, (int)MathF.Round(e.X), (int)MathF.Round(e.Y));

        if (action == ControlSurfacePointerAction.Up)
        {
            if (controlSurface.SelectedControl is TextBox)
                nativeSurface.ShowSoftKeyboard();
            else
                nativeSurface.HideSoftKeyboard();
        }
    }

    private void OnTextCommitted(object? sender, string text) => controlSurface.CommitText(text);

    private void OnComposingTextChanged(object? sender, string text)
    {
        // Composition remains owned by Android until CommitText. Rendering intermediate text as a
        // commit would duplicate CJK input and split emoji/grapheme sequences.
    }

    private void OnDeleteBackwardRequested(object? sender, EventArgs e) => controlSurface.DeleteBackward();

    private void OnControlSurfaceInvalidated(object? sender, EventArgs e)
    {
        if (!disposed && nativeSurface.HostState.LifecycleState == AndroidSurfaceLifecycleState.Resumed)
            nativeSurface.RequestRender();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
