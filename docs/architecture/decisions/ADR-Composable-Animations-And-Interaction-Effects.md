# ADR: Composable animations and interaction effects

- Status: Accepted
- Date: 2026-07-23
- Scope: `ModernFormsNext`, `ModernFormsNext.WindowKit`, platform hosts, Designer, and ControlGallery

Implementation note (2026-08-18): ModernFormsNext 1.10.0 added animated bounds, padding and border-
width visual-state interpolation, a broader brush compatibility planner, detached Designer editors,
and the Android Choreographer/settings-provider integration. Those later additions amend the
original first-release limitations without changing this ADR's single-scheduler decision.

## Context

ModernFormsNext already has a process-wide `AnimationScheduler`, typed interpolators, monotonic
elapsed time, owner/key replacement, reduced-motion policy, lifecycle pause/resume, brush
interpolation, and UI-thread dispatch. Existing helpers such as `FadeToAsync`, `ScaleToAsync`,
`RotateToAsync`, and `TranslateToAsync` use that scheduler.

The framework needs a public layer for reusable animation definitions, composition, keyframes,
repeat behavior, visual-state transitions, and interaction effects. Ripple and press feedback must
not introduce control-specific timers or renderer-specific implementations. The same model must
remain usable by the primary Windows host and the experimental Android `SkiaControlSurface`.

## Decision

### Layers

The system has four layers:

1. `AnimationScheduler` remains the only clock, tick source, dispatcher, lifecycle, and
   owner/key registry.
2. `AnimationDefinition` and `AnimationRun` provide reusable, public run definitions and
   cancellation/completion ownership.
3. composition definitions implement sequence, parallel, timeline, delay, keyframes, repeat, and
   auto-reverse without creating timers or blocking the UI thread;
4. controls consume definitions through visual-state transitions and an attachable
   `InteractionEffectCollection`.

Every animated property continues to use its normal setter. Render-only values invalidate visuals;
layout is requested only by a property whose existing setter is layout-affecting.

### Ownership and lifetime

A leaf animation is owned by the target object and an ordinal channel key. A run scope adds a
unique key prefix for nested groups so one group cannot replace unrelated work. Direct leaf runs
retain the existing replacement behavior.

`AnimationRun.Cancel` propagates to all active descendants. Control detach and disposal cancel
scheduler work owned by that control. Effect detach and disposal cancel effect-owned handles and
clear render state. Terminal scheduler entries release owner and callback references before their
completion task becomes observable, so retaining a completed handle does not retain a target.

Callbacks, cancellation-token callbacks, and task continuations are never invoked while the
scheduler lock is held.

### Cancellation and replacement

`AnimationReplacementMode.Replace` is latest-wins for the same owner/key. `IgnoreNew` returns the
existing run. A stale handle can only cancel its own entry and cannot remove a newer replacement.

Sequence stops after the first canceled or faulted child. Parallel waits for every child, propagates
cancellation to all children, and aggregates faults in definition order. Timeline starts each entry
at most once. Disposing a group cancels only descendants in that run scope.

### Composition

`Animation.Sequence` runs children in order. `Animation.Parallel` starts children together.
`AnimationTimeline` schedules offsets by running scheduler-owned delay entries, so offsets use the
same monotonic clock and lifecycle pause/resume behavior as value animation. No composition object
uses `Task.Delay`, a timer, or a background loop.

### Keyframes

`KeyframeAnimation<T>` captures an immutable keyframe snapshot when a run starts. Positions are
finite values in the inclusive range 0 through 1 and must be nondecreasing. The default duplicate
policy rejects duplicate positions; replace and allow policies are explicit. When duplicates are
allowed, sampling an exact duplicate position selects the last value at that position.

Each segment uses the easing assigned to its ending keyframe. Endpoints are exact. Zero-length
segments are deterministic, interpolation results must be finite where the value type is numeric,
and the public keyframe count is bounded. `Sample` and `Seek` use the same deterministic resolver
as a scheduler tick.

### Repeat and auto-reverse

Finite repeat counts describe forward iterations. Auto-reverse adds a reverse leg to each
iteration. Infinite repeat requires explicit cancellation in normal motion mode. When animations
are disabled or reduced motion is active, an infinite definition applies one deterministic final
sample and completes; it never enters a synchronous infinite loop.

Only the current iteration retains child handles. Completion, cancellation, or fault leaves the
scheduler idle when no unrelated animations remain.

### Visual-state transitions

Controls expose `Normal`, `Hover`, `Pressed`, `Focused`, and `Disabled` states. State priority is:

```text
Disabled > Pressed > Hover > Focused > Normal
```

Transitions are keyed by `(from, to)` and are latest-state-wins. The current presentation is used
as the source of a rapid replacement, so a stale transition cannot publish a final state.
Compatible background, foreground, and border brushes are interpolated through the existing brush
plan. Color, opacity, scale, translation, and rotation are render-only. Fonts, border widths, and
other layout metrics switch to the target style immediately.

