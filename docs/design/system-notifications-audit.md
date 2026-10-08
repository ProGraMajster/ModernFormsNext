# System notifications audit (#158)

Audit date: 2026-10-03. Base: `1de8a6b`. This subsystem is independent of #116.

The placeholder inventory below is historical to that base. During finalization #195/#79
introduced the public basic Android IPlatformNotificationService. The final review preserves it
and scopes the Windows-only implementation claim to the rich SystemNotifications contract.

## Decisions before implementation

* The existing `WindowsNotificationService` and `IPlatformNotificationService` are empty, internal placeholders. They provide no notification functionality or compatibility contract.
* Use `PlatformServiceRegistry`, the existing dispatcher and `PlatformActivationKind.Notification`. No additional service locator or single-instance runtime.
* Put notification contracts in the dependency-light backend foundation. The framework facade resolves that contract. Windows XML/options/selection belong to the Windows backend.
* Isolate Windows SDK projections and Windows App SDK in optional assemblies named outside the automatic `ModernFormsNext.WindowKit.Backend.*` bootstrap scan. Neither library is referenced by the core framework.
* Windows App SDK 2.5.1 is stable. Its package manifest pins Foundation 2.3.12. Use that component for notification APIs rather than pulling in WinUI, AI, widgets and search. Native runtime deployment is an application/installer responsibility.
* Use stable Windows SDK projection 10.0.28000.87 while retaining the SDK-supported 26100 TFM. A separate compile probe established that this is compatible with pinned .NET SDK 10.0.401 and exposes IsExpandableContentSupported; changing the TFM to 28000 is unnecessary. NuGet build props/validation keep consumer projections aligned.
* Explicit registration is required at application startup; unpackaged App SDK registration changes per-user COM identity. Merely reading capabilities must not register an application or install a shortcut.
* Serialize native mutations, use native progress data updates, preserve native sequence ordering, and expose cancellation before submission. A successful submission does not prove that Windows displayed a banner.
* Application activation payload is data, never an instruction to execute a file or URI. Application code decides whether to show/activate a form and whether to forward to another instance.

## Source evidence

* [Stable release and servicing policy](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-channels): 2.5.1; API backward compatibility to 17763 is distinct from OS servicing.
* [2.0 release notes, including 2.5.1](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0): no notification API additions identified in the stable 2.x change lists reviewed.
* [Windows 11 2026 update](https://blogs.windows.com/windowsexperience/2026/09/29/how-to-get-windows-11-2026-update/): 26H2 is a public release; marketing names do not select a backend.
* [.NET 10 supported OS](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md): Windows 7/8/8.1 are not supported. Older LTSC editions must be distinguished from expired consumer releases. The September 28 list predates 26H2.
* [App notification overview](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/): elevated processes are unsupported.
* [Console registration](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-console): subscribe before Register; unpackaged registration provides COM cold launch.
* [Builder API inventory](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.builder.appnotificationbuilder): arguments, buttons, inputs, text, images, progress, audio, duration, group/tag, scenarios and timestamp.
* [Camera preview](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.builder.appnotificationbuilder.addcamerapreview): stable documentation redirects to experimental; exclude from the production contract.
* [XML schema](https://learn.microsoft.com/uwp/schemas/tiles/toastschema/schema-root), [content](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-content), [actions](https://learn.microsoft.com/uwp/schemas/tiles/toastschema/element-action), [images](https://learn.microsoft.com/uwp/schemas/tiles/toastschema/element-image), [audio](https://learn.microsoft.com/uwp/schemas/tiles/toastschema/element-audio).
* [Data property availability](https://learn.microsoft.com/uwp/api/windows.ui.notifications.toastnotification.data): build 15063 / UniversalApiContract 4.
* [Native progress update](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-progress-bar): sequence-numbered updates; replace changes layout, update preserves position.
* [Desktop identity](https://learn.microsoft.com/windows/win32/shell/enable-desktop-toast-with-appusermodelid), [legacy desktop templates](https://learn.microsoft.com/windows/win32/shell/quickstart-sending-desktop-toast): installed AUMID shortcut and local images.
* [Shell balloon contract](https://learn.microsoft.com/windows/win32/api/shellapi/ns-shellapi-notifyicondataw): NIF_INFO, quiet time, realtime, icons and system-owned timeout.

## Planned selection matrix (API availability, not runtime certification)

| OS | Build | Native path | Important gates |
|---|---:|---|---|
| Windows 7 | 7600/7601 | Shell balloon | No toast, actions, inputs, progress or history; .NET 10 unsupported |
| Windows 8 | 9200 | Classic WinRT templates | Local image and installed shortcut; .NET 10 unsupported |
| Windows 8.1 | 9600 | Classic WinRT templates | .NET 10 unsupported |
| Windows 10 1507 | 10240 | Classic adaptive toast | Generic text/images/actions/inputs; runtime support not claimed |
| Windows 10 1511 | 10586 | Classic adaptive toast | Probe native members; runtime support not claimed |
| Windows 10 1607 / LTSB 2016 | 14393 | Classic adaptive toast | Hero/attribution; supported runtime depends on edition/lifecycle |
| Windows 10 1703 | 15063 | Classic adaptive toast | Progress/data, header, timestamp, priority with property probe |
| Windows 10 1709 | 16299 | Classic adaptive toast | Same capability gates |
| Windows 10 1803 | 17134 | Classic adaptive toast | Probe native members |
| Windows 10 1809 / LTSC 2019 | 17763 | App SDK, then classic | Optional runtime and app identity required |
| Windows 10 1903/1909 | 18362/18363 | App SDK, then classic | Expire-on-reboot property probe |
| Windows 10 2004/20H2/21H1 | 19041/19042/19043 | App SDK, then classic | Same contract gates |
| Windows 10 21H2 / LTSC 2021 | 19044 | App SDK, then classic | Edition/lifecycle separate |
| Windows 10 22H2 | 19045 | App SDK, then classic | Runtime/OS servicing separate |
| Windows 11 21H2 | 22000 | App SDK, then classic | Styled buttons via explicit feature probe |
| Windows 11 22H2/23H2 | 22621/22631 | App SDK, then classic | Urgent via explicit support query |
| Windows 11 24H2 / LTSC 2024 | 26100 | App SDK, then classic | Do not infer camera support |
| Windows 11 25H2 | 26200 | App SDK, then classic | Host available for native verification |
| Windows 11 26H1 | 28000 | App SDK, then classic | Hardware-specific branch; build ordering is not feature ordering |
| Windows 11 26H2 | 26300 | App SDK, then classic | No additional stable notification API identified; still probe |

These are selection/design expectations, not test results. Manual OS acceptance must be recorded separately.
