# AppShell routes and lifecycle

`AppShell` is a lightweight `Page` with an instance `RouteTable` and an ordinary retained
`NavigationPage`. Host it in an existing `PageHost` inside a Form or `SkiaControlSurface`.
It uses the existing focus, validation, resource inheritance, routed commands, dispatcher and
animation scheduler. Windows remains the primary platform; Android's Application/Form backend
is experimental. Designer/template integration is a subsequent slice.

```csharp
var shell = new AppShell();
using var documents = shell.Routes.Register("documents/{id}", route =>
    new ContentPage
    {
        Title = "Document " + route.Parameters["id"],
        Content = new TextBox { Text = route.Parameters.GetValueOrDefault("mode", "view") }
    });
form.Controls.Add(new PageHost { Page = shell, Dock = DockStyle.Fill });
await shell.GoToAsync("/documents/42?mode=edit");
```

The example requires the existing application UI thread. Retain registration leases for as long
as routes should exist; disposing a lease unregisters that exact registration. `Unregister(pattern)`
removes an exact normalized pattern, including parameter names. `Clear()` removes all registrations.
Registration/removal commit before owning shell command observers run; observer exceptions propagate
without undoing committed changes. Exact-pattern removal also permits recovery when a registration
observer throws before its lease can be returned. Factories and returned pages are borrowed until
canonical admission; removing a route never disposes existing pages or application delegates.

## Logical route contract

- Patterns contain literals or whole-segment ASCII identifier parameters such as `{id}`. There
  are no wildcards, optional segments, regex, reflection, executable strings or global registration.
- Matching is ordinal and case-sensitive. At the first differing segment a literal beats a
  parameter. Equivalent parameter shapes are rejected independent of names or registration order.
- Absolute paths resolve from the logical route root. Relative paths append to the **entire**
  current logical path; `..` removes its last segment. Both forms push ordinary navigation history.
  Absolute syntax does not reset history. The previous query is not inherited.
- Empty/trailing path segments normalize away. Strict UTF-8 percent decoding happens once.
  Malformed escapes, invalid Unicode, controls, encoded path separators, fragments, authority-style
  `//` and traversal above root are rejected. Query `+` means a literal plus; use `%20` for spaces.
- Query keys are sorted ordinally in canonical text. Duplicate keys and conflicts with path
  parameter names are rejected. `RouteContext.Parameters` is a detached immutable string map.
- A table accepts at most 256 registrations, 4096 UTF-16 units per request/normalized route,
  32 path segments, 32 combined parameters, 128 units per key and 4096 per decoded value.
  Bounds apply before execution. `Match` returns data without calling any factory.

Unknown routes throw `KeyNotFoundException`; invalid syntax/bounds throw `ArgumentException`.
Both are checked before replacing pending work. Reaffirming the exact canonical current route
cancels older work and retains the current page without invoking its factory again.

## Navigation, ownership and Back

Use `GoToAsync`, `GoBackAsync`, or the ordinary `NavigateCommand` (string parameter) and
`BackCommand`. Commands use the existing `AsyncCommand` observation and single-flight contract;
direct navigation can supersede an older request. Factories start on the UI thread and may return
a `Task<Page>` with cooperative cancellation. Background work must not access controls.

The newest valid intent, Back, host retirement, registration retirement or disposal prevents older
uncommitted work from admission. The shell permits at most 128 unfinished requests and 32 nested
synchronous callbacks. A superseded uncooperative application factory still occupies its slot until
it finishes. Its late page remains caller-owned and is never silently disposed by the framework.

Canonical validation can veto admission. `GoToAsync` returns false after veto/supersession and true
only while its target remains current through completion callbacks. Caller cancellation before
commit cancels its task; commitment is never rolled back. Callback exceptions after commitment
propagate while preserving actual ownership. `CurrentRouteChanged` follows stack commitment and
metadata synchronization; callbacks can navigate again. `Navigated.RemovedPages` and the page
returned by `GoBackAsync` transfer references for an explicit application reuse/disposal policy.

Inner `NavigationPage` and flyout bindings win before the outer shell on the canonical input route.
At root, an uncommitted factory can consume Back for cancellation. With no pending admission or
inner history, root Back reaches the existing Form close/veto contract. Native window deactivation
does not make retained pages disappear. Hide, disconnect, reparent and disposal retire older host work.
`TransitionDuration` uses the existing incoming navigation effect in milliseconds, including native
reduced motion. No separate focus tree, timer, runtime or native page window is created.

## Explicit lifecycle and URI readiness

