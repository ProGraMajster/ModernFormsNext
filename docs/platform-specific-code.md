# Platform-specific code architecture

ModernFormsNext keeps shared framework APIs independent from operating-system namespaces. The
platform boundary has three layers:

1. `ModernFormsNext.WindowKit` contains neutral windowing contracts such as `IWindowingPlatform`
   and `IWindowImpl`.
2. `ModernFormsNext.WindowKit.Backend` owns foundation contracts such as `IPlatformDispatcher`
   and `IPermissionService`, `IWindowKitBackend`, the single-backend registry, and bootstrap
   infrastructure that contains no Android or Win32 types.
3. Platform projects implement those contracts. Windows stays in
   `ModernFormsNext.WindowKit.Backend.Windows`; Android stays in
   `ModernFormsNext.WindowKit.Backend.Android`.

`WindowKitBackendRegistry.Register` initializes one backend and publishes it only after successful
initialization. Registering another platform in the same process fails with a diagnostic exception,
which prevents dispatcher and service replacement after controls have started using them. The
existing `FrameworkBootstrap` discovery path remains compatible for desktop Windows startup.
Android uses explicit `AndroidWindowKit.Initialize(options)` because discovery alone cannot supply
an Application Context or lifecycle.

Full desktop WindowKit services continue to use the established `AvaloniaGlobals` registry. The
lightweight backend layer now also exposes `PlatformServiceRegistry`, allowing Android foundation
services to share neutral contracts. The Android Application/Form host additionally uses WindowKit
and the shared rendering stack, without depending on the Windows backend. The foundation registers:

- `IPlatformDispatcher`;
- `IPlatformApplicationLifecycle`;
- `IPlatformAnimationSettings`;
- `IPlatformAnimationFrameSource`;
- `IPermissionService`.

The source-tree Android window host also registers `IWindowingPlatform` and the external-loop
`IDispatcherImpl` through the existing WindowKit registry. SAF storage, launcher, sharing and
local notification and native message-dialog contracts use that same registry; see [Android services](android-platform-services.md).
It does not register an empty
`IClipboard` or other placeholder services. Unsupported services remain explicit failures;
desktop window operations follow the [Android capability policy](android-windowing.md).

## Source isolation

Android-native code is compiled only for `net10.0-android` under the Android backend's `Platform/`
directory. Deterministic permission mapping, manifest-validation, status-classification, and request
coordinator logic also compile for `net10.0` so tests can run without an emulator. Foundation service
contracts live in the lightweight `WindowKit.Backend` assembly; windowing contracts live in
`WindowKit`. Android uses the shared Skia rendering types through those windowing contracts.
Shared public APIs contain no `Activity`, `Context`, Android
manifest constants, Win32 handles, or platform enums.

When future services are added, introduce or reuse a neutral contract in WindowKit and implement it
inside each platform backend. Do not scatter `#if ANDROID` through controls, rendering, or layout.
Features that do not exist on a platform should return a documented `NotSupported` result or leave
the service unregistered; they must not silently pretend to work.

`SystemMessageBox` is a shared async facade returning the existing `DialogResult`.
`IPlatformMessageDialogService` transports a neutral request, optional backend window and semantic
button index. AlertDialog and MessageBoxW stay inside their platform backends. MessageBoxForm
continues to use framework rendering. Android messages use the existing bounded request
coordinator and presentation epochs; they are not native child views or a second semantic tree.

## Future Android service boundaries

OpenUri, sharing, SAF pickers and basic notifications are implemented in the Android backend.
Clipboard, WebView, media, camera/microphone features and drag-and-drop remain separate work. Only capability-shaped DTOs and contracts
belong in shared code. Android `Intent`, `Activity`, `Context`, `Uri`, and permission strings remain
implementation details of the Android assembly.

The implemented and missing platform boundaries are summarized in
[Known limitations](known-limitations.md).
