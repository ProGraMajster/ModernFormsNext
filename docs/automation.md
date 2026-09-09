# In-process semantic automation

`ModernFormsNext.Automation` is the optional development-time semantic core from **issue #97
Phase 1a**. It reads the existing `AccessibleObject` hierarchy and executes existing semantic
actions. It can be used in an initialized live application or in `ModernFormsNext.Testing` tests.
The runtime package depends only on the platform-neutral ModernFormsNext target, never Testing,
Windows/Android backends or MCP. Existing application startup and accessibility behavior remain
unchanged when this package is omitted.

This is an in-process API. It opens no endpoint, enumerates no processes, and implements no
discovery, authentication, IPC, CLI, event stream, wait service or MCP adapter. The whole #97
issue remains open. A session ID identifies a semantic lifetime; it is **not an authentication
token** or a security boundary against application code already running in the same process.

## Opt-in and ownership

Create the session on the application's initialized production UI dispatcher. Register only the
windows or surfaces that the developer explicitly permits. Registrations may precede window
Show; they do not implicitly register other open windows.

```csharp
using ModernFormsNext;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Automation;

// On the application's UI thread, after normal platform startup:
var form = new Form { Text = "Orders" };
var save = form.Controls.Add(new Button
{
    Name = "saveOrder",
    Text = "Save",
    Command = new DelegateCommand(() => SaveOrder())
});

#if DEBUG
using var automation = new AutomationSession(
    AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions);
using var root = automation.RegisterRoot(form);

var found = await automation.FindOneAsync(root.RootId,
    new AutomationQuery { AutomationId = "saveOrder" });
if (found.Error == AutomationErrorCode.None)
{
    var action = await automation.PerformActionAsync(root.RootId,
        found.Value!.Handle, AccessibleActions.Invoke);
    // Accepted means canonical activation accepted the request. SaveOrder may still be running.
}
#endif
```

`SaveOrder` represents application-owned behavior. Keep the session/registration alive for the
intended development scope; dispose them before the owning dispatcher shuts down. An application
may also explicitly opt into testing its Release build, or omit the package and activation code
entirely. Merely referencing the assembly starts nothing.

`RegisterRoot(WindowBase)` covers Form and other existing windows. `RegisterRoot(SkiaControlSurface)`
borrows `surface.Root`; it does not create a new host. Window close/disposal immediately unregisters
that window. A cancelled close preserves registration. Surface disposal, root disposal and garbage
collection invalidate registration; surface disposal/collection is pruned when registration state
or a live operation is observed. Unregistering never disposes the application root.

The session and registration tokens hold roots weakly. Window lifetime subscriptions are removed
on unregister/stop. There are no semantic-change subscriptions in Phase 1a. Stop is idempotent and
invalidates every handle. `Stop`, `Dispose`, `RegisterRoot`, and registration `IsRegistered`/`Dispose`
are UI-thread-affine. `StopAsync` marshals cleanup for a background caller.

One session captures one `WindowKit.Threading.Dispatcher.UIThread` for its entire lifetime.
Current Windows windows and windowless surfaces use that application dispatcher, so multiple roots
do not require multiple dispatchers. Initialization is an application precondition: `VerifyAccess`
checks thread access, not whether a real backend was initialized. Do not construct the session
before platform startup or carry it across replacement/shutdown of its dispatcher. A future live
server can call the async methods from its own I/O threads without owning a UI dispatcher.

The Android backend also has a separate `IPlatformDispatcher` service. Phase 1a does not adapt that
service or claim Android-host automation parity. A future Android bridge must verify scheduling
and activity/surface lifetime explicitly; registering a surface alone is not proof of that integration.
No speculative dispatcher-injection or multiple-dispatcher API is introduced here.

A background `StopAsync` request takes effect when its queued cleanup executes. It cannot interrupt
the current synchronous getter or action. Once cleanup has executed, queued operations cannot act.
Cancellation is checked before/after returning capture getters and immediately before dispatching
an action. A cancellation request does not undo already executed application work.

## Identity, scope and lifetime

Every session gets a random nonce independent of PID. An `AutomationNodeHandle` contains exactly
the session nonce and `AccessibleObject.RuntimeId` as an invariant positive decimal **string**.
This preserves every Int64 bit when a later JavaScript client reads the DTO. No replacement
global node counter or control-name identity is introduced.

Each operation also requires the root registration ID. There is no cross-root or cross-session
fallback. Re-registering the same window produces a new root ID, while its canonical peer may keep
the same runtime ID. `AutomationId` is an exact ordinal locator, may fall back to Control.Name,
may be null for logical items, and may match multiple nodes.

