# Layout-aware visual-state metrics

Visual states can transition selected metrics that affect a control's content rectangle without
creating a second animation system. This layer extends the existing visual-state transition runtime
and uses the same process-wide `AnimationScheduler` as ordinary visual transitions and
[animated bounds](animated-layout.md).

## Resolution and state ownership

Controls continue to resolve states in this order:

```text
Disabled > Pressed > Hover > Focused > Normal
```

`Style`, `StyleHover`, `StylePressed`, `StyleFocused`, and `StyleDisabled` remain the authoritative
target styles. State styles inherit unset values through their `ControlStyle.ParentStyle` chain.
The transition runtime creates one temporary presentation style; it never writes interpolated
values back to a target style.

The implemented 1.10.0 scope supports these metrics:

- `ControlStyle.Padding`, interpolated independently on all four sides;
- aggregate and per-side `BorderStyle.Width` values.

`Control.Padding` remains the persistent fallback and never reports an interpolated value.
State-specific `ControlStyle.Padding`, when set, overrides that fallback for presentation layout.
Negative presentation padding produced by an overshooting easing is clamped through the normal
layout padding constraint.

Margin, explicit Width/Height/Size, MinimumSize/MaximumSize, font metrics, and container-specific
spacing are not visual-state metrics in the current style model and are deliberately outside this
contract. Corner radius remains render-only and is not part of this issue.

## One timeline and retargeting

Colors, brushes, transforms, padding, and border thickness are captured in one transition snapshot.
One scheduler entry advances the complete snapshot with the selected `VisualStateTransition`
duration and easing. Configuration is captured when the transition starts, so changing the
`VisualStateTransition` object affects future transitions without changing an active timeline.
There is no timer per control or per property.

When a newer state replaces an active transition, its source snapshot is the current presentation
style. A rapid `Normal -> Hover -> Pressed -> Hover` sequence therefore continues from what was
last displayed. A generation guard keeps a state change raised reentrantly during layout from
allowing an older callback to release the newer presentation style.

Zero-duration transitions, disabled animations, reduced motion, and design-time immediate policy
apply the exact target once without starting the tick source. If custom easing throws or returns a
non-finite value, the scheduler faults and removes that entry while the control first releases its
temporary style and synchronizes presentation metrics to the latest target.

## Layout, invalidation, and animated bounds

The runtime compares the previous and current integer presentation metrics after interpolation.
If rounding did not change padding or border thickness, no layout is requested. Otherwise all
changed metrics from that frame are applied in one self-layout; an auto-sized control also asks its
parent to measure once. Painting is invalidated through the existing control path.
Framework controls with cached content layout receive one internal presentation-content-metrics
notification for padding or border changes. Text, link, document, and rich-text caches therefore
use the same content rectangle that is rendered and hit-tested. The public `PaddingChanged` event
is not raised because `Control.Padding` itself did not change.

During that grouped layout, the animated-layout suppression scope from issue #25 prevents child
bounds computed from the presentation content rectangle from starting their own
`LayoutTransition`. This avoids applying the visual-state easing and then easing the same child
geometry a second time. The logical layout engine, Dock rules, constraints, and public
`Control.Bounds` implementation are unchanged.

`DockStyle.Fill` children consume the changing presentation display rectangle and resize
progressively. The current Anchor implementation does not rearrange anchored local bounds when
only container padding changes; layout-aware transitions preserve the same behavior as assigning
`Control.Padding` directly rather than introducing different animated semantics. Existing anchor
responses to container bounds changes remain owned by the normal layout and issue #25.

## Lifecycle and performance

The visual-state animation continues to use the control-owned scheduler key
`Control.VisualState`. Detach, recursive subtree removal, disposal, and window close cancel the
same owner entries and synchronize presentation metrics to the latest state. The scheduler stops
its shared tick source when the final entry is gone.

The frame path performs no reflection or full style reconstruction. Snapshots are value types,
interpolation is component-wise, and one existing temporary `ControlStyle` is reused throughout
the transition. Layout runs only when an integer layout metric actually changes.

The Designer stores padding and border-width style values plus transition duration and known easing
identifiers. It does not live-preview the transition and never serializes arbitrary easing
delegates; those remain code-first.
