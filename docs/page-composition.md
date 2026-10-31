# Tabbed and flyout pages

`TabbedPage` and `FlyoutPage` compose the lightweight [page runtime](pages-and-navigation.md).
They remain controls in an ordinary Form or `SkiaControlSurface`. `Application.Run(form)`,
the dispatcher, validation, resource/binding inheritance and accessibility tree are unchanged.
Windows is the primary runtime; Android uses the experimental Application/Form backend.

```csharp
var documents = new ContentPage
{
    Title = "Documents",
    Content = new TextBox { Text = "Retained document title" }
};
var settings = new ContentPage { Title = "Settings", Content = new TextBox() };
var tabs = new TabbedPage();
tabs.Pages.Add(documents);
tabs.Pages.Add(settings);
var flyout = new FlyoutPage
{
    Detail = tabs,
    Flyout = new ContentPage
    {
        Title = "Navigation",
        Content = new Button { Text = "Close navigation" }
    },
    DisplayMode = FlyoutDisplayMode.Auto,
    FlyoutWidth = 280,
    SplitBreakpoint = 720
};
((Button)((ContentPage)flyout.Flyout!).Content!).Command = flyout.CloseFlyoutCommand;
form.Controls.Add(new PageHost { Page = flyout, Dock = DockStyle.Fill });
```

Use `ControlGallery` → **Tabbed / Flyout pages** for retained editing, collection operations,
validation veto, responsive modes and independent flyout/tab directions. Its **Open navigation**
button lives inside the detail page, so canonical focus admission can move from that page to
the flyout. Application toolbars outside the component retain their own focus.

## Tab ownership and selection

All access belongs on the owning UI thread. `Pages` has a read-only indexer and live enumeration.
Use `ToArray()` when a callback may mutate the collection and a borrowed reference snapshot is needed.
Pages must be detached, unique and not disposed; cycles and simultaneous admission are rejected.

- `Pages.Add` / `Insert` reserve ownership through the real host before collection commitment.
  `Attached` callbacks can supersede admission. The boolean result reports whether the new entry
  remained retained through callbacks. A false result does not dispose or reclaim a transferred page.
- The first enabled addition is selected if selection is still empty. Later additions retain selection.
  `SelectedPage`, `SelectedIndex` and `TrySelectPage` refer to logical collection order; null / minus
  one clears selection. An unavailable target produces false. Invalid membership/index throws.
- Inactive pages remain hidden, owned and alive. Their controls, data context, resource inheritance,
  edits and accessibility identities are retained. Only the current page's actual presentation is exposed.
- Disabling the selected page keeps its selection and ordinary disabled presentation. A disabled
  page cannot become a new selection. Reenabling a page does not manufacture a selection change.
- `Move` preserves selected identity, parent, ownership and lifecycle. The selected index can change
  without `SelectedPageChanged`, because the selected page did not change.
- `Remove` first admits departure from a selected page. It chooses the next enabled page after the
  removed index, then a predecessor, otherwise clears selection. A veto leaves membership untouched.
  Successful removal transfers the old page without disposal. Explicitly reuse or dispose it.
- `Clear` admits departure once, then removes entries in captured order. A newer collection callback
  interrupts the remaining removals. Its false result can follow a committed prefix; it is not rollback.
  An application recovering transferred references should keep its earlier snapshot and ownership policy.
- Explicit page disposal or external reparenting removes that membership. The old host cannot reclaim
  a new owner's control. Disposing the tab container disposes pages it still owns.

`SelectedPageChanging` runs before canonical validation. Its Cancel retains the old selection.
The real page host selects an eligible admission target, so binding writes, validation, IME handoff,
redirected focus and callbacks use the existing focus transaction. An outside focus owner is preserved.
Selection commits before outgoing/incoming lifecycle notifications, then headers and
`SelectedPageChanged` update. `PagesChanged` reports committed membership/order after ownership cleanup.

Callbacks can mutate again, reparent, dispose or throw. Newer state is authoritative. Exceptions
after commitment propagate without undoing application effects. At most 32 synchronous nested tab
mutations are admitted; this bounds callback cycles without a separate queue or event loop.

## Header input and overflow

Headers are actual themed buttons in an existing scrollable row, with ordinary tab-list/tab-item
accessibility peers. There is no second representation of each page or editor. Removed header peers
become hidden and expose no actions. Header titles follow the page's existing `Text` / `Title` changes.

The row uses canonical scrollbar, wheel, touch and semantic scrolling. Selection reveals its real
header through the existing nested-scroll operation. It does not duplicate a virtualizer or scroll engine.
The selected enabled header is a tab stop; other enabled headers remain available through arrow commands.

- Ctrl+Tab / Ctrl+Shift+Tab and Ctrl+PageDown / Ctrl+PageUp select the next/previous enabled page, wrapping.
- Left/Right apply on a focused header, following physical direction and wrapping over available pages.
  These bindings do not take arrow editing away from a focused TextBox.
- Header Home/End choose the first/last enabled page in logical order. Keyboard focus follows a header
  switch when the previous focus was on the header row; editing focus uses the page host's remembered child.

