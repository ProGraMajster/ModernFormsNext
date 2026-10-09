using Android.Content;
using Android.Views;
using Android.Widget;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>Supplies a current Activity context and backend-owned container to feature adapters.</summary>
/// <remarks>UI thread only. Never retain the Context or Container after IsCurrent becomes false.</remarks>
public sealed class AndroidNativeViewSite : INativeViewSite
{
    private readonly INativeViewHostCallbacks callbacks;
    private readonly Func<bool> current;
    internal AndroidNativeViewSite(FrameLayout container, INativeViewHostCallbacks callbacks, Func<bool> current)
    { Container = container; this.callbacks = callbacks; this.current = current; }
    /// <summary>Gets the current presentation context, never a process-level Activity.</summary>
    public Context Context => Container.Context!;
    /// <summary>Gets the backend-owned container. Feature peers attach beneath it.</summary>
    public FrameLayout Container { get; }
    /// <inheritdoc/>
    public long Generation => callbacks.Generation;
    /// <inheritdoc/>
    public bool IsCurrent => current() && callbacks.IsCurrent;
    /// <inheritdoc/>
    public bool IsFocused => IsCurrent && callbacks.IsFocused;
    /// <inheritdoc/>
    public bool TryFocus() => IsCurrent && callbacks.TryFocus();
    /// <inheritdoc/>
    public bool MoveFocus(bool forward) => IsCurrent && callbacks.MoveFocus(forward);
    /// <inheritdoc/>
    public void RestoreFocus() { if (IsCurrent) callbacks.RestoreFocus(); }
}

/// <summary>Attaches a real Android View with an explicit owned or borrowed lifetime.</summary>
/// <remarks>
/// Create a fresh View with site.Context for each new session. Borrowed Views must be parentless
/// and belong to this exact Activity context; recreation must supply a different View.
/// Dispose removes the View and only disposes it when ownsView is true.
/// All methods require the UI thread. Ownership transfers only after successful construction.
/// </remarks>
public sealed class AndroidViewPeer : INativeViewPeer
{
    private View? view;
    private readonly AndroidNativeViewSite site;
    private readonly bool ownsView;
    private readonly FrameLayout.LayoutParams parameters;
    /// <summary>Attaches a parentless View to this session's container.</summary>
    /// <param name="site">The current Android hosting site.</param>
    /// <param name="view">A parentless peer created using site.Context.</param>
    /// <param name="ownsView">True transfers View disposal to this lease; false borrows it.</param>
    public AndroidViewPeer(AndroidNativeViewSite site, View view, bool ownsView)
    {
        this.site = site ?? throw new ArgumentNullException(nameof(site));
        ArgumentNullException.ThrowIfNull(view);
        if (!site.IsCurrent || view.Parent is not null || !Equals(view.Context, site.Context))
            throw new ArgumentException("A parentless View using the current site's Activity context is required.", nameof(view));
        parameters = new(1, 1);
        try { site.Container.AddView(view, parameters); }
        catch
        {
            // A failed constructor never transfers ownership of the caller's View. Release
            // our parameters and undo a partial attachment before propagating the failure.
            try { if (Equals(view.Parent, site.Container)) site.Container.RemoveView(view); }
            finally { parameters.Dispose(); }
            throw;
        }
        this.view = view; this.ownsView = ownsView;
    }
    /// <inheritdoc/>
    public void Resize(PixelSize size)
    {
        if (view is null || !site.IsCurrent) return;
        if (parameters.Width == size.Width && parameters.Height == size.Height) return;
        parameters.Width = size.Width; parameters.Height = size.Height;
        view.LayoutParameters = parameters;
    }
    /// <inheritdoc/>
    public void RequestFocus() { if (site.IsCurrent) view?.RequestFocus(); }
    /// <inheritdoc/>
    public bool TryMoveFocus(bool forward)
    {
        if (view is null || !site.IsCurrent) return false;
        var next = site.Container.FindFocus()?.FocusSearch(forward ? FocusSearchDirection.Forward : FocusSearchDirection.Backward);
        for (var parent = next?.Parent; parent is View current; parent = current.Parent)
            if (Equals(current, site.Container)) return next!.RequestFocus();
        return false;
    }
    /// <summary>Detaches the View; disposes it only for an owned lease.</summary>
    public void Dispose()
    {
        var previous = view; view = null;
        try { if (previous?.Parent is global::Android.Views.ViewGroup parent) parent.RemoveView(previous); }
        finally { try { if (ownsView) previous?.Dispose(); } finally { parameters.Dispose(); } }
    }
}
