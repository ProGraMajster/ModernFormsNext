using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform;

/// <summary>Describes native surfaces relative to framework-painted content.</summary>
public enum NativeViewAirspace
{
    /// <summary>No native surfaces are available.</summary>
    None,
    /// <summary>Native surfaces form one band above same-window framework content.</summary>
    AboveFrameworkContent
}

/// <summary>Reports presentation capabilities, independently of the rendering backend.</summary>
/// <param name="Supported">Whether this backend implements native hosting.</param>
/// <param name="RectangularClipping">Whether ancestor rectangular intersection is supported.</param>
/// <param name="FocusBridge">Whether canonical native/shared focus is bridged.</param>
/// <param name="MultipleHosts">Whether deterministic multiple-peer hosting is supported.</param>
/// <param name="Airspace">The same-window composition ordering model.</param>
public readonly record struct NativeViewCapabilities(
    bool Supported, bool RectangularClipping, bool FocusBridge, bool MultipleHosts,
    NativeViewAirspace Airspace)
{
    /// <summary>Gets the capabilities of an unavailable backend.</summary>
    public static NativeViewCapabilities Unavailable => default;
    /// <summary>Gets the rectangular native airspace baseline; transforms and effects are unsupported.</summary>
    public static NativeViewCapabilities Baseline => new(true, true, true, true, NativeViewAirspace.AboveFrameworkContent);
    /// <summary>Gets whether arbitrary native transforms are supported; false for this baseline.</summary>
    public bool ArbitraryTransforms => false;
    /// <summary>Gets whether native opacity composition is supported; false for this baseline.</summary>
    public bool OpacityComposition => false;
    /// <summary>Gets whether native filters and rendering effects are supported; false for this baseline.</summary>
    public bool Effects => false;
}

/// <summary>Describes one native composition update in client pixels, with a host-local rectangular clip.</summary>
/// <param name="LogicalBounds">Window-client logical bounds, before native pixel rounding.</param>
/// <param name="PixelBounds">Bounds relative to the top-level native client, already scaled once.</param>
/// <param name="PixelClip">Visible rectangle relative to the host's pixel origin.</param>
/// <param name="Scale">Logical-to-device scale, for diagnostics; do not scale PixelBounds again.</param>
/// <param name="Visible">Effective visibility including ancestors, composition and viewport.</param>
/// <param name="Enabled">Effective enabled state.</param>
public readonly record struct NativeViewPlacement(
    Rect LogicalBounds, PixelRect PixelBounds, PixelRect PixelClip, double Scale, bool Visible, bool Enabled);

/// <summary>Creates a fresh peer for each presentation session, including Activity recreation.</summary>
/// <remarks>
/// Feature packages implement this extension point. Calls occur on the UI thread, never during
/// painting or Designer preview. The returned lease transfers to the session, which disposes it
/// after detachment. A borrowed native object must be wrapped by a lease that does not destroy it.
/// Do not retain the site beyond its session; a replacement site uses a new native context.
/// </remarks>
public interface INativeViewFactory
{
    /// <summary>Creates a peer against a platform-specific site implemented by the backend.</summary>
    INativeViewPeer CreatePeer(INativeViewSite site);
}

/// <summary>Represents an owned peer lease, with native resource ownership defined by its adapter.</summary>
/// <remarks>All methods require the UI thread. Dispose releases feature callbacks and owned resources.</remarks>
public interface INativeViewPeer : IDisposable
{
    /// <summary>Updates peer content size in device pixels inside the backend-owned container.</summary>
    void Resize(PixelSize size);
    /// <summary>Requests native keyboard focus after canonical framework focus has committed.</summary>
    void RequestFocus();
    /// <summary>Traverses inside the native subtree; returns false at its boundary.</summary>
    bool TryMoveFocus(bool forward);
}

/// <summary>Identifies a current platform hosting container without exposing native types.</summary>
[NotClientImplementable]
public interface INativeViewSite
{
    /// <summary>Gets the session identity used to reject obsolete callbacks.</summary>
    long Generation { get; }
    /// <summary>Gets whether this site still belongs to its live presentation and control.</summary>
    bool IsCurrent { get; }
    /// <summary>Gets whether this live host owns canonical focus. Check before deferred feature initialization requests native focus.</summary>
    bool IsFocused { get; }
    /// <summary>Requests canonical host focus, including validation; false means native focus must be rejected.</summary>
    bool TryFocus();
    /// <summary>Leaves the native subtree through the framework's existing tab navigation.</summary>
    bool MoveFocus(bool forward);
    /// <summary>Restores the current canonical input surface after a rejected native focus entry.</summary>
    void RestoreFocus();
}

/// <summary>Connects a native session to the control's canonical focus transaction.</summary>
/// <remarks>Framework-owned assembly-boundary contract. Feature packages consume sites instead of implementing callbacks.</remarks>
[PrivateApi, NotClientImplementable]
public interface INativeViewHostCallbacks
{
    /// <summary>Gets the attachment generation.</summary>
    long Generation { get; }
    /// <summary>Gets whether callbacks may still affect the current control/presentation.</summary>
    bool IsCurrent { get; }
    /// <summary>Gets whether this live host is still the canonical focus owner after a reentrant feature callback.</summary>
    bool IsFocused { get; }
    /// <summary>Commits focus through framework validation.</summary>
    bool TryFocus();
    /// <summary>Uses existing Tab order to leave this host.</summary>
    bool MoveFocus(bool forward);
    /// <summary>Restores the current canonical owner's native input surface after a veto.</summary>
    void RestoreFocus();
}

/// <summary>Owns one container and peer lease. Dispose detaches peers, unregisters callbacks and destroys the container.</summary>
/// <remarks>Framework-owned assembly-boundary contract. Feature packages implement peer leases, never sessions.</remarks>
[PrivateApi, NotClientImplementable]
public interface INativeViewSession : IDisposable
{
    /// <summary>Applies a changed placement; implementations cache native state.</summary>
    void Update(NativeViewPlacement placement);
    /// <summary>Requests focus after the host's shared transaction.</summary>
    void RequestFocus();
    /// <summary>Returns native focus to the framework surface if this session owns it.</summary>
    void ReturnFocus();
    /// <summary>Compares hosts using the existing back-to-front control order.</summary>
    void PlaceAbove(INativeViewSession? previous);
}

/// <summary>Provides hosting for one top-level presentation through ITopLevelImpl.TryGetFeature.</summary>
/// <remarks>Framework/backend-owned assembly-boundary contract. Applications configure NativeViewHost.PeerFactory instead.</remarks>
[PrivateApi, NotClientImplementable]
public interface INativeViewHostProvider
{
    /// <summary>Gets backend composition capabilities.</summary>
    NativeViewCapabilities Capabilities { get; }
    /// <summary>Gets whether a native presentation is currently available.</summary>
    bool IsAvailable { get; }
    /// <summary>Gets effective presentation visibility, including Activity suspension.</summary>
    bool IsVisible { get; }
    /// <summary>Gets top-level enabled state, including modal ownership.</summary>
    bool IsEnabled { get; }
    /// <summary>Occurs when presentation availability or identity changes; old sessions are retired first.</summary>
    event Action? Changed;
    /// <summary>Creates a session on the UI thread. The backend owns its container.</summary>
    INativeViewSession CreateSession(INativeViewFactory factory, INativeViewHostCallbacks callbacks);
    /// <summary>Requests native keyboard focus for the framework's current input surface.</summary>
    void FocusFramework();
}
