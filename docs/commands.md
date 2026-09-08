# Commands, input bindings and routing

Commands represent reusable application actions. `System.Windows.Input.ICommand` is the contract:
existing application implementations can be assigned directly. `ModernFormsNext.DelegateCommand`
is the optional synchronous implementation provided by the framework. Events remain supported;
an application can use `button.Click` without creating any command.

The system includes delegate and asynchronous commands, parameters, availability, keyboard
gestures, scoped input bindings, routed commands and hierarchical handlers. `Button`, `MenuItem`
(including Menu, ToolBar and ContextMenu actions) and `NotifyIconMenuItem` share the same
command-source behavior. Commands remain code-first and independent of Designer tooling.

## Define once, reuse with different parameters

```csharp
using ModernFormsNext;
using System.Windows.Input;

bool canSave = true;
var save = new DelegateCommand(
    execute: parameter => Console.WriteLine($"Save {parameter}"),
    canExecute: parameter => canSave && parameter is string);

var first = new Button {
    Text = "Save first", CommandParameter = "First document", Command = save
};
var second = new Button {
    Text = "Save second", CommandParameter = "Second document", Command = save
};
var trayItem = new NotifyIconMenuItem("Save first") {
    CommandParameter = "First document", Command = save
};

canSave = false;
save.RaiseCanExecuteChanged(); // Each source reevaluates using its own parameter.

ICommand compatible = save;
```

For actions without parameters, use `new DelegateCommand(() => Save(), () => CanSave)`.
The parameter-aware overload takes `Action<object?>` and `Predicate<object?>`. Both constructors
reject a null execute delegate. A missing predicate means the command is available. Parameter
values, including null, are passed without reflection or conversion; applications validate the
types they accept in their predicate. There is no generic command or separate public `Command`
alias in Phase 1. The unrelated internal `DataBinding.Command` ID registry is unchanged.

## Availability and enabled intent

For a Button:

```text
effective Enabled = local Enabled intent AND parent Enabled AND command availability
```

The public setter records local intent. Its getter returns the effective value; it does not call
`CanExecute`. Assigning `Enabled = false` remains effective across requery, command replacement
and removal. Assigning `Enabled = true` cannot override an unavailable command or disabled parent.
`NotifyIconMenuItem` similarly combines local intent with command availability, preserving its
existing non-Control parent semantics. Open native tray menus keep their existing snapshot
behavior; execution is still guarded against a command that has become unavailable.

The source reevaluates when a different Command or CommandParameter reference is assigned,
when it receives CanExecuteChanged, and during an otherwise enabled activation. Assigning the
same reference is a no-op. If a parameter object changes internally, raise CanExecuteChanged.
Removing the command removes its availability restriction; the local enabled intent remains.
The parameter stays assigned for reuse until replaced or the source is disposed.

Predicates should be fast, free of side effects, and tolerate repeated evaluation. There is no
polling, per-frame evaluation, global requery event or CommandManager. State changes update the
existing rendering, focus, descendant-enabled and accessibility notification paths. They do not
request a new layout policy. Existing mouse/keyboard disabled-state guards still apply.

## Activation and event order

Button pointer activation, existing Space/Enter activation, `PerformClick()` and semantic
accessibility Invoke converge on the same normal action path:

1. Ignore disabled or disposed sources. Check the current command's CanExecute before Click.
2. Apply the Button's existing DialogResult behavior, then raise Click.
3. If the source is still alive and enabled, reevaluate the **current** command and parameter and
   execute it when available.

Click handlers can replace/remove the command, change its parameter, disable or dispose the
source. Those changes are respected. A Click handler that throws stops execution. A predicate
that changes the binding during a source's evaluation invalidates that evaluation; the source
does not execute its obsolete snapshot. Predicates should not rely on such reentrancy as an
application design pattern. Application ICommand implementations remain responsible for callbacks
and state changes inside their own Execute method.

Click precedes execution to preserve the event-based action path and let handlers update or cancel
the pending command. Executing first would make such changes too late; suppressing Click whenever
a command is assigned would break event subscribers. Put a reusable application action in Command;
do not perform the same action again in Click. The framework does not deduplicate application code.

Only left-button activation executes Button commands. Existing right-click/context-menu behavior
does not execute the command. Programmatic PerformClick does not require visibility; native input
and accessibility retain their existing visibility checks. Disabled/disposed Button.PerformClick
now does nothing, closing the previous programmatic bypass of the normal disabled input guard.
No public member is removed or renamed. NotifyIconMenuItem.PerformClick retains its existing
ObjectDisposedException behavior after disposal, and separators never activate.

