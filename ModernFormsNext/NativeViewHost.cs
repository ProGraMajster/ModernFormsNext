using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext;

/// <summary>Hosts a platform peer inside the normal framework layout and focus tree.</summary>
/// <remarks>
/// Native content occupies an airspace band above all Skia content in the same top-level.
/// Rectangular clipping is supported; opacity, rotation, scale and rounded ancestor clipping
/// suspend native visibility. Configure the factory and dispose the control on the UI thread.
/// The factory recreates peers against replacement Android Activity contexts. Painting and
/// Designer preview only draw the safe placeholder; they never create a native runtime.
/// </remarks>
/// <example><code>
/// var host = new NativeViewHost { Dock = DockStyle.Fill, Text = "Native content" };
/// host.PeerFactory = feature.CreateNativePeerFactory();
/// form.Controls.Add(host);
/// </code></example>
public class NativeViewHost : Control
{
    private INativeViewFactory? factory;
    private INativeViewSession? session;
    private INativeViewHostProvider? provider;
    private WindowBase? ownerWindow;
    private readonly List<Control> ancestors = [];
    private NativeViewPlacement? lastPlacement;
    private long generation;
    private bool synchronizing, pending, nativeFocusTransaction, retiring, creating;
    private INativeViewFactory? failedFactory;
    private NativeViewHostDiagnostics diagnostics = new(NativeViewHostState.Detached, default, 0, "No factory.");

    /// <summary>Creates a selectable native boundary with a safe text placeholder.</summary>
    public NativeViewHost()
    {
        SetControlBehavior(ControlBehaviors.Selectable, true);
        TabStop = true;
        NativePresentationChanged += PresentationChanged;
    }

    /// <summary>Gets or sets the backend-facing peer factory. Replacing it retires the old lease.</summary>
    /// <remarks>
    /// Configure on the UI thread. This runtime extension point is hidden from Designer serialization.
    /// Separate feature packages may supply a factory without reflection or friend assemblies.
    /// A factory must return a fresh lease for every session; the host never owns the factory itself.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public INativeViewFactory? PeerFactory
    {
        get => factory;
        set
        {
            if (ReferenceEquals(factory, value)) return;
            GetFocusScope().VerifyAccess();
            factory = value;
            failedFactory = null;
            RetireSession();
            Synchronize();
        }
    }

    /// <summary>Gets the active backend's composition capabilities; unavailable without a presentation provider.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NativeViewCapabilities HostingCapabilities => provider?.Capabilities ?? default;

    /// <summary>Gets a snapshot without native pointers or peer content.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NativeViewHostDiagnostics HostingDiagnostics => diagnostics;

    private void PresentationChanged(object? sender, EventArgs e) => Synchronize();
    private void ProviderChanged() { failedFactory = null; Synchronize(); }
    private void WindowChanged(object? sender, EventArgs e) => Synchronize();

    private void ObserveTree()
    {
        // Rebuild only when ancestry changes; geometry updates allocate no subscription list.
        int index = 0;
        Control? current = Parent;
        while (current is not null && index < ancestors.Count && ReferenceEquals(current, ancestors[index]))
        { index++; current = current.Parent; }
        if (current is null && index == ancestors.Count) return;
        foreach (var ancestor in ancestors) ancestor.NativePresentationChanged -= PresentationChanged;
        ancestors.Clear();
        for (current = Parent; current is not null; current = current.Parent)
        {
            ancestors.Add(current);
            current.NativePresentationChanged += PresentationChanged;
        }
    }

    internal void Synchronize()
    {
        if (retiring || IsDisposed || Disposing) return;
        GetFocusScope().VerifyAccess();
        if (synchronizing) { pending = true; return; }
        synchronizing = true;
        try
        {
            for (int pass = 0; ; pass++)
            {
                pending = false;
                SynchronizeCore();
                if (!pending) break;
                if (pass >= 31) throw new InvalidOperationException("Native host updates did not settle.");
            }
        }
        finally { synchronizing = false; }
    }

