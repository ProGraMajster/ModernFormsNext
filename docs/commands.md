# Commands and input bindings (issue #56, Phases 1 and 2)

Commands represent reusable application actions. `System.Windows.Input.ICommand` is the contract:
existing application implementations can be assigned directly. `ModernFormsNext.DelegateCommand`
is the optional synchronous implementation provided by the framework. Events remain supported;
an application can use `button.Click` without creating any command.

Phase 1 provides delegate commands, parameters, availability and two action sources: `Button`
and `NotifyIconMenuItem`. Phase 2 adds keyboard gestures and scoped input bindings to concrete
commands. Issue #56 remains open: routed commands, async helpers and Designer integration are
**not implemented**.

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

Execute and Click exceptions propagate unchanged through the calling action path. CanExecute
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
would be async void. Task-aware commands, execution state, cancellation and async exception
helpers belong to Phase 4. Existing application ICommand implementations can start tracked work,
update their own availability and publish CanExecuteChanged; this foundation adds no execution
lock, global execution state or dispatcher subsystem that such implementations must adopt.

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
Standalone `SkiaControlSurface` uses the same rules without a window scope. Inactive, hidden or
disposed control scopes are skipped. Application bindings apply only to input delivered to a
framework window/surface; they are not OS-global hotkeys.

Within a collection, the **first added available match** wins. Duplicates are allowed. A null
command, invalid gesture or `CanExecute == false` allows later registrations and outer scopes
to provide a fallback. If nothing executes, a fresh key press remains available to normal input.
This is binding lookup through control ancestry, **not hierarchical command routing**. Every
binding directly names its ICommand; there is no CommandTarget, CommandBinding or handler route.

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

Every delivered repeated KeyDown reevaluates current bindings and can execute once. Once a press
executes, remaining repeats/release stay consumed even if the command becomes unavailable; available
fallback bindings can still execute on later repeats. Native window deactivation/closure and surface
disposal clear remembered presses. AltGraph input always bypasses bindings, including suppression
left by an earlier shortcut with different modifiers.

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

Subscribe optionally to a collection's `Diagnostic` event for InvalidBinding, DuplicateGesture,
CommandUnavailable and Executed outcomes. Observations are synchronous, allocate event args only
when subscribed, and do not format parameter contents. Observers must not mutate input state;
their exceptions propagate. This is minimal binding diagnostics; command-route diagnostics remain
Phase 3. Collection changes do not trigger layout/rendering by themselves. Public InputBindings
properties are hidden from Designer browsing/serialization and remain runtime-only.

The existing ControlGallery **Button** section now supports Ctrl+1 / Ctrl+2 with the same save
command and parameters as its buttons. With focus in the section, the panel's Ctrl+1 saves first
and Ctrl+2 saves second. Focus **Save second**: its local Ctrl+1 overrides the panel and saves second.
Uncheck **Allow shared command** to disable both command sources and shortcuts. **Explicitly disable
first** affects that Button's local enabled intent; it does not change availability of the shared
command for another source. `KeyGestureTests`, `InputBindingTests` and `AndroidKeyboardBindingTests`
cover matching, lookup, input integration, mutation, lifecycle and source classification.

## Deferred work

- Phase 3: command targets, hierarchical routing, CommandBinding and diagnostics.
- Phase 4: task-aware async helpers, deeper menu/toolbar/context-menu integration, Designer
  assignment/serialization, and expanded examples/documentation.

MenuItem ownership/lifecycle requires separate work before safe event subscriptions can be added
across Menu, ToolBar, Ribbon and ContextMenu. Command properties on the Phase 1 sources are hidden
from design-time browsing/serialization. There is no Designer Ctrl+S fix, BindingNavigator port,
Developer Tools integration, automation bridge, release or version bump in this phase. See the
[Phase 1 audit](commands-phase1-audit.md) and [Phase 2 keyboard audit](commands-phase2-audit.md).
