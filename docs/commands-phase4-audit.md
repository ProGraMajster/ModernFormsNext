# Command integration audit — issue #56 Phase 4

Baseline: `59f274556d768d96bac67db9f335f6ff5f89092e` (Phases 1–3 merged).
This audit records the existing implementation before Phase 4 code changes.

| Area | Current state | Missing for final #56 | Proposed change |
| --- | --- | --- | --- |
| Async helper | ICommand/DelegateCommand are synchronous. | A supported Task-aware action. | One sealed AsyncCommand, parameterless/parameter-aware delegates and ExecuteAsync. No AsyncCommand<T> or async void. |
| Execution state | Sources already cache CanExecute and preserve local Enabled. | Busy-state availability and deterministic recovery. | Single-flight per shared command; IsExecuting and ExecutionTask. Existing source refresh and dispatcher policy. |
| Cancellation | No command-owned cancellation state. | A useful cooperative cancellation pattern. | Optional `(object?, CancellationToken) => Task` callback; Cancel, CanCancel, IsCancellationRequested. Avoid ambiguous unary object/token lambda overloads. |
| Exceptions | Dispatcher.Post rethrows on the UI loop; Task APIs expose faults to their caller. | Observe asynchronous failures from void ICommand.Execute. | Supervise the task and post the original fault to the existing dispatcher. ExecuteAsync returns the observed operation to callers. Retain ExecutionTask for explicit application shutdown coordination. |
| MenuItem | Non-Control ILayoutable shared by menu, popup, toolbar and ribbon; no ICommand or IDisposable. | Shared command source, availability, target and detach. | Adapt to existing CommandSource; runtime Command/Parameter/Target, guarded Click order, minimal command-resource disposal. Do not redesign popup/image ownership. |
| Context menus | ContextMenu inherits MenuDropDown; Show receives the originating Control, while the popup owns a separate control tree. | A deterministic logical command source before/after popup hiding. | Capture the Show origin for command context only; submenu items resolve their existing logical MenuItem root. Do not route via Form.Owner or rebuild the popup host. |
| Toolbar | ToolBar inherits MenuBase and uses MenuItem. | Action integration and coverage. | Reuse the same MenuItem implementation; no ToolStrip replacement. |
| Ribbon | Existing Ribbon groups contain MenuItem; Ribbon is explicitly incomplete. | Consistent behavior of existing action items. | Inherit shared MenuItem command semantics and release/refresh command resources through existing ownership. Do not claim Ribbon completeness or add controls. |
| NotifyIconMenuItem | Phase 1 shared CommandSource, no control tree. | Async behavior and deterministic routed invocation. | Reuse AsyncCommand directly; explicit CommandTarget supplies the sole routing context. Keep native menu ownership/snapshot behavior. |
| Button/input | Canonical Phase 1 source and Phase 2 resolver feed Phase 3 routing. | Async helper integration without extra predicate evaluation. | A guarded internal AsyncCommand entry point after existing source checks; preserve Click/gesture order. |
| Routed async use | Synchronous immutable handler routing and per-invocation snapshots. | A safe composition example. | Routed handler starts a shared AsyncCommand, sets Handled, and forwards availability changes. No AsyncRoutedCommand or Task-aware route rewrite. |
| Designer | PropertyGrid respects Browsable(false) and DesignerSerializationVisibility.Hidden; .mfdesign/codegen use declarative values. | Consistent runtime-only properties and save/reopen proof. | Hide command objects, arbitrary parameters/targets and binding collections; test PropertyGrid, serialization, clipboard, undo/redo, codegen and parser. No delegate serialization or command editor. |
| Docs/sample | Commands guide and small Button/routing gallery pages exist. | Final async/action-control guidance and executable example. | Extend existing command showcase modestly, with busy/completion/cancel status and menu/toolbar commands; keep DemoApp/templates unchanged. |
| Diagnostics | Opt-in input and routed diagnostics omit user data. | Minimal asynchronous outcomes. | Opt-in started/completed/faulted/cancelled diagnostics with parameter/exception types only; no user values/messages or tools UI. |
| Testing | Existing core, deterministic TestHost, platform providers and Designer suites. | Async and action-control regression coverage. | TaskCompletionSource/manual completion, existing dispatcher drains and native UIA/SendInput. No sleeps in async tests or new TestHost infrastructure. |
| Accessibility | Button and MenuBase logical MenuItem Invoke use canonical activation. | Busy/disabled and new routed MenuItem behavior. | Reuse normal activation and existing state notifications; no new semantics or backend architecture. |

## Contracts selected for implementation

- Commands are reusable and independent of an individual source lifetime. Disposing a Button,
  menu owner or tray item detaches that source; it never cancels/disposes a shared command.
- Start/query on the creating UI thread. Execution-state reads are thread-safe. Completion may
  occur on another thread; framework sources already marshal background CanExecuteChanged to
  the existing dispatcher. No new SynchronizationContext, scheduler or global execution lock.
- A single-flight command rejects a second start until the first operation finishes. A fresh
  cancellation source belongs to each cancellable invocation. Cancellation is cooperative;
  ignored requests do not manufacture task cancellation. State resets on success/fault/cancel.
- Task completion and notifications must not produce unobserved supervisor tasks. Original
  operation failures take precedence over observer failures while reporting that operation.
  Applications must await/cancel their work before ending the UI loop if they need completion UI.
- MenuItem command targets are ordinary Control identities. An explicit target must be in the
  logical source tree; ordinary ICommand ignores it. Tray items have no implicit control target.
  The shared source supplies its own lifetime to routed invocation so disposing a non-Control
  source also stops further callbacks without altering the Phase 3 public route API.
- Runtime command properties remain hidden/nonserializable. Code-behind assigns commands after
  InitializeComponent. Declarative command editing is a separate future capability, not a new
  .mfdesign requirement in this phase.

Implementation and local commits only. No push, PR, merge, issue closure, release, tag or version
change. Final readiness requires both a Phase 4 matrix and an audit of the whole live issue #56.
#85, #97, #61 UI, #62, #63 redesign, later TestHost phases, new accessibility systems and platforms
are out of scope.