For an unchanged, available binding, the source evaluates CanExecute once before Click and once
after Click. Assignments and notifications during Click can cause additional evaluations.
`DelegateCommand.Execute` checks CanExecute once for callers that invoke it directly. Framework
sources use an internal action entry point after their own fresh check, avoiding a redundant third
evaluation outside the source's binding/exception guard. Arbitrary ICommand implementations receive
the normal Execute call and may perform their own checks.

The post-Click check closes the gap caused by synchronous Click mutations, including availability
changes without a notification. It is not an atomic transaction with application state on other
threads. Neither the helper nor the source assumes an application ICommand finishes its external
work when Execute returns.

## Lifetime and exceptions

Sources subscribe once per command reference. Replacement, removal and disposal detach the old
handler; stale invocation lists and queued old notifications cannot affect the new binding.
Event subscriptions hold a weak reference to the source behavior, so a long-lived command does
not keep an abandoned control alive. Explicit disposal still provides deterministic detach.
Disposal releases command and parameter references without disposing either object. A shared
command remains usable by the other sources. The command retains its own delegates/captures for
its lifetime; application-owned event subscriptions retain normal .NET ownership semantics.

Synchronous Execute and Click exceptions propagate unchanged through the calling action path. CanExecute
exceptions likewise propagate, but first make the receiving source unavailable. The command and
parameter assignment remain installed; a successful later requery, different assignment or
command removal restores a usable state. Enabled getters remain safe, cached state reads even
after a failing predicate. There is no catch-all error bus, wrapping or silent failure.

RaiseCanExecuteChanged uses ordinary synchronous multicast-event semantics. A throwing subscriber
stops that invocation list; later subscribers may not refresh until the next notification. Every
activation still checks availability. Exceptions in work posted by a background notification go
through the existing dispatcher exception path, on the UI thread rather than the raising thread.

## Threading and asynchronous work

Create/bind, change parameters/Enabled, activate and dispose sources on their owning UI thread.
UI-thread CanExecuteChanged is handled synchronously. Background CanExecuteChanged is posted via
`Application.RunOnUIThread`; the application UI loop must be initialized and running to process
it. The callback evaluates the current parameter on that UI thread. Notifications pending at
replacement or disposal are ignored. The helper does not synchronize application data accessed
by predicates or action delegates; use the application's existing synchronization rules.

`DelegateCommand` is synchronous. Do not pass async lambdas to its Action constructors: those
would be async void. Use `AsyncCommand` for Task-based work. Existing application ICommand
implementations retain their own execution and exception contracts.

### AsyncCommand

One helper supports `Func<Task>`, `Func<object?, Task>` and
`Func<object?, CancellationToken, Task>`, each with an optional availability predicate.
The two-argument cancellation callback avoids ambiguous unary object/token lambdas.

```csharp
var load = new AsyncCommand(
    async (parameter, token) => await repository.LoadAsync((Document)parameter!, token),
    parameter => parameter is Document);
var loadButton = new Button { Text = "Load", Command = load, CommandParameter = document };
toolbar.Items.Add(new MenuItem("Load") { Command = load, CommandParameter = document });
```

Create, query, start and cancel on the creating UI thread. `IsExecuting`, `ExecutionTask`,
`CanCancel` and `IsCancellationRequested` are safe to read from other threads. A running invocation
makes CanExecute false for every source sharing the command. Single-flight protects against
double activation and reentrant starts; rejected starts do nothing and do not replace ExecutionTask.
`ExecuteAsync` returns a completed Task for a rejected start, not the already running task.
Use `ExecutionTask` when application code needs to observe the current/latest invocation.

Before the operation starts, IsExecuting becomes true and CanExecuteChanged is raised. On success,
fault or cancellation the slot is released and the event is raised again. There is no polling.
Sources retain their local Enabled=false intent throughout. The sealed helper's internal source
entry point preserves Button's query → DialogResult → Click → fresh query → Execute order.
State changes can legitimately cause extra availability queries in other bound controls.

The original operation Task is observed explicitly, with no async-void supervisor and no discarded
supervisor Task. Two entry points have deliberately different exception ownership:

| Entry point | Completion and exception policy |
| --- | --- |
| `ExecuteAsync(parameter)` | Returns the invocation Task. Await/catch it in application code; no duplicate dispatcher exception. |
| `ICommand.Execute(parameter)` / framework source | Observes execution faults and posts the original exception to the existing UI dispatcher exception path. ExecutionTask retains that outcome. |
| `CanExecute` / initial predicate / thread-affinity validation | Exceptions propagate synchronously. Source predicates retain the existing fail-closed behavior. |
| Cancellation | A cancelled operation produces a cancelled invocation Task, not an unhandled dispatcher error. |

```csharp
// Inside a Task-returning application operation (not a DelegateCommand async lambda):
try { await load.ExecuteAsync(document); }
catch (OperationCanceledException) { /* Application cancellation policy. */ }
catch (IOException) { /* Application recovery policy. */ }
```

