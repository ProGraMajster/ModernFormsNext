# Native system notification validation

Related: [API guide](../system-notifications.md), [compatibility/inventory](../system-notifications-compatibility.md),
[cross-platform architecture](../design/system-notifications-cross-platform.md).
Never equate native submission, synthetic COM invocation, headless tests and manual OS interaction.

## Final master integration (2026-10-07)

After the initial feature commit, master advanced to `9214c8f` with #195/#79. Its basic Android
IPlatformNotificationService is preserved and explicitly distinguished from the rich contract here.
The merge was conflict-free, including both TestPlatformServices registrations/lifetimes. The
feature diff relative to the new master remains the same 47 files. No notification implementation,
serialization, package configuration or sample source changed during this integration; only the
base integration and documentation clarification changed.

Requalification on the same Windows 11 Home 26H2 26300.9550 x64 host:

| Check | Result |
|---|---|
| Fresh restore, full Debug and Release builds | PASS; both builds 0 warnings / 0 errors |
| Complete Debug suite | PASS: **4337/4337**, nine assemblies, **0 failed / 0 skipped**, no retry |
| Notification-specific suite | PASS: **90/90** (37 common, 53 Windows) |
| Repacked five libraries, new consumer and another empty cache | PASS: one restore, Release build with 0 warnings/errors, provider/common load |
| Rebuilt App SDK / Classic template / Shell native smokes | PASS; includes retained rapid progress, replacement, history, exact and cross-group identity/removal |
| Rebuilt sample separate-process restart seed/finish | PASS: long logical ID/group, numeric/indeterminate progress and removal |
| Portable-source compilation, negative projection diagnostic, SDK-only numeric restart and Classic callback/removal probe | Initial final-review results below remain applicable; all involved feature sources and package configuration are unchanged |
| Diff whitespace, local documentation links and commit inputs | PASS; 47 feature files, no generated output/private artifacts |

The extra 118 cases relative to 4219 come from upstream #195/#79. Local evidence has the
integration-* prefix in artifacts/notifications-final, with separate TRX and consumer cache.
Manual limitations and existing real cold Send evidence remain exactly as scoped below. No
new Android native execution is claimed by this Windows integration check.

## Final review (2026-10-07)

Reviewed source on master base `cc00609880cdb2f7d0351ee7e69d2d3badd91698` plus the complete
issue #158 diff. A fresh host query reports **Windows 11 Home 26H2, build 26300.9550, x64**;
.NET SDK 10.0.401. This supersedes the earlier host label for these new runs only.
See the [20-point final review](../design/system-notifications-final-review.md) for API findings,
corrections, native boundaries and future-platform conclusions.

| Check | Result |
|---|---|
| Fresh solution restore | PASS |
| Full Debug / Release builds | PASS; both 0 warnings / 0 errors; sequential `-m:1 /p:UseSharedCompilation=false` |
| Full Debug suite | PASS: **4219/4219**, 0 failed, 0 skipped, nine test assemblies; no retry required |
| Notification-specific tests | PASS: **90/90** (37 common/testing, 53 Windows), 0 failed/skipped |
| Common sources under net8.0 / net9.0 / net10.0 | PASS; actual contracts, validation and service base compile with no package/platform dependencies; 0 warnings/errors |
| Five Release packages and dependency inspection | PASS; common Backend package has no dependencies; App SDK and WinRT stay optional with their transitive configuration assets |
| Fresh package consumer / empty package cache | PASS: one restore, Release build (0 warnings/errors), load both providers and portable contracts without automatic runtime initialization |
| Missing projection negative consumer / separate empty cache | PASS: expected actionable WindowsSdkPackageVersion diagnostic; intentional build rejection, not a successful consumer build |
| App SDK native smoke | PASS: 20 rapid updates, actual retained value 1 then .76, indeterminate, replacement, one history identity, exact removal, empty/literal reserved-group separation |
| Separate-process restart seed/finish | PASS: long logical ID/group recovered from history, numeric and indeterminate updates, exact removal |
| SDK-only numeric path / separate processes | PASS: actual retained .1 → .2 → .3 → .4, then .8 → .9 after restart; probe identity unregistered after finish |
| Classic WinRT template native smoke | PASS with existing sample identity; Show/history/exact removal and group separation |
| Classic callback regression probe | PASS: logical ID/group/payload preserved and removed subscription suppressed; **synthetic invocation of the attached native delegate**, not a user click |
| Classic group and ID removal probe | PASS: group removal preserves the same ID in another group; subsequent ID removal removes the remaining entry |
| Shell native smoke | PASS: native Show/remove; this automated run does not certify visual appearance |
| Real App SDK cold activation | Existing stage-2 user confirmation retained: Send, reply=Etap 2, choice=no. No changes to its registration/startup/serialization/lifecycle/input path in final cleanup; not repeated |
| Git diff / documentation links / commit inputs | PASS: whitespace check and local documentation targets; no build outputs, caches, private probes or machine paths in the feature diff |
| Other OS versions, fresh Classic installer/MSIX, reboot, audio/DPI/focus matrix, native Android/macOS/iOS/Linux | NOT EXECUTED — environment/manual verification unavailable |

