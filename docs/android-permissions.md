# Android manifests and permissions

Android authorization has separate build-time and runtime concepts:

- A **manifest permission** is an application's declaration that it may use a capability. Every
  requested permission must be present in the final manifest.
- A **normal permission** is granted by the system without a runtime dialog after declaration.
- A **dangerous permission** also needs runtime user approval on Android 6/API 23 and newer.
- A **special permission** is controlled by application or system settings rather than a standard
  runtime dialog.
- A **uses-feature** declaration describes hardware/software compatibility and store filtering. It
  does not grant access and does not replace a permission.

Android's manifest overview documents the declaration requirement and runtime model, while the
`uses-feature` reference explains device filtering:

- <https://developer.android.com/guide/topics/manifest/manifest-intro>
- <https://developer.android.com/guide/topics/manifest/uses-feature-element>

## Library manifest and merging

The backend's `Properties/AndroidManifest.xml` contains no functional permissions. A .NET Android
application supplies its own manifest template. During build, .NET for Android generates component
entries and merges library manifests and overlays into the effective manifest using Google's
manifest merger by default. Relevant official build properties are documented at
<https://learn.microsoft.com/dotnet/android/building-apps/build-properties>.

The application — not the backend — opts into every capability it uses:

```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          package="com.example.myapp">
  <uses-permission android:name="android.permission.CAMERA" />
  <uses-permission android:name="android.permission.RECORD_AUDIO" />
  <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />

  <uses-feature android:name="android.hardware.camera.any"
                android:required="false" />
</manifest>
```

Set `required="false"` when the application remains useful without that hardware. A camera
permission can otherwise imply required camera hardware and affect store filtering.

Do not add every possible permission. Declarations affect privacy review, user trust, store policy,
and device filtering. Clipboard is not in `PlatformPermission` because ordinary clipboard access is
not an Android runtime permission.

### Inspect the final manifest

Build the application, then inspect the generated manifest under its intermediate output:

```powershell
dotnet build .\samples\ModernFormsNext.Android.SmokeTest\ModernFormsNext.Android.SmokeTest.csproj
rg "uses-permission|uses-feature" `
  .\samples\ModernFormsNext.Android.SmokeTest\obj\Debug\net10.0-android\AndroidManifest.xml
```

For package-stage details, also inspect
`obj\Debug\net10.0-android\android\AndroidManifest.xml`. Debug builds can contain permissions added
by .NET Android deployment tooling; validate Release output for shipping policy. In the smoke test,
the backend library manifest is visible below the `lp` intermediate folder and contributes no
feature permission. `RECORD_AUDIO` remains absent, proving that the service does not declare it.

Attributes such as `[UsesPermission]` also generate entries. They are intentionally not used by the
backend because an attribute in a library would silently opt every consuming application into that
permission.

## Neutral API

Resolve `IPermissionService` from the initialized backend or use its strongly typed property:

```csharp
var result = await AndroidWindowKit.Current.Permissions
    .RequestAsync(PlatformPermission.Camera, cancellationToken);

if (result.Status == PlatformPermissionStatus.NotDeclared)
    Log(result.DiagnosticMessage);
```

Statuses are `Unknown`, `Granted`, `Denied`, `Restricted`, `PermanentlyDenied`, `NotDeclared`, and
`NotSupported`. `Restricted` is available for platform policy integrations; the first Android
implementation does not manufacture it when Android exposes only a simple grant/deny result.

The service validates the final installed package declarations through `PackageManager` before any
runtime request. A missing declaration returns `NotDeclared`, does not open a dialog, and names the
exact Android permission to add.

## SDK mapping

The mapper receives an API level, so `Build.VERSION.SdkInt` is not scattered through the service:

| Logical permission | Android mapping |
|---|---|
| Camera | `CAMERA` |
| Microphone | `RECORD_AUDIO` |
| Notifications | API 33+: `POST_NOTIFICATIONS`; older: no runtime request |
| Photos | API 33+: `READ_MEDIA_IMAGES`; API 23–32: `READ_EXTERNAL_STORAGE` |
| Videos | API 33+: `READ_MEDIA_VIDEO`; API 23–32: `READ_EXTERNAL_STORAGE` |
| Audio | API 33+: `READ_MEDIA_AUDIO`; API 23–32: `READ_EXTERNAL_STORAGE` |
| LocationWhenInUse | coarse or fine foreground location |
| LocationAlways | API 29: staged background runtime request; API 30+: application settings |
| BluetoothScan | API 31+: `BLUETOOTH_SCAN`; API 23–30: foreground location |
| BluetoothConnect | API 31+: `BLUETOOTH_CONNECT`; older: normal `BLUETOOTH` declaration |
| NearbyDevices | API 33+: `NEARBY_WIFI_DEVICES`; API 31–32: scan/connect; API 23–30: location |

Android 13 introduced granular media and nearby-Wi-Fi permissions; Android 12 introduced modern
Bluetooth permissions. Background location on Android 11/API 30 and newer is selected in settings,
not from an “Allow all the time” runtime choice:

- <https://developer.android.com/about/versions/13/behavior-changes-13>
- <https://developer.android.com/about/versions/12/behavior-changes-12>
- <https://developer.android.com/develop/sensors-and-location/location/permissions/background>

For app-owned media or user-selected files, prefer scoped storage and system pickers rather than
requesting broad read access merely because the mapper supports it.

## Requests, rationale, and settings

`RequestAsync(IEnumerable<PlatformPermission>)` deduplicates logical permissions and Android
permission strings, then opens at most one dialog. Concurrent callers are serialized. Cancellation
does not abandon an already-visible native dialog or permit a second dialog to overlap it.

After a denial, `ShouldShowRationale` delegates to the current Activity. The backend records that a
permission was requested in private preferences. A later denied state with no rationale is reported
as `PermanentlyDenied`; the service does not ask again automatically. The host should explain the
feature and expose a user-initiated action for `OpenApplicationSettingsAsync`.

`LocationAlways` is intentionally staged. Request foreground location first. API 29 can then show a
separate background dialog; API 30+ reports `ApplicationSettings` and never opens settings without
the host's explicit call.

When no resumed Activity exists, checks still work, but a runtime request returns `Unknown` with an
actionable diagnostic. Settings navigation returns `false`. Activity destruction completes the
native operation with an error so queued requests do not remain suspended forever.
