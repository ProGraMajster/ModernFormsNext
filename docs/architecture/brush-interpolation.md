# Brush interpolation compatibility

## Purpose

ModernFormsNext uses one platform-neutral brush interpolation planner for control visual states,
theme resources, and the public typed animation APIs. It extends the shared animation scheduler;
it does not introduce another timer, frame loop, renderer, or public brush hierarchy.

The planner has two responsibilities:

1. decide whether two authored brushes have a safe visual interpolation path; and
2. prepare all structural data once so an animation frame only updates an existing working brush.

The source and target brushes are never modified by a local transition. Progress at or below zero
returns the exact source reference, progress at or above one returns the exact target reference,
and only the open interval uses the animation-local working brush. This gives theme resources and
visual states deterministic identity and notification behavior at both endpoints.

## Compatibility matrix

The table describes the end-to-end behavior of visual-state and `ThemeManager` transitions. Those
callers probe compatibility before creating a local interpolator and apply their established
discrete fallback when no plan exists. A directly created
`AnimationInterpolators.CreateBrushInterpolator()` accepts only compatible non-null pairs and
retains the existing validation exception for an unsupported pair because
`IAnimationInterpolator<T>` has no `TryInterpolate` result.

| From / to | Solid | Linear | Radial | Sweep | Glass | NoBrush, null, custom |
| --- | --- | --- | --- | --- | --- | --- |
| Solid | interpolate | promote to linear | promote to radial | promote to sweep | discrete | discrete |
| Linear | promote to linear | interpolate | discrete | discrete | discrete | discrete |
| Radial | promote to radial | discrete | interpolate | discrete | discrete | discrete |
| Sweep | promote to sweep | discrete | discrete | interpolate | discrete | discrete |
| Glass | discrete | discrete | discrete | discrete | discrete | discrete |
| NoBrush, null, custom | discrete | discrete | discrete | discrete | discrete | discrete |

Only the exact built-in `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, and
`SweepGradientBrush` types participate. A derived or otherwise unknown brush may add state with no
safe generic interpolation rule, so it deliberately uses the caller's existing discrete fallback.
There is no implicit reflection or user-code invocation to discover a custom path.

Linear, radial, and sweep geometry is not morphed across kinds. Such a morph would need an authored
geometry contract rather than an arbitrary conversion, so these pairs also remain discrete.

## Solid and gradient transitions

For a solid-to-gradient pair, the solid is represented internally as a gradient whose stops all
have the solid color. The other endpoint supplies the gradient kind, geometry, stop offsets, and
spread mode. The reverse path uses the same rule. The working brush therefore has stable structure
during the open interval, while changing to or from the exact solid endpoint remains visually
continuous.

Opacity and transform come from each authored endpoint and continue to interpolate normally.
No synthetic gradient is stored back into either authored brush.

## Gradient-stop normalization

Stops are ordered and captured once when the plan is created.

- Equal stop counts retain ordinal pairing. Stop colors and offsets both interpolate, preserving
  the behavior of the original brush animator.
- Different non-empty stop counts use a sorted canonical union of both offset sets. For a repeated
  offset, the union retains the larger multiplicity from either endpoint, preserving hard stops.
- Each endpoint is resampled onto that canonical structure once. Exact duplicate colors retain
  stable order; missing offsets use piecewise color interpolation between neighboring authored
  stops.
- An empty gradient is treated as no paint, not as a transparent color. Empty-to-populated and
  solid-to-empty pairs therefore use the discrete fallback. Two empty gradients of the same kind
  remain structurally compatible.

The canonical offsets stay fixed for intermediate frames. The exact authored target replaces the
working brush at completion, so normalization cannot permanently alter authored stop count,
ordering, offsets, or identity.

## Other brush state

Compatible plans interpolate:

- color channels, including alpha, in the existing sRGB byte interpolation;
- brush opacity;
- `Matrix3x2` transform;
- linear start and end points;
- radial center, origin, and radius;
- sweep center, start angle, and end angle;
- gradient-stop colors and offsets.

`GradientSpreadMode` is categorical. The source mode is retained for intermediate frames and the
exact target mode becomes visible at completion. Radius remains non-negative.

## Scheduling, retargeting, and lifecycle

Brush transitions use `AnimationScheduler` and the same manual-clock test infrastructure as other
animations. There is no timer per brush, property, control, or resource. Visual-state transitions
share the control's state-transition entry; theme brush values share the single theme-transition
entry.

When a visual state or theme is replaced during a transition, the currently published presentation
brush is captured as the next source. The old scheduler entry is canceled and replaced, so the new
run continues from what was visible rather than restarting from an authored endpoint. Completion,
cancellation, control disposal, subtree detach, and theme replacement continue to use the existing
scheduler ownership and cleanup rules.

An explicit `Brush.AnimateTo` is different: it intentionally preserves the destination object's
identity and mutates it in place. Because its structure cannot change without replacing that
object, this compatibility API still requires matching exact built-in types and equal gradient
stop counts. It raises normal observable brush notifications on every changed frame.

## Rendering and resource notifications

Control visual-state rendering reads effective/current presentation brushes for background, text,
and borders. An intermediate working brush is therefore what the renderer paints and what a rapid
retarget captures. Authored `Style`, `StyleHover`, and other state brushes are not changed.

`ThemeManager` publishes the exact source at transition start, the reusable working brush during
intermediate frames, and the exact resolved target at completion. `ResourceDictionary` change
notifications follow those reference replacements. Controls already bound through dynamic
resources continue to invalidate through the established resource and observable-brush paths.

## Performance contract

Plan creation may allocate snapshots, sorted stop arrays, a canonical offset array, and one working
brush. The frame path performs indexed loops over fixed arrays and mutates existing brush and stop
objects. It does not use reflection, LINQ, structural parsing, replacement stop arrays, or a new
brush per frame. Scheduler batching retains the existing one-invalidation-batch-per-tick behavior.

## Deliberate limitations

- Cross-kind gradient geometry, `GlassBrush`, `NoBrush`, `null`, and custom/derived brushes use a
  discrete fallback.
- Color channels use the existing sRGB interpolation; linear-light or color-space selection is not
  introduced here.
- The Designer serializes known visual-state transition/easing metadata, but it does not live-
  preview transitions and custom delegate easing remains code-first.
- The planner is platform-neutral and the Android scheduler integration is implemented. Full
  physical-device frame-pacing and rendering validation remains outstanding.

See also [Paint and gradient architecture](paint-and-gradients.md),
[UI animation scheduler architecture](ui-animation-scheduler.md), and
[Animations](../animations.md).
