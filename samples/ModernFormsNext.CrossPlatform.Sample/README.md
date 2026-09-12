# ModernFormsNext cross-platform sample

This directory is one multi-target application project targeting `net10.0-windows` and
`net10.0-android`. `App`, `MainPage`, state, layout, and interaction logic compile unchanged for
both targets. Platform startup and native adaptation are isolated under `Platforms/Windows` and
`Platforms/Android`; there is no MAUI, XAML, AndroidX, separate shared project, or duplicated page.

The shared page uses real ModernFormsNext controls and exercises scrolling, resizing, single-line
and multiline IME input, Polish/Asian/RTL text, emoji, focus, buttons, checkboxes, flow layout,
dispatcher callbacks, lifecycle/density diagnostics, an explicit camera permission flow, and the
shared animation runtime. The animation smoke section covers ripple, press scale, visual states,
layout/presentation transitions, simultaneous animations, theme transitions, reduced motion, and
idle-to-wake diagnostics.

The **Use system accessibility preferences** checkbox is an explicit opt-in to the
sample's authored normal/high-contrast themes and one-time scaling of theme typography.
It begins unchecked, reports unknown detection separately, and uses an owned UI-dispatched
subscription that survives borrowed Activity/surface recreation. The page measures larger
text rows; its existing scrollbars expose overflow from single-line controls. Explicit
fonts stay authored. Turning following off retains the current theme. See
[accessibility preferences](../../docs/accessibility/preferences.md) for platform sources,
lifetime and the separate validation status; physical/manual preference checks are not
implied by the headless tests.

The Android-only `ACCESSIBILITY_PHASE4` intent opens a separate, opt-in fixture of
real link, numeric, date, grid, rich-text, protected-text and scroll controls. Its
`AccessibilityPhase4Instrumentation` runner uses the native `UiAutomation` service
connection. It leaves the historical `ACCESSIBILITY_DEMO` fixture and Phase 3 runner
unchanged. See [Phase 4 native checks](../../docs/accessibility/android-phase4-validation.md)
for the exact invocation, coverage and pending evidence. An ordinary launch does not
open or execute either instrumentation fixture.

Windows attaches `App.Root` to a normal ModernFormsNext `Form`. Android creates one Skia view and
adapts touch, hardware keys, IME, density, invalidation, and lifecycle into the same framework
control pipeline. Android support remains experimental and is not yet a complete `Form`/window
backend.

The shared command section uses Ctrl+S, Ctrl+Shift+S and F1 with real command-backed buttons
and an availability fallback. Android connects the native hardware-key handler to the same
surface input resolver; Activity recreation keeps the shared page and its registrations.
F1 selects a RoutedCommand handled by the page in the current input route. The page removes its
exact Application registration before child disposal, including after Application exit.
This addition has deterministic and scoped API 34 emulator validation. See the [hardware input contract and evidence matrix](../../docs/android-hardware-input.md)
and the [sample interaction checklist](../../docs/cross-platform-sample.md#hardware-command-section).
Physical hardware keyboards are **NOT EXECUTED — environment unavailable**. Software-keyboard
composition and the observed Gboard hardware-text fallback limit remain separate test paths.

Touch uses stable pointer IDs, deepest-control hit testing, independent capture, one-click tap
semantics, drag cancellation, and the real `ScrollableControl` scrollbar state. The diagnostic
area separates control-action receipt from platform-service invocation and completion.

From the repository root:

```powershell
.\scripts\windows\Run-CrossPlatformSample.ps1
.\scripts\android\Run-CrossPlatformSample.ps1 -DeviceId <serial>
```

See [`docs/cross-platform-sample.md`](../../docs/cross-platform-sample.md) for Visual Studio steps,
ADB/emulator commands, limitations, and the general manual validation checklist. The animation-
specific architecture, support matrix, and rotate/background/multi-touch/reduced-motion checklist
are in
[`docs/architecture/android-animation-runtime.md`](../../docs/architecture/android-animation-runtime.md).
