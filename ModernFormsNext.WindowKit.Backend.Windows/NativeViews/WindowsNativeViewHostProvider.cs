using System.ComponentModel;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Windows;

internal sealed class WindowsNativeViewHostProvider(IntPtr window) : INativeViewHostProvider, IDisposable
{
    private readonly IntPtr parentWindow = window;
    private readonly List<Session> sessions = [];
    private bool disposed;
    public NativeViewCapabilities Capabilities => NativeViewCapabilities.Baseline;
    public bool IsAvailable => !disposed && NativeViewInterop.IsWindow(parentWindow);
    public bool IsVisible => IsAvailable;
    public bool IsEnabled => IsAvailable && NativeViewInterop.IsWindowEnabled(parentWindow);
    public event Action? Changed;
    internal void NotifyState() => Changed?.Invoke();
    internal bool Contains(IntPtr handle) => sessions.Any(s => s.Contains(handle));
    public void FocusFramework() { if (IsAvailable) NativeViewInterop.SetFocus(parentWindow); }
    public INativeViewSession CreateSession(INativeViewFactory factory, INativeViewHostCallbacks callbacks)
    {
        if (!IsAvailable) throw new InvalidOperationException("The Windows presentation is unavailable.");
        var session = new Session(this, factory, callbacks);
        sessions.Add(session);
        return session;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        List<Exception>? failures = null;
        foreach (var session in sessions.ToArray())
            try { session.Dispose(); } catch (Exception error) { (failures ??= []).Add(error); }
        try { Changed?.Invoke(); } catch (Exception error) { (failures ??= []).Add(error); }
        Changed = null;
        if (failures is not null) throw new AggregateException("Native Windows hosting cleanup failed.", failures);
    }

    private sealed class Session : INativeViewSession
    {
        private readonly WindowsNativeViewHostProvider owner;
        private readonly INativeViewHostCallbacks callbacks;
        private readonly WindowsNativeViewSite site;
        private readonly NativeViewInterop.Hook focusHook, messageHook;
        private IntPtr container, focusRegistration, messageRegistration;
        private INativeViewPeer? peer;
        private NativeViewPlacement? last;
        private IntPtr lastAfter = new(-2);
        private bool disposed, requestingFocus;

