# UI animation scheduler architecture

## Purpose and scope

This document records the state of animation timing after the Paint/Brush foundation was merged
and defines the shared scheduler that ModernFormsNext should use on Windows and experimental
Android. The scope is scheduling, time, cancellation, replacement, interpolation, lifecycle, and
diagnostics. It deliberately excludes ThemeManager, Shape controls, navigation transitions, and a
designer-authored transition system. The animated-layout foundation now consumes this scheduler as
described in [Animated layout architecture](animated-layout.md); it does not add another ticker.

Implementation update (2026-08-18): ThemeManager, Shape controls, composable definitions,
Designer-authored safe transition metadata, Android Choreographer pacing, and Android live
animation-scale observation now consume this seam. Navigation transitions remain future work.

## Pre-change state

Before this stage, ModernFormsNext had a small animation surface under
`ModernFormsNext.Animations`:

- `ControlAnimationExtensions` exposes fade, translation, scale, and rotation helpers;
- `ControlAnimationEffects` composes those helpers into shake, pulse, and hover effects;
- `Switch` uses the same internal `Animation` and `AnimationManager` for its thumb transition;
- `Easings` provides linear, quadratic, and selected cubic curves;
- render-transform properties on `Control` invalidate rendering without requesting layout.

`AnimationManager` is a process-wide list driven by an asynchronous loop. It reads a shared
`Stopwatch`, copies the active list to an array, updates every animation, then awaits
`Task.Delay(16)`. It replaces an animation by target and string key, but exposes only an untyped
completion `Task` through the existing helpers.

This is a useful compatibility surface, but not a safe cross-platform scheduler. Because the loop
continues with `ConfigureAwait(false)`, property callbacks normally run on a thread-pool thread
rather than the UI dispatcher. A fixed delay also drifts by the time spent processing a frame.
There is no delayed start, pause, lifecycle integration, public state/handle, fault isolation,
central motion policy, or deterministic clock for tests.

## Existing timers and local mechanisms

The repository contains several timing mechanisms with different responsibilities:

| Location | Mechanism | Responsibility | Migration decision |
| --- | --- | --- | --- |
| `Animations/AnimationManager.cs` | `Stopwatch` plus `Task.Delay(16)` | UI property animations | Replace with the shared scheduler. |
| `Switch.cs` | `AnimationManager` | Thumb/track state transition | Delegate to the shared scheduler. |
| `PictureBox.cs` | framework `Timer` | Animated-image frame timing | Keep separate; frame durations are media timing, not property interpolation. |
| `MarkdownEditor.cs` | framework `Timer` | Preview debounce | Keep separate; this is delayed work, not animation. |
| `ToolTip.cs`, `ToolBar.cs` | framework `Timer` | Delay and auto-pop behavior | Keep separate; these are interaction deadlines. |
| `Timer.cs` | WindowKit `DispatcherTimer` | General UI timer component | Preserve as public infrastructure. |
| Android `PostInvalidateOnAnimation` | native render invalidation | Coalesced surface redraw | Keep as the rendering endpoint, not the animation clock. |

One shared scheduler must replace only the first category. Turning every timeout or animated GIF
into an animation would mix unrelated ownership and completion semantics.

## Problems to solve

1. Updates must never mutate controls or brushes from the thread pool.
2. Progress must use monotonic elapsed time rather than a frame counter or accumulated delays.
3. There must be one tick source for all active animations, stopped completely while idle.
4. Start, cancellation, state reads, pause, resume, and shutdown must be thread-safe.
5. User callbacks must never run under the scheduler lock.
6. Replacing `(owner, key)` must cancel the old entry exactly once.
7. Disposed or detached controls must not remain owned by the scheduler.
8. A callback or easing failure must fault only its animation and remain diagnosable.
9. Tests must advance a manual clock and tick source without sleeping.
10. A central policy must support disabled animations, reduced motion, and duration scaling.

## Target architecture

The existing public helpers remain adapters. The new implementation consists of:

- one `AnimationScheduler.Default` instance for the current UI process;
- an internal monotonic clock based on `Stopwatch.GetTimestamp`;
- one shared idle-aware tick source, created on first use, late-bound to a backend source, and
  stopped when idle;
- an optional platform-neutral frame-source service, implemented by Android with Choreographer;
- a dispatcher adapter that prefers the registered Android `IPlatformDispatcher` and otherwise
  uses WindowKit `Dispatcher.UIThread`;
- an owner-and-key identity for replacement;
- `AnimationHandle` for cancellation, state, pause/resume, completion, and fault inspection;
- `AnimationOptions` for duration, delay, easing, and replacement behavior;
- reusable typed interpolators for platform-neutral values;
- a central `AnimationPolicy` for enabled/reduced-motion behavior and duration scaling;
- snapshot diagnostics with counters rather than a telemetry pipeline.

