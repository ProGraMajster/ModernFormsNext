# Android animation runtime architecture

## Status and scope

Android support remains experimental. This document describes the Android integration for the
shared ModernFormsNext animation scheduler, interaction effects, animated layout, visual states,
and theme transitions. It does not introduce an Android animation model or interpolator. Windows
remains the primary and best-supported runtime.

The runtime is designed around this path:

```text
Android lifecycle and MotionEvent
  -> AndroidSkiaHostView logical input
  -> SkiaControlSurface and shared Control state
  -> AnimationScheduler
  -> presentation state
  -> Control invalidation
  -> AndroidAppHost / PostInvalidateOnAnimation
  -> Skia render
  -> Choreographer next-frame signal while work remains
```

Shared code owns animation definitions, easing, interpolation, scheduling, effect state,
presentation geometry, cancellation, and elapsed time. The Android backend owns only native frame
signals, lifecycle bridging, MotionEvent translation, density conversion, system animation-scale
observation, surface gating, and native cleanup.

## Frame pacing and time

`AndroidChoreographerAnimationFrameSource` implements the platform-neutral
`IPlatformAnimationFrameSource` contract. The default shared scheduler resolves this service when
frame demand starts, rather than permanently selecting a source when `AnimationScheduler.Default`
is first read. An early singleton read is therefore safe. If animation work itself starts before
backend registration, the shared idle-aware timer is temporary: the next fallback signal performs
one atomic handoff to Choreographer and is not delivered as a duplicate scheduler tick. Other
environments retain the shared fallback.

There is one Choreographer source for the process-wide scheduler. It keeps no per-control timer and
never uses `Thread.Sleep` or a busy loop. A native callback is pending only when both conditions are
true:

- the shared scheduler has active animation work; and
- at least one Android Skia surface is attached and resumed.

The state machine coalesces repeated starts and allows at most one Choreographer callback to be
pending. Delivery clears the pending state before scheduler work runs. If work remains, the source
posts one callback for a subsequent display frame; otherwise it becomes idle. Starting work after
an idle period wakes the source without retaining the previous callback delegate.

All native post/remove operations are marshaled through a `Handler` created for
`Looper.MainLooper`. The Choreographer instance is acquired lazily inside that main-Looper
reconciliation, so even backend initialization invoked from another thread cannot bind frame
callbacks to that thread's Looper. `DoFrame`, shared presentation updates, and resulting
invalidations consequently execute on the Android UI thread.

Choreographer supplies pacing only. The scheduler continues to calculate progress from its
monotonic clock and clamps progress to the animation interval. It therefore follows the display's
actual refresh rate rather than assuming 60 Hz. Dropped or late frames advance directly to the
appropriate elapsed-time presentation, without accumulating a callback backlog. A long frame may
visibly skip intermediate states but cannot extend a completed animation indefinitely.

`PostInvalidateOnAnimation` remains the coalesced render endpoint. An animation property or
presentation-state update requests invalidation through the established Control-to-host path; the
frame source does not repaint the entire tree by itself.

## Background, foreground, and surface lifecycle

`AndroidActivityTracker` publishes the platform-neutral application lifecycle already consumed by
`AnimationScheduler`. Background or no-host state globally pauses the scheduler and stops native
frame demand. Resume rebases the scheduler's effective monotonic time by subtracting the paused
interval. For example, a 500 ms animation paused after 100 ms resumes near 100 ms even if the app
spent 20 seconds in the background.

Native surface availability is a separate gate. `AndroidSkiaHostView` activates its lightweight
frame-source registration only while the view is attached and the host is resumed. Pause, stop,
detach, disposal, and surface recreation remove that registration and any pending callback when no
other active surface exists. Resume or reattach can wake a still-active scheduler entry. This
separation covers activity switching, screen off/on, view detach, and activity recreation without
putting Android lifecycle types into shared APIs.

## Touch, pointer ownership, and interaction effects

`AndroidMotionEventPlan` maps Android action codes and action indices into platform-neutral pointer
events. Pointer indices are used only to read the current `MotionEvent`; the resulting stable
Android pointer ID is passed to shared input. The mapping is:

| Android action | Framework input |
| --- | --- |
| `ACTION_DOWN` | one `Down` for the action pointer ID |
| `ACTION_POINTER_DOWN` | one `Down` for the action pointer ID |
| `ACTION_MOVE` | one `Move` for every current pointer ID |
| `ACTION_POINTER_UP` | one `Up` for the action pointer ID |
| `ACTION_UP` | one `Up` for the action pointer ID |
| `ACTION_CANCEL` | cancel all active pointer IDs |

Coordinates are converted from device pixels to logical pixels before shared hit testing. Shared
`SkiaControlSurface` capture then keeps each pointer associated with its original target. Reordering
native pointer indices does not transfer ownership.

