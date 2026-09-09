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
Parent pointers are validated against the traversed edge with bounded canonical parent-chain
hops for omitted implementation peers (FormClientArea), never projected as additional nodes. Duplicate
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

## Final public API review

Phase 1a has 18 exported types before and after review. No type exists solely for tests. The
following contracts remain public because callers need them to invoke the service, own an opt-in
registration, express a request, or interpret returned data. Internal traversal/entry/frame types,
redaction logic, dispatcher plumbing and lifetime accessors remain internal. No existing framework
public API or enum value changes.

| Type | Why public | Stable for Phase 1b? | Verdict |
| --- | --- | --- | --- |
| AutomationSession | In-process service and explicit session lifetime | Yes; adapters call its async methods | Keep |
| AutomationRootRegistration | Disposable borrowed-root scope and effective policy | Yes; token remains in process | Keep |
| AutomationNodeHandle | Request identity: session plus canonical decimal RuntimeId | Yes; root scope remains required separately | Keep |
| AutomationNodeSnapshot | Detached semantic output used by clients and predicates | Yes; not a wire schema or revision | Keep |
| AutomationRootInfo | Detached registered-root description | Yes; no live registration reference | Keep |
| AutomationQuery | Simple immutable AND filters | Yes; additive filters need no selector language | Keep |
| AutomationQueryOptions | Caller-selected immutable traversal budgets | Yes; independent of transport limits | Keep |
| AutomationResult&lt;T&gt; | Typed query data, completeness and safe diagnostics | Yes; no protocol/exception payload | Keep |
| AutomationIssue | Bounded diagnostic record clients must interpret | Yes; controlled metadata only | Keep |
| AutomationActionValue | Explicit string/number input without object payloads | Yes; adapters use factories after wire validation | Keep |
| AutomationActionResult | Action acceptance and semantic error | Yes; business completion stays separate | Keep |
| AutomationBounds | Immutable canonical geometry with explicit reference frame | Yes; no native handle or clipping claim | Keep |
| AutomationCapability | Session/root Inspect, Query and Actions policy | Yes; no speculative flags | Keep |
| AutomationErrorCode | Machine-readable semantic outcomes | Yes; existing values must remain stable | Keep |
| AutomationProperty | Safe diagnostic field identity | Yes; avoids arbitrary property-name strings | Keep |
| AutomationActionStatus | Acceptance categories distinct from completion | Yes; no generic Success promise | Keep |
| AutomationRedaction | Explains omitted private/unknown payload | Yes; bounded diagnostics do not disable privacy | Keep |
| AutomationCoordinateSpace | Distinguishes window screen and surface geometry | Yes; no backend-specific types | Keep |

The session and registration are live service/lifetime types, not DTOs to serialize. Requests and
output use strings, numbers, enums, immutable records/arrays and existing immutable range metadata.
Output getters support serialization; output-only internal constructors intentionally do not promise
JSON deserialization. A transport must define and validate its own wire envelopes. Public signatures
contain no JSON dependency. Nullable metadata means absent, redacted or unavailable; result codes
and diagnostics disambiguate failures. A failed collection request returns an initialized empty array.

Each snapshot field supplies current Phase 1a inspection, identity, query, action discovery, hierarchy,
privacy, geometry or completeness information. SessionId/RuntimeId are convenience projections of
Handle, not extra stored identities. CaptureId correlates a returned result and its snapshots today;
later waits can identify the observation that satisfied a predicate. It does not order captures or
claim atomicity, a semantic revision, render completion, idle, or business completion.

One captured production UI dispatcher matches current Windows WindowBase and SkiaControlSurface
ownership. A future server can schedule from I/O threads through the existing async API. Creating
another dispatcher per root would conflict with current framework ownership. Backend initialization
is the application's precondition. Android's separate IPlatformDispatcher is not automatically wired
to this contract; Android bridge scheduling and activity/surface recreation remain future validation,
not promised parity. Session/registration lifetimes must end before their dispatcher shuts down.

Friend access is granted only to ModernFormsNext.Automation. Its consumed core internals are exactly
WindowBase.InputBindingsClosed, Control.IsDisposed and SkiaControlSurface.IsDisposed. These are
read-only lifetime observations; command collections, root adapters and private setters are not used.
Removing the friend seam would require a larger public core lifetime API or duplicate lifecycle state.

Final review added regressions for a full diagnostic budget followed by a privacy fault, cancellation
during a getter and final action validation, queued query/action versus session/root termination,
background StopAsync during a getter, cancellation plus Stop/Dispose, FormClientArea omission, and
detached/cyclic/over-budget parent chains. Three regression cases failed before the fixes. Fault
tracking now remains independent of exported diagnostic capacity, and cooperative cancellation is
checked at returning capture getter boundaries and immediately before the canonical action call.

The default depth64/nodes4096/results1024 budgets stay unchanged. Hard limits bound structural
storage, result storage, diagnostics and parent hops; huge declared child counts never preallocate
matching storage. Raising all limits and retaining maximum-length text can still consume substantial
memory, so defaults are recommended. Synchronous application callbacks must return promptly and
honor canonical semantics. Reentrant application mutation is not an atomic transaction or a sandbox;
the canonical action remains the final authority. No polling, timeout or thread-abort feature was added.
