# Committed viewport Scroll notifications

`ScrollableControl.Scroll` reports actual movement through the existing scrollbar and
child-displacement path. It is synchronous on the UI thread. No additional scroll position
is maintained: the publication loop retains only the last reported offsets and causes.

```csharp
var panel = new ScrollableControl { AutoScroll = true, Size = new Size(320, 200) };
panel.Controls.Add(new Button { Bounds = new Rectangle(20, 500, 160, 36), Text = "Save" });
panel.Scroll += (_, e) => Console.WriteLine(
    $"{e.ScrollOrientation}: {e.OldValue} -> {e.NewValue} ({e.Type})");
panel.VerticalScrollProperties.Value = 40; // ThumbPosition, after child displacement
```

The event's `OldValue` and `NewValue` are **logical-pixel offsets relative to Minimum**.
For example, raw scrollbar values 340 → 350 with Minimum 40 report 300 → 310.
`ScrollProperties.Value` still uses the raw range; its validation has not changed.
`ScrollOrientation` always identifies the changed axis. Existing enum values and public
members are preserved.

Consumers that previously treated every Scroll as user input should check `Type`:
ThumbPosition now also reports programmatic movement and layout resets. The built-in
CarouselView preserves selection for those changes and handles current input only on its
active page axis; changing orientation must not select page zero when the old axis resets.

| Movement source | ScrollEventType |
| --- | --- |
| Public Value assignment, range clamp, resize clamp, fitting-content/AutoScroll reset | ThumbPosition |
| Arrow or wheel | SmallIncrement / SmallDecrement |
| Track click or accessibility page amount | LargeIncrement / LargeDecrement |
| Thumb drag or touch drag | ThumbTrack |
| Accessibility small amount | SmallIncrement / SmallDecrement |
| Accessibility absolute percentage | ThumbPosition |
| Accessibility signed viewport fraction | LargeIncrement / LargeDecrement |

Reaching an endpoint preserves the operation's cause; it does not invent First/Last.
No movement means no event, including a repeated assignment, clamped wheel step, repeated
thumb position, or hide/show without offset changes. There is no extra EndScroll event.
Changing Minimum can move the logical origin even if the raw Value stays unchanged.
Range metadata or viewport-size changes without offset movement still refresh the existing
derived viewport/accessibility hooks, but do not raise public Scroll.

## Ordering and reentrancy

The offset and surviving children's bounds are committed first. The existing
`OnScrollViewportChanged` hook refreshes derived content, then accessibility metadata and
public Scroll are published. When a batch changes both axes, horizontal is published before
vertical. Each axis coalesces to its current final offset and the last cause that moved it;
intermediate offsets undone before publication produce no event. Touch uses this batch path.
Accessibility validates both axes first, then completes horizontal before starting vertical;
if an observer removes/disables the target, vertical is not attempted and the action returns
false. Each committed axis can therefore also publish an accessibility metadata snapshot.
Nested wheel routing still consumes movement in the innermost viewport and bubbles at its endpoint.

An observer can change either axis. This commits normally but defers further notifications
until the current callback returns. Remaining callbacks for an obsolete position stop; the
next event is created from current state after refreshing the derived viewport. For example,
a touch batch reaches `(10, 20)`, then its horizontal handler assigns `(40, 60)`:

1. Horizontal 0 → 10, ThumbTrack.
2. Horizontal 10 → 40, ThumbPosition.
3. Vertical 0 → 60, ThumbPosition.

The original vertical 20 event is not delivered after the value has become 60. A public
Value assignment inside an input callback gets its own ThumbPosition cause. Reentrant
movement is limited by the existing 64-pass stabilization bound; a non-stabilizing observer
throws after cleanup, and a later operation can still run.

Subscribers are snapshotted per event. Removing a subscriber affects subsequent events;
removing/reparenting or disposing the viewport retires pending callbacks for that lifetime.
Exceptions are collected while valid remaining observers and viewport notifications run,
then surfaced as an AggregateException. A publication error does not erase a child-movement
error. Movement is already committed and is not rolled back. Mutating `e.NewValue` is
informational: it neither cancels nor replaces the offset and is restored before the next
subscriber. Overrides of `OnScroll` and `OnScrollViewportChanged` should call base.

## Standalone ScrollBar

`ScrollBar.Scroll` reports input changes after ValueChanged, using **raw range values** and
the real axis. Arrows, wheel, track and thumb follow the cause table above. Public Value and
range assignments retain their existing ValueChanged behavior without a standalone Scroll
event. If a ValueChanged callback replaces the committed value, the obsolete input Scroll
event is retired. The owning ScrollableControl observes canonical movement directly and
does not forward the scrollbar's Scroll event, avoiding duplicates.

## Validation

`ScrollableControlScrollEventTests` covers both axes and 13 movement paths, nonzero Minimum,
clamps, resize, hidden ancestors, no-ops, resets, batched/reentrant changes, mutable event
arguments, observer removal/disposal, bounded feedback and exception preservation.
Existing scroll-layout, touch and accessibility regressions remain applicable.
The Windows `--scroll-layout` scenario checks public event metadata and child geometry inside
callbacks on a real HWND while exercising native wheel/thumb/track, visibility, range and
resize transitions. ControlGallery → ScrollableControl includes programmatic actions and a
bounded log outside the scrolling viewports. DemoApp/template startup is unaffected.

This notification change adds no smooth scrolling, fling physics, native backend dependency,
or claim of physical Android touch/IME qualification.

## Revealing a control

`ScrollControlIntoView(target)` is available on `ScrollableControl` and its derived containers.
Use it when choosing a search result or showing a field from a validation summary.

```csharp
resultsList.SelectedIndexChanged += (_, _) =>
{
    if (resultsList.SelectedItem is SearchResult result)
        detailsPanel.ScrollControlIntoView(result.Field);
};

void ShowFirstValidationError(Control firstInvalidField)
{
    if (detailsPanel.ScrollControlIntoView(firstInvalidField))
        validationSummary.Text = "Check the highlighted field.";
}
```

The receiver must have AutoScroll enabled and a nonempty padded viewport. The target must be
a visible explicit descendant, including through ordinary nested containers. Null throws
`ArgumentNullException`. Self, detached, foreign, hidden, disposed, zero-size and implicit
targets return false. A disabled but visible field can still be revealed programmatically.
The method uses the owning UI thread and leaves keyboard focus and selection with their owners.

The method moves the nearest scrollable ancestor first, continues outward through the
receiver, and leaves ancestors above that receiver to the caller. Each movement follows the
existing clamp and publishes one neutral Scroll per changed axis. A fitting target moves the
minimum distance on each axis. An oversized target aligns its leading edge unless its bounds
already cover the viewport. A fitting viewport or an endpoint can produce a successful
request with zero movement. True means the request completed within the available ranges.

Coordinates use current logical viewport geometry after subtracting borders, padding and
scrollbars. Presentation transforms are mapped through the existing ancestor pipeline and
compared using an axis-aligned rectangle. Clipping by other controls, occlusion and the
visibility of individual transformed pixels are outside this rectangle calculation.
Observers can change layout after one axis. The next axis reads the fresh geometry.
Removing or reparenting the target during a callback stops further movement and returns false,
with previously committed scroll offsets still applied.