`RippleEffect` stores one ripple per shared pointer ID. Its existing `MaxConcurrentRipples` and
overflow policy remain authoritative. `StartFromPointer` uses the logical, target-local coordinate
from the shared routed event. `PressScaleEffect` tracks a set of active pointer IDs, so releasing or
canceling one finger does not clear another finger's press. Move-out, disable, detach, subtree
removal, reparent, disposal, and lifecycle cancellation continue through shared interaction-scope
cleanup. Presentation geometry remains part of shared hit testing; Android does not maintain a
second geometry model.

## Reduced motion and animator duration scale

Android reads only `Settings.Global.ANIMATOR_DURATION_SCALE`, the system setting that controls
animator duration. Window transition scale is not combined with framework animation duration:
doing so could disable or multiply application animations because of an unrelated system-window
preference.

The backend preserves the finite positive scalar, so values such as `0.5`, `1`, and `2` shorten,
preserve, or lengthen newly started shared animations. The application duration scale and platform
scale multiply in the platform-neutral `AnimationPolicy`. A scale of zero marks animations
disabled and reduced, completes active and newly started animations immediately at their exact
targets, and leaves the scheduler idle. Invalid, inaccessible, or implausibly large values use the
compatibility scale `1` and expose fallback diagnostics rather than failing startup.

A main-Looper `ContentObserver` watches the animator-duration setting while the application is in
the foreground and the scheduler is subscribed. It refreshes the immutable platform snapshot when
the setting changes, without requiring an application restart. The observer uses the application
`ContentResolver`, not an Activity, and unregisters on background, final unsubscribe, or provider
disposal. Startup, foreground entry, and explicit policy refresh remain safe fallback refresh
points if observation cannot be registered.

## Themes, layout, orientation, and resizing

Theme transitions continue to use the shared ThemeManager and Brush interpolators. Android adds no
Brush implementation. Solid-to-solid, solid-to-gradient, gradient-to-solid, compatible-gradient,
retarget, and documented discrete-fallback behavior therefore match other backends. Platform scale
zero and disabled animation policy make ThemeManager commit the exact target snapshot without
creating transition scheduler work.

Logical bounds remain the source of truth for Dock, Anchor, orientation, density, and surface-size
layout. `LayoutTransition` and visual-state metric transitions only animate shared presentation
geometry between the latest logical results. A resize during active work retargets from the current
presentation state to the new logical target; completion clears presentation overrides. Repeated
portrait/landscape or size changes must therefore converge on the latest logical bounds without
accumulating transform drift.

## Cleanup and ownership

- Shared `Control.Dispose`, detach, subtree removal, and reparent paths cancel scheduler ownership,
  pointer capture, ripples, press state, layout presentation, and visual-state transitions.
- `AndroidSkiaHostView` cancels active native pointers and removes its active-surface registration
  on pause, stop, detach, and disposal.
- The frame source releases the scheduler callback on idle and removes a pending native callback
  when its surface/demand gate closes.
- The animation-settings provider stores only the application context and unregisters its observer;
  neither the observer nor frame-source registration retains an Activity or Control.
- Scheduler callback faults terminate only the failing entry, release it, and remain visible in
  shared diagnostics.
- A single-surface host must cancel process-owned work such as an active ThemeManager transition
  when that host is shutting down. The cross-platform sample does this before detaching its tree;
  multi-window hosts should instead cancel at their application-level ownership boundary.

The shared scheduler intentionally retains active owners and update delegates until termination;
explicit cancellation/disposal is therefore the deterministic lifetime boundary. There is no
finalizer-based animation cleanup and no arbitrary sleep in lifecycle or GC tests.

## Diagnostics

`AnimationScheduler.GetDiagnostics()` reports active count, active/idle state, tick and completion
counters, and scheduler faults. `AnimationScheduler.GetPlatformDiagnostics()` reports provider
state, effective reduced-motion flags, and the exact platform duration scale.

`AndroidWindowKit.Current.GetAnimationRuntimeDiagnostics()` adds Android lifecycle state, active
surface count, scheduler frame demand, whether one Choreographer callback is pending, posted and
delivered callback counters, observer registration state, and the latest observer error. The
cross-platform sample displays these snapshots on demand. They are intentionally snapshots rather
than per-frame log output, so normal rendering does not spam diagnostics.

## Capability and fallback matrix

The validation column is evidence-specific. "Automated" means deterministic tests or builds in
the repository; it does not mean emulator or physical-device observation.