CanExecuteChanged and Diagnostic use synchronous event semantics and may originate on a background
completion thread. Framework command sources marshal their guarded refresh through the existing
dispatcher; custom observers must marshal any UI work themselves. Observer failures become execution
failures, with an original operation failure taking precedence. Do not block the UI thread waiting
for a Task. Complete/await or explicitly cancel application-owned work before ending the UI loop if
its UI updates or dispatcher exception delivery must be processed; stopped dispatchers cannot run
queued work. No new SynchronizationContext, scheduler or global execution state is introduced.

### Cooperative cancellation and lifetime

The token overload creates one token source per accepted invocation. `Cancel()` requests cancellation
once and sets IsCancellationRequested; it does not force an operation to stop. CanCancel is false
without a cancellable running invocation or after its first request. State resets when the operation
actually finishes. A callback that ignores its token and completes successfully is still successful.
Cancellation-callback failures propagate to the Cancel caller.

```csharp
var cancel = new DelegateCommand(load.Cancel, () => load.CanCancel);
EventHandler refreshCancel = (_, _) => cancel.RaiseCanExecuteChanged();
load.CanExecuteChanged += refreshCancel;
cancelButton.Command = cancel;
// The application owner must detach refreshCancel when it no longer needs this relationship.
```

Commands are reusable and ownerless. Disposing one Button/item, removing a binding or closing one
window never automatically cancels/disposes a command shared with another source. Disposed, removed,
replaced and closed-window sources ignore stale notifications. The application that owns an operation
decides its cancellation/shutdown policy. ExecutionTask intentionally retains the most recent result
or exception until another invocation starts or the command becomes unreachable.

### Composing routing with asynchronous work

Routing remains synchronous; a routed handler starts a helper and marks the route handled.
There is no separate AsyncRoutedCommand and no asynchronous traversal of a changing control tree.

```csharp
var save = new RoutedCommand("Save");
var saveWork = new AsyncCommand(parameter => repository.SaveAsync((Document)parameter!));
editor.CommandBindings.Add(new CommandBinding(save,
    (_, e) => { saveWork.Execute(e.Parameter); e.Handled = true; },
    (_, e) => e.CanExecute = saveWork.CanExecute(e.Parameter)));
EventHandler refreshRoute = (_, _) => save.RaiseCanExecuteChanged();
saveWork.CanExecuteChanged += refreshRoute;
// Detach refreshRoute when the application registration is released.
```

### Async diagnostics

Subscribe to `AsyncCommand.Diagnostic` for Started, Completed, Faulted and Cancelled outcomes.
`AsyncCommandDiagnosticEventArgs` contains only Kind, ParameterType and ExceptionType; sender is
the command. It never contains the parameter value, user/control/password text, exception object,
exception message, Task or cancellation internals. Do not add those values in application logging.
No event args are allocated when nobody subscribes. The existing input-binding and routed-command
diagnostics described below remain the APIs for lookup, conflicts and route failure.

## Accessibility, testing and samples

An available command-backed Button still exposes semantic Invoke. Unavailable commands map to
the existing disabled/unavailable state and cannot be invoked. Shared accessibility action
handling calls PerformClick, so Windows UIA and Android accessibility consume the same normal
Button/command path. Phase 1 adds no accessibility architecture or agent automation bridge.
This semantic path is the existing extension point for future #97 consumers.

`CommandTests`, `CommandSourceTests`, the command regression in `AccessibilitySemanticTests`, and
`CommandSourceHostTests` cover the public behavior. Host tests explicitly drain the existing
dispatcher; no timing sleeps, GC timing assertions, input simulator or new TestHost API is used.
The existing Windows HWND/UIA integration host also exercises a command-backed Button and a
disabled command source. Android provider tests invoke a real Button through the existing shared
surface/session adapter. These automated checks do not claim manual TalkBack or visual validation.

Run `dotnet run --project samples/ControlGallery/ControlGallery.csproj` and open **Button**. The
small command section has two buttons sharing one command, separate document parameters, a shared
availability checkbox and an explicit-disable checkbox for the first button. Check enabled,
hover, pressed and keyboard-focus rendering in the existing light/dark themes. This example does
not modify DemoApp or the generated template experience.

## Keyboard gestures and input bindings (Phase 2)

`KeyGesture` is an immutable value containing the existing framework `Keys` key and
`ModernFormsNext.WindowKit.Input.KeyModifiers` flags. Equality, hash codes and `Matches(KeyEventArgs)`
compare the key and exact modifiers. Ctrl+S and Ctrl+Shift+S are distinct. Letters, digits, function
and navigation keys are supported by the shared model. `ToString()` is for debugging (for example,
`Ctrl+Shift+S`), not localization or parsing. There is no parser or chord sequence support.

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;

