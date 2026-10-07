# System notification compatibility and API inventory

Reviewed 2026-10-03 against stable Windows App SDK 2.5.1 and Windows SDK projection
10.0.28000.87 with the existing 26100 TFM (verified with SDK 10.0.401). See the [user guide](system-notifications.md),
[audit with per-build matrix](design/system-notifications-audit.md), and
[actual test results/checklist](testing/system-notifications.md).

Three independent questions must be answered: does Windows expose the API, does the selected
backend and app identity expose it, and is the current .NET runtime supported on that OS/edition?
An API compatibility gate is not OS/runtime certification. In particular, ModernFormsNext
**does not run on Windows 7/8/8.1 as a supported .NET 10 application**. No framework TFM was
downgraded. The Shell adapter and classic template path are isolated so a separately maintained
compatibility package/test host could be built in the future.
[.NET 10 OS policy](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

## Native feature matrix

Win10 legacy means builds 10240–17134; modern means 17763+. Entries describe OS/backend
possibilities, conditional on registration and the capability query. A dash means unavailable
through that path. Win7/8 rows are design coverage, not executed .NET 10 runtime support.

| Feature | Win7 | Win8/8.1 | Win10 legacy | Win10 modern | Win11 |
|---|---|---|---|---|---|
| Native title/body, default/silent audio | Shell | Classic templates | Classic | App SDK/classic | App SDK/classic |
| Balloon info/warning/error/custom icon, large/quiet/realtime | Shell | Shell fallback | Shell fallback | Shell fallback | Shell fallback |
| Local image | — | One template image | Yes | Yes | Yes |
| Inline/app-logo/circle images | — | — | Adaptive desktop + COM | Yes | Yes |
| Hero/attribution/context-menu action | — | — | From 14393 | Yes | Yes |
| HTTP(S) images | — | — | Packaged adaptive path | App SDK; classic packaged | Same |
| Package/app-data images | — | Packaged API, not classic desktop local-file template | Package identity | Package identity | Package identity |
| Foreground/protocol/system actions, text/selection input | — | — | Adaptive desktop + COM | Yes | Yes |
| UWP background task/pending-update | — | — | Not provided by this desktop service | Not provided | Not provided |
| Progress/indeterminate/native data updates | — | — | From 15063 + Data API | Yes, registered identity | Same |
| Native history/tag/group removal | — | API-dependent (8.1+); no .NET certification | Probe History | Yes | Yes |
| Header/custom timestamp | — | — | From 15063 | Yes | Yes |
| Default/Reminder/Alarm/IncomingCall | — | Legacy duration/audio only | Adaptive scenarios | Yes | Yes |
| Urgent | — | — | — | Runtime support query, normally false | Runtime support query |
| Success/Critical styles, tooltips/icon-only actions | — | — | — | Runtime support queries | Runtime support queries |
| Custom audio | — | — | Packaged resources | Packaged resources | Packaged resources |
| Looping system audio, Short/Long | Shell owns duration | Toast native options | Yes | Yes | Yes |
| Priority | — | — | From 15063 + property probe | Yes | Yes |
| Expiration/suppress popup | — | Restricted legacy path | Adaptive path | Yes | Yes |
| Expire on reboot | — | — | — | From 18362 + property probe | Yes |
| Mirroring policy/RemoteId | — | — | From 14393 + probe | Classic; modern explicit/package identity | Same |
| Expanded-content XML | — | — | — | — | From 26100, API presence and IsExpandableContentSupported query |
| Cold activation | — | Legacy template live callback only | Installed COM activator | App SDK registration or installed classic COM | Same |
| Dismissed/Failed events | Shell callbacks only | Classic live events | Classic live events | Classic only; not App SDK | Same |

The rows are bounded by these Microsoft contracts, not inferred from a marketing release name:
[Shell structure/flags](https://learn.microsoft.com/windows/win32/api/shellapi/ns-shellapi-notifyicondataw),
[legacy desktop toast](https://learn.microsoft.com/windows/win32/shell/quickstart-sending-desktop-toast),
[toast content/version notes](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-content),
[WinRT property history](https://learn.microsoft.com/uwp/api/windows.ui.notifications.toastnotification),
[App SDK builder](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.builder.appnotificationbuilder).

Windows 10 LTSB/LTSC 2016 (14393), LTSC 2019 (17763), LTSC 2021 (19044), and Windows 11
LTSC 2024 (26100) use their actual build/API gates; edition servicing is a separate requirement.
The per-build audit also covers 1507, 1511, 1703, 1709, 1803, 1903–22H2, and Windows 11
21H2, 22H2, 23H2, 24H2, 25H2, 26H1 and 26H2.

Windows 11 26H2 is the latest public release considered here. The 26H1 hardware branch (28000)
has a larger build number than 26H2 (26300); this does not imply a larger feature set.
No additional **stable App SDK notification API** was identified in the reviewed 2.x release
notes. This does not assert that every OS rollout or newly documented Windows SDK member is
identical to 24H2/25H2: the property/probe gaps below remain explicit.
[26H2 announcement](https://blogs.windows.com/windowsexperience/2026/09/29/how-to-get-windows-11-2026-update/),
[stable App SDK notes](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0).

## Supported inventory

* **Builder content:** arguments, text blocks/language/max lines, attribution, timestamp,
  inline/hero/app-logo/circle image, alternative text and scale/language query hints.
* **Builder interaction:** buttons, argument payload, foreground/protocol/system activation,
  explicit action IDs, input association, icons, tooltips, supported button styles,
  context-menu placement, text and selection inputs/defaults, dismiss and snooze.
* **Builder presentation:** native audio, silent, looping, duration, scenarios; runtime
  queries for urgent, style and tooltip availability. Per-notification validation diagnostics.
* **Builder data:** native progress, title/status/value override, indeterminate through native
  NotificationData and provider-owned ordering; logical Id/Group replacement.
* **AppNotification metadata:** tag/group, expiration, expire on reboot, priority and banner
  suppression. Native numeric Id is used to verify Show acceptance; callers use logical Id/Group keys
  rather than native tags or process-specific native objects.
* **AppNotificationManager:** support/settings probes, explicit registration and branding,
  event-before-register ordering, live/cold activation entry point, update, remove by tag/group,
  clear, get native history and unregister the live process. Dependency failures enable fallback.
* **WinRT ToastNotification/Notifier/History:** adaptive and legacy templates, Data/Update,
  Activated/Dismissed/Failed, Hide, native history, mirroring policy and RemoteId. Optional COM
  activator uses Microsoft's documented INotificationActivationCallback ABI.
* **XML beyond the builder:** native adaptive groups/subgroups and documented binding/text
  layout hints through bounded RawXml with explicitly declared required capabilities; expanded
  content is gated by the native IsExpandableContentSupported query.
* **Shell:** NIF_INFO; info/warning/error/custom HICON, no sound, large icon, quiet-time and
  realtime flags; click/hidden/timeout signals correlated to each native HWND. No fake rich UI.
* **Framework:** registry facade, copied immutable payloads, existing lifecycle ingress, UI
  dispatch, serialized operations, cancellation/disposal, deterministic fake and simulated OS selection.

## Not supported by the Windows/backend version

Features removed by capability validation are reported in Warnings. Classic desktop without
COM activation keeps legacy templates even on a newer OS; otherwise buttons could display with
no reliable handler. App SDK without explicit/package identity can use numeric progress but
does not advertise indeterminate or the WinRT mirroring bridge. Missing runtime, disabled
notifications and elevation are distinguished from feature-level availability.

Older Windows has no progress/header/timestamp before 15063 or hero before 14393. Shell cannot
provide images, buttons, reply inputs, progress, native notification history or cold activation.
No backend claims to override Focus Assist, accessibility duration or user sound settings.

## Intentionally not implemented

| API/feature reviewed | Concrete reason / alternative |
|---|---|
| Camera preview, ConferencingConfig and Setting button style | Microsoft documentation resolves these members to the experimental App SDK view. They are not exposed by the stable dependencies used here. No experimental dependency is introduced. |
| Dedicated expanded-content layout object model | RawXml already represents the native layout schema without a competing layout tree. The stable 28000.87 projection supplies IsExpandableContentSupported while retaining the 26100 TFM; callers declare ExpandableContent in RequiredFeatures. Visual acceptance still requires the target OS rollout. |
| UWP background activation / pending-update task lifecycle | Requires background-task/manifest registration, deferral and process-lifetime semantics different from desktop foreground COM. A desktop callback can handle data without showing UI, but cannot advertise the UWP contract. |
| ScheduledToastNotification / recurring local schedules | AppNotificationManager has no scheduler; WinRT uses a separate persistent scheduled queue, with distinct cancellation, delivery-time, repeat and asset-lifetime contracts. The current interface models immediate submission and native history, not that queue. No timer substitute is used. A future scheduler must expose native schedule enumeration/cancellation and identity constraints explicitly. |
| Push notifications/WNS channels, raw push and periodic tile/badge updates | Require separate channel authentication, server delivery and/or background trigger contracts. They are not local notification rendering or progress. Core does not acquire cloud credentials or poll servers. |
| Tile/Badge managers and multi-account ToastCollectionManager | Separate shell surfaces/account-scoped stores; a collection manager needs account-specific identity and history ownership. This service deliberately owns one application notification store. Header/Group provide grouping within that store. |
| UserNotificationListener / access to other apps' notifications | Requires user consent and privileged listener access, unrelated to sending/managing this application's own notifications. No access requested. |
| `UnregisterAll` as automatic shutdown cleanup | Deletes persistent registration needed for retained notifications/cold launch. The installer, not ordinary DisposeAsync, owns uninstall cleanup. |
| App SDK native Dismissed/Failed callbacks | Those events are not supplied by AppNotificationManager. No synthetic callback or polling timer pretends to observe them. Select classic if live dismissal events are required. |
| Raw XML semantic inference and automatic schema rewriting | New attributes can carry behavior the framework cannot safely infer. The caller must declare capabilities; the XML parser only enforces structural/security bounds. |
| Downloading/transcoding/cache ownership for image/audio assets | Application data lifecycle and authentication cannot be inferred by a UI framework. Native remote images are passed to Windows; applications prepare and retain local/package assets. |
| Automatic instance redirection and forced foreground activation | Competes with app-owned lifecycle/IPC and cannot bypass Windows focus policy. Typed activation is delivered to the canonical lifecycle; the application chooses its instance/window. |

Sources for the exclusion audit:
[camera preview](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.builder.appnotificationbuilder.addcamerapreview),
[conferencing](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.appnotification.conferencingconfig),
[setting style](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.builder.appnotificationbutton.setsettingstyle),
[expandable-content query](https://learn.microsoft.com/uwp/api/windows.ui.notifications.toastnotification.isexpandablecontentsupported),
[notification delivery methods](https://learn.microsoft.com/windows/apps/develop/notifications/choosing-a-notification-delivery-method),
[WinRT notification namespace inventory](https://learn.microsoft.com/uwp/api/windows.ui.notifications).

## Future

Android, Linux, macOS and iOS adapters; a separately qualified legacy-runtime compatibility package;
native persistent scheduling with its own queue contract; stable conferencing APIs
when available in a compatible stable SDK. These are not implemented support claims.

Stage 2 separates semantic capabilities/access from Windows-specific capabilities and options.
Windows retains the native matrix above. Android, macOS, iOS and Linux are **architecture targets,
not implemented support**; see the [cross-platform review](design/system-notifications-cross-platform.md).
