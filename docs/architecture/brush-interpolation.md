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

## Default compatibility matrix

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

By default only the exact built-in `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, and
`SweepGradientBrush` types participate. A derived or otherwise unknown brush may add state with no
safe generic interpolation rule, so it deliberately uses the caller's existing discrete fallback.
There is no implicit reflection or user-code invocation to discover a custom path.

## Opt-in compatibility matrix

Set `BrushInterpolationOptions.EnableExtendedMorphing = true` to add the following mappings.
The default matrix remains unchanged when the option is false.

| Pair, in either direction | Extended behavior |
| --- | --- |
| Solid / populated gradient of any supported kind | Existing promotion to the gradient geometry |
| Same populated gradient kind | Existing ordinal or normalized stop interpolation |
| Different populated gradient kinds | Permanent discrete fallback |
| Glass / Glass with equal `ShowHighlight` and `ShowInnerBorder` | Interpolate four colors, opacity and transform |
| Glass / Glass with different layer flags | Permanent discrete fallback |
| NoBrush or any empty gradient / supported painted brush | Fade the painted endpoint using its complete geometry and layer flags |
| Any two explicit no-paint values, including different empty gradient kinds | One local NoBrush during the open interval |
| Glass / Solid or populated gradient | Permanent discrete fallback |
| Null / any brush; custom or derived / any brush | Permanent discrete fallback |

A no-paint fade copies the painted endpoint's colors, stop structure, transform and geometry,
then interpolates opacity from or to zero. It has no invented transparent stop colors or geometry.
The exact original references are restored at both endpoints. Null stays distinct: a control may
resolve it to its fallback color. Glass here means the existing layered tint/border brush; it does
not sample the background, add blur or require GPU work. Its flags must match because fill and
stroke paths interpret those layers differently.

Unknown and derived brushes permanently retain the automatic planner's discrete fallback.
For a custom code-first animation, supply an explicit `IAnimationInterpolator<T>` to the existing
typed scheduler API and manage that custom value's ownership. Automatic state/theme planning does
not invoke custom interpolators or a global registration mechanism.

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
- By default an empty gradient is treated as no paint. Empty-to-populated and
  solid-to-empty pairs therefore use the discrete fallback. Two empty gradients of the same kind
  remain structurally compatible.

Planning admits at most **4096 authored stops per endpoint and 4096 normalized canonical stops**.
Over-budget pairs are incompatible before sorting or constructing working collections. A union
that exceeds the budget is rejected before canonical/resampled arrays are allocated. This is an
animation admission bound, not a new brush setter limit; an oversized authored resource remains
unchanged and can still render. Direct factory use throws for an incompatible pair; state/theme
callers take their discrete fallback. Explicit in-place animation rejects unsupported size before
scheduling. Planning is O(n log n) for ordering and O(n + m) for union/resampling; frame work is
O(k) for the fixed working stop count.

The canonical offsets stay fixed for intermediate frames. The exact authored target replaces the
working brush at completion, so normalization cannot permanently alter authored stop count,
ordering, offsets, or identity.

## Other brush state

Compatible plans interpolate:

- color channels using the captured temporal color-space policy, with separately linear alpha;
- brush opacity;
- `Matrix3x2` transform;
- linear start and end points;
- radial center, origin, and radius;
- sweep center, start angle, and end angle;
- gradient-stop colors and offsets.

`GradientSpreadMode` is categorical. The source mode is retained for intermediate frames and the
exact target mode becomes visible at completion. Radius remains non-negative.

## Temporal color space

`ColorInterpolationSpace.Srgb` keeps encoded byte-channel interpolation (black/white midpoint
128). `LinearSrgb` decodes RGB with the standard sRGB transfer function, interpolates in linear
light, then encodes the result (black/white midpoint 188). Alpha remains a straight, separately
linear byte channel in both policies. This does not change premultiplication, the renderer's
compositor, display color management or gradient spatial sampling. The transfer functions follow
the [W3C sRGB definition](https://www.w3.org/TR/css-color-4/#valdef-color-srgb).

Gradient normalization continues to sample each endpoint's **spatial** color function in encoded
sRGB, matching the current renderer. Only interpolation **over time** uses the selected policy.
This preserves the source's gradient appearance when a transition first starts.

```csharp
var policy = new BrushInterpolationOptions
{
    ColorSpace = ColorInterpolationSpace.LinearSrgb,
    EnableExtendedMorphing = true
};
button.StyleTransitions.Add(VisualState.Normal, VisualState.Hover,
    new VisualStateTransition { BrushInterpolation = policy });

var options = new ThemeApplyOptions();
options.Transition.Enabled = true;
options.Transition.BrushInterpolation = policy;
// ThemeManager.Apply(theme, options) uses the same planner.

var interpolation = AnimationInterpolators.CreateBrushInterpolator(policy);
var presented = interpolation.Interpolate(new NoBrush(), new SolidColorBrush(Color.White), 0.5f);
```

Factories snapshot configuration when created; states and theme requests snapshot it when started.
Configure on the UI thread. Editing the original options does not alter a running animation.
Brush-only policy leaves raw visual-state fallback colors and non-brush theme colors unchanged.
`CreateColorInterpolator(space)` and `CreateGradientStopInterpolator(space)` allow explicit
code-first temporal policies for those values too.

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
Its opt-in overload is `brush.AnimateTo(target, policy, duration, ...)`; the argument order keeps
the original duration/key overload unambiguous, including an existing null key argument. Glass
still requires matching flags. No-paint fades that replace types belong to the value-style factory.
The policy overloads for brushes and stops are provided by `BrushInterpolationExtensions` in the
same `ModernFormsNext.Animations` namespace; existing static extension-class entry points remain.

## Rendering and resource notifications

Control visual-state rendering reads effective/current presentation brushes for background, text,
and borders. An intermediate working brush is therefore what the renderer paints and what a rapid
retarget captures. Authored `Style`, `StyleHover`, and other state brushes are not changed.

`ThemeManager` publishes the exact source at transition start, the reusable working brush during
intermediate frames, and the exact resolved target at completion. `ResourceDictionary` change
notifications follow those reference replacements. Controls already bound through dynamic
resources continue to invalidate through the established resource and observable-brush paths.

## Performance contract

Plan creation may allocate bounded snapshots, sorted stop arrays, a canonical offset array, and one working
brush. The frame path performs indexed loops over fixed arrays and mutates existing brush and stop
objects. It does not use reflection, LINQ, structural parsing, replacement stop arrays, or a new
brush per frame. Scheduler batching retains the existing one-invalidation-batch-per-tick behavior.
Linear-light conversion uses one fixed shared 256-entry decode table and scalar encoding math.
Finite geometry interpolation uses double intermediates so opposite finite float coordinates
cannot overflow during an open-interval convex frame.

## Deliberate limitations

- Populated cross-kind gradient geometry, null and custom/derived brushes retain their fallback.
- The Designer transition editor persists closed `BrushColorSpace` and
  `EnableExtendedBrushMorphing` metadata and round-trips known C# initializers. Default keys stay
  absent. Unknown names, foreign enum types and wrong scalar kinds are rejected. Static Designer
  preview does not schedule transitions; generated runtime applications apply the selected policy.
- Custom delegate easing remains code-first.
- The planner is platform-neutral and the Android scheduler integration is implemented. Full
  physical-device frame-pacing and rendering validation remains outstanding.

See also [Paint and gradient architecture](paint-and-gradients.md),
[UI animation scheduler architecture](ui-animation-scheduler.md), and
[Animations](../animations.md).