var save = new DelegateCommand(
    parameter => SaveDocument((string)parameter!),
    parameter => CanSave && parameter is string);

form.InputBindings.Add(new KeyBinding(save, new KeyGesture(Keys.S, KeyModifiers.Control)) {
    CommandParameter = "current document"
});
editor.InputBindings.Add(new KeyBinding(save, new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Shift)) {
    CommandParameter = "selected document"
});
var refresh = new KeyBinding(new DelegateCommand(Refresh), new KeyGesture(Keys.F5));
Application.InputBindings.Add(refresh);
// Remove global bindings that capture short-lived windows before those windows close.
Application.InputBindings.Remove(refresh);
```

`InputBinding` is the abstract shared model; `KeyBinding` is its concrete keyboard binding. Each
holds a nullable `ICommand`, nullable `CommandParameter` and mutable `KeyGesture`. Parameter objects
are passed unchanged. A missing command or `default(KeyGesture)` is an inactive registration.
The gesture constructor rejects unknown/modifier-only keys, embedded modifier flags, AltGraph
shortcuts, and unmodified/Shift-only printable keys. Configure Enter/navigation/function keys
carefully: an available binding intentionally takes precedence over the control's default action.

### Lookup order and conflicts

The existing window KeyDown event keeps first refusal. If it sets Handled, no binding lookup or
control dispatch occurs. Otherwise lookup runs before ordinary control KeyDown, in this order:

1. Focused control's `InputBindings`.
2. Its nearest ancestor, then each remaining ancestor up to the surface root.
3. Owning `WindowBase.InputBindings` (including Form).
4. `Application.InputBindings`.

With no focused control, lookup starts at the root and still checks window/application scopes.
Stale focus references to controls detached or moved into another tree are treated as no focus;
detaching a candidate's scope during CanExecute also invalidates that evaluation.
Standalone `SkiaControlSurface` uses the same rules without a window scope. Inactive, hidden or
disposed control scopes are skipped. Application bindings apply only to input delivered to a
framework window/surface; they are not OS-global hotkeys.

Within a collection, the **first added available match** wins. Duplicates are allowed. A null
command, invalid gesture or `CanExecute == false` allows later registrations and outer scopes
to provide a fallback. If nothing executes, a fresh key press remains available to normal input.
This is binding lookup through control ancestry, **not hierarchical command routing**. Every
binding directly names its ICommand. Phase 3 performs a separate handler-routing stage only when
that command is a RoutedCommand; ordinary commands keep their direct execution behavior.

### Execution, consumption and mutation

On KeyDown: match gesture, read the current command/parameter, evaluate CanExecute and execute.
An unchanged DelegateCommand gets one predicate check; its existing internal action entry point
avoids a redundant check. Other ICommand implementations receive ordinary Execute and can perform
their own checks. Keyboard execution does not synthesize Button.Click. Assign the same command
to a Button to share the domain action with pointer, normal button keyboard and accessibility Invoke.
Button's Phase 1 guard/CanExecute/DialogResult/Click/fresh-CanExecute/Execute sequence is unchanged.

A successful binding sets `SuppressKeyPress` (also Handled) before calling application code. On
Windows the managed Handled flag now reaches the original raw event, allowing the existing backend
to suppress resulting text. Its corresponding KeyUp is also consumed so changing focus to another
Button cannot activate that button on release. Window KeyUp observers still see the event; clearing
Handled there cannot forward an already consumed release to a control. KeyUp never executes a binding.
Windows character suppression is reset for every new native KeyDown, including unmapped Unicode
packet keys, so handling an earlier shortcut/editing key cannot discard a later text input sequence.

Every delivered repeated KeyDown reevaluates current bindings and can execute once. Once a press
executes, remaining repeats/release stay consumed even if the command becomes unavailable; available
fallback bindings can still execute on later repeats. Native window deactivation/closure and surface
disposal clear remembered presses. AltGraph input always bypasses bindings, including suppression
left by an earlier shortcut with different modifiers.
Repeated KeyDown events are separate command invocations, useful for navigation or volume changes.
Actions such as Save should tolerate repetition or use CanExecute to reject input while unavailable;
Phase 2 does not add per-gesture repeat flags or an execution lock.

Matching registrations are snapshotted before application predicates run. Adding/removing bindings
or changing focus during Execute cannot restart the current activation or execute another binding.
Predicates must be fast and free of side effects. If a candidate's command/parameter/gesture,
collection membership/version or lifetime changes during its predicate, the obsolete evaluation
is abandoned for that event. Subsequent presses use the new state. Predicate/Execute exceptions
propagate unchanged; an Execute exception still leaves the press consumed. No polling or
CanExecuteChanged subscription is needed for input bindings: availability is checked on input.

### Modifiers, text and platforms

The existing KeyModifiers model supplies Control, Shift, Alt, Meta and AltGraph. Phase 2 adds only
the missing `Keys.Meta` flag and raw Meta mapping; previous enum values remain unchanged. Meta
means the backend's platform modifier, not an automatic Ctrl/Command-key alias.

Windows already marks physical right Alt as AltGraph, even when Control+Alt are also present.
**AltGraph never matches a gesture**, so Ctrl+Alt bindings do not steal Polish AltGr combinations.
Plain and Shift-only printable gestures are rejected. Unbound editing keys/shortcuts and text
commit/composition paths retain their existing behavior. A true Ctrl+Alt event without AltGraph
can match. No new keyboard-layout detection, dead-key decoder, IME subsystem or #62 work is added.
Tests of committed accented text do not establish native keyboard-layout or dead-key-device coverage.

Windows uses the existing raw-key → WindowBase → control pipeline, without hooks or RegisterHotKey.
Android preserves modifiers and source metadata on its existing `AndroidInputKeyEvent`. Only
physical-device view events are eligible; InputConnection, soft-keyboard and virtual-device
transitions stay editing input. Hosts forward `isTextInput: !e.IsHardwareKey` to the surface key
overloads and include the event's modifiers. The cross-platform sample demonstrates this adapter.
The original two-argument Android event constructor retains editing defaults and deconstruction.

Android's native adapter currently forwards only Backspace/Delete/Enter/arrows. Letters, digits
and function keys are supported by KeyGesture but **not forwarded by that adapter yet**. There is
no claim of Windows/Android shortcut parity or physical Android-device verification. Right Alt is
conservatively marked AltGraph on Android as well. Existing software text editing does not become
a shortcut stream. TestHost gains no keyboard simulation API.

### Ownership, threading, diagnostics and Designer

Create bindings and first access scope collections on the owning UI thread, following Phase 1's
command-source affinity policy. Collection mutation and binding property setters reject a different
thread with InvalidOperationException; they do not dispatch automatically. Lookup/execution also
runs on the UI thread. There is no background-safe collection or new synchronization policy.

One binding instance can belong to one collection at a time; duplicate registration of that same
object throws. Separate objects may share gestures, commands and parameters. Removing, replacing
or clearing a registration releases collection ownership, and the detached binding can be reused.
The binding itself retains its assigned command/parameter until changed or no longer referenced.
Disposing a control, actually closing/disposing a window, or application shutdown clears owned
collections and diagnostic subscribers. Cancelled window closure preserves them. Commands and
parameters are application-owned and are never disposed by the collection. Application bindings
retain their captures until removal/shutdown; remove short-lived captures explicitly. There are
no static control dictionaries, per-frame scans or allocations for unmatched key lookup.
The existing Application.Run contract still permits only one main loop per process. Phase 2 does
not add application restart support: shutdown clears the global collection, and a new process
starts without old registrations. Tests use the same internal cleanup path for isolated collections.

Subscribe optionally to a collection's `Diagnostic` event for InvalidBinding, DuplicateGesture,
CommandUnavailable and Executed outcomes. Observations are synchronous, allocate event args only
when subscribed, and do not format parameter contents. Observers must not mutate input state;
their exceptions propagate. These binding diagnostics are separate from the Phase 3 route
diagnostics below. InputBindings changes do not trigger layout/rendering by themselves. Public InputBindings
properties are hidden from Designer browsing/serialization and remain runtime-only.
This public observer API lets applications explain conflicting registrations and unavailable
shortcuts using their own logging. It exposes the binding and outcome, not resolver snapshots,
scope traversal state or command-route details.

The existing ControlGallery **Button** section now supports Ctrl+1 / Ctrl+2 with the same save
command and parameters as its buttons. With focus in the section, the panel's Ctrl+1 saves first
and Ctrl+2 saves second. Focus **Save second**: its local Ctrl+1 overrides the panel and saves second.
Uncheck **Allow shared command** to disable both command sources and shortcuts. **Explicitly disable
first** affects that Button's local enabled intent; it does not change availability of the shared
command for another source. `KeyGestureTests`, `InputBindingTests` and `AndroidKeyboardBindingTests`
cover matching, lookup, input integration, mutation, lifecycle and source classification.

## Routed commands and command bindings (Phase 3)

Use `RoutedCommand` when an action's implementation depends on the target's location in the UI.
It implements `System.Windows.Input.ICommand`, has an immutable optional `Name`, and compares by
reference identity. Two commands called `"Save"` are different actions. It captures no target,
registers no global ID and introduces no dependency on WPF or a backend-specific type.

`InputBinding` answers **which command this gesture selects**. `CommandBinding` answers **which
handler on this target's route can execute that command**. A DelegateCommand or custom ICommand
never enters the handler resolver, even when its source has a CommandTarget assigned.

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;

var save = new RoutedCommand("Save");
bool canSave = true;

form.CommandBindings.Add(new CommandBinding(save,
    executed: (_, e) => {
        SaveDocument(e.Parameter);
        e.Handled = true;
    },
    canExecute: (_, e) => e.CanExecute = canSave));

var editor = form.Controls.Add(new TextBox());
var saveButton = form.Controls.Add(new Button {
    Text = "Save", Command = save, CommandTarget = editor, CommandParameter = document
});
form.InputBindings.Add(new KeyBinding(save, new KeyGesture(Keys.S, KeyModifiers.Control)) {
    CommandTarget = editor, CommandParameter = document
});

canSave = false;
save.RaiseCanExecuteChanged(); // Normal Button enabled/accessibility update.
```

