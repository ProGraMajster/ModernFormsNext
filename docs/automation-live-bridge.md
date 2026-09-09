# Windows live automation bridge

Issue #97 Phase 1b adds an optional **development/testing** bridge to a running ModernFormsNext
application. `ModernFormsNext.Automation.Windows` targets `net10.0-windows` and depends only on
`ModernFormsNext.Automation`. It provides a Windows server, discovery and a usable public client.
The nonpackable `ModernFormsNext.Automation.Cli` executable exercises the same client API.

The [semantic core](automation.md) remains authoritative: root registrations, session/runtime
handles, canonical `AccessibleObject` queries, `PerformAction`, capabilities, safe errors and
privacy redaction. The adapter never traverses controls, invokes commands directly or maintains
another live node index. Internal DTO constructors are shared narrowly with the Windows assembly
to reconstruct detached client results; serializer attributes and Win32 APIs stay out of the core.

## Explicit enablement and shutdown

Referencing either package creates **no listener, descriptor, token or endpoint**. The application
must call `Start` explicitly after initializing its production UI dispatcher. This applies equally
to Debug and Release. There is no `Debugger.IsAttached` switch and no library `#if DEBUG` policy.
Production apps may omit the adapter reference or simply never call `Start`.

```csharp
using ModernFormsNext.Automation;
using ModernFormsNext.Automation.Windows;

// On the initialized application UI dispatcher; form is an application-owned Form.
var semantic = new AutomationSession();
var registration = semantic.RegisterRoot(form);
var server = WindowsAutomationServer.Start(semantic, new()
{
    ApplicationName = "Orders development instance",
    Capabilities = AutomationCapability.Inspect
        | AutomationCapability.Query | AutomationCapability.Actions
});

// In the application's asynchronous shutdown path, while its UI dispatcher still runs:
await server.StopAsync();
registration.Dispose(); // UI thread
semantic.Dispose();     // UI thread
```

Keep the server in the application's lifetime owner. Await shutdown before allowing the main
window/message loop to exit; do not block the UI thread with `.Wait()` or `.Result`. A closing
handler can cancel the first close, await server cleanup, then permit and reissue close. Normal
ModernFormsNext startup installs the dispatcher synchronization context; explicitly marshal the
final registration/session disposal when using `ConfigureAwait(false)`.

The server **borrows** the session and never stops it, unregisters roots, closes windows or disposes
controls. `StopAsync`/`DisposeAsync` are idempotent. There is one server lease per semantic session;
a duplicate `Start` reports `Busy` until cleanup completes. Starting again after shutdown generates
a new endpoint, instance nonce and token. The application can independently stop the core session;
subsequent semantic requests then report session end.

## Discovery and identity

`AutomationDiscovery.DiscoverAsync()` reads at most 1024 descriptors from
`%LOCALAPPDATA%/ModernFormsNext/Automation/v1`, ordered by instance ID. An absent directory returns
an empty list without creating it. There is no brute-force named-pipe enumeration.

Each `<instance>.json` descriptor contains only public metadata:

| Field | Meaning |
| --- | --- |
| `instanceId` | Random lowercase Guid N bridge-lifetime nonce |
| `applicationName` | Explicit public label, 1–128 characters; not an identity |
| `processId` | Owning process ID |
| `processStartUtcTicks` | Process creation time as a precision-preserving decimal string |
| `endpointName` | `mfn-automation-{pid}-{instance}` |
| `protocolVersion` | Integer `1` |
| `capabilities` | Server/session capability intersection |
| `createdUtc` | Diagnostic publication time; not an elapsed deadline clock |

Two applications with the same name remain distinct. Clients choose an instance or an unambiguous
PID, never a name. Discovery and connection verify process liveness and start identity, preventing
PID reuse from selecting a different process. Connection also checks the actual pipe server PID
before sending the credential. Authentication checks the bridge nonce independently of PID and
the semantic `AutomationSession.SessionId`.

Discovery removes validated records whose process has exited or whose start identity differs.
The associated secret is removed too. Malformed/unsafe records are ignored, not interpreted as
paths to delete. Orphan secret files without a descriptor can be removed after two minutes; the
age guard avoids deleting a server publishing its descriptor. Filename checks allow only a Guid N
basename inside the fixed directory. Files are created exclusively and flushed; an incomplete
record during publication is ignored until a later discovery. No file becomes a valid descriptor
until the listener and secret exist.

