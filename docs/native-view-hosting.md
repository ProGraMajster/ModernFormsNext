# Native view hosting

`NativeViewHost : Control` is the shared layout, focus and lifecycle boundary for a
platform peer. It is infrastructure for future WebView and MediaElement packages;
it does not implement a browser, media playback, Drag&Drop or GPU composition.
Factories, peer leases and typed platform sites are supported public extension
points for separate feature packages. Providers, sessions and callbacks cross
assembly boundaries but are framework-owned `PrivateApi` contracts marked
`NotClientImplementable`. Sites are also framework-owned and
`NotClientImplementable`, with a supported public consumption contract.

## Architecture and API

See the [public extension boundary review](design/native-view-host-api-review.md)
for the intended audience, implementer restrictions and ownership of every new
public type and member group.

```text
Control tree -> NativeViewHost -> WindowKit hosting provider -> session
                                                        -> native container -> feature peer
WindowBase -> existing rendering surface / input root    (separate presentation services)
```

Configure `PeerFactory` on the owning UI thread. A factory implements
`INativeViewFactory.CreatePeer(INativeViewSite)` and returns an
`INativeViewPeer` lease. Its three operations are peer-content resize, focus
request and optional internal Tab traversal. Its disposal owns feature-specific
callbacks and the peer's declared native resource lifetime. The framework
supplies bounds, clip, visibility, enabled state, focus validation and session
retirement. Factories themselves remain application/feature-owned.

```csharp
var host = new NativeViewHost
{
    Dock = DockStyle.Fill,
    Text = "Feature placeholder",
    PeerFactory = feature.CreateNativePeerFactory()
};
form.Controls.Add(host);
```

The example's `feature` is supplied by a separate platform integration package,
not a built-in WebView or media API. A portable feature factory can delegate to
backend-specific adapters. Shared API exposes no HWND, Android View/Context,
browser types or `object NativeView`. Backend sites are deliberately typed:

- Windows adapters receive `WindowsNativeViewSite.ParentWindow`, a backend-owned
  child container HWND. They may return `WindowsHwndViewPeer` for a real child
  HWND, or their own peer lease around a future browser controller.
- Android adapters receive `AndroidNativeViewSite.Context` and `Container`.
  Create a fresh View from this context and return `AndroidViewPeer`.
  Do not use an old Activity context or move an old View across recreation.

An asynchronous feature runtime may return a lease before initialization
completes; that lease must retain the latest content size, cancel initialization
on disposal, and check `site.IsCurrent` before attaching an asynchronous result.
Before honoring a deferred native-focus request, also check `site.IsFocused`;
initialization must not reacquire canonical focus after the user selected another
control. This read-only value comes from the existing canonical owner.
That is feature initialization, not another hosting lifecycle.

## Ownership and lifecycle

| Resource | Owner and cleanup |
|---|---|
| Shared control and factory | Application/feature; dispose control on the UI thread; factory is not disposed by the host |
| Hosting session | Host; replacement, cross-window reparent or control disposal retires it |
| Container HWND / FrameLayout | Backend session; destroyed after peer lease disposal |
| Peer lease | Session; Dispose removes feature callbacks and detaches native content |
| Owned peer | Adapter lease destroys HWND / disposes Android View |
| Borrowed peer | Lease detaches without destruction; caller owns its later cleanup |
| Window / Activity presentation | Existing WindowBase / AndroidActivityHost lifecycle |

For Windows, `WindowsHwndViewPeer(..., ownsWindow: false)` borrows a same-thread
WS_CHILD HWND, then hides it and returns it to its original parent on disposal.
The parent must outlive the borrow. Owned HWNDs are explicitly destroyed.
For Android, `AndroidViewPeer(..., ownsView: false)` borrows a parentless View
created with that exact site's context. Every recreated session must provide a
fresh appropriate View. Borrowing never permits reuse of a retired Activity's
View in another Activity.

Callbacks are revoked before cleanup. Sessions unregister native hooks/observers,
dispose peer leases and destroy containers deterministically, including when a
preceding feature cleanup throws. The inherited Control finalizer performs no
UI/native cleanup; explicit UI-thread disposal is required.

Removing a host hides its peer immediately. Retirement is posted to the existing
dispatcher boundary so a synchronous collection move within the same window can
retain the lease. A detached host still present at that boundary retires.
Cross-window moves retire the old site immediately and create a new peer in the
destination presentation. Focus retirement uses the existing reparenting rules;
retaining a peer does not retain obsolete shared focus.

Android's stable window hosting provider follows existing presentation epochs.
A retired presentation disposes all sessions and publishes unavailability before
its native root is released. A replacement Activity builds a new root and invokes
the same factories with new sites. Old site callbacks cannot affect the new root.
Background/resume hides/restores retained peers; main Form Hide/Show follows the
existing Android presentation retirement/recreation policy.