        internal Session(WindowsNativeViewHostProvider owner, INativeViewFactory factory, INativeViewHostCallbacks callbacks)
        {
            this.owner = owner; this.callbacks = callbacks;
            container = NativeViewInterop.CreateWindowEx(0, "STATIC", "", 0x46000000,
                0, 0, 1, 1, owner.parentWindow, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (container == IntPtr.Zero) throw new Win32Exception();
            site = new(container, callbacks);
            focusHook = Focus; messageHook = Message;
            try
            {
                peer = factory.CreatePeer(site) ?? throw new InvalidOperationException("The factory returned no peer lease.");
                focusRegistration = NativeViewInterop.SetWindowsHookEx(5, focusHook, IntPtr.Zero, NativeViewInterop.GetCurrentThreadId());
                messageRegistration = NativeViewInterop.SetWindowsHookEx(3, messageHook, IntPtr.Zero, NativeViewInterop.GetCurrentThreadId());
                if (focusRegistration == IntPtr.Zero || messageRegistration == IntPtr.Zero) throw new Win32Exception();
            }
            catch { Dispose(); throw; }
        }
        internal bool Contains(IntPtr handle) => !disposed && handle != IntPtr.Zero &&
            (handle == container || NativeViewInterop.IsChild(container, handle));
        public void Update(NativeViewPlacement placement)
        {
            if (disposed || !site.IsCurrent) return;
            var old = last; last = placement;
            if (old?.PixelBounds != placement.PixelBounds)
            {
                var b = placement.PixelBounds;
                NativeViewInterop.Check(NativeViewInterop.SetWindowPos(container, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, 0x14));
                if (old?.PixelBounds.Size != b.Size) peer?.Resize(b.Size);
                if (disposed || !site.IsCurrent) return;
            }
            if (old?.PixelClip != placement.PixelClip)
            {
                var c = placement.PixelClip;
                var region = NativeViewInterop.CreateRectRgn(c.X, c.Y, c.Right, c.Bottom);
                if (region == IntPtr.Zero) throw new Win32Exception();
                // SetWindowRgn transfers HRGN ownership only on success.
                if (NativeViewInterop.SetWindowRgn(container, region, true) == 0)
                { NativeViewInterop.DeleteObject(region); throw new Win32Exception(); }
            }
            if (disposed || !site.IsCurrent) return;
            if (old?.Enabled != placement.Enabled) NativeViewInterop.EnableWindow(container, placement.Enabled);
            if (disposed || !site.IsCurrent) return;
            if (old?.Visible != placement.Visible) NativeViewInterop.ShowWindow(container, placement.Visible ? 5 : 0);
        }
        public void RequestFocus()
        {
            if (disposed || !callbacks.IsFocused || last is not { Visible: true, Enabled: true } || requestingFocus) return;
            requestingFocus = true;
            try
            {
                peer?.RequestFocus();
                // Async/non-focusable peers still need a native keyboard boundary while the
                // canonical host owns focus. Do not leave input on the underlying Skia HWND.
                if (!disposed && callbacks.IsFocused && !Contains(NativeViewInterop.GetFocus()))
                    NativeViewInterop.SetFocus(container);
            }
            finally { requestingFocus = false; }
        }
        public void ReturnFocus() { if (Contains(NativeViewInterop.GetFocus())) owner.FocusFramework(); }
        public void PlaceAbove(INativeViewSession? previous)
        {
            if (disposed) return;
            // Inserting back-to-front immediately beneath the preceding host would reverse the
            // control order. Put the foremost host at HWND_TOP and the following earlier host
            // behind it; the shared order invokes us front-to-back.
            IntPtr after = previous is Session session ? session.container : IntPtr.Zero;
            if (after == lastAfter) return;
            lastAfter = after;
            NativeViewInterop.Check(NativeViewInterop.SetWindowPos(container, after, 0, 0, 0, 0, 0x13));
        }
        private IntPtr Focus(int code, IntPtr gaining, IntPtr losing)
        {
            if (code == 9 && Contains(gaining) && !requestingFocus)
            {
                // CBT runs before native focus commits, so a validation veto prevents the
                // native editor from becoming active at all. Never unwind through Win32.
                try { if (last is not { Visible: true, Enabled: true } || !site.TryFocus()) return new IntPtr(1); }
                catch { return new IntPtr(1); }
            }
            return NativeViewInterop.CallNextHookEx(focusRegistration, code, gaining, losing);
        }
        private IntPtr Message(int code, IntPtr removed, IntPtr pointer)
        {
            if (code >= 0 && removed != IntPtr.Zero && !disposed)
            {
                var message = Marshal.PtrToStructure<NativeViewInterop.Message>(pointer);
                if (message.Id == 0x100 && message.WParam.ToUInt64() == 9 && Contains(message.Window))
                {
                    bool forward = NativeViewInterop.GetKeyState(0x10) >= 0;
                    try
                    {
                        if (!(peer?.TryMoveFocus(forward) ?? false)) site.MoveFocus(forward);
                        message.Id = 0; // WM_NULL: do not deliver Tab again to the native peer.
                        Marshal.StructureToPtr(message, pointer, false);
                    }
                    catch { /* Existing canonical transaction retains its owner after a veto/failure. */ }
                }
            }
            return NativeViewInterop.CallNextHookEx(messageRegistration, code, removed, pointer);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; site.Retired = true;
            owner.sessions.Remove(this);
            if (focusRegistration != IntPtr.Zero) NativeViewInterop.UnhookWindowsHookEx(focusRegistration);
            if (messageRegistration != IntPtr.Zero) NativeViewInterop.UnhookWindowsHookEx(messageRegistration);
            focusRegistration = messageRegistration = IntPtr.Zero;
            var previous = peer; peer = null;
            try { previous?.Dispose(); }
            finally
            {
                var handle = container; container = IntPtr.Zero;
                if (NativeViewInterop.IsWindow(handle)) NativeViewInterop.Check(NativeViewInterop.DestroyWindow(handle));
            }
        }
    }
}