CommandBinding's `Command`, `Executed` and optional `CanExecute` delegates are immutable. Replace
the registration to change handlers. An absent CanExecute handler is **unavailable**, not an
implicit permission to execute. All handlers are synchronous and receive the visited owner as
sender. For the application terminal, sender is `typeof(Application)`.

### Target resolution and boundaries

| Entry | Target order | Boundary |
| --- | --- | --- |
| Button | Explicit CommandTarget, otherwise the source Button itself | Same window or standalone surface as the Button |
| KeyBinding | Explicit CommandTarget, otherwise valid focus, binding control scope, then keyboard entry root | Existing input root; foreign/stale focus is ignored |
| Direct target overload | Supplied Control | The target's own attached tree |
| Parameter-only ICommand | No target/context; CanExecute false, Execute no-op | No global focus guessing |

An invalid explicit target fails closed; it does not fall back to focus/source. Keyboard fallback
ignores disposed controls as implicit focus, even if native focus bookkeeping still retains their
old Parent reference. Button target
resolution deliberately does not follow focus: pointer activation can change focus, while a Save
button often acts on a separate editor. Set CommandTarget explicitly for that editor. A Button's
default route remains stable when some other control or window has focus.

Use `save.CanExecute(parameter, target)` and `save.Execute(parameter, target)` for direct calls.
The latter performs its own fresh query. `Control` is the target contract; there is no new public
ICommandTarget abstraction. Targets must reach an existing native window adapter or standalone
SkiaControlSurface root. Detached/disposed targets and closed windows are unavailable. A caller
cannot use a detached Control merely to reach application fallback handlers.

