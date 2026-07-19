# UI animations

ModernFormsNext uses one shared, platform-neutral scheduler for UI property animations. Progress is
derived from monotonic elapsed time, not a frame count, and all easing, interpolation, property
updates, and final-value writes execute through the current UI dispatcher. The same API is used on
Windows and the experimental Android backend.

The scheduler starts its single tick source when the first animation needs it and stops after the
last animation completes, is canceled, or faults. It does not create a timer for each control or
animation.

## Basic control animation

Use `Control.Animate` for a custom eased-progress callback owned by a control:

```csharp
using ModernFormsNext.Animations;

AnimationHandle handle = card.Animate(
    key: "Opacity",
    duration: TimeSpan.FromMilliseconds(200),
    update: progress => card.Opacity = progress,
    easing: Easings.EaseOut);

AnimationState terminalState = await handle.Completion;
```

The callback receives finite eased progress. Raw progress is always 0 through 1, while a custom
easing function may return a finite overshoot value outside that range. Property setters remain
responsible for their normal invalidation behavior. `Opacity`, translation, scale, and rotation are
render-only properties; layout-affecting properties must use the framework's established layout
path.

Existing helpers remain available and delegate to the shared scheduler:

```csharp
await card.FadeToAsync(0.5f, duration: 180, easing: Easings.EaseOut);
await card.TranslateToAsync(40f, 0f, duration: 220, easing: Easings.EaseInOut);
await card.ScaleToAsync(1.08f, duration: 160);
await card.RotateToAsync(6f, duration: 160);
```

## Typed values and interpolation

Use `AnimationScheduler.Animate<T>` when an animation has explicit start and target values:

```csharp
AnimationHandle handle = AnimationScheduler.Default.Animate(
    owner: card,
    key: "Location",
    from: new PointF(20f, 20f),
    to: new PointF(180f, 80f),
    interpolator: AnimationInterpolators.PointF,
    update: point => card.Location = Point.Round(point),
    options: new AnimationOptions
    {
        Duration = TimeSpan.FromMilliseconds(300),
        Easing = Easings.EaseInOutCubic
    });
```

Built-in interpolators cover `float`, `double`, `int`, `PointF`, `SizeF`, `RectangleF`,
`System.Drawing.Color` including alpha, and `Matrix3x2`. Numeric and geometric values interpolate
component by component. `int` uses midpoint rounding away from zero. Color interpolation currently
uses sRGB byte channels rather than a linear-light color space.

`AnimationOptions.Duration` and `Delay` are non-negative `TimeSpan` values. A zero duration applies
the final value once on the UI thread and completes without starting the tick source. Delayed or
dropped frames advance to the position dictated by elapsed time instead of extending the duration.

## Easing

The built-in backend-independent functions are:

- `Easings.Linear`
- `Easings.EaseIn`
- `Easings.EaseOut`
- `Easings.EaseInOut`
- `Easings.EaseOutCubic`
- `Easings.EaseInOutCubic`

A custom easing receives raw progress between 0 and 1. Returning `NaN` or infinity, or throwing an
exception, faults only that animation. A finite value outside 0 through 1 is preserved for
overshoot curves; use a target property or interpolator that can accept the resulting value.

## Cancellation, completion, and replacement

`AnimationHandle` exposes `State`, `Completion`, `Exception`, `Cancel`, `Pause`, and `Resume`.
Cancellation is idempotent, does not apply the final value, and completes the task with
`AnimationState.Canceled` rather than reporting success or throwing a cancellation exception.

```csharp
AnimationHandle handle = card.Animate(
    "Hover",
    TimeSpan.FromMilliseconds(250),
    progress => card.Opacity = 0.7f + (0.3f * progress));

handle.Cancel();
if (await handle.Completion == AnimationState.Canceled)
{
    // The final value was not forced.
}
```

Animations are identified by owner reference and ordinal key. Starting another animation with the
same owner and key uses `AnimationReplacementMode.Replace` by default and cancels the previous
handle. Different keys or owners run in parallel. Set `ReplacementMode` to `IgnoreNew` to keep the
existing animation and receive its handle instead.

```csharp
AnimationScheduler.Default.Start(
    card,
    "Hover",
    progress => card.Opacity = progress,
    new AnimationOptions
    {
        Duration = TimeSpan.FromMilliseconds(180),
        ReplacementMode = AnimationReplacementMode.IgnoreNew
    });
```

Use `AnimationScheduler.Default.Cancel(owner, key)`, `CancelAll(owner)`, or
`control.CancelAnimations()` for owner-scoped cleanup.

## Brush and gradient animation

There are two deliberately different Brush patterns.

For a local transition, create one animation-local interpolator. It clones the source brush once,
then reuses and mutates that working clone. The original and target remain unchanged, so other
controls that share either endpoint are unaffected:

