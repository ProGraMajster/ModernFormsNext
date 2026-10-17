# CollectionView

`CollectionView` is the concrete, code-first data consumer of the shared
[`ItemsControl` virtualization infrastructure](virtualization.md). It realizes
owned controls near the viewport and recycles them independently of source values
and logical selection. It uses the existing layout, scrollbars, input, focus,
binding, theme and native accessibility paths. It is part of the 1.12.0 development
source; this guide does not imply that these APIs have already been published.

```csharp
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Drawing;
using ModernFormsNext;

var entries = new ObservableCollection<string>(
    Enumerable.Range(0, 100_003).Select(index => $"Entry {index}"));
var view = new CollectionView
{
    Dock = DockStyle.Fill,
    ItemSize = new Size(240, 32),
    CacheItemCount = 2,
    ItemsSource = entries
};
view.SelectedIndexChanged += (_, _) => Console.WriteLine(view.SelectedItem);
view.ItemInvoked += (_, e) => Console.WriteLine($"Activate occurrence {e.Index}: {e.Item}");
view.SelectedIndex = 90_000;
view.ScrollToItem(90_000);
```

The default template paints text and selection/focus cues. `SelectedIndex` is the
single canonical selection; `-1` means none. `SelectedItem` is read-only and may be
null for either no selection or a selected null value. Equal values and repeated
references are separate occurrences. Indexed insert/move notifications map
selection, focus and requested logical peers; remove/replace/reset retire the
affected identity. Replacing `ItemsSource` clears selection. `INotifyCollectionChanged`
and `IBindingList` use the existing subscription path. Call `RefreshItems()` after
changing a plain `IList` without notifications, including same-count replacements.

Every source mutation, factory callback, property change and public operation
belongs on the owning UI thread. The source/indexer must return cached synchronous
values; the control does no background retrieval or I/O. Source objects, values
and the factory remain application-owned. Counts, numeric metrics and content
coordinates retain the finite constraints described in the virtualization guide.

## Runtime templates

`ItemTemplate` borrows the existing `IItemContainerFactory<Control>` contract:

```csharp
using ModernFormsNext.Virtualization;

sealed class TextRows(CollectionView owner) : IItemContainerFactory<Control>
{
    public Control CreateContainer() => new Label();
    public void PrepareContainer(Control row, int index)
        => row.Text = owner.ItemsSource![index]?.ToString() ?? string.Empty;
    public void ClearContainer(Control row, int previousIndex) => row.Text = string.Empty;
    public void DiscardContainer(Control row) => row.Dispose();
}
// view.ItemTemplate = new TextRows(view);
```

Create distinct, unparented, undisposed controls. The host owns them after return
and makes each root a keyboard target. Prepare binds the current index. Clear
receives its previous index **after the source may have changed**: retain original
subscription/binding data instead of looking up that old index. Before Clear, the
host retires canonical DataBindings on the container and its old owned descendants;
cleanup assignments cannot write recycled text back through those old bindings.
Prepare must recreate per-item bindings, including bindings on composite children.
Binding context refresh follows the existing inherited context path after attachment.

Clear other subscriptions, commands and per-item state in ClearContainer. The
factory's DiscardContainer releases its resources; the host also disposes the
control, including after a throwing discard callback. The factory is not disposed.
Replacing ItemTemplate discards old rows through their original factory, resets
measurements and requests layout while retaining logical selection and focus.
Callbacks may replace source/template, redirect focus or dispose; older work checks
the canonical generation. Moving an owned row into another tree violates ownership:
the host preserves that newer tree and reports the error instead of disposing it.

Custom templates own their rendering/selection decoration. The Gallery's
`CollectionView` page shows bound composite rows, an interactive button, selection
and focus cues, source moves, renaming and observed counts with 100,003 entries.
This API does not introduce the deferred global DataTemplate/template-selector or
resource-lookup system.

## Layout, input and accessibility

Use the inherited vertical/horizontal stack and fixed-cell wrap options. Sizes
are logical pixels; variable stack extents use `MeasureItemContainer` and the
existing viewport-anchor correction. Wrap requires fixed extents. Disabling the
inherited VirtualizationEnabled switch suspends realization; CollectionView has
no eager, full-source rendering fallback.

Arrows, Home/End and PageUp/PageDown use shared navigation/reveal/focus recovery.
Pointer row activation, Enter/Space on the host or row, and accessibility Invoke
commit selection before ItemInvoked. A selection or source-value callback that
replaces the source, redirects selection, hides, disables, reparents or disposes
the view prevents a stale activation. Interactive descendants
keep their own keyboard, text and command behavior.

Canonical logical list-item peers represent source occurrences, with weak entries
only for queried items and bounded strong pins for the current viewport/cache and
selected/focused peers. Native weak providers therefore keep stable IDs across GC
while those occurrences remain active. Indexed notifications map peers before callbacks;
retired peers detach permanently. Native child projection is the realized/cache
range plus selected/focused peers, while flat collection metadata and indexed
Windows UIA navigation address the full count. A realized logical item's child is
its actual template control; embedded editors keep their existing text/value/focus
providers. Hit testing follows that same tree. No parallel semantic tree is created.

For a custom template, set the row's AccessibleName to a useful label in Prepare
and retire it in Clear. Without a realized custom label, the logical item uses an
ordinal name; it does not expose the raw model's ToString, which may contain data
the template intentionally redacts. Default text rows use their displayed text.
Custom root accessibility projections retain their existing visual/custom fallback.

## Designer and evidence

The built-in toolbox discovers CollectionView. Scalar layout settings participate
in the existing Property Grid, undo/redo, mfdesign and C# generation/parsing/runtime
round trips. Preview uses twelve synthetic text values with a cache capped at two,
production layout/rendering and deterministic disposal. The authored values remain
unchanged. ItemsSource, ItemTemplate, selection and runtime diagnostics are hidden
from serialization; project constructors, indexers and template delegates are never
executed for metadata or preview.

The [shared native certification](virtualization-certification.md) now exercises
CollectionView as its variable/template consumer alongside opt-in ListView. It
adds actual native End/Enter, template replacement, logical-peer retirement and
Android pointer/orientation checks, with Windows native DPI bounds checks. The
6,500-pair paced workload, original assertions and retained-memory threshold remain.
Record the exact source tree, configuration/APK hash and separate HWND, emulator,
physical and human-manual results. A runner or successful build is not runtime PASS;
finite soak evidence is not an unlimited-duration leak or absolute frame-rate claim.
Windows remains the primary runtime; Android remains experimental. See the
[platform support matrix](platform-specific-features.md) for backend boundaries.
