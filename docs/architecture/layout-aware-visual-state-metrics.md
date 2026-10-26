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

The current supported matrix is:

| Metric | Interpolation and units | Invalidation |
| --- | --- | --- |
| `ControlStyle.Padding` | Four integer logical-pixel sides, with the existing padding constraint. | Grouped content layout. |
| Aggregate/per-side `BorderStyle.Width` | Existing integer border-width values and rounding. | Grouped content layout. |
| `BorderStyle.Radius` | Non-negative integer style-radius values; the planner adds no DPI conversion. Existing renderer coordinate conversion is retained. Negative legacy pairs switch to the target. | Repaint only; no new hit-test geometry. |
| Resolved `FontSize` | Integers from 1 through 4096, in the same legacy font-size units with one renderer DPI conversion. Both endpoints need the same borrowed typeface instance and full style flags. | Text-cache notification and grouped layout. |
| `LineHeight` | Positive shared line-height multiplier through 16, with the font-size compatibility rule. | Text-cache notification and grouped layout. |
| `LetterSpacing` | Additional logical-pixel spacing from -1024 through 1024, scaled once by shared text layout, with the font-size compatibility rule. | Text-cache notification and grouped layout. |
| Font family, face, weight, slant, underline, strikeout | Discrete target selection if identity or flags differ. Distinct low-level typeface instances conservatively use this fallback even if their family names match. | One initial text/layout notification. |
| Margin, Width/Height/Size, MinimumSize/MaximumSize, container-specific spacing | Not `ControlStyle` state metrics; preserve ordinary control/layout contracts or use `LayoutTransition` for supported bounds results. | Existing setter/layout behavior. |

`Control.Padding` remains the persistent fallback and never reports an interpolated value.
State-specific `ControlStyle.Padding`, when set, overrides that fallback for presentation layout.
Negative presentation padding produced by an overshooting easing is clamped through the normal
layout padding constraint.

Font sizes outside the interpolation range retain target-discrete behavior; there is no new
validation limit on `ControlStyle.FontSize`. An incompatible font pair switches its line height
and spacing together. New text/radius channels clamp eased progress to 0 through 1 to preserve
valid endpoint ranges; existing visual/padding/border easing behavior is unchanged.

```csharp
caption.Style.FontSize = 12;
caption.Style.LineHeight = 1;
caption.Style.LetterSpacing = 0;
caption.StyleHover.FontSize = 20;
caption.StyleHover.LineHeight = 1.5;
caption.StyleHover.LetterSpacing = 0.75;
caption.Style.Border.Radius = 2;
caption.StyleHover.Border.Radius = 14;
caption.StyleTransitions.Add(VisualState.Normal, VisualState.Hover,
    new VisualStateTransition { Duration = TimeSpan.FromMilliseconds(180), Easing = Easings.CubicOut });
```

The control must support that state and consume current-style text metrics. State styles retain
their existing parent-style chains; explicitly configure both endpoints if their defaults differ.
`Control.Font`, `Control.LineHeight` and `Control.LetterSpacing` remain persistent local values.
Read `CurrentStyle.GetFontSize()`, `GetLineHeight()` and `GetLetterSpacing()` for presentation.
An immutable `TextFont` descriptor is borrowed; the temporary `FontSize` overrides its resolved
size without constructing a font per frame.

## One timeline and retargeting

Colors, brushes, transforms, padding, border thickness, radius and text metrics share one snapshot.
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

The runtime compares presentation metrics after interpolation. Radius-only frames and unchanged
integer font/padding/border values request no content layout. Intermediate line-height/spacing
values use shaping-engine Single precision, while endpoint values remain exact. Changed content
metrics issue one grouped self-layout request; an auto-sized control also asks its parent to
measure once. The existing engine can perform its normal stabilization passes. Painting uses
the existing invalidation path.
Framework controls with cached content layout receive one internal presentation-content-metrics
notification for padding, border or text changes. Text, link, document, and rich-text caches therefore
use the same content rectangle that is rendered and hit-tested. The public `PaddingChanged` event
is not raised because `Control.Padding` itself did not change. Presentation fonts do not raise
persistent `FontChanged`. TextBox's lazy shaping key includes typeface identity, device font size,
style flags, line height and spacing, keeping paint, caret and hit testing on the same block.
GroupBox caption measurement uses its renderer's current font; `CaptionFontSize` still overrides it.
The shared text/image layout engine measures the current presentation typography, so button,
label and glyph-control text bounds agree with shaping during a transition. This does not add
text-based AutoSize behavior to controls whose existing preferred-size contract does not provide it.

AutoSize uses each control's existing preferred-size implementation. This feature does not add
text measurement to Label's base preferred-size implementation. Independently styled rich
documents, explicit caption metrics and custom renderers retain their existing text paths and
need not consume every shared typography value.

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

The frame path performs no reflection, font/typeface construction or full style reconstruction.
Value snapshots and one temporary `ControlStyle` are reused. Scalar equality avoids boxed Padding,
and empty host-invalidation batches do not allocate window lists. Focused tests require zero
managed allocation for warm radius-only detached-control frames and unchanged TextBox cache reads.
Real window snapshots and text shaping retain their ordinary costs.

Changed text metrics can rebuild wrapped text and propagate AutoSize measurement each frame.
Use bounded captions/editors; this is not a promise of allocation-free reflow or fixed frame time.
A representative test measures 64 frames of at most 512 UTF-16 units, verifies one metric
notification/frame and no typeface selection, and enforces a predeclared 256 KiB/frame allocation
ceiling. Recorded timings are host-specific. Prefer opacity, scale or translation for long
documents, large item surfaces or frequent state thrashing.

The Designer reuses existing style/font/radius data and provides nullable line-height/spacing
editors for all five state styles. The existing runtime bounds admit known neutral scalars;
wrong kinds, non-finite and out-of-range values cannot publish an edit transaction. Document/C#
round trips remain data-only. Static previews do not play transitions or execute arbitrary easing
delegates. Custom animation-definition playback remains the separate Designer animation contract.