```csharp
ModernFormsNext.Drawing.Brush from = new SolidColorBrush(Color.MediumPurple);
ModernFormsNext.Drawing.Brush to = new SolidColorBrush(Color.DeepSkyBlue);
card.BackgroundBrush = from;

AnimationHandle handle = AnimationScheduler.Default.Animate(
    owner: card,
    key: "Background",
    from: from,
    to: to,
    interpolator: AnimationInterpolators.CreateBrushInterpolator(),
    update: value => card.BackgroundBrush = value,
    options: new AnimationOptions
    {
        Duration = TimeSpan.FromMilliseconds(300),
        Easing = Easings.EaseInOut
    });
```

For an intentional shared-resource transition, animate the observable brush in place:

```csharp
AnimationHandle handle = sharedBrush.AnimateTo(
    targetBrush,
    TimeSpan.FromMilliseconds(300),
    key: "ThemeTransition",
    easing: Easings.EaseInOut);
```

In-place mutation raises the existing `Brush.Changed` notifications on the UI thread. Every live
control using that brush repaints, and no layout is requested. Supported concrete pairs are
`SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, and `SweepGradientBrush`. Gradient
pairs must have the same concrete type and stop count. Incompatible structures throw before the
animation is scheduled; there is no implicit snap or structural blending.

Compatible animations include color, opacity, `Matrix3x2` transform, linear start/end points,
radial center/origin/radius, sweep angles, stop colors and offsets. `GradientSpreadMode` changes at
the final value. A single stop can also be animated explicitly:

```csharp
AnimationHandle handle = gradient.GradientStops[1].AnimateTo(
    new GradientStop(Color.Gold, 0.72f),
    TimeSpan.FromMilliseconds(240));
```

## Dynamic resources

Animating a Brush stored in an application, window, or control resource intentionally updates all
controls currently resolved to that same instance. Resource subscriptions remain weak and trigger
targeted repaint through the normal Brush notification path.

Replacing a resource while its old Brush is still animating rebinds consumers to the replacement.
The animation remains attached to the old object; it is not transferred to the new resource.
Retain its handle or cancel by its owner/key when a newer theme transition supersedes it. This rule
prevents one transition from silently mutating a structurally unrelated replacement.

## Lifecycle and thread safety

`Start`, cancellation, pause/resume, state reads, diagnostics, and shutdown are thread-safe.
Starting from a worker thread is supported, but every user callback and property mutation is posted
to the existing platform UI dispatcher. Callbacks are never invoked while the scheduler lock is
held and should stay short enough for a UI frame.

Disposing a control or detaching it from an established parent cancels animations owned by that
control. Custom owners must retain and cancel their handles when their lifetime ends. Application
exit shuts down the default scheduler and cancels remaining work; a shut-down scheduler cannot be
restarted.

On the experimental Android backend, moving the activity to the background pauses scheduler time
and stops ticks. Returning to the foreground resumes at the same effective position, excluding the
background interval. Windows uses the WindowKit UI dispatcher; minimized or delayed windows do not
cause idle ticking, and elapsed-time progress catches up when the dispatcher processes a frame.

## Reduced motion and duration scaling

The central policy lets an application control motion without changing every control:

```csharp
AnimationPolicy policy = AnimationScheduler.Default.Policy;
policy.AnimationsEnabled = true;
policy.ReducedMotion = false;
policy.DurationScale = 0.5; // Half the configured duration for newly started animations.
```

When animations are disabled, reduced motion is enabled, or the duration scale is zero, the final
value is posted once on the UI thread and completion reports `Completed`; no tick source starts.
Changing to one of those modes while animations are active completes them through the same
final-value path. Positive duration scale is captured when each animation starts.

ModernFormsNext does not yet read the operating system reduced-motion preference automatically.

## Diagnostics and faults

`AnimationScheduler.GetDiagnostics()` returns a low-cost snapshot containing the active count,
tick count, completed/canceled/faulted totals, average scheduler tick duration, tick-source state,
pause state, and shutdown state. This is a development view, not a telemetry service.

If easing, interpolation, or an update callback throws, only that animation moves to `Faulted`.
The exception is available through `AnimationHandle.Exception`, a trace error is written, and other
animations continue.

## Designer behavior

Animations started for an `IComponent` whose `Site.DesignMode` is true apply their final value on
the designer dispatcher without starting periodic ticks. Runtime animation state is not serialized
into `.Designer.cs` or `.mfdesign` files.

## Current limitations

- Repeat, auto-reverse, queueing, blending, and animation groups are not part of this first API.
- The shared tick source currently uses timer pacing; a stable native render-loop signal may replace
  that internal source later without changing elapsed-time semantics.
- There is no automatic native reduced-motion preference adapter yet.
- There is no general animated-layout subsystem. Updating bounds or spacing uses the existing
  setters and can be more expensive than render-only transforms.
- Brush interpolation requires matching built-in concrete types and equal gradient stop counts.
- Android is experimental; physical-device frame pacing, lifecycle transitions, and rendering still
  require manual validation.

See [the scheduler architecture](architecture/ui-animation-scheduler.md), the
[architecture decision](architecture/decisions/ADR-UI-Animation-Scheduler.md), and the Animations
page in `samples/ControlGallery` for implementation rationale and interactive checks.