## Geometry, scrolling and updates

Layout remains authoritative. Existing `ClientPointToParentPresentation(PointF)`
maps the device-space local geometry through the same presentation path as
paint/input/IME. The root adds WindowBase.DisplayRectangle origin, including
managed chrome and safe-area offsets. Native pixel bounds use that device result
directly; logical diagnostic bounds are derived by dividing by RenderScaling.
This avoids a lossy device -> logical -> device round trip at fractional DPI.
Neither backend scales the supplied pixel placement again.

Scrolling already moves child bounds in ScrollableControl. The host never
subtracts another scroll offset. Clipping intersects host bounds, ancestor client
rectangles, scroll viewport minus implicit scrollbars and top-level client extent.
Dock/Anchor, nested Panel, FlowLayoutPanel and TableLayoutPanel use the same path.
Partial clip changes move/clip the retained peer; an empty clip hides it.
On Android the container also rejects pointer entry outside the visible clip;
ClipBounds alone affects drawing and would leave clipped-out peers hit-testable.

Sparse notifications subscribe only to the host's ancestry. Bounds, layout,
scroll, visibility/enabled, root reparenting, presentation/DPI/density and explicit
style invalidation drive updates. There is no timer, separate render loop, polling,
per-frame control-tree enumeration or hosting work in OnPaint. The last placement
is cached before calling native code; unchanged state makes no native geometry call.
Mutable style objects retain the existing rule: call Invalidate or PerformLayout
after editing their values.

Ordinary invalidation compares a sparse snapshot of the subscribed control's
virtual client rectangle and composition support. An unchanged snapshot does not
invoke hosting synchronization, capability reads or geometry projection. The
snapshot is allocated only for subscribed paths and replaced only on changes.
The canonical scaled-bounds method uses bit tests to avoid enum boxing; pixel
rounding and derived overrides remain unchanged. Warm measurements and regression
tests require equal ordinary-invalidation allocations with and without an idle
host, and zero new session updates or capability reads.

Windows uses a dedicated WS_CHILD/WS_CLIPCHILDREN/WS_CLIPSIBLINGS container,
SetWindowPos in client pixels and SetWindowRgn with an owned rectangular region.
HRGN ownership transfers to Windows only when SetWindowRgn succeeds.
Android uses one FrameLayout root per window:

```text
Activity FrameLayout (existing popup/modal presentation order)
  PresentationRoot
    AndroidSkiaHostView
    native host A FrameLayout -> feature View
    native host B FrameLayout -> feature View
  popup/modal PresentationRoot
```

Each root has exactly one Skia presentation. Native containers are siblings;
there is no second framework control tree or Activity per host.

## Airspace, order and unsupported composition

All native peers in a top-level occupy one band above its Skia-painted content.
Back-to-front native ordering follows the first differing ancestors' existing
ControlCollection order; BringToFront/SendToBack use that same order.
There is no public ZIndex. Skia siblings cannot paint above a same-window peer,
even when their control index is later. Use a separate popup/modal top-level for
an overlay. Windows popup/modal HWNDs remain above owner children. On Android,
the entire popup/modal presentation root remains above the owner's native band.
Modals disable native owner input and restore it on completion.

Opacity below one, rotation, arbitrary render scale, layout presentation-size
stretching, rounded ancestor borders and configured interaction effects hide the
peer and report Unsupported. Removing these conditions restores the retained
peer. Normal Location, scrolling, Dock/Anchor and translation are supported.
Non-finite transform values suspend visibility before native geometry conversion.
Skew, arbitrary paths, native filters/effects and arbitrary Skia/native interleaving
are not provided by this baseline. Do not simulate them with bitmap capture or a
second composition engine. A visible rectangle is not a promise of rounded or
nonrectangular clipping.

## Focus, validation and input

MFN-to-native requests first commit through ControlFocusScope, including validation
preflight, then request native focus. Native focus/pointer entry requests the same
transaction. Windows uses a thread-local CBT hook before native focus commits;
a validation veto blocks it. Android intercepts pointer entry before dispatch and
bridges completed native focus changes; a veto restores the current canonical
framework input surface. GotFocus/LostFocus are never emitted outside the existing
shared transaction.

Native focus loss does not clear shared focus. A subsequent gain commits its own
transaction; obsolete sites reject callbacks by session identity and existing
Activity/presentation epoch. Old MFN text/IME sessions are retired by the canonical
focus code rather than revived by a native focus-loss callback.

Feature focus requests can synchronously select another control. The native
container fallback rechecks canonical ownership after the peer callback, so it
cannot steal input from that newer owner.