The previous 3898/3898 baseline grows by 313 upstream tests after fast-forwarding master and eight
new notification cases in this review. The 82/82 notification baseline grows to 90/90. No skips,
retries, test thresholds or CI requirements were changed. The final full suite used frozen source
and complete Debug outputs. Before the initial feature commit, subsequent changes only completed documentation.

The current native runs prove API operations and retained OS state in the tool-host execution
context. They do not turn Accepted/history into a fresh visible-banner certification. Historical
Explorer/manual evidence below remains separate. No default-template verification is needed:
startup/templates are unchanged and the dedicated SystemNotifications sample exercises this API.

Ignored local evidence is in artifacts/notifications-final: restore/build/test logs and TRX,
package consumers and caches, portable compilation and native/callback/restart output. The SDK-only
probe was rebuilt from artifacts/notifications-stage2/sdk-progress-probe against the final sources;
its new evidence is in notifications-final. These private probes are not shipped as product assets.

## Stage 2: cross-platform contract refactor (2026-10-07)

Same development machine and base commit as stage 1 below. The OS build was not independently
re-read for this stage, so the older 25H2 host label must not be treated as stage-2 certification.
The final review above records a fresh OS query. The unpublished notification contract was
corrected before merge; Windows remains the only implemented provider platform.
The following results apply to the stage-2 sources, independently of historical stage-1 evidence.

| Check | Result |
|---|---|
| Full solution restore | PASS |
| Final full Debug and Release builds, sequential | PASS; both 0 warnings / 0 errors |
| Complete Debug suite | PASS: 3898/3898, 0 failed, 0 skipped, across nine test assemblies |
| Notification-specific tests | PASS: 82/82 (34 common/testing and 48 Windows); 31 additional cases beyond stage 1 |
| Portable contract profiles | PASS: modern/legacy Windows, future Android/iOS/macOS/Linux portal/minimal server capability combinations; these are common-contract fakes, not native providers |
| Isolated common source compile for net8.0 / net9.0 / net10.0 | PASS, 0 warnings/errors; includes contracts, validation and service base. No solution TFM changed; this does not qualify all framework dependencies for #44 |
| Optional providers / package consumer | PASS: five local packages, fresh consumer and empty package cache; one restore, Release build and provider/common-contract load. Explicit WindowsSdkPackageVersion precedes first restore; no automatic native initialization |
| App SDK native smoke | PASS: actual retained numeric/indeterminate progress values, replacement, exact removal, native history, logical reserved-name separation |
| Separate-process App SDK restart seed/finish | PASS: long logical ID/group recovered from native history; progress updates and exact removal after process restart |
| SDK-only numeric path without explicit AUMID | PASS: retained values .1 → .2 → .3 → .4, then .8 → .9 in a new process. The native counter remains provider-owned |
| Classic template and Shell native smokes | PASS; classic uses the already registered sample identity, not a fresh installed classic COM identity |
| Synthetic startup action + two inputs / warm body | PASS with Test-Activation.ps1 -StartProcessManually; explicitly bypasses Windows process creation |
| Real cold body activation after refactor | PASS: user clicked the notification; new COM-launched sample reported reply-42/body with default input values. This alone was not counted as Send/reply verification |
| Real cold Send activation after refactor | PASS: Explorer submitted the reply and exited; user expanded it, entered Etap 2, selected No and clicked Send. A new process carried the SDK COM launch marker; UI Automation read action=send, choice=no, reply=Etap 2. Native history XML had both inputs and the Send action |
| Git whitespace validation | PASS: git diff --check |
| Native Android/macOS/iOS/Linux, other Windows versions, fresh classic installer/MSIX, reboot, DPI/audio visual matrix | NOT EXECUTED — environment/manual verification unavailable |

