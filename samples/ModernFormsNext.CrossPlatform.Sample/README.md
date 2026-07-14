# ModernFormsNext cross-platform sample

This directory is one multi-target application project targeting `net10.0-windows` and
`net10.0-android`. `App`, `MainPage`, state, layout, and interaction logic compile unchanged for
both targets. Platform startup and native adaptation are isolated under `Platforms/Windows` and
`Platforms/Android`; there is no MAUI, XAML, AndroidX, separate shared project, or duplicated page.

The shared page uses real ModernFormsNext controls and exercises scrolling, resizing, single-line
and multiline IME input, Polish/Asian/RTL text, emoji, focus, buttons, checkboxes, flow layout,
dispatcher callbacks, lifecycle/density diagnostics, and an explicit camera permission flow.

Windows attaches `App.Root` to a normal ModernFormsNext `Form`. Android creates one Skia view and
adapts touch, hardware keys, IME, density, invalidation, and lifecycle into the same framework
control pipeline. Android support remains experimental and is not yet a complete `Form`/window
backend.

Touch uses stable pointer IDs, deepest-control hit testing, independent capture, one-click tap
semantics, drag cancellation, and the real `ScrollableControl` scrollbar state. The diagnostic
area separates control-action receipt from platform-service invocation and completion.

From the repository root:

```powershell
.\scripts\windows\Run-CrossPlatformSample.ps1
.\scripts\android\Run-CrossPlatformSample.ps1 -DeviceId <serial>
```

See [`docs/cross-platform-sample.md`](../../docs/cross-platform-sample.md) for Visual Studio steps,
ADB/emulator commands, limitations, and the complete manual validation checklist.