The scheduler is process-wide because the current framework has one UI dispatcher and
`Application.Run` loop. Its contracts do not depend on a specific control, so a future multi-UI-
context architecture can provide one scheduler per context without changing animation entries.

## Windows requirements

The Windows backend owns the WindowKit UI dispatcher. A timer callback may originate on a worker
thread, but it only queues one pending tick; all easing, interpolation, property mutation, and
completion work then executes on `Dispatcher.UIThread`. No tick is scheduled while the active set
is empty or globally paused. Window minimization therefore cannot produce an idle busy loop; a
finite active animation still completes according to monotonic time when the dispatcher runs.

## Android requirements

Android registers `IPlatformDispatcher` backed by its main `Looper` and an
`IPlatformAnimationFrameSource` backed by Choreographer. The scheduler uses these services when
present, even if `AnimationScheduler.Default` was read before backend startup, so shared controls
and brushes are mutated on the Android main thread and frames follow the display cadence without
assuming 60 Hz. Active fallback demand hands off once without delivering a duplicate tick. The
Choreographer instance is acquired only from a main-Looper reconciliation. The source keeps at
most one callback pending and is
gated by scheduler demand plus an attached, resumed Skia surface. The activity tracker exposes the
platform-neutral lifecycle contract: backgrounding pauses the scheduler and stops callbacks, while
foregrounding resumes from the same effective time. The background duration is excluded,
preventing a visual jump after resume. See
[Android animation runtime architecture](android-animation-runtime.md). Physical-device frame
pacing still requires manual validation.

## UI thread model

Public start and cancel operations may be called from any thread. They mutate only lock-protected
scheduler state. The shared tick source requests work through the dispatcher and coalesces pending
requests. Animation callbacks, easing, interpolation, completion transitions, and policy-driven
final-value application occur on the UI thread. No callback executes while the scheduler lock is
held. Callers remain responsible for keeping update callbacks short.

## Time model

The clock returns a monotonic `TimeSpan`. Each entry records its effective start time. Raw progress
is calculated from `(now - start - delay) / duration` and clamped to 0..1. A delayed dispatcher
frame advances directly to the correct elapsed-time position; it does not extend the animation.
Global pause records a clock instant and subtracts paused duration from future effective time.
Individual handle pause shifts only that entry's start time on resume.

Tests inject a manual clock and invoke a manual tick. No unit test relies on `Thread.Sleep`.

## Scheduler lifecycle

The first active animation starts the shared tick source. Completion, cancellation, replacement,
or fault removes the entry. Removing the final entry stops the tick source. Global pause also stops
it; resume restarts it only when entries remain. `Application.Exit` shuts down the default
scheduler, cancels remaining entries, stops the timer, and detaches platform lifecycle events.

`Control.Dispose` cancels owned animations. Detaching an established child from its visual parent
also cancels its entries; newly constructed top-level controls are unaffected. This explicit
ownership path is preferred over finalizers or relying on weak references to compensate for a
capturing update delegate.

## Start, cancellation, and replacement

Starting an animation validates owner, key, duration, delay, easing, and interpolator. The default
replacement behavior is `Replace`: an existing entry with the same owner identity and ordinal key
is canceled before the new entry is installed. `IgnoreNew` is available for callers that prefer to
retain the old entry. Different keys and different owners run independently.

Cancellation is idempotent. It removes the entry, stops ticking when it was the last entry, changes
the handle state to `Canceled`, and completes the termination task with that state. It never
applies the final value or reports `Completed`. Canceling an already terminal handle has no effect.

## Progress, completion, and errors

Zero-duration animations and animations disabled by policy post the final update once on the UI
thread and do not start the tick source. Completion and every other terminal transition happen at
most once. A thrown easing, interpolator, or update callback changes only that entry to `Faulted`,
stores the exception on its handle, increments diagnostics, writes a trace error, and lets other
entries continue.

Custom easing receives clamped raw progress in 0..1. A finite result outside 0..1 is permitted for
overshoot curves. NaN and infinity fault the animation rather than reaching a UI property.

## Interpolators

`IAnimationInterpolator<T>` separates scheduling from value production. Built-in interpolators
cover `float`, `double`, `int`, `PointF`, `SizeF`, `RectangleF`, `Color` including alpha, and
`Matrix3x2`. Numeric and geometric interpolation uses component-wise linear interpolation. Integer
results use explicit midpoint rounding. Color channels interpolate independently in the current
sRGB byte representation; linear-light interpolation is deferred.

