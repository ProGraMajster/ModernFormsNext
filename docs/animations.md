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

The composable equivalents return an `AnimationDefinition` rather than starting immediately:

```csharp
AnimationDefinition fade = card.FadeTo(
    0.5f,
    new AnimationOptions
    {
        Duration = TimeSpan.FromMilliseconds(180),
        Easing = Easings.CubicOut
    });

AnimationRun run = fade.Start();
AnimationState terminalState = await run.Completion;
```

Existing `FadeToAsync`, `TranslateToAsync`, `ScaleToAsync`, and `RotateToAsync` calls remain
source-compatible. Migrate only when a caller needs composition, repeat, cancellation of a whole
group, or reuse of a definition.

## Typed control animations

`Control.AnimateAsync<T>` is the compact public entry point for a typed value:

```csharp
await card.AnimateAsync(
    key: "fade",
    from: 0f,
    to: 1f,
    interpolator: AnimationInterpolators.Float,
    options: new AnimationOptions
    {
        Duration = TimeSpan.FromMilliseconds(300),
        Easing = Easings.CubicOut
    },
    update: value => card.Opacity = value);
```

Use `PropertyAnimation<T>` when the same operation must participate in a group or timeline. The
property setter decides whether a frame is visual-only or layout-affecting; the animation layer
does not force layout.

## Custom animation definitions

Derive from `AnimationDefinition` for an operation that is more expressive than interpolation
between two values:

```csharp
public sealed class ShakeAnimation : AnimationDefinition
{
    public float Distance { get; set; } = 8f;
    public int Oscillations { get; set; } = 4;

    protected override void Update(AnimationContext context, float progress)
    {
        context.Target.TranslationX =
            MathF.Sin(progress * MathF.PI * 2f * Oscillations)
            * Distance
            * (1f - progress);
    }
}

AnimationRun run = new ShakeAnimation
{
    Duration = TimeSpan.FromMilliseconds(500),
    Easing = Easings.CubicOut
}.Start(card);
```

`AnimationContext` exposes `Target`, direction-aware raw `Progress`, `EasedProgress`, monotonic
`Elapsed`, scaled `Duration`, and the run `CancellationToken`. The scheduler invokes `Update` on
the UI dispatcher and never under its internal lock. The context releases its target before
terminal completion becomes observable.

## Sequence, parallel, delay, and timeline

Definitions compose without `Task.Delay`, per-group timers, or a blocking UI wait:

```csharp
await Animation.Sequence(
    card.FadeTo(0.4f),
    Animation.Delay(TimeSpan.FromMilliseconds(80)),
    card.ScaleTo(1.12f),
    Animation.Parallel(
        card.FadeTo(1f),
        card.ScaleTo(1f),
        card.TranslateTo(0f, 0f))
).RunAsync();
```

Sequence stops at the first cancellation or failure. Parallel starts every child, waits for all
children, and reports failures in declaration order through `AggregateException`. Canceling the
returned `AnimationRun` propagates to every active descendant.

Timeline entries use scheduler-backed monotonic delays:

```csharp
var timeline = new AnimationTimeline()
    .At(TimeSpan.Zero, card.FadeTo(0.5f))
    .At(TimeSpan.FromMilliseconds(100), card.ScaleTo(1.1f))
    .At(TimeSpan.FromMilliseconds(250), card.ScaleTo(1f));

await timeline.RunAsync();
```

An entry starts at most once per timeline leg. Application background time is excluded by the same
lifecycle pause used for every other scheduler entry.

## Keyframes and deterministic seeking

```csharp
var keyframes = KeyframeAnimation<float>
    .Create(
        card,
        value =>
        {
            card.ScaleX = value;
            card.ScaleY = value;
        })
    .Keyframe(0f, 1f)
    .Keyframe(0.4f, 1.15f, Easings.CubicOut)
    .Keyframe(1f, 1f, Easings.BounceOut);

keyframes.Duration = TimeSpan.FromMilliseconds(600);
await keyframes.RunAsync();
```

Positions must be finite, nondecreasing values from 0 through 1. The default duplicate-position
policy is `Reject`; `ReplacePrevious` and `KeepBoth` are explicit alternatives. Segment easing is
stored on the segment's ending keyframe. Endpoints are exact and a zero-length duplicate segment
selects the last value at that exact position. A definition is limited to
`KeyframeAnimation<T>.MaximumKeyframeCount` frames.