    private void SynchronizeCore()
    {
        ObserveTree();
        WindowBase? window = FindWindow();
        bool designer = window?.Site?.DesignMode == true || Site?.DesignMode == true || ancestors.Any(a => a.Site?.DesignMode == true);
        if (window is null && session is not null && !designer)
        {
            // Collection reparenting briefly passes through a detached tree. Hide immediately,
            // then retire at the next dispatcher boundary unless it reattaches to the same window.
            var hidden = lastPlacement.GetValueOrDefault() with { Visible = false };
            session.Update(hidden);
            lastPlacement = hidden;
            diagnostics = new(NativeViewHostState.Suspended, hidden, generation, "Detached pending retirement.");
            long expected = generation;
            Dispatcher.UIThread.Post(() =>
            {
                if (!retiring && expected == generation && FindWindow() is null)
                { RetireSession(); SetDiagnostics(NativeViewHostState.Detached, default, "Detached."); }
            });
            return;
        }
        if (!ReferenceEquals(ownerWindow, window))
        {
            RetireSession();
            ObserveWindow(window);
            failedFactory = null;
        }
        var nextProvider = !designer && window is { IsBackendClosed: false }
            ? window.window.TryGetFeature(typeof(INativeViewHostProvider)) as INativeViewHostProvider : null;
        if (!ReferenceEquals(nextProvider, provider))
        {
            RetireSession();
            if (provider is not null) provider.Changed -= ProviderChanged;
            provider = nextProvider;
            if (provider is not null) provider.Changed += ProviderChanged;
            failedFactory = null;
        }
        if (designer || factory is null || window is null || provider is null || !provider.Capabilities.Supported)
        {
            RetireSession();
            SetDiagnostics(provider is null && window is not null && !designer && factory is not null
                ? NativeViewHostState.Unsupported : NativeViewHostState.Detached, default,
                designer ? "Designer placeholder." : factory is null ? "No factory." : "Native hosting unavailable.");
            return;
        }
        bool unsupported = HasUnsupportedNativeComposition || ancestors.Any(a => a.HasUnsupportedNativeComposition);
        NativeViewPlacement placement = GetPlacement(window, unsupported);
        if (!provider.IsAvailable)
        {
            RetireSession();
            SetDiagnostics(NativeViewHostState.Suspended, placement, "Presentation unavailable.");
            return;
        }
        if (ReferenceEquals(failedFactory, factory)) return;
        try
        {
            if (session is null && window.Visible)
            {
                var callbacks = new Callbacks(this, ++generation, window);
                INativeViewSession candidate;
                creating = true;
                try { candidate = provider.CreateSession(factory, callbacks); }
                finally { creating = false; }
                if (!callbacks.IsCurrent || !ReferenceEquals(window, FindWindow()) || pending)
                {
                    candidate.Dispose();
                    pending = true;
                    return;
                }
                session = candidate;
                lastPlacement = null;
                NativeViewOrdering.Add(this);
            }
            if (session is not null && lastPlacement != placement)
            {
                var active = session;
                lastPlacement = placement; // Commit before native calls that can reenter layout/focus.
                active.Update(placement);
                if (!ReferenceEquals(active, session)) return;
            }
            SetDiagnostics(unsupported ? NativeViewHostState.Unsupported :
                placement.Visible && session is not null ? NativeViewHostState.Attached : NativeViewHostState.Suspended,
                placement, unsupported ? "Opacity, rotation, scale, rounded clipping or effects are unsupported." :
                placement.Visible ? null : "Hidden or clipped.");
            NativeViewOrdering.Update(window);
        }
        catch (Exception error)
        {
            failedFactory = factory;
            try { RetireSession(); }
            finally { SetDiagnostics(NativeViewHostState.Faulted, placement, error.GetType().Name); }
        }
    }

