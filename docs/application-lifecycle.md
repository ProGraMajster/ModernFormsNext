# Application lifecycle, activation and host recreation

`Application.Lifecycle` exposes the lifecycle provider already used by the platform backend and
animation scheduler. Applications can observe state, receive activation, and explicitly hand off
small versioned state. The API does not create another message loop, serialize controls, or choose
an application storage format. These additions are in the current source tree; this change does
not publish a package or change its version.

Use `ModernFormsNext` for the facade and lifetime policy, and
`ModernFormsNext.WindowKit.Backend.Lifecycle` for the shared immutable data and event types.
Access the facade, subscribe, and deliver data on the owning UI thread. Keep callbacks short.
Providers implementing only the older `IPlatformApplicationLifecycle` contract remain compatible
with coarse consumers. The facade's explicit delivery methods throw `NotSupportedException` if
the current provider lacks the richer controller capability.

Dispose owned controls explicitly on their UI thread. A control finalizer must not close UI
popups, dispose managed children or retire text-input sessions. For the ownership rules,
custom-control disposal pattern and audited exceptions, see the
[control finalization and ComboBox disposal audit](development/issue-149-disposal-audit.md).

## Initialize a form before its first display

Subscribe to `Form.Load` or override `OnLoad(EventArgs)` (calling base) for synchronous UI-thread
initialization after construction and initial managed layout, but before native display.
Both `Show()` and `ShowDialog(owner)` use the same preparation:

```text
initial layout -> Load -> layout after Load -> startup positioning
-> Visible = true -> VisibleChanged -> native Show -> Shown
```

Changes to controls, Size and StartPosition in Load participate in the final layout and centering.
The native window may already exist before Load. Neither Load nor Shown means first-paint
completion; activation may occur synchronously during native Show.

```csharp
var form = new Form { StartPosition = FormStartPosition.CenterScreen };
form.Load += (_, _) =>
{
    form.Controls.Add(new Label { Text = "Ready", Dock = DockStyle.Fill });
    form.ClientSize = new System.Drawing.Size(640, 400);
};
Application.Run(form);
```

Load runs at most once per instance. Repeated Show and Hide/Show do not repeat it or reset Shown.
A Show called reentrantly during preparation is ignored. Hide in Load cancels that display
attempt; a later explicit Show can display the initialized instance. In a modal attempt, that
cancellation completes the task with the current DialogResult and leaves the owner available.
A canceled Closing during Load allows showing to continue; a successful Close or modal
DialogResult prevents display and completes modal cleanup. Close remains terminal.

A Load exception propagates synchronously, with no native Show, Shown, or OpenForms registration.
The form keeps its user controls; it does not dispose them as error recovery. Later Show or
ShowDialog attempts throw InvalidOperationException with the initialization error as the inner
exception instead of rerunning Load. Use a new instance to retry. A modal owner is not disabled
or stripped of its text-input session until Load succeeds. A preassigned non-None DialogResult
still completes ShowDialog without initializing or showing the form.

Load does not await async void handlers. Async initialization is an application concern.
Designer document opening, metadata discovery and preview do not execute the designed form's
Load. This managed Form lifecycle is shared by Windows and the headless test backend; it does
not map to Android Activity/View lifecycle or change the Android surface host.

## Observe window visibility

`WindowBase.VisibleChanged` is inherited by Form and PopupWindow. It reports actual changes
of the public `WindowBase.Visible` value on the UI thread. The handler already sees the new
value. Repeated Show on a visible window and Hide on a hidden window do not emit duplicates.

```csharp
form.VisibleChanged += (_, _) =>
{
    statusLabel.Text = form.Visible ? "Window visible" : "Window hidden";
};
```

The normal order is:

- Show/ShowDialog: preparation (including first Form.Load), `Visible = true`, VisibleChanged,
  native Show, then the existing one-shot Shown.
- Hide: retire text input, `Visible = false`, VisibleChanged, native Hide and popup input-owner
  restoration. A later Show emits true again without repeating Load or Shown.