| Feature | Android support | Implementation | Fallback | Validation |
| --- | --- | --- | --- | --- |
| Animation scheduler | Experimental shared runtime | Shared `AnimationScheduler`, monotonic clock, and late-bound platform source | Temporary shared timer until a backend source is registered or when native source startup fails | Automated early-access/handoff, scheduler, and Android builds; manual smoothness required |
| Frame pacing | Integrated | One demand-driven Choreographer source gated by active surfaces | No frames while no surface is active | Automated state-machine tests; emulator/device refresh-rate check required |
| RippleEffect | Integrated shared effect | Stable pointer IDs and shared presentation rendering | Existing overflow policy | Automated shared effect and MotionEvent-plan tests; visual touch check required |
| Multi-touch | Integrated | One shared capture/effect owner per Android pointer ID | `ACTION_CANCEL` clears all pointers | Automated reorder/cancel tests; real multi-touch required |
| PressScaleEffect | Integrated shared effect | Shared active-pointer set and scheduler | Exact neutral scale after cancellation/cleanup | Automated shared interaction tests; visual check required |
| Layout transitions | Integrated shared presentation | Latest logical layout retargets presentation bounds | Immediate layout under reduced motion | Automated layout tests; rotate/resize check required |
| Visual-state transitions | Integrated shared presentation | Shared state plans and scheduler | Exact target state | Automated core tests; sample check required |
| Brush/theme transitions | Integrated shared interpolation | ThemeManager and shared Brush plans | Documented discrete fallback for incompatible brushes | Automated compatibility/retarget tests; Skia visual check required |
| Reduced motion | Dynamic | Animator-duration scale plus lifecycle-aware ContentObserver | Foreground/explicit refresh and scale `1` on read failure | Automated evaluator/policy tests; settings UI check required |
| Orientation and resize | Integrated | Density-aware surface resize and logical relayout | Immediate presentation when transitions are disabled | Automated surface/layout tests; repeated rotate check required |
| Background/foreground | Integrated | Shared pause/resume time rebasing plus surface gate | No native frames while inactive | Automated elapsed-time/lifecycle tests; app-switch and screen-off check required |
| Detach/reparent/dispose | Integrated | Shared ownership cleanup plus Android surface registration disposal | Explicit owner cancellation | Automated subtree/effect/surface tests; activity-recreate check required |
| Diagnostics | Integrated | Shared and Android snapshot APIs | Observer failures are recorded | Automated build/sample checks; inspect during smoke test |
| GPU/CPU rendering | Experimental Skia surface | `SKCanvasView`, existing invalidation and Skia renderer | Platform-selected canvas path; no Android-only animation fallback | Build verified; emulator/device profiling and visual validation required |

## IME boundary

Animation integration does not create a second input loop, replace the selected control, request
focus per frame, or recreate the input connection during invalidation. Existing TextBox selection,
composition, committed-text, keyboard, and UTF-16/code-point routing remain unchanged. The sample
keeps single-line and multiline TextBox cases beside the animation smoke controls. Actual soft
keyboard behavior across orientation and background/foreground remains a manual emulator/device
regression check.

## Manual validation checklist

### Recorded issue #29 evidence

Automated validation and manual runtime evidence are intentionally separate:

- Automated validation covers scheduler timing and policy, lifecycle interleavings, source
  idle/wake and late binding, pointer-plan translation, shared multi-pointer ownership, cleanup,
  layout/visual-state/theme retargeting, project builds, and packaging validation.
- A user-reported emulator smoke test on 2026-08-15 verified application startup, the basic Android
  host, AnimationScheduler/Choreographer operation, animation start and retarget, theme transition,
  and TextBox with the Android soft keyboard/IME. No crash was observed. Emulator API level,
  refresh rate, and image were not recorded, so this is not evidence for a particular device class
  or display cadence.
- No physical-device validation has been recorded for issue #29.

The remaining cases below still require explicit manual observation unless a later validation
record says otherwise.

Run the Android target of `ModernFormsNext.CrossPlatform.Sample` on every available environment.
Record emulator image/API/refresh rate or physical-device model/API separately.

1. Start and rapidly retarget simultaneous layout, opacity, scale, and rotation animations. Confirm
   the diagnostic callback counters advance only while work is active and restart after idle.
2. Touch the interaction button with one and then two fingers. Confirm each ripple stays under its
   finger, press scale remains active until the last pointer releases, and cancel/move-out recovers.
3. Trigger the visual-state-capable control and animated light/dark theme changes, including theme
   retargeting during layout and interaction work.
4. Set Android animator duration scale to `0`, a fractional value, `1`, and a value above `1` while
   the app is running. Confirm the displayed scale changes without restart; scale `0` lands exactly
   at targets and becomes idle.
5. Rotate portrait to landscape and back repeatedly during layout and visual-state transitions.
   Resize if the emulator supports it and confirm controls converge to logical layout without drift.
6. Background for at least 20 seconds during a 500 ms animation, then return. Confirm it resumes
   near its pre-background progress instead of jumping by background elapsed time. Repeat with
   screen off/on.
7. Recreate or close the Activity during active ripple, press, layout, and theme work. Confirm no
   crash, stale callback, duplicate frame loop, retained old Activity, or continuing old input.
8. Focus both TextBox controls, show the soft keyboard, edit composed/emoji text, animate nearby
   controls, rotate, and background/foreground. Confirm focus and IME state are not reset merely by
   animation invalidation.
9. Inspect diagnostics after the final animation: active count `0`, scheduler idle, frame demand
   false, and no callback pending. Review log output only for scheduler/observer faults.

Automated validation must be reported separately from emulator and physical-device results. Never
infer device smoothness, GPU behavior, multi-touch ergonomics, IME behavior, or leak freedom solely
from a successful APK build.
