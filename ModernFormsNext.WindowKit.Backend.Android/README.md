# ModernFormsNext WindowKit Android backend

This project is the Android platform foundation for WindowKit. It targets `net10.0-android` and
currently provides:

- explicit backend registration and initialization;
- process-wide `Application Context` ownership;
- weak `Activity` tracking through `Application.IActivityLifecycleCallbacks`;
- a main `Looper`/`Handler` dispatcher;
- platform-neutral permission contracts with Android SDK-aware mapping;
- final-manifest validation and serialized runtime permission requests.
- a lifecycle-aware, density-aware `AndroidSkiaHostView` shared-control surface;
- logical multi-pointer, resize, invalidation, render-count, hardware-key, and complete IME event
  translation.

Together with core `SkiaControlSurface`, the backend can render one real ModernFormsNext control
tree in a host activity. It does **not** yet implement general Android windows or
`Application.Run(Form)`, clipboard, camera capture, microphone capture, notifications, media,
WebView, file pickers, sharing, accessibility bridging, or drag-and-drop. Unsupported services are
deliberately not registered.

The surface separates Activity state from native view attachment, coalesces invalidation without a
continuous frame timer, and cancels pointer capture during pause/stop/detach. Its input connection
reports surrounding text/selection/composition and routes Unicode commit, composing text,
selection, deletion, Enter/arrows, and hardware editing keys into the selected framework control.
It does not use a hidden native `EditText` or an Android-specific control tree.

## Initialize

Initialize before using Android platform dispatcher or permission services. Calling from the first
activity is supported:

```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);

    AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this)
    {
        DiagnosticSink = message => Android.Util.Log.Info("MFN.WindowKit", message)
    });

    AndroidWindowKit.ObserveHostActivity(this);
}
```

`ObserveHostActivity` covers initialization that happens during the first activity callback. The
registered application lifecycle callbacks track subsequent foreground, background, rotation, and
destroy/create transitions. Activity references are weak.

The host activity must forward Android's permission result callback:

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

## Manifest policy

The library manifest at `Properties/AndroidManifest.xml` declares no camera, microphone, media,
location, notification, Bluetooth, or nearby-device permissions. Applications opt into only the
capabilities they use. Runtime permission calls do not alter the manifest and return `NotDeclared`
before opening a dialog when a required entry is missing.

See [Android backend](../docs/android-backend.md),
[Android permissions](../docs/android-permissions.md), and the
[Android smoke test](../samples/ModernFormsNext.Android.SmokeTest/README.md). The end-to-end shared
control pipeline is demonstrated by
[the cross-platform sample](../samples/ModernFormsNext.CrossPlatform.Sample/README.md).