The pair `root.RootId` and `snapshot.Handle` is the required operation scope. The snapshot/root
descriptor already carries both, so clients need not infer IDs. Keeping registration scope separate
from canonical identity lets the same peer participate in independently permitted registrations;
neither a composite handle nor a second identity counter is needed. Transport adapters may wrap
these existing values in a request envelope without changing semantic identity.

Every query/action re-traverses the allowed active canonical tree. Temporary peer/edge maps exist
only during that UI operation. They are discarded before DTOs leave the dispatcher; there is no
persistent peer cache or second semantic tree.

| Change | Handle behavior |
| --- | --- |
| Same control reordered | Same canonical peer and handle |
| Removed/detached or Hidden/Invisible | Currently unavailable; this is not proof of disposal |
| Same cached control/TreeView peer reinserted | Resolvable again if #59 preserves its RuntimeId |
| ListBox occurrence removed and added again | New occurrence identity, as defined by #59 |
| New control with the same name | New RuntimeId; old handle never retargets |
| Wrong root | NodeUnavailable; no fallback to another root |
| Other session or ended root registration | StaleNode |
| Stopped session | SessionEnded |

## Queries and immutable snapshots

The async methods are `GetRootsAsync`, `InspectAsync`, `GetChildrenAsync`, `FindOneAsync` and
`FindAllAsync`. Find filters combine AutomationId, accessible Name, normalized ControlType,
all-required States and all-excluded States with AND. Null filters are ignored. Empty strings
match only empty strings. There is no XPath/CSS language or reflection query.

```csharp
var selectedItems = await automation.FindAllAsync(root.RootId, new AutomationQuery
{
    ControlType = AccessibleControlType.ListItem,
    RequiredStates = AccessibleStates.Selected,
    ExcludedStates = AccessibleStates.Unavailable
});
if (selectedItems.Error != AutomationErrorCode.None || selectedItems.Truncated)
{
    // Inspect the safe Issues; do not infer that the returned list is complete.
}
```

Traversal is depth-first preorder, including the registered root, with canonical child indices
ascending. Root descriptors preserve registration order. Child queries return direct projected
children. Snapshot parent IDs describe these traversed edges within the registered scope. Form's
canonical child enumeration skips its internal FormClientArea although Parent pointers pass
through that peer; bounded parent-chain validation accepts this existing projection without
adding the omitted peer to query results. Outside-scope parents are not exported.

`FindOne` returns NodeNotFound for zero complete matches, a snapshot for exactly one complete
match, and AmbiguousMatch when at least two match. A limit or getter fault cannot certify uniqueness
or absence. A second candidate is checked even when MaxResults is one; no `FindFirst` behavior is
silently substituted.

Snapshots contain session/root/runtime IDs, locator/name, canonical role/type/states/actions,
safe value/range, bounds, projected parent/child IDs, redaction reasons, truncation and capture ID.
They contain no Control, AccessibleObject, object payload, delegate, CLR Type, native handle or
exception. Child IDs and result/diagnostic collections are immutable. Error collection results
use initialized empty arrays and can be inspected/serialized safely.

CaptureId identifies one UI operation, not a semantic revision, a completed render, a transaction
across async work, or an assertion that state is still current. Window bounds preserve canonical
screen coordinates; windowless bounds preserve surface coordinates, in logical pixels. Bounds do
not claim complete clipping, occlusion or native accessibility parity.

## Limits and faulty custom peers

Session `AutomationQueryOptions` is immutable:

| Option | Default | Accepted range |
| --- | --- | --- |
| MaxDepth | 64 | 0–256; root depth is zero |
| MaxNodes | 4096 | 1–100000 |
| MaxResults | 1024 | 1–100000 |

MaxNodes counts node/child attempts, including malformed or hidden children, plus parent-chain
hops through omitted peers. MaxResults also bounds root descriptors. The registration count is
bounded by MaxNodes. Each exported/input text field is limited to 4096 UTF-16 code units; oversized
output is omitted and reported as LimitExceeded. Action/query inputs exceeding that limit return
InvalidArgument. Diagnostics are bounded by MaxNodes too.

Cycles, repeated references, duplicate RuntimeId, invalid/null child entries, negative child count
and inconsistent parent chains return structured issues. A huge reported count never causes an
equally huge preallocation or unbounded child loop. Getter exceptions are caught without ending
the session and appear as GetterFault. Incomplete data has explicit Error/Issues/Truncated metadata;
check all three. Actions fail closed when traversal cannot establish valid reachability.

These are cooperative in-process bounds. They cannot preempt a single synchronous getter that
blocks forever or enumerates infinitely inside its own implementation. Custom peers must return
promptly and obey their semantic contract. This layer is not a sandbox for hostile application
code or a hard real-time deadline service. The framework model is unchanged.

## Privacy and errors

