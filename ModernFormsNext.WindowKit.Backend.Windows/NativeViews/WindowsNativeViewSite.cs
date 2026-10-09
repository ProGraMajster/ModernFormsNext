using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Windows;

/// <summary>Provides the Windows container HWND to feature adapters such as a future browser controller.</summary>
/// <remarks>The backend owns ParentWindow. All access requires its UI thread; do not destroy or retain it after retirement.</remarks>
public sealed class WindowsNativeViewSite : INativeViewSite
{
    private readonly INativeViewHostCallbacks callbacks;
    internal bool Retired;
    internal WindowsNativeViewSite(IntPtr parent, INativeViewHostCallbacks callbacks)
    { ParentWindow = parent; this.callbacks = callbacks; }
    /// <summary>Gets the child container HWND; adapters create their peer beneath this handle.</summary>
    public IntPtr ParentWindow { get; }
    /// <inheritdoc/>
    public long Generation => callbacks.Generation;
    /// <inheritdoc/>
    public bool IsCurrent => !Retired && callbacks.IsCurrent;
    /// <inheritdoc/>
    public bool IsFocused => IsCurrent && callbacks.IsFocused;
    /// <inheritdoc/>
    public bool TryFocus() => IsCurrent && callbacks.TryFocus();
    /// <inheritdoc/>
    public bool MoveFocus(bool forward) => IsCurrent && callbacks.MoveFocus(forward);
    /// <inheritdoc/>
    public void RestoreFocus() { if (IsCurrent) callbacks.RestoreFocus(); }
}

/// <summary>Adapts an existing child HWND with explicit resource ownership.</summary>
/// <remarks>
/// Construct in INativeViewFactory.CreatePeer on the UI thread. The HWND must have WS_CHILD.
/// The session owns this lease. Borrowed windows are hidden and returned to their original
/// parent on disposal; owned windows are destroyed. This type does not own the site.
/// The original parent must outlive a borrowed lease. Ownership transfers only after successful construction.
/// </remarks>
public sealed class WindowsHwndViewPeer : INativeViewPeer
{
    private IntPtr handle;
    private readonly IntPtr originalParent;
    private readonly bool ownsWindow;
    private PixelSize? lastSize;
    /// <summary>Attaches a child HWND to the site's container.</summary>
    /// <param name="site">The current Windows hosting site.</param>
    /// <param name="childWindow">A real child HWND on the same UI thread.</param>
    /// <param name="ownsWindow">True transfers destruction to the lease; false borrows the window.</param>
    public WindowsHwndViewPeer(WindowsNativeViewSite site, IntPtr childWindow, bool ownsWindow)
    {
        ArgumentNullException.ThrowIfNull(site);
        if (!site.IsCurrent || !NativeViewInterop.IsWindow(childWindow) ||
            (NativeViewInterop.GetWindowLong(childWindow, -16) & 0x40000000) == 0 ||
            NativeViewInterop.GetWindowThreadProcessId(childWindow, out _) != NativeViewInterop.GetCurrentThreadId())
            throw new ArgumentException("A current same-thread child HWND is required.", nameof(childWindow));
        handle = childWindow;
        originalParent = NativeViewInterop.GetParent(handle);
        this.ownsWindow = ownsWindow;
        NativeViewInterop.SetParent(handle, site.ParentWindow);
        if (NativeViewInterop.GetParent(handle) != site.ParentWindow)
            throw new System.ComponentModel.Win32Exception();
        NativeViewInterop.ShowWindow(handle, 5);
    }
    /// <inheritdoc/>
    public void Resize(PixelSize size)
    {
        if (handle == IntPtr.Zero || lastSize == size) return;
        lastSize = size;
        if (handle != IntPtr.Zero)
            NativeViewInterop.Check(NativeViewInterop.SetWindowPos(handle, IntPtr.Zero, 0, 0,
                size.Width, size.Height, 0x14));
    }
    /// <inheritdoc/>
    public void RequestFocus() { if (handle != IntPtr.Zero) NativeViewInterop.SetFocus(handle); }
    /// <inheritdoc/>
    public bool TryMoveFocus(bool forward) => false;
    /// <summary>Detaches a borrowed HWND or destroys an owned HWND exactly once.</summary>
    public void Dispose()
    {
        var previous = handle;
        handle = IntPtr.Zero;
        if (previous == IntPtr.Zero || !NativeViewInterop.IsWindow(previous)) return;
        NativeViewInterop.ShowWindow(previous, 0);
        if (ownsWindow) NativeViewInterop.Check(NativeViewInterop.DestroyWindow(previous));
        else
        {
            NativeViewInterop.SetParent(previous, originalParent);
            if (NativeViewInterop.GetParent(previous) != originalParent)
                throw new System.ComponentModel.Win32Exception();
        }
    }
}