Routing itself resolves application availability; it does not select a control or synthesize input.
Normal Button and keyboard-source input guards continue to enforce their effective enabled/input
state. Direct programmatic target calls require an attached live target and use the handlers'
CanExecute policy; they do not derive application permission from the target's cached command-enabled
flag. This also allows a disabled command source to recover after a successful requery.

The parent chain ends at its current window. Form.Owner and popup ownership do not cross that
boundary. A standalone surface uses its borrowed root and has no WindowBase scope. Non-Control
semantic accessibility nodes are not command targets or additional route nodes in this phase.

### Route, CanExecute and Handled

The resolver snapshots the current `target → Parent → ...` chain, followed by the owning window
(if present) and `Application.CommandBindings` as a terminal fallback. The internal native root
adapter is represented by the window; standalone surface infrastructure adds no handler scope.
Every matching registration is captured in insertion order before invoking application code.
No second tree, cached route graph, reflection lookup or static control dictionary is used.

Only target-to-root traversal is implemented. There is no preview/tunnel command stage: the
existing Window.KeyDown preview already gives input handlers first refusal, and Phase 3 does not
introduce a general routed-event framework.

Each binding gets fresh CanExecuteCommandEventArgs with Command, Parameter, Target, Source,
CanExecute=false and Handled=false:

| Query result | Effect |
| --- | --- |
| CanExecute=true | Select the first available binding |
| CanExecute=false, Handled=false | Continue to the next registration/scope |
| CanExecute=false, Handled=true | Veto the rest of this route |
| No available registration | Command unavailable |

Execution starts at that selected binding. ExecutedCommandEventArgs carries Command, Parameter,
Target, Source and a fresh Handled=false. **Set Handled=true after handling an action** to stop.
If left false, traversal continues; each later binding must pass its own CanExecute query before
its execution handler runs. A query's Handled value does not implicitly handle execution. This
permits duplicate registrations and explicit continuation without invoking unavailable handlers.

```csharp
editor.CommandBindings.Add(new CommandBinding(save,
    (_, e) => { SaveEditorDocument(e.Parameter); e.Handled = true; },
    (_, e) => e.CanExecute = editorHasDocument));
// If editorHasDocument is false and the query is unhandled, the window handler can take over.
```

### Mutation, lifecycle, threading and exceptions

