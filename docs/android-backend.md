# Android backend

> [!WARNING]
> Android support in ModernFormsNext 1.11.0 is **Experimental** and is not recommended for
> production applications. See the [Android platform status](platforms/android.md) for the
> supported vertical slice and current limitations.

The Android backend includes an experimental [Application/Form window host](android-windowing.md),
software Skia rendering and shared animation-runtime integration, with explicit mobile limitations.
Windows remains the primary and best-supported runtime. The Android project targets
`net10.0-android` with API 23 as its current minimum and has no MAUI or AndroidX dependency.

## Startup

Initialize on the Android main thread from the native Application's `OnCreate`, before accessing
WindowKit dispatcher or windowing services:

```csharp
var backend = AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this)
{
    PermissionRequestTimeout = TimeSpan.FromMinutes(2),
    DiagnosticSink = message => Android.Util.Log.Info("MFN.WindowKit", message)
});
```

Use `AndroidWindowActivity.OnStartApplication` to call `Application.Run(new MainForm(...))` once.
The backend supplies the Activity/Skia presentation and reconnects surviving Forms after Activity
recreation. See the complete [startup example](android-windowing.md#startup); normal application
startup does not manually construct a Skia host view or control surface.

Repeated initialization with the same Application Context is idempotent. A different context or a
second platform backend is rejected. The normalized `AndroidApplicationContext` retains the
process-lifetime `Application`, never the activity.

## Activity and lifecycle

`AndroidActivityTracker` registers as `Application.IActivityLifecycleCallbacks`. It keeps the latest
activity through `WeakReference<Activity>` and reports `Unknown`, `Created`, `Foreground`,
`Background`, or `NoActivity`. Only a resumed, non-finishing, non-destroyed activity is eligible to
show permission UI or open settings.

During rotation, destruction clears the weak reference only when the destroyed activity is still
the current host. A delayed callback from the old activity therefore cannot erase a replacement
activity that has already been created or resumed. A permission request owned by the old activity
completes with a diagnostic instead of hanging; the host can retry after the replacement activity
is resumed. When initialization occurs inside the first activity's `OnCreate`, call
`ObserveHostActivity` only if a service must use that not-yet-resumed Activity immediately.
Animation surfaces must be marked active only from their normal start/resume lifecycle. Subsequent
Activity transitions are automatic.

An optional `ActivityProvider` exists for hosts with their own lifecycle integration. It is invoked
on demand; the delegate must not keep destroyed activities alive.

### Experimental animation runtime and platform preference

The backend registers `AndroidPlatformAnimationSettings` through the shared backend service
registry. When an application context exists, it reads
`Settings.Global.ANIMATOR_DURATION_SCALE`. Its exact scalar is combined with application policy on
the UI dispatcher; zero requests reduced motion and immediate, deterministic targets.

The provider refreshes during startup, on foreground entry, and when application code calls
`AnimationScheduler.RefreshPlatformPolicy()`. A main-thread ContentObserver is registered only
while the application is foregrounded and the scheduler is subscribed; it uses the application
resolver and unregisters on background or disposal. A missing context, failed read, or observer
failure uses compatibility defaults or refresh fallback, records diagnostics, and does not fail
application startup.

The scheduler uses one demand-driven Choreographer callback while an attached and resumed Skia
surface exists. It does not assume 60 Hz or maintain a timer per control. Detailed ownership,
frame, lifecycle, cleanup, capability, and validation guidance is in
[Android animation runtime architecture](architecture/android-animation-runtime.md).

## Dispatcher

`AndroidMainThreadDispatcher` uses `Looper.MainLooper` and `Handler`. It provides:

- `CheckAccess()`;
- asynchronous `Post(Action)`;
- `InvokeAsync(Action)` and `InvokeAsync<T>(Func<T>)`;
- pre-execution cancellation;
- exception propagation through returned tasks;
- inline invocation on the main thread to avoid self-deadlock.

The dispatcher is registered in `PlatformServiceRegistry` and as WindowKit's externally owned
dispatcher implementation. `Application.Run` retains application lifetime after returning to the
native Looper. `AndroidWindowKit.Current.Dispatcher` remains available for platform services.

## Shared-control Skia surface

`AndroidSkiaHostView` is one `SKCanvasView` that owns Android activity/surface lifecycle, density
conversion, resize, multi-pointer tracking, hardware editing keys, IME connection, coalesced
invalidation, and disposal.
`SkiaControlSurface` belongs to the core framework and adapts a real `Control` tree to that canvas,
including framework layout, paint, hit testing, pointer capture, selection, and committed-text
routing. This low-level API remains available. The cross-platform sample now uses Application.Run
and Form: its canonical adapter renders through WindowBase and borrows the same surface input
router without creating a second control or focus root.

The view renders only after invalidation or resize. It does not run a permanent frame timer; the
shared animation scheduler requests Choreographer callbacks only while animation work remains.
Canvas and Android objects remain platform-owned; the adapter borrows the shared control root so
activity recreation can detach and reattach without discarding application state.

Activity state and native view attachment are tracked independently. A detached or paused view
does not render; one pending invalidation survives until it is attached and resumed. Pointer
cancellation, pause, stop, detach, and disposal clear all tracked pointers and framework capture.

Every Android pointer ID has independent framework capture. A down transition targets the deepest
enabled control and supplies coordinates local to that control. A small move remains tap-eligible;
crossing the logical-pixel drag threshold cancels the child press. If the target has an
`AutoScroll` ancestor, that ancestor then updates its real horizontal/vertical scrollbar values,
so content position, scrollbar thumbs, clamping, and `Scroll` notifications stay synchronized.
Touch movement does not synthesize desktop hover. A valid tap raises exactly one `Click`, before
`MouseUp`, while release outside capture, scrolling, cancellation, detach, and lifecycle loss do
not click. This path is shared core behavior and contains no Android API dependency.

The input connection supplies surrounding text, UTF-16 selection, and composition to Android.
Commit/composition/finish, selection, deletion in UTF-16 or code points, Enter, Delete, Backspace,
and arrow keys route into the selected framework `TextBox`. Surrogate pairs and complete framework
text elements are preserved. No native `EditText` is used.

The low-level surface remains available alongside the newer Application/Form host. Independent
desktop windows, native service dialogs, clipboard, drag-and-drop and custom cursor artwork remain
outside this mobile window policy. See [the capability matrix](android-windowing.md#capability-matrix).

## Runtime permission callback

The backend uses the supported platform `Activity.RequestPermissions` API without adding AndroidX.
`AndroidWindowActivity` forwards results to the one central coordinator automatically. Custom
Activities, including the native foundation smoke host, forward them explicitly:

```csharp
public override void OnRequestPermissionsResult(
    int requestCode,
    string[] permissions,
    Permission[] grantResults)
{
    if (!AndroidWindowKit.HandleRequestPermissionsResult(requestCode, permissions, grantResults))
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
}
```

Only one native dialog can be active. Later requests wait in a queue. If a caller cancels after a
dialog is visible, that caller's task is canceled promptly but the native operation keeps the queue
gate until Android responds, the owning activity is destroyed, or the configured timeout expires.
`NotDeclared`, `NotSupported`, `Granted`, and `PermanentlyDenied` are terminal results and never
continue to `RequestPermissions`; in particular, a missing manifest declaration is reported to the
shared caller without attempting to display a platform dialog.

## Smoke test

`samples/ModernFormsNext.Android.SmokeTest` is a native technical host. It displays backend,
Activity, lifecycle, SDK, camera, microphone, and notification state. Camera and notifications are
declared; microphone is deliberately omitted to exercise `NotDeclared`. See its README for the
manual rotation, denial, settings, and manifest checklist.

The separate `samples/ModernFormsNext.CrossPlatform.Sample` is the real shared-control vertical
slice. It is not a replacement for the native foundation smoke test; each sample has a different
validation role.

## Limitations

- The source-tree Application/Form host supports one main Form and owned modal/popup surfaces;
  desktop window-management parity is not provided.
- No complete Android clipboard, notification delivery, camera/media capture, WebView,
  file picker, sharing, or drag-and-drop service exists yet.
- Android 14 selected-photo access is not represented as a partial grant. Prefer a system photo
  picker for user-selected images until a dedicated media-selection API is designed.
- Runtime permission dialogs require callback forwarding, supplied by `AndroidWindowActivity`
  or explicitly by a custom Activity; this avoids an AndroidX dependency in this foundation.
- Platform mapping, queue, density, lifecycle, invalidation, resize, Unicode input-state, and
  disposal behavior run as `net10.0` tests. Deployment remains an explicit device/emulator step
  through repository scripts.

See the [central known-limitations index](known-limitations.md) for issue mapping and validation
priorities. Automated host tests and successful Android builds are not physical-device evidence.

## Troubleshooting

- `NotDeclared`: add the exact `<uses-permission>` reported by the diagnostic to the application
  manifest, rebuild, and inspect the merged manifest.
- `Unknown` with “no active Activity”: wait for `OnResume`; do not request from a background service.
- `PermanentlyDenied`: explain why the feature needs access and offer an explicit action that calls
  `OpenApplicationSettingsAsync`.
- Request never completes: verify `OnRequestPermissionsResult` is forwarded and inspect the
  the stable `ModernFormsNext` logcat tag.
