namespace ModernFormsNext.VisualStudioExtension.Hosting;

/// <summary>
/// Identifies the current state of a Visual Studio child-window attachment.
/// </summary>
internal enum VisualStudioDesignerHostState
{
    Detached,
    Attached,
    Faulted,
    Disposed
}

/// <summary>
/// Performs platform operations needed to embed a ModernFormsNext window in Visual Studio.
/// </summary>
internal interface IVisualStudioNativeWindowOperations
{
    void Attach(IntPtr childHandle, IntPtr parentHandle);

    void Resize(IntPtr childHandle, int width, int height);

    void Focus(IntPtr childHandle);

    void Detach(IntPtr childHandle);
}

/// <summary>
/// Coordinates attach, resize, DPI, focus, detach, and failure state for one hosted window.
/// </summary>
/// <remarks>
/// Keeping this state machine independent from WinForms and the ModernFormsNext designer shell
/// makes close/reopen and initialization-failure behavior deterministic and unit-testable.
/// </remarks>
internal sealed class VisualStudioDesignerHostLifecycle : IDisposable
{
    private readonly IVisualStudioNativeWindowOperations nativeOperations;
    private IntPtr childHandle;

    public VisualStudioDesignerHostLifecycle(IVisualStudioNativeWindowOperations nativeOperations)
    {
        this.nativeOperations = nativeOperations ?? throw new ArgumentNullException(nameof(nativeOperations));
    }

    public VisualStudioDesignerHostState State { get; private set; }

    public int Dpi { get; private set; } = 96;

    public string? LastDiagnostic { get; private set; }

    public void Attach(IntPtr childWindowHandle, IntPtr parentWindowHandle)
    {
        ThrowIfDisposed();

        if (childWindowHandle == IntPtr.Zero)
            throw new ArgumentException("The hosted ModernFormsNext window handle cannot be zero.", nameof(childWindowHandle));
        if (parentWindowHandle == IntPtr.Zero)
            throw new ArgumentException("The Visual Studio parent window handle cannot be zero.", nameof(parentWindowHandle));

        if (State == VisualStudioDesignerHostState.Attached)
            Detach();

        childHandle = childWindowHandle;
        LastDiagnostic = null;

        try
        {
            nativeOperations.Attach(childWindowHandle, parentWindowHandle);
            State = VisualStudioDesignerHostState.Attached;
        }
        catch (Exception ex)
        {
            TryDetachAfterFailure(childWindowHandle);
            childHandle = IntPtr.Zero;
            State = VisualStudioDesignerHostState.Faulted;
            LastDiagnostic = $"Could not attach the Designer HWND: {ex.Message}";
            throw new InvalidOperationException(LastDiagnostic, ex);
        }
    }

    public void Resize(int width, int height)
    {
        ThrowIfDisposed();

        if (State != VisualStudioDesignerHostState.Attached)
            return;

        try
        {
            nativeOperations.Resize(childHandle, Math.Max(1, width), Math.Max(1, height));
        }
        catch (Exception ex)
        {
            LastDiagnostic = $"Could not resize the Designer HWND: {ex.Message}";
            throw new InvalidOperationException(LastDiagnostic, ex);
        }
    }

    public void UpdateDpi(int dpi, int width, int height)
    {
        ThrowIfDisposed();

        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), "Designer host DPI must be positive.");

        Dpi = dpi;
        Resize(width, height);
    }

    public void Focus()
    {
        ThrowIfDisposed();

        if (State != VisualStudioDesignerHostState.Attached)
            return;

        try
        {
            nativeOperations.Focus(childHandle);
        }
        catch (Exception ex)
        {
            LastDiagnostic = $"Could not focus the Designer HWND: {ex.Message}";
            throw new InvalidOperationException(LastDiagnostic, ex);
        }
    }

    public void Detach()
    {
        ThrowIfDisposed();

        if (childHandle != IntPtr.Zero)
        {
            try
            {
                nativeOperations.Detach(childHandle);
            }
            catch (Exception ex)
            {
                LastDiagnostic = $"Could not detach the Designer HWND cleanly: {ex.Message}";
            }
        }

        childHandle = IntPtr.Zero;
        State = VisualStudioDesignerHostState.Detached;
    }

    public void Dispose()
    {
        if (State == VisualStudioDesignerHostState.Disposed)
            return;

        if (childHandle != IntPtr.Zero)
        {
            try
            {
                nativeOperations.Detach(childHandle);
            }
            catch (Exception ex)
            {
                LastDiagnostic = $"Could not detach the Designer HWND during disposal: {ex.Message}";
            }
        }

        childHandle = IntPtr.Zero;
        State = VisualStudioDesignerHostState.Disposed;
    }

    private void TryDetachAfterFailure(IntPtr failedChildHandle)
    {
        try
        {
            nativeOperations.Detach(failedChildHandle);
        }
        catch
        {
            // Preserve the original attach failure. The production implementation restores any
            // parent/style state it managed to change before the failure was reported.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(State == VisualStudioDesignerHostState.Disposed, this);
    }
}