## Security boundary

The discovery directory, descriptors, secret files and pipe use a protected DACL owned by the
current **User SID**, granting that SID full control. Unexpected ACLs and reparse points are
rejected. ACLs on an existing directory are validated, not silently widened or rewritten. New
files receive their restrictive ACL during creation. Unsafe discovery produces a controlled error.

The pipe is created through minimal Windows-only `CreateNamedPipeW` interop with an explicit ACL,
overlapped I/O, initial first-instance protection and `PIPE_REJECT_REMOTE_CLIENTS`. Merely selecting
`.NET PipeOptions.CurrentUserOnly` is insufficient for the explicit remote policy. After the first
bounded client write, the server uses identification-level impersonation to compare the actual
peer's User SID. The policy allows the **same user across elevation levels**; it does not treat
elevation as a security boundary. See Microsoft's [pipe security guidance](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)
and [CreateNamedPipeW contract](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-createnamedpipew).

Every server lifetime generates 32 bytes from `RandomNumberGenerator`. They are stored in a
separate protected `<instance>.auth` file, not the normal descriptor. The client reads that file
after validating the descriptor and ACL. The handshake sends the secret as Base64 and compares it
in constant time. Secrets never enter CLI arguments, normal output, response DTOs or exception
messages. The implementation adds no logging subsystem and clears owned byte buffers where
practical. Stop removes credential files and clears the server's secret buffer. A stale token does
not authenticate a new server.

This is a same-user development bridge, **not a sandbox against malicious code already running
as the same user or as an administrator**. Such code may read protected files, inspect process
memory, manipulate the application or create its own bridge. There is no remote administration,
TCP/WebSocket listener, Android transport or additional elevation guarantee.

The automated security suite checks actual ACL contents, successful ordinary-user authentication,
and OS access-denied results for a restricted token opening the pipe/descriptor/secret. A separate
SMB loopback probe first connects to a permissive positive control, then verifies bridge rejection;
if that prerequisite is unavailable it explicitly reports NOT EXECUTED. Restricted-token testing
is distinct from another logged-on user, and SMB loopback is distinct from a second physical
machine. Those latter scenarios require separate environment evidence.

## Handshake, capabilities and wire limits

Protocol 1 uses a small custom JSON envelope, not an RPC framework. Requests contain `version`,
positive monotonic `id`, numeric `kind`, `deadlineMilliseconds` and typed `payload`. Responses
contain `version`, `id`, controlled `error` and detached `result`. A malformed frame without a
validated ID receives an uncorrelated terminal error with ID zero. No arbitrary application error
text or exception object is serialized.

Handshake verifies protocol, actual peer, instance nonce, credential and requested capabilities.
Granted capabilities are the requested/server/core intersection. Canonical root policy is checked
again by the core on every operation. Snapshot advertised actions describe canonical support;
clients must also respect negotiated capabilities. Root descriptors retain the canonical root
policy, which may be broader than the particular client's grant.

One authenticated client owns a server at a time, including read-only clients. A second valid
client receives `Busy`. That client may have one outstanding semantic request; `Cancel` is a
separate control frame and does not require another slot. Public client concurrent calls report
`Busy` instead of accumulating an unbounded queue. At most seven connections are being served,
plus one pending listener instance. Handshakes have a five-second deadline.
Response writes also have a five-second I/O deadline, so a peer that stops reading cannot retain
an unfinished writer indefinitely. After semantic work finishes, the next request may wait for
that bounded response write to finish; it does not run concurrently with prior semantic work.

| Budget | Default | Allowed server configuration |
| --- | --- | --- |
| Request JSON bytes | 65536 | 4096–1048576 |
| Response JSON bytes | 4194304 | 4096–16777216 |
| String payload | 4096 UTF-16 characters | Fixed |
| JSON depth | 32 | Fixed |
| Maximum request deadline | 60000 ms | 1–60000 ms |
| Outstanding semantic requests/client | 1 | Fixed |
| Core concurrent observations/session | 64 | Fixed |

Frames use a four-byte little-endian signed positive length followed by exactly that many UTF-8
JSON bytes. Length is checked before body allocation. Partial reads, EOF, invalid lengths,
malformed JSON, duplicate fields and oversized strings are handled explicitly. Response encoding
uses a capped buffer, so an oversized detached result reports `PayloadTooLarge` during encoding.
Core traversal depth/node/result budgets remain separate and authoritative. Query limits do not
become transport frame limits.