- Programmatic or native close: commit terminal state and `Visible = false`, finish mandatory
  input cleanup, emit VisibleChanged if the value changed, then Closed and modal completion.
  Closing an already hidden window does not emit another false event.

A callback may Hide, Show or Close. Show called from a true visibility callback is ignored;
Hide/Close there cancels the pending native show. Show called from a false Hide callback can
supersede the older Hide, which must not hide the newer display. An existing recursive Hide
is ignored while Hide is already unwinding. Remaining observers of a superseded transition
are skipped, so they are not called with the opposite current value.

Observer exceptions are collected using the existing window lifecycle policy. Failed show
notification rolls back its unfinished display (including a false notification), without
disposing user controls or resetting successful Load. Hide and close complete mandatory
cleanup before propagating observer errors; multiple failures are aggregated. A canceled or
failed initial modal show restores its owner and completes modal cleanup. Hiding an already
established modal dialog retains the existing modal operation until it is closed.

Overrides of `OnVisibleChanged(EventArgs)` must call base. This event describes managed window
visibility, not HWND creation, first paint, activation, minimization or Control.Visible. It
does not propagate a new event through the control tree or map to Android Activity/View events.
Designer event discovery/generation uses the existing metadata mechanism and does not execute
the designed form's lifecycle callbacks.

## Observe window geometry

`WindowBase.Resize`, `SizeChanged` and `LocationChanged` are inherited by Form;
`Form.ClientSizeChanged` observes the usable area after managed chrome. Each event uses
`EventHandler` and has a protected `On... (EventArgs)` hook whose overrides must call base.

```csharp
form.SizeChanged += (_, _) =>
{
    // Size and the normal Dock/Anchor layout already reflect the actual backend result.
    statusLabel.Text = $"Window: {form.Size}; client: {form.ClientSize}";
};
form.LocationChanged += (_, _) => SaveWindowPosition(form.Location);
form.ClientSizeChanged += (_, _) => RefreshClientArea(form.ClientSize);
```

Size and ClientSize are integer logical pixels. Location retains physical screen pixels,
including negative monitor coordinates; Bounds combines that location with a logical size.
The backend remains the geometry source: requests are not reported until actual geometry
is available. The common path reads its current properties rather than trusting an older
Resized/PositionChanged payload (Windows WM_MOVE coordinates also differ from public Location).

For a size change the order is **backend state -> adapter/client layout -> Resize ->
SizeChanged -> ClientSizeChanged**, with the last event only if ClientSize changed. A combined
size/move update then publishes LocationChanged. Root layout must finish and resume before
these callbacks; explicitly suspended root layout coalesces changes until resumed. Layout
explicitly suspended by application code in a child remains subject to that child's normal
SuspendLayout/ResumeLayout contract.

No-op setters and duplicate backend callbacks do not emit events. Resize describes an actual
Size change after layout, not every layout pass. ClientSizeChanged also occurs after title-bar,
decoration or border layout without an outer Size change; an unchanged ClientSize emits nothing.
Style objects are passive: directly edited borders are observed when the next root layout
applies them, including first-show preparation or a backend geometry callback. A drawable area
smaller than managed chrome lays out an empty client area, never negative-sized user content.

Geometry callbacks run synchronously on the UI thread, including real changes during Load and
startup centering before VisibleChanged/native Show. Load and Shown remain one-shot. Reentrant
geometry, Hide or Close supersedes remaining callbacks for the older transition; no-op geometry
does not recurse. State and layout commit before observers. Observer errors are collected and
propagated after the other still-current notifications; multiple failures are aggregated. This
preserves the existing calling path and native backend exception policy rather than introducing
a new exception dispatcher.