```csharp
using var links = shell.AttachLifecycle(
    Application.Lifecycle, new Uri("example://app/documents/"), "main", includeState: true);
// The application/backend supplies explicit URI/Protocol notifications through its existing provider.
// Await/catch links.LastOperation when present; OperationFailed reports current UI-thread failures.
```

This opt-in lease borrows the current rich lifecycle notification provider, including a read-only
application override. Legacy providers lacking rich notifications are unsupported. A shell permits
one lease; dispose it on its creating UI thread before provider replacement. Constructors register
no protocols or lifecycle handlers. Binding installs no service, persistence, IPC, browser, permission,
OS protocol handler or window activation. Native URI transport remains the app/backend's responsibility.

Only URI/Protocol activation with the explicit scheme, host, port and whole normalized path base
maps to logical routes. Foreign bases are ignored; fragments and invalid matching route data fault
the observed operation. At most one latest data notification and one dispatcher ticket are queued.
Positive stale/duplicate provider sequences are ignored. Later explicit user navigation wins over
queued delivery. Startup activation can survive its first mount; hiding a previously presented shell
retires pending delivery. Background/suspension defers newly received data and cancels running work;
canceled running work is not automatically replayed. Exit cancels/drops work. Newer activation,
provider/runtime change or host retirement cannot let an old factory reclaim ownership.

`LastOperation` exposes the most recently accepted operation from any thread; superseded queued work
completes false. Running cooperative cancellation cancels its operation task. Automatic errors are
internally observed and available through this faulted task and `OperationFailed`. Errors from that
observer aggregate with the original error. Diagnostics/failure metadata omit activation payloads.

## Bounded route-only state

```csharp
var saved = shell.CaptureState("main");
// The application explicitly stores/transports this existing PlatformApplicationStateData if desired.
var fresh = new AppShell();
fresh.Routes.Register("documents/{id}", route => CreateDocument(route.Parameters["id"]));
bool restored = await fresh.RestoreStateAsync(saved, "main");
```

State schema 1 contains `mfn.shell.<key>.count` and numbered canonical route strings. The explicit
key is at most 64 ASCII letters/digits/underscores/hyphens, starting with a letter or underscore.
At most 32 outer visits and the existing state handoff limits (64 entries, 4096 units per value,
65536 total UTF-16 units including keys) are accepted. No editors, nested control state, objects,
delegates or native handles are serialized. Route strings can contain private data: protect any
application persistence accordingly. Over-budget capture throws; data is never silently truncated.

Restoration applies only to a fresh empty shell. A populated retained shell preserves its user's
history across Activity recreation. All schema, namespace, bounds, canonical strings and registrations
are checked before any factory runs. Unsupported/malformed snapshots return false. Valid factories
then use the ordinary navigation queue; a fault or newer callback may leave a committed prefix,
which is retained instead of rolling back newer application state. Only the last visit may animate
when explicitly requested. Caller cancellation cancels restoration without reclaiming borrowed pages.

Lifecycle state integration is separately enabled by `includeState: true`. Earlier non-null
`StateSaving.Data` is preserved; later application assignments remain authoritative. Independent
shells need distinct keys; the existing one-data handoff still follows subscription order rather
than implicitly merging application payloads. A null link base permits state-only integration.

Use **ControlGallery → AppShell routes** for explicit routes, parameters, validation, Back,
route-state capture and example lifecycle delivery. The example link button delivers canonical
data locally; it does not demonstrate external OS protocol registration. Native certification,
emulator, physical-device and human IME/accessibility evidence are separate qualification lanes.

## Native qualification entry points

The Windows native test host accepts `--app-shell`; its finite scenario targets only its own HWND.
It covers actual text-method handoff, nested Back, a pending root factory, lifecycle data, fresh
restoration, 100 push/pop cycles, deactivation and Form close veto. The ordinary Windows test row
keeps the existing 30-second host budget. Android's opt-in instrumentation adds actual native
InputConnection, system Back, Home/resume, rotation and Activity recreation with two captured frames.
It preserves the existing 15-second per-condition and release-runner timeout budgets.

```powershell
dotnet ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost/bin/Release/net10.0-windows/ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll --app-shell
./scripts/android/Invoke-ReleaseValidation.ps1 -Scenario AppShell `
  -DeviceId '<explicit authorized serial>' -ApkPath '<signed standalone Release APK>' `
  -OutputDirectory '<new evidence directory>'
```

Run emulator and physical-device lanes independently. The existing Android runner records the exact
APK/source identity, device properties, settings before/after, native report and frames. These entry
points describe coverage, not a claim that a particular source/host has passed. Real human keyboard,
vendor IME, TalkBack speech and external OS protocol delivery require their own evidence.
