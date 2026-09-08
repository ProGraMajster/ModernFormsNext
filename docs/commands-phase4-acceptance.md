# Issue #56 final command-system acceptance audit

The full live issue body and Phase 1–3 completion comments were reread on 2026-09-08, after the
Phase 4 implementation. Base master: `59f274556d768d96bac67db9f335f6ff5f89092e`.
This audit assesses the original issue, including its explicitly conceptual/optional API direction
and longer-term Designer wording. It does not interpret every suggested type name as a required API.

The tables describe implemented contracts and their regression evidence. Final exact-HEAD build,
test, ApiCompat, package, documentation and native smoke outcomes belong to the local review report
and `artifacts/issue-56-phase4` validation records. They are separate from implementation claims.

## Phase 4 matrix

| Requirement | Status | Evidence |
| --- | --- | --- |
| One minimal async helper | Implemented | AsyncCommand implements ICommand; parameterless, parameter and cancellable callbacks; ExecuteAsync/ExecutionTask; XML and commands guide. |
| Execution state / single-flight | Implemented | AsyncCommandHostTests: busy slot published before callbacks, reentrant/competing starts rejected, shared sources recover. |
| Cancellation | Implemented | Fresh per-invocation token, cooperative Cancel, CanCancel/IsCancellationRequested; inline completion and callback-failure tests. |
| Exceptions / no async void | Implemented | Original Task observed; ExecuteAsync faults to caller, Execute faults to existing dispatcher; original failure identity and observer failures tested. |
| Action-control integration | Implemented | MenuItem uses CommandSource across Menu, ToolBar, ContextMenu and existing Ribbon items; NotifyIconMenuItem explicit routed target. |
| Click / target / lifecycle compatibility | Implemented | ActionCommandHostTests and ActionCommandPopupTests cover fresh binding checks, parameters, ignored ordinary targets, logical origins, source disposal and actual Form.Close. |
| Routed async use | Implemented by composition | Synchronous CommandBinding starts AsyncCommand; availability forwarding tested and documented; no new route subsystem. |
| Designer | Runtime-only support implemented | CommandDesignerTests: 13 metadata cases, three PropertyGrid/codegen/parser round trips, clipboard undo/redo and inert runtime-assignment boundary. |
| Deterministic tests | Implemented | TaskCompletionSource/manual thread completion and existing dispatcher; no sleep-based async tests or added TestHost API. |
| Accessibility | Existing contracts reused | Button/menu semantic Invoke, disabled/recovery and identity tests; existing Windows UIA/Android provider regressions retained. |
| Diagnostics / privacy | Implemented | Async started/completed/faulted/cancelled, parameter/exception types only; no values, text or exception messages. |
| Docs / sample | Implemented | commands.md, XML APIs, audit; modest existing gallery extension showing delegate/async/routed/keyboard/toolbar/context actions. |

## Full original issue matrix

