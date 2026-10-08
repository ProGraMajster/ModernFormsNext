# System notifications: common API and Windows providers

This is the OS notification service for issue #158. It is separate from the in-app
Toast/Snackbar/InfoBar work in #116. Windows draws the banner and Notification Center
entry. ModernFormsNext does not emulate unsupported OS features with framework windows.

The implementation uses the existing `PlatformServiceRegistry`, dispatcher and application
lifecycle. `SystemNotifications` resolves `ISystemNotificationService`; explicit Windows
registration selects App SDK, classic WinRT, or Shell balloon. Android, Linux, macOS and iOS
have no implementation of this rich contract yet. Reading capabilities never installs or registers anything.

The existing basic Android `IPlatformNotificationService` from #79/#195 remains available for
plain title/body notifications. It does not implement this richer `ISystemNotificationService`
contract. Future Android integration should reuse its native channel/permission/notification
infrastructure; no Android bridge or new backend is added by #158.

## Quick start and deployment

Reference `ModernFormsNext` and the optional `ModernFormsNext.SystemNotifications.AppSdk`
project/package in a Windows application. Add `ModernFormsNext.SystemNotifications.WinRT`
if classic fallback is wanted. These are new package projects in this checkout, not a claim
that a new release has been published. Core framework packages have no Windows App SDK dependency.

The optional projects use `net10.0-windows10.0.26100.0` and Windows SDK projection
`10.0.28000.87`, compiled with this repository's pinned .NET SDK 10.0.401 while retaining
the supported 26100 TFM. The modern
provider uses the components pinned by stable Windows App SDK 2.5.1: Foundation 2.3.12,
InteractiveExperiences 2.1.9 and Runtime 2.5.1. It does not reference WinUI/XAML.

For a framework-dependent unpackaged EXE, use an appropriate Windows RID (`win-x64`,
`win-x86`, or `win-arm64`) and the following application properties. Project references do
not import NuGet build props; package consumers receive the SDK initialization defaults through
`buildTransitive`. **Set WindowsSdkPackageVersion explicitly in the application before its first
restore.** NuGet props are unavailable on that first restore, so they cannot reliably select the
projection download. Both optional packages diagnose an older projection during build.

```xml
<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
<WindowsSdkPackageVersion>10.0.28000.87</WindowsSdkPackageVersion>
<WindowsPackageType>None</WindowsPackageType>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
<WindowsAppSdkBootstrapInitialize>false</WindowsAppSdkBootstrapInitialize>
<WindowsAppSdkDeploymentManagerInitialize>false</WindowsAppSdkDeploymentManagerInitialize>
<WindowsAppSdkUndockedRegFreeWinRTInitialize>false</WindowsAppSdkUndockedRegFreeWinRTInitialize>
```

The disabled SDK module initializers are important: the service performs nonfatal bootstrap
after the OS gate, allowing fallback if the runtime is missing. Applications that already
own App SDK initialization must coordinate that ownership. Self-contained App SDK, trimming,
NativeAOT and single-file publishing have not been qualified by this feature.

The **installer** must deploy the complete official Windows App Runtime redistributable,
including Framework, Main and Singleton packages. Having only the Framework package is
insufficient for `AppNotificationManager.Register`. The service does not install packages,
launch an installer, request elevation, or silently repair runtime registration.
[Microsoft registration requirements](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.appnotificationmanager.register).

Register on the STA/UI thread before `Application.Run`, and subscribe before the message loop starts:

```csharp
using ModernFormsNext;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Notifications;

[STAThread]
static void Main()
{
    var notifications = WindowsSystemNotificationService.Register(new()
    {
        AppUserModelId = "Contoso.VideoDownloader",
        Backend = WindowsNotificationBackendKind.Automatic
    });
    var form = new MainForm();
    notifications.Activated += (_, activation) =>
    {
        // Resolve activation.NotificationId against the application's persisted queue.
        if (form.WindowState == FormWindowState.Minimized)
            form.WindowState = FormWindowState.Normal;
        form.Show();
    };
    // Await notifications.DisposeAsync() in the application's asynchronous shutdown path
    // BEFORE the dispatcher stops. See the sample's cancel-first Closing handler.
    Application.Run(form);
}
```