An earlier Release attempt overlapped a source edit and compiled tests against an older Windows
assembly (missing RequiredCommonFeatures). Both configurations were rebuilt successfully after
freezing sources; the final suite used those complete outputs. No retry, skip or threshold was
added to tests. The final full suite did not require a retry.

The native investigation also found that history does not reliably return the supplied progress
sequence: SDK history reconstructs it as 1. Low positive SDK updates could return success without
changing retained values on this host. WinRT now uses documented sequence-zero always-update
semantics; the SDK-only path reserves a durable positive counter above the native entry ID and
prior reservation. Tests inspect actual retained values, not just acceptance. See the architecture
document for ordering, storage scope, error behavior and multi-process limits.

Local ignored evidence is under artifacts/notifications-stage2: final build/test logs and TRX,
common target probe, package consumer, native smoke/restart logs, SDK numeric probe, native XML,
synthetic startup and real-cold-send UI Automation output. No notification content is written by
the production sequence store. The temporary probe's native identity is unregistered after testing.

## Stage 1: historical evidence (2026-10-03)

Host: Windows 11 Home 25H2, build 26200, x64; .NET SDK 10.0.401. Work is based on commit
`1de8a6b`, with the issue #158 working diff. The initially missing official Windows App Runtime
Main 2.5.1 and Singleton 8002.5.1 packages were installed with owner authorization; the service
itself never installed them. Before that installation modern registration reported unavailable.

| Check | Result |
|---|---|
| Full solution restore and Debug build | PASS; 0 build warnings/errors |
| Full Release build / complete Debug test suite | PASS: 0 build warnings/errors; final Debug suite 3867/3867 tests, 0 skipped, including 51 new notification cases |
| Local optional-provider packages and consumer SDK configuration | PASS: both providers and their shared dependencies pack; a fresh-cache consumer restores once, compiles and loads both providers plus the new shared API. The application explicitly sets WindowsSdkPackageVersion=10.0.28000.87; NuGet supplies disabled bootstrap/deployment module initializer defaults. Omitting the projection produces the intended diagnostic |
| App SDK real Show → native history → 76% update → indeterminate → replace → single identity → remove | PASS (`--smoke`) |
| Classic template Show/remove under the sample's registered identity | PASS (`--classic --smoke`); does not certify fresh installer registration |
| Shell native Show/remove | PASS (`--shell --smoke`) |
| Synthetic native COM action + two inputs, then body callback in an already-running process | PASS; same process, UI-dispatched sample callback |
| Synthetic COM cold CoCreateInstance from the tool host's PowerShell process | FAILED in that execution context: `REGDB_E_CLASSNOTREG` although the SDK-created per-user LocalServer32 entry was present. This is distinct from the successful real Windows cold launch below |
| The identical Test-Activation.ps1 launched by Explorer in a normal desktop process | PASS: cold action + two input values, then warm body activation in the same process; orderly disposal and exit |
| Startup with SDK COM launch marker, then synthetic action + two inputs and warm body | PASS after reading AppInstance startup data; the pre-fix Release provider failed the same test by losing the first payload. This explicitly bypasses OS process creation |
| Retained reply/basic toast launched by the tool host | User reported no visible banner or Notification Center entry, despite Accepted/native history and WPN delivery events; keeping the sample open made no difference |
| Isolated Microsoft AppNotificationBuilder/manager, without ModernFormsNext, launched by the tool host | User likewise reported no visible toast; native Setting=Enabled, nonzero ID and retained history |
| The same sample EXE launched manually from Explorer, Reply + choice | PASS: the user confirmed a visible notification. Execution/registration context must be distinguished from API acceptance; no global notification settings were changed |
| Shell balloon with sample left running | PASS for visible notification, confirmed by the user; this does not qualify advanced toasts |
| Real Notification Center Send after process exit | PASS: sending only the reply via an Explorer-launched `--leave-notification` sample retained it after exit; the user entered Test 123, chose No and clicked Send. Windows created a new process with `----AppNotificationActivated: -Embedding`; UI Automation read `choice=no, reply=Test 123` from the sample label. User confirmation and screenshot agree on reply-42/send/2 |
| Visual cropping/wrapping/DPI, audio, foreground focus, MSIX, reboot behavior | NOT EXECUTED — environment/manual verification unavailable |

The native smoke is deliberately explicit App SDK by default; automatic fallback cannot turn
a failed modern registration into a modern PASS. Synthetic activation passes application payloads
directly through the real COM ABI, bypassing Shell rendering/input collection; it is not proof
that a user clicked a banner. The successful real cold launch above is separate evidence. The
initial tool-host failures must not be generalized into a Windows cold-launch defect; the
precise reason for that host-context difference was not established. No global notification
settings, COM permissions or system services were changed to obtain the successful result.

