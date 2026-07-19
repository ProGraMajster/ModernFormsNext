# ADR: Shared UI animation scheduler

- **Status:** Accepted
- **Date:** 2026-07-19

## Context

ModernFormsNext has public control animation helpers and an animated `Switch`, but their internal
manager advances a process-wide list from a `Task.Delay(16)` loop. Continuations can update UI
objects from the thread pool, processing time contributes to drift, every frame allocates a list
snapshot, and there is no lifecycle, deterministic clock, handle state, fault isolation, or motion
policy. Paint/Brush values are now observable and suitable for animation, which makes the timing
and ownership deficiencies more important.

Windows and Android already expose different UI dispatch primitives through WindowKit contracts.
The solution must preserve the existing public helper surface and must not add timers to individual
controls.

## Problem

The framework needs one platform-neutral mechanism that derives progress from monotonic time,
executes callbacks on the correct UI thread, becomes completely idle without animations, survives
frame delays, pauses across Android background lifecycle, releases disposed owners, and can be
tested without real time.

## Considered options

### Keep and patch the asynchronous delay loop

Posting every callback back to the UI thread would fix affinity but retain drift, overlapping loop
risks, array allocation, awkward lifecycle handling, and poor deterministic testing.

### One native animation driver per backend

Using display-link/choreographer APIs can improve frame pacing, but it would create different
behavior and testing seams before the Android window/render backend is mature. It also increases
the platform surface for a foundation that only needs a reliable shared tick request.

### One timer per animation or control

This is simple locally but scales poorly, complicates cancellation and backgrounding, and violates
the requirement for one coherent scheduler.

### Shared scheduler with a monotonic clock, one tick source, and dispatcher boundary

All entries share one clock and tick source. The source requests coalesced work, while the existing
platform dispatcher guarantees UI-thread execution. Clock, dispatcher, and tick source are
injectable internally for deterministic tests.

## Decision

Adopt the shared scheduler option.

- `AnimationScheduler.Default` is the compatibility and application entry point.
- Progress is calculated from a monotonic clock and real elapsed time.
- A single periodic tick source runs only while entries are active and not paused.
- Tick requests are coalesced and dispatched through Android's registered
  `IPlatformDispatcher` or WindowKit `Dispatcher.UIThread`.
- Owner identity plus an ordinal key provides replacement and cancellation.
- Public handles expose terminal state, completion, cancellation, and optional pause/resume.
- Existing control helpers remain adapters; `Switch` no longer creates its own animation model.
- Observable brushes are mutated in place only through explicit compatible brush animation APIs.
- Central policy implements enabled/reduced-motion behavior and duration scaling.
- Android activity lifecycle implements a platform-neutral lifecycle contract consumed by the
  scheduler.
- Native frame callbacks and advanced animation composition remain future enhancements behind the
  tick-source seam.

## Consequences

All UI mutation is serialized on the correct UI dispatcher. Dropped frames do not lengthen
animations, idle CPU use drops to zero, and tests can advance exact timestamps. One failing entry
cannot stop unrelated entries. Explicit owner cancellation and lifecycle integration bound memory
and timer lifetimes.

The timer is a pacing request rather than a guarantee of a particular FPS. It can be replaced by a
stable render-loop signal later. The scheduler owns active callbacks strongly until they terminate,
so custom owners must cancel when their lifecycle ends. Android physical-device pacing and native
reduced-motion discovery remain manual/future work.

## Rejected alternatives

- A second `PaintAnimationManager` was rejected because Brush uses the same scheduling semantics.
- A `Timer` stored on every `Control` was rejected because it duplicates platform and lifecycle
  work.
- A frame-count progress model was rejected because it drifts and behaves differently under load.
- Silent exception swallowing was rejected because it hides broken animations.
- Caching or serializing running scheduler entries was rejected; future theme JSON contains target
  values, not runtime timer state.
- Full navigation, Shape, ThemeManager, and animated-layout APIs were rejected from this stage to
  keep the foundation independently reviewable.
