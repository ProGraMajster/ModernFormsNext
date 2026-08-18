# Animated layout architecture

## Purpose and scope

Animated layout is an opt-in presentation layer over the existing ModernFormsNext layout engine.
It lets a control move and resize smoothly after application code or a layout engine commits a new
rectangle. It does not introduce another visual tree, layout engine, timer, or platform-specific
control implementation.

The implementation interpolates the complete `RectangleF` (`X`, `Y`, `Width`, and `Height`) as one
value. Navigation transitions remain separate work. The Designer serializes a safe known-easing
subset, Android uses the shared Choreographer-backed scheduler, and selected visual-state content
metrics build on this foundation through the linked layout-aware contract below.

## Logical and presentation geometry

`Control.Bounds`, `Location`, `Size`, `Left`, `Top`, `Width`, and `Height` continue to represent the
logical target. Constraints and layout are complete before these properties change. Code reading a
control during a transition therefore sees one stable, final layout result rather than frame-
dependent intermediate values.

An internal presentation rectangle begins at the previous visual rectangle and approaches the
logical target. Parent composition, hit testing, coordinate conversion, and accessibility bounds
use this presentation rectangle. At completion it is discarded, making presentation geometry
exactly equal to logical geometry with no accumulated rounding drift. Controls without a
`LayoutTransition` retain the previous rendering and input path.

The control back buffer is still created and painted at the logical target size. Composition scales
that buffer into the current presentation rectangle. A child is therefore rendered once in its
parent's local coordinate system and naturally follows a moving or resizing parent; changing only
the parent's absolute position does not create redundant child transitions.

When an animated parent resize causes Dock or Anchor to recompute descendant rectangles, those
descendants commit their new logical bounds without starting redundant local transitions during
that layout pass. The parent's presentation scaling already carries the complete subtree between
the old and new sizes. Independent descendant changes outside that layout pass remain eligible for
their own transitions.

Visual-state padding and border metrics are a separate presentation input that can require a
grouped layout when their rounded value changes. That path reuses this subsystem's descendant
transition suppression so child rectangles are not eased twice. See
[Layout-aware visual-state metrics](layout-aware-visual-state-metrics.md).

## Starting and retargeting

`Control.UpdateBounds` is the single integration point after `SetBoundsCore` has applied
`MinimumSize` and `MaximumSize`. Layout engines already converge on this path by assigning one
complete child rectangle. Dock, Anchor, FlowLayoutPanel, TableLayoutPanel, and direct bounds changes
therefore share the same transition behavior without changing their logical semantics.

If a target changes while a transition is active, owner-and-key replacement cancels the previous
scheduler entry and the replacement starts at the current presentation rectangle. It never jumps
back to the old source or forward to the replaced target. Several setters in one synchronous layout
pass can replace the pending target, but only one scheduler entry remains active for the control.

The transition is immediate when it is absent, disabled, has a non-positive duration, motion policy
requests immediate completion, the control is not attached and created, either rectangle has no
positive area, or the control is hidden. Disabling or removing a transition during animation
cancels its scheduler entry and snaps presentation geometry to the logical target.

## Scheduling, easing, and threading

Animated layout uses `AnimationScheduler.Default`, the same monotonic, dispatcher-backed scheduler
as the rest of the animation system. Every control uses the owner key `Control.LayoutTransition`, so
retargeting uses normal replacement semantics. `LayoutTransition.Easing` accepts the existing
`Easings` functions; rectangle interpolation uses `AnimationInterpolators.RectangleF`.

There is no timer per control. The scheduler starts its one tick source for the first active
transition, removes completed, canceled, and faulted entries, and stops when idle. Production
callbacks run through the registered UI dispatcher. Tests inject the existing manual clock and tick
source and never sleep.

Each frame updates only presentation state and requests repaint. It does not write logical bounds or
run layout. Invalidation marks the composition owner dirty, reports the union of the old and new
presentation rectangles, and uses the existing window-level coalesced frame request. Android keeps
using its main-Looper dispatcher and `PostInvalidateOnAnimation` rendering endpoint; the core has no
Android or Windows dependency.

## Rendering, clipping, and input

Both the normal control compositor and the form adapter call the same back-buffer composition
helper. This preserves the previous fast path when there is no presentation or render transform and
keeps opacity, translation, scale, and rotation compatible with animated geometry.

Hit testing checks presentation bounds. A point inside a resized presentation rectangle is mapped
back into the logical back-buffer coordinate system before nested routing and event delivery. This
keeps pointer input aligned with what is drawn, including nested controls. `PointToScreen` and
accessibility bounds use the same presentation mapping. Logical layout calculations continue to use
logical bounds only.

Parent clipping is unchanged. A presentation rectangle is composed inside the parent's existing
back buffer and clip, just like an immediate child rectangle. Animated layout does not relax
overflow or introduce a separate clip tree.

## Dock, Anchor, and constraints

Dock and Anchor calculate their final child rectangles exactly as before. A right-docked control
receives its new logical position immediately after its parent resizes; presentation then moves to
that position. A left-and-right anchored control receives its constrained target width first and
presentation interpolates the width. Flow and table layout results use the same bounds path.

`MinimumSize` and `MaximumSize` are applied by `SetBoundsCore` before transition creation. The exact
final presentation rectangle is consequently always the already-constrained logical rectangle.
Intermediate presentation sizes are visual interpolation values and do not feed back into layout.

## Lifecycle and ownership

The control owns its scheduler entry. Hiding, detaching, or disposing a control cancels the active
transition and synchronizes presentation state. Detaching a subtree and closing a window recursively
cancel animation ownership for descendants as well, preventing captured callbacks from retaining a
dead visual tree. Application shutdown continues to use the scheduler's global shutdown path.

Transition configuration changes must occur on the UI thread, like other control properties. The
scheduler can accept starts and cancellation from any thread, but all presentation updates and
invalidation callbacks follow its UI-dispatcher contract.

## Public API

```csharp
using ModernFormsNext.Animations;

card.LayoutTransition = new LayoutTransition
{
    Enabled = true,
    Duration = TimeSpan.FromMilliseconds(250),
    Easing = Easings.EaseOut
};

// Bounds is the logical target immediately. Rendering and input transition to it.
card.Bounds = new Rectangle(100, 50, 400, 200);
```

`LayoutTransition` is expandable in a property grid. `Enabled` and `Duration` are ordinary editable
properties. The ModernFormsNext Designer stores built-in easing identifiers without serializing
arbitrary delegates; custom easing delegates remain code-first.

## Current limitations

- transitions are configured per control; there is no inherited container policy;
- controls without positive source and target area snap instead of animating through zero size;
- parent overflow and clipping behavior is unchanged;
- the Designer edits and round-trips transition configuration but does not run a live transition
  preview;
- Android uses the shared scheduler and Choreographer frame source, but broad emulator and
  physical-device validation remains outstanding;
- layout-aware visual-state composition is defined in
  [Layout-aware visual-state metrics](layout-aware-visual-state-metrics.md); metrics outside padding
  and border widths remain discrete.

The ControlGallery **Animated layout** page provides manual checks for movement, resizing, rapid
retargeting, hit testing, nested content, and disabling a transition mid-flight.