`FlowDirection` is explicitly `LeftToRight` or `RightToLeft` per container. RTL reverses the physical
header controls in the ordinary positive-coordinate scroll row; collection, Home/End and Ctrl+Tab
order remain logical. The same setting on a flyout chooses its leading edge independently. This local
contract does not rewrite authored text or local page edge choices. The separate inherited
[Control.RightToLeft contract](right-to-left-layout.md) applies to nested Flow/Table layouts;
page collection and header navigation order remain logical.

## Flyout ownership, modes and focus

`Flyout` and `Detail` each use a real `PageHost`. A page cannot occupy both slots. `TrySetFlyout` /
`TrySetDetail` report canonical replacement admission; property assignment uses the same path.
Replacement detaches the previous page without disposal. Disposing the container owns both retained slots.

`FlyoutWidth` and `SplitBreakpoint` are positive logical pixels. The preferred width is clamped to the
available padded width; the detail receives the remainder in Split. DPI follows normal control geometry.

- **Overlay** presents the flyout above the retained detail when `IsFlyoutOpen` is true. The owned backdrop
  intercepts hits in the covered detail. Only the owned detail host is disabled; authored page enabled intent
  is preserved. The detail remains retained and logically presented behind the overlay.
- **Split** presents both slots side by side. The overlay open intent is retained but does not hide either slot.
- **Auto** chooses Split at or above the padded logical breakpoint, otherwise Overlay. `EffectiveDisplayMode`
  reports the current choice. Crossing from Split into Overlay with a focused flyout editor preserves that
  editor by opening the overlay instead of hiding it without validation.

`IsFlyoutOpen` is read-only committed overlay intent. `TrySetFlyoutOpen` changes it immediately;
`SetFlyoutOpenAsync` can fade an incoming overlay using the shared scheduler. Empty flyouts cannot open.
The first voluntary departure from either edited slot uses normal validation before intent commits.
An incoming host becomes available before canonical focus transfer; the old host then disables/hides.
Focus outside this component is preserved. A newer appearance callback which redirects to a detail editor
abandons the older opening before that editor can be disabled.

```csharp
// Usually bind a button inside Detail so focus belongs to this composition's route.
openButton.Command = flyout.ToggleFlyoutCommand;
closeButton.Command = flyout.CloseFlyoutCommand;
bool applied = await flyout.SetFlyoutOpenAsync(true, cancellationToken: token);
```

Toggle is an ordinary single-flight `AsyncCommand`; its `ExecutionTask` exposes failures. Immediate Close
remains available during opening and retires the older effect. Back, Escape and backdrop dismissal use
the same admission, so a validation veto retains the editor and consumes that dismissal. Scoped keyboard
bindings apply on the actual focus route. A nested NavigationPage gets its nearest Back first; when its
stack no longer handles Back, the flyout can dismiss, then normal platform fallback remains available.

While overlay focus is in the flyout, Tab / Shift+Tab wrap through its ordinary eligible controls.
Editors that request Tab retain their existing input behavior. This is a component-scoped overlay;
other application controls outside its bounds remain part of the application's normal focus policy.

## Completion, lifecycle and limits

Incoming duration defaults to 180 milliseconds. Zero duration, `animated:false` and the shared reduced-motion
policy settle immediately. The effect changes only an owned presentation frame; authored page opacity is
preserved. Closing is immediate. Cancellation before commitment changes no intent; after commitment it
settles the visual effect without rewinding intent. A newer request, owner retirement, mode change or
disposal prevents an older completion from publishing into the new lifetime.

`Appeared` waits for an animated incoming flyout. Host availability and actual visibility drive page
lifecycle; native focus loss and Activity recreation alone do not remove pages. `FlyoutOpenChanged` and
`EffectiveDisplayModeChanged` publish the current committed state after presentation updates, coalescing
superseded callback changes. Exceptions propagate/fault the request without rollback. Rejecting dispatcher
delivery faults completion without running UI writes on the rejecting thread. Finalizer paths perform no
managed page/tree cleanup. Synchronous request nesting is bounded at 32 and layout callback settlement at 64.

NativeViewHost retains its existing [airspace/composition limitations](native-view-hosting.md); these page
containers do not introduce native controls, a renderer or a second application runtime. Runtime composition
uses [AppShell/routes](app-shell-routing.md) where application navigation is required.
[Designer page authoring](designer-pages.md) supports known static ownership and bounded preview;
route factories and application callbacks stay explicit in user code.
Native automated, emulator, physical-device, human IME/TalkBack/keyboard and interactive VS evidence remain
separate qualification lanes.

The checkout-only native scenario uses the ordinary signed Release/default-AOT sample APK:

```powershell
./scripts/android/Invoke-ReleaseValidation.ps1 -Scenario PageComposition `
  -DeviceId '<explicit serial>' -ApkPath '<signed Release APK>' `
  -OutputDirectory '<new evidence directory>'
```

Run separately on an emulator and an unlocked authorized phone. The existing runner records
APK/source hashes, identity, original/final settings, native reports and two frames. Windows uses
the native host's `--page-composition` case and its own HWND; it does not inject global keyboard input.
The Windows runner records the actual motion policy: enabled motion exercises busy supersession;
native immediate motion instead verifies settled lifecycle, idle scheduling and actual Back dismissal.
It does not override system settings or report the immediate-policy case as a busy-transition pass.
