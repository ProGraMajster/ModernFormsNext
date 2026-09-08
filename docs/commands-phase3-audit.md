# Command routing audit — issue #56 Phase 3

Baseline: `bf4b120a9fc3b98f7b1f2e075b68abab394b74eb` (Phase 2 merged).
This audit precedes implementation. Phase 1/2 remain the canonical action and keyboard paths.

| Area | Existing behavior | Needed for Phase 3 | Proposed reuse |
| --- | --- | --- | --- |
| Control tree | ControlCollection maintains Parent; parenting cycles are rejected. | A bounded route with deterministic order. | Snapshot the live Parent chain per invocation; no registry, graph cache or reflection. |
| Windows | WindowBase is a Component, not a Control. Its ControlAdapter roots the tree; FindWindow follows Parent. | Window handlers and ownership boundary. | Include the existing adapter's window once; never follow Form.Owner or popup ownership. |
| Focus | Native windows have adapter.SelectedControl. SkiaControlSurface observes Selected in its borrowed tree. No unambiguous application-wide active focus. | Target selection without crossing windows. | Keyboard entry points already supply focus/root/window. Button defaults to itself. |
| Input/events | Window KeyDown preview, Phase 2 lookup, then selected-control dispatch. No general routed-event/tunnel infrastructure. | Command handler lookup independent of gesture lookup. | A separate synchronous target-to-root command traversal; retain all input ordering and consumption. |
| Semantics | Canonical accessibility includes logical/non-Control item nodes and platform adapters. | Accessible activation of existing command sources. | Button Invoke still calls normal activation. Non-Control semantic nodes do not become route nodes. |
| Action sources | CommandSource centralizes ICommand, parameters, weak subscriptions, Enabled, version guards and exceptions. | Target-aware evaluation for RoutedCommand only. | Extend this helper narrowly for Button; plain ICommand and NotifyIconMenuItem retain their paths. |
| InputBindingResolver | Snapshots matching gesture registrations; validates versions/lifetimes; evaluates concrete ICommand. | Feed routed commands a target and consume the prepared evaluation. | A small RoutedCommand branch after gesture lookup; ordinary commands remain direct. |
| Lifetime | Control disposal, actual window close and application shutdown already release input bindings. | Release handler ownership and observers. | Parallel command cleanup at those same lifetime points; cancelled close preserves bindings. |
| Diagnostics | InputBindingCollection has opt-in diagnostic events. | Explain route traversal and outcomes. | Per-RoutedCommand opt-in diagnostics, separate from gesture diagnostics, no global subscribers. |
| Designer | DesignerCommandService owns design/session operations; native command IDs have a separate legacy purpose. | Preserve tooling boundaries. | No reuse of Designer services/native command registry for runtime routing; hide runtime properties from serialization. |
| TestHost | Real controls, immutable diagnostic tree snapshots, deterministic UI dispatcher. | Repeatable route/activation tests. | Existing control/window seams and host dispatcher; no alternate production tree or keyboard simulator. |

## Selected contracts

- A sealed `RoutedCommand : System.Windows.Input.ICommand` has immutable optional Name and
  reference identity. Target overloads are canonical. Parameter-only calls have no context:
  CanExecute returns false and Execute does nothing. Framework sources supply their context.
  This avoids inventing a global focus singleton or guessing between windows.
- `Control? CommandTarget` is additive on Button and InputBinding only, and ignored for ordinary
  ICommand. Explicit invalid/detached/disposed/wrong-context targets fail closed without fallback.
  Button: explicit target, otherwise source Button. Keyboard: explicit target, otherwise valid
  focused control, binding control scope, then entry root. Focus from another tree is ignored.
- A target must reach an existing native ControlAdapter or standalone surface root. Detached
  trees are unavailable. The route uses controls, then window (if present), then application as
  a terminal fallback. Popup ownership does not extend the route to the owner's window.
- Immutable CommandBinding ties a RoutedCommand to synchronous Executed and optional CanExecute
  delegates. An absent CanExecute delegate is unavailable. Collections have one-owner bindings,
  insertion order and creating-UI-thread affinity. Commands are never disposed by a collection.
- Query scans in order: true selects the first available binding; false/Unhandled continues;
  false/Handled vetoes the remaining route. Execution starts at the selected binding and may
  continue to later available bindings until Executed.Handled. Each later binding is queried
  before execution. This gives local override and explicit event-style continuation without
  accidentally executing a handler whose CanExecute was false.
- Each invocation captures nodes and matching immutable registrations before application code.
  Query and execution share that snapshot. Adds/removes/reparent/focus changes take effect at the
  next invocation, without changing the current route. Disposed owners/targets and closed windows
  are terminal safety boundaries, even for a captured route. Nested calls get separate contexts.
- No preview/tunnel: existing window input preview remains sufficient for this phase. Routing
  has no async state, cancellation, command registry, global reentrancy lock or service locator.
- RoutedCommand.RaiseCanExecuteChanged uses Phase 1 subscription/dispatcher behavior. Collection
  edits notify the affected commands; source attachment refreshes routed Button availability.
  Application state changes require explicit requery; there is no global polling.
- Diagnostics are public opt-in per command for app logging and future tools. Events identify
  visited owners, bindings, query/execution/handled results and constant failure reasons. They
  do not expose the route snapshot or parameter value, call ToString, or copy control text.
  Handler exceptions propagate unchanged; diagnostic reporting must not replace an original
  command exception. Observers should be fast and must not mutate UI state.

Phase 4, async helpers, deeper action-control integration, Designer work, #61 UI, #85 and #97
remain outside this implementation. Issue #56 stays OPEN. Work is committed locally only.
