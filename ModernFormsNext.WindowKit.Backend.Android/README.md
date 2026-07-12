# ModernFormsNext WindowKit Android backend

This project is the Android platform foundation for WindowKit. It targets `net10.0-android` and
currently provides:

- explicit backend registration and initialization;
- process-wide `Application Context` ownership;
- weak `Activity` tracking through `Application.IActivityLifecycleCallbacks`;
- a main `Looper`/`Handler` dispatcher;
- platform-neutral permission contracts with Android SDK-aware mapping;
- final-manifest validation and serialized runtime permission requests.

It does **not** yet render ModernFormsNext controls or implement Android windows, clipboard,
camera capture, microphone capture, notifications, media, WebView, file pickers, sharing, or
drag-and-drop. Unsupported services are deliberately not registered.

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
[Android smoke test](../samples/ModernFormsNext.Android.SmokeTest/README.md).
