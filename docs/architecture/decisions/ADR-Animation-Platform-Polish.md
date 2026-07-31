# ADR: Animation platform policy, ripple overflow, and Designer effects

Status: Accepted for the animation platform polish draft

## Context

ModernFormsNext already has one monotonic, idle-aware `AnimationScheduler`, an
`AnimationPolicy`, platform lifecycle services, and scheduler-backed interaction effects. The
remaining platform gaps are native reduced-motion discovery, explicit ripple overflow semantics,
and deterministic editing of interaction effects in the ModernFormsNext Designer.

The implementation must extend those systems. It must not introduce a second scheduler, polling
loop, per-ripple timer, platform type in shared UI APIs, or a Designer dependency in the runtime
framework.

## Decision

### Platform animation settings

`ModernFormsNext.WindowKit.Backend` defines an observable, platform-neutral animation-settings
provider. A provider publishes immutable snapshots containing:

- a stable source name;
- whether reduced motion is requested;
- whether animations are enabled by the platform;
- the last successful or attempted platform update;
- whether a fallback was used;
- provider state;
- the last non-sensitive error message.

Providers support an explicit refresh and a change event. Events are copied under the provider
lock and invoked after the lock is released. `AnimationScheduler` subscribes at most once,
unsubscribes during shutdown, and marshals policy updates through its existing UI dispatcher.
The scheduler exposes a read-only diagnostics snapshot and an explicit refresh method. No native
provider is queried or subscribed when the scheduler is operating in Designer mode.

The application-level `AnimationPolicy.AnimationsEnabled` remains an independent application
switch. Effective animation availability is the conjunction of that switch and the platform
snapshot. A platform reduced-motion request completes active animations at their final value by
using the existing policy-change path. It does not add a new repaint or layout mechanism.

### Windows source

Windows reads `SPI_GETCLIENTAREAANIMATION` with `SystemParametersInfoW`. A false client-area
animation preference means reduced motion. The existing Win32 message-only infrastructure
observes `WM_SETTINGCHANGE` and tells the single provider to refresh. This follows the Windows
recommendation to reload used system parameters because `lParam` does not reliably identify the
specific setting. No additional HWND, hook, timer, or per-window subscription is created.

If the API is unavailable or fails, the provider reports animations enabled, reduced motion
false, `FallbackUsed = true`, and the error in diagnostics. That fallback preserves historical
ModernFormsNext behavior.

### Experimental Android source

The Android provider reads `Settings.Global.ANIMATOR_DURATION_SCALE` and
`TRANSITION_ANIMATION_SCALE`. A finite zero value in either setting requests reduced motion; both
values must be positive to permit animations. The application context is sufficient, so no
foreground `Activity` is required.

Android live observation is intentionally limited in this experimental backend. The scheduler
refreshes the provider when the shared lifecycle reports `Foreground`. This avoids a permanent
`ContentObserver` and its `Handler` ownership while still reflecting changes after resume. Missing
context, inaccessible settings, malformed values, and platform exceptions use the same safe
fallback as Windows. Device/emulator runtime behavior must not be claimed from compile-time or
abstraction tests alone.

### Threading and lifetime

Native reads may occur on the notifying thread. Policy mutation and completion callbacks occur on
the scheduler UI dispatcher. Provider and scheduler locks never cover external callbacks. The
scheduler owns only its event subscription, not the process-wide platform provider. Shutdown
removes provider and lifecycle subscriptions, cancels scheduler work, and leaves the tick source
idle.

### Ripple overflow policies

`RippleOverflowPolicy` defines four deterministic behaviors when
`MaxConcurrentRipples` is reached:

- `RemoveOldest` cancels the first active wave and starts the new wave;
- `RemoveNewest` cancels the last active wave and starts the new wave;
- `IgnoreNew` leaves existing waves unchanged and creates no wave or handle;
- `ReplaceAll` cancels every active wave and starts only the new wave.

The default is `RemoveOldest`, preserving the behavior shipped with composable animations. The
legacy `RippleEvictionPolicy` surface remains as a compatibility bridge. All waves still use the
shared scheduler; disposal and cancellation clear their handles and visual state.

### Designer collection editor and serialization

The runtime `InteractionEffectCollection` remains the owner-attached collection. Designer-only
editing lives in `ModernFormsNext.Designer` and supports the built-in `RippleEffect` and
`PressScaleEffect`. Unsupported effect types are not offered and are rejected when imported.

The `.mfdesign` value is a deterministic structured object containing `Count` and ordered `ItemN`
entries. Each entry stores a type discriminator and supported serializable properties. Generated
C# emits ordered `control.InteractionEffects.Add(new ... { ... });` calls. Removing an entry
removes its corresponding call; reopening and code reverse-sync rebuild one ordered collection
without appending duplicates.

The editor works on detached descriptions. It never attaches effects, starts animations, queries
platform settings, or creates scheduler handles. Generated runtime code performs the normal
single attach when `InteractionEffects.Add` executes. The current Designer has no transaction or
undo/redo service, so collection edits participate in its existing dirty/save workflow but cannot
offer transactional undo until that infrastructure exists.

## Serialization compatibility

Runtime public APIs remain code-first. Existing `Ripple` and `PressEffect` convenience properties
continue to work. Existing `.mfdesign` documents without `InteractionEffects` deserialize as an
empty collection. Unknown future effect entries are rejected with a Designer diagnostic rather
than instantiated through arbitrary reflection.

Animation and interaction decoration remain opt-in. New controls own independent empty
`InteractionEffects` and `StyleTransitions` collections, and the Designer neither persists empty
collections nor emits initializer calls for them. Theme application is immediate unless callers
explicitly enable `ThemeTransitionOptions.Enabled`; built-in animation tokens never activate a
transition by themselves.

The shared pointer pipeline resolves one leaf control and does not bubble interaction effects to
containers. It tracks the Pressed visual state only for controls with built-in hover interaction or
an explicitly configured pressed style/transition. This keeps panels, layout surfaces,
`DataGridView`, headers, and scrollbars on their pre-animation presentation by default while still
allowing any control to opt into a per-control effect. Cell/row/action-cell targeting is not
modeled by the current grid architecture and remains a separate API design step.

The opt-in boundary changes presentation only. Focus, capture, leaf hit testing, control-specific
pointer handling, caret placement, selection, and keyboard input remain unconditional. The routed
control handler completes first; the pipeline then resolves an optional Pressed presentation and
finally notifies the target's explicit effect collection. Focus loss, cancellation, detach, and
disposal clear both control capture and platform pointer ownership. Starting another independent
touch pointer may move keyboard focus but does not cancel the earlier touch sequence.

## Known limitations

- Android refreshes on foreground instead of observing the global setting continuously.
- Android runtime behavior requires a device or emulator for confirmation.
- The Designer initially supports only `RippleEffect` and `PressScaleEffect` and their safe,
  deterministic properties; arbitrary easing delegates and custom clip implementations are not
  serialized.
- Designer undo/redo remains unavailable because the existing Designer session has no command
  transaction stack.
- This work does not add animated layout, gradient-stop morphing, Shapes/Geometry, new large
  effects, or broader Android runtime stabilization.

## Consequences

Native accessibility changes flow through one provider subscription and the existing scheduler
policy path. Ripple overflow is explicit and testable without extra timers. Designer-generated
effect code is stable and runtime-compatible while keeping all editing UI outside the core
framework. Tests use deterministic providers, lifecycle objects, dispatchers, and clocks; they do
not use timing sleeps.