Request kinds are `Handshake`, `SessionInfo`, `GetRoots`, `Inspect`, `GetChildren`, `FindOne`,
`FindAll`, `PerformAction`, `WaitForCondition`, `Checkpoint`, `Cancel` and `Disconnect`.
Incompatible versions report `ProtocolMismatch`; this phase does not define a version-negotiation
matrix. The transport-neutral payload semantics can be reused later without exposing pipe handles.

## Public client and actions

```csharp
var applications = await AutomationDiscovery.DiscoverAsync(cancellationToken);
var selected = applications.Single(app => app.InstanceId == requestedInstanceId);
await using var client = await WindowsAutomationClient.ConnectAsync(
    selected, cancellationToken: cancellationToken);
var roots = await client.GetRootsAsync(cancellationToken);
var root = roots.Value.Single(); // Require explicit selection when more than one root is present.
var found = await client.FindOneAsync(root.RootId,
    new AutomationQuery { AutomationId = "save" }, cancellationToken);
if (found.Error == AutomationErrorCode.None)
{
    var accepted = await client.PerformActionAsync(root.RootId, found.Value!.Handle,
        AccessibleActions.Invoke, cancellationToken: cancellationToken);
}
```

All client methods are asynchronous and hide the pipe stream. Semantic results retain their
existing `AutomationErrorCode`, partial-result flags, capture IDs and immutable snapshots.
Transport failures use `AutomationTransportException.Error` with controlled messages. A detached
handle always includes semantic session and canonical runtime ID; its operation also supplies the
exact root registration ID. Reconnect does not invent new node identities.

`PerformActionAsync` delegates exclusively to the existing core action path. `Accepted` means
canonical acceptance, not completed business work. If an action may have executed but its response
is lost, the client reports **`OutcomeUnknown`**. It never automatically retries Invoke or another
mutation. Inspect application state or seek application-specific resolution before deciding what
to do next. Even cancellation may leave a mutation ambiguous.

Password/protected/sensitive values stay redacted through JSON, client DTOs and CLI output. Custom
getter exceptions are reported as safe semantic codes. Action parameters are not echoed. For
ordinary unmarked controls, the existing semantic value is intentionally observable; applications
must mark confidential custom peers appropriately.

## Live waits, deadlines and checkpoint

Wait execution belongs to `AutomationSession`, shared by in-process and Windows clients. Conditions
are `NodeExists`, `NodeNotExposed`, `Enabled`, `Exposed`, `Focused`, `Selected`, `ValueEquals`,
`StateContains` and `RootEnded`. Supply an exact handle or an existing unique query; root-ended
observes registration lifetime and takes neither. There is no expression language.

The core performs an immediate read. If unsatisfied, it subscribes to bounded canonical peers on
the UI dispatcher and rereads before awaiting a wakeup. Changes between initial query and
subscription cannot be lost. Notifications are hints that trigger a fresh read; bounded periodic
reconciliation handles missing notifications and newly exposed nodes. Observation references are
weak and are removed on every terminal path.

```csharp
var completed = await client.WaitForConditionAsync(root.RootId, new()
{
    Kind = AutomationWaitKind.ValueEquals,
    Query = new() { AutomationId = "status" },
    Value = "Completed"
}, new() { Timeout = TimeSpan.FromSeconds(10) }, cancellationToken);
```

Wait timeout defaults to ten seconds, is bounded to sixty seconds and uses monotonic elapsed
time. Zero still checks immediately. Reconciliation defaults to 100 ms (20 ms–1 s allowed).
Incomplete captures cannot certify absence or uniqueness; protected values cannot be probed by
equality. The result distinguishes satisfaction, timeout, cancellation, root/session end and
semantic failure. A transport request deadline can end a longer core wait with `DeadlineExceeded`.
Client cancellation sends a control frame and allows a bounded two-second response/cleanup grace.
On a lost connection the client closes the stream, and the server cancels outstanding work.

Timeout and cancellation cannot preempt an application getter or action that blocks the UI thread.
Shutdown and observation cleanup require that dispatcher to continue running. Keep custom peers
prompt and await cleanup before closing the dispatcher.