Brush animation uses the observable model without allocating a new brush on every tick. The generic
Brush interpolator captures its endpoints once and mutates one animation-local working value,
leaving shared source and target instances unchanged. It supports solid pairs, same-kind gradients
with equal or different non-empty stop counts, and solid-to-linear/radial/sweep promotion. Stop
normalization is planned once and preserves hard-stop multiplicity. Cross-kind gradients,
`GlassBrush`, `NoBrush`, null, empty-to-populated gradients, and custom/derived brushes retain a
discrete fallback. An explicit `AnimateTo` helper instead mutates an existing Brush in place and
therefore still requires the same built-in type and stop count. Spread mode changes at completion.
The full matrix is documented in [Brush interpolation compatibility](brush-interpolation.md).

## Invalidation

The scheduler does not invalidate globally. Existing property setters retain responsibility:

- `Control` opacity and render transforms request repaint only;
- observable brush and gradient-stop setters repaint controls subscribed to that brush;
- animated layout commits logical bounds through the established layout path, then updates only its
  presentation rectangle and composition invalidation on frames.

Cancellation performs no property write, so it creates no extra repaint. Layout and render
invalidation remain separate concerns.

## Paint/Brush, resources, and themes

The owner of an explicit in-place brush animation is the brush itself. Its synchronous `Changed`
event continues through the weak, reference-counted subscriptions introduced with dynamic
resources. A local transition normally uses its control as owner and assigns its animation-local
working brush. Replacing a resource while its previous brush is animating does not transfer an
explicit in-place animation to the new object: consumers rebind to the replacement, and the old
animation can be canceled by its handle or owner key. `ThemeManager` uses local value plans
instead; it retains the published presentation brush when a newer theme replaces an active
transition.

The JSON ThemeManager serializes target theme values, not live scheduler state or native timer
data. Theme transitions construct compatible local animation plans after resource resolution,
publish the current presentation brush during a rapid replacement, and restore the exact target
reference at completion.

## Shape and future navigation work

Shape controls use the shared paint/geometry/rendering architecture and can consume the same typed
interpolators and owner keys. Navigation surfaces can later use multiple keys for opacity and
transform without owning timers. Animated layout uses a focused presentation-geometry helper:
layout computes the target once, while scheduler frames request composition invalidation without
rerunning layout.

## Reduced motion

`AnimationPolicy` is central and platform-neutral. Its effective reduced-motion value is the OR of
the application preference and the latest immutable platform snapshot, so application code cannot
override an operating-system accessibility request. `ApplicationReducedMotion` exposes the
application part independently for settings surfaces.

`IPlatformAnimationSettings` is registered through the existing backend service registry. The
scheduler subscribes once, marshals provider changes through its UI dispatcher, applies policy, and
then releases provider and scheduler locks before user-visible completion callbacks can run.
Startup and foreground entry explicitly refresh the snapshot. Windows additionally refreshes from
the existing message-only WindowKit window on `WM_SETTINGCHANGE`; no poller, timer, or native handle
is introduced. Experimental Android reads the exact global animator duration scale from the
application context and uses a lifecycle-aware ContentObserver for changes while foregrounded.

When animations are disabled, reduced motion is requested, or duration scale is zero, newly started
animations apply their final value once on the UI thread and complete without a timer. Disabling
motion while animations are active completes them through the same final-value path. A positive
application duration scale is multiplied by the platform scale and captured when an entry starts;
for example, 0.5 halves its duration and 2 doubles it. Native read failures retain compatibility
defaults and are visible through
`GetPlatformDiagnostics()` instead of failing application startup.

## Performance and memory risks

- The tick source is process-wide and coalesces outstanding dispatcher posts.
- The active list and tick buffer are reused; the hot path avoids LINQ and per-frame snapshots.
- Entry and completion objects allocate once per animation, not once per frame.
- Color and geometry interpolation are value-type operations.
- Brush plans capture one managed snapshot and mutate one brush rather than allocating brushes per
  frame.
- The scheduler holds active owners and callbacks strongly because they are required to perform
  updates. Explicit control disposal/detach cancellation bounds that lifetime.
- A user callback can still capture unrelated long-lived state; cancellation is the release path.
- A very large number of callbacks can exceed a frame budget. Diagnostics expose active count,
  ticks, terminal outcomes, and average tick duration for development investigation.

## Completion criteria

This stage is complete when:

1. legacy control animation helpers and `Switch` delegate to one scheduler;
2. the fixed `Task.Delay` loop is removed;
3. all callbacks execute through the UI dispatcher;
4. monotonic and manual clocks produce elapsed-time progress without drift;
5. idle, pause, resume, replacement, cancellation, shutdown, and faults are deterministic;
6. control disposal/detach and Android backgrounding release or pause work as documented;
7. reduced motion and duration scaling are central;
8. typed value and compatible brush animations are covered by fast tests;
9. ControlGallery demonstrates the public behavior without running work after disposal;
10. Windows, experimental Android, Designer, VSIX, samples, tests, and packages validate without
    suppressing warnings.
