using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using SkiaSharp;
using NativeView = Android.Views.View;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

/// <summary>Presents framework Forms in one Activity using the existing Android Skia view.</summary>
/// <remarks>
/// Create in OnCreate after initializing AndroidWindowKit; then call Application.Run once.
/// The Activity owns and must dispose this host from OnDestroy. Forward Start, Resume, Pause,
/// Stop, ConfigurationChanged and WindowFocusChanged. Back calls HandleBack. A replacement
/// host reattaches existing Forms; it never disposes their control trees or starts another loop.
/// All calls require the main thread. Prefer AndroidWindowActivity for automatic forwarding.
/// </remarks>
public sealed class AndroidActivityHost : IDisposable, IAndroidWindowHost
{
    private readonly Activity activity;
    private readonly AndroidWindowingPlatform platform;
    private readonly FrameLayout container;
    private readonly Dictionary<AndroidWindowImpl, Presentation> presentations = [];
    private long generation;
    private bool started, resumed, focused, disposed;
    private global::Android.Window.IOnBackInvokedCallback? backCallback;

    /// <summary>Attaches this Activity's content area to the initialized backend.</summary>
    /// <param name="activity">The Activity owning this host; never retained by the process registry.</param>
    /// <param name="savedState">The OnCreate Bundle for existing bounded lifecycle restoration.</param>
    public AndroidActivityHost(Activity activity, Bundle? savedState = null)
    {
        this.activity = activity ?? throw new ArgumentNullException(nameof(activity));
        platform = AndroidWindowKit.Current.Windowing;
        platform.VerifyAccess();
        // Deliver restoration before application startup even when Android's automatic
        // Application callbacks run after the overridden Activity.OnCreate returns.
        AndroidWindowKit.Current.ActivityTracker.OnActivityCreated(activity, savedState);
        container = new FrameLayout(activity);
        container.LayoutChange += OnLayout;
        try
        {
            generation = platform.Attach(this);
            activity.SetContentView(container);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                backCallback = new BackCallback(this);
                activity.OnBackInvokedDispatcher?.RegisterOnBackInvokedCallback(0, backCallback);
            }
        }
        catch { Dispose(); throw; }
    }

    /// <summary>Gets whether the backend already owns a main Form, including during recreation.</summary>
    public bool HasMainWindow => platform.MainWindow is not null;
    internal bool BeginStartup()
    {
        if (platform.StartupInvoked) return false;
        platform.StartupInvoked = true;
        return true;
    }
    // Resolve only this Form's active presentation, never an unrelated current Activity.
    internal Activity? MessageDialogActivity(AndroidWindowImpl window)
        => Current && resumed && !activity.IsFinishing && !activity.IsDestroyed &&
            window.Visible && !window.IsClosed && presentations.TryGetValue(window, out var p) &&
            !p.Disposed && p.Epoch == window.PresentationEpoch ? activity : null;

    private bool Current => !disposed && platform.IsCurrent(this, generation);
    bool IAndroidWindowHost.CanReplace => disposed || activity.IsChangingConfigurations || activity.IsDestroyed;

    /// <summary>Forwards Activity.OnStart.</summary>
    public void Start()
    {
        Verify(); started = true;
        foreach (var p in presentations.Values.ToArray()) if (!p.Disposed) p.View.StartHost();
    }
    /// <summary>Forwards Activity.OnResume and reacquires native input confirmation.</summary>
    public void Resume()
    {
        Verify(); resumed = true;
        foreach (var p in presentations.Values.ToArray()) if (!p.Disposed) p.View.ResumeHost();
        ActivateTop();
        ConfirmFocus();
    }
    /// <summary>Retires input before Activity.OnPause completes.</summary>
    public void Pause()
    {
        Verify(); resumed = false;
        AndroidWindowingPlatform.Complete(presentations.Values.ToArray().Select(p => (Action)(() =>
        {
            if (p.Disposed) return;
            try { p.Window.ConfirmFocus(false); }
            // Deactivation can synchronously hide this or another popup in the snapshot.
            finally { if (!p.Disposed) p.View.PauseHost(); }
        })));
    }
    /// <summary>Forwards Activity.OnStop and stops rendering.</summary>
    public void Stop()
    {
        Verify(); started = resumed = false;
        AndroidWindowingPlatform.Complete(presentations.Values.ToArray().Select(p => (Action)(() =>
        {
            if (!p.Disposed) p.View.StopHost();
        })));
    }
    /// <summary>Refreshes density, geometry and insets after an in-place configuration change.</summary>
    public void ConfigurationChanged()
    {
        Verify();
        foreach (var p in presentations.Values.ToArray())
            if (!p.Disposed) { p.View.RefreshConfiguration(); Layout(p); p.UpdateGeometry(); }
    }
    /// <summary>Forwards the actual Activity window focus callback, separately from resume.</summary>
    public void WindowFocusChanged(bool hasFocus) { Verify(); focused = hasFocus; ConfirmFocus(); }
    /// <summary>Dismisses the top popup, closes a modal, or requests cancellable main Form closure.</summary>
    /// <returns>True when a framework window handled the request, including canceled closure.</returns>
    public bool HandleBack() { Verify(); return Current && platform.Back(); }

    void IAndroidWindowHost.Present(AndroidWindowImpl window)
    {
        long operation = window.PresentationEpoch;
        if (presentations.TryGetValue(window, out var previous))
        {
            if (previous.Epoch == operation) { ((IAndroidWindowHost)this).Update(window); return; }
            // A descendant's close observer may reshow its owner while an older Hide is
            // still unwinding. Retire the old View, not the newer window/text session.
            presentations.Remove(window);
            previous.Dispose();
        }
        if (window.IsClosed || !window.Visible || operation != window.PresentationEpoch || presentations.ContainsKey(window)) return;
        var p = new Presentation(this, window);
        presentations.Add(window, p);
        try
        {
            container.AddView(p.View, new FrameLayout.LayoutParams(1, 1));
            window.NativeSurfaces = [p.Framebuffer];
            window.TextInput.Attach(p.View);
            // Native text/geometry callbacks can synchronously hide or close this window.
            // Do not continue starting a presentation retired by that newer operation.
            if (p.Disposed || window.IsClosed || !window.Visible) return;
            ((IAndroidWindowHost)this).Update(window);
            if (p.Disposed || window.IsClosed || !window.Visible) return;
            if (started) p.View.StartHost();
            if (resumed) p.View.ResumeHost();
            if (started) p.View.RequestRender();
        }
        catch
        {
            if (presentations.TryGetValue(window, out var failed) && ReferenceEquals(failed, p))
            {
                presentations.Remove(window);
                p.Dispose();
            }
            throw;
        }
    }
    void IAndroidWindowHost.Hide(AndroidWindowImpl window)
    {
        if (presentations.Remove(window, out var p)) p.Dispose();
        if (Current) ActivateTop();
    }
    void IAndroidWindowHost.Update(AndroidWindowImpl window)
    {
        if (!presentations.TryGetValue(window, out var p)) return;
        p.View.Enabled = window.Enabled;
        if (!window.IsPopup && window.Title is not null) activity.Title = window.Title;
        Layout(p);
    }
    void IAndroidWindowHost.Activate(AndroidWindowImpl window)
    {
        if (window.IsPopup) return;
        if (presentations.TryGetValue(window, out var p) && window.Enabled)
        {
            p.View.RequestFocus();
            ConfirmFocus();
        }
    }
    void IAndroidWindowHost.Invalidate(AndroidWindowImpl window)
    {
        if (started && presentations.TryGetValue(window, out var p)) p.View.RequestRender();
    }
    void IAndroidWindowHost.Clear() => Clear();
    void IAndroidWindowHost.Finish() => activity.Finish();

    private void ActivateTop()
    {
        var top = platform.Windows.LastOrDefault(w => w.Visible && !w.IsPopup && w.Enabled && presentations.ContainsKey(w));
        if (top is not null) ((IAndroidWindowHost)this).Activate(top);
    }
    private void ConfirmFocus()
    {
        if (!Current) return;
        // Android may retain the native Window across Activity recreation without repeating
        // an already-true Activity focus callback. Query the actual attached View hierarchy;
        // resumed alone is not evidence of native input focus.
        focused = container.HasWindowFocus;
        var focusedWindow = presentations.Values.LastOrDefault(p => p.View.HasFocus)?.Window;
        while (focusedWindow?.IsPopup == true) focusedWindow = focusedWindow.Owner;
        foreach (var p in presentations.Values.ToArray())
            p.Window.ConfirmFocus(resumed && focused && ReferenceEquals(p.Window, focusedWindow));
    }
    private void OnLayout(object? sender, NativeView.LayoutChangeEventArgs e)
    {
        if (Current) foreach (var p in presentations.Values.ToArray()) Layout(p);
    }
    private void Layout(Presentation p)
    {
        if (disposed || p.Disposed) return;
        var density = p.View.Density;
        int width = container.Width, height = container.Height;
        if (width <= 0 || height <= 0) return;
        int w = width, h = height, x = 0, y = 0;
        if (p.Window.IsPopup || p.Window.IsDialog)
        {
            w = Math.Clamp((int)Math.Ceiling(p.Window.RequestedSize.Width * density), 1, width);
            h = Math.Clamp((int)Math.Ceiling(p.Window.RequestedSize.Height * density), 1, height);
            if (p.Window.IsPopup)
            {
                int[] origin = new int[2]; container.GetLocationOnScreen(origin);
                x = Math.Clamp(p.Window.RequestedPosition.X - origin[0], 0, width - w);
                y = Math.Clamp(p.Window.RequestedPosition.Y - origin[1], 0, height - h);
            }
            else { x = (width - w) / 2; y = (height - h) / 2; }
        }
        if (p.View.LayoutParameters is not FrameLayout.LayoutParams current ||
            current.Width != w || current.Height != h || current.LeftMargin != x || current.TopMargin != y)
            p.View.LayoutParameters = new FrameLayout.LayoutParams(w, h) { LeftMargin = x, TopMargin = y };
    }
    private void Clear()
    {
        var previous = presentations.Values.ToArray();
        presentations.Clear();
        AndroidWindowingPlatform.Complete(previous.Select(p => (Action)p.Dispose));
    }
    /// <summary>Detaches native presentations and callbacks, preserving live framework Forms.</summary>
    public void Dispose()
    {
        if (disposed) return;
        platform.VerifyAccess();
        // Revoke the presentation generation even when an application observer fails.
        // Every native resource must still be released after a preceding cleanup error.
        AndroidWindowingPlatform.Complete([
            () => platform.Detach(this, generation),
            () => { disposed = true; container.LayoutChange -= OnLayout; },
            () =>
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(33) && backCallback is not null)
                    activity.OnBackInvokedDispatcher?.UnregisterOnBackInvokedCallback(backCallback);
            },
            () => { var previous = backCallback; backCallback = null; (previous as IDisposable)?.Dispose(); },
            Clear,
            container.Dispose
        ]);
    }
    private void Verify() { platform.VerifyAccess(); ObjectDisposedException.ThrowIf(disposed, this); }

    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    private sealed class BackCallback(AndroidActivityHost host) : Java.Lang.Object, global::Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked()
        {
            // A callback already queued by Android can outlive unregistration/replacement.
            if (host.Current && !host.HandleBack()) host.activity.Finish();
        }
    }

    private sealed class Presentation : IDisposable
    {
        private readonly AndroidActivityHost host;
        internal readonly AndroidWindowImpl Window;
        internal readonly AndroidSkiaHostView View;
        internal readonly Framebuffer Framebuffer = new();
        private readonly long epoch;
        internal long Epoch => epoch;
        internal bool Disposed;
        internal Presentation(AndroidActivityHost host, AndroidWindowImpl window)
        {
            this.host = host; Window = window;
            epoch = window.PresentationEpoch;
            View = new AndroidSkiaHostView(host.activity)
            {
                AccessibilityUsesScreenPixels = true,
                AccessibilityHost = window.InputRoot as IPlatformAccessibilityHost
            };
            View.Render += Render;
            View.Pointer += Pointer;
            View.KeyInputHandler = Key;
            View.KeyboardStateReset += Reset;
            View.InsetsChanged += Insets;
            View.FocusChange += Focus;
            View.LayoutChange += Geometry;
        }
        private bool Current => !Disposed && host.Current && !Window.IsClosed && Window.Visible && Window.PresentationEpoch == epoch;
        private IWindowSurfaceInput? Input => Window.InputRoot as IWindowSurfaceInput;
        private void Geometry(object? sender, NativeView.LayoutChangeEventArgs e) => UpdateGeometry();
        internal void UpdateGeometry()
        {
            if (!Current || View.Width <= 0 || View.Height <= 0) return;
            int[] origin = new int[2]; View.GetLocationOnScreen(origin);
            var metrics = View.Resources!.DisplayMetrics!;
            Window.ScaledDensity = View.ScaledDensity;
            var displayBounds = new PixelRect(0, 0, metrics.WidthPixels, metrics.HeightPixels);
            Window.ConfirmGeometry(new Size(View.Width / View.Density, View.Height / View.Density), View.Density,
                new PixelPoint(origin[0], origin[1]), new Screen(View.Density, displayBounds, displayBounds, true),
                new PlatformHandle(View.Handle, "AndroidView"));
            if (Current) Window.ConfirmInsets(View.CurrentInsets);
        }
        private void Render(object? sender, AndroidSkiaRenderEventArgs e)
        {
            if (!Current) return;
            // Geometry is confirmed by native layout/configuration callbacks. Avoid Java
            // metrics queries, screen snapshots and resize delegates on every paint.
            if (!Window.Attached) UpdateGeometry();
            if (!Current) return;
            Framebuffer.Resize(View.Width, View.Height, e.Density);
            Window.Paint?.Invoke(new Rect(Window.ClientSize));
            if (Current && Framebuffer.Bitmap is { } bitmap)
            {
                e.Canvas.DrawBitmap(bitmap, new SKRect(0, 0, (float)(bitmap.Width / e.Density), (float)(bitmap.Height / e.Density)));
                Window.PaintCount++;
            }
        }
        private void Pointer(object? sender, AndroidPointerEvent e)
        {
            if (!Current) return;
            Window.ActivePointers = View.HostState.ActivePointerCount;
            if (!Window.Enabled) { Window.GotInputWhenDisabled?.Invoke(); host.ActivateTop(); return; }
            // Tapping the owner dismisses temporary descendants before starting a new gesture.
            if (e.Action == AndroidPointerAction.Down && !Window.IsPopup)
                foreach (var popup in host.platform.Windows.Where(w => w.IsPopup && w.Visible).ToArray()) popup.DismissPopup();
            if (Current) Input?.Pointer(e.PointerId, (WindowSurfacePointerAction)e.Action, new Point(e.X, e.Y));
        }
        private bool Key(AndroidInputKeyEvent e) => !Current || !Window.Enabled ||
            (Input?.Key(e.PlatformKey, e.Modifiers, e.IsDown, !e.IsHardwareKey, e.IsDeadKey, e.IsCanceled) ?? false);
        private void Reset(object? sender, EventArgs e) { if (!Disposed) Input?.CancelInput(); }
        private void Insets(object? sender, WindowInsetsChangedEventArgs e) { if (Current) Window.ConfirmInsets(e.Insets); }
        private void Focus(object? sender, NativeView.FocusChangeEventArgs e)
        {
            // Android delivers old-view loss before new-view gain. Observe the completed native
            // transaction so transferring focus into an editable popup does not deactivate its owner.
            if (Current) View.Post(() => { if (Current) host.ConfirmFocus(); });
        }
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            View.Render -= Render; View.Pointer -= Pointer; View.KeyboardStateReset -= Reset;
            View.InsetsChanged -= Insets; View.FocusChange -= Focus; View.LayoutChange -= Geometry;
            AndroidWindowingPlatform.Complete([
                () => Window.RetirePresentation(epoch),
                () => Window.TextInput.Detach(View),
                () => View.KeyInputHandler = null,
                () => View.AccessibilityHost = null,
                () => host.container.RemoveView(View),
                View.Dispose,
                Framebuffer.Dispose
            ]);
        }
    }

    // Existing WindowBase framebuffer rendering writes directly into this software backing.
    // SKCanvasView presents it with one density conversion. No second control renderer or loop.
    private sealed class Framebuffer : IFramebufferPlatformSurface, IDisposable
    {
        internal SKBitmap? Bitmap;
        private double density;
        internal void Resize(int width, int height, double scale)
        {
            density = scale;
            if (Bitmap?.Width == width && Bitmap.Height == height) return;
            Bitmap?.Dispose();
            Bitmap = new SKBitmap(Math.Max(1, width), Math.Max(1, height), SKColorType.Bgra8888, SKAlphaType.Premul);
        }
        public ILockedFramebuffer Lock()
        {
            var bitmap = Bitmap ?? throw new InvalidOperationException("No active Android paint surface.");
            return new LockedFramebuffer(bitmap.GetPixels(), new PixelSize(bitmap.Width, bitmap.Height), bitmap.RowBytes,
                new Vector(96 * density, 96 * density), PixelFormat.Bgra8888, null);
        }
        public void Dispose() { Bitmap?.Dispose(); Bitmap = null; }
    }
}
