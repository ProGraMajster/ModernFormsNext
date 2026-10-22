# Pages and stack navigation

`Page` is a lightweight `Control`, not a native window. `ContentPage` supplies one content
slot, `PageHost` presents a page inside an existing Form or `SkiaControlSurface`, and
`NavigationPage` retains a stack. Application startup remains `Application.Run(form)`.
Windows is the primary runtime; Android uses the experimental Application/Form backend.

```csharp
var home = new ContentPage
{
    Title = "Documents",
    Padding = new Padding(12),
    Content = new TextBox { Text = "Document title" }
};
var navigation = new NavigationPage(home);
var form = new Form { Text = "Documents" };
form.Controls.Add(new PageHost { Dock = DockStyle.Fill, Page = navigation });

var open = new AsyncCommand(async () =>
{
    var details = new ContentPage
    {
        Title = "Details",
        Content = new Label { Text = "Selected document" }
    };
    if (!await navigation.PushAsync(details) && details.Parent is null) details.Dispose();
});
// Put these buttons in the application's ordinary toolbar/panel:
var openButton = new Button { Text = "Open", Command = open };
var backButton = new Button { Text = "Back", Command = navigation.BackCommand };
Application.Run(form);
```

Mount pages through a `PageHost` to receive host lifecycle events. Directly adding a page
as an ordinary Form child does not assign a page owner. `ControlGallery` → **Pages and
navigation** demonstrates content, validation, retained history and Back.

## Ownership and the actual tree

All access and navigation starts belong on the owning UI thread. There is one control tree,
one focus transaction, one binding/resource inheritance path and one semantic tree. A host
uses real private presentation controls in that tree; do not edit those implementation
parents. A retained inactive page is hidden, keeps its controls and resources, and remains
owned until popped or removed. Its existing accessibility peers are hidden; they are not
copied into a second semantic representation.

- A host accepts a detached page. An already parented page, duplicate retained/pending push,
  disposed page or parenting cycle is rejected. Detach explicitly before transferring hosts.
- `PageHost.TrySetPage` returns false if canonical focus validation rejects departure or a
  newer callback supersedes the replacement. `Page = value` uses the same admission.
- Replacing `PageHost.Page` detaches the previous page without disposing it. The caller
  owns that old page. Host disposal disposes pages it still owns.
- `ContentPage.Content` is the sole authored child. Replacement detaches the old content
  without disposal; normal collection reparenting, resources, binding and focus apply.
  Use a Panel for multiple children. A newer callback assignment wins.
- `PushAsync` retains a detached page. The first push into an empty navigator creates its
  root. `PopAsync` retains the root and returns the removed page; `PopToRootAsync` returns
  removed entries in their former root-to-current order. Dispose or reuse transferred pages.
- `NavigationStack` is a read-only snapshot containing borrowed page references. External
  removal/reparenting or explicit page disposal removes that entry from history. Removing
  the current page reveals the remaining predecessor. The old host cannot reclaim it.
- Navigator disposal disposes remaining owned entries, cancels queued requests and retires
  transitions. Pages transferred by application callbacks to another owner survive.

Content fills its page's logical padded rectangle, minus the child's margin, with normal
minimum/maximum bounds constraints. Dock/Anchor values are retained; the one-content slot
supplies the final outer bounds. Host padding surrounds its presentation control; page
bounds are local to that real parent. DPI scaling follows existing control geometry.

## Lifecycle and event order

Constructing a page or assigning it to a detached host raises no appearance events and
starts no window, timer or animation. An owned inactive page is `Attached`; an unowned
page is `Detached`. Presentation starts in a shown, visible Form or an attached neutral
surface, with design sites remaining inert.

On a visible page switch the logical current page/history commits first, then the previous
page receives `Disappearing` → `Disappeared`, and the incoming page receives `Appearing` →
`Appeared`. Each state commits before `LifecycleStateChanged` and the corresponding event.
`Appeared` waits for an animated incoming appearance to settle. Explicit page disposal
retires its host association and reaches `Disposed`; the finalizer path runs no managed
tree cleanup or lifecycle callbacks. State getters remain readable after disposal.

