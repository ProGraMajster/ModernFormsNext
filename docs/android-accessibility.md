# Android accessibility backend (issue #59, Phase 3)

## Audit before implementation

Baseline: `028b32da615632810d0eb7e62815d1ac0397bb3a` (Phase 2 PR #103),
verified against master and the open issue on 2026-09-05.

The Android backend has one `AndroidSkiaHostView` (`SKCanvasView`). The cross-platform
sample's `AndroidAppHost` borrows its process-owned `App.Root` through `SkiaControlSurface`.
The activity forwards attach/start/resume/pause/stop/configuration/dispose; recreation
preserves the shared tree and replaces the surface. Rendering scales the canvas once by
`Density`, while input converts physical pixels to logical coordinates. The surface uses
normal control selection/focus and its existing IME bridge. It does not implement
`IWindowBaseImpl` or the full Android application host contract.

There was no AccessibilityNodeProvider, delegate, virtual node mapping, touch exploration,
or accessibility event integration. Window-based notifications require `FindWindow()` and
therefore did not reach this windowless surface. Phase 3 adds an internal surface notification
route and exposes its existing root through `IPlatformAccessibilityHost`. The canonical
`AccessibleObject` -> `PlatformAccessibleObjectAdapter` transport from Phases 1/2 is reused.
The internal transport's historical UIA name does not imply Windows dependencies.

The Android project already has a plain net10.0 target for deterministic backend tests and
a native net10.0-android target. No new test project or dependency is required. The available
sample Release configuration enables AOT; minimum Android API is 23. The installed SDK
rolls forward from global.json's 10.0.201 to 10.0.400 under `latestFeature`.

The existing ComboBox popup requires a Form; the windowless Android host cannot expand it.
Phase 3 must not advertise an unavailable expansion action or implement #72 to supply it.
Full scroll, advanced text/IME (#62), application lifecycle (#63), and diagnostics (#61)
remain separate work.

Native mapping follows Android's [virtual descendant provider contract](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeProvider)
and [node information API](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo).
