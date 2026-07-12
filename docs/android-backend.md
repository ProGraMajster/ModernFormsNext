# Android backend

The Android backend is an early platform foundation, not a complete ModernFormsNext UI backend.
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

During rotation, destruction clears the old weak reference. A permission request owned by that
activity completes with a diagnostic instead of hanging; the host can retry after the replacement
activity is resumed. When initialization occurs inside the first activity's `OnCreate`, call
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

## Smoke test

`samples/ModernFormsNext.Android.SmokeTest` is a native technical host. It displays backend,
Activity, lifecycle, SDK, camera, microphone, and notification state. Camera and notifications are
declared; microphone is deliberately omitted to exercise `NotDeclared`. See its README for the
manual rotation, denial, settings, and manifest checklist.

## Limitations

- ModernFormsNext controls and forms do not yet render on Android.
- No Android `IWindowingPlatform`, clipboard, notification delivery, camera/media capture, WebView,
  file picker, sharing, or drag-and-drop service exists yet.
- Android 14 selected-photo access is not represented as a partial grant. Prefer a system photo
  picker for user-selected images until a dedicated media-selection API is designed.
- Runtime dialogs require host callback forwarding; this avoids an AndroidX dependency in this
  foundation.
- No emulator/device automation is part of the ordinary test suite. Platform mapping and queue
  behavior run as `net10.0` tests; device behavior is covered by the smoke test.

## Troubleshooting

- `NotDeclared`: add the exact `<uses-permission>` reported by the diagnostic to the application
  manifest, rebuild, and inspect the merged manifest.
- `Unknown` with “no active Activity”: wait for `OnResume`; do not request from a background service.
- `PermanentlyDenied`: explain why the feature needs access and offer an explicit action that calls
  `OpenApplicationSettingsAsync`.
- Request never completes: verify `OnRequestPermissionsResult` is forwarded and inspect the
  `MFN.WindowKit` diagnostic log.