The supplied screenshot exposed a sample presentation bug: Label defaults to single-line
rendering, so input values existed in its accessible text but its second line was not drawn.
The sample now explicitly enables Multiline for status/backend diagnostics. Native UI Automation
confirmed both actual values before that presentation correction; they were not inferred from
the input count alone.

## Reproducible commands

```powershell
dotnet restore .\ModernFormsNext.slnx
dotnet build .\ModernFormsNext.slnx -c Debug --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet build .\ModernFormsNext.slnx -c Release --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test .\ModernFormsNext.slnx -c Debug --no-restore --no-build -m:1 /p:UseSharedCompilation=false
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --smoke
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --classic --smoke
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --shell --smoke
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --restart-seed
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --restart-finish
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --leave-notification
.\samples\SystemNotifications\Test-Activation.ps1
.\samples\SystemNotifications\Test-Activation.ps1 -StartProcessManually
dotnet run --no-build --project .\samples\SystemNotifications\SystemNotifications.csproj -- --history
```

The COM script requires the sample to be closed and registered at the current executable path.
Do not reinterpret a successful invocation against an already-running sample as a cold start.
If an automation/tool process reports class-not-registered while a normal desktop launch works,
repeat the identical test from a normal user terminal/Explorer and record both execution contexts.
Do not repair global Windows COM settings, weaken permissions or call a submitted/history toast
visually qualified based on that error alone.
Code-only headless coverage verifies validation, Show/Update/replace/remove, activation/lifecycle,
input copies, identity, degradation, invalid images/scenarios, cancellation/disposal and concurrent
serialized update ordering. Pure selector tests cover simulated OS builds without claiming VM coverage.
Native smoke additionally verifies that dismissing an ungrouped toast preserves another toast
with the same logical ID in a named group, including an indeterminate update when available. It also
accepts the logical group mfn.default and verifies that its hashed native group does not collide
with the native empty-group sentinel. Each run uses a fresh logical ID to remain independent of
history left by interrupted previous runs.
Both App SDK and classic checks passed after the correction. Null removal selectors are rejected
before they can turn into a destructive Clear operation.

The native regression exposed two WinRT constraints on this host: assigning an empty Group and
calling unpackaged History.Remove(tag, emptyGroup, appId) both threw ArgumentException. Hiding an
object returned from history failed with 0x803E0108 because it was not the object originally shown.
Both toast providers therefore use a stable nonempty native group for the public empty group;
nonempty logical groups, including the sentinel spelling itself, are hashed separately. Exact
dismissal never broadens to ID-wide deletion; no in-memory-only identity cache is used. The native identity policy is documented in the API guide.

The package check also caught a first-restore problem with supplying WindowsSdkPackageVersion
through NuGet props: the initial restore downloaded the SDK default projection before those props
existed, then the build expected the newer projection and failed with NETSDK1112. The packages
now require that property explicitly in the application, as shown in the quick start, and validate
the minimum version. A new consumer directory and package cache verified one restore followed
by a successful build/run with --no-restore. A separate fresh consumer without the property failed
with the actionable ModernFormsNext projection diagnostic. Local package source mapping ensured
the consumer used the working-tree framework packages rather than published older assemblies.

## Representative OS acceptance matrix

| OS/edition | Required path | Execution status |
|---|---|---|
| Windows 10 1607 LTSB/LTSC 2016, where the runtime/edition is still supported | Installed classic COM + templates + Shell | NOT EXECUTED — environment unavailable |
| Windows 10 1809 / LTSC 2019 | App SDK; missing-runtime fallback; classic | NOT EXECUTED — environment unavailable |
| Windows 10 22H2 (runtime servicing prerequisites) | Modern + classic + Shell | NOT EXECUTED — environment unavailable |
| Windows 11 21H2 / 22H2 | Modern; feature probes, disabled/Focus Assist | NOT EXECUTED — environment unavailable |
| Windows 11 24H2 | Modern + classic | NOT EXECUTED — environment unavailable |
| Windows 11 25H2 | Modern + classic + Shell | Historical stage-1 checks only; final sources not requalified on this build |
| Windows 11 26H1 | Hardware-specific branch/probes | NOT EXECUTED — environment unavailable |
| Windows 11 Home 26H2, 26300.9550 x64 | Modern + classic templates + Shell | Final native automated checks PASS as scoped above; remaining manual matrix NOT EXECUTED |
| Windows 7 / 8 / 8.1 | Separate compatibility host would be required | NOT EXECUTED — current .NET 10 runtime unsupported; no compatibility host exists in this change |