`Sample(progress)` computes a value without scheduling. `Seek(progress)` computes and applies the
same value synchronously, which is useful for deterministic previews and scrubbing. Supply an
`IAnimationInterpolator<T>` to `Create` for custom value types.

## Repeat and auto-reverse

```csharp
AnimationDefinition pulse = card.ScaleTo(1.08f)
    .Repeat(3)
    .AutoReverse();

await pulse.RunAsync();
```

`Repeat(count)` requires a positive forward-iteration count. Auto-reverse adds a reverse leg to
each iteration. `RepeatForever()` must be canceled through its `AnimationRun`, an external token,
or owner lifetime cleanup:

```csharp
using var cancellation = new CancellationTokenSource();
AnimationRun run = card.RotateTo(360f).RepeatForever().Start(
    cancellationToken: cancellation.Token);

cancellation.Cancel();
await run.Completion;
```

Only the current iteration retains scheduler handles. Under reduced motion or disabled animations,
an infinite repeat applies one deterministic sample and completes instead of spinning.

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
- `Easings.CubicIn`
- `Easings.CubicOut`
- `Easings.CubicInOut`
- `Easings.BounceOut`

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

Composed children receive run-local key prefixes. Nested groups therefore do not replace another
group or unrelated target work even if their leaf definitions use the same property key.
`AnimationRun.Dispose()` is equivalent to canceling that run only. A stale leaf handle cannot
remove a newer replacement because scheduler removal checks entry identity.

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

## Visual-state transitions

Controls resolve one of `Normal`, `Hover`, `Pressed`, `Focused`, or `Disabled` with this priority:

```text
Disabled > Pressed > Hover > Focused > Normal
```

Add directional transitions without creating another hover or focus state machine:

```csharp
button.StyleHover.BackgroundColor = SKColors.MediumPurple;
button.StyleHover.ScaleX = 1.04f;
button.StyleHover.ScaleY = 1.04f;

button.StyleTransitions.Add(
    VisualState.Normal,
    VisualState.Hover,
    new VisualStateTransition
    {
        Duration = TimeSpan.FromMilliseconds(150),
        Easing = Easings.CubicOut
    });
```

`StylePressed`, `StyleFocused`, and `StyleDisabled` complement the existing `Style` and
`StyleHover`. Compatible foreground, background, and border brushes, plus opacity, translation,
scale, and rotation, interpolate. Layout metrics such as border width switch immediately and do
not request layout per frame.

Rapid state changes replace the old transition from its current presentation; the latest state is
authoritative. Theme and dynamic-resource changes cancel the stale transition, re-resolve the
currently active state, and use the existing resource and Brush change notifications. Reduced
motion applies the resolved target presentation immediately.

## Interaction effects

Effects are reusable objects attached through one control-owned collection:

```csharp
button.InteractionEffects.Add(new RippleEffect());
button.InteractionEffects.Add(new PressScaleEffect());
```

An effect has one target at a time, receives shared pointer/keyboard and render hooks, owns its
scheduler channels, and cancels them on removal or disposal. Adding the same instance to the same
collection is idempotent; attaching it to a different control while still attached throws.
`InteractionEffects` is hidden from ordinary Designer serialization. Convenience properties
support the common cases:

```csharp
button.Ripple = new RippleEffect
{
    Color = Color.FromArgb(90, 255, 255, 255),
    Duration = TimeSpan.FromMilliseconds(450),
    StartFromPointer = true,
    RadiusMode = RippleRadiusMode.CoverControl,
    Layer = RippleLayer.AboveBackgroundBelowContent,
    MaxConcurrentRipples = 4,
    EvictionPolicy = RippleEvictionPolicy.Oldest
};

button.PressEffect = new PressScaleEffect
{
    PressedScale = 0.97f,
    PressDuration = TimeSpan.FromMilliseconds(80),
    ReleaseDuration = TimeSpan.FromMilliseconds(120)
};
```

### Ripple

