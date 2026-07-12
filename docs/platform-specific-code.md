# Platform-specific code architecture

ModernFormsNext keeps shared framework APIs independent from operating-system namespaces. The
platform boundary has three layers:

1. `ModernFormsNext.WindowKit` contains neutral contracts such as `IPlatformDispatcher` and
   `IPermissionService`.
2. `ModernFormsNext.WindowKit.Backend` owns `IWindowKitBackend`, the single-backend registry, and
   bootstrap infrastructure that contains no Android or Win32 types.
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
services to be used without loading desktop rendering/windowing dependencies. The first Android
iteration registers:

- `IPlatformDispatcher`;
- `IPermissionService`.

It does not register `IWindowingPlatform`, `IClipboard`, or empty placeholders. Consumers therefore
receive a controlled missing-service failure instead of a false success.

## Source isolation

Android-native code is compiled only for `net10.0-android` under the Android backend's `Platform/`
directory. Deterministic permission mapping, manifest-validation, status-classification, and request
queue logic also compile for `net10.0` so tests can run without an emulator. Neutral contracts live
in the lightweight `WindowKit.Backend` assembly, which avoids pulling desktop Skia/System.Drawing
dependencies into an Android app. Shared public APIs contain no `Activity`, `Context`, Android
manifest constants, Win32 handles, or platform enums.

When future services are added, introduce or reuse a neutral contract in WindowKit and implement it
inside each platform backend. Do not scatter `#if ANDROID` through controls, rendering, or layout.
Features that do not exist on a platform should return a documented `NotSupported` result or leave
the service unregistered; they must not silently pretend to work.

## Future Android service boundaries

Clipboard, OpenUri, sharing, file pickers, notifications, WebView, media, camera, microphone, and
drag-and-drop should each remain Android backend services. Only capability-shaped DTOs and contracts
belong in shared code. Android `Intent`, `Activity`, `Context`, `Uri`, and permission strings remain
implementation details of the Android assembly.
