# Android platform services

Android remains **Experimental**. This is the authoritative direct-scope #79 service matrix.
The backend uses the existing Application/Form host, Activity tracker, dispatcher, permission
contracts and registries. It adds no AndroidX, MAUI, broad storage permission or background service.
Minimum API remains 23. Windows is the primary supported platform.

| Service | Supported | Requires Activity | Requires permission | Cancel semantics | Recreation policy | Notes |
|---|---|---|---|---|---|---|
| Open file | Yes, SAF | Resumed at start | User-selected URI grant | User: empty; caller: canceled Task | HostLost; retry | OPEN_DOCUMENT, single/ClipData multiselect |
| Save file | Yes, SAF | Resumed at start | URI grant | User: null; caller: canceled Task | HostLost; retry | CREATE_DOCUMENT; creates a new item, no overwrite |
| Pick folder | Yes, one tree | Resumed at start | Tree URI grant | User: empty; caller: canceled Task | HostLost; retry | OPEN_DOCUMENT_TREE; multiple folders explicitly rejected |
| Storage URI | Yes, provider-dependent | No | Existing access | I/O exceptions | Independent | content streams and accessible file URIs; no invented path |
| Bookmark | Conditional | No | Persistable grant offered by result | Denial: null | Grant survives, managed Tasks do not | Versioned opaque string; may be revoked |
| Framework MessageBoxForm | Yes, MFN-rendered | Existing Form host | None | Existing Form modality | Existing Form policy | Framework theme/layout; not OS-native UI |
| Native system message dialog | Yes, AlertDialog | Owner presentation or resumed Activity | None | Standard Cancel; token dismisses/cancels | HostLost, dismiss, no replay | Standard buttons and adapted icons; no custom content/text prompt |
| URI launch | Yes | Resumed | No runtime permission | Immediate handoff | Never replayed | VIEW; tel uses DIAL; handler visibility required |
| File launch | Content URI only | Resumed | Temporary read grant | Immediate handoff | Never replayed | No raw private paths/FileProvider subsystem |
| Share text | Yes | Resumed at start | No | Chooser return: Success, including dismissal; caller: canceled Task | HostLost; no replay | Success is not delivery or recipient-selection evidence |
| Share URI/file | Accessible content URIs | Resumed at start | Temporary read grant | As text sharing | HostLost; no replay | Up to 32 attachments; web links belong in Text |
| Local notifications | Yes, basic | No | Explicit Notifications permission on API33+ | No pending UI | Independent | Show/update by stable ID, dismiss, one stable channel |
| Clipboard (#57) | Tracked separately | — | — | — | — | No new clipboard implementation |
| DragDrop (#57) | Tracked separately | — | — | — | — | No runtime DnD implementation |
| WebView (#20) | Tracked separately | — | — | — | — | No WebView added |
| NativeViewHost (#60) | Tracked separately | — | — | — | — | No native child-view hosting added |
| Camera/microphone feature services | No | — | Authorization exists only | — | — | Permission success does not implement capture |
| Media | No | — | Authorization exists only | — | — | No playback/capture subsystem |
| Font/print dialogs, tray, cursor | Unsupported Android services | — | — | — | — | Existing Windows implementations remain |
| Accessibility/settings | Existing backend support | Host where applicable | None | Existing contract | Existing host lifecycle | See Android accessibility and settings documentation |

## Registration and thread contract

WindowKit contracts resolve through `AvaloniaGlobals`: `IStorageProvider`,
`IPlatformLauncherService`, `IPlatformShareService`, `IPlatformNotificationService`, `IPlatformMessageDialogService`.
The same storage instance is returned from Android window `TryGetFeature(typeof(IStorageProvider))`.
`PlatformServiceRegistry` continues to own dispatcher, permissions and lifecycle. There is no
additional global Android service locator. Android-native types stay in the backend assembly.

Call picker, launcher, sharing and service diagnostic APIs on the Android main thread. Use the
existing `IPlatformDispatcher.InvokeAsync` to enter that thread; unwrap the returned Task when
dispatching an asynchronous picker. Do not block the main Looper or start a nested event loop.
Storage IPC runs on workers with application context. Streams are returned directly and owned
by the caller, without copying entire documents into RAM. Notification operations are thread-safe
and use application context even while all Activities are paused.

Process-wide services do not keep an Activity beyond a native request. The tracker resolves the
current resumed, non-finishing Activity synchronously at operation start; explicit message owners
resolve their own presentation. Pending identity is weak. An active AlertDialog owns its native
context only until retirement, when its listeners and native reference are cleared.
Backend shutdown finishes pending requests and disables notification/launch/UI operations.
Existing storage items retain application-context access, independently of the window runtime.

## Native UI ownership, cancellation and failure

One bounded coordinator owns SAF, sharing, runtime-permission UI and native messages. It admits one operation,
without an unbounded queue. Competing picker requests fail with `PlatformServiceException(Busy)`;
sharing/launch return `Busy`; the existing permission facade reports `Unknown` with a content-free
Busy diagnostic. A rejected permission request is not recorded as a user denial.

`PickerOptions.CancellationToken` and `FileSystemDialog.CancellationToken` cancel managed waiting.
A native modal may still exist: its slot stays occupied until the matching callback or Activity
destruction. No second native UI is launched over it. Picker/chooser waits time out after five
minutes; permission waits use the existing `PermissionRequestTimeout`. Timeout throws
`TimeoutException` and retains the native slot until return/destruction. Process death ends the
managed runtime; no pending Task or coordinator is serialized.

Reserved request codes 8192–32767 never repeat in one backend lifetime. Exhaustion reports
Unavailable instead of wrapping. Old Activity identities or request codes cannot complete a later
operation. Destroying the owner implements **model B**: `HostLost`, no replay, caller may retry after
the replacement resumes. A user cancel returns the original picker null/empty result.

| Condition | Picker/storage behavior | New service behavior |
|---|---|---|
| Caller cancellation | OperationCanceledException | Sharing: OperationCanceledException |
| User cancel | Empty open/folder or null save | Share cannot distinguish dismissal from target handoff |
| Destroy/recreation | PlatformServiceException(HostLost) | Share returns HostLost |
| Missing Activity | PlatformServiceException(Unavailable) | Unavailable |
| Missing/hidden handler | PlatformServiceException(NoHandler) | NoHandler |
| Permission missing/denied | Existing grant/provider security failure | Notifications: NotDeclared or PermissionDenied |
| Shutdown | PlatformServiceException(Shutdown) | Shutdown |
| Invalid arguments | ArgumentException/ArgumentNullException | Same |
| Native/provider fault | Original exception; never silently translated to user cancel | Same except explicit no-handler/security status cases |

Supported `AndroidWindowActivity` forwards callbacks automatically. A custom Activity forwards:

```csharp
protected override void OnActivityResult(int code, Result result, Intent? data)
{
    if (!AndroidWindowKit.HandleActivityResult(this, code, result, data))
        base.OnActivityResult(code, result, data);
}

public override void OnRequestPermissionsResult(int code, string[] names, Permission[] grants)
{
    if (!AndroidWindowKit.HandleRequestPermissionsResult(this, code, names, grants))
        base.OnRequestPermissionsResult(code, names, grants);
}
```

Use those hooks in addition to existing AndroidActivityHost lifecycle forwarding. Custom native
requests must avoid the documented reserved code range. The old permission overload remains
compatible; supported hosts pass explicit Activity identity.

## System messages and framework messages

Use `SystemMessageBox.ShowAsync` for OS-native UI: Android `AlertDialog` or Windows
`MessageBoxW`. Use `MessageBoxForm` for a ModernFormsNext-rendered modal Form with framework
themes and layout. Existing call sites are unchanged. Neither API hosts arbitrary native content.

```csharp
using ModernFormsNext.WindowKit.Platform.Services;

DialogResult result = await SystemMessageBox.ShowAsync(owner,
    "Delete the selected file?", "Confirm",
    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, cancellationToken);
if (result == DialogResult.Yes) DeleteSelectedFile();
```

The facade reuses the original `ModernFormsNext.DialogResult`. `MessageBoxButtons` and
`MessageBoxIcon` live in the neutral WindowKit service namespace. The backend contract
`IPlatformMessageDialogService.ShowAsync` accepts an optional `IWindowBaseImpl`, an immutable
`PlatformMessageDialogRequest` and a token. It returns a validated zero-based semantic button
index; only the facade maps it to DialogResult. This avoids moving the existing result type or
making WindowKit depend on controls. Text is plain, non-null and NUL-free; empty text is allowed.
There are no configurable default-button, custom image/content, input or progress options.

| Buttons | Semantic index / DialogResult | Android Back / outside tap |
|---|---|---|
| OK | 0 / OK | Ignored |
| OKCancel | 0 / OK, 1 / Cancel | Cancel |
| YesNo | 0 / Yes, 1 / No | Ignored |
| YesNoCancel | 0 / Yes, 1 / No, 2 / Cancel | Cancel |
| RetryCancel | 0 / Retry, 1 / Cancel | Cancel |

Android assigns the first choice to positive, the second to negative and the third to neutral;
the OS controls visual ordering. OK/Cancel use system-localized labels. Yes/No/Retry use backend
resources (English fallback and Polish translation), because Android's public Yes/No resources
can read as OK/Cancel. Android Information uses the system info drawable, Warning/Error the
system alert drawable; Question is adapted to no icon. Windows uses its corresponding standard
icons and system-localized buttons. Windows retains native Escape semantics: OK alone acknowledges
with OK; sets with Cancel return Cancel; YesNo ignores Escape. Android does not synthesize OK from Back.

### Android button language policy (v1)

OS-native presentation does **not** imply that all button labels are translated by Android.
The public Android resources `yes` and `no` are aliases in meaning for OK and Cancel in both
API23 and API36 AOSP; they were deprecated in API30 for that mismatch. There is no public Retry
resource. Using private resource IDs or searching internal resource names would not be a stable
SDK contract. OK/Cancel alone use public system strings.

Yes/No/Retry use these supported Android resource keys:

| Key | Default fallback | Bundled Polish |
|---|---|---|
| `mfn_message_yes` | Yes | Tak |
| `mfn_message_no` | No | Nie |
| `mfn_message_retry` | Retry | Ponów próbę |

V1 bundles English defaults and Polish qualifiers. Other locales, including German, French and
Japanese, deliberately fall back to English unless the application supplies translations.
This is a documented localization limitation, not full OS-language parity. A localized application
can add all three keys to its own `Resources/values-<language>/strings.xml` as AndroidResource items;
normal Android resource merging gives application resources priority over the library. The
Activity's Android resource configuration selects the labels. No global culture or framework
localization service is changed; the framework localization ADR is still a proposal. Applications
also own the localization of the message/title they pass.

For example, an application supporting German can provide `Resources/values-de/strings.xml`:

```xml
<resources>
  <string name="mfn_message_yes">Ja</string>
  <string name="mfn_message_no">Nein</string>
  <string name="mfn_message_retry">Erneut versuchen</string>
</resources>
```

See the [public Android string contract](https://developer.android.com/reference/android/R.string#yes)
and [Android library resource merging](https://developer.android.com/studio/projects/android-library#Considerations).
Native validation checks the bundled Polish labels and English fallback through isolated
en/pl/de/fr/ja configuration contexts, without changing device language.

### Scheduling and lifetime

Call on the UI thread. Android returns a pending Task without blocking the main Looper.
Windows reserves its request and returns a pending Task immediately, then schedules MessageBoxW
on the owning UI dispatcher. The scheduled callback revalidates the owner and enters the native
Win32 modal loop; it is never moved to Task.Run. Keep the owner alive until completion. The OS pumps
native messages, including dispatcher cancellation. Owner close/hide before show is HostLost;
cancellation before show opens no UI; shutdown retires even a request that has not started.
Windows uses the owner's real HWND. With null owner it uses an unowned task-modal dialog,
disabling this UI thread's top-level windows. A busy Windows message service rejects another message,
including between scheduling and native show. The slot is released after native modal cleanup.

A supplied Form must be visible and live. Android resolves that Form's current Activity
presentation and epoch, not an unrelated Activity from the tracker. Null owner uses the eligible
resumed Activity; no Activity means `PlatformServiceException(Unavailable)`. Hidden/retired owners
cannot silently become unowned dialogs. Owner close/hide, presentation retirement and Activity
recreation finish Android requests with `HostLost`; no dialog/action is replayed. A later request
has a different identity; stale native callbacks cannot finish it. Backgrounding preserves an
already-shown dialog until resume, destruction or explicit cancellation.

AlertDialog shares the existing single native-request slot with SAF, permission and chooser UI.
There is no extra queue. A competing request follows the existing Busy policy. Pre-cancel shows
nothing. Cancellation during display dismisses the dialog, clears listeners and cancels its Task,
then permits another request. User Cancel is a normal DialogResult and is distinct from token
cancellation. Shutdown dismisses and faults pending work with Shutdown; an unexpected native
dismissal is HostLost. Click/cancel/destruction compete for exactly one terminal completion.
Native dialog/context references exist only within an active request, and are cleared at retirement.

Native focus and accessibility remain owned by the OS. No ModernFormsNext AccessibleObject tree
is manufactured for native buttons. The existing Activity window-focus callbacks restore framework
activation; neither backend adds a second focus system.

`TestPlatformServices.MessageDialogs` records owner/request and provides one-shot NextResult,
NextStatus and NextException. Each TestHost resets it and clears retained content on disposal.
This fake opens no OS UI and is not native validation evidence.

Platform references: [AlertDialog](https://developer.android.com/reference/android/app/AlertDialog),
[MessageBoxW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-messageboxw),
[EndDialog](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enddialog).

## Storage and dialog facades

```csharp
var dialog = new OpenFileDialog { AllowMultiple = true, CancellationToken = cancellationToken };
dialog.AddFilter("Text", "*.txt", "*.log");
if (await dialog.ShowDialog(owner) == DialogResult.OK)
{
    foreach (var file in dialog.SelectedFiles)
    {
        using (file)
        using (var stream = await file.OpenReadAsync())
        {
            // Parse the stream incrementally. content:// is not a local filesystem path.
        }
    }
}
```

`FileNames`/`FileName` and `FolderBrowserDialog.SelectedPath` preserve local Windows paths;
for content storage they contain the absolute URI. `SelectedFiles` and `SelectedFolder`
provide the actual portable objects. Caller owns returned objects and streams; subsequent dialog
calls replace selection references without disposing previously returned items.
Use `InitialLocation` for an existing storage folder, and `InitialDirectory` for local paths.

SAF needs no READ/WRITE_EXTERNAL_STORAGE permission. Android 11+ blocks certain root, Download,
Android/data and Android/obb selections. A provider may reject writes or hide metadata. Missing
names are empty; unavailable size/creation/modification fields are null. No fake metadata is
synthesized. The write mode requests truncation; provider errors remain errors.

Explicit MIME filters take precedence. Simple extension globs map through deterministic known
types then Android MimeTypeMap. Multiple open MIME types use EXTRA_MIME_TYPES. Unsupported globs
or unknown extensions widen to */* to avoid excluding intended files; filter labels are not native
SAF UI. Save uses the first MIME choice (or inferred extension, then application/octet-stream);
SAF has no desktop type-choice UI and CREATE_DOCUMENT does not overwrite. Suggested title and
overwrite-prompt policy are provider/system-owned. Suggested initial URI is only sent at API26+.

ClipData order is retained, exact URI duplicates are removed, and Data is the single-selection
fallback. Multi-folder selection is explicitly unsupported. All WellKnownFolder values return
null: public Desktop/Documents/Downloads/Music/Pictures/Videos have no universally accessible raw
path under scoped storage. They are not silently mapped to private application directories.

Selected tree wrappers enumerate/create/delete provider documents. Arbitrary cross-provider moves
are unsupported; explicitly copy streams when that is desired. Parent is available for wrappers
created by tree enumeration/creation; no parent path is guessed for isolated picked documents.
TryGet accepts accessible file URIs and introspectable content URIs, returning null for unavailable
items. It never prompts for broad access. A content URI is not guaranteed to be introspectable.

Bookmarks are opaque `mfn-saf-v1` strings describing file/tree identity. Saving takes only read/write
persistable grants actually offered by the result flags. Opening checks persisted permission and
current provider access; releasing revokes that grant (including other wrappers relying on it).
Dispose only invalidates that wrapper, not the grant or already-open streams. Treat bookmarks as
private data. The OS/provider can revoke them or remove documents; they are not permanent promises.

## URI launching and sharing

```csharp
var launcher = AvaloniaGlobals.GetRequiredService<IPlatformLauncherService>();
var status = launcher.OpenUri(new Uri("https://example.com/help"));
var share = AvaloniaGlobals.GetRequiredService<IPlatformShareService>();
await share.ShareAsync(new PlatformShareRequest { Title = "Share", Text = "Example" }, cancellationToken);
```

Launch is synchronous only for the immediate OS handoff; it never waits for another application.
`Help` resolves navigation as before and uses this backend contract when registered. Windows uses
shell execution; legacy unregistered hosts retain the previous fallback.

Android checks handler visibility before launching. The library manifest provides narrow queries
for SAF, http/https, mailto, DIAL, content viewing and SEND/SEND_MULTIPLE. A custom scheme requires
the application's corresponding queries entry on Android 11+. No QUERY_ALL_PACKAGES is added.
No-handler also covers handlers hidden by package visibility. file/content URIs are excluded from
general OpenUri: use OpenFile for accessible content files. Unsafe executable intent/javascript/data
schemes are rejected. tel never requests CALL_PHONE.

File launching and attachments require readable content URIs. Grants are temporary read-only flags
plus ClipData. No raw private path is exposed, no FileProvider is introduced, and no target package,
social SDK or email transport is selected. Sharing ordinary web URIs means putting the URL in Text.
Closing the chooser says nothing about delivery.

## Local notifications

```csharp
// Only in response to an explicit application/user action:
var permission = await PlatformServiceRegistry.GetRequiredService<IPermissionService>()
    .RequestAsync(PlatformPermission.Notifications);
var notifications = AvaloniaGlobals.GetRequiredService<IPlatformNotificationService>();
if (notifications.Status == PlatformServiceStatus.Success)
    notifications.Show(new PlatformNotification("download-ready", "Ready", "Your document is ready"));
// Show again with the same ID updates that notification.
notifications.Dismiss("download-ready");
```

The application declares POST_NOTIFICATIONS to opt in on API33+. The sample declares it for its
explicit permission button. The backend library declares no feature permission. Show never opens
permission UI. Before API33 the existing permission mapping needs no runtime dialog. On API24+
Status observes app-wide disablement; API23 lacks that public query, so acceptance cannot promise
visibility. API26+ uses stable channel `mfn.local`, created only when absent; user importance/sound
choices are preserved. No Android channel type leaks into shared contracts.

Stable notification ID is the exact Android tag plus fixed integer 0, not a hash. Show/update/dismiss
need no Activity. A tap uses the package launcher PendingIntent with immutable/update flags supported
at API23, without custom action/deep-link payloads. The application owns the channel and small icon;
the backend uses its icon or the system info icon. FCM, schedules/alarms, rich actions/replies, badges
and background services are outside #79. Windows exposes explicit NotSupported for this basic
contract; separately tracked #158 is not silently imported from an unmerged worktree.

## Diagnostics and validation

`AndroidWindowKit.Current.GetServiceDiagnostics()` returns immutable rows and pending/shutdown
facts. It retains no Activity, Context, Intent, native URI, permission string, file/share/notification
payload. Read it on the main thread. Per-input handler and provider support is checked at request
time; a general supported category does not certify every third-party provider or URI handler.

Deterministic tests exercise MIME/URI plans, exact native request state, cancellation, stale owner
and request rejection, recreation, shutdown, notification API boundaries and real Core consumers
through scoped TestHost services. They do not prove native UI operation.

The sample includes `PlatformServicesValidationInstrumentation` for an English disposable Android
test profile. Its folder selector targets the AOSP emulator storage label sdk_gphone64_x86_64;
other device UIs need their own selector qualification. Create a synthetic Documents/MFN-Issue79
directory, keep the fixture free of accumulated mfn-79-* documents between runs, install a
standalone Debug or Release APK and run the explicitly named instrumentation. It exercises actual DocumentsUI,
ContentResolver, system chooser/dialer, permission and notification manager. Evidence belongs in
`artifacts/issue-79/`; screenshots, APK/AAB, TRX, logs and synthetic files are not source artifacts.
For repeat runs, prepare notification permission consistently: revoke POST_NOTIFICATIONS,
clear user-fixed and set user-set with adb pm permission flags. This models a prior denial that
can be requested again; merely clearing all OS flags while preserving the app's request history
does not represent a first request. The runner still uses the production permission service and
the actual system Allow button; it never grants permission programmatically.

The same instrumentation accepts `-e mode messages` for a device-independent native message
subset: standard choices, title/body hierarchy, cancellation, focus, shared-slot contention,
background/resume, recreation, rotation and shutdown. It does not touch user documents or request
notification permission. Full services mode includes this subset and the existing SAF/permission
regressions. It uses actual native UI, not OCR. Physical-device and API23 runtime qualification must be reported separately from API34 emulator
checks. See the local validation report for this working tree; no release/device parity is implied.

Android platform references: [SAF](https://developer.android.com/training/data-storage/shared/documents-files),
[handler visibility](https://developer.android.com/training/package-visibility/use-cases),
[notification permission](https://developer.android.com/develop/ui/views/notifications/notification-permission).
