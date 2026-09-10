# Runtime queue: initial audit

Audit date: 2026-09-10. Source baseline: `ba396f95adab82564a0681bc922096599ba8c1ca`,
confirmed equal to fetched `origin/master`. All eight issues below are OPEN. Full bodies and
all comments were read through GitHub; historical validation in comments is not current evidence.
This is an initial scope/dependency audit, not implementation acceptance.

## #64 — Add headless TestHost and application UI testing infrastructure

Sources: [issue and comments](https://github.com/ProGraMajster/ModernFormsNext/issues/64),
[PR #93](https://github.com/ProGraMajster/ModernFormsNext/pull/93),
[host](../../ModernFormsNext.Testing/ModernFormsTestHost.cs),
[window](../../ModernFormsNext.Testing/TestWindowHost.cs),
[dispatcher](../../ModernFormsNext.Testing/UiTestDispatcher.cs),
[guide](../testing/testhost.md), [tests](../../ModernFormsNext.Testing.Tests).

Already: Phase 1 real Form/control hosting, scoped window factory and dispatcher, layout, viewport,
scale, immutable structural snapshots, bounded drain, exception diagnostics and resilient cleanup.
Commands subsequently added consumer tests; 104 Testing tests on this baseline are not proof of
input simulation. `HeadlessWindowImpl` exposes neither a surface nor input injection helpers.
`UiTestDispatcher.WaitForIdleAsync` already exists and must not be reimplemented.

Missing: raw pointer/keyboard/text helpers, deterministic double-click timing, focus inspection,
controllable production animation scheduling, raster snapshots and ecosystem service adapters.
The production paths are `WindowBase.OnInput`, `ControlAdapter`, `Control.Raise*`,
`AnimationScheduler`, and `WindowBase.DoPaint`. Tab is currently processed by the raw text path.
Binding/resources/theme resolution already execute unchanged in the host.

| Acceptance | Initial status | Remaining evidence/work |
|---|---|---|
| Headless tree and real layout | PASS (source) | Retain existing tests |
| Pointer/key routing | PARTIAL | Add public helpers through backend raw input |
| Focus/Tab | PARTIAL | Expose canonical focus and drive production Tab path |
| Sleep-free animations | PARTIAL | Scope real scheduler clock/tick source |
| Binding/resources/themes | PASS (source) | Expand public consumer coverage |
| Layout/state snapshots | PASS (source) | Add optional real off-screen paint capture |
| Native integration kept separate | PASS (architecture) | Explicit platform evidence limits |
| Supported package/API | PASS (source) | Additive XML docs/examples/package smoke |
| Phase 4 future ecosystem coverage | PARTIAL | Activation #63, navigation #12, virtualization #55 do not yet exist |

No hard predecessor prevents Phases 2/3 or existing-system Phase 4 work. Future integration
must remain explicitly pending until its production contract exists, rather than inventing
testing-only lifecycle/navigation/virtualization systems.

## #63 — Add cross-platform application lifecycle and activation model

[Issue](https://github.com/ProGraMajster/ModernFormsNext/issues/63) has no comments.
Already: `Application`, `WindowBase` close/deactivation, shared
`WindowKit.Backend/Lifecycle/IPlatformApplicationLifecycle` with Unknown/Foreground/Background/NoHost,
Android `AndroidActivityTracker`, `AndroidSurfaceHostState`, animation lifecycle subscription
in `AnimationScheduler.Lifecycle.cs`, and sample Activity recreation. PR #54 supplied Android
animation runtime; [AND-05](../known-limitations.md) records the remaining shared-policy gap.

Missing: normalized application activation/state-restoration/inset contracts, ordering and
application-vs-window lifetime policy, Windows mapping, Android intents/recreation integration,
bounded privacy-safe transition diagnostics and test backend. Extend the existing shared backend
contract additively. #72 is OPEN but broader Android windowing is not a prerequisite for these
contracts; #60/#20 remain OPEN future consumers, not permission to implement native hosting/WebView.

Acceptance: common callbacks PARTIAL; Windows/Android parity PARTIAL; safe normal/argument/file/URI
activation PARTIAL; deterministic ordering PARTIAL; native-host hooks PARTIAL; future restoration
and forwarding readiness PARTIAL. Added audit checkboxes (restoration, safe areas, active subsystem
stress and lifetime distinctions) remain PARTIAL. Device/recreation behavior needs separate evidence.

## #62 — Add IME and advanced text input composition infrastructure

[Issue](https://github.com/ProGraMajster/ModernFormsNext/issues/62) has no comments.
Already: `SkiaControlSurface` text composition/edit operations, `ControlSurfaceTextInputState`,
Android `AndroidTextInputState` and `AndroidSkiaHostView` input connection, TextBox Unicode editing,
Windows raw text/IME messages and IMM32 helpers. The issue explicitly acknowledges the Android
TextBox implementation. [Android guide](../platforms/android.md), AND-04, and editor guides
record incomplete control/vendor/language coverage.

Missing: shared client abstraction beyond TextBox-specific surface code, bounded surrounding
text, caret/composition session lifecycle across controls and platforms, Windows candidate/AltGr
validation, advanced editor integration. Reuse focus, raw committed-text path and Android logic.
The broad #69 device matrix is OPEN and excluded from this queue; its manual observations cannot
be substituted by unit tests. #63 supplies recreation semantics, not a second IME runtime.

Acceptance: separate committed text PASS (source); international input PARTIAL; explicit composition
PARTIAL; Android composing/commit PARTIAL; stale-session cleanup PARTIAL; common editor layer
PARTIAL. Additional cross-control/API/vendor/candidate/focus/recreation matrix remains PARTIAL.

## #109 — Complete Android hardware shortcut forwarding and modifier parity

[Issue](https://github.com/ProGraMajster/ModernFormsNext/issues/109) has no comments.
#56 is CLOSED: PRs #105/#106/#107/#110 provide CommandSource, KeyGesture/InputBinding,
hierarchical resolver, routed and async commands. Current `AndroidSkiaHostView.PublishKey`
forwards only editing/navigation subset; shared `SkiaControlSurface.ProcessKeyDown` already
has the canonical resolver and explicit text-input separation.

All six acceptance items are PARTIAL: full key/modifier mapping; down/up/repeat/handled lifetime;
IME/dead-key/AltGraph safety; route/fallback integration; mapping plus native evidence; documented
API/device/layout limits. Execute after #62 per queue. #72 is related, not a hard windowing blocker.
Physical hardware keyboard observation: NOT EXECUTED — environment unavailable.

## #59 — Add accessibility and platform UI automation infrastructure

[Issue/comments](https://github.com/ProGraMajster/ModernFormsNext/issues/59), PRs #102/#103/#104.
Already: canonical `AccessibleObject` identity, roles/states/actions/logical children and privacy;
Windows MSAA/UIA provider; Android mapper/session/provider; windowless surface event transport;
ControlGallery AccessibilityPanel; semantic/native backend tests. The issue body's suggestion
that Android mapping is absent is outdated; the later Phase 3 comment and source supersede it.

Missing: Phase 4 broader controls, advanced Text/full scroll patterns, missing-name diagnostics,
updated cross-platform matrix and future virtualized/DevTools integration. Reuse the existing
semantic tree. #55 and #61 are future integration dependencies, not blockers for present controls.

Acceptance: common control coverage PARTIAL; focus notifications PASS (source); advanced action/
pattern breadth PARTIAL; custom logical children PASS (source); neutral API PASS (source);
default semantics PARTIAL; original renderer/tree preserved PASS. Historical TalkBack emulator
results in #104 are historical only. Physical device: NOT EXECUTED — environment unavailable.

## #58 — Add runtime performance overlay, profiling counters and diagnostics hooks

[Issue](https://github.com/ProGraMajster/ModernFormsNext/issues/58) has no comments.
Already: isolated `AnimationSchedulerDiagnostics`, Android animation diagnostics, font diagnostics,
paint/layout/invalidation code and shader scope lifetime. No unified profiler or overlay exists.
REN-02 documents per-scope shader allocations. Extend instrumentation at real pipeline boundaries;
do not infer frame timing from a periodic FPS timer or report unsupported GPU counters as zero.

All seven acceptance items are PARTIAL: frame/layout/render metrics, independent programmatic API,
backend/scale visibility, opt-in expensive detail, disabled overhead, contextual slow-frame records,
extensible hooks. Additional shader create/dispose counters, allocation budgets and gradient/clipping/
animation/layout/redraw stress remain PARTIAL. #46 GPU and #55 virtualization are future providers;
neither blocks the instrumentation foundation. Overlay is secondary to the data contract.

## #61 — Add runtime Developer Tools and Visual Tree Inspector

[Issue](https://github.com/ProGraMajster/ModernFormsNext/issues/61) has no comments.
Already: canonical hit testing in Control/SkiaControlSurface, resource resolution and binding
diagnostics, accessibility semantics, command diagnostics, animation diagnostics and TestHost
snapshots. A runtime inspector/picker UI and coherent inspection API do not exist.

All seven acceptance items are PARTIAL: select rendered control, hierarchy/layout properties,
winning resource scope, focus/effective state, production logic reuse, disabled overhead,
profiler integration. Execute after #58; consume its metrics. #59 is sufficient for initial
accessibility inspection; #60 future native-host diagnostics must remain explicitly unavailable.

## #97 — Add agent automation bridge and optional MCP adapter for running applications

[Issue/comments](https://github.com/ProGraMajster/ModernFormsNext/issues/97), PRs #111/#112,
[core guide](../automation.md), [live bridge](../automation-live-bridge.md).
Already: separate optional core and Windows packages, borrowed canonical roots/session identity,
bounded immutable queries/actions, authenticated same-user local named pipes and discovery,
public client/CLI, bounded wait subscribe/re-read/reconciliation/cancel and dispatcher checkpoint.
Source includes real native-process/security tests. Checkpoint deliberately does not promise global idle.

Missing: Phase 1c ControlGallery example and integrated Focus/Select acceptance/docs; broader
events/diagnostics/screenshots; optional MCP adapter. #59 supplies semantics today; #58/#61
diagnostic integration waits for those features, but is not a blocker for Phase 1c.

Acceptance: explicit enable PASS (source); external discovery PASS (source); semantic tree PASS;
representative focus/select workflows PARTIAL; semantic waits PASS; externally consumable events/
diagnostics PARTIAL; optional MCP PARTIAL; canonical reuse PASS; default-off PASS; Release omission
PASS (architecture). No MCP dependency may enter core. Native security observations in comments
are historical, not rerun evidence. Different user/second machine: NOT EXECUTED — environment unavailable.