Host hide/removal/closure ends presentation; show/remount starts a fresh appearance.
Native focus loss, app suspension and Android Activity recreation alone do not remove a
page or manufacture disappearance events. The shared animation scheduler handles native
pause and reduced motion independently. Reparenting a navigator or its ancestor retires
the old navigation queue and transition; committed history remains available in its new host.

For an explicit request:

1. `Navigating` can cancel before validation or ownership commitment.
2. The real host selects an eligible admission target through `ControlFocusScope`. Editing
   validation, binding writes, IME handoff and redirected focus use the canonical transaction.
   A veto keeps the old page and editor; an outside focus owner is not stolen.
3. The incoming ownership reservation is checked again after callbacks and cancellation.
   History then commits. Lifecycle callbacks observe the new history.
4. Removed pages detach, `StackChanged` reports committed history, and admitted focus is
   restored to the incoming page's remembered eligible child or ordinary traversal target.
5. The shared scheduler settles appearance; `Navigated` reports the committed operation and
   `RemovedPages`, then its task completes. External entry removal also raises `StackChanged`.

Callbacks may navigate again, hide/close, reparent, dispose or throw. New requests queue in
FIFO order, with a limit of 128 waiting requests. Duplicate pending pushes are rejected.
Do not synchronously block the UI thread on a navigation task from one of its own callbacks.
Application callbacks keep normal exception behavior; cleanup still settles owned state.
An exception after history commits faults the task without undoing application effects.
Keep an earlier stack snapshot when application callbacks may throw and the caller needs
to recover every removed reference after a fault. A snapshot never grants ownership of a
page that a callback subsequently transferred elsewhere.

## Transitions and cancellation

`TransitionDuration` defaults to 180 milliseconds. `animated: false`, zero duration and the
existing reduced-motion policy settle immediately. The effect changes only an owned
presentation control's opacity, preserving authored page opacity. No private clock, timer,
layout engine or renderer is introduced. Native child views retain the existing airspace
limitations during unsupported composition; see [NativeViewHost](native-view-hosting.md).

Cancellation observed before the history commit leaves history unchanged and detaches an
uncommitted reservation. Queued cancellation is observed when that request reaches the
front; the bounded queue is released on drain/disposal. After commit, cancellation stops
only the visual effect and settles the committed page. It does not rewind newer work.
Host retirement prevents old completion callbacks from publishing into a new lifetime.
If the UI dispatcher rejects delivery, active and queued tasks fault without running UI
completion or observer callbacks on the rejecting thread. Committed history is retained;
normal managed control cleanup still belongs on the UI thread.

## Back and platform boundaries

`BackCommand` is an ordinary `AsyncCommand`: buttons share its availability and single-flight
execution; `ExecutionTask` exposes its result/fault. `ICommand.Execute` faults use the existing
UI dispatcher exception path. BrowserBack and Alt+Left are ordinary scoped input bindings.
A second Back during navigation is consumed without another pop or native window closure.
Bindings apply only when their navigator is on the actual focus route; nested navigators
use the normal nearest-scope rule.

Android system Back dismisses the current popup first. For a non-popup dialog/main window,
the backend delivers BrowserBack down/up into the existing raw input route. A handled event
suppresses native close. An unhandled event keeps the existing dialog/main close fallback,
including Form.Closing cancellation. Hide/close/recreation during dispatch consumes the old
delivery; it cannot close a replacement presentation. The IME still owns Back while visible.
Windows title-bar close remains a Form/window operation; it is not translated into a pop.

[TabbedPage and FlyoutPage](page-composition.md) now compose this same runtime. Routes, shell,
deep-link factories, Designer code generation and template integration remain later children of #12.
Human TalkBack/IME/physical-keyboard and interactive Visual Studio qualification remain
separate from deterministic, native automated, emulator and physical-device evidence.

The explicit native Android scenario uses the ordinary signed Release/default AOT sample APK:

```powershell
./scripts/android/Invoke-ReleaseValidation.ps1 -Scenario PageNavigation `
  -DeviceId '<explicit serial>' -ApkPath '<signed Release APK>' `
  -OutputDirectory '<new evidence directory>'
```

Run on an unlocked authorized phone and an emulator separately. The existing runner archives
APK/source hashes, device identity, original/final settings, native reports and two frames.
The Windows native test host's `--pages` case addresses only its own HWND with key messages;
it does not require global desktop input or establish physical keyboard evidence.
