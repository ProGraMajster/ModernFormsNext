# System notifications final review (#158)

Reviewed 2026-10-07 against current source and the full feature diff, with
[issue #158](https://github.com/ProGraMajster/ModernFormsNext/issues/158) and
[roadmap #44](https://github.com/ProGraMajster/ModernFormsNext/issues/44).
The feature branch was fast-forwarded from 1de8a6b to master cc00609 before final qualification;
all 46 pre-existing feature files retained identical SHA-256 checksums during that update.
This document records the pre-commit review; GitHub history supplies the resulting commit/PR identity.

## Review conclusions

1. **Public API audit:** one application-facing contract namespace, ModernFormsNext.Notifications;
   one canonical service registry, lifecycle and dispatcher. All content members were reviewed
   against the member table in the [cross-platform design](system-notifications-cross-platform.md).
   The older empty internal platform placeholders are not public aliases or active providers.
2. **Remaining Windows leaks:** no native placement, XML/scenario, sequence or tag limit remains
   in application-facing common types. A shared backend removal parameter still called a logical
   ID “tag”; its name is corrected to id. Generous common allocation bounds remain deliberate.
3. **Corrections:** Windows group hashing incorrectly reused nonblank-ID validation and rejected
   valid whitespace-only groups. Classic live template activation omitted Group. Hashing now
   preserves every valid group; the classic callback decodes the exact launch envelope sent to
   Windows. Tests cover both, Unicode, ordinal distinctions, full 4096-unit identity bounds,
   foreign options and null/type-changing option snapshots. The sample's progress counter is
   named for its UI purpose and native smoke exercises 20 rapid updates followed by a lower value.
   A third architectural gap was the global-only permission gate: protected per-operation access
   hooks now allow native channel restrictions/exemptions while retaining the global status.
   Two controlled-transport cases cover both directions without a fake platform implementation.
   The guide now explicitly documents asynchronous native status/history query failures.
4. **Common API:** logical Id/Group, text, semantic images, actions/required inputs, progress,
   timestamp/expiry/urgency/sender, copied activation, native history references and typed options.
   No scheduler, cloud push client, OS permission UI or native rendering is implemented in common.
5. **Windows API:** retained image layout/crop/query, headers, attribution, actions, system/protocol
   mechanisms, icons/tooltips/styles/context menu, scenarios/duration/audio, priority/suppression,
   expiry/reboot/mirroring, history, raw XML/expanded content, classic templates and Shell options.
   Existing exclusions remain explicit: UWP background tasks and experimental SDK features.
6. **Capabilities:** semantic flags plus a typed platform capability snapshot and backend limits.
   Removal is independent of history. Windows probes real APIs; future Linux must query its transport.
7. **Access:** current permission/status is independent of features. Explicit RequestPermission
   never fabricates Windows consent. AllowedPresentation carries known partial permissions;
   future native authorization details can be additive platform extensions. Global access cannot
   promise an Android channel is enabled or an Apple Focus policy permits a banner. The protected
   GetShowAccessCoreAsync/GetUpdateAccessCoreAsync hooks permit per-operation evaluation without
   rewriting the canonical serialization/validation service or falsely changing global permission.
8. **Identity:** logical ordinal keys, stable 96-bit SHA-256 native encoding, an empty-group sentinel
   distinct from its literal logical spelling, payload-based history recovery and opaque removal
   of unknown/raw native entries. Hash collision resistance is finite, not a collision-free claim.
   The full common identity bound is distinct from Windows' 5000-byte total XML limit.
9. **Progress:** no public sequence counter. The service serializes calls; WinRT uses documented
   sequence-zero semantics and the SDK-only numeric path reserves a durable positive sequence.
   A later smaller percentage is valid application state; callers order their business updates.
   Native values, restart, replacement and removal are checked independently of acceptance codes.
10. **Options:** exact concrete type keys, duplicate/type-changing/null snapshot rejection, copied
    collections and documented immutable/deep-copy extension ownership. Foreign types coexist
    without core referencing platform assemblies; a provider only interprets its own extensions.
11. **Android:** channels/small icon/importance/styles/media/conversations/bubbles/foreground
    services/privacy remain native extensions; shared actions/inputs/progress/sender/identity map
    to native notifications, PendingIntent and RemoteInput through the future Android host.
12. **macOS:** separate host/delegate/identity and capability qualification, using UserNotifications
    content, categories, text responses, attachments and authorization. Apple options can be shared;
    macOS-specific extensions and presentation rules remain separate.
13. **iOS:** the same semantic vocabulary with an iOS lifecycle/authorization implementation and
    iOS-specific options/entitlements. Provisional/ephemeral and per-setting authorization must not
    be collapsed into guaranteed alerts. Scheduling and Live Activities are separate integrations.
14. **Linux:** distinguish freedesktop D-Bus IDs/live signals from portal string IDs/app activation;
    query dynamic capabilities and portal version/options. No fabricated native history, process
    restart guarantee for stale D-Bus server IDs, or assumption that Flatpak permits direct D-Bus.
15. **#44:** common assembly has no NuGet/platform dependency or Windows TFM. An isolated probe
    recompiles the actual common notification sources for net8/net9/net10. No solution targets
    change and no Windows 7/8/8.1 runtime support is claimed.
16. **Changed files and cleanliness:** the [complete file list](../testing/system-notifications.md#files-changed)
    covers the combined feature. Ignored artifacts, packages, caches and private probes remain
    outside the commit. The existing sample COM script is intentional reproducible tooling.
17. **Validation:** see the final-review result table in the [testing report](../testing/system-notifications.md).
    Results are recorded after rebuilding the current master base; earlier stages remain historical.
18. **Manual limits:** the stage-2 real cold Send confirmation remains applicable to the unchanged
    App SDK registration/startup/serialization/lifecycle/input path. Only the classic live-template
    callback changed; that path advertises no cold activation. Its callback regression probe is
    synthetic and does not certify a native user click. Other OS editions, fresh classic installer,
    MSIX, reboot, audio/DPI/focus matrix and future platforms remain NOT EXECUTED where unavailable.
19. **Separate future work:** native Android/macOS/iOS/Linux providers and host integration; #44
    target/package/CI qualification; native scheduling and push; media/foreground service/Live
    Activity integration; OS/installer/manual matrix qualification. None is a hidden backend here.
20. **Fundamental breaking change:** none is currently known for the reviewed Android/macOS/iOS/Linux
    scenarios. Concrete native options, registration and additional APIs will be necessary;
    architecture review is not a guarantee that implementation reveals no further constraints.

The Android operation-access correction follows the official [runtime permission exemptions](https://developer.android.com/develop/ui/views/notifications/notification-permission#exemptions).
Apple settings were cross-checked against [UNNotificationSettings](https://developer.apple.com/documentation/usernotifications/unnotificationsettings),
including its DocC metadata. Linux transport differences were rechecked against the
[freedesktop protocol](https://specifications.freedesktop.org/notification/latest/protocol.html) and
[portal v2](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Notification.html).

## Qualification state

Final qualification passed on Windows 11 Home 26H2, build 26300.9550 x64, SDK 10.0.401:
restore; complete Debug/Release builds with 0 warnings/errors; 4219/4219 full Debug tests;
90/90 notification tests; common net8/net9/net10 compilation; five packages and a fresh-cache
consumer; expected negative projection diagnostic; App SDK, Classic and Shell native smokes;
rapid/restarted native progress, logical history/identity/removal, and the synthetic Classic
callback regression. No test failures/skips or retries were required. The full evidence scope,
reproduction commands, 47-file inventory and unexecuted manual matrix are in the testing report.
`git diff --check` and documentation link checks passed before commit.

This review found no unresolved fundamental common-API redesign requirement. The finalization
request authorizes a commit, push and PR to master after this report; merging, releases, version
changes and manual issue closure remain outside this task.
