# Android backend

The Android backend is an early platform foundation with an experimental shared-control Skia
surface, not a complete ModernFormsNext window backend.
Windows remains the primary and best-supported runtime. The Android project targets
`net10.0-android` with API 23 as its current minimum and has no MAUI or AndroidX dependency.

## Startup

Initialize from Android application startup or the first activity before accessing WindowKit
dispatcher services:

```csharp
var backend = AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this)
{
    PermissionRequestTimeout = TimeSpan.FromMinutes(2),
    DiagnosticSink = message => Android.Util.Log.Info("MFN.WindowKit", message)
});

AndroidWindowKit.ObserveHostActivity(this);
```

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
`ObserveHostActivity` once so that activity is immediately available. Subsequent transitions are
automatic.

An optional `ActivityProvider` exists for hosts with their own lifecycle integration. It is invoked
on demand; the delegate must not keep destroyed activities alive.

## Dispatcher

`AndroidMainThreadDispatcher` uses `Looper.MainLooper` and `Handler`. It provides:

- `CheckAccess()`;
- asynchronous `Post(Action)`;
- `InvokeAsync(Action)` and `InvokeAsync<T>(Func<T>)`;
- pre-execution cancellation;
- exception propagation through returned tasks;
- inline invocation on the main thread to avoid self-deadlock.

The dispatcher is registered in the lightweight `PlatformServiceRegistry` and is also available as
`AndroidWindowKit.Current.Dispatcher`. It does not yet claim to be the event-loop implementation for
ModernFormsNext Android windows, because that UI backend has not been built.

## Shared-control Skia surface

`AndroidSkiaHostView` is one `SKCanvasView` that owns Android activity/surface lifecycle, density
conversion, resize, multi-pointer tracking, hardware editing keys, IME connection, coalesced
invalidation, and disposal.
`SkiaControlSurface` belongs to the core framework and adapts a real `Control` tree to that canvas,
including framework layout, paint, hit testing, pointer capture, selection, and committed-text
routing. This is the pipeline used by `ModernFormsNext.CrossPlatform.Sample`.

The view renders only after invalidation or resize. It does not run a permanent frame timer.
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

The integration is deliberately narrower than `Application.Run(Form)`: Android does not yet
implement the complete `IWindowImpl`/`IWindowingPlatform` contract, multiple windows, native
dialogs, accessibility bridging, clipboard, drag-and-drop, or platform cursor artwork. Do not
describe this slice as full Android parity.

## Runtime permission callback

The backend uses the supported platform `Activity.RequestPermissions` API without adding AndroidX.
The activity forwards results to the one central coordinator:

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

- A single real ModernFormsNext control tree can render through the shared-control Skia surface, but
  general `Application.Run(Form)` and Android window creation are not implemented.
- No Android `IWindowingPlatform`, clipboard, notification delivery, camera/media capture, WebView,
  file picker, sharing, or drag-and-drop service exists yet.
- Android 14 selected-photo access is not represented as a partial grant. Prefer a system photo
  picker for user-selected images until a dedicated media-selection API is designed.
- Runtime dialogs require host callback forwarding; this avoids an AndroidX dependency in this
  foundation.
- Platform mapping, queue, density, lifecycle, invalidation, resize, Unicode input-state, and
  disposal behavior run as `net10.0` tests. Deployment remains an explicit device/emulator step
  through repository scripts.

## Troubleshooting

- `NotDeclared`: add the exact `<uses-permission>` reported by the diagnostic to the application
  manifest, rebuild, and inspect the merged manifest.
- `Unknown` with “no active Activity”: wait for `OnResume`; do not request from a background service.
- `PermanentlyDenied`: explain why the feature needs access and offer an explicit action that calls
  `OpenApplicationSettingsAsync`.
- Request never completes: verify `OnRequestPermissionsResult` is forwarded and inspect the
  the stable `ModernFormsNext` logcat tag.
