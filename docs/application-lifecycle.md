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