ScalingChanged and native resize/move may arrive in multiple steps. Each geometry event exposes
the geometry/layout committed for that step; there is no new DPI event or global DPI/geometry
transaction. A scale-only change with unchanged logical Size does not emit Resize/SizeChanged.
Windows (including real HWND move/resize) and headless TestHost are covered. These Form events
do not describe Android Activity, orientation or SkiaControlSurface lifecycle. Designer metadata,
handler generation and document round-trip discover them without running user lifecycle code.

## Observe normal, minimized and maximized state

`Form.WindowStateChanged` uses `EventHandler<WindowStateChangedEventArgs>` and a protected
`OnWindowStateChanged` hook. Overrides must call base to notify subscribers. The immutable
event data contains `OldState` and `NewState`, both `FormWindowState` values:

```csharp
form.WindowStateChanged += (_, e) =>
{
    // The backend has confirmed the transition; form.WindowState equals e.NewState.
    statusLabel.Text = $"Window state: {e.OldState} -> {e.NewState}";
};
```

The backend remains the state source. Both programmatic requests and native/system actions
use its existing state callback. No-op assignments and repeated confirmations raise no duplicate.
OldState is the previous confirmed state reported by this form (initially the newly created
backend's state), not an intermediate configuration that was never applied to a displayed window.

While hidden, including before the first Show, the existing WindowState getter exposes the
requested state for the next display. Setting it raises no state event yet. The backend confirms
the actual state during Show; a confirmation may also be needed when re-showing does not generate
a native state message. For example, configuring Maximized before the first Show produces one
Normal-to-Maximized notification when applied. Changing the configuration back to Normal before
Show produces none. Native maximize/minimize state is retained across Hide/Show until another
state is requested. Show confirmations of an already reported state are deduplicated.

Notifications run synchronously on the UI thread after state commit. Windows WM_SIZE normally
delivers applicable resize/layout events before WindowStateChanged, but minimization can confirm
state without a regular resize callback. There is no cross-backend atomic state/geometry/DPI
transaction or universal event order. WindowStateChanged does not mean activation or visibility
changed. Size/ClientSize remain logical pixels and Location remains physical screen pixels.

Load, VisibleChanged and one-shot Shown retain their contracts: a preconfigured state can be
confirmed during native Show, after Load and the true visibility notification, before Shown.
Reentrant state changes, Hide or Close stop remaining obsolete observers. Hide/Close during the
initial native Show cancels that display and retires its modal attempt. Hiding an already established
modal session retains its existing ownership contract until it is closed; this event does not
redesign Hide versus Close. Observer failures preserve the committed state. Other still-current
observers run before errors propagate, with multiple errors aggregated. A synchronous failed show
retires its display and modal ownership; the existing native callback exception policy is unchanged.

The headless host confirms states without inventing geometry changes. Windows tests use real HWND
system commands for minimize/maximize/restore with custom and system decorations. Designer discovers
the event and generates its typed handler through normal metadata reflection. Android Activity,
orientation and SkiaControlSurface lifecycle are outside this Form contract; no Android Form-state
support, public DPI event, activation API or fullscreen state is added here.

## Observe DPI and rendering scale

`Control.DpiChanged` and `WindowBase.DpiChanged` (inherited by Form) expose the existing
backend `ScalingChanged` lifecycle. Both use immutable `DpiChangedEventArgs`: `OldScale` and
`NewScale` retain the backend's double precision; `OldDpi` and `NewDpi` multiply those values
by 96 and truncate exactly like `Control.DeviceDpi`. For example, 1.25 means 120 DPI and 2.25
means 216 DPI. A fractional scale change can notify even when its integer DPI stays the same.

```csharp
form.DpiChanged += (_, e) =>
    statusLabel.Text = $"Scale: {e.OldScale:P0} -> {e.NewScale:P0}";
customControl.DpiChanged += (_, e) =>
    RebuildDeviceCache(e.NewScale); // customControl.DeviceDpi already equals e.NewDpi
```

The backend remains the source of truth. Identical confirmations, first Show without a scale
change, and moves at unchanged DPI raise no DPI event. This is not monitor, activation, visibility
or window-state notification. Windows commits its scale, applies the suggested physical rectangle
(which can trigger geometry callbacks), then reports ScalingChanged. No second scaling operation
or cross-platform ordering of geometry versus DPI is introduced. Logical bounds and font sizes
remain logical; Location remains in physical screen pixels.

Control propagation keeps the existing parent-first traversal, in collection order (explicit
children followed by implicit chrome). For each current control it releases the back buffer and
preferred-size cache, calls the existing protected `OnDpiChanged(EventArgs)` hook, then visits its
children. Overrides keep their signature and must discard their own DPI caches before calling
base. The base hook invalidates rendering, requests layout and raises the typed public event.
Children have not necessarily completed their own hooks when the parent event runs. Explicitly
suspended layout remains deferred, just as before. The window event follows the subtree update
and window layout request; the internal ControlAdapter emits no extra public DPI event.

Child snapshots and parent-generation checks skip detached, disposed or obsolete routes, including
detach-and-reattach during a parent callback. Moving an already notified child into a later subtree
does not notify it twice. A newer DPI transition supersedes older traversal/observers; resize from
a handler retains the new geometry. Hide/Close retires subsequent window observers. Errors do not
roll back DPI: still-current descendants complete their cache hooks and layout before failures
propagate using the existing exception policy. Derived hooks remain responsible for their own caches.

Headless tests use the existing render-scale capability. Windows tests inject WM_DPICHANGED into
real HWNDs with custom and system decorations; this verifies the native message path, not physical
multi-monitor behavior. Manual multi-monitor verification is separate. Designer discovers both
typed events and generates handlers without executing user code. AndroidSkiaHostView/SkiaControlSurface
density is not bridged to these notifications here; there is no Android WindowBase parity or
WinForms child-HWND BeforeParent/AfterParent phase.

## Read the right state

| Value | Meaning |
| --- | --- |
| `Snapshot.Phase` | Application phase: `NotStarted`, `Starting`, `Running`, `Suspended`, `Exiting`, or `Exited`. |
| `Snapshot.State` | Existing coarse availability: `Unknown`, `Foreground`, `Background`, or `NoHost`. This remains the scheduler's lifecycle input. |
| `Snapshot.IsActive` | Backend-reported application activity. It is independent of an individual Form's keyboard focus. |
| `Form.IsActive` | Activity of that particular window, reported by its backend. `Activated` and the existing `Deactivated` event observe window changes. |
| `Snapshot.HostCount` | Native hosts tracked by the provider, rather than the number of Forms in a shared control tree. |
| `Snapshot.HostGeneration` | Monotonic host generation within this provider's lifetime. A replacement Android Activity can advance it without restarting the application. It is not a durable process identifier. |

`Running` with an inactive desktop application is valid. Switching to another desktop application
does not by itself suspend this application's animations. Switching between this application's
windows can change window activity while application activity remains unchanged. An Android
surface may have a native host and no Forms at all. Do not derive one of these values from another.

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

var form = new Form { Text = "Lifecycle example" };
var status = form.Controls.Add(new Label { Width = 400 });
Application.Lifecycle.LifecycleChanged += (_, e) =>
    status.Text = $"{e.Current.Phase}: {e.Current.State}";
form.Activated += (_, _) => status.Text = "This window is active";
Application.Run(form);
```

The existing scheduler continues to consume the canonical coarse `StateChanged` event. App code
does not need a second pause/resume policy for ordinary framework animations. Native resources
owned by an application can use these hooks while retaining their own platform lifetime rules.

## Choose when the application loop exits

The existing `Application.Run(form)` behavior remains the default. An additive overload accepts
`ApplicationLifetimeMode`:

| Mode | Exit trigger |
| --- | --- |
| `MainWindowClosed` | Closing the designated Run root requests exit. Other application-owned Forms are not automatically disposed. |
| `LastWindowClosed` | A Form closes and `Application.OpenForms` becomes empty. A shown Form that is subsequently hidden still counts; popups do not count as Forms. |
| `Explicit` | `Application.Exit()` or platform termination ends the loop. Closing the initial Form alone does not request exit. |

```csharp
using ModernFormsNext;

var main = new Form { Text = "Main" };
var tools = new Form { Text = "Tools" };
main.Shown += (_, _) => tools.Show();
Application.Run(main, ApplicationLifetimeMode.LastWindowClosed);
```

Call `Run` once per application runtime on its UI thread. The `ICloseable` overload supports a
custom lifetime root without showing or disposing it; `LastWindowClosed` still observes Form
closure independently of that root. Host Activity recreation does not start another application
loop. `Exit` requests graceful loop shutdown; it does not terminate the process or dispose every
application-owned Form.

## Activation is explicit data

`ActivationReceived` supplies a `PlatformApplicationActivation`. Its `Kind` is `Launch`,
`Arguments`, `Files`, `Uri`, `Protocol`, `Notification`, or `SecondaryInstance`. `Arguments` and
`Files` are copied string collections; `Uri` is an absolute `System.Uri` when applicable.
The payload does not grant file access, execute commands, navigate a browser, or activate a window.

For an application-owned transport, forward the decoded request on the UI thread:

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

var lifecycle = Application.Lifecycle;
PlatformApplicationActivation? pendingActivation = null;
lifecycle.ActivationReceived += (_, e) => pendingActivation = e.Activation;

lifecycle.DeliverActivation(new PlatformApplicationActivation(
    PlatformActivationKind.Arguments, arguments: ["--page", "settings"]));
lifecycle.DeliverActivation(new PlatformApplicationActivation(
    PlatformActivationKind.Protocol, uri: new Uri("example://document/42")));
```

Repeated activation requests remain distinct. `LastActivation` retains the latest explicit
payload for deliberate application use; subscribing later does not replay earlier events.
`DeliverActivation` uses the same provider as native delivery. It supplies no single-instance
IPC, shell association registration, notification transport, or permission mechanism.

Payloads are bounded: at most 64 arguments and 64 file identifiers, 4096 UTF-16 characters per
item, and 65,536 characters across the activation payload. File identifiers can be native paths
or document/content URI strings. Interpret them using the originating platform's access rules.

## Save and restore a small application-owned schema

`StateSaving` and `StateRestoring` exchange `PlatformApplicationStateData`: a positive integer
schema `Version` and a copied, read-only string dictionary. Reasons are `Suspend`, `Recreation`,
and `Exit`. Store identifiers and simple values that the application can validate and rebuild.

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

string currentPage = "home";
var lifecycle = Application.Lifecycle;
lifecycle.StateSaving += (_, e) =>
    e.Data = new PlatformApplicationStateData(1,
        new Dictionary<string, string> { ["page"] = currentPage });
lifecycle.StateRestoring += (_, e) =>
{
    if (e.Data.Version == 1 && e.Data.Values.TryGetValue("page", out var page)
        && page is "home" or "settings")
        currentPage = page;
};

// An explicit host handoff, performed on the UI thread outside a lifecycle callback.
PlatformApplicationStateData? saved = lifecycle.SaveState(PlatformApplicationStateReason.Recreation);
if (saved is not null)
    lifecycle.RestoreState(saved, PlatformApplicationStateReason.Recreation);
```

The application owns schema migration and the behavior for unknown versions. Convert numeric
strings with an explicit culture and validate restored values. No control tree, arbitrary CLR
object graph, native handle, or delegate is serialized. A state object permits at most 64 entries,
128 characters per nonblank key, 4096 per value, and 65,536 combined characters.

Saving is synchronous. Set `e.Data` during the callback; it becomes read-only after delivery.
When several subscribers supply data, the last successful assignment wins, so a single
application-level coordinator is usually the clearest owner. `async void` cannot defer the save
response. `SaveState` returns null if nobody supplies state, or after terminal exit. The facade
performs no persistence, encryption, or file I/O. Persist important data during normal operation;
shutdown callbacks alone are not a durability guarantee.

## Callback ordering and reentrancy

The provider commits a snapshot, raises the existing coarse event if its state changed, then
raises `LifecycleChanged`. The rich event includes immutable `Previous`, `Current`, and a
monotonic `Sequence`. Duplicate snapshots and stale host generations are suppressed. Backends
can omit intermediate states; consumers must not require every platform to emit the same trace.

`Application.Run` requests `Starting`, initial activation, and `Running` before showing its Form.
An exit requested during startup prevents later startup steps. Subscriptions and lifetime
handlers are attached before showing, so closing from `Shown` is supported. A native host can
deliver activation independently of `Run`; applications should initialize routing before the
host delivers requests and may inspect `LastActivation` when attaching later.

Reentrant activation, restoration, and snapshot publications are queued until the current
notification finishes. A synchronous `SaveState` inside a lifecycle notification is rejected;
post the complete save operation to the UI dispatcher instead. Do not start `Application.Run`
inside a lifecycle callback; post startup if necessary.

`Application.Exit()` is idempotent. When called inside a lifecycle notification, it latches the
exit request immediately and defers the shutdown transaction until the notification unwinds.
Normal application shutdown publishes `Exiting`, requests state for `Exit`, releases owned
runtime bindings/animation work, raises `Application.OnExit`, cancels the loop, and publishes
`Exited`. A backend that already requested exit can have saved state at its earlier native
shutdown notification. Backend terminal notifications also request shutdown of the existing
application loop. No subsequent activation resumes an exited provider.

Observer failures do not skip remaining observers or mandatory shutdown cleanup. Synchronous
callers receive the failure; multiple failures can be aggregated. Posted failures follow the
dispatcher error path. Native callback adapters report failures through backend diagnostics
instead of unwinding an operating-system callback. A failing save can still lose unsaved state.

## Android host recreation and Windows events

The Windows implementation maps window lifetime, application activation, power suspend/resume,
and session-end messages into the existing provider. Ordinary desktop deactivation changes
application activity while leaving coarse availability in `Foreground`. Session-end intent
requests graceful application exit. File/URI interpretation and custom transports remain the
application's responsibility.

The Android implementation aggregates Activity callbacks and weak host identities. Stopping or
destroying one Activity does not necessarily remove the application's other hosts. Destroying
an Activity is not application exit. A replacement Activity advances host generation while the
existing process and application-owned model may survive.

Android's saved-instance-state callback requests `Recreation` data and writes the bounded schema
to its Bundle. The first Activity creation in a cold process consumes any supplied Bundle before
subsequent activation and resumed interaction. Recreation within the surviving process preserves
the current application model and does not restore a potentially older Activity Bundle over it.
An Activity replacement with saved state also avoids replaying its original launch Intent.
In-memory state alone does not survive process death, and restoration depends on Android actually
supplying previously saved state. Android does not guarantee `OnDestroy` or a final save before
termination.

For subsequent Intents, forward `OnNewIntent` from the Android Activity using
`AndroidWindowKit.HandleNewIntent(this, intent)` after the normal base callback. Initial Activity
activation and saved-state callbacks are observed by the backend. The mapper preserves shared
stream/content/file identifiers as `Files`, HTTP(S) as `Uri`, and other absolute schemes as
`Protocol`. Android permission grants and stream access remain native responsibilities.

These hooks extend the existing Android Skia host. They do not add Android desktop Form parity,
native WebView/media controls, or a guarantee that all platform integrations restore themselves.

## Safe area and the on-screen keyboard

`ModernFormsNext.WindowKit.WindowInsets` contains `SafeArea` and `Ime`, each a `Thickness` in
logical pixels. Values must be finite and nonnegative. Persistent system bars/cutouts and
temporary keyboard occlusion are separate. A backend must exclude insets already removed by
native client-area fitting, so the application does not apply the same space twice.

`Form.Insets` (inherited from `WindowBase`) is informational and raises `InsetsChanged`; it does
not change Form padding or client sizing automatically. A missing backend feature reports zero.
An embedded `SkiaControlSurface` applies its own `Insets.SafeArea` to the bounds of its borrowed
root and runs layout/render invalidation, preserving application padding:

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit;

var root = new Panel();
using var surface = new SkiaControlSurface(root);
surface.Resize(400, 800);
surface.Insets = new WindowInsets(
    safeArea: new Thickness(0, 24, 0, 16),
    ime: new Thickness(0, 0, 0, 280));
```

The safe area above constrains content; the `Ime` value does not shrink it automatically. The
application chooses whether to scroll, reposition, or resize content for keyboard avoidance.
Fractional safe-area sides round outward and clamp to the available surface size. Rendering,
input, and accessibility retain the same surface coordinate system. Disposing the surface does
not dispose its borrowed root.

The Android host reports density-converted native insets and the cross-platform sample forwards
them into `SkiaControlSurface.Insets`. API 30+ supplies typed IME insets; the API 23–29 fallback
reports persistent system insets and available cutouts, with zero IME insets. Re-read insets after
density, native surface, or configuration changes rather than retaining physical-pixel values.

## Diagnostics, tests and evidence

`Application.Lifecycle.GetDiagnostics()` returns a detached immutable snapshot with the current
lifecycle, last activation **kind**, open/active Form counts, and at most 64 recent transitions.
It excludes activation arguments, file names, URIs, and saved-state values. `LastActivation` and
explicit state delivery do expose payloads by design; avoid writing them into general logs.

The [headless TestHost](testing/testhost.md) uses this same publisher and production dispatcher.
It supports deterministic phase/activation/restoration tests and real `Application.Run` lifetime
tests. Host disposal revokes its facade and restores the borrowed application runtime; keep
detached diagnostics, rather than a facade or control from an expired scope.

Shared and headless tests establish deterministic shared semantics. Native build results,
automated emulator interaction, manual interaction, and physical-device checks are separate
evidence categories. Executed commands and the current acceptance status belong in the
[session acceptance report](development/codex-autonomous-issue-run.md).

The [1.11.0 scope-freeze audit](development/1.11.0-roadmap-completion.md#issue-63--application-lifecycle-and-activation)
checks this existing foundation against current master. Final current-APK lifecycle,
inset and stress observations are consolidated in #69. Common hooks are available
to future native-hosted products; this does not declare those products implemented
or make their implementation part of the frozen lifecycle checkpoint.

### Initial Android emulator series — 2026-09-10

Automated interaction with the existing Pixel_8 emulator exercised the cross-platform sample on
Android API 34 at 1080 × 2400 physical pixels and 420 dpi (density 2.625). The signed standalone
Debug APK was installed with `adb install -r`, preserving application data. Its SHA256 was
`B943D024A9EC424C236BFB7A52804F23513E719F5EC6E73221F9CE30416A544B`.
These observations apply to that APK. The final-source smoke rerun below identifies a separate
build and does not claim to repeat every case in this broader initial series.

The run retained screenshots, accessibility XML, process identities, native Activity/window/IME
state, and process-filtered logs. Screenshots were inspected before coordinate actions. An
artifact verification script passed **26/26 recorded-evidence assertions**; this count is separate
from the framework's unit and headless test totals.

| Scenario | Observed result |
| --- | --- |
| Launch and Home/resume | `Running` / `Foreground`, generation 1, and `Launch`; Home produced a stopped native Activity and hidden IME, and resume retained the process and generation. |
| Deep link | An explicit `ACTION_VIEW` using the sample protocol reached the existing `SingleTop` Activity as `Protocol`, preserving process and host generation. |
| Shared input and dispatch | Actual Gboard taps updated the framework TextBox; native button taps produced three shared clicks and a completed dispatcher callback reporting UI access. |
| Animation during background/resume | Five active scheduler entries were visible before Home. After resume and diagnostic refresh, the scheduler was idle with no pending Choreographer callback or scheduler demand and one active surface. |
| Activity recreation | An unhandled font-scale configuration change advanced generation 1 → 2 in the same process, preserving edited text, click count and dispatcher count. Restoring font scale produced generation 3. The recreated native input connection accepted another Gboard commit. |
| Process death and Bundle restoration | After Home, `am kill` removed the old process; resuming its retained task created a new process and restored click count 3 from the saved Bundle. Text and dispatcher count reset because the sample does not persist them. |
| Active work during repeated recreation | Theme and composed/layout animation work was observed before further Activity replacements. The surviving process reached generation 5 and returned to an idle scheduler, no pending frame callback, and one attached surface with zero active pointers. |
| Fitted system area | Native content occupied `[0,132]`–`[1080,2337]`, excluding the top 132 and bottom 63 physical pixels. The header began one 24-logical-pixel margin inside that fitted content, with no duplicate safe-area padding; the refreshed logical surface was 411 × 840. |

Captured process logs contained no fatal exception, ANR or lifecycle-callback failure, and the
final process crash buffer was empty. Temporary accessibility, touch-exploration, Activity and
font-scale settings were restored to their exact original values, including originally absent
keys. Detailed input-text diagnostics stayed disabled; only synthetic text was entered.

The evidence has these practical limits:

- Setting `always_finish_activities=1` alone did not recreate this emulator's Activity. Only the
  observed font-scale replacements count as recreation evidence. Orientation alone would not
  establish recreation because the sample handles that configuration change.
- The sample's default native `ADJUST_PAN` left its windowless focused editor behind the keyboard.
  Shared `Ime` remains informational; automatic keyboard avoidance is not demonstrated. The
  sample did not expose numeric shared inset values, so nonzero shared-inset and injected-cutout
  cases were **NOT OBSERVED**.
- Labels immediately after attachment can retain pre-paint surface/animation values. The sample's
  `Refresh lifecycle snapshot` button produced the current attached-surface and idle-frame values.
- Dispatcher callbacks executed during the sequence, but the UI did not prove that a particular
  callback remained pending across teardown. That exact ordering is **NOT OBSERVED** in this
  emulator run and retains separate deterministic-test evidence.
- The native Skia View was exercised. WebView/media/native-widget lifetime, physical-device
  interaction, TalkBack gestures, advanced IME composition, landscape, injected cutouts and
  long-duration stress were **NOT EXECUTED**.

### Final-source Android smoke rerun — 2026-09-10

A separate short smoke ran after the cleanup and property-storage corrections, against source
commit `83a2234d6a5a6b83ba228b03bba38971014127fe`. The signed standalone Debug APK's verified SHA256
was `6EEBA58B9698138016C2A4B761282BEF68C6C61C7B881C85EC0F2F2B6AF2C794`.
It was installed with data-preserving `adb install -r` on the same Pixel_8/API 34 emulator.

The final-source run passed **21/21 recorded-evidence assertions**, separately from the earlier
26/26 series and the framework test totals. It observed normal launch and a `Protocol` new
Intent, then genuine Activity recreation with active IME. Edited text survived in the borrowed
control tree, and a new native input connection accepted another real Gboard commit. A further
recreation began with five active animation entries; the same process reached host generation 5
while retaining the edited text, two shared clicks and a completed UI dispatcher callback.

After an explicit diagnostic refresh, the final surface was attached at 411 × 840 logical pixels
with zero active pointers. The scheduler reported zero active animations, no pending
Choreographer callback or scheduler demand, and one active surface. Captured process logs and
the final crash buffer contained no fatal exception, ANR or lifecycle-callback failure. All five
temporary emulator settings were restored exactly. No regression was observed in this smoke.

Cold-process Bundle restoration and the full Home/resume sequence were **not repeated** in this
short final-source run; their observed results belong to the initial APK above. The keyboard
avoidance, label refresh, unobserved pending-work/inset cases and unexecuted device/platform
coverage listed above remain applicable.
