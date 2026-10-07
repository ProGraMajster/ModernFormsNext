# System notifications: cross-platform contract review

Stage 2 of #158, reviewed 2026-10-07 against #44. Windows is implemented; Android, macOS,
iOS and Linux are architecture targets, not implemented or certified backends. No solution
TFM, package version or released framework contract changes as part of this review.

## Audit decisions before implementation

| Previous common surface | Decision and reason |
|---|---|
| Id plus native Tag/Group, 16-character limits and SHA tag derivation | Use logical Id/Group keys. Native encoding belongs to Windows; preserve exact replacement/removal after process restart. |
| Inline/Hero/AppLogo/Avatar placement | Replace layout positions with Content/Thumbnail/Identity/Portrait image roles. Windows placement, crop and AddImageQuery become image options. |
| Text.MaxLines and Windows three-block/five-choice validation | Move line hints to Windows text options; expose backend limits separately from generous common safety bounds. |
| Action Application/Background/Protocol/Dismiss/Snooze | Keep application response and open-URI intent; native foreground/background/system mechanisms become Windows action options. Inputs are associated with actions in the common model. |
| Progress.SequenceNumber and StaleUpdate | Remove the common sequence protocol. Windows owns native ordering; the common service serializes calls. |
| One PlatformOptions record | An immutable collection keyed by exact option type lets one content object carry settings for several future platforms. Foreign options are preserved and ignored by other providers. |
| Windows flags in SystemNotificationFeatures | Keep semantic capabilities in common, with a typed platform-capabilities extension for the selected backend. Windows retains all native feature flags. |
| Capabilities without a status query | Add explicit status and permission-request operations. Querying status never requests permission; Windows never fabricates a permission dialog. |
| Windows Timestamp/ExpiresAt | Promote these semantic values to common content; retain native priority, scenario, reboot and mirroring settings in Windows options. |
| History as a list of native tag/group pairs | History entries have an optional logical key and a backend-owned reference. An unknown/raw native entry remains removable without inventing a logical identity. |
| Backend namespace for app-facing records | Use ModernFormsNext.Notifications for public content/service contracts, retaining their dependency-light assembly. Backend helpers stay under WindowKit.Backend.Notifications. |
| XML-compatible text validation in common | Common validates bounded Unicode/data, uniqueness and references. XML, native URI restrictions and native layout limits belong to Windows. |

The existing App SDK registration, startup relay/AppInstance activation, COM activator, dispatcher,
native XML, progress transports, history and Shell resources are retained. No competing registry,
lifecycle, scheduler or platform implementation is introduced.

## Source review

