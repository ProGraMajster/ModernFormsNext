# CarouselView and RefreshView

These controls are part of the 1.12.0 development source. Their presence here does
not mean that a 1.12.0 package has been published. They use the existing collection,
input, command, focus, binding, resource and accessibility infrastructure.
Windows is the primary runtime; the Android backend remains Experimental.

## Viewport-sized pages

`CarouselView` owns one implicit `CollectionView`. It uses that view's source,
factory, generator, selection, occurrence identities and bounded recycle pool.
There is no additional page cache or selection model.

```csharp
var pages = new CarouselView
{
    Dock = DockStyle.Fill,
    ItemsSource = new[] { "Overview", "Details", "History" },
    PageOrientation = Orientation.Horizontal,
    CacheItemCount = 2
};
pages.SelectedIndexChanged += (_, _) => Console.WriteLine(pages.SelectedItem);
pages.MoveNext();
pages.SelectedIndex = 2;
```

`PageSize` is the current shared scroll viewport in logical pixels. The default
template displays text; `ItemTemplate` borrows `IItemContainerFactory<Control>`
for interactive page roots and descendants. Follow the ownership and binding
retirement rules in [CollectionView](collection-view.md). Source values and the
factory remain application-owned. Generated controls belong to the generator;
`GetItemContainer` returns a temporary borrowed view, not a page identity.

Navigation snaps immediately to fixed pages. There is no animated transition or
private timer. Wheel and root arrow/PageUp/PageDown input navigate whole pages;
Ctrl+PageUp/Down also work from interactive descendants. Descendants retain their
ordinary editing/activation keys. Touch release selects the nearest page; cancel
returns to the current selection. Programmatic navigation does not steal focus.
Logical item accessibility reveal/focus and scrolling use the same canonical
selection and providers. A page root can raise `ItemInvoked`; a descendant button
keeps its own action.

`SelectedIndex == -1` means no selection. A nonempty replacement source initially
selects its first page unless a callback makes a newer explicit request. Observable
indexed insert/move changes map the selected occurrence. Remove, replace and reset
can clear selection; call `MoveNext()` or set an index to choose a page again.
Call `RefreshItems()` after changing a plain `IList`. Reentrant source/selection,
focus, hide, reparent and disposal callbacks cannot restore obsolete navigation.
Resize, axis and DPI changes reuse ordinary layout and keep the selected page.

## Refresh ownership and input

`RefreshView` is a normal scrollable container. Add authored content to `Controls`
with normal Dock/Anchor layout. Its implicit framework buttons occupy a fixed
header (`HeaderHeight`, default 32 logical pixels). `Text` is the refresh caption;
`CancelText` is the cancellation caption. These properties can use ordinary
bindings/resources. The content stays enabled when the refresh command is busy.

```csharp
var refresh = new RefreshView { Dock = DockStyle.Fill, PullThreshold = 64 };
var reload = new AsyncCommand(async (_, token) => await ReloadAsync(token));
refresh.Command = reload;
refresh.Controls.Add(pages);
refresh.RefreshStateChanged += (_, _) => Console.WriteLine(refresh.IsRefreshing);
// Application shutdown owns cancellation/awaiting of reload.ExecutionTask.
```

Pull downward below the header, then release after `PullProgress` reaches one.
`PullThreshold` is a positive distance in logical pixels, default 64. Movement is
converted from device pixels exactly once by the shared surface router. A short,
reversed or canceled pull resets the indicator without executing refresh.
The actual refresh button, its accessibility Invoke action, F5 and
`RequestRefresh()` use the same path. A physical pointer is not required.

The existing pointer router chooses one consuming scroll ancestor after its
ordinary drag threshold, using the dominant movement axis. An inner scroller
away from its edge keeps the sequence even if it later reaches the top. An
ancestor refresh host can claim a downward sequence that starts at the top;
a horizontal carousel continues to consume horizontal paging. Hide, disable,
reparent, geometry changes and disposal retire old pointer ownership. There is
no second gesture recognizer and no mid-sequence transfer to a refresh ancestor.

Commands, parameters and targets are borrowed. `CommandSource` performs normal
availability checks, routed execution, background notification marshalling and
stale subscription rejection. `AsyncCommand` owns single-flight execution,
completion and cooperative cancellation. `IsRefreshing` and `CanCancelRefresh`
describe the current command. `CancelRefresh()` requests its cancellation; it
does not manufacture completion. Hiding, replacing or disposing the control
does not cancel/dispose an application command. Other `ICommand` implementations
execute synchronously; no asynchronous lifetime is inferred from them. A null
command allows synchronous `RefreshRequested`-only usage.

An admitted refresh checks availability, raises the real refresh button's Click,
then `RefreshRequested`, then makes a fresh guarded command execution. Recursive
requests are ignored. A handler that replaces bindings, hides/reparents/disposes
the host, or starts newer work prevents obsolete execution. Exceptions stop the
old request and retain normal command/dispatcher exception behavior.
`PullProgressChanged` observes the committed progress, including the release reset;
`RefreshStateChanged` observes committed command busy/cancel state. Both are on the
UI thread. Do not block that thread awaiting completion.

## Designer and validation boundaries

Designer creates only known framework controls and bounded synthetic pages (12
items, cache capped at two), never project source indexers, factories, commands or
application delegates. Runtime sources/templates, selection and refresh command
state are hidden from authored serialization and Property Grid. Page orientation,
cache size, header height and pull threshold use the production validation path;
implicit controls are not serialized as authored children. Metadata, `.mfdesign`,
C# generation/parser, compiled runtime and undo/redo follow existing Designer
contracts. Interactive Visual Studio F5 remains a distinct manual lane.

ControlGallery's **Carousel / Refresh** page demonstrates 100,003 pages, jumps,
source moves, interactive descendants, axis changes, F5/button/pull refresh and
application-owned explicit completion/cancellation. The explicit native
virtualization certification runners additionally exercise these production
controls before the unchanged collection soak: Windows HWND keyboard/wheel/mouse
messages and 96/144/192 DPI; Android native keyboard and MotionEvent page swipes,
pull/release/cancel and hide retirement. Emulator and physical-device results must
be recorded independently. Automated native input is not human visual, TalkBack
or interactive Visual Studio qualification.