Native keyboard/text and pointer events stay in the native subtree, without
duplicating input into the Skia root.
If a peer is not focusable (including Android buttons in touch mode) or is still
initializing, the native container retains the keyboard boundary for the host.
This prevents the underlying framework input surface from receiving native keys.
Tab asks the lease to traverse internally; at its boundary the host invokes
existing SelectNextControl. A simple HWND lease
is one tab stop; complex feature peers may implement internal traversal. There is
no global shortcut tunneling from arbitrary native descendants.

## Designer, accessibility and diagnostics

Designer, detached preview and any ancestor/window with Site.DesignMode use the
Skia placeholder. Painting never calls the factory. Text is the safe placeholder
metadata inherited by future feature controls. PeerFactory, HostingCapabilities
and HostingDiagnostics are hidden from Designer serialization; runtime handles
and sessions have no public shared property.

The NativeViewHost is a shared accessibility boundary. Native content keeps its
own OS accessibility subtree; it is not synthesized as MFN virtual descendants.
This baseline does not expand Android accessibility or UIA provider contracts.

HostingCapabilities reports native attach, rectangular clip, focus bridge,
multiple-host support and the airspace band. Unsupported backend capabilities
are false. HostingDiagnostics reports Detached/Attached/Suspended/Unsupported/
Faulted, logical bounds, pixel bounds, local clip, scale, visibility/enabled,
session generation and a safe reason. It contains no pointer or peer text.
Factory/update failures retire the lease and report the exception type; changing
the factory or replacing a presentation permits a new attachment attempt.

## Capability matrix

Headless entries are simulated contract behavior, never OS validation.

| Capability | Windows | Android | Headless | Designer |
|---|---|---|---|---|
| Native attach | Real child HWND | Real View | Fake lease | No |
| Bounds | Client pixels | Root pixels | Recorded | Placeholder layout |
| Rectangular clip | Window region | Container ClipBounds | Recorded | Skia |
| Scrolling | Retained peer | Retained peer | Recorded | Layout preview |
| Visibility / enabled | Effective ancestors/window | Effective ancestors/presentation | Simulated | Placeholder |
| Focus / validation | Canonical transaction + CBT | Canonical transaction + native bridge | Simulated | No native focus |
| Tab | Native subtree boundary -> shared order | Native subtree boundary -> shared order | Simulated | No native traversal |
| Multiple hosts | Existing control order | Existing control order | Recorded | Multiple placeholders |
| Reparent same-window | Retain synchronous move | Retain synchronous move | Simulated | Layout only |
| Reparent cross-window | Recreate session | Recreate in legal existing Form/presentation | Simulated | Layout only |
| DPI / density | Existing RenderScaling | Existing density/configuration | Controlled viewport | Preview scaling |
| Popup / modal overlay | Separate top-level above children | Separate root above native band | Shared lifecycle only | No native overlay |
| Activity recreation | Not applicable | Fresh factory/site/context | Simulated replacement | No runtime |
| Opacity < 1 | Hide + Unsupported | Hide + Unsupported | Same policy | Skia placeholder |
| Rotation / skew | Hide for rotation; no skew API | Same | Same policy | Skia placeholder |
| Arbitrary scale / size stretch | Hide + Unsupported | Hide + Unsupported | Same policy | Skia placeholder |
| Rounded / nonrectangular clip | Rounded hides; no path support | Same | Same policy | Skia placeholder |
| Filters / effects | Unsupported | Unsupported | Same policy | Placeholder only |
| Same-window Skia above peer | Unsupported; native band wins | Same | No OS airspace | Skia only |
| Designer native runtime | Never | Never | Never under DesignMode | Never |

## Validation and future integration

TestHost.NativeViews supplies a fake provider/session recorder for lifecycle,
geometry, clip, visibility/enabled, generation, focus, reentrancy and ordering.
It deliberately does not claim real native input or resource validation.
The Windows UiAutomationHost `--native-view` scenario and Android sample
`NativeViewValidationInstrumentation` create real test peers, independently of
the normal application. ControlGallery's NativeViewHost page provides two real
Windows EDIT peers with scrolling and airspace controls.

WebView readiness: a future shared feature can derive from NativeViewHost and
supply Windows/Android peer leases without copying layout, clipping, scrolling,
DPI, visibility, HWND discovery, focus validation or Activity lifecycle.
Its controller-specific content resize, focus request and async initialization
remain adapter responsibilities. No browser package is installed by this work.

Media readiness: the same site/lease infrastructure supports a future native
video View/child surface; playback and any special compositor requirements are
feature work. This is not a promise of arbitrary GPU surface composition.

GPU readiness: NativeViewHost depends only on platform presentation and the
existing control geometry/focus contracts. Its public API does not depend on a
software framebuffer, SoftwareRenderingBackend or IRenderFrame.