| Acceptance criterion / issue capability | Status | Evidence | Remaining gap |
| --- | --- | --- | --- |
| One command reused by multiple controls | Met | Shared DelegateCommand/AsyncCommand Button, menu/toolbar and tray tests; gallery. | None for core #56. |
| Automatic CanExecute/Enabled | Met | Shared CommandSource preserves local Enabled intent; background requery marshalled; async busy/recovery and accessible unavailable state. | None for core #56. |
| Keyboard invokes the same command as visual controls | Met | Phase 2 shared resolver; gallery Ctrl+S/Ctrl+1/Ctrl+2; async keyboard and parameter regression. | Android native forwarding remains an explicit backend subset (#109). |
| Deterministic routing through nested/focused controls | Met | Phase 3 target → ancestor → Window → Application snapshots; target/focus mutation, reentrancy, disposed/closed route tests. | No tunnel/chord requirement in the initial issue. |
| Conflicting shortcut precedence documented | Met | Nearest scope, first-added available match, unavailable fallback; KeyGesture/InputBinding/Windows/Android mapping tests and commands.md. | None for supported key events. |
| Normal events remain supported | Met | Event-only use; Button query → DialogResult → Click → fresh query → Execute; menu/tray equivalent without DialogResult. | Applications must not duplicate the same action in Click and Command. |
| Safe supported async pattern | Met | One Task-based AsyncCommand; direct awaiting or supervised dispatcher failures; no async-void command helper. | Application must coordinate work before ending its UI loop. |
| ICommand / command parameters / reusable instances | Met | System.Windows.Input.ICommand, DelegateCommand, AsyncCommand, unchanged parameter references and shared lifetimes. | None. |
| Execution state / temporary unavailability | Met | IsExecuting, single-flight, explicit notifications, multi-source busy tests. | Concurrent-execution policy intentionally not added. |
| Cancellation readiness | Met | Optional parameter/token callback, fresh token source, Cancel and cancellation state; success/fault/cancel recovery. | Advanced cancellation policies are not a core requirement. |
| Local/window/application input and command scopes | Met | Existing Phase 2/3 collections, registrations, cleanup and hierarchy tests. | Android has a standalone surface context until general host #72. |
| Explicit targets | Met | Button/InputBinding/MenuItem same-tree targets; tray explicit-only target; ordinary ICommand ignores targets. | No ambient target for nonvisual tray sources by design. |
| Requery / invalidation | Met | CanExecuteChanged, explicit RaiseCanExecuteChanged, source subscription/version guards; no polling manager required. | A conceptual CommandManager type is unnecessary. |
| Modifiers, common shortcuts and function keys | Met in shared model / Windows-primary runtime | KeyGestureTests, InputBindingTests and WindowsKeyboardInputTests; exact modifier matching, logical key model. | Android hardware parity #109 and layout/device evidence remain explicit. |
| Disabled commands do not unexpectedly consume shortcuts | Met | Existing availability fallback and text/AltGraph safety tests; busy AsyncCommand uses the same lookup guard. | IME expansion remains #62. |
| Menu / context / toolbar evaluation and integration | Met | Shared MenuItem model, logical parent context and captured Show origin; real Show exercised with existing window proxy and native gallery smoke. | Existing popup owner is not moved across Forms; use one ContextMenu per Form. |
| AppShell/navigation and future docking evaluated where appropriate | Evaluated; future work retained | NavigationPaneItem is a selection model, not a Click action; AppShell and DockWorkspace have no current action API to adapt. | #12 and #21 own their future actions; no speculative controls added. |
| Existing Ribbon actions | Inherit shared item support | Existing MenuItem instances use the helper; execution, target and owner-disposal tests. | Ribbon remains incomplete; its group/page ownership model is unchanged. |
| Longer-term Designer direction | Safe initial boundary met | Hidden/nonserializable runtime properties; code-behind assignment; no design-time execution; model operations and native Designer smoke. | Declarative discovery/editing/serialization tracked by #108; not required by initial runtime acceptance. |
| Missing handler / invalid target / disabled diagnostics | Met | Existing InputBinding and RoutedCommand diagnostic outcomes; route failures use stable explanations. | Developer Tools UI is separately #61. |
| Execution exception diagnostics | Met | Sync route failure path and AsyncCommand typed outcomes plus documented original exception propagation. | Application logs must preserve the documented privacy policy. |
| Future inspection without implementation leaks | Met | Existing public binding collections and opt-in diagnostics; no exported resolver/scheduler snapshots or platform types. | Inspector UI remains #61. |
| Templates, platform boundaries, dependencies and versioning | Preserved | Platform-neutral shared implementation, no dependency/version/template changes. | Native Android parity and VS Experimental Instance evidence are not inferred from builds. |

## Follow-ups and review boundary

- [#108 — safe declarative Designer command discovery and editing](https://github.com/ProGraMajster/ModernFormsNext/issues/108).
- [#109 — Android hardware shortcut forwarding and modifier parity](https://github.com/ProGraMajster/ModernFormsNext/issues/109).
- Existing #12/#21/#61/#62/#63/#69/#72/#85/#97/#99 keep their own scope. No additional cancellation
  policy or generic AsyncCommand type was justified by a concrete requirement.

The implemented scope covers the original core acceptance direction. Final READY TO CLOSE versus
KEEP OPEN is gated by the exact-HEAD validation report and review, not merely by this Phase 4 table.
Publication and issue closure require the separately authorized finalization gate: a final API and
lifecycle review, exact-HEAD validation, green PR checks, normal merge and post-merge verification.
No release, tag or version change is part of command-system completion.

The final lifecycle review additionally covers hidden context-origin release, nested activation
across popup hiding and menu/toolbar disposal inside their own actions. Async regressions cover
both reentrant entry points, notification-driven predicate mutation, starting a later invocation
from completion, independent operation/cancellation failures, two shared Buttons during disposal,
and parameters whose ToString must never be called by diagnostics.

The original issue explicitly describes Designer assignment/serialization tooling as longer-term
and says the initial runtime must not depend on it. #108 therefore does not block core completion.
The shared modifier/key model and Windows runtime meet the shortcut criteria; the issue does not
require full Android hardware parity. #109 remains a documented backend enhancement, not an
unimplemented shared command criterion. Neither follow-up justifies claiming untested device parity.