* [Issue #158](https://github.com/ProGraMajster/ModernFormsNext/issues/158) separates native notifications from #116.
* [Roadmap #44](https://github.com/ProGraMajster/ModernFormsNext/issues/44) requires a separate compatibility audit/CI before adding net8/net9 targets; it does not imply Windows 7 support.
* [Android overview](https://developer.android.com/develop/ui/views/notifications), [actions/direct reply/progress](https://developer.android.com/develop/ui/views/notifications/build-notification), [channels](https://developer.android.com/develop/ui/views/notifications/channels), [permission](https://developer.android.com/develop/ui/views/notifications/notification-permission), [styles](https://developer.android.com/develop/ui/views/notifications/expanded), [Live Updates](https://developer.android.com/develop/ui/views/notifications/live-update), [bubbles](https://developer.android.com/develop/ui/views/notifications/bubbles).
* [Apple permission](https://developer.apple.com/documentation/usernotifications/asking-permission-to-use-notifications), [categories/actions](https://developer.apple.com/documentation/usernotifications/declaring-your-actionable-notification-types), [content](https://developer.apple.com/documentation/usernotifications/unnotificationcontent), [attachments](https://developer.apple.com/documentation/usernotifications/unnotificationattachment), [request identity](https://developer.apple.com/documentation/usernotifications/unnotificationrequest), [center](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter).
* [Freedesktop protocol](https://specifications.freedesktop.org/notification/latest/protocol.html), [XDG Notification portal v2](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Notification.html).

## Final common contract and member audit

The public namespace is `ModernFormsNext.Notifications`. Contracts stay in the existing
`ModernFormsNext.WindowKit.Backend` assembly, which both the high-level facade and platform
providers already reference. Moving them to the control assembly would reverse the backend
dependency; creating another assembly is unnecessary. Existing platform-service contracts use
backend namespaces, but ordinary notification content is application-facing. Infrastructure
(`SystemNotificationServiceBase`, validation, registration and Windows transports) keeps backend
namespaces. The `ModernFormsNext.SystemNotifications` facade remains a class, avoiding a
namespace/type collision. This is a deliberate breaking correction to the **unreleased #158 API**.

| Members | Meaning and decision |
|---|---|
| Id, Group, Key(Id, Group) | Ordinal logical identity; the pair partitions replacement/removal. Persist IDs in app data. No public Tag. Group need not mean a visual header. |
| Title, Message, Text(Content, Language) | Plain localized content; a provider combines or limits blocks. Language is optional BCP-47 metadata, not Windows XML. |
| Images(Source, Role, AlternateText) | Application-owned asset references; Content, Thumbnail, Identity, Portrait describe intent. Exact native layout/resources are provider extensions. |
| Actions(Title, ActivationData), Id, ActivationType, TargetUri, InputIds | Stable response identity, opaque payload, Application/OpenUri intent and required input associations. The OS may return additional declared inputs. No COM/Intent/delegate in common. |
| Inputs(Id, Title, Placeholder, DefaultValue, Choices); Selection(Id, Title) | Text or discrete selection. Query TextInput and SelectionInput separately; Android suggestions/data replies can use input extensions without redefining selection as free text. |
| Progress(Value, Status, Title, ValueText) | Value is a finite fraction or null for indeterminate; empty status is legal in common. Windows requires a nonempty status and validates it locally. No common sequence. |
| Timestamp, ExpiresAt, Urgency | Displayed event time, native expiry and relative attention. None schedules delivery or bypasses policy. High is not an Apple critical-alert entitlement or Android full-screen intent. |
| Sender(Id, DisplayName, Image) | Person identity useful for messaging; native conversation metadata remains an extension. Windows currently degrades SenderMetadata with a warning. |
| PlatformOptions on content/text/image/action/input; permission request and activation PlatformData | Typed immutable extension collection; exact concrete type lookup and replacement. Supports several platforms in one object. |
| AllowDegradation | Optional unsupported content is dropped with warnings; false rejects it. Invalid data is not silently repaired. |
| Capabilities(Backend, Features, Reason, Limits, Platform) | Implemented abilities and native content limits, independent of authorization. Typed platform capabilities do not grow a global OS-specific enum. |
| Access(Availability, CanRequestPermission, Reason, AllowedPresentation) | Current access; Ready, PermissionRequired, PermissionDenied, DisabledByUser, Restricted, Unavailable or Unknown. AllowedPresentation can express partial alert/sound/badge permission. |
| PermissionRequest(Presentation, PlatformOptions) | Explicit consent operation. Platform extensions can later request Apple provisional options or carry Android host context. No implicit prompt in Show. |
| Result(Status, Key, Warnings, ErrorCode) | Acceptance is not visible/audible delivery. ErrorCode is backend-specific. No common stale-sequence outcome. |
| Activation(NotificationId, Group, ActivationData, ActionId, UserInput, PlatformData) | Copied response on the canonical dispatcher/lifecycle; native metadata can include future portal activation tokens. Treat all response data as untrusted. |
| Change(Key, Reason, ErrorCode) | Actual native callbacks only. No event/timer pretends to observe unavailable dismissal signals. |
| HistoryEntry(Key?, Reference) | Native entries only, with unknown logical keys allowed. Opaque immutable references allow exact removal of raw/foreign entries. References are scoped to a provider session; persist logical keys instead. |
| Service operations and facade | Show, progress Update, exact Dismiss, DismissHistory, RemoveById, RemoveByGroup, Clear, GetHistory, GetStatus, explicit RequestPermission, events and async disposal. Removal/GroupRemoval/Clear are independent of History. |

Common safety bounds are 4096 UTF-16 units for logical IDs/groups, 16384 for ordinary content,
256 items per collection and 32 option types. They bound memory/work, not screen layout.
Native payloads may have smaller byte limits. XML validity, Windows input IDs, image file formats,
three text blocks, five inputs/choices and line counts are Windows concerns. Backends advertise
count limits; common normalization applies those limits with warnings and removes actions whose
required inputs did not survive. No fixed native count is implied when a limit is null.

Options are copied at construction and again at submission. Derived extension records must be
immutable; records owning mutable collections override `Snapshot` to return a type-preserving
immutable deep copy. Unknown foreign options are retained and ignored by unrelated providers.
The common assembly references no concrete Windows, Android, Apple or Linux option class.
CustomAudio remains a semantic capability; audio resource/configuration syntax lives in native
options rather than claiming that one sound URI works on all platforms.

## Windows mapping and retained functionality

* App SDK remains the modern registration/activation owner. The callback subscription, Register,
  AppInstance first-payload recovery and startup relay are unchanged in purpose. Classic keeps
  its installed COM activator and legacy template path; Shell keeps native tray HWND/HICON lifetime.
* Content maps to escaped adaptive XML or legacy templates. Thumbnail maps to Hero, Identity to
  app-logo override, Portrait to circle-cropped logo, Content to inline. Native placement can be
  overridden by `WindowsSystemNotificationImageOptions`, including `AddImageQuery`. Unsupported
  placements degrade to inline with a warning; duplicate resulting placements are bounded.
* `WindowsSystemNotificationTextOptions.MaxLines`, `WindowsNotificationActionOptions` and
  `WindowsSystemNotificationOptions` preserve line hints, icon/tooltips/styles/context menu,
  input layout, target PFN, native dismiss/snooze, headers, attribution, scenarios, audio/duration,
  high priority, suppress popup, reboot expiration, mirroring/RemoteId, raw adaptive XML and balloons.
  Background UWP task activation/pending-update remains explicitly unsupported, as before.
* `WindowsSystemNotificationCapabilities` owns InlineImages, HeroImages, AppLogo, LoopingAudio,
  Headers, UrgentScenario, ButtonStyles, ContextMenuActions, Scenarios, Attribution, SuppressPopup,
  Priority, ButtonTooltips, RawXml, RebootExpiration, Mirroring and ExpandableContent. Native
  support probes and optional package loading remain authoritative.
* Native tag is the first 96 SHA-256 bits of UTF-8 logical Id, encoded as 16 base64url characters.
  A nonempty logical Group uses the same stable mapping. Empty Group maps to `mfn.default`;
  logical `mfn.default` itself is hashed and cannot alias empty. A truncated hash is not a
  mathematical collision-free encoding; do not mix arbitrary external native tags with this store.
  The managed launch envelope retains logical ID/group across process restart. History only
  recovers a key if its recomputed native pair matches; raw XML is not rewritten to invent an ID.
  Raw entries remain removable by opaque history reference. No sidecar identity map is required.
* The service serializes updates. WinRT NotificationData uses its documented sequence-zero
  always-apply mode, so restart never reuses a stale process counter. This also serves App SDK
  indeterminate progress under its registered explicit/package AUMID. App SDK's numeric-only
  fallback forbids zero; it reserves an increasing counter before submission in a small, locked,
  atomically replaced file under LocalApplicationData/ModernFormsNext/NotificationSequences,
  scoped by the SDK's executable-path identity. The reservation also exceeds the native entry ID,
  which bounds the initial stream on the tested Windows host; SDK history reconstructs its own
  progress sequence as 1 and is not a counter source. No message/input data is stored. Corruption,
  exhaustion or I/O failure is reported, not reset silently. This is not an application scheduler.
  Applications with multiple writer processes must coordinate their business-state ordering.
* GetStatus and RequestPermission both read the Windows native Setting. Neither opens a dialog.
  Shell reports Unknown because it lacks a reliable per-application permission query. Features
  remain advertised when the user disables delivery.

History cannot be used to reconstruct an update counter: the App SDK source explicitly gives
recovered progress a default sequence of 1, and the host's WinRT history returned a different
stable number rather than each supplied update number. Native tests therefore inspect retained
progress **values**, with a bounded wait for OS persistence, rather than accepting a success code
or assuming history sequence equality. Sources: [WinRT sequence contract](https://learn.microsoft.com/uwp/api/windows.ui.notifications.notificationdata.sequencenumber),
[App SDK history conversion](https://github.com/microsoft/WindowsAppSDK/blob/main/dev/AppNotifications/AppNotificationUtility.cpp).

This is not support for Windows 7/8/8.1 under .NET 10. Shell/classic stay isolated for a future
separately qualified compatibility package. Optional SDK/projection dependencies and consumer
first-restore requirements remain Windows-only; no versions or solution targets changed here.

## Future Android mapping (design, no provider)

| Native concern | Mapping / future extension boundary |
|---|---|
| NotificationManager notify(tag, id), cancel and active entries | Encode the logical Id/Group into a stable native tag plus integer ID; do not truncate all identity to a collision-prone integer alone. Preserve original keys in extras. Query native active entries only where available; do not invent History. |
| Channels and channel importance | Future AndroidSystemNotificationOptions.ChannelId selects an app-created channel; channel creation/defaults belong to Android registration. API 26+ channel settings and user choices control interruption. Common Urgency is a preference, never a channel mutator. |
| Runtime permission | API 33+ POST_NOTIFICATIONS maps to explicit RequestPermission and current Access. The Android host supplies Activity/lifecycle coordination. A denied permission does not remove Actions/Progress capabilities. Channel-specific disabling needs native diagnostics/options; global Ready cannot promise every channel. SystemNotificationServiceBase offers protected GetShowAccessCoreAsync/GetUpdateAccessCoreAsync hooks (defaulting to global status) so a native provider can evaluate the request or existing identity without changing global permission. Android media/self-managed-call exemptions and foreground-service visibility require the native host policy, not a blanket permission bypass. |
| Small/large icons and BigPicture | Mandatory small icon is an Android resource/registration setting, not an arbitrary common portrait. Identity/Portrait can map to large icon/Person; Content/Thumbnail can map to BigPicture. Android image options select exact roles/resource handles where needed. |
| BigText, Messaging, Media | Plain additional text may combine into BigText. Sender and timestamp support a message; a future Android conversation extension carries message history, people, shortcut/conversation identity and MessagingStyle. MediaStyle requires a media-session token and native transport actions; keep it in Android options. |
| Progress and Live Updates | Semantic fraction/null maps to setProgress. Update resubmits the same native identity when needed, respecting native alert-once behavior. New progress-centric styles/promoted ongoing Live Updates have API/eligibility requirements and belong to Android options/capabilities. No Windows counter is involved. |
| Actions, PendingIntent, RemoteInput | Application/OpenUri describes intent; the Android host chooses Activity/BroadcastReceiver/service mechanics. Required input IDs map to RemoteInput result keys. Direct reply requires appropriate PendingIntent mutability; immutable normal intents and mutable reply intents are distinct native contracts. URI grants/data input and suggestion/free-form behavior use action/input extensions. |
| Groups, summaries and categories | Group is the logical grouping key; Android group summary, sort order, alert behavior and category are options. They do not replace logical notification identity or invent a common summary notification. |
| Conversations, Person and bubbles | Sender maps to Person. Shortcut IDs, long-lived conversation metadata, bubble intent/icon/height and user eligibility remain Android extensions with runtime capability/policy checks. A portrait alone is not a conversation entitlement. |
| Foreground service / ongoing | A notification does not start or own a foreground service. The app's existing Android host/service lifecycle handles startForeground, type declarations, restrictions and stop behavior. Ongoing, service visibility and category are Android options; no hidden background work is created by common Show. |
| Badges, privacy, lock screen, full-screen | Badge count/icon behavior and public/private/secret visibility plus a redacted public version are Android options. Full-screen/time-sensitive intents require platform-specific permission, channel eligibility and native app policy; High urgency never grants them. |

Reference additions: [Notification.Builder](https://developer.android.com/reference/android/app/Notification.Builder),
[notification permission](https://developer.android.com/develop/ui/views/notifications/notification-permission),
[Live Updates](https://developer.android.com/develop/ui/views/notifications/live-update).
These are adapter design decisions derived from the native contracts, not Android implementation evidence.

## Future Apple mapping: shared vocabulary, separate macOS and iOS hosts

Use a future **AppleSystemNotificationOptions** for the shared UserNotifications vocabulary,
with additive **IOSSystemNotificationOptions** and **MacOSSystemNotificationOptions** only for
host-specific settings. One common notification can carry all three typed extensions. Do not
ship empty types pretending to implement these platforms today.

| Native concern | Mapping / future extension boundary |
|---|---|
| UNUserNotificationCenter / authorization | Each host owns the native center/delegate and queries notification settings. Explicit authorization requests map alert/sound/badge selection and optional Apple provisional/other authorization flags. Denied/restricted status remains separate from capabilities; quiet/provisional authorization must not claim alert permission. |
| UNNotificationRequest identifier | Encode the complete logical Id/Group pair into a deterministic native identifier; preserve original keys in userInfo. Same-identifier replacement and delivered-notification removal stay distinct from pending scheduled requests. |
| Content / attachments / sound / badge | Title/body and additional text map to supported content fields or documented combination. Images map to local UNNotificationAttachment assets with provider validation/copy ownership. Attachment type/thumbnail hints, sound name/critical sound and badge number are Apple options. An OS sandbox may require retaining/copying assets; common never silently downloads them. |
| Categories / actions / text input | Register category/action descriptors before delivery, using deterministic category identities or explicitly configured category IDs. Common action IDs/data map through userInfo and category action IDs; UNTextInputNotificationAction supports text reply. Advertise TextInput and native InputsPerAction limits; no fictitious selection widget. Destructive/authentication/foreground flags are Apple action options. |
| Grouping / person | Group maps to threadIdentifier in addition to the replacement identifier. Sender metadata can contribute content/person presentation, but communication notifications/Siri intents and entitlements require Apple extensions; a common Sender does not automatically grant native communication presentation. |
| Interruption / relevance | Default/Low/High are preferences only. Exact passive/active/time-sensitive/critical interruption level and relevanceScore belong to Apple options. Probe SDK/OS and entitlement/settings; no promise of Focus bypass. |
| Response and foreground presentation | didReceive response becomes copied Activation with action ID and text input. willPresent decisions belong to host/Apple configuration (banner/list/sound/badge), not a second UI/lifecycle loop. Subscribe the center delegate during startup and finish native completion handlers correctly. |
| Scheduling | Show means immediate submission (nil trigger where supported). A future scheduler uses a separate pending-request contract with triggers, enumeration and cancellation. ExpiresAt is not a schedule and must be reported unsupported if no equivalent expiry is available. |
| Progress | UserNotifications is not a generic live progress bar. Do not advertise Progress/LiveUpdates because content can be replaced. iOS ActivityKit/Live Activities would be a distinct integration, not a fake progress toast. |

**iOS:** native lifecycle launch/response handling, permission presentation and foreground decisions
belong to the iOS host. UserNotifications content/text actions/thread identifiers date from iOS 10;
interruptionLevel/relevanceScore from iOS 15. Runtime authorization, Focus, critical-alert
entitlements and app lifecycle restrictions must be qualified on real iOS devices.

**macOS:** UserNotifications host/delegate/identity setup is separate from iOS; content/text actions/
thread identifiers are available from macOS 10.14, interruptionLevel/relevanceScore from macOS 12.
The same API metadata does not promise identical banners, summaries, action layout, sandbox
attachment rules or permission UX. Probe and test the selected macOS host independently.

Sources: [interruptionLevel](https://developer.apple.com/documentation/usernotifications/unnotificationcontent/interruptionlevel),
[relevanceScore](https://developer.apple.com/documentation/usernotifications/unnotificationcontent/relevancescore),
[threadIdentifier](https://developer.apple.com/documentation/usernotifications/unnotificationcontent/threadidentifier),
[text input actions](https://developer.apple.com/documentation/usernotifications/untextinputnotificationaction).
Apple DocC platform availability metadata was checked for both macOS and iOS; no Apple runtime was executed.

## Future Linux mapping: select and probe the actual transport

**Freedesktop Notifications / D-Bus:** GetCapabilities determines supported actions, body markup,
images/icons, sound and persistence; do not infer them from a distribution or kernel version.
Common plain text must be escaped if the server supports markup. Urgency maps to the native hint;
image-data/path/app-icon support and action presentation vary. Notify returns a uint32 server ID,
and replaces_id updates an existing entry. A future adapter needs an app-owned mapping scoped to
the notification server/bus lifetime; IDs cannot be safely reused after a server restart. There is
no standard history enumeration API, so History remains false. CloseNotification may still support
Removal. ActionInvoked/NotificationClosed are live D-Bus signals, not general cold-start guarantees;
activation-token support is optional and fits typed Activation.PlatformData.

**XDG Desktop Portal:** prefer the broker in Flatpak/sandbox contexts instead of assuming direct
session-bus permission. AddNotification uses an application-scoped string ID suitable for stable
logical pair encoding; RemoveNotification uses the same ID. Portal version and advertised options
must be queried. Version 2 SupportedOptions advertises category/button-purpose support, not an
universal replacement for freedesktop GetCapabilities. V2 has richer icon/sound/priority/display
hints and reply-purpose button data; gate these against the portal version and response. Native
`app.*` actions can use exported GApplication D-Bus activation; ordinary ActionInvoked signals
require a live client. ColdActivation is advertised only when the application's activation contract
is actually registered. Neither portal existence nor a string ID implies general native History.

Future LinuxSystemNotificationOptions may carry server/portal hints, desktop identity, action
names/targets, category, display privacy/persistence and supported native resources. A native
history Reference can wrap a server ID/token without exposing it as the common logical ID.
Progress is false unless the actual transport/server offers an appropriate mechanism; native
replacement can still be true. Flatpak permission/broker failure maps Access separately from
supported features. No fake polling history, progress bar or replacement timer is introduced.

Sources: [Freedesktop protocol](https://specifications.freedesktop.org/notification/latest/protocol.html),
[portal Notification v2](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Notification.html).

## Design scenarios and #44

| Application | Common model | Native additions; remaining limit |
|---|---|---|
| ModernTubeDownloader on Windows | Thumbnail, progress, same-key completion, Open/Open folder action IDs and cold response payload | Existing App SDK registration and application-owned persisted download lookup. No common redesign. |
| Future Android downloader | Same content/progress/action model, explicit permission query/request | Channel and small-icon configuration, native progress/foreground-service host integration. No Windows sequence or 16-character key. |
| Future Android/iOS/macOS messenger | Sender, message/image, timestamp, actions, required text reply ID, Group and activation payload | Android MessagingStyle/conversation or Apple category/person integration through typed options. Selection and multiple inputs depend on capabilities. No common redesign. |
| Future Linux app | Basic text, dynamic Actions/Replacement/Removal capabilities, logical keys and responses | D-Bus mapping lifetime or portal string identity/activation contract. Missing native history/cold launch remains explicitly unsupported. |

No reviewed scenario requires a known breaking change to the six core content/service/capability
types. Future providers necessarily add concrete options, host registration and native code;
that is additive, not a promise that every platform can perform every operation. Full push,
scheduling, media/session ownership, foreground services and Live Activities remain separate
contracts. Runtime implementation may reveal further constraints; architecture review is not
cross-platform certification.

The common notification sources use managed BCL types and no platform SDK references. An isolated,
ignored source-only probe compiles the same contracts, validation and service base for net8.0,
net9.0 and net10.0. It does not retarget the framework, qualify package dependencies, or implement
#44. The existing solution remains .NET 10; future target/OS support still requires #44's wider audit.

## Validation scope

See [recorded stage-2 results](../testing/system-notifications.md). Portable fake profiles test
capability/access combinations and snapshot/degradation behavior, not native future-platform
implementations. Native Windows probes remain separate from real user clicks. Stage 2 did not
commit or publish changes. The subsequent [final review](system-notifications-final-review.md)
qualifies the combined feature for a pull request without a release, dependency upgrade or new
platform backend.
