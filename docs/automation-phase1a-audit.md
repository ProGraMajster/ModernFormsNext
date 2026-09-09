# Issue #97 Phase 1a: semantic core audit

Baseline: `master == origin/master`, `6e3a7cfd148915f51b305aa17296d41fbd9222c1`, divergence 0/0.
Read the complete current [issue #97](https://github.com/ProGraMajster/ModernFormsNext/issues/97)
(OPEN, no comments), its acceptance direction, and the local dependency audit and body proposal.
The issue still describes the complete external bridge. This delivery implements only its
in-process semantic foundation; it does not meet the external-client acceptance of all Phase 1.

## Capability audit before API design

| Capability | Existing source | How #97 will reuse it | Gap |
| --- | --- | --- | --- |
| Canonical peers | `Accessibility/AccessibleObject.cs`, `Control.Accessibility.cs` | Read the actual cached accessibility objects | Defensive snapshot projection |
| Runtime identity | `AccessibleObject.RuntimeId` | Nonvirtual, immutable, atomically assigned positive Int64 | Session nonce and decimal-string handles |
| Locator | `Control.ControlAccessibleObject.AutomationId` | Exact ordinal match; fallback to Control.Name is canonical | Zero/one/multiple result contract |
| Role/type/state | `AccessibleRole`, `AccessibleControlType`, `AccessibleStates` | Copy canonical values | Immutable bounded result |
| Logical/custom children | `Control.ControlAccessibleObject.LogicalChildren.cs`, `GetAccessibilityChildren` | Traverse GetChildCount/GetChild in index order | Cycle, duplicate, parent consistency, count and getter fault protection |
| Active projection | `EnumerateActiveChildren`, peer View/State | Respect Hidden/Invisible and logical expansion | Absence must not imply disposal |
| Item lifetime | Occurrence identity for ListBox; ConditionalWeakTable for TreeView | Re-resolve each live operation | Tests must distinguish occurrence replacement from cached peer reinsertion |
| Privacy | IsSensitive, Protected, TextBox password semantics | Central conservative redaction before reading payload fields | Custom getters and exception text are untrusted |
| Range | AccessibleRangeValue, TrackBar/ProgressBar peers | Copy immutable range; validate finite values and bounds | No arbitrary parameter object in DTO |
| Actions/focus | SupportedActions/PerformAction; Control.Select | Advertised canonical operations only | Validate current state and return accepted/rejected status |
| Commands | Button.PerformClick, CommandSource, Delegate/Routed/AsyncCommand | Invoke through canonical activation, preserving Click and CanExecute | No command catalog; accepted is not async completion |
| UI thread | WindowKit.Threading.Dispatcher.UIThread/InvokeAsync | Capture production dispatcher at session creation | Async entry from background callers |
| Window roots | WindowBase.AccessibilityObject, Closed/Disposed | Explicit borrowed root registration, weak ownership | Already-ended check uses existing internal InputBindingsClosed lifetime flag |
| Surface roots | SkiaControlSurface.Root and Dispose | Register root peer with surface coordinates | Minimal internal disposed-state accessor; no public agent APIs |
| Notifications | AccessibleObject.ClientNotification | Reserve for later waits design | Not a complete tree event stream; no subscriptions/polling now |
| Tests | ModernFormsTestHost, UiTestDispatcher, TestWindowHost | Real Form/controls and production dispatcher in tests | New Automation.Tests references both packages |

## Design decisions

Use a separate optional `ModernFormsNext.Automation` package targeting `net10.0`, depending only
on the neutral ModernFormsNext target. It follows existing package metadata/readme/XML conventions.
Do not change versions or add third-party dependencies. Testing and platform backends are not core
dependencies. Core never references Automation.

Sessions are explicit and created on the existing UI dispatcher. Roots are borrowed, weakly held,
and individually disposable; window close/disposal removes registration without stopping other
roots. Surface disposal is checked when registration state or a live operation is observed.
The only framework seam is friend access for existing window lifetime state and a read-only
internal surface disposed accessor. No new public Control/Application/Form API is needed.

Handles contain session nonce plus canonical runtime ID, both strings. Root scope is a separate
registration ID, required on every node query/action. Re-registration gets a new root ID.
There is no persistent peer index or second tree: each bounded UI operation traverses allowed
canonical children and discards temporary peer references before returning immutable DTOs.

Traversal is depth-first preorder, child indices ascending, roots in registration order. Every
visit/child attempt counts against a budget; a huge child count never allocates a matching array.
Parent pointers are validated against the traversed edge, never recursively followed. Duplicate
RuntimeId and repeated references are errors (RuntimeId is nonvirtual, so genuine duplicates
require corrupt peers; tests may simulate corruption). Incomplete queries cannot certify unique
matches or absence. Actions fail closed when reachability cannot be fully validated.

Public errors contain only codes and fixed enum metadata, never arbitrary exception messages,
ToString output or request parameters. Sensitive/protected peers redact value/range and textual
metadata conservatively; privacy propagates to descendants. Getter faults do not terminate a
session. Snapshots and queries report structured faults and explicit truncation.

## Deferred contracts

Waits will consume query predicates, immutable state, capture IDs, scoped handles, completeness
and error codes. Do not publish speculative WaitCondition APIs: Phase 1b must design notification
registration/reconciliation, cancellation, monotonic deadlines and narrow idle checkpoints.

Future transport adapters call the same async semantic methods. No Stream, Socket, JSON attributes,
protocol version, discovery, auth/token handshake, IPC, CLI, MCP, timers, event stream, screenshots
or raw input belongs in this delivery. No #64 Phase 2, #61/#62/#63/#72/#108/#109 implementation.
Issue #97 stays OPEN; only a local amendment proposal will be prepared after implementation.