Registration is process-scoped and performed once. The returned service is application-owned.
Dispose releases callbacks and live COM registration without deleting retained notifications
or uninstalling app identity. Do not synchronously wait for disposal on the UI thread.
The [complete sample](../samples/SystemNotifications/Program.cs) demonstrates shutdown ordering.

## Basic content, text and images

```csharp
var result = await SystemNotifications.ShowAsync(new SystemNotification
{
    Id = "download-42",
    Title = "Download complete",
    Message = "Example video"
});
```

The common model contains plain Unicode text and semantic image roles. Windows XML-escapes
text and advertises a three-block limit for Title, Message and additional Text together; other
backends may advertise different limits. Extra blocks degrade with warnings, or are rejected in
strict mode. `SystemNotificationText.Language` is optional BCP-47 metadata. Windows line limits
belong to `WindowsSystemNotificationTextOptions.MaxLines` (one through four, at most two for
the first block). `Timestamp` is common; attribution remains a Windows option.

```csharp
Images = [
    new(thumbnailPath, SystemNotificationImageRole.Thumbnail, "Video thumbnail"),
    new(logoPath, SystemNotificationImageRole.Portrait, "Publisher")
]
```

Windows maps Thumbnail to Hero, Portrait to a circle-cropped app logo, Identity to app logo,
and Content to inline. `WindowsSystemNotificationImageOptions.Placement` overrides that mapping;
its `AddImageQuery` controls Windows URI query hints. One image per native placement is retained.
The application owns image files and keeps them available while Windows may display content.
PNG/JPEG/GIF/BMP headers are checked on a worker thread without decoding a large bitmap.
Unreadable, invalid or over-3-MiB local files are removed with warnings; Windows performs final
decoding. These are Windows validation rules, not global image-format or size promises.

