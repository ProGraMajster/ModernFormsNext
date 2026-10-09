# Native hosting extension boundary

Native hosting is a presentation service coordinated by the existing control
tree. The rendering path remains `WindowBase -> rendering backend -> SKCanvas`.
No native-host interface references a rendering surface or framebuffer.

## Public surface and intended implementers

The following table reviews every new public type and its declared members.
Record constructors/properties/equality operations are public value semantics;
they carry no native resource ownership. Public visibility is needed across the
framework/backend/feature-package assembly boundaries.

| Type / members | Audience and visibility decision |
|---|---|
| `NativeViewHost`: constructor, `PeerFactory`, `HostingCapabilities`, `HostingDiagnostics`; protected paint/focus/disposal overrides | Application or derived feature control. Supported public control; overrides reuse base focus/disposal. Runtime properties are hidden from Designer serialization. |
| `NativeViewHostState`: Detached, Attached, Suspended, Unsupported, Faulted | Application diagnostics; supported public enum. |
| `NativeViewHostDiagnostics`: State, Placement, Generation, Reason | Application diagnostics; supported public immutable value. Reasons contain framework text or exception type, never feature content. |
| `NativeViewAirspace`: None, AboveFrameworkContent | Application capability reporting; supported public enum. |
| `NativeViewCapabilities`: Supported, RectangularClipping, FocusBridge, MultipleHosts, Airspace; Unavailable, Baseline, ArbitraryTransforms, OpacityComposition, Effects | Application/feature capability reporting; supported public immutable value. Unsupported composition flags remain false. |
| `NativeViewPlacement`: LogicalBounds, PixelBounds, PixelClip, Scale, Visible, Enabled | Public pointer-free diagnostic value and backend update payload. No `PrivateApi` because application diagnostics expose it. |
| `INativeViewFactory.CreatePeer` | Supported external implementer seam. Public, no `PrivateApi` or `NotClientImplementable`: feature packages implement it. The application/feature owns the factory. |
| `INativeViewPeer`: Resize, RequestFocus, TryMoveFocus, inherited Dispose | Supported external implementer seam. Public, no `PrivateApi` or `NotClientImplementable`. A session owns the returned disposable lease. All calls require the UI thread. |
| `INativeViewSite`: Generation, IsCurrent, IsFocused, TryFocus, MoveFocus, RestoreFocus | Supported external consumption seam, implemented by the framework. Public and `NotClientImplementable`, without `PrivateApi`; members may be added without requiring feature implementations. IsFocused lets asynchronous initialization honor focus only while the host still owns it. |
| `INativeViewHostCallbacks`: Generation, IsCurrent, IsFocused, TryFocus, MoveFocus, RestoreFocus | Framework-owned cross-assembly contract; public with `PrivateApi` and `NotClientImplementable`. A feature implements neither callbacks nor canonical focus. IsFocused rechecks canonical ownership after reentrant feature focus requests. |
| `INativeViewSession`: Update, RequestFocus, ReturnFocus, PlaceAbove, inherited Dispose | Framework/backend-owned cross-assembly lifecycle; public with `PrivateApi` and `NotClientImplementable`. A feature never owns a container/session. |
| `INativeViewHostProvider`: Capabilities, IsAvailable, IsVisible, IsEnabled, Changed, CreateSession, FocusFramework | Framework/backend-owned window feature; public with `PrivateApi` and `NotClientImplementable`. Applications configure PeerFactory instead of injecting a second provider. |
| `WindowsNativeViewSite`: ParentWindow, Generation, IsCurrent, IsFocused, TryFocus, MoveFocus, RestoreFocus | Supported public Windows adapter consumption seam. Sealed, internal constructor; features cannot construct a site. Native HWND is confined to the Windows backend assembly. |
| `WindowsHwndViewPeer`: constructor, Resize, RequestFocus, TryMoveFocus, Dispose | Supported public Windows lease helper. Ownership transfers only after successful construction. Borrowed HWNDs return to an original parent that must remain alive. |
| `AndroidNativeViewSite`: Context, Container, Generation, IsCurrent, IsFocused, TryFocus, MoveFocus, RestoreFocus | Supported public Android adapter consumption seam. Sealed, internal constructor. Activity Context and FrameLayout are confined to the Android backend target. |
| `AndroidViewPeer`: constructor, Resize, RequestFocus, TryMoveFocus, Dispose | Supported public Android lease helper. Fresh parentless View from the exact site Context; borrowing does not permit migration across Activities. Failed construction undoes partial attachment and frees its LayoutParams without transferring View ownership. |
| `TestWindowHost.NativeViews` | Supported TestHost recorder access; simulation only. |
| `TestNativeViewHostProvider`: Capabilities, CapabilityQueries, IsAvailable, IsVisible, IsEnabled, Changed, Sessions, FrameworkFocusRequests, SetAvailable, CreateSession, FocusFramework | Public sealed simulation/inspection API with internal construction. Contract implementation supports framework tests; application tests use SetAvailable and recorder properties. No OS support claim. |
| `TestNativeViewSession`: Generation, IsCurrent, IsFocused, IsDisposed, Placement, UpdateCount, FocusRequests, InFrontGeneration, TryFocus, MoveFocus, RestoreFocus, Update, RequestFocus, ReturnFocus, PlaceAbove, Dispose | Public sealed simulation/inspection API with internal construction. Callback entry points are guarded; it never creates an OS peer. |
| `NativeViewHostingPanel`: constructor | Public ControlGallery page following the existing gallery convention; not framework library API. |
| `NativeViewValidationInstrumentation`: constructors, OnCreate, OnStart | Public Android test runner needed for JNI instrumentation discovery; not normal application startup or a framework extension point. |

