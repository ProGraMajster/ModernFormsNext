# Shared item virtualization

The first slice of [#55](https://github.com/ProGraMajster/ModernFormsNext/issues/55),
tracked by [#199](https://github.com/ProGraMajster/ModernFormsNext/issues/199), provides
shared indexed geometry, metrics, occurrence mapping and container recycling under
`ModernFormsNext.Virtualization`. Production control integration and the complete
parent acceptance are tracked separately by
[#200](https://github.com/ProGraMajster/ModernFormsNext/issues/200).

The runtime integration in [#202](https://github.com/ProGraMajster/ModernFormsNext/issues/202)
adds a reusable `ItemsControl` and opt-in virtualized `ListView`. Designer and
Windows/Android certification completed in
[#203](https://github.com/ProGraMajster/ModernFormsNext/issues/203), closing #200/#55.
[CollectionView](collection-view.md), tracked by #274 under #13, consumes that same
completed infrastructure. This does not change TreeView/DataGrid behavior.
Existing TreeView.VirtualMode resolves nodes when they
expand; it is a separate existing behavior and is not container recycling.

See the [certification workload and evidence boundaries](virtualization-certification.md)
for repeatable Designer, HWND and Android validation. The presence of a runner does
not imply that every platform lane has passed for the current source tree.

## Use the supported ListView consumer

```csharp
var view = new ListView
{
    Dock = DockStyle.Fill,
    VirtualizationEnabled = true,
    ItemsLayout = ItemsLayout.Stack,
    ItemSize = new Size(220, 32),
    CacheItemCount = 3
};
view.SuspendLayout();
try
{
    for (int index = 0; index < 100_000; index++)
        view.Items.Add($"Entry {index}");
}
finally { view.ResumeLayout(); }

view.SelectedIndex = 90_000; // logical selection; no immediate focus/scroll
view.ScrollToItem(90_000);  // realizes the requested occurrence and reveals nested viewports
```

`ListView.VirtualizationEnabled` defaults to **false** to retain the old drawn-grid
behavior. Its new base is `ItemsControl`, which inherits `ScrollableControl` and
`Control`; existing `Items`, `SelectedItem`, `ItemDoubleClicked`, `Style` and
`ListViewRenderer.RenderItem` extension contracts remain. In virtualized mode the
renderer visits only the realized range and layout updates item Bounds before paint.
Unrealized Bounds are empty. Bounds use device pixels for the existing renderer;
container bounds, ItemSize, estimates and scroll offsets use logical pixels. The
host applies padding/scroll and the consumer converts item geometry exactly once.

The default ListView cell is 76 by 76 logical pixels, including six pixels of
trailing cell spacing, and wraps vertically. Distinct `ListViewItem` instances
represent distinct rows. Use the shared template host for sources where repeated
references or equal values represent separate occurrences. Virtualized ListView
uses one logical selected occurrence; its SelectedItem and item.Selected flags
follow SelectedIndex. The legacy first-selected-item behavior remains when disabled.
Enabling virtualization adopts the first legacy selection and clears other legacy
selected flags; a newer selection committed by a template callback takes precedence.
ListViewItemCollection publishes indexed `INotifyCollectionChanged` notifications
and supports `Move(oldIndex, newIndex)` without replacing occurrence identity.

## Derive another consumer using the same host

```csharp
public sealed class LogList : ItemsControl
{
    public void SetEntries(System.Collections.IList entries) => SetItemsSource(entries);

    protected override void PrepareItemContainer(Control row, object? item, int index)
    {
        // Uses the existing Control binding system and inherited BindingContext.
        row.DataBindings.Add(nameof(Control.Text), item!, nameof(LogEntry.Text),
            formattingEnabled: true, ModernFormsNext.DataBinding.DataSourceUpdateMode.Never);
    }

    protected override double MeasureItemContainer(Control row, object? item,
        int index, Size estimate) => ((LogEntry)item!).Expanded ? 64 : 32;
}
```

Set `VariableItemSize = true` for measured stacks. The default container is a
focusable Label with selection/focus feedback; replace `CreateItemContainer` for
other templates. Return a fresh unparented, undisposed control. Containers become
host-owned, so applications must not dispose or reparent them. Source values and
the IList remain borrowed; the host never disposes them. The source indexer must
be cached/synchronous and must not block the UI thread. The ordinary framework
BindingContext must be available for DataBindings, including a context supplied
explicitly for a standalone windowless tree.

ListView draws its logical items separately from its private focus containers.
Its default measurement therefore retains the primary `ItemSize` extent, including
when `VariableItemSize` is enabled. A derived ListView may override
`MeasureItemContainer` for a genuinely variable presentation. Measuring the empty
focus container would collapse the drawn cells and is not the default policy.

`ClearItemContainer` receives the original prepared object and index, after focus,
capture and attachment retirement. Do not look up that old index in the mutated
source. Remove template subscriptions and release template-owned resources there;
the host clears canonical DataBindings before this cleanup hook, so resetting a
container cannot write a recycled/empty value back to its old source. Every cleanup
is still attempted when another cleanup throws. Partial preparation is also cleaned. Binding preparation before first
attachment is reassociated through the existing BindingContext path after creation.
Borrowed control references from `GetItemContainer` are transient; never store
them as logical identity. A template failure faults its generator and attempts all
owned cleanup, then propagates the original error/cleanup errors. Replace the source
(for example clear it, then set a corrected source) to create a fresh generator.

The host subscribes to INCC, or IBindingList when INCC is absent. In-place binding
property notifications refresh only realized bindings and retain logical identity.
Indexed insert/remove/move/replace notifications map selection, focus and metrics
before callbacks observe them. Duplicate values are distinct occurrences. Removed,
replaced and reset occurrences clear selection; unknown or out-of-sequence index
history conservatively resets it. Plain IList count changes are detected by layout;
publish same-count replacements with the protected `RefreshItemsSource` hook.
Perform source mutation and every host operation on the owning UI thread.

## Scrolling, measurement and focus

Stack orientation can be vertical or horizontal. Wrap uses fixed cells and complete
intersecting rows/columns; VariableItemSize is rejected for wrap. ItemSize includes
spacing and must be positive. CacheItemCount is a nonnegative item buffer on each
side. Choose a bounded buffer appropriate to the viewport; asking for all source
items in the cache intentionally defeats bounded realization.

Unknown stack cells use the primary ItemSize estimate. Realized measurements update
prefix metadata. The leading occurrence and its intra-cell pixel offset are retained
through corrections, indexed changes, size changes, and font/theme/DPI invalidation.
During a metric reset the anchor survives temporary estimates until its cell is
measured; shrinking cells clamp an offset that no longer fits. Call
`InvalidateItemMeasurements` after an application-specific size policy changes.
Corrections use the existing LayoutEngine/ScrollableControl path after the canonical
layout completion hook, with a 64-pass stabilization limit. There is no layout from
paint, separate layout tree or dispatcher.

`ScrollToItem` realizes estimated bounds, corrects measurements and uses the same
nested ancestor reveal as accessibility. Setting SelectedIndex alone does not move
scroll/focus. On the row or host, Home/End/PageUp/PageDown and directional keys
navigate logical occurrences, including unrealized ones; wrap arrows use rows or
columns. Interactive controls inside a custom template keep their ordinary key
handling and command/input routing. Tab uses existing Control navigation.

Retiring a focused row transfers focus through the canonical focus scope to the host.
Its logical occurrence remains; re-entering the realized range restores its container
only while the host still owns focus. Reparenting the host retires the old root's focus
and never steals focus in the new root. ListView's stable existing logical item peers
provide Select, Focus and ScrollIntoView; private recycled controls are excluded from
the accessibility tree. Peer identity survives visual recycling. Other ItemsControl
templates use the existing accessible Control tree and may extend those canonical
peers for their own domain semantics.

## Runtime diagnostics and practical limits

`GetVirtualizationSnapshot()` reports actual logical/live/pool counts, created/reused/
discarded operations, range, measured count, content extent, last layout duration and
pass count. It creates no profiler or source enumeration. Existing PerformanceProfiler
records the host work as Custom activity inside ordinary Layout; applications can
read snapshots into their existing registered extension counters/gauges. Do not
register counters repeatedly while scrolling. Discard counts describe invocations,
including a callback that throws; they do not certify native cleanup success.

Pure generator/geometry warmed operations have separate zero-allocation tests.
Actual Control scrolling also raises canonical layout, bounds, attachment and input
callbacks, so its managed allocation contract is bounded viewport work rather than
zero bytes. The regression suite compares the same warmed workload at 1,000 and
100,000 source items and records allocation, elapsed time and process retained-memory
observations. Retained-memory samples include the process/runtime and are not proof
of a native leak check. Numeric variable metrics scale with source-window capacity;
appends reuse geometric capacity without rebuilding all prefix sums. Structural
inserts/removes/moves shift numeric metadata without creating an entire Control tree.

The host accepts finite indexed windows and finite integer scroll extents; it rejects
geometry beyond that range explicitly. Large or logically unbounded/paged sources
must expose a bounded window. No synchronous network fetch, async source runtime or
independent paging scheduler is introduced. TreeView can flatten visible logical
nodes and DataGrid can adapt indexed rows to the shared generator/metrics/layout
contracts while preserving their existing selection/accessibility/editing services;
this PR does not claim those existing controls now use recycled containers.

## Gallery and evidence boundaries

ControlGallery's **Virtualization** page displays a variable-height derived
ItemsControl alongside ListView, each with 100,000 entries. Jump, move, scroll,
keyboard, resize, theme/DPI and observed diagnostics use the actual shared host.
It adds no experimental content to the default DemoApp/template.

Runtime deterministic tests and a real Windows HWND fixture cover the production
path, including native key/wheel messages and stable logical accessibility peers.
Native tests are automated evidence, not human manual inspection. Safe scalar
properties have default/serialization metadata; source/template callbacks are
protected runtime hooks. Full bounded Designer round trips and Android emulator/
physical-device qualification remain required in #203. A build or Windows test must
never be presented as Android/Designer/manual PASS.

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
These establish shared engine behavior. Rendered controls, keyboard/focus/accessibility,
Designer previews and native hosts require the integration/certification evidence in
#202/#203 and the current concrete consumer's qualification. Foundation tests alone
do not establish Android runtime or physical-device frame pacing. Do not add
virtualization-only native startup to Designer; previews remain small and data-only.
