# Autonomous queue: initial Series 3 audit, issues 20–28

Audit date: 2026-09-10. Source baseline: `master` at
`ba396f95adab82564a0681bc922096599ba8c1ca` (PR #112 merge). This is the initial
read-only audit required before the queue starts, not the issue-specific re-audit after Series 2.
Recheck `master`, issues, comments, and dependencies before implementing each issue.

The full current issue bodies and comment collections were retrieved with `gh issue view` for
#14, #89, #45, #77, #73, #74, #76, #78, and #88. All nine are **OPEN**, have **zero comments**,
and have no milestone at this snapshot. Local raw responses are under ignored
`artifacts/autonomous-audit/issue-<number>.json`; PR history is recorded in
`artifacts/autonomous-audit/series3a-pr-history.json`. No issue, PR, branch, or commit was changed.
Only this audit document was added. Existing `.codex/config.toml` is unrelated local work.

No build, test, application, emulator, physical device, screen reader, or Visual Studio check was
executed by this audit. References to tests below mean inspected test source; historical PR results
are not current execution evidence. Acceptance rows are an initial implementation assessment:
**PASS** means the existing code/documented foundation satisfies that particular subrequirement;
**PARTIAL** includes missing behavior or validation. None of these issues is COMPLETE.

## Shared foundations and dependency assessment

| Foundation | Live GitHub state | Evidence and consequence |
| --- | --- | --- |
| ThemeManager #7 | CLOSED | PR [#17](https://github.com/ProGraMajster/ModernFormsNext/pull/17), merge `002b9209`: atomic UI-thread application, fallback resource precedence, theme JSON, immutable snapshots, overrides, transition planner, diagnostics. Reuse this infrastructure. |
| Animated layout #25 | CLOSED | PR [#49](https://github.com/ProGraMajster/ModernFormsNext/pull/49), merge `5f2b4306`: production logical bounds plus presentation geometry, scheduler ownership, hit testing and recursive cancellation. |
| Visual-state layout metrics #26 | CLOSED | PR [#50](https://github.com/ProGraMajster/ModernFormsNext/pull/50), merge `befded76`: padding and border widths, grouped layout, content-cache updates, double-animation suppression. |
| Brush compatibility #27 | CLOSED | PR [#51](https://github.com/ProGraMajster/ModernFormsNext/pull/51), merge `b120273c`: same-kind gradients, unequal nonempty stop counts, solid/gradient promotion, deterministic fallback. |
| Android animation runtime #29 | CLOSED | PR [#54](https://github.com/ProGraMajster/ModernFormsNext/pull/54), merge `171855a8`: Choreographer, lifecycle pause/rebase, animator-duration-scale observation, shared scheduler. Historical partial emulator smoke is not evidence for new system-theme behavior. |
| Designer/runtime layout parity #42 | CLOSED | PR [#92](https://github.com/ProGraMajster/ModernFormsNext/pull/92), merge `0fb159f2`: reusable parity harness; RTL explicitly remains excluded. PR [#100](https://github.com/ProGraMajster/ModernFormsNext/pull/100), merge `145b2419`, subsequently changes ScrollableControl and mouse-wheel routing and must remain in RTL regression coverage. |
| Profiling #58 | OPEN | Earlier queue work supplies measurement infrastructure for #74, #76, #78. Existing deterministic allocation tests remain usable now; an open profiling issue does not prohibit source audit or algorithm work. |
| Accessibility #59 | OPEN | Canonical semantics, Windows UIA and Android virtual nodes already exist. Earlier queue work must be rechecked; Phase 4 being open is not a blanket blocker on every later control. |
| Lifecycle #63 | OPEN | Existing backend lifecycle and animation policies are real infrastructure. Earlier queue work must establish the broader activation contract consumed by #77 and #88. |
| GPU #46 | OPEN; excluded from this session | Related future integration for Brush rendering only. **Not a dependency blocker** for #76 or shared Skia rendering. |

The user's order is an execution constraint. It is separate from a compile-time or architectural
dependency. Real integration edges are `resources/theme -> #14`, `shared direction semantics -> #89`,
`system appearance contract -> #45/#77`, `lifecycle -> #77/#88`, and existing scheduler/planner/layout
foundations into #73/#74/#76/#88. #89 follows #14 in the queue but its layout algorithms can be tested
with explicit Arabic/Hebrew strings without a localization runtime. #74 should consume typography
metrics only where actual measurement/rendering support exists; the later #78 is not a reason to
invent a private typography pipeline or silently claim those metrics work.

No external blocker was demonstrated for the implementation portions of these nine issues.
Missing device or visual validation is a remaining acceptance item, not a fabricated dependency.
Do not mark an entire issue BLOCKED merely because its acceptance suite has not yet been run.

Cross-cutting sources inspected: [CHANGELOG](../../CHANGELOG.md),
[roadmap](../roadmap/ModernFormsNext-Framework-Roadmap.md),
[known limitations](../known-limitations.md), [dynamic resources](../dynamic-resources.md),
[themes](../themes.md), [animation guidance](../animations.md), the linked architecture documents,
ControlGallery panels, framework tests, Designer tests, Windows and Android source. The release
changelog describes shipped foundations; it is not a ledger of all later master commits.

## #14 — Add JSON-based localization system

Issue: [#14](https://github.com/ProGraMajster/ModernFormsNext/issues/14). Initial status: **PARTIAL**
(resource foundation exists; localization implementation missing).

Already implemented: canonical `ResourceDictionary`, dynamic CLR property references, dispatcher
delivery, resource precedence/lifetime, atomic theme resource publication, culture-aware formatting
in individual controls. The [localization ADR](../architecture/decisions/ADR-Localization-System.md)
is explicitly **Proposed**, not an implemented API: provider resolution, exact/neutral/default
culture fallback, namespaced keys, requested-key publication through dynamic resources, separate
plural-rules service and opt-in culture direction. Runtime searches find no `LocalizationManager`,
`ILocalizationProvider`, `Localizer`, or `SetLocalizedText` implementation. Neither ControlGallery nor
templates contain the requested localization integration.

| Issue scope / acceptance direction | Initial result | Work still required |
| --- | --- | --- |
| JSON language files | PARTIAL | Implement a bounded validated JSON catalog/provider using established serializer conventions. Theme JSON is a different schema. |
| Culture fallback and neutral cultures | PARTIAL | Implement deterministic exact, parent/neutral, default and missing-key resolution; validate invalid cultures and cycles/duplicate inputs where relevant. |
| Runtime language switching | PARTIAL | Add UI-thread commit and culture-change semantics; test rapid requests, cancellation and rollback/error behavior. |
| Typed or key-based resource access | PARTIAL | Key-based lookup is enough for this alternative; typed generation may remain optional, with the scope explicit. |
| Control-tree refresh without recreating app | PARTIAL | Reuse keyed dynamic references and production setters, including detached/reparented/disposed controls; do not add per-control localization event plumbing. |
| Parameterized messages and pluralization readiness | PARTIAL | Define format grammar, culture selection, parameter validation and explicit plural extension semantics. Do not present English singular/plural branches as global plural rules. |
| Missing-key diagnostics | PARTIAL | Define safe bounded diagnostics and missing-key fallback policy. |
| Embedded and external packs | PARTIAL | Explicit resource/stream/file loading and ownership/cancellation; no implicit adjacent file execution. |
| Designer and template integration | PARTIAL | Safe declarative metadata and examples, preserving data-only Designer and minimal default template. |
| Windows and Android culture behavior | PARTIAL | Shared culture contract plus platform-specific acquisition where needed; preserve UI-thread rules. |
| Documentation and samples | PARTIAL | Accept/update ADR, add schema/use guide and ControlGallery language-switch sample. |

Technical route: extend the canonical resources model through a localization-owned namespace and
explicit provider lifetime. Keep ordinary application overrides authoritative. Formatting culture
and UI culture must be documented separately. Test with [DynamicResourceTests](../../ModernFormsNext.Tests/DynamicResourceTests.cs)
and ThemeManager's existing atomic publication patterns. No breaking API or new large dependency
has been justified. Windows/Android rendered text and culture switching, interactive Designer and
template smoke remain **NOT EXECUTED** in this audit.

## #89 — Complete right-to-left FlowLayout and TableLayout behavior

Issue: [#89](https://github.com/ProGraMajster/ModernFormsNext/issues/89). Initial status: **PARTIAL**
(production engines exist; inherited RTL semantics and complete behavior missing).

There is an explicit `FlowDirection.RightToLeft` proxy, not a complete inherited control direction
system. [FlowLayout.ContainerProxy](../../ModernFormsNext/Layout/FlowLayout.ContainerProxy.cs)
hardcodes `_isContainerRTL = false` and retains commented scrolling corrections.
[TableLayout.SetElementBounds](../../ModernFormsNext/Layout/TableLayout.cs) similarly initializes
`isContainerRTL` to false with the `control.RightToLeft` read commented out. Runtime source has no
live `Control.RightToLeft` property; references in these paths are commented. Thus the issue's
reference to selected inherited direction behavior must not be read as a complete public inheritance
contract. Existing table span and margin code contains a dormant RTL branch worth extending rather
than replacing.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| RTL inheritance and explicit direction semantics | PARTIAL | Add a documented additive inherited direction contract and precedence relative to explicit FlowDirection; define invalidation on ancestor changes/reparent. |
| Flow order, wrap, alignment, margins, auto-scroll coordinates | PARTIAL | Complete proxy transforms and scrolling origin policy in the production FlowLayout/ScrollableControl path. |
| Table visual ordering, alignment, spans, nested containers | PARTIAL | Activate and verify production RTL positioning, logical column indexing and visual mapping. |
| Preserve Dock/Anchor/Padding/Margin, DPI, hit testing, keyboard navigation | PARTIAL | Existing infrastructure must be regression-tested under RTL and mixed-direction trees. |
| Runtime/Designer parity cases | PARTIAL | Extend the existing [parity suite](../../ModernFormsNext.Designer.Tests/DesignerRuntimeLayoutParityTests.cs); current scenarios cover LTR/top-down/wrap and explicitly exclude full RTL. |
| Localized text and accessibility directionality | PARTIAL | Add Arabic/Hebrew examples, keyboard focus and semantic bounds/order checks; do not infer full bidi text support from geometry tests. |

Use existing [FlowLayoutPanel](../../ModernFormsNext/FlowLayoutPanel.cs),
[TableLayoutPanel](../../ModernFormsNext/TableLayoutPanel.cs),
[ScrollableControl](../../ModernFormsNext/ScrollableControl.cs), Designer projections and
[parity guidance](../architecture/designer-runtime-layout-parity.md). Add ControlGallery cases to
the existing Flow/Table panels. #14 supplies culture-to-direction opt-in integration; #42 is already
closed; accessibility backends already provide the canonical semantics to validate. Runtime and
Designer layout changes are deterministic test work. Arabic/Hebrew visual review, screen reader,
Windows/Android observation and monitor-derived DPI checks remain **NOT EXECUTED**.

## #45 — Add dynamic Windows 11 accent color and system theme integration

Issue: [#45](https://github.com/ProGraMajster/ModernFormsNext/issues/45). Initial status: **PARTIAL**.

Already implemented: [IPlatformThemeSettings](../../ModernFormsNext.WindowKit.Backend/PlatformThemeSettings.cs)
and [WindowsPlatformThemeSettings](../../ModernFormsNext.WindowKit.Backend.Windows/WindowsPlatformThemeSettings.cs)
read application light/dark on demand and reuse Windows animation settings. ThemeManager commits
resource snapshots atomically with override precedence, diagnostics and optional shared-scheduler
transitions. There is also an existing [PlatformColorValues](../../ModernFormsNext.WindowKit/PlatformColorValues.cs)
contract and WindowKit color-change event infrastructure. The Windows
[Win32PlatformSettings](../../ModernFormsNext.WindowKit.Backend.Windows/Avalonia.Win32/Win32PlatformSettings.cs)
live color implementation is commented out and `GetColorValues()` returns the default fallback.
It must not be mistaken for working Windows accent observation.

| Acceptance direction | Initial result | Work still required |
| --- | --- | --- |
| Opt into current Windows 11 accent | PARTIAL | Backend palette read, capability/fallback diagnostics, explicit independent accent/mode opt-in. |
| Manual OS accent change updates running app | PARTIAL | Live backend observation, deduplication/coalescing and UI-thread ThemeManager integration. |
| Wallpaper-driven accent changes update app | PARTIAL | Observe authoritative accent notifications rather than treating every wallpaper event as a palette change; validate both manual and automatic wallpaper paths. |
| System light/dark updates when enabled | PARTIAL | On-demand preference reading exists; automatic theme reapplication does not. |
| Custom themes/colors override system values | PARTIAL | Current resource/ThemeManager precedence is reusable; granular system follow/override policy is missing. |
| Repeated changes avoid leaks/uncontrolled redraws | PARTIAL | Existing atomic apply/invalidation batching helps; notification lifecycle and subscriber disposal need deterministic tests. |
| ControlGallery and documentation | PARTIAL | Extend [ThemeManagerPanel](../../samples/ControlGallery/Panels/ThemeManagerPanel.cs) and theme guide with capabilities, seven-color palette mapping, opt-out, fallback and platform limits. |

Technical route: audit both existing WindowKit color values and `IPlatformThemeSettings` before
choosing an additive observable contract. A new disconnected theme service would duplicate existing
infrastructure. Keep native APIs, observation, lifetime and native handles entirely in the Windows
backend; suppress runtime observers in Designer. Preserve immediate/non-system defaults and allow
independent accent and mode control. Follow the existing animation settings subscription/coalescing
pattern where appropriate. Actual OS accent, automatic wallpaper, light/dark, fallback and
multi-window observation remain **NOT EXECUTED**. No API names in the issue are binding: they are
explicitly conceptual.

## #77 — Add Android system appearance provider and automatic theme reapplication

Issue: [#77](https://github.com/ProGraMajster/ModernFormsNext/issues/77). Initial status: **PARTIAL**.

No Android `IPlatformThemeSettings` registration exists in
[AndroidWindowKitBackend](../../ModernFormsNext.WindowKit.Backend.Android/Platform/AndroidWindowKitBackend.cs).
ThemeManager still selects its explicit System fallback. Existing Android activity tracking,
surface configuration refresh and main-Looper dispatcher must be reused. Existing
[AndroidPlatformAnimationSettings](../../ModernFormsNext.WindowKit.Backend.Android/Platform/AndroidPlatformAnimationSettings.cs)
already observes animator-duration scale and contributes reduced-motion policy. A claim that Android
has no reduced-motion infrastructure is outdated; a claim that it lacks system light/dark theme
observation remains accurate.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| Read current light/dark through capability contract | PARTIAL | Backend provider using the shared #45 appearance contract and reliable Android configuration values. |
| Coalesced configuration changes on UI thread | PARTIAL | Connect configuration callbacks/observation to shared appearance publication, with duplicate and stale-generation handling. |
| Atomic ThemeManager reapply preserving overrides | PARTIAL | Existing apply path already supplies atomic resource behavior; system appearance integration is missing. |
| Recreation, background/foreground, missing capabilities, rapid duplicates | PARTIAL | Extend existing activity/lifecycle/configuration bridges without retaining destroyed Activities; consume #63 contract. |
| Reduced-motion/high-contrast only where reliable | PARTIAL | Reuse existing animator-scale policy; document unsupported high-contrast capabilities instead of inventing semantics. |
| Automated backend tests and recorded emulator/physical-device validation | PARTIAL | Add deterministic provider tests plus separate observed emulator and physical-device records. Neither was executed here. |

Technical route: application-context ownership and lifecycle-safe registration; bounded UI-thread
publication; dispose native observers when inactive/shut down as dictated by the final lifecycle
contract. Platform types stay out of core and Designer queries stay suppressed. Related Windows #45
should supply the shared contract; #63 is a real integration predecessor in the queue, not a reason
to rebuild Android lifecycle locally. Add a cross-platform sample scenario. Required unavailable
physical-device checks must be recorded as **NOT EXECUTED — environment unavailable** when that
environment is confirmed unavailable; their absence cannot be converted into a PASS by a build.

## #73 — Define inherited layout-transition policy and zero-area animation behavior

Issue: [#73](https://github.com/ProGraMajster/ModernFormsNext/issues/73). Initial status: **PARTIAL**
(substantial behavior and tests already exist; broader policy decision/contract remains).

[LayoutTransition](../../ModernFormsNext/Animations/LayoutTransition.cs),
[animated-layout architecture](../architecture/animated-layout.md), and
[AnimatedLayoutTests](../../ModernFormsNext.Tests/AnimatedLayoutTests.cs) already define per-control
ownership, owner/key replacement, source retargeting, detach/disposal/hide/window-close cancellation,
ancestor clipping and child transition suppression when a parent resizes. Zero-area endpoints
deterministically snap. This issue explicitly permits either bounded inherited/enter-exit support
**or a stable extensibility contract and permanent fallback**. It does not mandate a second layout
engine or require owner approval merely to document a compatible policy.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| Per-control versus inherited/container ownership/replacement | PARTIAL | Existing per-control rules are documented; decide and document inheritance boundary or implement additive opt-in policy. |
| Supported zero-area enter/exit and deterministic fallback | PARTIAL | Existing snap behavior is real; define whether it is permanent and give explicit supported extension semantics, or add safe enter/exit behavior. |
| Nested transitions, reparenting, clipping, reduced motion, cancellation, disposal | PARTIAL | Most lifecycle/nested contracts and regression tests exist; reconcile them with the final inheritance/zero-area policy and add missing direct cases. |
| No recursive layout or layout from paint | PASS for existing implementation | Presentation-only frame changes and normal UpdateBounds integration already implement this constraint; preserve it. |
| Deterministic clock tests and nested layout/render validation | PARTIAL | Existing tests cover Dock, Anchor, Flow, Table, nested suppression, presentation hit testing and accessibility bounds; new policy/zero-area and rendered cases remain. |
| Public docs and animated-layout guidance | PARTIAL | Existing guide is substantial; update final policy, examples and stale XML references to a future known-easing Designer editor (safe Designer editing now exists). |

No completed #25 work should be repeated. Broader live Designer animation preview is separate #75.
Respect existing back-buffer lifetime, UI-thread configuration and recursive cancellation. Extend
the existing Animated layout ControlGallery page. New rendered/nested Windows/Android checks remain
**NOT EXECUTED**; unavailable visual validation is not proof that the contract itself is blocked.

## #74 — Extend visual-state interpolation to additional layout and text metrics

Issue: [#74](https://github.com/ProGraMajster/ModernFormsNext/issues/74). Initial status: **PARTIAL**.

[Control.VisualStates](../../ModernFormsNext/Control.VisualStates.cs) already owns one visual snapshot
and scheduler entry for color, brush, transform, padding and border widths. Its layout-metric change
path groups layout/cache invalidation and suppresses descendant double animation. Style font and
border radius properties exist but are not part of that interpolation snapshot. The
[current metric contract](../architecture/layout-aware-visual-state-metrics.md) explicitly excludes
other metrics, and [LayoutAwareVisualStateMetricTests](../../ModernFormsNext.Tests/LayoutAwareVisualStateMetricTests.cs)
cover the existing safe subset extensively.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| Supported font/text, corner, spacing, sizing and candidate matrix | PARTIAL | Publish an explicit matrix and reflow opt-in/cost policy; distinguish missing model members from existing discrete ones. |
| Compatible interpolation and deterministic fallback | PARTIAL | Extend snapshot/planner for supported metrics; preserve meaningful values for incompatible fonts and unsupported typography. |
| Existing layout versus render invalidation | PARTIAL | Reuse grouped layout and content-cache notifications; radius is visual, font changes affect measurement/reflow. |
| Cancellation, replacement, reduced-motion, zero-duration | PARTIAL | Current tests/code cover the foundation; each added metric needs the same lifecycle/reentrant guarantees. |
| Deterministic intermediate-state, stability, allocation, parity tests | PARTIAL | Extend existing manual-clock tests and #42 parity fixtures; use #58 counters for measured budgets. |
| Unsupported metrics/performance documentation | PARTIAL | Update matrix, unit semantics, resource/theme interaction and DPI/reflow costs. |

Do not automatically animate all layout properties. `ThemeTypography` stores line-height and letter
spacing, but base text measurement does not consume them globally; #78 is the later shared-rendering
work. Any #74 policy must explicitly describe this boundary rather than introduce a temporary text
engine. Preserve authored target values and one presentation style, no new scheduler. Existing
ControlGallery visual-state/metric pages should demonstrate actual supported values. All new
allocation, rendered, DPI, Android and interactive Designer checks are **NOT EXECUTED** here.

## #76 — Extend Brush morphing and color-space interpolation

Issue: [#76](https://github.com/ProGraMajster/ModernFormsNext/issues/76). Initial status: **PARTIAL**.

[BrushAnimationPlan](../../ModernFormsNext/Animations/BrushAnimationPlan.cs) already prepares immutable
endpoint snapshots, normalizes nonempty gradient stops once, promotes solids to gradient geometry,
reuses one working brush, and returns exact endpoint references. ThemeManager and visual states
consume that same planner. [Brush matrix](../architecture/brush-interpolation.md) and
[compatibility tests](../../ModernFormsNext.Tests/BrushInterpolationCompatibilityTests.cs) already
cover hard stops, duplicate offsets, unequal stop counts, every gradient kind, fallback, alpha,
resource rendering, rapid retarget, zero duration and allocation-free prepared intermediate frames.
The missing pieces in #76 remain: selected broader mappings/policies and explicit color space.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| Expanded source/target compatibility matrix | PARTIAL | Existing matrix is implemented; define the expanded supported set without promising every pair morphs. |
| Gradient geometry and empty/populated stop mappings | PARTIAL | Same-kind geometry and nonempty normalization exist; cross-kind/empty mapping policy remains explicit fallback today. |
| GlassBrush and NoBrush/null ownership | PARTIAL | Current discrete fallback preserves endpoint ownership; establish final permanent/opt-in behavior and test it. |
| Custom/derived extension point or permanent fallback | PARTIAL | Existing exact-built-in-type fallback is safe; issue permits documenting it as permanent instead of arbitrary custom invocation. |
| Explicit sRGB versus linear-light policy | PARTIAL | Current color interpolation is sRGB only; add opt-in policy through the canonical planner/interpolators with deterministic color/alpha tests. |
| Cancellation, replacement, resource invalidation, reduced motion | PARTIAL | Existing foundation covers these; verify each new mapping and policy in visual-state, resource and explicit animation paths. |

Preserve the distinction between value-style local plans and in-place `Brush.AnimateTo`, whose
destination identity and structure cannot change arbitrarily. Default color behavior must remain
source-compatible. Geometry mapping requires a defensible visual model, not an arbitrary conversion.
No reflection or custom constructors in Designer. Extend
[BrushInterpolationPanel](../../samples/ControlGallery/Panels/BrushInterpolationPanel.cs), docs and
measured allocation tests. #27 is closed; #46 GPU is merely related future work and **must not block
this issue**. Render snapshots, new allocation checks and Windows/Android visual evidence remain
**NOT EXECUTED** in this audit.

## #78 — Add shared shadow rendering and global typography metrics

Issue: [#78](https://github.com/ProGraMajster/ModernFormsNext/issues/78). Initial status: **PARTIAL**.

Existing [ThemeTypography](../../ModernFormsNext/Theming/ThemeValues.cs) already validates and stores
positive finite line-height multipliers and finite letter spacing in logical pixels; font size is
in points. Theme JSON round-trips these values with tests. `ToFont()` drops those extra metrics.
[TextMeasurer](../../ModernFormsNext/TextMeasurer.cs) and
[SkiaTextExtensions](../../ModernFormsNext/Extensions/SkiaTextExtensions.cs) are central shared
measurement/rendering integration points and currently do not apply them globally.
There is no shared shadow token/model. Individual shadows do exist, for example
[GroupBoxRenderer.RenderShadow](../../ModernFormsNext/Renderers/GroupBoxRenderer.cs) uses scoped
Skia paint/mask-filter/path resources with DPI conversion and custom clipping. Reuse/extract
appropriate semantics; do not claim no shadow rendering exists anywhere.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| Shadow, line-height, letter-spacing documented units | PARTIAL | Typography token units exist; design additive shared shadow values and consumption semantics. |
| Consistent measurement/rendering without unbounded allocations | PARTIAL | Thread typography through central text layout and drawing, including hit testing/caret caches; introduce bounded cache ownership for shadow resources. |
| Clipping, DPI, opacity, disabled state, theme and cached lifetime | PARTIAL | Existing renderer patterns help; shared contract/cache key invalidation and disposal require implementation/tests. |
| ThemeManager, JSON, dynamic resources, Designer-safe metadata | PARTIAL | Typography JSON already exists; integrate shared consumption/shadow schema and safe metadata without executable serialization. |
| Representative controls, rendered validation, allocations | PARTIAL | Add measured representative coverage including text inputs and containers, not only a decorative sample. |
| Honest unsupported controls/backends documentation | PARTIAL | Existing guide identifies gaps; publish staged supported-control matrix and platform capability limits. |

The risk is measurement/rendering mismatch across editors, document controls, baseline placement,
selection, line wrapping and DPI, plus native filter/cache lifetime. Avoid a cache keyed only by a
mutable brush or shadow object without size/DPI/version tracking. Windows and Android share text
and Skia code; device parity requires observation. Use #58 instrumentation and existing
[ThemeJsonSerializerTests](../../ModernFormsNext.Tests/Theming/ThemeJsonSerializerTests.cs),
theme refresh tests and text/document tests. New runtime, rendered, allocation, Designer and
Android observations are **NOT EXECUTED** here. #7 is closed and GPU #46 is not required.

## #88 — Implement ToolTip animation, fading, and inactive-window behavior

Issue: [#88](https://github.com/ProGraMajster/ModernFormsNext/issues/88). Initial status: **PARTIAL**.

[ToolTip](../../ModernFormsNext/ToolTip.cs) is a real Skia popup component with delay/auto-pop timers,
hover attachment, placement, owner-draw, styling and disposal. `UseAnimation`, `UseFading`, and
`ShowAlways` only store booleans; no show/hide animation consumes them.
[PopupWindow](../../ModernFormsNext/PopupWindow.cs) subscribes to owning-form deactivation using an
anonymous handler that hides the popup. This is a concrete activation/handler-lifetime integration
point to audit when #88 is implemented. ToolTip clears the active interactive-popup marker after
showing, preserving passive tooltip behavior, but this alone is not evidence of complete focus
or accessibility behavior. Existing delay timers are interaction deadlines; the prohibition is
against adding per-tooltip animation timers, not against preserving those existing delay semantics.

| Acceptance criterion | Initial result | Work still required |
| --- | --- | --- |
| UseAnimation/UseFading timing, replacement, cancellation, reduced motion | PARTIAL | Add a bounded shared-scheduler popup presentation policy or explicitly decide permanent no-op behavior as the issue permits. |
| ShowAlways inactive owner, app deactivation, multiple windows, unsupported platforms | PARTIAL | Define separate owner/application activation semantics through #63 and existing popup contracts; preserve defaults for interactive popups. |
| Delay/placement/lifetime and no activation stealing | PARTIAL | Existing delay/placement code works as foundation; test with transition cancellation, replacement, close, ownership changes and passive native popup behavior. |
| Accessibility announcements and supported contrast/motion | PARTIAL | ToolTipPopupControl lacks dedicated announcement integration; extend canonical #59 semantics and existing motion policy. |
| Deterministic scheduler, ownership, lifecycle, rendered validation | PARTIAL | [ToolTipTests](../../ModernFormsNext.Tests/ToolTipTests.cs) currently cover API/delays/caption/style/validation, not scheduler/native activation. Extend appropriate headless and backend fixtures. |
| Document any permanent compatibility no-op | PARTIAL | Current [tooltip guide](../tooltips.md) states limitations; record final permanent scope and supported native behavior after design/implementation. |

Technical route: keep content/popup ownership explicit and animate through `AnimationScheduler`,
retaining exact placement, cancellation and disposal; do not animate native window activation.
Extend [ToolTipPanel](../../samples/ControlGallery/Panels/ToolTipPanel.cs). #63 and #59 supply contracts,
but their open umbrella status alone is not an external blocker. Windows inactive-window, multiple
windows, screen-reader announcements, clipping/DPI and Android popup capability observation remain
**NOT EXECUTED**. A physical device or screen reader PASS cannot be inferred from deterministic tests.

## Initial audit disposition

| Queue order | Issue | Status at baseline | Commit/PR for this session | Remaining work type |
| --- | --- | --- | --- | --- |
| 20 | #14 | PARTIAL | None | Localization runtime, integration, tests, docs, observed validation |
| 21 | #89 | PARTIAL | None | Shared inherited direction, Flow/Table/scrolling/Designer behavior, validation |
| 22 | #45 | PARTIAL | None | Live system palette/appearance observation and shared theme integration |
| 23 | #77 | PARTIAL | None | Android appearance provider and lifecycle integration; recorded device evidence |
| 24 | #73 | PARTIAL | None | Final compatible inheritance/zero-area policy and missing targeted validation |
| 25 | #74 | PARTIAL | None | Expanded supported metric matrix, interpolation and measured reflow policy |
| 26 | #76 | PARTIAL | None | Expanded mapping policy and explicit color-space interpolation |
| 27 | #78 | PARTIAL | None | Shared shadow and typography consumption, caching and rendered measurement parity |
| 28 | #88 | PARTIAL | None | Popup transition/activation/accessibility policy, implementation and validation |

No implementation or current validation PASS is claimed by this file. The next action for these
issues is their ordered fresh audit after preceding queue work, followed by the issue-specific
technical plan, implementation, tests, documentation and final acceptance review.