`PrivateApi` in this repository identifies framework/backend-only audience.
Removing it from the supported factory/peer/site extension surface makes external
use explicit. `NotClientImplementable` is appropriate for consumption-only
interfaces whose implementation belongs to the framework; it is deliberately
absent from factories and peer leases. All helper services/interop/order and
invalidation snapshots remain internal or private.

## External feature package use

An independent assembly can derive a control from NativeViewHost, implement
INativeViewFactory/INativeViewPeer and consume typed sites without reflection or
friend assemblies. A Windows controller is initialized beneath
WindowsNativeViewSite.ParentWindow; an Android native component is created with
AndroidNativeViewSite.Context and wrapped in AndroidViewPeer. The feature never
copies layout, ancestor clipping, scrolling, DPI/density, visibility, validation,
parent-HWND discovery or Activity recreation.

The compile proof builds independent neutral, Windows and Android assemblies
using these public members. It delegates native content creation to the feature;
it installs no WebView2/browser/media package. Future WebView controller content
resize and asynchronous initialization belong to its peer lease. A lease must
cancel pending initialization and reject results after `site.IsCurrent` becomes
false. Future native video content uses the same parent/context; no API requires
bitmap capture or CPU texture copying. GPU compositor/decoder/product behavior
remains separate work.

## Review invariants

- Host owns session; session owns container and peer lease; feature owns factory.
  Explicit disposal runs on the UI thread. Finalization performs no native work.
  Revocation precedes cleanup; mandatory container cleanup continues after a peer
  disposal exception. Failed retirement cannot leave Attached diagnostics.
- The canonical ControlFocusScope owns selection and validation. Both native
  entry and shared entry use it; native loss never clears a newer shared owner.
  Fallback focus checks ownership after callbacks. Internal traversal delegates
  only at its boundary to existing SelectNextControl.
- Android sessions belong to one window/presentation. Observers subscribe after
  root attachment and unsubscribe the exact retained wrapper. Stable-provider
  detach compares presentation identity even after epoch revocation. Replacement
  Activities receive fresh sites and Views, before old native roots are released.
- Native containers form one band above same-window Skia. Existing sibling and
  ancestor order defines native order. Popup/modal windows provide overlay.
  Rectangular intersection and one device conversion remain authoritative;
  unsupported composition hides retained peers and never forwards non-finite
  coordinates.
- Ordinary invalidation observes only relevant mutable composition/client values
  on subscribed paths. Unchanged values cause no hosting/provider call. Canonical
  scaled bounds retain rounding/override semantics with allocation-free flag
  tests. No timer, second renderer, focus manager, layout engine or Activity
  registry is introduced.
- Designer paints a derived-control placeholder without invoking its factory.
  Diagnostics expose only geometry, state, generation and safe reasons. Native
  accessibility stays in the OS subtree without synthetic MFN descendants.

See [native hosting usage and capability matrix](../native-view-hosting.md).