    private NativeViewPlacement GetPlacement(WindowBase window, bool unsupported)
    {
        RectangleF bounds = PresentationRootBounds(new(0, 0, ScaledWidth, ScaledHeight), devicePixels: true);
        // Invalid user transforms must suspend the peer before any float-to-native conversion.
        // Preserve the last valid geometry so restoring the transform reuses the same lease.
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) ||
            !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height))
            return lastPlacement.GetValueOrDefault() with { Visible = false, Scale = window.Scaling };
        RectangleF clip = bounds;
        foreach (var ancestor in ancestors)
        {
            var viewport = ancestor is ScrollableControl scroll ? scroll.NativeHostingViewport : ancestor.ClientRectangle;
            clip.Intersect(ancestor.PresentationRootBounds(viewport, devicePixels: true));
        }
        double scale = window.Scaling;
        clip.Intersect(new RectangleF(0, 0, (float)(window.window.ClientSize.Width * scale), (float)(window.window.ClientSize.Height * scale)));
        var pixels = PixelRect.FromRect(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height), 1);
        var clipped = PixelRect.FromRect(new Rect(clip.X, clip.Y, Math.Max(0, clip.Width), Math.Max(0, clip.Height)), 1);
        var localClip = new PixelRect(clipped.X - pixels.X, clipped.Y - pixels.Y, clipped.Width, clipped.Height);
        bool visible = Visible && provider?.IsVisible == true && window.Visible && !window.IsBackendClosed && !unsupported &&
            clipped.Width > 0 && clipped.Height > 0 && pixels.Width > 0 && pixels.Height > 0;
        return new(new Rect(bounds.X / scale, bounds.Y / scale, bounds.Width / scale, bounds.Height / scale), pixels, localClip, scale,
            visible, Enabled && provider?.IsEnabled == true);
    }

    private void ObserveWindow(WindowBase? window)
    {
        if (ownerWindow is not null)
        {
            ownerWindow.VisibleChanged -= WindowChanged;
            ownerWindow.SizeChanged -= WindowChanged;
            ownerWindow.DpiChanged -= WindowChanged;
        }
        ownerWindow = window;
        if (window is null) return;
        window.VisibleChanged += WindowChanged;
        window.SizeChanged += WindowChanged;
        window.DpiChanged += WindowChanged;
    }

    private void SetDiagnostics(NativeViewHostState state, NativeViewPlacement placement, string? reason)
    {
        if (diagnostics.State == state && diagnostics.Placement == placement &&
            diagnostics.Generation == generation && diagnostics.Reason == reason) return;
        var next = new NativeViewHostDiagnostics(state, placement, generation, reason);
        diagnostics = next;
        if (ownerWindow is { IsBackendClosed: false, Visible: true } && provider?.IsAvailable == true) Invalidate();
    }

    private void RetireSession()
    {
        if (session is not null || creating) generation++; // Revoke live callbacks before native cleanup.
        var previous = session;
        session = null;
        lastPlacement = null;
        NativeViewOrdering.Remove(this);
        try { previous?.Dispose(); }
        catch (Exception error)
        {
            // Retirement has already revoked callbacks. Do not leave an Attached snapshot
            // when feature cleanup fails before a replacement factory can be synchronized.
            diagnostics = new(NativeViewHostState.Faulted, default, generation, error.GetType().Name);
            throw;
        }
    }

    internal WindowBase? NativeWindow => ownerWindow;
    internal INativeViewSession? NativeSession => session;

    /// <inheritdoc/>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (Selected && !nativeFocusTransaction) session?.RequestFocus();
    }

    /// <inheritdoc/>
    protected override void OnLostFocus(EventArgs e)
    {
        try { base.OnLostFocus(e); }
        finally { if (!Selected) session?.ReturnFocus(); }
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        // A native peer paints itself. Runtime painting remains a harmless placeholder
        // for detached/Designer/unsupported states and never starts a peer.
        e.Canvas.DrawText(string.IsNullOrEmpty(Text) ? "Native view" : Text, ClientRectangle,
            this, ContentAlignment.MiddleCenter, maxLines: 1, ellipsis: true);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        GetFocusScope().VerifyAccess(); // Reject wrong-thread calls before touching a native lease.
        retiring = true;
        try
        {
            try { RetireSession(); }
            finally
            {
                if (provider is not null) provider.Changed -= ProviderChanged;
                provider = null;
                ObserveWindow(null);
                foreach (var ancestor in ancestors) ancestor.NativePresentationChanged -= PresentationChanged;
                ancestors.Clear();
                NativePresentationChanged -= PresentationChanged;
            }
        }
        finally { base.Dispose(true); }
    }

    private sealed class Callbacks(NativeViewHost host, long identity, WindowBase window) : INativeViewHostCallbacks
    {
        public long Generation => identity;
        public bool IsCurrent => !host.retiring && !host.IsDisposed && !host.Disposing &&
            identity == host.generation && ReferenceEquals(host.FindWindow(), window) && !window.IsBackendClosed;
        public bool IsFocused => IsCurrent && host.Selected && ReferenceEquals(host.GetFocusScope().Owner, host);
        public bool TryFocus()
        {
            if (!IsCurrent || host.lastPlacement is not { Visible: true, Enabled: true }) return false;
            host.nativeFocusTransaction = true;
            try { return host.GetFocusScope().Request(host) && IsCurrent && host.Selected; }
            finally { host.nativeFocusTransaction = false; }
        }
        public bool MoveFocus(bool forward)
            => IsCurrent && window.Controls.Owner.SelectNextControl(host, forward, true, true, true);
        public void RestoreFocus()
        {
            if (!IsCurrent) return;
            if (host.GetFocusScope().Owner is NativeViewHost native) native.session?.RequestFocus();
            else host.provider?.FocusFramework();
        }
    }
}