For Windows 7, a future compatibility host must isolate the Shell adapter, validate the exact
native NOTIFYICONDATA size on x86/x64, custom HICON ownership, quiet/realtime flags and callback
ordering. For 8/8.1 it must additionally qualify projection/COM availability and installed classic
shortcut/template behavior. Compiling current net10 projects is not that qualification.

## Manual procedure for each available OS

1. Record exact build, edition, architecture, runtime/App SDK package versions, package identity,
   AUMID/COM registration and notification settings. Test a non-elevated desktop app first.
2. Show a basic message, then real video hero + logo/circle + inline variants. Inspect cropping,
   alt text/accessibility, localization, long/wrapped text, 100/150/200% DPI and multiple monitors.
3. Send one download; update many percentages and indeterminate/finalizing through the semantic
   progress API. Confirm actual retained values, one history entry, no repeated banner flood,
   correct completion replacement, updates after restart, and NotFound after user removal.
   The application never supplies native sequence numbers.
4. Test body/Open/Open folder and protocol/system actions. Test reply and selection with Unicode,
   empty values and non-default choices. Confirm exactly one lifecycle payload and correct action ID.
5. Repeat activation while visible, minimized, hidden/background, then after normal process exit.
   Repeat after reboot and with another running instance. Confirm persisted download lookup,
   chosen app instance and OS foreground-policy behavior. Never execute a raw activation path.
6. Check long logical IDs across groups, per-ID removal across groups, group removal, clear and
   retained native history after disposal/restart. Check header activation separately.
7. Check silent/default/system/custom packaged audio, looping Alarm/Reminder/IncomingCall,
   Short/Long, supported Urgent/styles/tooltips, suppress popup, expiration/reboot and mirroring policy.
8. Test disabled notifications, Focus Assist, missing runtime, missing classic installer identity,
   invalid/deleted image, strict degradation and elevated process fallback. Confirm diagnostics.
9. Test clean MSIX install/update/uninstall and clean ordinary EXE install/update/uninstall. Ensure
   retained notifications launch the installed EXE, and uninstall removes only that app's identity.
10. Close while updates/callbacks are queued. Confirm no remaining sample process or notification
    HWNDs, no post-dispose callback, no synchronous UI deadlock, and no history erased by shutdown.

A visible success in one OS build must not be copied into the other rows. Record each unexecuted
check explicitly. Default DemoApp/template verification is not required: startup/templates are
unchanged; the dedicated sample exercises the notification integration.

## Files changed

The complete issue #158 feature diff contains 47 files (Windows implementation, portable refactor and final review):

