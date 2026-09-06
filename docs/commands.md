# Commands (issue #56, Phase 1)

Commands represent reusable application actions. `System.Windows.Input.ICommand` is the contract:
existing application implementations can be assigned directly. `ModernFormsNext.DelegateCommand`
is the optional synchronous implementation provided by the framework. Events remain supported;
an application can use `button.Click` without creating any command.

This phase provides delegate commands, parameters, availability and two action sources: `Button`
and `NotifyIconMenuItem`. Issue #56 remains open. Keyboard gestures, routed commands, scopes,
async helpers and Designer integration are **not implemented by this phase**.

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

## Deferred work

- Phase 2: KeyGesture, KeyBinding, InputBinding, shortcut precedence and window/application scopes.
- Phase 3: command targets, hierarchical routing, CommandBinding and diagnostics.
- Phase 4: task-aware async helpers, deeper menu/toolbar/context-menu integration, Designer
  assignment/serialization, and expanded examples/documentation.

MenuItem ownership/lifecycle requires separate work before safe event subscriptions can be added
across Menu, ToolBar, Ribbon and ContextMenu. Command properties on the Phase 1 sources are hidden
from design-time browsing/serialization. There is no Designer Ctrl+S fix, BindingNavigator port,
Developer Tools integration, automation bridge, new keyboard shortcut system, release or version
bump in this phase. See the [baseline audit](commands-phase1-audit.md).