Query and execution within one invocation share the captured nodes and registrations. Reparenting,
removing a control, changing focus, adding/removing bindings or replacing a registration does not
rebuild that route midway. Those changes affect the next invocation. Disposal of the source, target
or **any captured owner** stops the current traversal before another query/action callback, even
when that owner was already visited or the live target has moved elsewhere. Closing the captured
window or application shutdown also stops traversal. A close/dispose during CanExecute prevents
the selected action from running; removing/reparenting alone does not. Diagnostic completion events
may still describe the handler that just returned. Nested
commands, including recursive calls to the same command, each own a separate invocation context.
There is no shared mutable cursor or global reentrancy lock.

Create RoutedCommand and CommandBinding on the UI thread. Route calls and collection mutations
reject a different thread. RaiseCanExecuteChanged uses the existing Phase 1 notification policy:
framework sources marshal background notifications through the application dispatcher. No new
synchronization or scheduler is added. Predicates should be fast and free of side effects.
Requery caused by collection mutation inside the same source's query does not recursively replace
its in-progress snapshot; application state changes should be followed by explicit requery.

Adding/removing/replacing/clearing CommandBindings requeries affected commands. Attaching or
reparenting a source subtree refreshes routed Button availability. Other application state changes
require `RaiseCanExecuteChanged`; there is no focus polling or command registry. Ordinary ICommand
evaluation counts, Button Click ordering, local Enabled intent and NotifyIconMenuItem behavior
remain unchanged. Button accessibility Invoke follows normal Button activation and command execution;
CanExecute=false updates effective Enabled and accessibility availability through that same source.

A CommandBinding has at most one collection owner. Removal releases ownership and permits re-add
or transfer. Clearing collections does not dispose commands/delegate captures. Control disposal,
actual window close/disposal and application shutdown release registrations; cancelled close
preserves them. Application registrations retain captures until removed/shutdown: explicitly remove
short-lived captures. Shutdown cleanup does not add support for a second Application.Run loop.

Original CanExecute and Executed exceptions propagate unchanged. Each invocation's state is local,
so an exception cannot corrupt a later route. Button retains Phase 1's fail-closed availability and
recovery behavior. There are no async handlers, execution-state flags or cancellation helpers.

### Opt-in route diagnostics

Subscribe to `RoutedCommand.Diagnostic` to observe NodeVisited, BindingFound, CanExecuteEvaluated,
Executed, Handled and Failed transitions. This per-command public event supports application logging
and future tools without a Developer Tools UI. Owner identifies a Control, WindowBase or
`typeof(Application)` terminal. Target and Binding are identities; CanExecute records query outcomes.
FailureReason contains only framework-defined text. The internal route snapshot is not exposed.

```csharp
save.Diagnostic += (_, e) => {
    // No parameter value or control text is formatted here.
    Log($"{e.Kind}; owner type={e.Owner?.GetType().Name}; parameter type={e.ParameterType?.FullName}");
};
```

Diagnostics never call parameter.ToString, copy control text/passwords, or include exception
messages. ParameterType exposes only the CLR type. Do not retain owner/binding references longer
than necessary. Without observers, no diagnostic event args are allocated. Observers execute
synchronously on the UI thread and should not mutate UI state. Observer exceptions propagate,
except that an original command-handler exception wins over a failure-reporting observer exception.

The ControlGallery **Command routing** page shares one RoutedCommand between two Buttons and Ctrl+S.
**Save locally** has a local override; **Save via window** reaches a real window CommandBinding.
The status shows the executed owner and count. **Allow routed Save** updates both sources and
keyboard availability. Unloading the page removes its window registration and short-lived captures.
Runtime CommandBindings and Button.CommandTarget are hidden from Designer browsing/serialization.

The routing regressions cover full owner traversal, lifetime aborts after reparenting, nested queries,
recursive execution, diagnostic reentrancy and inner exceptions. Dedicated Windows UIA provider and
Android accessibility session tests invoke a real routed Button through the canonical semantic path,
verify query/Click/query/execution ordering, and reject activation after availability becomes false.
These provider tests do not establish physical-device or screen-reader coverage.

## Menu and other action controls

`MenuItem.Command`, `CommandParameter` and `CommandTarget` reuse Button's command source helper.
Menu, ToolBar and ContextMenu already share this item model. Existing Ribbon items inherit that
behavior too; Ribbon remains an incomplete control and this work does not expand its feature set.
Separators never activate. A derived MenuItem must call base.OnClick to preserve the command path.

| Source | Default routed target/source context | Explicit target |
| --- | --- | --- |
| Button | The Button | Must belong to the same source tree |
| Menu / ToolBar / existing Ribbon item | The logical owning control, including for nested submenu items | Must belong to the same logical owner's tree |
| ContextMenu / standalone MenuDropDown | Control passed to Show | Must belong to that origin's tree |
| NotifyIconMenuItem | None; no ambient focus/window lookup | Required for RoutedCommand; routes within the target's attached tree |