One central capture layer classifies IsSensitive, Protected and known password TextBox owners.
It suppresses Value, RangeValue, Name and AutomationId for sensitive nodes and their descendants,
including untrusted custom peers. Payload getters are skipped when privacy is already sensitive
or unknown. Failed privacy/state getters cause conservative redaction. Privacy is rechecked during
capture; an escalation discards already captured payload.

`PrivacyUnknown` specifically means a failing privacy/state getter. An ordinary custom
`AccessibleObject` inherits `IsSensitive == false` and remains queryable without an extra marker.
Fault tracking remains effective even after the bounded diagnostic collection fills up.

This conservative policy intentionally omits even labels/locators on a password node. An application
can address such a node by a known runtime handle or an unambiguous type/state query, for example
ControlType.Edit with RequiredStates.Protected. There is no filtering by a redacted value. Ordinary
editable TextBox.Name follows #59 semantics and never falls back to the user's Text.

Password SetValue is allowed when Actions and the canonical peer permit it, with no value readback
in the action result. The caller owns any secret in `AutomationActionValue`; do not log/echo or
serialize it as diagnostics. Its ToString returns only a fixed label. The action service accepts
no arbitrary object parameter. Existing application CommandParameter objects remain inside the
normal command path and are never formatted by Automation.

Client-facing failures contain only controlled codes and property/ID metadata. They never include
Exception.Message, Exception.ToString, control text, action values or arbitrary parameter.ToString.
No exception/log/diagnostic event stream is exposed. An unmarked custom peer can still deliberately
misrepresent private data as ordinary semantic metadata; application code must honor the canonical
privacy markers. Phase 1a cannot infer secrets from arbitrary strings.

## Actions and dispatcher

`PerformActionAsync` supports Invoke, Focus, SetValue, Select, Toggle, Expand, Collapse, Increment,
Decrement and ScrollIntoView **only when currently advertised**. Scroll has no Phase 1a parameter
contract and returns Unsupported. Exactly one flag is required. Only SetValue accepts a typed text
or finite numeric value; null clears text. Range minimum/maximum/read-only semantics are checked,
and TrackBar's integral natural value is enforced. Other peer-specific rules remain canonical.

All live reads, range validation, state rechecks and PerformAction run on the captured production
Dispatcher. Same-thread calls use Dispatcher.Invoke's existing context; background calls use its
InvokeAsync queue. No additional SynchronizationContext, worker UI thread or dispatcher is created.
Cancellation stops queued work or traversal between getters, and never rolls back an action.
Do not synchronously wait on an unfinished operation from the UI thread.

Reachability is checked again after getter-based action validation, since custom callbacks can
detach/close a node. The canonical implementation remains the final authority on state/action
acceptance. The service does not execute ICommand directly, invoke private event handlers, set
AccessibleObject.Value directly, or use reflection. Button activation preserves Click ordering,
CommandSource, CanExecute and Delegate/Routed/AsyncCommand behavior.

| Action status | Meaning |
| --- | --- |
| Accepted | PerformAction returned true; business work may still be running |
| Rejected | Canonical false, unavailable state or capability denial; see Error |
| Unsupported | Not advertised or outside the Phase 1a allowlist |
| NodeUnavailable | Stale/ended scope, no active node, or invalid/incomplete reachability |
| InvalidArgument | Invalid action flags or typed value |
| ApplicationError | Getter or action threw; no private exception detail is exported |

A throwing action may already have changed application state. Do not automatically retry a
mutation after ApplicationError. An accepted AsyncCommand can remain IsExecuting until its
application-owned Task finishes. Acceptance does not prove command execution after a Click handler
changed CanExecute, or business success after later async completion.

## TestHost integration and future seams

Only `ModernFormsNext.Automation.Tests` references both Automation and Testing. It creates real
Forms, buttons with Delegate/Routed/AsyncCommand, TextBox/password, CheckBox, RadioButton, ListBox,
TreeView and custom semantic children. It verifies actual state changes, canonical identity,
bounded malformed peers, privacy, root/session lifetime and background dispatch with explicit
dispatcher drains and controlled task completion.

No public WaitCondition is published speculatively. Phase 1b can use existing immutable filters,
states, capture metadata, root-scoped handles, safe errors and completeness flags. It must separately
design condition evaluation, notification subscription/reconciliation, cancellation/deadlines and
narrow idle checkpoints. ClientNotification is not assumed to cover all custom changes.

Future named-pipe/other transport adapters call these same async methods. No Stream/Socket, JSON
attribute, wire protocol version, auth handshake or MCP type is present in the core contract.
Phase 1b still needs Windows IPC, discovery/authentication, live waits and a minimal client; actual
external-client acceptance follows separately. Events, screenshots, MCP and broader diagnostics
remain their later scope. No #64 Phase 2 or #61/#62/#63/#72/#108/#109 work is part of this package.

See the [pre-implementation audit](automation-phase1a-audit.md) for the source capability matrix.
