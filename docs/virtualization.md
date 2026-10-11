# Shared item virtualization foundation

The first slice of [#55](https://github.com/ProGraMajster/ModernFormsNext/issues/55),
tracked by [#199](https://github.com/ProGraMajster/ModernFormsNext/issues/199), provides
shared indexed geometry, metrics, occurrence mapping and container recycling under
`ModernFormsNext.Virtualization`. Production control integration and the complete
parent acceptance are tracked separately by
[#200](https://github.com/ProGraMajster/ModernFormsNext/issues/200).

This foundation does not yet make ListView, TreeView or DataGrid virtualized, add
CollectionView, or establish Windows/Android/Designer integration acceptance. The
parent issue remains open. Existing TreeView.VirtualMode resolves nodes when they
expand; it is a separate existing behavior and is not container recycling.

## Geometry through the existing layout system

`VirtualizingLayout` derives from the existing `LayoutEngine`. An existing Control
can implement `IVirtualizingLayoutHost`, return a current `VirtualizationContext`,
and realize/arrange that range inside the canonical `Control.PerformLayout` path.
Normal suspension, parent layout, scroll, focus and input contracts remain the host's
responsibility. Do not perform layout from paint or create another semantic tree.

The same pure geometry methods can be used by controls that draw logical rows rather
than create child Controls. Neither the layout strategy nor generator reads all
source values to compute a range.

```csharp
using System.Drawing;
using ModernFormsNext.Virtualization;

var layout = new VirtualizingStackLayout();
var metrics = new VirtualizingItemMetrics(100_000, estimatedExtent: 24);
var context = new VirtualizationContext(
    itemCount: 100_000,
    viewport: new RectangleF(0, 12_000, 400, 240),
    estimatedItemSize: new SizeF(400, 24),
    cacheItemCount: 3,
    metrics: metrics);

RealizedRange range = layout.GetRealizedRange(in context); // [497, 513)
RectangleF target = layout.GetItemBounds(in context, 90_000);
SizeF extent = layout.GetExtent(in context);
```

All geometry uses logical pixels. Viewport X/Y are unscrolled-content offsets, not
device coordinates. Returned item bounds start at the content origin; the host must
translate them using its existing padded/scrolled `DisplayRectangle` exactly once.
It must convert DPI through the normal backend path. Cell extents include spacing;
the host arranges controls, their margins and padding within these cells. Existing
Dock/Anchor/AutoSize policies must be handled explicitly by the integrating host.

Stacks support vertical/horizontal direction and optional variable metrics. Wrap
supports fixed cells in rows/columns, reflows against the viewport cross axis, and
realizes whole visible rows/columns plus the item buffer. Variable metric maps are
explicitly rejected for wrap; variable wrap is not claimed. Empty sources,
zero-area viewports and viewports entirely outside content realize no items.
The viewport and item ranges are half-open: an exact trailing boundary excludes
the next item. Negative content offsets are clipped to the content origin.

`VirtualizingItemMetrics` retains numeric metadata per logical item, not controls.
Unknown positive extents use the estimate. Measurement updates, prefix offsets and
lookup use logarithmic-time balanced prefix-tree operations. Identical measurements do not alter
offsets. The host owns anchor correction: save a logical occurrence and intra-item
offset before updating metrics, map that occurrence after a collection mutation,
then restore its corrected offset through the existing scrollbar path. Metrics do
not schedule layout or move scrollbars themselves. Reset discards all measurements
in O(itemCount) metadata work; it is not a per-frame operation. Zero/non-finite
measurements are rejected rather than inventing a hidden zero-area policy.

The current source window uses finite `int` indices/counts. Metrics require a count
at most 536,870,912 items because their balanced prefix storage uses CLR arrays;
available memory imposes a smaller practical limit. Returned
geometry is `RectangleF`/`SizeF`; representational overflow throws explicitly.
Integrating hosts must document their existing integer scrollbar limits and anchor
rounding. Logically unbounded sources can expose a finite/paged window; an async
source API and infinite extents are not implemented in this slice.

## Containers, binding and ownership

`ItemContainerGenerator<TContainer>` is independent from a particular control or
source value type. `IItemContainerFactory<TContainer>` connects it to an indexed
source and, when applicable, an existing Control host. A factory owns its source
subscription; the generator borrows the factory and never disposes the source.

The contract is:

1. `CreateContainer` returns a distinct, unbound, never-discarded container and
   transfers ownership to the generator. The factory cleans up an allocation that
   fails before successful return.
2. `PrepareContainer(container, index)` binds one current logical occurrence.
   Save the binding data needed later to detach subscriptions/commands/input state.
3. `ClearContainer(container, previousIndex)` retires that recorded binding before
   recycling/discard. The source may already have changed: never look up the old
   item from `previousIndex`. Retire capture/focus through the canonical framework
   path, detach from the host, remove handlers/resources owned by that binding,
   and leave the container unbound. Do not dispose a recyclable container here.
4. `DiscardContainer` releases the cleared container permanently, at most once
   for that ownership. The generator does not infer native cleanup success.

```csharp
// factory implements IItemContainerFactory<RowContainer> and borrows the source.
using var generator = new ItemContainerGenerator<RowContainer>(factory,
    recyclingCapacity: 32);

generator.Realize(range, itemCount: 100_000);
if (generator.TryGetContainer(500, out RowContainer? row))
{
    // The host may inspect the mapped row; lookup does not scroll or focus it.
}
```

The realization buffer and retained pool are different budgets. The buffer is part
of the requested `RealizedRange`; `RecyclingCapacity` bounds additional cleared
containers. The pool can retain reusable containers after the viewport shrinks.
Zero capacity explicitly disables retention. Retired bindings are cleared before
new bindings are prepared, so jump scrolling can reuse the same containers. A
fully settled repeated request performs no factory callbacks. Creation/reuse/
discard invocation and live/pool counts are available without snapshot allocation;
timing and existing-profiler producers belong to control integration.

## Collection changes and logical state

Subscribe once through the existing source/binding model. After each indexed
notification, call `NotifyItemsChanged(notification, newCount)` before requesting
the newly calculated viewport range. It returns an `ItemIndexChange` that can also
map selected/focused occurrence indices and the viewport anchor. Moves preserve
occurrences. Remove/replace retire only affected occurrences; reset retires all.
Duplicate/equal values remain distinct. A notification with unknown starting
indices becomes an explicit conservative reset. Inconsistent indexed block/count
metadata throws before an external request mutates generator state. Count changes
without notification also reset stale container mappings.

```csharp
ItemIndexChange mapping = generator.NotifyItemsChanged(e, source.Count);
selectedIndex = mapping.MapIndex(selectedIndex); // -1 remains no selection
// Recalculate geometry and request the range through the existing layout path.
```

The generator does not own selection, focus, an accessibility tree or a binding
engine. Never infer logical selection from a recycled container instance. A host
with stable source keys can explicitly restore state after reset; this initial
contract does not guess identity through `Equals` or duplicate object references.

## Threading, callbacks and failure

All generator/factory/metric work belongs on the host's owning thread. They are
not thread-safe and introduce no dispatcher. A Control adapter must use the
existing framework thread-affinity checks before accessing controls. Pure layout
queries have no UI affinity when borrowed metrics are not being changed concurrently.
Background source updates must marshal through the established dispatcher first.

Reentrant callbacks can request another range, notify collection changes or dispose
the generator. Range requests coalesce; every collection notification retains
order. Mapping/ownership is committed before callbacks, and old work checks its
generation before proceeding. No nested loop or blocking wait is used. Identical
reentrant requests do not restart preparation. A bounded stabilization loop fails
explicitly after 64 nonsettling changes rather than hanging the UI thread.

Disposal requested from a callback marks the generator disposed immediately and
cleans up after that callback returns. Explicit disposal clears all active bindings
and discards the active/pool containers. It does not dispose the factory/source and
has no finalizer. A factory exception faults/disposes the generator, attempts every
owned container's cleanup, and propagates the original/aggregated cleanup errors.
Even a partially prepared binding receives `ClearContainer`. Repeated disposal is
inert. `DiscardedContainerCount` counts discard invocations, including failures;
it is not a claim that native resources were successfully released.

## Evidence boundaries

The foundation's deterministic tests cover range/cell planning, measured prefixes,
100k indexed sources, repeated scroll/jump realization, pool bounds, allocation
after warmup, occurrence mutations, reentrant requests/disposal and cleanup failure.
These establish shared engine behavior. They do not establish rendered controls,
keyboard/focus/accessibility integration, Designer previews, native HWND behavior,
Android emulator runtime or physical-device frame pacing. Those remain required
by #200 and the parent before #55 can close. Do not add virtualization-only native
startup to Designer; previews must remain small and data-only.