`CheckpointAsync` always queues a normal-priority dispatcher turn, after earlier work at that
priority. It is not global application idle and says nothing about future timers, network I/O,
GPU rendering or arbitrary Task completion. AsyncCommand completion is observed through an
application-exposed semantic postcondition, not a global command completion bus.

## Disconnect, shutdown and crash

Client `DisconnectAsync`/`DisposeAsync` closes only its connection. EOF cancels the server-side
wait/request, and the server releases the controlling slot after canonical cleanup. Reconnection
may briefly report Busy while that cleanup completes. Client disconnect completion denotes local
stream closure, not a remote acknowledgement of UI cleanup.

Server shutdown cancels listeners, connections and waits, removes discovery/auth files, and waits
for cleanup while retaining the borrowed core session. Clients see `SessionEnded` or
`ApplicationUnavailable` if the connection disappears; mutations retain OutcomeUnknown when
appropriate. A process crash similarly terminates waits, and the next discovery removes stale
records. No mutation is replayed after reconnect.

## CLI

Build/run `ModernFormsNext.Automation.Cli` or invoke its generated executable. It is a nonpackable
repository client, not a globally installed tool. The usage name `mfn-automation` below is a shell
alias for that executable. `--json` emits machine-readable JSON; list/tree also offer readable
text. Other successful commands emit compact JSON in either mode. Error text is always controlled.

```text
mfn-automation list --json
mfn-automation info --instance INSTANCE --json
mfn-automation connect --pid PID --json
mfn-automation roots --instance INSTANCE --json
mfn-automation tree --instance INSTANCE --root ROOT --depth 3
mfn-automation find --instance INSTANCE --root ROOT --automation-id save --json
mfn-automation inspect --instance INSTANCE --root ROOT --session SESSION --node NODE --json
mfn-automation action --instance INSTANCE --root ROOT --session SESSION --node NODE --action Invoke --json
mfn-automation action --instance INSTANCE --root ROOT --session SESSION --node NODE --action SetValue --value-stdin --json
mfn-automation wait --instance INSTANCE --root ROOT --condition ValueEquals --automation-id status --equals Completed --timeout-ms 10000 --json
```

Pass text action values through stdin; they are bounded to 4096 characters and never echoed.
`--number` accepts an invariant finite numeric value. Wait equality can also read stdin using
`--value-stdin`; avoid putting confidential text in shell arguments. Root is optional only when
the application exposes exactly one root. Handles require both `--session` and `--node`.
Tree depth defaults to three and is limited to 0–32; core result/traversal limits still apply.

Exit codes: `0` success/satisfied, `2` invalid usage or ambiguous/missing selection requirements,
`3` semantic failure/incomplete capture/unsatisfied wait, `4` transport/discovery failure or
ambiguous instance selection, `5` OutcomeUnknown, `130` cancellation. CLI does not guess between
same-name applications or automatically retry actions.

## Validation and deferred scope

`ModernFormsNext.Automation.Windows.TestHost` is a dedicated native Windows executable with Form,
DelegateCommand/AsyncCommand buttons, ordinary/password text boxes, checkbox, list, tree and status.
Tests launch it as a separate process, discover/authenticate through real pipes and exercise semantic
actions/waits without UIA, mouse/keyboard coordinates or screenshots. CLI tests launch additional
client processes and save nonsecret smoke output under ignored `artifacts/issue-97-phase1b/cli`.
Application-owned fixture gates complete async commands without fixed client sleeps. The no-start
fixture retains the adapter reference and proves default-off in whichever configuration is tested.

The Windows test suite covers framing, limits, negotiation, stale/PID-reuse descriptors, bad/missing/
stale tokens, instance/version mismatch, actual ACL denial, remote-path probing, privacy, root close,
disconnect, server stop, process kill and unknown mutation outcomes. Neutral wait tests cover the
subscription race, reconciliation, immediate predicates and actual subscription cleanup. These are
automated semantic/native-process checks, not manual visual or accessibility-tool validation.

Issue #97 remains open. Phase 1c acceptance/integration work is separate; any broader event history,
screenshots, diagnostics, alternate transports or application-specific policy remain deferred.
No MCP SDK, server, tool, package or repository is added. A later optional
`ModernFormsNext.Automation.Mcp` adapter must consume the proven client contracts separately.