Pointer and touch activation starts at the contact location unless `StartFromPointer` is false.
Space or Enter starts from the center. `CoverControl` computes the farthest current corner on each
render, so a resize in flight remains covered; `Fixed` uses `FixedRadius`. Alpha fades linearly
while radius uses the configured easing.

Waves are clipped to control bounds and the current corner radius. `AboveBackgroundBelowContent`
keeps content legible; `AboveContent` is available for stronger feedback. Active waves are bounded
from 1 through 32 and `Oldest` explicitly cancels the oldest wave when the limit is reached.
Disabled controls do not start waves. A touch cancel removes waves for that pointer ID; global
cancel, removal, disable, detach, and disposal clear all effect-owned work. Decorative ripple is
omitted under reduced motion.

### Press, hover, and focus

`PressScaleEffect` tracks independent pointer IDs plus keyboard activation. Its multiplier composes
with public control transforms and visual-state transforms rather than overwriting `ScaleX` or
`ScaleY`. Pointer cancel, leave, lost focus, disable, removal, and disposal restore the neutral
scale, preventing a stuck press. A faulty custom effect easing faults its scheduler entry but also
removes the ripple or restores the requested press endpoint, so no orphaned visual remains after
the scheduler returns to idle. Reduced motion applies the held/released endpoint without periodic
ticks.

Hover and focus effects use visual-state transitions. There is no parallel hover/focus
subscription or scheduler.

### Render order and custom clipping

The shared control-buffer path is:

```text
background and border
effects below content
content
effects above content
focus ring
```

Ripple is not implemented by individual renderers. `IInteractionEffectClip` can provide a custom
Skia clip for future shape/geometry controls. The built-in
`ControlBoundsInteractionEffectClip.Instance` clips to scaled bounds and the resolved border
radius. Each effect reuses its `InteractionEffectRenderContext` between frames; it borrows the
target-local canvas for the current render call, so effects must never retain either object.

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
into `.Designer.cs` or `.mfdesign` files. Ripple does not start in the Designer. Press preview
applies deterministic endpoints, and complex transition/effect collections stay hidden from the
ordinary property grid.

## Windows and experimental Android

Windows is the primary supported runtime. Mouse, keyboard activation, focus, capture cancellation,
resize, minimize/restore, high DPI, multiple windows, and repaint batching use the common control
and scheduler paths.

Android remains experimental. `SkiaControlSurface` forwards touch down/up/cancel, pointer IDs, and
multi-touch sequences to the same control/effect pipeline. Lifecycle background/foreground pauses
monotonic scheduler time. Orientation and resize use current control geometry. Native
reduced-motion discovery and device-specific frame pacing are not yet validated; do not infer
runtime parity from a successful Android build.

## Performance and repaint batching

The scheduler opens one application visual-invalidation batch per tick. Parallel property updates,
state transitions, and ripple frames in one window coalesce to one platform repaint request for
that tick. Transform, Brush, and effect frames are visual-only; no layout is requested unless a
user-supplied update changes a layout-affecting property.

There are no per-effect timers, hidden background loops, or per-ripple threads. Keyframe and ripple
counts are bounded, effect rendering is linear in active effects and waves, and terminal scheduler
entries release owner/update references before completion is observed. The shared tick source
stops when no runnable work remains.

## Current limitations

- There is no automatic native reduced-motion preference adapter yet.
- There is no general animated-layout subsystem. Updating bounds or spacing uses the existing
  setters and can be more expensive than render-only transforms.
- Brush interpolation requires matching built-in concrete types and equal gradient stop counts.
- Visual-state layout metrics switch discretely rather than interpolate.
- Ripple has one bounded eviction policy, `Oldest`; additional policies can be added without
  changing effect attachment or rendering APIs.
- Complex effect collections are code-first and do not yet have a dedicated Designer collection
  editor.
- Android is experimental; physical-device frame pacing, multi-touch rendering,
  background/foreground, and orientation changes still require manual validation.

See [the scheduler architecture](architecture/ui-animation-scheduler.md), the
[scheduler architecture decision](architecture/decisions/ADR-UI-Animation-Scheduler.md), the
[composable-animation architecture decision](architecture/decisions/ADR-Composable-Animations-And-Interaction-Effects.md),
and **Animations and Interaction Effects** in `samples/ControlGallery` for implementation rationale
and interactive checks.