A theme or dynamic-resource change during a transition re-resolves the current target and replaces
the old transition. Reduced motion applies the target presentation without periodic ticks.

### Interaction effects

`InteractionEffectCollection` attaches reusable effects to a control. Attach is exclusive and
adding the same instance to the same collection is idempotent; attaching it to another target
while still attached is rejected. The control dispatches pointer, keyboard, focus, enabled,
detach, and render notifications to the collection through one framework hook; effects do not
subscribe independent event-handler graphs.

Effects receive pointer identity and device kind. Windows mouse input uses pointer zero. The
experimental Android surface forwards its platform pointer ID and touch device kind. Pointer
cancel is distinct from normal release.

### Ripple and press feedback

`RippleEffect` starts from a pointer/touch location or the control center. Radius can cover the
current control bounds, so resizing during a ripple changes the required cover radius. Alpha fades
while radius grows. A bounded active list enforces the explicit oldest-first eviction policy.
Each ripple is a scheduler entry, never a timer; scheduler tick batching coalesces repaint
requests per window.

Keyboard activation starts a centered ripple. Disabled controls do not start effects. A
platform-specific pointer cancel removes only waves owned by that pointer; global cancellation
clears the entire gesture state. Detach/dispose cancels all remaining ripple handles.
Reduced motion may omit this decorative effect entirely.

`PressScaleEffect` tracks independent pointer and keyboard presses. It animates a dedicated
interaction scale multiplier so it composes with the control's public render transform rather than
competing for `ScaleX` or `ScaleY`. Release, leave, disable, pointer cancel, detach, and disposal
all restore the neutral multiplier.

Hover and focus animation use the visual-state transition system. No second hover/focus state
machine is introduced.

### Rendering and clipping

The shared control paint path is:

```text
background
effects below content
content
effects above content
focus overlay
```

The hook is part of the common control-buffer pipeline used by Windows and `SkiaControlSurface`;
ripple is not added to individual control renderers.

Effects use `IInteractionEffectClip`. The built-in bounds clip respects the current
`ControlStyle.Border` corner radius. The abstraction intentionally accepts a control and a Skia
canvas so advanced geometry-aware effects can provide a path without changing effect APIs.
The canvas is borrowed and is never retained.

### Repaint batching and performance

Each scheduler tick enters the existing application visual-invalidation batch. Multiple property
updates, parallel animations, state transitions, and concurrent ripples in one window therefore
produce one platform repaint request for that tick. Effect and transform updates never request
layout.

Per-run delegates and snapshots are allocated at start, not per frame. Ripple count and keyframe
count are bounded. Rendering remains linear in active effects and active ripples. Finished
definitions, scheduler entries, and effect states release targets and callbacks.

### Reduced motion

`AnimationPolicy` remains authoritative. Disabled animation or reduced motion applies finite
definitions at their deterministic endpoint without starting the tick source. Decorative ripple
is skipped. Press feedback restores its neutral state. Infinite repeat collapses to one sample.

### Designer

Designer-attached controls do not start runtime scheduler or platform services. Complex collections
are hidden from ordinary property-grid expansion and use content serialization only where safe.
Effects retain stable configuration but no live handles. Preview sampling is deterministic and
does not depend on wall-clock time.

### Platform status

Windows is the primary runtime target. Mouse capture, keyboard activation, focus, minimize/restore,
resize, DPI scaling, multiple windows, and scheduler idle behavior use the shared implementation.

Android remains experimental. `SkiaControlSurface` forwards touch down/up/cancel and pointer IDs,
supports multiple active pointers, and already participates in application lifecycle pause/resume.
No runtime-success claim is made without a device or emulator.

## Consequences

- Existing async helpers remain source-compatible and delegate to public definitions.
- The scheduler gains a richer frame callback and terminal-reference cleanup, but remains the only
  scheduling service.
- Common rendering gains effect hooks; individual renderers do not learn about ripple.
- Visual-state styles become additive public API. Existing `Style` and `StyleHover` behavior remains
  the default for controls that do not configure transitions.
- Custom effects can depend on Skia in the shared framework, consistent with the existing rendering
  architecture.

## Known limitations

- Visual-state layout interpolation is intentionally limited to padding and border widths; other
  layout metrics switch discretely.
- Cross-kind gradient geometry, Glass/NoBrush/null, empty-to-populated gradients, and custom brush
  structures switch discretely.
- Legacy renderers that draw specialized item focus indicators continue to own those indicators;
  the final focus-overlay hook is available for new effects and control-specific migration.
- Android frame pacing, multi-touch visuals, background/foreground behavior, setting changes, and
  orientation changes require broad device or emulator validation.
- Designer editors support the safe detached metadata subset and known easing identifiers. Custom
  delegate easing, general `AnimationDefinition` activation, live effect preview, and Designer-wide
  undo/redo remain unavailable.