Ordinary ICommand, DelegateCommand and AsyncCommand ignore CommandTarget completely. It never
changes their parameter or availability. Routed event args expose the logical Control as Source;
a tray item's Source is null. Internal lifetime guards also track the non-Control action item.

```csharp
menu.Items.Add(new MenuItem("Save") {
    Command = save, CommandParameter = document, CommandTarget = editor
});
contextMenu.Items.Add(new MenuItem("Save here") { Command = save, CommandParameter = document });
contextMenu.Show(editor, editor.PointToScreen(new System.Drawing.Point(0, editor.Height)));
var traySave = new NotifyIconMenuItem("Save") {
    Command = save, CommandParameter = document, CommandTarget = editor
};
```

Physical popup parenting does not change a menu's logical route and routing never follows Form.Owner.
ContextMenu.Show captures its origin and refreshes availability. Existing native popup ownership
still belongs to the first Form: use a separate context menu instance per Form. Reusing that popup
with a foreign Form fails closed for routed commands; it does not move the popup or create a route
across windows. Reopening within the same Form can change the origin. No popup/window lifetime
redesign or tray native snapshot ownership change is included.

Hiding a context menu releases its captured origin and suspends command subscriptions until the
next Show. During pointer activation, the popup still closes before Click; the origin survives
only through that synchronous Click/command call and is released in finally, including for nested
submenu actions. Disposal releases it immediately after owner cleanup. A menu/toolbar disposed by
its own action cannot re-register itself as the active menu when that action returns.

An unattached MenuItem does not query/subscribe to its command until inserted. Removing an item
suspends its subscription and permits reuse. Disposing an item releases its command/parameter/target
and owned child bindings; owning menu/toolbar disposal does the same. A submenu popup only borrows
its parent's items. Caller-owned images and command objects are not disposed. Removing an entire
experimental Ribbon group/page retains the existing Ribbon ownership rules; explicitly dispose
removed item instances when retiring them. General ownership redesign remains #63.

Existing menu semantic Invoke uses this same guarded action path. Async availability changes update
the existing unavailable state and preserve accessible-object identity; no second semantic tree or
new platform provider is involved.

## Designer boundary

Command, CommandParameter and CommandTarget on action sources, and Control/Window InputBindings and
CommandBindings, have Browsable(false) and DesignerSerializationVisibility.Hidden metadata.
The framework metadata reader marks them hidden/nonserializable and the PropertyGrid omits them.
Runtime delegates, arbitrary object parameters, target references and handler collections do not
have a declarative .mfdesign representation. Assign them in code-behind after InitializeComponent,
or through the existing runtime binding path. Do not put them into generated InitializeComponent.

No new command editor/markup is needed to open command-capable controls, save/reopen a document,
copy/paste, undo/redo, generate code or reverse-parse generated code. These operations use the design
model and never execute runtime commands. `CommandDesignerTests` covers the metadata and each of
those document paths. Discoverable declarative command editing remains future tooling, as allowed
by #56's longer-term Designer direction. Visual Studio Ctrl+S is outside this command work.
Safe declarative editing is tracked by [#108](https://github.com/ProGraMajster/ModernFormsNext/issues/108).

## Final showcase and validation scope

The **Command routing** gallery page also includes a toolbar action with an explicit local target,
a DelegateCommand reset action, a context menu, and a cancellable AsyncCommand shared by Button and
toolbar. Run async task shows busy/disabled state, completion and cancellation; both sources recover.
The sample delay is illustrative only. Unloading explicitly cancels the page-owned demo operation,
detaches custom observers and removes its window registration.

`AsyncCommandHostTests`, `ActionCommandHostTests`, `ActionCommandPopupTests` and `CommandDesignerTests`
cover deterministic completion/fault/cancellation, shared sources, disposal/replacement/actual
window close, routing, popup origins, accessibility and Designer boundaries. They use manually
completed Tasks and existing dispatcher/window substitutes, without sleeps or new TestHost APIs.
Native ControlGallery and Designer smoke evidence is recorded separately from headless tests.

The existing Android hardware-key forwarding subset, physical-device parity and advanced IME
limitations remain as documented in the keyboard section. There is no BindingNavigator port,
Developer Tools UI, automation bridge, new platform, release or version bump here. See the
[Phase 1 audit](commands-phase1-audit.md), [Phase 2 audit](commands-phase2-audit.md),
[Phase 3 audit](commands-phase3-audit.md) and [Phase 4 audit](commands-phase4-audit.md).
Android hardware shortcut forwarding/modifier parity is tracked by
[#109](https://github.com/ProGraMajster/ModernFormsNext/issues/109); composition and device evidence
remain under [#62](https://github.com/ProGraMajster/ModernFormsNext/issues/62) and
[#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69).