- [ModernFormsNext.SystemNotifications.AppSdk/AppSdkNotificationBackend.cs](../../ModernFormsNext.SystemNotifications.AppSdk/AppSdkNotificationBackend.cs)
- [ModernFormsNext.SystemNotifications.AppSdk/AppSdkProgressSequence.cs](../../ModernFormsNext.SystemNotifications.AppSdk/AppSdkProgressSequence.cs)
- [ModernFormsNext.SystemNotifications.AppSdk/ModernFormsNext.SystemNotifications.AppSdk.csproj](../../ModernFormsNext.SystemNotifications.AppSdk/ModernFormsNext.SystemNotifications.AppSdk.csproj)
- [ModernFormsNext.SystemNotifications.AppSdk/buildTransitive/ModernFormsNext.SystemNotifications.AppSdk.props](../../ModernFormsNext.SystemNotifications.AppSdk/buildTransitive/ModernFormsNext.SystemNotifications.AppSdk.props)
- [ModernFormsNext.SystemNotifications.AppSdk/buildTransitive/ModernFormsNext.SystemNotifications.AppSdk.targets](../../ModernFormsNext.SystemNotifications.AppSdk/buildTransitive/ModernFormsNext.SystemNotifications.AppSdk.targets)
- [ModernFormsNext.SystemNotifications.WinRT/ClassicNotificationActivator.cs](../../ModernFormsNext.SystemNotifications.WinRT/ClassicNotificationActivator.cs)
- [ModernFormsNext.SystemNotifications.WinRT/ModernFormsNext.SystemNotifications.WinRT.csproj](../../ModernFormsNext.SystemNotifications.WinRT/ModernFormsNext.SystemNotifications.WinRT.csproj)
- [ModernFormsNext.SystemNotifications.WinRT/WinRTNotificationBackend.cs](../../ModernFormsNext.SystemNotifications.WinRT/WinRTNotificationBackend.cs)
- [ModernFormsNext.SystemNotifications.WinRT/buildTransitive/ModernFormsNext.SystemNotifications.WinRT.targets](../../ModernFormsNext.SystemNotifications.WinRT/buildTransitive/ModernFormsNext.SystemNotifications.WinRT.targets)
- [ModernFormsNext.Testing.Tests/SystemNotificationLifetimeTests.cs](../../ModernFormsNext.Testing.Tests/SystemNotificationLifetimeTests.cs)
- [ModernFormsNext.Testing.Tests/SystemNotificationPortableContractTests.cs](../../ModernFormsNext.Testing.Tests/SystemNotificationPortableContractTests.cs)
- [ModernFormsNext.Testing.Tests/SystemNotificationTests.cs](../../ModernFormsNext.Testing.Tests/SystemNotificationTests.cs)
- [ModernFormsNext.Testing/TestPlatformServices.cs](../../ModernFormsNext.Testing/TestPlatformServices.cs)
- [ModernFormsNext.Testing/TestSystemNotifications.cs](../../ModernFormsNext.Testing/TestSystemNotifications.cs)
- [ModernFormsNext.Tests/ReleaseVersionConsistencyTests.cs](../../ModernFormsNext.Tests/ReleaseVersionConsistencyTests.cs)
- [ModernFormsNext.WindowKit.Backend.Windows.Tests/SystemNotificationContentTests.cs](../../ModernFormsNext.WindowKit.Backend.Windows.Tests/SystemNotificationContentTests.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/IWindowsNotificationBackend.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/IWindowsNotificationBackend.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/ShellBalloonNotificationBackend.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/ShellBalloonNotificationBackend.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsNotificationBackendSelector.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsNotificationBackendSelector.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsNotificationCapabilities.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsNotificationCapabilities.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsNotificationOptions.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsNotificationOptions.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsSystemNotificationService.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsSystemNotificationService.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsToastContent.cs](../../ModernFormsNext.WindowKit.Backend.Windows/Notifications/WindowsToastContent.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/UnmanagedMethods.cs](../../ModernFormsNext.WindowKit.Backend.Windows/UnmanagedMethods.cs)
- [ModernFormsNext.WindowKit.Backend.Windows/WindowsTrayIcon.cs](../../ModernFormsNext.WindowKit.Backend.Windows/WindowsTrayIcon.cs)
- [ModernFormsNext.WindowKit.Backend/Lifecycle/PlatformApplicationActivation.cs](../../ModernFormsNext.WindowKit.Backend/Lifecycle/PlatformApplicationActivation.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/ISystemNotificationService.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/ISystemNotificationService.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/SystemNotification.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/SystemNotification.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationAccess.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationAccess.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationCapabilities.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationCapabilities.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationOptions.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationOptions.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationServiceBase.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationServiceBase.cs)
- [ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationValidation.cs](../../ModernFormsNext.WindowKit.Backend/Notifications/SystemNotificationValidation.cs)
- [ModernFormsNext.slnx](../../ModernFormsNext.slnx)
- [ModernFormsNext/SystemNotifications.cs](../../ModernFormsNext/SystemNotifications.cs)
- [docs-site/toc.yml](../../docs-site/toc.yml)
- [docs/README.md](../../docs/README.md)
- [docs/design/system-notifications-audit.md](../../docs/design/system-notifications-audit.md)
- [docs/design/system-notifications-cross-platform.md](../../docs/design/system-notifications-cross-platform.md)
- [docs/design/system-notifications-final-review.md](../../docs/design/system-notifications-final-review.md)
- [docs/system-notifications-compatibility.md](../../docs/system-notifications-compatibility.md)
- [docs/system-notifications.md](../../docs/system-notifications.md)
- [docs/testing/system-notifications.md](../../docs/testing/system-notifications.md)
- [samples/SystemNotifications/Program.cs](../../samples/SystemNotifications/Program.cs)
- [samples/SystemNotifications/README.md](../../samples/SystemNotifications/README.md)
- [samples/SystemNotifications/SystemNotifications.csproj](../../samples/SystemNotifications/SystemNotifications.csproj)
- [samples/SystemNotifications/Test-Activation.ps1](../../samples/SystemNotifications/Test-Activation.ps1)