Absolute local paths/file URIs work for desktop applications. `ms-appx` and `ms-appdata` require
package identity. HTTP(S) is fetched by Windows where the backend advertises `RemoteImages`;
there is no framework download or UI-thread network I/O. `WindowsSystemNotificationImageOptions.AddImageQuery` asks Windows to
append scale/contrast/language hints. Supply a 2:1 hero image and appropriately sized logo
assets; OS cropping/DPI behavior still needs visual checks. Raw XML covers adaptive subgroups
with additional images. [Microsoft image rules](https://learn.microsoft.com/uwp/schemas/tiles/toastschema/element-image).

## Actions and inputs

```csharp
Actions = [
    new("Open", "open") { Id = "open" },
    new("Open folder", "open-folder") { Id = "folder" }
]
```

Common Application actions map to foreground COM activation on Windows. OpenUri actions set
`TargetUri`; `WindowsNotificationActionOptions.TargetApplicationPfn` is optional. Native
`WindowsNotificationActionOptions.ActivationType` Dismiss/Snooze values use Windows system actions;
snooze requires an Alarm or Reminder scenario. Up to five actions are accepted, including
context-menu actions. Windows options supply icons, input association, tooltips and
Success/Critical styles. Style and tooltip support are queried at runtime by the modern provider.
Visible icon buttons must consistently provide usable icons. Icon-only buttons require a
supported tooltip. Context-menu placement requires build 14393 or later.

```csharp
Inputs = [
    new("reply") { Title = "Reply", Placeholder = "Your message" },
    new("quality") {
        Title = "Quality", Choices = [new("hd", "HD"), new("sd", "SD")],
        DefaultValue = "hd"
    }
],
Actions = [new("Send", "send") {
    Id = "send", InputIds = ["reply", "quality"],
    PlatformOptions = new(new WindowsNotificationActionOptions { InputId = "reply" })
}]
```

Input IDs and selection IDs must be unique; selection defaults must exist. The Windows backend advertises
five inputs and five choices per selection; these are not common API limits. The OS may impose additional layout limits. `activation.UserInput`
contains copied values. Desktop callbacks can perform work without displaying a form, but this
does not implement the UWP background-task contract: Windows `Background` actions are removed with a
warning and `PendingUpdate` is unavailable. [Microsoft action schema](https://learn.microsoft.com/uwp/schemas/tiles/toastschema/element-action).

## Progress, update and identity

Persist the application's logical Id/Group pair. Value is 0..1 or null for indeterminate.
Windows requires a nonempty Status; the common model does not. Title and ValueText are optional.
The backend owns native update ordering; application code has no SequenceNumber.

```csharp
var content = new SystemNotification {
    Id = "download-42", Group = "downloads",
    Title = "Downloading...", Message = "Example YouTube video",
    Images = [new(thumbnailPath, SystemNotificationImageRole.Thumbnail)],
    Actions = [new("Open", "open") { Id = "open" }, new("Open folder", "folder") { Id = "folder" }],
    Progress = new() { Value = 0, Title = "Example YouTube video", Status = "Downloading..." }
};
var shown = await SystemNotifications.ShowAsync(content);
if (shown.IsAccepted && shown.Key is { } key) {
    await SystemNotifications.Service!.UpdateAsync(key, new() {
        Value = .76, Status = "Downloading...", ValueText = "76%"
    });
    await SystemNotifications.ShowAsync(content with { Title = "Download complete", Progress = null });
}
```

Replacing the same logical Id/Group changes existing content where Replacement is supported.
Windows maps each Id/nonempty Group to a deterministic 16-character base64url-encoded 96-bit
SHA-256 prefix. Empty Group maps to native `mfn.default`; a logical group literally named
`mfn.default` is hashed and remains distinct. Logical IDs/groups allow 4096 UTF-16 units, but
native XML still has a 5000-byte payload bound. Hashes have a theoretical collision risk.
No process-local identity cache is required for update/remove after restart. Normal payloads
retain the original logical key for native history recovery; raw XML remains untouched and
may return HistoryEntry.Key=null with an opaque removal Reference. Persist logical keys,
not backend references. Native interop can use `WindowsToastContent.ToNativeTag/ToNativeGroup`.

App SDK history references retain the native numeric ID, so exact removal also works for entries
created by another library without Tag/Group. Classic history exposes only Tag/Group; an untagged
foreign entry returns `Unsupported` with a `HistoryIdentity` warning on `DismissHistoryAsync`.
It never substitutes group removal or clears the store. References belong to the provider session
that returned them; refresh history after creating a new service.

Calls are serialized. WinRT uses documented sequence-zero always-apply updates; the SDK-only
numeric fallback reserves its positive sequence internally in a content-free per-executable
counter under LocalApplicationData/ModernFormsNext/NotificationSequences. Neither API's history
is treated as an authoritative progress sequence. Multiple application writers still coordinate
business-state order. `NotFound` means the notification may have been dismissed; do not re-show
it automatically on every progress change. No progress timer or resend is used by Windows.

Modern indeterminate progress uses WinRT `NotificationData` with the **same AUMID registered
by App SDK**, because `AppNotificationProgressData.Value` only accepts a double. Set an explicit
AUMID (or use package identity); otherwise `IndeterminateProgress` is false. This is native
data binding, with App SDK retaining activation ownership.
[Microsoft progress semantics](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-progress-bar).

`DismissAsync`, `DismissHistoryAsync`, `RemoveByIdAsync`, `RemoveByGroupAsync`, `ClearAsync` and `GetHistoryAsync`
address this app's native store. Empty history on an unsupported backend is not evidence of an
empty OS store; check `History`. Removal, GroupRemoval and Clear are separate capabilities. Headers (`Id`, `Title`, `Arguments`) provide visual grouping
from build 15063. A header is separate from the replacement Group.

## Audio, scenarios and delivery

```csharp
ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
PlatformOptions = new(new WindowsSystemNotificationOptions {
    Scenario = WindowsNotificationScenario.Reminder,
    Duration = WindowsNotificationDuration.Long,
    Audio = "ms-winsoundevent:Notification.Looping.Alarm2", LoopAudio = true
})
```

Omit audio options for the OS default. `Silent=true` is mutually exclusive with Audio/LoopAudio.
Supported system events include Default, IM, Mail, Reminder, SMS, and Looping.Alarm/Call (and
numbered variants 2..10). Custom `ms-appx`/`ms-resource` audio requires package identity;
accepted file extensions are AAC, FLAC, M4A, MP3, WAV and WMA. Local file, HTTP and app-data
audio are rejected. Looping requires Long duration and an appropriate source. Windows owns
sound policy and timing; no timer emulates Short/Long.
[Microsoft audio rules](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-custom-audio).

Default, Reminder, Alarm and IncomingCall are typed scenarios; Alarm/Reminder require an
action. Urgent is enabled only after `IsUrgentScenarioSupported()`. HighPriority, SuppressPopup,
ExpiresAt, and ExpiresOnReboot are separately guarded native delivery properties. Expiration on
reboot is available from build 18362; `true` expires the entry on reboot. `AllowMirroring` and
`RemoteId` expose the native mirroring policy/correlation properties, without providing a
cross-device transport or guaranteeing that a paired device receives anything.
[WinRT property versions](https://learn.microsoft.com/uwp/api/windows.ui.notifications.toastnotification).

## Activation and application ownership

Native body, action and input callbacks are copied and posted to the existing UI dispatcher.
They update `Application.Lifecycle` using `PlatformActivationKind.Notification`; the full typed
payload is in `LastActivation.Notification`. `ISystemNotificationService.Activated` receives
the same activation. Registration buffers up to 32 callbacks that arrive before the wrapper
is ready. Subscribe before entering the message loop; avoid slow synchronous startup work.
The App SDK adapter registers the live callback first, then reads
`AppInstance.GetCurrent().GetActivatedEventArgs()`. The SDK retains the first payload from its
COM launch marker in startup data instead of raising the normal running-process event. Both
paths enter the same framework lifecycle and dispatcher; no additional app-instance policy is
introduced. The native regression probe checks this distinct startup path.

The service never executes activation arguments. Look up NotificationId in a persisted download
queue, then allowlist action IDs and resolve the queue's trusted output path before opening a
file or Explorer. Validate reply values. A payload may be replayed or originate outside the app.
Common logical IDs/groups are bounded to 4096 UTF-16 units; response values to 16384 each.
Windows further bounds outgoing payload to 2048, action ID to 256 and input IDs to 64. Give actions with long payloads a separate short Id.

When running, App SDK uses its registered COM server; when closed, Windows needs the installed
executable/identity to launch it. A relocated/deleted build directory invalidates registration.
An activation can start another process depending on identity, installation and app instance
policy. The framework does not invent a second single-instance mechanism: apps must coordinate
existing IPC/instance redirection and decide which window to restore. Windows foreground-lock
policy still applies; receipt of a callback does not guarantee foreground focus.

Modern App SDK provides NotificationInvoked but no equivalent Dismissed/Failed events; its
capabilities do not claim those callbacks. Classic retains at most 128 live toast subscriptions;
history persists independently. Shell retains up to 32 live identities and reports available
click/hidden/timeout callbacks. Shell has no cold activation or persistent history.

## Packaged and unpackaged identity

* **Unpackaged App SDK:** explicit Register uses Microsoft's documented per-user COM/app
  registration. No Start Menu shortcut is required for that path. Keep the EXE at its installed
  location. DisplayName and DisplayIcon must be supplied together if custom branding is wanted.
* **Packaged/MSIX App SDK:** the manifest owns identity, the desktop toast activation extension
  and COM ExeServer registration (including the App SDK activation arguments). The service uses
  package identity and does not overwrite branding. Follow Microsoft's manifest example.
* **Classic desktop:** the installer owns the AUMID Start Menu shortcut, its
  `System.AppUserModel.ToastActivatorCLSID`, and CLSID `LocalServer32` (or equivalent MSIX manifest).
  Pass the same AUMID and ClassicActivatorId. The service registers/revokes only the running class
  factory. Without the activator, it deliberately uses legacy templates and live-process events,
  without promising interactive adaptive/cold activation.
* **Shell:** no App SDK/MSIX dependency. A native tray HWND/icon owns each transient balloon.
  Custom HICON is borrowed through ShowAsync; the application keeps it alive through submission.

Installers must remove their own registration on uninstall. Disposing the running service is not
an uninstall. App SDK `UnregisterAll` is intentionally not called at shutdown.
[App SDK activation/manifest](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-quickstart),
[classic shortcut identity](https://learn.microsoft.com/windows/win32/shell/enable-desktop-toast-with-appusermodelid).

## Capabilities, degradation and threading

```csharp
var capabilities = SystemNotifications.Capabilities;
if (capabilities.Supports(SystemNotificationFeatures.Images | SystemNotificationFeatures.LiveUpdates)) {
    // Semantic downloader features are available.
}
var windows = capabilities.GetPlatform<WindowsSystemNotificationCapabilities>();
bool exactHero = windows?.Supports(WindowsSystemNotificationFeatures.HeroImages) == true;
var access = await SystemNotifications.GetStatusAsync();
if (access.Availability == SystemNotificationAvailability.PermissionRequired && access.CanRequestPermission)
    access = await SystemNotifications.RequestPermissionAsync(); // explicit app decision
```

Capabilities describe implementation, even while notifications are denied/disabled. Access
separately reports Ready, PermissionRequired, PermissionDenied, DisabledByUser, Restricted,
Unavailable or Unknown. AllowedPresentation can describe partial alert/sound/badge authorization.
Windows only queries its native setting, including in RequestPermissionAsync; no fake dialog
is shown. Shell reports Unknown. Show never automatically requests permission. A global Ready
is not a guarantee against quiet mode or future per-channel restrictions. Provider authors can
use SystemNotificationServiceBase.GetShowAccessCoreAsync/GetUpdateAccessCoreAsync for native
per-operation restrictions or exemptions; their defaults query global access. These hooks never
request consent or change the application's global permission result.

Platform options form a typed collection, for example
`new SystemNotificationOptions(new WindowsSystemNotificationOptions { Silent = true })`.
Future platform option types can coexist in it. A provider reads only its own types. Extensions
must be immutable or override Snapshot to deeply copy mutable collections. Common flags do not
include Windows layout/scenario/XML details; those live in WindowsSystemNotificationCapabilities.

Automatic selection tries App SDK on 17763+, then classic WinRT, then an allowed Shell fallback.
An explicit backend preference does not silently select another provider. Capability probing
uses OS/API presence, package identity and builder support queries. Elevated processes cannot
use toast activation; automatic selection can still choose Shell. `Capabilities.Reason` and
standard .NET Trace diagnostics explain unavailable dependencies/fallback.

Optional unsupported content is removed while preserving text; inspect result Warnings.
`AllowDegradation=false` rejects such requests. Malformed required data returns Invalid;
disabled OS settings return Disabled. Native submission/removal errors are translated to Failed
plus HRESULT. Status/history queries can fault with a native exception, including the access
query performed before Show/Update; callers should handle those asynchronous failures. Accepted means
submission succeeded, not that a banner was visible or audible. Quiet hours, Focus Assist,
user settings, OS policy and native layout remain authoritative.

Show snapshots caller collections synchronously. Subsequent operations are serialized and can
be awaited from any thread. Cancellation is observed before submission; it cannot undo an
accepted native request. Disposal prevents new work/callback delivery, waits for in-flight work,
and releases handlers. COM registration/revocation and Shell HWND work use the existing UI
dispatcher. Local image probing is off the UI thread. There are no per-progress timers.

## Advanced XML and deliberate exclusions

Use `WindowsSystemNotificationOptions.RawXml` for adaptive group/subgroup layouts, binding
base URI/language, text alignment/style/wrapping and additional documented schema attributes.
The `ExpandableContent` flag uses the OS `IsExpandableContentSupported` query, after API/build
guards; declare that flag in RequiredFeatures for expanded-content-specific XML.
Declare Windows `RequiredFeatures` and semantic `RequiredCommonFeatures` explicitly; unsupported requirements reject the payload. This is an
expert escape hatch, not a schema validator: the OS validates native semantics. Raw launch/action
arguments are delivered as supplied, with no inferred NotificationId. DTDs/external entities,
payloads over 5000 UTF-8 bytes, more than 128 elements, and depth over 12 are rejected.

The [API inventory and compatibility matrix](system-notifications-compatibility.md) records
supported, version-limited and intentionally unimplemented features, including camera/conferencing,
scheduling, background tasks, push, badges and collection managers.
The [native validation checklist](testing/system-notifications.md) separates automated evidence
from visual/audio, MSIX and OS-VM checks. `ModernFormsNext.Testing` includes
`host.Services.SystemNotifications`, configurable capabilities, deterministic activation,
user dismissal and immutable content snapshots; it does not render or imitate native delivery.

## Cross-platform architecture

Implemented now: Windows. Architecture reviewed for future Android, macOS, iOS and Linux;
no native provider for those targets is shipped here. See the [member audit, native mappings,
permission model and #44 assessment](design/system-notifications-cross-platform.md).
The namespace and contracts changed before the first #158 release; this is not a migration of
an already released stable notifications API.
