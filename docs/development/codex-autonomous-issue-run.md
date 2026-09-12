# Autonomous issue development run

Started 2026-09-10. This report records current observations separately from historical issue/PR
claims. It is updated at each implementation/validation boundary. No release, tag, version bump,
NuGet/VSIX/Marketplace publication is authorized by this run.

## Starting repository and validation

- Fetched `origin`; `master == origin/master == ba396f95adab82564a0681bc922096599ba8c1ca`.
- Initial audit branch: `codex/autonomous-runtime-queue`; renamed for the first implementation to
  `codex/issue-64-testhost-input-clock-rendering` to retain the repository's per-issue PR workflow.
- Existing untracked `.codex/config.toml` is user-owned and remains untouched/uncommitted.
- SDK: repository `global.json`; solution: `ModernFormsNext.slnx`.
- `dotnet restore .\ModernFormsNext.slnx`: PASS.
- `dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true`: PASS, zero errors.
- `dotnet test .\ModernFormsNext.slnx --configuration Debug --no-restore --no-build -m:1 /p:UseSharedCompilation=false`: **2367/2367 PASS**, zero failures/skips across nine projects.
- Existing `NU1902` for private build dependency `Microsoft.Build.Tasks.Git 10.0.301` remains
  visible (four repeated warnings in solution build). No dependency change or suppression.
- Raw GitHub audit responses/build logs/TRX are local ignored artifacts, not package contents.
- Native HWND/UIA/automation checks included in the automated suites are automated native
  integration evidence. They are not manual visual, TalkBack, emulator, or physical-device evidence.

## Initial queue audit

Each linked audit uses current issue bodies, all comments, current code, tests, documentation,
known limitations/roadmap, and related history. A source PASS is not a new runtime validation.

| Order | Issues | Initial audit |
|---|---|---|
| 1–8 | #64, #63, #62, #109, #59, #58, #61, #97 | [Runtime/testing/automation](codex-audit-series1.md) |
| 9–19 | #40, #68, #35, #36, #37, #81, #108, #75, #84, #38, #39 | [Designer/resources](codex-audit-series2.md) |
| 20–28 | #14, #89, #45, #77, #73, #74, #76, #78, #88 | [Framework features A](codex-audit-series3a.md) |
| 29–36 | #85, #70, #55, #13, #12, #57, #86, #87 | [Framework features B](codex-audit-series3b.md) |

The user-specified order remains authoritative. An open related issue is not automatically a hard
dependency. Existing accessibility, commands, animation, resource, Designer and automation
contracts are reused. At the initial audit, #64 lifecycle/navigation/virtualization integration
depended on #63/#12/#55. The #63 implementation below now supplies lifecycle integration;
navigation/virtualization remain dependent on #12/#55. No parallel testing runtime fills those gaps.

Excluded implementation: #46, #72, #79, #80, #60, #20, #15, #16, #21, #43, #44, #69, #82, #99.
Reading those issues to verify dependencies does not start their implementation.

## #64 — implementation plan after audit

Current status: PARTIAL (Phase 1 already merged in PR #93). No issue closed by this report.

1. Add per-window and primary-window input helpers using `IWindowImpl.Input`; preserve
   `WindowBase` preview, command resolution, control hit testing, capture and focus ownership.
   Public keys use existing framework `Keys`, pointer coordinates use logical client pixels.
   Tab sends the production key/translated-text sequence, respecting handled KeyDown.
2. Replace double-click wall-clock coupling with a per-window monotonic time source; a host
   supplies controlled time. Keep existing click/event semantics and document native limits.
3. Scope the real default `AnimationScheduler` to a controlled clock/tick source, restore the
   prior scheduler and dispatcher after disposal/failure, and test intermediate/final states,
   pause/replacement/cancellation/reentrancy. No timer per animation or copy of the scheduler.
4. Add optional framebuffer-backed raster capture through `WindowBase.DoPaint` and immutable
   retained images. Bound pixel allocations; test render scale, clipping, repeated capture,
   mutation and disposal. Do not re-parent controls into a second surface tree.
5. Extend detached diagnostics with focus/input state without logging committed text. Add
   controlled existing-contract platform adapters and consumer binding/resource/theme examples.
6. Add deterministic tests in the existing Testing test project. Run focused tests, then
   full Debug/Release builds and appropriate complete regressions, docs/package/API validation.

Compatibility: additive public Testing APIs and internal production seams. Windows/Android
production routing and scheduler remain canonical. No backend types enter the shared control API.
Designer/sample template startup is not changed. UI operations remain on the host owner thread;
only one host per process remains supported. Borrowed application process state is restored,
host-owned forms/surfaces/scheduler resources are deterministically disposed.

Manual visual/platform-specific validation: NOT EXECUTED — environment unavailable. Headless
snapshots/tests establish only the shared production paths actually executed.

Audit commit: `36184e7`. Implementation commit: `a75b3f98ebf476bbbc0e23cfad4d34cc042d3138`.
PR: [#113](https://github.com/ProGraMajster/ModernFormsNext/pull/113), merged into master on
2026-09-10. The subsequent #63 implementation and its acceptance record appear below.

### #64 — implementation and regression record

The working implementation extends `ModernFormsNext.Testing` with `TestInput`, `TestClock`,
`RenderedSnapshot`, scoped clipboard/lifecycle/theme/motion services, and focus/input diagnostics.
Input enters the existing backend raw-input callback. Raster capture exposes an ephemeral
framebuffer to the existing window paint callback. Animation time drives the real scheduler and
dispatcher timers. Public framework APIs remain compatible; the supporting framework seams are
internal. XML documentation and [the consumer guide](../testing/testhost.md) describe ownership,
UI-thread access, logical/pixel coordinates, timer coalescing, bounded work and native limitations.

Tests exposed defects that were not visible in the Phase 1 host:

- Capture cancellation did not reach nested captured controls. Cleanup now traverses the real
  captured subtree.
- Input/focus callbacks could close or detach their target and then resume work against its dead
  window. Guards must preserve existing standalone control behavior and callback event order.
- Captured execution contexts retained a disposed factory scope and therefore its host. Revocable
  holders clear service/factory references; expired contexts cannot create another headless window.
- The singleton theme manager cached a scheduler from another host lifetime. Default scheduler
  lookup now follows the current scoped scheduler; explicitly injected managers retain their input.
- Failed convenience gestures could leave capture or command-consumed key state active. Cleanup
  uses the existing capture-loss/key-release paths and preserves original/secondary exceptions.
- Closing callbacks could recursively close/dispose the host. Reentrant close is guarded and host
  teardown waits until the current close cleanup has completed.
- Modal `Shown` could close the dialog before `ShowDialog` returned its task; backend closure and
  failing callbacks also required idempotent release of the canonical modal owner relationship.
- Component finalization after a failed Form constructor entered UI binding cleanup with no
  adapter. Finalization now avoids managed UI callbacks; explicit disposal retains normal cleanup.
- A closed popup remained subscribed to its owner's deactivation event. Actual popup closure and
  disposal now detach that subscription, while ordinary Hide retains it for reuse.

Validation history is retained to distinguish detected failures from accepted results:

| Check | Observed result | Consequence |
|---|---|---|
| First expanded Testing run | 184/187 PASS, 3 failures | Fixed nested capture, callback closure, retained factory context |
| Next Testing run | 191/191 PASS | Accepted that intermediate source state only |
| First complete Debug build | PASS, zero errors, same 4 existing NU1902 warnings | No new build warning suppression |
| First complete Debug regression | 43 core failures; other projects passed | Found standalone-control compatibility regression in a new liveness guard; corrected before acceptance |
| Expanded Testing after composition/modal/popup fixes | **256/256 PASS**, zero failures/skips | Includes disposal, focus, popup reuse, raster, services, real composition and controlled timers |
| Complete Debug regression after compatibility fixes | **2525/2525 PASS**, zero failures/skips | Includes 257 Testing tests, 1154 core tests and all seven other suites |
| Complete Release regression | **2525/2525 PASS**, zero failures/skips | Same full solution scope |
| Full Debug / Release builds | PASS / PASS, zero errors | Each has four repeated existing NU1902 warnings; Android targets and VSIX build validation included |
| ApiCompat Debug / Release | 11/11 PASS in each configuration | Compared existing package/TFM public APIs against exact starting master |
| Local pack and package validation | 11 nupkg + 10 snupkg PASS | Version unchanged; nothing published |
| Isolated package-only consumer | PASS | Fresh package cache, public input/Unicode/Tab/click/resource/clipboard/animation/tree/raster APIs |
| Consumer PNG review | PASS (off-screen raster observation) | Scaled controls, Unicode/emoji and updated status rendered inside expected bounds; not native manual evidence |
| ControlGallery startup/closure | PASS (automated native process smoke) | Real responsive window, clean logs, accepted normal close request and exited |
| Documentation | PASS | 32 script assertions; DocFX metadata/build zero warnings/errors; 4/4 local archives validated |

The current source passes implementation regression. Testing covers
sequence/parallel/repeat/timeline composition when called both directly and from dispatcher
callbacks; arbitrary application task continuations remain asynchronous. Modal and popup tests
execute existing production behavior. Current master assemblies were built successfully in an
isolated local worktree for ApiCompat comparison (Release, zero errors, existing NU1902 repeated
five times including restore). The isolated package consumer and both full regressions passed.

Activation/lifecycle acceptance now passes through the actual #63 runtime contracts, full
Debug/Release regression and isolated package consumer below. Navigation/virtualization acceptance still depends on #12/#55;
those criteria have not been removed from #64. Native Windows/Android/IME/accessibility evidence
remains separate.

### #64 — acceptance matrix

The issue body and both comments were refreshed after implementation; no additional scope or
comment appeared. Issue #64 remains OPEN and its target/roadmap metadata is unchanged.

| Acceptance direction | Result | Evidence / remaining boundary |
|---|---|---|
| Create and lay out an application tree without manually opening a desktop window | PASS | Existing host/layout tests and isolated package consumer |
| Simulated pointer and keyboard use normal hit testing/routing | PASS | TestInput tests, commands, hierarchy, clipping/occlusion, capture and callback failures |
| Deterministic focus and Tab | PASS | Independent windows, modal owner disable/restore, real popup input, unavailable/reentrant targets |
| Advance animations without real-time sleeps | PASS | Production scheduler/timers, midpoint/final values, composition, themes, lifecycle policy and terminal cleanup |
| Existing Data Binding/resources/ThemeManager work unchanged | PASS | Production binding/resource consumer tests, controlled services, warmed singleton and consecutive-host regressions |
| Layout/state snapshots for regression | PASS | Detached structural and real off-screen image snapshots, scale/clipping/budget/disposal tests |
| Native behavior remains separate integration evidence | PASS | Existing native bridge/UIA tests remain separate; no native service is falsely emulated |
| Supported package/API for application developers | PASS | Additive documented public Testing APIs, isolated local NuGet consumer, package validation and ApiCompat |

| Phase / detailed scope | Result | Remaining work |
|---|---|---|
| Phase 1 host/layout/dispatcher | PASS | Preserved and extended; no replacement runtime |
| Phase 2 pointer/key/text/focus/idle, modal and popup focus | PASS | Actual framework paths with supported existing key mapper |
| Phase 3 clock/rendering/structured snapshots/diagnostics | PASS | Controlled production scheduling, detached images and bounded diagnostics |
| Phase 4 existing binding/resource/theme/command/platform-service integration and testing template | PASS | [Consumer guide](../testing/testhost.md), [xUnit template](../testing/testhost-template.md), tests and local package consumer |
| Phase 4 activation/lifecycle integration with #63 | PASS | Same rich publisher, public Application.Lifecycle activation/save/restore, real Application.Run lifetime tests, scheduler integration and scoped runtime restoration; final #63 Debug/Release regression and 154 public package-consumer assertions pass. Native platform evidence remains separate below. |
| Phase 4 navigation and shared virtualization integration | BLOCKED (dependencies #12/#55) | Those canonical runtime subsystems do not yet exist; revisit after implementing them |
| Future touch, drag-and-drop and focus-scope helpers | NOT APPLICABLE to current phases | Explicit future directions; no excluded feature implemented |

Overall issue status: **PARTIAL**, with navigation/virtualization ecosystem coverage still
**BLOCKED** on #12/#55. The #63 lifecycle integration now has passing shared/runtime/package
validation. This is not permission to close #64. Documentation validation
also corrected 44 source links in the initial audits to verified baseline GitHub permalinks; no
audit findings were changed.

Manual Windows visual review, Android emulator/physical-device checks, TalkBack and Visual Studio
interaction for this phase: **NOT EXECUTED — environment unavailable**. The ControlGallery check
was an automated native startup/close smoke; the inspected PNG was headless raster output.
Template/reference-app validation: NOT APPLICABLE; generated application startup was unchanged.

### #63 — refreshed audit and implementation plan

At this pre-implementation audit, baseline master was
`ba396f95adab82564a0681bc922096599ba8c1ca` after fetch. Full current issue and all comments (zero)
were read again. `codex/issue-63-lifecycle-activation` initially stacked on #64 PR #113 at
`311fa82`, before that PR merged. The merged baseline is recorded below; this paragraph preserves
the audit's original provenance. The earlier all-queue audit supplies related issue/history context.

Existing canonical pieces: Application.Run/Exit, window backend activation callbacks,
IPlatformApplicationLifecycle's four coarse states, the scheduler's lifecycle policy, Android
Activity tracker and a borrowed SkiaControlSurface across sample Activity recreation. Missing:
rich normalized events, activation payloads, state handoff hooks, active-window diagnostics,
Windows app lifecycle mapping, Android multi-Activity aggregation and real inset propagation.
Run currently subscribes after Form.Shown and lacks guaranteed cleanup; Exit callbacks can
prevent loop cancellation. These directly block lifecycle correctness.

Implementation plan, before code changes:

1. Preserve the existing interface and enum. Extend the same provider through an optional rich
   interface and one deterministic publisher. Commit normalized state before old scheduler
   notifications and then richer public callbacks. Bound reentrant notification queues, activation
   payloads and restoration data. Never infer file/URI intent from arbitrary launch arguments.
2. Expose the provider through Application.Lifecycle with UI-thread callbacks, immutable snapshots,
   privacy-preserving bounded diagnostics and explicit save/restore hooks. Reuse existing test
   services and isolate process Application state in TestHost; no second runtime or scheduler.
3. Preserve main-root closure as the default Run policy. Add explicit last-form/explicit-exit
   policies, wire real window activation, reconcile native closure, and guarantee once-only Exit
   and cleanup even when user callbacks fail or close windows reentrantly.
4. Map Windows app activation independently of window activation/background. Add launch arguments,
   session/power notifications and graceful shutdown through the same provider. File/URI forwarding
   accepts explicit safe data; single-instance transport is deferred as allowed by the issue.
5. Aggregate Android Activities without equating recreation to process termination. Map initial
   and subsequent intents, bounded Bundle state handoff, normalized background/foreground and
   host generations. Keep renderer/IME teardown in the existing native host and shared surface.
6. Add shared logical safe-area and IME-inset data, optional backend feature and real existing
   content-root layout integration. Do not overwrite application Padding or create another tree.
   Android converts native occlusion to the shared model; fitted desktop client areas default zero.
7. Test duplicates, ordering, reentry, failures, multiple windows, activation copying/privacy,
   restoration/recreation, animation/pending-work/IME lifetime and inset layout/input coordinates.
   Build/test the full solution serially; inspect available emulator tooling before claiming it
   unavailable. Native hosted views/WebView/Media implementations remain outside this issue.
8. Document API examples, platform and process-death limits, lifetime policies and state ownership;
   then revisit every #63 criterion and the dependent #64 lifecycle acceptance row.

Compatibility and ownership: additive APIs; no large dependencies, version/package metadata or
Designer serialization changes. UI mutation remains on the owning dispatcher. Native objects stay
in platform projects; activation/restoration snapshots own copied primitive data only. Scope
revocation and disposal detach subscriptions and preserve borrowed application/control state.

### #64 — PR finalization and merged baseline

The user's PR finalization policy authorizes merging useful completed scope even while an issue
remains OPEN/PARTIAL. PR #113 was reviewed at exact head `311fa8241d2531ecffb10c2de4b3ffcb77dcded8`;
required build CI passed and final code/acceptance review found no blocker. The PR was marked Ready
and merged using the repository's normal merge-commit strategy on 2026-09-10.
Merge: `ad0ee5679f8e122f3fb1107ffdaf931e58621abb`. Local `master` and `origin/master` were fast-forwarded
to that commit. The #63 branch incorporated the new master at `2e6d52250421f0f168a1b1ad3a828a1214657a8f`.
Its tree equals the reviewed #64 PR tree, so no in-progress #63 source or original local config was
replaced. The #63 PR will target master directly. Post-merge master
[CI run 34504661851](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34504661851)
completed with **SUCCESS** at merge commit `ad0ee5679f8e122f3fb1107ffdaf931e58621abb`.
PR merge and this run's status were reverified through GitHub during the final #63 acceptance audit.
Issue #64 remains OPEN; subsequent completed issues will follow the same PR/CI/review/merge policy.

### #63 — validation history and final results

- Restore passed. Initial full Debug build passed with zero errors, four existing repeated NU1902
  warnings and two new Android nullable warnings; both new warnings were corrected before acceptance.
- First focused Testing run: 269/284 PASS. All 15 failures shared an invalid Exited snapshot retaining
  host count. Exited now reports NoHost/zero hosts; cleanup remains guaranteed.
- First full Debug regression: Automation 140, Windows bridge 61, sample 16, Designer 640,
  Testing 287, core 1177, VSIX 26 and Android 181 tests passed. The Windows native suite aborted:
  an individual window activation eagerly rebound a global lifecycle facade from another UI thread.
  Window callbacks now update cached counts without binding; actual Run/public lifecycle access binds
  through its owning dispatcher. This run is NOT accepted as a complete pass; native rerun is required.
- A test-owned UiAutomationHost child left after the abort was identified by its exact workspace
  executable path and stopped. No unrelated process was stopped.
- Pixel_8 is now running as emulator-5554, boot-complete verified through adb. Emulator validation
  is in progress; prior unavailable status for #64 does not substitute for this new evidence.

Subsequent acceptance corrections: Exit now posts through the registered shared dispatcher, so
Android does not depend on a WindowKit loop. Runtime identities revoke stale queued exit requests.
Surface/Activity/native window cleanup attempts all mandatory steps after observer failures;
the retired synthetic parent refuses re-adopting a borrowed tree from ParentChanged. Native Android
detach completes before cancellation can reenter Java-peer disposal. The borrowed-tree reuse
regression then exposed an existing PropertyStore empty-integer-array bug; removing its final block
now restores the null empty-store invariant, matching object entries. The unchanged reuse regression
and an additional empty/repopulate test pass.

Final production-source commits: `6d8d6b4` (shared contracts, runtime/TestHost and regressions) and
`83a2234d6a5a6b83ba228b03bba38971014127fe` (native backends/sample/integration tests). Full Debug
build passed with zero errors and four existing NU1902 warnings. Debug regression passed
**2623/2623, zero failed/skipped**: core 1182, Testing 298, Android 188, Windows 72,
Designer 640, automation 140, Windows bridge 61, sample 16, VSIX 26.
Both ControlGallery and the template-reference DemoApp exposed responsive native windows and
completed a normal window-close request with exit code zero. This is automated native startup/exit
evidence, not manual visual review. No template source or generated application structure changed.

Full Release build also passed with zero errors and four existing NU1902 warnings; Release
regression passed **2623/2623, zero failed/skipped**. ApiCompat passed **11/11 in each configuration**
against merged master `ad0ee5679f8e122f3fb1107ffdaf931e58621abb`; a dependency-resolution false
positive was corrected in the validation resolver, without suppressing compatibility findings.
Package validation passed **11 nupkg and 10 snupkg**, with version **1.10.0 unchanged**. Nothing
was published. Documentation scripts passed **32 assertions**; final DocFX passed with zero
warnings/errors and 1004 HTML pages. All four documentation archives passed validation. Their
metadata identifies source commit `83a2234d6a5a6b83ba228b03bba38971014127fe`; run logs are retained
as `artifacts/autonomous-audit/phase63-docfx.log` and `phase63-doc-validation.log`.

The independent NuGet-only consumer passed **154 public-API assertions**, after a fresh-cache
restore and Release build with zero warnings/errors. It covers immutable argument/URI payloads,
explicit state handoff, ordering/privacy, scheduler policy, app versus window activity, window
insets and surface safe-area layout, all three real Run modes, canceled closure, failing Exit
cleanup, scope revocation and legacy-provider compatibility. It has no ProjectReference or
reflection/internal access. The first consumer compile exposed only consumer API mistakes
(Form.Padding and internal Control.IsDisposed); the artifact was corrected to assert public
ClientSize/Bounds and Disposed events, then the entire script passed with another empty cache.
Evidence and package hashes are retained under
`artifacts/autonomous-audit/consumer63/runs/20260910T172622771Z-f27722ca/`.

Android evidence has two distinct APK provenances, detailed in the
[lifecycle guide](../application-lifecycle.md). The broader initial Pixel_8/API 34 series passed
**26/26 recorded-evidence assertions**, including Home/resume and cold-process Bundle restoration.
The separate final-source smoke passed **21/21** against source `83a2234d6a5a6b83ba228b03bba38971014127fe`
and APK SHA256 `6EEBA58B9698138016C2A4B761282BEF68C6C61C7B881C85EC0F2F2B6AF2C794`.
It observed Protocol delivery, genuine font-scale Activity recreation with active IME, retained
edited text and another Gboard commit, and recreation beginning with five active animations.
The same process reached generation 5 with two shared clicks and a completed UI dispatcher
callback; the refreshed 411 × 840 logical surface was attached with zero active pointers,
zero active animations, no pending frame callback or scheduler demand, and one active surface.
No captured crash, ANR or lifecycle failure occurred; all five temporary settings were restored
exactly. Final-source evidence is in `artifacts/autonomous-audit/phase63-android/final-83a2234/`.
The final smoke did not repeat cold-process/Home cases. Numeric nonzero shared insets/cutouts,
automatic keyboard avoidance and a specifically pending callback spanning native teardown were
not observed; physical/manual and future native-product coverage remain separate limitations.

### #63 — final acceptance matrix

The [current issue #63](https://github.com/ProGraMajster/ModernFormsNext/issues/63) and **all
comments (zero)** were refreshed through `gh issue view` on 2026-09-10. The issue remains OPEN;
the body is unchanged from the implementation audit. Every explicit acceptance-direction item
and every additional 1.10.0-audit checkbox appears separately below. Detailed requirements and
test scenarios follow so the broader issue scope is also visible.

**Status convention:** **PASS** records executed checks for the stated scope, not universal native
parity. **PARTIAL** identifies remaining evidence or future product integration. **BLOCKED** identifies
a missing canonical dependency. **NOT APPLICABLE** identifies explicitly excluded future scope.
Native observations retain their APK provenance; managed tests do not emulate the OS. No issue
checkbox or remote issue state is changed by this report.

| Explicit acceptance direction | Implementation and evidence location | Result / remaining boundary |
|---|---|---|
| Application code observes normalized lifecycle without platform-specific APIs | `Application.Lifecycle`, immutable snapshots, rich events, explicit DeliverActivation/SaveState/RestoreState; facade and public-consumer tests | PASS — Debug/Release and isolated consumer validate the shared UI-thread API and optional capability over the existing coarse provider. |
| Windows and Android map lifecycle into the same shared contracts | Both register the canonical IPlatformApplicationLifecycle provider with rich notifications/controller; Windows native adapter and Android weak Activity reducer use the same publisher | PASS — managed regressions, Windows native 72/72 and final-source Android 21/21 pass within the recorded scenarios. Android remains an experimental host, not a full desktop window backend. |
| Activation identifies normal, argument, file and URI launches where supported | Run launch/arguments; copied bounded explicit payloads; Android initial/new Intent and content/file/URI/protocol mapping; native Windows launch and mapper/codec tests | PASS — typed payload/mapper/consumer checks and actual Protocol delivery pass. Windows file/URI forwarding is explicit readiness, not shell association or automatic argument inference; native file-provider variants are not comprehensively observed. |
| Lifecycle ordering is deterministic and testable | Snapshot commit before coarse then rich events; FIFO reentrant delivery, duplicate/stale suppression, terminal permanence, save ordering, bounded queues and failure aggregation | PASS — publisher/facade/lifetime regressions and native Windows callback scenarios pass. Platforms need not emit every intermediate native state. |
| WebView/Media/native-hosted controls can react through common hooks | Shared lifecycle and state contracts are available without feature-specific backend calls; the existing renderer/surface and scheduler consume established contracts | PARTIAL — common hooks and the existing Skia host pass; concrete future WebView/Media/native-control integrations remain deferred to their own implementations. No substitute controls or duplicate runtime were added. |
| Future restoration and single-instance activation can build on the same model | Explicit versioned state handoff and DeliverActivation accept application-owned data; Android Bundle adapter; SecondaryInstance/Notification descriptors | PASS — foundation contracts, copying, state handoff and codec checks pass. Durable storage, migration, single-instance IPC and notification transport remain application/future-feature responsibilities. |

| Additional 1.10.0 audit acceptance checkbox | Implementation and evidence location | Result / remaining boundary |
|---|---|---|
| Shared restoration hooks and deterministic recreation/background/suspend/activation ordering | StateSaving/StateRestoring, documented reentrancy; native save before suspension; cold-process Bundle restoration before subsequent activation; same-process recreation preserves the live model | PASS — final managed ordering/codec checks and final-source same-process recreation pass. Cold-process/Home observations belong to the separate initial APK and were not repeated on final source. Android cannot guarantee a final save or OnDestroy after process termination. |
| Safe-area/system insets reach framework windows/content without Android types | WindowInsets and optional feature contract; informational WindowBase.Insets; SkiaControlSurface safe-area layout preserves Padding and shared input/render coordinates; Android density/overlap mapping | PARTIAL — geometry/pointer/raster/mapper and public consumer checks pass; fitted native area and attached surface were observed. Numeric nonzero shared-inset/cutout cases were not observed. Ime remains separate application policy; API 23–29 has no typed IME inset. |
| Lifecycle stress with active animations, IME, native hosted views and pending work | Existing scheduler, controlled dispatcher, shared surface and actual Android Skia host; cancellation/composition cleanup regressions and emulator scenario | PARTIAL — final-source active-IME/animation recreation and idle cleanup pass, as do deterministic queued-work tests. A specifically pending callback spanning native teardown was not observed; absent future WebView/Media/native-host products cannot supply product-specific stress evidence. |
| Distinguish application lifetime, Activity recreation and individual window lifetime in tests/docs | Independent app/window activity, host count/generation, three application lifetime modes, multi-Activity reducer, scoped TestHost runtime, [consumer guide](../application-lifecycle.md) | PASS — full regressions, all three Run modes in the consumer and native recreation pass. Activity destruction is not application termination. |

| Detailed requirement from the issue | Current scope / evidence | Result / remaining boundary |
|---|---|---|
| Extend existing Application/window/backend infrastructure | Existing Run/Exit, dispatcher, IPlatformApplicationLifecycle registry key and AnimationScheduler remain authoritative | PASS — integration regressions and consumer pass; no second application runtime, semantic tree or animation pause policy. |
| Starting, activation/deactivation, entering/leaving background, suspend/resume and exit | Phase, coarse State and application IsActive are independent; windows expose their own IsActive; publisher and adapter tests cover transitions | PASS — managed/Windows checks and recorded Android scenarios pass. Desktop deactivation does not invent background or pause animations. |
| Safe platform-neutral activation and future notification/secondary-instance kinds | Bounded copied strings/absolute URI; all seven declared kinds; payload validation/copy/privacy tests | PASS — tested descriptors; native transport and permissions are separate. |
| Windows launch, app/window activity, file/URI readiness, session/display and shutdown | Run arguments, WM_ACTIVATEAPP and window callbacks, power suspend/resume, session query/end/cancellation, graceful loop exit | PASS — native 72/72 includes owned-process callback integration. Physical sleep/logoff/display-device changes are not claimed by synthetic owned-message tests; no invented Display phase. |
| Android foreground/background, pause/resume, configuration/process recreation, Intent/deep links and state preservation | Aggregated weak Activities, generations/retired identities, explicit native Intent adapter, bounded Bundle handoff, borrowed control tree | PASS — Android 188/188 and separately recorded initial 26/26/final-source 21/21 emulator assertions. Cold-process/Home cases were not repeated in the final smoke; cold-process restore depends on a supplied saved Bundle. |
| Multiple windows, background policy, popup/floating-window readiness and lifetime modes | MainWindowClosed default, LastWindowClosed and Explicit; OpenForms excludes popups; provider activity does not derive from focused Form; existing modal/popup paths remain | PASS — full regressions, native window checks and consumer modes pass. Future DockWorkspace floating products are not implemented by this change. |
| Animation scheduler and timers/background-sensitive work | Existing coarse StateChanged subscription drives scheduler pause/rebase; application/native consumers can subscribe to the same contract | PASS — scheduler/consumer checks and final-source active-animation recreation pass. Timers are not all automatically suspended and arbitrary application background work is not controlled. |
| Renderer/surface and native-host cleanup/recreation | Existing SkiaControlSurface/AndroidSkiaHostView paths with borrowed-root ownership and independent cleanup after user callback failures | PASS — final failure-cleanup/reuse regressions, package consumer and final-source attached/idle Android surface pass. This validates the existing Skia host, not absent future native products. |
| Persistence and navigation hooks without serializing control trees | Positive schema version plus bounded ordinal string map; explicit save/restore and Android codec | PASS — handoff/codec/consumer checks pass. Persistence is optional; actual navigation integration remains dependent on #12, not silently completed here. |
| Documented UI callback context and marshaling | Facade/publisher enforce owning UI thread; native adapters marshal through their platform dispatcher; Exit uses registered shared dispatcher with WindowKit fallback | PASS — final regressions include shared-dispatcher-only and stale queued-scope cases; native sample callback reports UI access. |
| Duplicate normalization and deterministic notification/error order | Equal/stale snapshots suppressed, repeated activation remains distinct, queue/drain bounded, failures do not suppress remaining mandatory observers | PASS — publisher, Android reducer and owned native Windows duplicate/failure tests pass. |
| Fake/test backend without real mobile OS | TestApplicationLifecycle derives the actual publisher; real Application.Run uses the canonical controlled dispatcher; TestHost restores borrowed application state | PASS — Testing 298/298 and isolated public consumer pass. Tests do not emulate OS foreground policy or native Activity callbacks. |
| Developer Tools diagnostics readiness and payload privacy | Detached current snapshot, last activation kind, maximum 64 transitions, active/open Form counts; no activation/state contents in default diagnostics | PASS — data/privacy regressions and consumer checks pass; Developer Tools display itself remains future UI. |
| Documentation and compatibility | XML docs, [lifecycle guide](../application-lifecycle.md), [TestHost guide](../testing/testhost.md), canonical docs-site TOC, limits/roadmap updates | PASS — ApiCompat 11/11 in each configuration, package validation, consumer, 32 documentation assertions, DocFX and all four documentation archives pass. No version or release metadata change. |

| Required test scenario | Identified automated coverage | Final result |
|---|---|---|
| Normal start → active → exit | ApplicationLifetimeTests, facade tests, Windows LifecycleScenario using real Application.Run | PASS — full regressions, public Run consumer and native Windows scenario |
| Deactivate/reactivate | Publisher/Windows adapter tests, Form activation tests, two-window native scenario | PASS — full regressions and owned native Windows scenario |
| Background/foreground | TestApplicationLifecycleTests scheduler integration and Android reducer/native scenario | PASS — final managed tests; native Home/resume observed on initial APK only |
| Suspend/resume | Publisher/scheduler tests, Windows owned power messages, Android stop/resume mapping | PASS — final managed/native Windows tests and recorded Android recreation; no physical Windows suspend |
| Activation with arguments | Typed payload/facade tests and native process launch | PASS — final regressions, package consumer and Windows launch |
| URI/file activation payloads | Copied payload validation, explicit facade delivery, Android Intent mapper/codec and native deep-link scenario | PASS — final managed/consumer checks and native Protocol delivery; no comprehensive native file-provider matrix |
| Repeated/duplicate native notifications | Publisher duplicate/stale tests, Android retired/duplicate Activity tests, native duplicate Windows resume | PASS — final managed and native Windows regressions |
| Window close during lifecycle transition | Starting/background Exit, Shown/Activated closure, failing observers, modal cleanup, runtime-identity isolation | PASS — final regressions and public consumer callback/failure cases |
| Native hosted control cleanup/recreation | Existing shared surface disposal/capture/composition tests and Android renderer/IME recreation evidence | PARTIAL — current Skia host passes final-source recreation/IME/animation/idle checks; future native-hosted products remain unimplemented and unvalidated |

Final review identified a pre-bootstrap null-dispatcher cache, eager lifecycle
binding from unrelated native window threads, invalid Exited host counts, reentrant/terminal
shutdown ordering, shared-platform-only Exit dispatch, stale queued runtime requests, and
callback-failure surface cleanup. Corrective changes and their new tests are included in the
final Debug/Release results and source identified above. The final-source Android smoke covers
the latest teardown changes; its narrower scope does not relabel the initial APK's broader cases
as rerun. Windows native
tests send messages only to the spawned process's own verified HWNDs; they are native callback
integration evidence, not a real request to suspend or end the user's Windows session.

| Final #63 validation gate | Status / exact evidence boundary |
|---|---|
| Restore and complete Debug build | PASS — zero errors; four existing NU1902 build warnings |
| Complete Debug regression, including latest cleanup/dispatcher tests | PASS — 2623/2623, zero failed/skipped |
| Complete Release build and regression | PASS — zero build errors, four existing NU1902; 2623/2623 tests, zero failed/skipped |
| Windows native Application.Run/session/power/window integration | PASS — Windows suite 72/72, including owned-process native callback scenarios |
| Android target build, APK provenance and emulator lifecycle/IME/animation/recreation smoke | PASS — Android 188/188; final-source APK hash recorded above, final smoke 21/21; initial broader series 26/26 has separate provenance |
| Numeric native shared-inset/cutout and keyboard-avoidance evidence | PARTIAL — shared geometry/mapper/consumer checks and fitted area observed; nonzero numeric shared insets/cutouts and automatic IME avoidance not observed |
| API compatibility against merged master ad0ee5679f8e122f3fb1107ffdaf931e58621abb | PASS — 11/11 Debug and 11/11 Release; dependency resolver corrected without suppressions |
| Package validation and isolated package-only consumer | PASS — 11 nupkg + 10 snupkg; unchanged 1.10.0; 154 assertions after fresh-cache restore, Release build zero warnings/errors |
| Documentation scripts | PASS — 32 assertions |
| Final DocFX | PASS — zero warnings/errors; 1004 HTML pages |
| Four documentation archives | PASS — 4/4 validated; metadata records final source 83a2234d6a5a6b83ba228b03bba38971014127fe |
| ControlGallery and template-reference DemoApp | PASS — responsive native windows, normal closure and exit code zero; automated startup/exit only |
| Manual Windows visual, physical Android device, broader IME/vendor/native-control matrix | PARTIAL — NOT EXECUTED — environment unavailable; only explicitly recorded observations may change this status |
| Implementing future WebView/Media/native widgets, navigation #12 or virtualization #55 | NOT APPLICABLE to this implementation; no claim that these future products or #64 dependent criteria are complete |

Overall #63 status at this checkpoint: **PARTIAL UMBRELLA**, with passing shared, Windows,
package-consumer, documentation and scoped Android emulator evidence. Native inset/device
coverage and future product integrations retain the boundaries above.
The current implementable scope is eligible for the user's ordinary
PR/CI/review/merge workflow after its actual checks pass, even if future product integrations
keep the parent issue open. This does not close #63 or complete #64's #12/#55-dependent rows.

Final source review of `83a2234d6a5a6b83ba228b03bba38971014127fe` against merged master
`ad0ee5679f8e122f3fb1107ffdaf931e58621abb` found no unresolved code blocker in this scope.
The review reconciled the dispatcher, runtime ownership, native teardown, borrowed-root and
PropertyStore corrections with the passing regressions. Subsequent changes only finalize
documentation; no production or test source differs from the validated source commit.

PR [#115](https://github.com/ProGraMajster/ModernFormsNext/pull/115) was created Ready,
validated by required CI run `34509085249` on final head
`3a8a405b3e534949eedb22a1dd4b72ad5179a4e4`, and merged using the normal merge strategy
as `5eb19a5098f5fdba794d57fe599cf0224f2ab4d8`. The fetched local master matched origin
and the merged tree exactly matched the reviewed PR head. Documentation was rebuilt
and all four archives revalidated at that final PR head. #63 remains OPEN/PARTIAL
for the acceptance boundaries above, not because its completed PR remains Draft.

The post-merge master CI run `34510156039` also completed successfully.

## Issue #62 — Add IME and advanced text input composition infrastructure

Status: **IMPLEMENTED / LOCAL VALIDATION PASS — dedicated PR/CI/merge pending; issue remains PARTIAL**. Starting master:
`5eb19a5098f5fdba794d57fe599cf0224f2ab4d8`; branch `codex/issue-62-text-composition`.
The [audit and technical plan](issue-62-text-input-plan.md) records the reread issue,
zero comments, current dependencies, source/history/test/docs findings and the
additive shared client/session/native adapter design before production edits.
Existing Android/document composition is reused. No #62 implementation validation
has been executed at this audit checkpoint.

### Implementation and validation in progress

The additive shared client/options/snapshot/composition contracts, revocable canonical-focus
sessions, TextBox/RichTextBox/Markdown integration, IMM32 and Android InputConnection adapters,
TestHost helpers, composition adornment, consumer guide and both appropriate samples are
implemented locally. No package version, dependency, template-reference content or native
control substitution was introduced. The Windows popup path borrows the owner's IMM transport
and forwards raw input through the existing popup resolver. Android connections capture a
single session instead of resolving whichever editor has focus when a stale callback arrives.

At this intermediate working-tree checkpoint, restore passed. The first complete Debug build
failed only in the new sample code (missing System import and inaccessible IsDisposed use);
those source errors were corrected. The framework and test assemblies produced by that build
passed 1222/1222 core tests, including the new client and composition-rendering cases, and
198/198 Android managed tests. The focused Windows IMM32/popup/owned-HWND suite passed 25/25
after correcting fixtures to host/select real visible TextBoxes. The earlier focused session
suite passed 12/12 before subsequent lifetime hardening. These are intermediate results, not
final full-solution validation of later edits.

Review found additional reentrant attachment, event subscription, parent change, popup reuse
and callback-failure cleanup cases. Corrections and regressions are in progress. Managed border
offsets are now included in native caret geometry to match ControlAdapter painting. Final
Debug/Release, API/package/documentation gates and real keyboard observations remain pending.
Logs and TRX evidence are under ignored `artifacts/autonomous-audit/phase62-*`; the final
acceptance matrix will identify the actual validated source and native binary provenance.

### Reviewed implementation checkpoint

Production, sample and regression-test source is committed as
`bf0fa945781413b644d8da558d8df0ad9d4818c9`. The final Debug build passed with zero errors
and four existing NU1902 warnings. All nine test projects passed **2759/2759**, with zero
failures or skips: Automation 140, Automation.Windows 61, cross-platform sample 16,
Designer 640, Testing 359, core 1222, VSIX 26, Android 198 and Windows 97.
This includes the last regression preventing a constructed, unshown window from lending
its native text session when application setup selects a child.

The review corrections cover reentrant native attachment/subscription, callback failures,
ancestor removal/reparenting, hidden/disabled/disposed editor finalization, popup reuse and
owner routing, activation/close ordering and managed-border caret coordinates. Native UI
tests sharing framework services now use the existing xUnit collection mechanism to prevent
concurrent access to those services. The earlier two failures caused by repaint after native
closure and the subsequent parallel Windows fixture race were corrected before this final run.

API comparison uses all 11 baseline assemblies built from merged master `5eb19a5`. An initial
unfiltered comparison found only three changed compiler-generated state-machine type names
in `AsyncStateMachineAttribute` on unchanged public Markdown async methods. Their private
ordinal names changed after additive internal methods. The validation configuration excludes
only `System.Runtime.CompilerServices.AsyncStateMachineAttribute`, using the SDK-supported
attribute exclusion input; it continues checking all public members, signatures, parameter
names and other attributes. There is no repository API suppression or public contract removal.
The initial configured Debug comparison passed 11/11; final Debug/Release comparisons remain
part of the pending gates below. Raw metadata evidence and the exact local configuration are
recorded in `phase62-apicompat-state-machine-evidence.json` and
`phase62-apicompat-configuration.md` under the ignored audit artifacts.

Eight off-screen production-rendering images of the actual gallery were inspected at 100%
and 150% scale: plain, multiline, rich and Markdown composition underlines were visible.
The intro text was shortened after clipping at 150%; a final capture will validate that edit.
These captures do not establish native candidate-window behavior. An isolated API 36 emulator
is ready alongside the existing API 34 emulator; final APK installation and actual Gboard
interaction are still pending at this checkpoint. Release, packages, consumer, final DocFX,
native acceptance, PR and CI are not yet reported as passed.

### Native and package validation findings after the first implementation commit

The first APK, built from `bf0fa94`, has SHA-256
`411124AEED7E6C17C5906A009802AA6CB60AFD77F557F844A1EBEF24224A9BE9`.
Actual Gboard interactions on API 34 and API 36 confirmed compose, replacement, deletion
and space commit in the single-line editor, but exposed three incomplete behaviors:
API 34 did not keep the editor above the keyboard; Next hid the keyboard on both APIs;
and a multiline Enter produced no line break. These are observed failures, not passed
acceptance rows. API 34 uses Gboard 12.4 and API 36 Gboard 15.1; full versions and native
measurements are recorded with the ignored emulator artifacts.

The API 34 native window used automatic `adjustPan`: Android sees one Skia view and cannot
discover the shared scroll controls. Its root IME frame started at y=1517 in a 1080x2400
window, overlapping the native view's bottom y=2337 by 820 physical pixels. Merely preferring
the original root insets did not fix the observed case: comparison APK
`B941B1BA465B018BC6E27721323000D227D5D5FBD349B25E89E2C192DFAB4203`
still reproduced it. The sample now explicitly requests native `AdjustResize` and scrolls
the current caret after viewport resizing as well as remaining IME-overlap changes.
This follows Android's [window response guidance](https://developer.android.com/develop/ui/views/touch-and-input/keyboard-input/visibility).
The root-inset correction remains necessary to use the same coordinate system as the mapper.

The synchronous old-client-null/new-client handoff now defers keyboard dismissal with a
generation check and native focus/attachment guards. This protects Next/Previous and avoids
hiding a different native view's keyboard through their shared window token. Native
diagnostics remain opt-in; the new client path records method/numeric/newline metadata,
and the sample can record coalesced native/root inset measurements without changing layout.
Fresh APK observations of these corrections remain pending at this checkpoint.

The newline failure was reproduced in deterministic shared tests: 16 real payload cases
failed before the correction. An initial Markdown fixture incorrectly selected its container
through the nonvirtual Control.Select method; that fixture was corrected before the recorded
16-failure checkpoint. The existing virtual text edit now accepts complete leading LF/CRLF
payloads while preserving standalone CR, single-line and Markdown AcceptsReturn policies.
All 53 text-client cases plus the image-copy resource-boundary case passed in the focused
Debug run (**54/54**); full updated Debug/Release runs are pending.

The first Release build passed (zero errors, four existing NU1902), but its full regression
was **2758/2759**: an unchanged image-collision test failed during temporary-directory cleanup
because `shared.png` was locked. Review found no evidence of an asynchronous framework loader
in that test; all copy streams were awaited/disposed. The original focused test subsequently
passed. Its assertion now explicitly opens the copied file with FileShare.None before decoding
file bytes, preserving collision/pixel checks and testing the processor's release boundary
without relying on native filename-decoder stream ownership. No retries, sleeps, forced GC
or ignored IOException were added, and no unproven framework race is claimed fixed.

Both configured API comparisons passed **11/11 Debug and 11/11 Release** for the first source.
The first local pack validated **11 nupkg and 10 snupkg** at unchanged 1.10.0. A fresh-cache,
package-only consumer restored and built with zero warnings/errors, but its final Tab assertion
incorrectly assumed composition freezes canonical focus. The actual contract bypasses command
bindings while retaining ordinary control navigation and text-preserving session retirement.
The consumer has been corrected to check Tab transfer, stale-session rejection and fresh
reacquisition, with a separate Escape-binding guard. Its corrected run remains pending.

An isolated native Windows harness passed real Polish (Programmers) HKL translation through
TranslateMessage/DispatchMessage to an owned TextBox/HWND: `ąęłĄ` appeared once, AltGr did not
run the Ctrl+Alt binding, and a separate genuine Ctrl+Alt gesture did. A dead-key probe found
Shift+VK_OEM_3; WM_DEADCHAR `~` inserted no text, followed by A producing one WM_CHAR `ą`.
The thread's keyboard-state bytes and unchanged user layout were verified after completion.
The test used synthetic thread-local key states, not a physical keyboard or a CJK IME.
Core/Windows assemblies both identified source `bf0fa94`; hashes and complete message evidence
are in `native-keyboard62/runs/20260911-122142-127/`.

The final gallery capture at that source produced ten images (two baselines plus eight
compositions) at 100%/150%; the shortened intro fits. ControlGallery and the unchanged
template-reference DemoApp also exposed responsive native windows and closed normally with
exit code zero. These are scoped rendering/startup checks, not native candidate-window proof.

### Corrected newline and Android keyboard handoff checkpoint

Source `3949100f70875b779878154e3d6556544686d63d` passed complete Debug and Release builds
with zero errors and four existing NU1902 warnings per configuration. All nine test projects
passed **2785/2785 in each configuration**, with zero failures or skips. The additional 26
core regressions cover full newline payloads, Unicode suffixes, composition cancellation,
single-line filtering and Markdown policy/history. This also includes the image-copy
resource-boundary assertion described above.

The corrected embedded-assembly APK has SHA-256
`942E5AE891473B6AA4295FAA3F334E1687F45500AE7C521B26D9CDFF1798E283`.
Actual Gboard on API 34 and API 36 now keeps the keyboard visible during Next and delivers
multiline Enter as `CommitText(LF)`, inserting the expected line break. API 34's resized view
ends at the native keyboard top (y=1517); its zero remaining shared IME overlap therefore
does not need additional padding. API 36 retains edge-to-edge native bounds and reports the
remaining overlap through the shared insets. Rich text composition, native emoji insertion
and deletion were also observed on both emulators. These observations belong to this APK,
not to the earlier failed or inset-only comparison packages.

The continuing native matrix exposed three more gaps before final acceptance: API 36's
focus-only Next did not scroll the new caret until typing; Gboard SelectAll was not forwarded
to the canonical client; and IME-synthetic Shift+arrow lost the native modifier. Narrow
corrections and native retests are in progress. Modern opt-in SendKeyEvent diagnostics are
also being restricted to metadata so printable key/Unicode codes are not logged. These later
edits are not covered by the 2785/2785 checkpoint until their own validation is recorded.

The narrow selection correction preserves native IME editing modifiers while keeping the
independent hardware/shortcut classification false. SelectAll uses only the captured client's
document-length metadata and existing SetSelection operation, with revocation checks after
metadata callbacks. Six added regressions cover real-editor Shift selection without shortcut
execution, large-document metadata-only selection, reentrant revocation and focus replacement.
The complete Android managed suite passed **204/204** after this correction; native target,
fresh APK and complete solution/package/documentation gates remain pending.

### Issue #62 — acceptance and validated implementation

Status: **OPEN / PARTIAL**. Current implementation source is
`507ca07a5509d6edc62316ebbbcb9a67ce2ecea8`. Its Android APK SHA-256 is
`B2604F0A694661D2A50AC00159996D4B586A8332110CB48A49D56011DC025882`.
The earlier entries retain the audit, discovered failures and their corrections;
the results below supersede their pending statements only for the exact source
and scenario named. The guide is [Text input and composition](../text-input.md).

The implementation reuses canonical focus and existing editor documents/virtual
operations. Captured native sessions are revoked before handoff and cleanup;
finish preserves visible preedit, while explicit cancel restores its current
checkpoint. Rich formatting and Markdown history remain in their existing models.
Shared contracts contain bounded immutable snapshots, actual shaped caret/baseline,
semantic operations/options and metadata diagnostics. Windows supplies IMM32 and
owner-HWND popup routing; Android supplies captured InputConnection operations,
selection notifications, hints/actions and native lifecycle integration.

#### Exact validation gates

| Gate | Current result | Evidence boundary |
|---|---|---|
| Debug build, source 507ca07 | PASS | Zero errors, four existing NU1902 warnings; `phase62-reviewed-debug-build.log`. |
| Full Debug tests, source 507ca07 | **2791/2791 PASS** | Independently parsed nine TRXs in `phase62-reviewed-debug-results`, zero failed/skipped. |
| Debug API compatibility, source 507ca07 | **11/11 PASS** | `phase62-reviewed-debug-apicompat.log`; merged-master 5eb19a5 baseline and the documented compiler-generated attribute exclusion. |
| Release build/tests, source 507ca07 | **2791/2791 PASS** | Zero build errors, four existing NU1902 warnings; nine TRXs in `phase62-reviewed-release-results`, zero failed/skipped. Final summary: `phase62-reviewed-summary.json`. |
| Release API compatibility, source 507ca07 | **11/11 PASS** | `phase62-reviewed-release-apicompat.log`; same baseline and documented compiler-generated attribute exclusion as Debug. |
| Final NuGet pack/validation | **11 nupkg / 10 snupkg PASS** | `packages62-reviewed` and `phase62-reviewed-packages.log`; package version remains 1.10.0. |
| Isolated final-package consumer | **74 assertions / 6 cases PASS** | `consumer62/runs/20260911T125815839Z-9a1a1c7f`: fresh private cache, mapped feed, zero ProjectReferences, build zero warnings/errors. Covers full LF/CRLF payload, singleline filtering, typed rollback/history, bounded/private metadata, stale sessions, native-method borrowing, Tab/Next and Escape binding. Package and program hashes are recorded in its `provenance.json`. |
| Executable documentation checks | **32 assertions PASS** | `phase62-reviewed-doc-tests.log`. |
| DocFX | **PASS, 1016 HTML files** | `phase62-reviewed-doc-build.log`: build succeeded with zero warnings/errors. |
| Four offline documentation archives | **4/4 PASS** | `phase62-reviewed-doc-validate.log`: validated version 1.10.0 archives with metadata source commit `507ca07a5509d6edc62316ebbbcb9a67ce2ecea8`. Final serial validation script exited 0. |
| Windows native keyboard | PASS scoped, source bf0fa94 | Polish HKL AltGr/dead-key translation through TranslateMessage/DispatchMessage to own HWND; synthetic thread-local keyboard states, restored afterward. `native-keyboard62/runs/20260911-122142-127`. Physical/CJK observations are not implied. |
| Native sample startup, source 507ca07 | **3/3 PASS scoped** | CrossPlatformSample on Windows, ControlGallery and unchanged DemoApp each had a responsive HWND and graceful exit 0; `phase62-reviewed-native-sample-smoke.json` and matching provenance. This is startup/lifetime evidence, not manual visual or candidate-window acceptance. |
| Offscreen gallery rendering | PASS scoped, earlier documented source | Ten final offscreen gallery captures at 100%/150%; `phase62-gallery-final-renderings`. These do not prove native candidate placement. |
| Android broad run, source 3949100 / APK 942E5AE8…1798E283 | **37/42 outcomes PASS; five failures fixed in 507** | The 21-row matrix across API 34/36 records four editors, emoji, Done, cancel/finish, Home/resume, IME-active recreation, separate animation-active recreation and cleanup. Five failures concern initial focus-only caret visibility and SelectAll/Shift selection; all have passing final-APK retests. Runtime settled to idle with one surface, retained text/counter and 6/6 original settings restored per emulator. Evidence: `phase62-android-smoke/RESULTS.md`, `evidence-outcomes.json` and corrected device folders. |
| Android final retest, source 507ca07 / APK B2604F0A…C025882 | **30/30 outcomes PASS** | Fifteen rows on each API cover startup, visible caret, compose/delete/commit, Next with immediate caret scrolling, LF Enter, Rich SelectAll and selected-fragment replacement, Rich/Markdown Done, Markdown editing, redacted modern key metadata and exact 6/6 settings restoration. The final device folders and outcome manifest retain matching provenance. Earlier lifecycle/emoji/animation observations are not relabeled as this APK. |
| Dedicated PR / PR CI / merge | **PASS** | Ready [PR #117](https://github.com/ProGraMajster/ModernFormsNext/pull/117), final head `e9ca9862df56591073e1a47f78ed536a6d2e11d4`, required CI [34613748529](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34613748529) passed. Normal merge `61c15190fef81e0f986c63956220fe5658ca0dca` succeeded; #62 remains OPEN/PARTIAL. |

The Android outcome manifest contains 72 scenario/device rows with existing evidence
references: 42 for the broader `3949100` run and 30 for the final `507ca07` rerun.
All five historical failures have passing affected-path final retests. API 36's final
Enter result is established by captured text and keyboard actions; its early native
LF trace had rotated out. Both APIs retain the exact LF trace at source `3949100`.
The final privacy observation covers the modern InputConnection SendKeyEvent line;
separately opt-in legacy View-key diagnostics are not claimed globally redacted.

Activity recreation retained the process and used verified replacement Activity/view
identities after font-scale changes; IME-active and animation-active recreation were
separate scenarios. Cold-process restoration, simultaneous IME/animation teardown,
rotation-specific behavior and long stress runs were not exercised by this #62 run.
The separate Focus-next demo button and native clipboard actions were not independently
exercised; actual focus transitions used native Next and pointer focus. Copy/Cut becoming
enabled demonstrates selection feedback, not successful clipboard transport. Settings
restoration passed 6/6 values per emulator after both completed sessions.

API checks exclude only `System.Runtime.CompilerServices.AsyncStateMachineAttribute`
after direct metadata inspection confirmed private generated state-machine ordinal
changes on three otherwise unchanged public Markdown methods. Other attributes,
public signatures and parameter checks remain enabled; no CP0015/NoWarn or public
API suppression is used. Exact configuration and raw evidence are retained in the
audit artifacts.

#### Every acceptance-direction criterion

| Criterion | Status | Concrete evidence and limit |
|---|---|---|
| Text input is not derived solely from raw key events | **PASS** | Semantic ITextInputClient commit/composition operations, existing KeyPress compatibility, binding exclusion and native adapter paths. `CompositionUsesExistingKeyPressAndPublishesFinalMetadata` and host/Windows/Android protocol regressions. |
| Polish/international keyboard input works correctly | **PARTIAL** | Shared Polish/Unicode/AltGraph tests and real Polish HKL translation pass. Broader physical/layout/CJK matrix remains unexecuted. |
| Active composition is explicit and updates controls correctly | **PASS shared/protocol; PARTIAL full native compatibility** | Metadata stages/ranges, typed cancellation, Markdown transaction/history and shaped provisional underline tests; four-editor Gboard observations at 3949100. Representative native CJK candidate behavior is unverified. |
| Android keyboards compose/commit without corruption | **PARTIAL** | Shared protocol tests and observed Gboard operation/LF/emoji success on API 34/36. Final-APK SelectAll, synthetic Shift and LF scenarios pass; the broader vendor matrix remains incomplete. |
| Focus changes/disposal leave no stale IME sessions | **PASS automated; PARTIAL complete native matrix** | Captured-client revocation, ancestor/reparent/hide/disable/dispose, popup/modal and exception/reentry tests; observed Next and recreation at 3949100. Final focus/selection retests pass; native candidate teardown remains unverified. |
| Framework controls reuse one common composition layer | **PASS** | TextBoxDocument and virtual Insert/Delete reused by plain, multiline, rich and Markdown; common session coordinator and actual-layout adornment. No second document or focus engine. |

#### Every additional 1.10.0 audit checkbox

| Criterion | Status | Coverage and unresolved boundary |
|---|---|---|
| TextBox/RichTextBox/Markdown/editor, hardware/software keyboards, dead keys, AltGr and representative CJK | **PARTIAL** | All existing editor consumers covered; Polish native HKL and Gboard evidence recorded separately. Physical keyboards and representative CJK engines remain **NOT EXECUTED — environment unavailable**. |
| Multiple Android API levels and vendor IMEs: compose, commit, deletion, selection, surrounding text | **PARTIAL** | API 34 Gboard 12.4.05.482060964 and API 36 Gboard 15.1.08.726012951 are two API/version configurations of one vendor. They do not satisfy a multiple-vendor claim. Bounded protocol tests pass; final native selection scenarios pass with matching provenance and restored settings. |
| Candidate/caret geometry, focus, popup/modal, Activity recreation and disposal during composition | **PARTIAL** | Actual-layout/baseline/DPI/transform/border tests, popup/modal ownership tests, observed keyboard occlusion and recreation. Final source-specific focus/selection observations pass; representative CJK candidate placement remains unverified. |
| Separate automated, observed emulator and physical-device evidence | **PASS reporting discipline** | Deterministic protocol tests, native injected messages, native HKL translation, offscreen rendering and live emulator actions retain distinct source/provenance. Physical-device testing remains **NOT EXECUTED — environment unavailable**. |

#### Other requirement groups retained from the full issue

| Requirement group | Status | Source/tests and interpretation |
|---|---|---|
| Separate committed text; start/update/commit/cancel; composition text and replacement ranges | **PASS** | WindowKit input contracts, TextBox.TextInput and ControlTextInputHost; explicit empty result, replacement, reverse-selection and callback-order regressions. Finish and cancel have different documented semantics. |
| Caret/selection synchronization, caret index, oriented selection, composition range, editable/readonly state | **PASS shared/protocol; PARTIAL native breadth** | TextInputState absolute offsets/options; existing editor selection; captured SelectAll uses metadata DocumentLength with post-query identity validation. Final native selection observations pass with matching provenance and restored settings. |
| Current/surrounding text and large-document bounds | **PASS protocol** | Immutable slices with TextStart/DocumentLength; hard 65,536 UTF-16 ceiling, zero-text metadata query, surrogate-safe clipping, revision-consistent Android queries and oversized-selection unavailability. |
| Dead keys, AltGr, international layouts, Polish characters | **PARTIAL** | Modifier/shortcut/international editing regressions and native Polish HKL proof; additional layouts/physical/CJK validation is not inferred. |
| Unicode, emoji and supplementary characters | **PASS shared/protocol; PARTIAL vendor breadth** | MaxLength and selection retain complete scalars; deletion preserves text elements; malformed native sequences rejected. Observed native emoji is recorded at 3949100. |
| Native LF/CRLF and newline-prefixed Unicode | **PASS at 507ca07 automated** | 26 client cases cover commit/compose/cancel across all existing editors, CR convention, singleline suffix filtering and Markdown AcceptsReturn; 53 client cases total. The final public package consumer passed its LF/Unicode and singleline prefix assertions within 74 assertions, specifically rejecting old packages that drop this payload. |
| Software keyboard show/hide; scopes text/numeric/email/URL/phone/password; autocorrect/capitalization; mobile return actions | **PARTIAL full native behavior** | Android EditorInfo/actions/handoff and shared options; Gboard Next/Enter/Done observed at prior source. Hints and visibility remain OS policy. Password prediction is restricted. Windows touch keyboard is unsupported by the IMM adapter. |
| Initial TextBox, multiline, RichTextBox and Markdown/editor consumers | **PASS shared** | One existing editing route; rich typed fragment restoration and unaffected formatting; Markdown existing undo transaction/final caret/redo; full legacy KeyPress payload. |
| Future document/code editors | **PASS extension seam; future products NOT APPLICABLE** | Protected Control.GetTextInputClient accepts stable custom adapters without requiring TextBox inheritance or full-document copying. No future editor product is claimed complete. |
| Windows text services, candidate/composition, caret and DPI, platform-neutral public API | **PASS IMM32 implementation; PARTIAL full native compatibility** | Context lifetime, result/default-message exclusion, geometry, cancellation and popup leases; actual owned-HWND integration and native Polish translation. Shared APIs expose no Win32 or TSF types. Native CJK placement remains unobserved. |
| Android InputConnection compose/commit/delete/selection/surrounding/actions/options/lifecycle; avoid key=character assumption | **PARTIAL native matrix** | Captured revocable session, bounded queries, batch/cursor notifications; synthetic navigation preserves modifiers without becoming hardware shortcut input. Select All has a targeted captured-client path. Final affected-path retests pass; the broader vendor matrix remains incomplete. |
| Canonical focus, mid-composition policy, popup/modal and native hosted ownership | **PASS automated; PARTIAL complete native observations** | Existing Control/WindowBase focus/traversal, session handoff, conditional popup owner leases and SetTextInputActive. Retired instances return null/false and cannot target later focus. |
| Cancellation on removal/disposal and exceptional/reentrant cleanup | **PASS automated** | Session/tree/window lifecycle tests include failed observers, disabled/hidden ancestors, reparenting, native attach rejection and combined detach/finish errors. Ownership is canceled; visible Android preedit is preserved by finish. Explicit cancel alone rolls back a current checkpoint. |
| All named regression families | **PASS deterministic coverage; PARTIAL native matrix** | Dead-key/AltGr/Polish, emoji/surrogates, CJK-shaped payloads, replacing selection, backspace/delete in composition, focus change, removal/disposal and Android composing/commit have shared/protocol tests. A CJK string fixture is not native CJK IME evidence. |
| Diagnostics: active client, composition/range, scope and backend connection; no entered text by default | **PASS detached metadata and scoped native privacy observation** | TextInputDiagnostics stores bounded metadata without text/control references. Modern native Trace removes SendKeyEvent character/key arguments; final-APK modern metadata observations pass. Existing separately opt-in legacy View-key tracing retains its documented sensitive diagnostics. HasNativeMethod describes an attached capability, not measured keyboard visibility/vendor health. Full runtime DevTools UI remains future work. |
| Phase 1 shared contracts/operations/state/selection/tests | **PASS** | Shared production contracts and current passing regression suite. |
| Phase 2 Windows integration and international/candidate validation | **PARTIAL** | Implemented IMM32 with scoped native proof; broad CJK/layout/candidate acceptance remains incomplete. |
| Phase 3 Android connection/operations/options/actions/lifecycle | **PARTIAL** | Implementation and managed/native evidence; final selection/focus/privacy observations pass and settings are restored. Vendor breadth remains incomplete. |
| Phase 4 current controls/diagnostics/docs/sample | **PASS implementation and final artifact gates** | Existing controls, metadata, guide/gallery/sample completed in current source. Final packages, isolated consumer, documentation scripts, DocFX and four archives pass; final-source startup passes 3/3. Affected native observations remain separately qualified above. |

Windows support is IMM32-compatible input, not a TSF text store or TSF locking,
reconversion, handwriting or dictation. Windows password IME composition and
touch-keyboard requests remain unavailable; ordinary password character input is
retained. IME insets are informational, with sample-level scrolling and AdjustResize
policy; typed IME inset reporting is not supplied for API 23–29. Native-host handoff
does not implement WebView, Media or a new native-control hierarchy. Future
#12/#55/#61 scope is not declared complete.

Final source review of `507ca07a5509d6edc62316ebbbcb9a67ce2ecea8` against merged
master `5eb19a5098f5fdba794d57fe599cf0224f2ab4d8` found no unresolved code blocker
in the implemented scope. Later edits only finalize documentation; no production
or test source differs from this validated commit. No new dependency, public API
removal, version bump or release/publication metadata change is included.

The implemented slice passed local validation, independent final review and required
PR CI. PR #117 merged on 2026-09-11 at 15:10:56Z. Its final documentation-only head
also passed a fresh DocFX build with zero warnings/errors and all four archives.
The merged tree matches the reviewed PR tree; local master and origin/master were
fast-forwarded to the merge. Post-merge CI
[34614592355](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34614592355)
passed at that exact merge. #62 remains OPEN/PARTIAL for the broader native-language,
vendor, physical-device and candidate requirements.

## Issue #109 — Complete Android hardware shortcut forwarding and modifier parity

Started from verified merged master `61c15190fef81e0f986c63956220fe5658ca0dca`.
The full current issue, all comments (zero), empty formal blocked-by list and the
related #56/#62/#69/#72 histories were audited with source, tests, docs, limitations,
roadmap and sample consumers. Three independent bounded audits covered native input,
shared resolver/lifetime and history/Designer/documentation boundaries.

The [pre-implementation plan](issue-109-android-hardware-input-plan.md) records all
six acceptance criteria and the additive native handled/reset seams. Current shared
command runtime is reused; #69/#72/#108 remain separate. At the audit checkpoint no
#109 implementation validation had run; subsequent results are recorded below.
The initial unrelated `.codex/config.toml` remains untracked and untouched.

### Issue #109 — initial implementation and regression evidence

The committed audit/plan precedes implementation at `43d95ba`. The adapter extends
the existing WindowKit key identities and canonical resolver, adds a single native
handled-result route and keyboard reset/cancellation boundaries, and preserves the
old seven-key event and surface void APIs. No Android command registry or new
windowing host is introduced. The sample uses its actual adapter in linked-source
Android integration tests and demonstrates local/page/Application command routes.

Initial local runs, before final source freeze and native acceptance:

| Gate | Result | Scope |
|---|---|---|
| Restore | PASS | Existing NU1902 for Microsoft.Build.Tasks.Git 10.0.301 remains; no dependency change. |
| Full managed Android test project | 305/305 PASS | Production key mapper, real sample bridge/surface, source/modifiers, scopes, repeats, IME separation, cancellation and transport tests. Native lost-release pairing is a subsequent focused addition. |
| Core input/effect focused tests | 141/141 PASS | New surface and existing binding/interaction regressions. |
| Full Core Debug tests | 1285/1285 PASS | Existing editor, layout, rendering, input, lifecycle and resource regressions included. |
| Full sample Debug tests | 22/22 PASS | Six new command/ownership cases in the existing project. Four new lifetime/scope cases first failed against the initial implementation, then passed after fixes. |
| Baseline API source build | PASS | Fresh external worktree at master `61c1519`, 11 package assembly/TFM inputs plus both Android backend TFMs. Generated Windows interop rewrote line endings, but its normalized Git blob equals HEAD and the source diff is empty; raw Git status is recorded rather than falsely called clean. |
| Final Debug/Release solution, API/package/consumer/docs gates | Pending at this initial checkpoint | Final source and documentation results are recorded below. |
| Native emulator and sample rendering/startup | Pending at this initial checkpoint | Subsequent #109 observations are separate from historical #62 evidence. |

The first Android integration run passed 32 of 33 cases. Its failing assertion
incorrectly expected a managed Left event delivered after the native IME stage to
select a candidate. The existing editor accepts visible preedit and moves its
caret; the corrected regression explicitly checks that behavior, no command call,
and a separate commit at the new caret. It does not claim native candidate testing.
Initial new Core tests also needed nullable snapshot unwrapping before compilation;
no production workaround or suppression was added for that test-source error.

Independent sample review found and reproduced four static ownership/scope failures:
failed constructor/platform facts retained an Application binding, a throwing Add
diagnostic retained the just-added item, throwing child disposal skipped the old
Disposed-based cleanup, and F1 could target another page retaining selection.
Registration now occurs after full initialization with rollback; cleanup captures
the collection and precedes child disposal; the existing RoutedCommand machinery
uses the current input target. Status labels wrap on narrow screens, and ordinary
input diagnostics no longer expose printable key identities. Native handler
replacement is included in mandatory host cleanup because reset observers may throw.

The subsequent native pairing addition passed the complete Android managed suite
at **314/314**. Nine new cases cover per-device Down/Up pairing, late releases and
orphan repeats after reset, actual Button activation safety, legacy/IME isolation,
reentrancy and bounded capacity cleanup. This is a native transport lifetime guard;
command matching and consumed shortcut state remain in the existing shared resolver.

### Issue #109 — validated source and local artifact gates

Production/test checkpoint: `8fd369650ebeba53ec78e299483c8db8a0216701`, with a clean
tracked tree when its builds, tests, package files and APK were produced. The APK
SHA-256 is `564A74A2F23E4107A4B58C87BFA830B43566B84A0C5959E0E672B2B14638EE2D`.
Subsequent finalization changes only Markdown documentation. The original unrelated
untracked `.codex/config.toml` remains untouched.

All paths in this section are relative to ignored `artifacts/autonomous-audit/`.
No generated binaries, local paths, diagnostic logs or package outputs are committed.

| Gate | Result | Exact evidence and practical scope |
|---|---|---|
| Restore | PASS | SDK selected by the repository; existing Microsoft.Build.Tasks.Git 10.0.301 NU1902 remains, without dependency/version changes. |
| Full Debug build and tests | **2944/2944 PASS** | `phase109-first-debug-build.log`, `phase109-first-debug-tests.log` and nine TRXs in `phase109-first-debug-results`: zero failures/skips; build zero errors, four existing NU1902 warnings. |
| Full Release build and tests | **2944/2944 PASS** | Corresponding `phase109-first-release-*` logs and nine TRXs: zero failures/skips; build zero errors, four existing NU1902 warnings, including native Android compilation/AOT. |
| API compatibility against merged master 61c1519 | **13/13 PASS per configuration** | Authoritative logs: `phase109-first-debug-apicompat-complete-refs.log` and `phase109-first-release-apicompat.log`. Eleven package assembly/TFM inputs plus both Android backend TFMs; no attribute exclusions or unresolved references. |
| NuGet pack and validation | **11 nupkg / 10 snupkg PASS** | `packages109-first`, `phase109-first-pack.log`, `phase109-first-packages.log`; version remains 1.10.0 and nothing is published. |
| Isolated package consumer | **40 assertions / 6 cases PASS** | `consumer109/runs/20260911T160111061Z-0aff1b30`: fresh private cache/feed, zero ProjectReferences, build zero warnings/errors, package/program hashes recorded. Exercises factory conversion, exact modifiers/repeats, text/dead-key safety, reset/cancellation, handling/focus and old void/borrowed APIs. |
| Native Android APK | **PASS** | `phase109-8fd3696-apk-build.log` and provenance JSON: zero errors, two existing NU1902 warnings, exact committed source and hash above; not physical-keyboard evidence. |
| Native Windows sample startup | **3/3 PASS scoped** | `phase109-first-native-sample-smoke.json` and provenance: CrossPlatformSample, ControlGallery and unchanged DemoApp each displayed a responsive HWND and closed normally with exit 0. DemoApp verifies template startup only. |
| Actual sample offscreen rendering | **8/8 captures PASS scoped** | `render109/runs/20260911T155643354Z/RESULTS.md`: 320/411 logical widths at 100%/150%, initial and command states; new rows fit/wrap without overlap, exact editor/page/save-as/help counts 1/1/1/1 and disabled Save state visible. DLL hashes/source recorded. Windows-font offscreen rendering is not native Android or physical/manual evidence. |
| Documentation script tests | **32 assertions PASS** | `phase109-first-doc-tests.log`. |
| Initial source DocFX | **1018 HTML files, zero warnings/errors** | `phase109-first-doc-build.log` produced four archives. Their initial validation correctly stopped the gate on a conservative local-path pattern; the cause and final rerun are recorded below. |

Both solution configurations contain Automation 140, Automation.Windows 61, sample 22,
Designer 640, Testing 359, core 1285, VSIX 26, Android 314 and Windows 97 tests.
These results include the pre-existing accessibility, automation, serialization,
command, resource, animation and Windows backend regressions.

Validation failures were investigated rather than hidden. The first API invocation
could not resolve Android's generated resource-designer reference; the comparison
helper now includes each side's own generated DLL as well as its actual assets and
Android reference pack. The authoritative reruns above contain no unresolved
references and no inherited #62 compiler-attribute exclusions. The first isolated
consumer omitted Tab KeyUp before pressing Shift+Tab, correctly retaining a consumed
press. Adding the real release fixed the test driver; no framework behavior changed.

The first archive validator matched the prose fragment `Back`, `Home`, `power`,
`volume`, `media` when these words were slash-separated: its case-insensitive Unix
home-directory detector produced a false positive. There was no local path leak.
The plan now uses a comma-separated list; the validator remains unchanged. The
initial gate failure remains in `phase109-first-doc-validate.log` and is not reported
as an archive PASS. Final documentation is rebuilt and validated at its committed head.

Independent review found no unresolved code blocker in the scoped adapter. It also
corrected the guide's cancellation wording: no ordinary release ripple or activation
is synthesized, but PressScale can animate its return to rest. Existing narrow-page
single-line header/diagnostic text still clips; the eight render checks establish
the new rows' fit, not universal text-fit for the whole sample.

The serial commands were `dotnet restore ModernFormsNext.slnx`; Debug and Release
`dotnet build ModernFormsNext.slnx --configuration <configuration> --no-restore -m:1
/p:UseSharedCompilation=false /p:EnableWindowsTargeting=true`; corresponding
`dotnet test` with `--no-build --no-restore -m:1`, TRX logging and the same properties;
`dotnet msbuild artifacts/autonomous-audit/ApiCompat109.proj -t:Compare` for each
configuration; `dotnet pack` Release with `--no-build --no-restore -m:1`; repository
package/documentation validators and the isolated consumer runner. The Android
sample build selected `net10.0-android`, `SignAndroidPackage`, embedded assemblies
and APK format. Full exact command lines and exit status remain in the named scripts/logs.

### Issue #109 — native keyboard acceptance

Native evidence is kept in `phase109-android-smoke/`, separately from managed tests,
offscreen images and prior #62 runs. The current implementation/APK identity is the
8fd3696 source and 564A74A2…4638EE2D hash recorded above. API 34 used Pixel_8, Gboard
12.4.05.482060964-preload-x86_64 and its existing AT keyboard device 2 with Generic.kl;
delivered events had Keyboard source and FromSystem flags. This is injection into
an emulated input device, not a physical USB/Bluetooth keyboard. The emulator console
alone reported acceptance without delivery. On the owned userdebug emulator, a
temporary root adb session enabled bounded evdev injection; original daemon identity
and settings must be restored before accepting the run. No SELinux, permissions,
bootloader or keyboard-layout configuration was changed.

| Native scenario | Observed result |
|---|---|
| F1 / Ctrl+S / Ctrl+Shift+S | PASS: Help, page Save and Save As each increased only its own counter. Ctrl and Ctrl+Shift retained distinct native meta states 12288 and 12353. |
| Focused editor Ctrl+S | PASS: verified editor focus; editor counter 0→1. |
| Unavailable editor fallback | PASS: disable the inner binding, refocus the same editor and press Ctrl+S; page counter 1→2. |
| Native repeats and release | PASS: ten delivered S Down events with RepeatCount 0–9 produced exactly ten page executions (2→12); release was consumed without another action. |
| Ctrl+RightAlt+S | PASS scoped safety: native meta state 12322, unhandled and no command-count change. This is not proof of general international text entry. |
| Pause/canceled/late Space release | PASS: Space Down followed by Home caused a canceled native Up; after pause/resume, a late device Up without a native canceled flag was handled without activation. Save As stayed at 2; a fresh Space pair then increased it exactly once to 3. |
| Ordinary A / Shift+A and available accent key | PARTIAL: native events reached the view unhandled, without semantic text commits or inserted characters in this Gboard configuration. No shortcut or duplicate text was produced. Hardware typing/layout composition is not reported as supported by this observation. |
| API 34 and API 36 virtual negative controls | PASS scoped exclusion: virtual-device F1/Ctrl+S did not execute commands. A virtual source is not positive hardware evidence. |
| API 36 eligible positive keyboard route | **NOT EXECUTED — environment unavailable**: Android 16/Gboard 15.1.08.726012951-preload-x86_64 console delivery produced no native key event. This transport limitation is not treated as a framework failure or native shortcut success. |
| Physical keyboards, additional vendors/layouts/CJK | **NOT EXECUTED — environment unavailable**. |

The API 34 qwerty2 device's vendor layout maps Linux F1 to Android Menu; this
unsupported system key correctly remained unhandled. The positive shortcut lane
used the existing AT keyboard's standard mapping. Layouts were inspected, not changed.
The guide and AND-07 explicitly describe the observed hardware-text limitation:
the native Skia View has no editable KeyListener; semantic IME/text-service commits
remain the canonical text path. No character translator was added to shortcut routing.

A bounded comparison actually reinstalled the preceding #62 APK from source
`507ca07a5509d6edc62316ebbbcb9a67ce2ecea8`, SHA-256
`B2604F0A694661D2A50AC00159996D4B586A8332110CB48A49D56011DC025882`, on the same API 34
emulator/device/IME. Ordinary and Shift-modified A again arrived unhandled without
inserted text or semantic commit. This establishes the observed text limit predates
#109; it is not inferred solely from source inspection. Evidence is separated by
process: baseline snapshots 20–22 and process-scoped log, then restored #109 snapshot
23. The current #109 APK was reinstalled without clearing package data afterward.

The consolidated native matrix in `phase109-android-smoke/RESULTS.md` and
`evidence-outcomes.json` contains **16 cases: 12 PASS, 2 FAIL for observed hardware
text/composition limitations, and 2 NOT EXECUTED** for API 36 positive delivery and
physical hardware. These failures are retained; the native run is not reported as
an unconditional 16/16 success. Both installed APK hashes were checked against the
current 8fd3696 artifact after the baseline comparison. Both emulators restarted
normally without opt-in input diagnostics. Final cleanup released all nine injected
evdev key codes without failures, restored all six recorded settings on each
emulator, and restored API 34 adb to its original UID 2000; API 36 never used root.

### Issue #109 — final acceptance disposition

| Acceptance criterion | Implemented and validated scope | Remaining boundary |
|---|---|---|
| Supported identities and Ctrl/Shift/Alt/Meta, including both Save gestures | Explicit mapping, original values preserved, full deterministic coverage and actual API 34 F1/Save variants. | Native policy/vendor mappings can reserve or reinterpret keys; every layout/device is not claimed. |
| Down/Up/repeat/handled/focus/lifetime without duplicate actions/text | Exclusive native route, captured lifetime, per-device press pairing, shared suppression/reset and control handling; regression suites plus native repeat and pause/late-release observations pass. | Physical reliability and broader lifecycle stress are unobserved. |
| AltGraph/dead-key/software/IME safety | Provenance and modifier separation, no text-derived shortcuts, semantic commit path retained; deterministic safety and scoped native RightAlt observation pass. | Observed Gboard hardware text insertion is incomplete; broader international/vendor/IME observations remain partial. |
| Canonical scope lookup and unavailable fallback | Actual mapper/sample bridge exercises control, two ancestors, surface and Application; Windows window scope retained. Native focused editor and unavailable fallback pass. | General Android window host remains excluded #72; no fabricated WindowBase scope. |
| Deterministic tests and separate emulator/physical records | Existing nine test projects pass in both configurations; native API 34 and API 36 negative observations have their own provenance. | Physical hardware and API 36 positive delivery remain explicitly unexecuted. |
| Supported combinations and honest parity documentation | Supported key matrix, return/reset/ownership contract, sample, AND-07, Android status and roadmap updated. | #109 remains **OPEN/PARTIAL** for the recorded native breadth and text-integration limit. |

All currently implemented shortcut work is suitable for a dedicated Ready PR after
final documentation validation and required CI. The parent issue's remaining
physical/native breadth does not prevent merging this useful scope under the user's
finalization policy. No public API removal, renderer replacement, new command/tree
system, dependency, package boundary, release publication or version bump is included.

### Issue #109 — final documentation, PR and verified merge

Final documentation head `479674524142b9cb4811293c3362ce23202d8007` passed 32 script
assertions, DocFX with **1018 HTML files and zero warnings/errors**, and **4/4 archive
validation**. `phase109-4796745-summary.json` independently checks all 18 TRXs,
authoritative 13/13 API logs, the 40-assertion package consumer and Markdown-only
changes since implementation 8fd3696. The final native report and independent review
agree on all 16 observed cases and their limitations; no scoped code/docs blocker
remained.

Ready [PR #118](https://github.com/ProGraMajster/ModernFormsNext/pull/118) passed required
CI [34621407730](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34621407730)
at that exact head. Normal merge succeeded on 2026-09-11 at
`f9e363fb8ed270c9db60a6e2e48f37d5a6b08f11`. Fetch/prune and fast-forward pull completed;
local master equals origin/master and its entire tree equals the reviewed PR head.
#109 remains OPEN/PARTIAL. The next #59 branch starts from this verified merge.

Post-merge master CI [34622176286](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34622176286)
also completed successfully at exact merge `f9e363fb8ed270c9db60a6e2e48f37d5a6b08f11`.

## Issue #59 — Add accessibility and platform UI automation infrastructure

Fresh baseline: `f9e363fb8ed270c9db60a6e2e48f37d5a6b08f11`; branch
`codex/issue-59-accessibility-phase4`. The refreshed complete issue and all three
comments match the preliminary audit, with no formal blockers. Existing Phases 1–3
and their historical evidence are preserved. Related current issues, prior merged
PRs, source, tests, platform adapters, samples, Designer metadata and documentation
were audited; the original unrelated `.codex/config.toml` remains untouched.

The [Phase 4 audit and technical plan](issue-59-accessibility-phase4-plan.md) commits
the current scope before implementation: existing link/numeric/bar controls, viewport
Scroll, grid/table, date/calendar, practical Text/TextRange, explicit preference
consumption, bounded diagnostics/Designer and cross-layer acceptance. Concrete
current editor, grid, popup, layout and privacy defects are prerequisites within
these slices, not reasons to defer them to future virtualization or Android hosting.
The technical plan was committed before the first Phase 4 source change.

### Phase 4 implementation checkpoint — 2026-09-12, uncommitted work in progress

The existing canonical hierarchy now has concrete link/numeric/bar, viewport,
grid/table, date/calendar and practical text implementations under development.
Optional preferences, bounded snapshot diagnostics and Designer metadata use their
existing framework contracts. The new [grid/calendar guide](../accessibility/grids-and-calendars.md)
documents identities, callbacks, input, platform boundaries and limits.

These are **intermediate working-tree checks**, not final PR acceptance or native
screen-reader evidence:

- Initial Core grid/edit/text subset: **38/38**; initial Windows UIA provider suite:
  **80/80**; initial Android mapping subset: **22/22**, plus native Android backend
  compilation. Later changes require rerunning all these checks.
- Grid/viewport Automation snapshot/action subset **5/5** and protocol subset
  **5/5**; subsequent diagnostics/grid/viewport subset **13/13**.
- Callback regressions against the previous working implementation reproduced all
  **5/5 failures** (sparse-row reattachment, post-EndEdit reattachment, throwing
  selection cleanup, publishing an incompletely bound row, and replacing DataSource
  in a property getter). Their fixes are now present and passed the later full Core
  run. Baseline grid editing probes also preserve the earlier **4 FAIL / 1 PASS**
  result against exact master `f9e363f`, distinct from these working-tree probes.
- New DPI/raster and viewport cases reached **56/56**, then **62/62** when the first
  six viewport hardening cases were included: 25 current-controls, 5 pixel-parity,
  13 composite viewport, 13 viewport, 6 hardening cases. More tests were added later.
- First full Core run: **1369 PASS / 5 FAIL / 1374**. Four failures concern existing
  presentation-padding behavior and the old generic action test's Scroll payload;
  the fifth concerns the date popup's native-hide lifecycle. The next full Core run
  passed **1377/1377** after the fixes; subsequent review added further regressions.
- First Designer accessibility run: **10 PASS / 7 FAIL / 17**. Reverse parsing
  preserved the metadata but normalized implicit Dock ordering; the test now authors
  that existing property explicitly before requiring identical generated code.
- The first new Windows preference reader compile exposed a generated-COM cleanup
  pattern mismatch. The corrected reader compiled and its four focused tests passed,
  including an actual Windows WinRT query/subscription/disposal smoke.
- Subsequent Designer accessibility **17/17**, Testing preferences/hardening **18/18**,
  cross-platform sample full suite **28/28**, and Automation full suite **160/160**
  passed. The Automation first full run's two test-fixture/old-payload expectations
  were corrected while retaining explicit invalid/unsupported and full diagnostic-budget coverage.
- Independent calendar review reproduced **3/3** red probes for culture boundaries
  and retired providers. Two grid probes reproduced unwanted second-axis scrolling
  after row/column reinsertion. The corrected grid/calendar subset passed **38/38**.
- Actual Windows out-of-process Text assertions passed. Native Scroll and password
  privacy passed in the later 9-pass subset. A focus trace identified UIAutomationCore's
  separate AutoSetFocus call; canonical Scroll preservation is checked independently.
- The new actual HWND Grid/Table/Calendar scenario passed all **17** checks in its
  fourth run, including edit event order, headers, sorting identity, reveal, read-only
  values, stale actions, checkbox state, popup selection and native popup retirement.
- Full Android managed run reached **356 PASS / 1 FAIL / 357**. The new own-password
  action fixture lacked a visible host; its corrective surface setup awaits rerun.
- `origin/master` was fetched again and still equals baseline `f9e363f`; no Phase 4
  PR exists yet. The resumed environment initially had no running emulators; API 34
  was started from the existing Pixel_8 AVD for the new standalone-APK checks.

Restore metadata now reports NU1902 for the pre-existing private build dependency
`Microsoft.Build.Tasks.Git` 10.0.301 through SourceLink. The existing master has the
same pin; this is a known baseline warning, not a new Phase 4 runtime dependency.
The [upstream advisory](https://github.com/advisories/GHSA-23fw-v26w-5fgq) identifies
10.0.303 as the fixed 10.0.3xx package. Dependencies remain unchanged under this
issue's committed scope; zero-warning build acceptance is not claimed.

Further working-tree verification passed full Core **1389/1389**, Testing **435/435**,
Windows **139/139**, Android **357/357**, and Windows Automation **66/66**. These
precede the final review corrections below and are not attributed to a committed
implementation revision.

The third intermediate standalone APK, SHA-256
`5D9E33E169246F91A2101C2AD396E81F4B0227357C0B96D4CADC505723DCD1F1`, passed the expanded
native runner **64/64 on API 34 and 64/64 on API 36**. The earlier API 34 run stopped
after 35 assertions on an incorrect fixture expectation that sensitive ancestry
must reject ordinary Click. The corrected fixture preserves that assistive action,
checks its actual callback, and separately proves redaction and rejected protected
text/range operations on real controls. Sensitivity is not a blanket native-action
authorization policy. The old failed result remains recorded separately.

Native validation of the ordinary sample then exposed an **ANR on both emulators**,
including API 34 with accessibility services temporarily disabled. A bounded API 36
comparison installed the preceding #109 APK (the 564A74A2 hash recorded above): its
ordinary launch remained responsive and an injected Save click increased only its
editor counter from 0 to 1. The Phase 4 startup regression therefore blocks acceptance
until diagnosed and corrected; passing the dedicated accessibility fixture does not
establish ordinary-sample usability. Original emulator settings were recorded before
changes and must be restored after the remaining native checks.

Independent final review also reproduced four failing Windows UIA COLORREF tests
(alpha roundtrip and alpha-only mixed spans) and a failing semantic grid edit test
(a callback-owned replacement editor on the same cell was incorrectly overwritten).
The color projection correction then passed **6/6**, including clipped forward and
backward searches. Grid/date review passed **58/58** after exact edit-session ownership
and popup privacy corrections. A further red probe found that the popup's independent
name could inherit a protected picker label; the corrected complete DatePicker suite
passed **29/29**. Activity recreation checks now wait for actual replacement host,
node and subscription state instead of assuming a fixed delay is sufficient.

The startup ANR was traced to repeated geometry during canonical child membership
queries. A temporary, bounded native trace measured the fifth APK's first Resize at
**10.473 seconds and 192088 GetScrollState calls**. The real-tree regression first
reproduced 512 unnecessary geometry reads; it preserves offscreen membership and
custom peers' independent Invisible state. The correction avoids full State reads
only for the exact default peer when its View already determines membership, skips
scroll geometry with no scrolling ancestor, and maps transformed corners without
temporary arrays/LINQ. No tree cache or second membership contract was introduced.

The sixth APK on the same API 34 emulator measured **0.209 seconds and 938 calls**
for the same first Resize; the notification, registration and child-lookup counts
were unchanged. Its ordinary launch exposed the native tree without an ANR. This is
a scoped instrumented comparison, not a universal performance benchmark. Full Core
then passed **1397/1397** on the working source. The actual sample's two-render test
and a separate production Android session/render test also passed. All temporary
trace fields, reflection and logging were removed afterward; final clean-source APK
and broader native checks are still required.

All final Debug/Release solution checks, unfiltered API comparison with exact master,
package/consumer/docs checks, the expanded native Windows UIA scenarios, and new
Android emulator observations still remain. Physical-device/screen-reader manual
checks are **NOT EXECUTED — environment unavailable**. No Phase 4 PR, merge, release
publication or issue closure is claimed at this checkpoint.

### Phase 4 committed implementation and final validation — 2026-09-12

The following evidence supersedes the pending statements in the working-tree
checkpoint above for the explicitly identified revisions. The audit/plan was
committed as `11ef3f9` before implementation. Changes are in dedicated
[PR #119](https://github.com/ProGraMajster/ModernFormsNext/pull/119), based on merged
master `f9e363fb8ed270c9db60a6e2e48f37d5a6b08f11`. Implementation, fixtures/guides,
the Gallery compilation correction and the preserved public trimming annotation
are separate commits. Final implementation source is
`bc160dcbbe8f7ea0acff1cdee18569f8bb8cd50f`.

Current controls now expose canonical link/numeric parts, viewport actions,
grid/table metadata, date/calendar peers and practical text ranges. Native adapters
invoke actual control operations. Optional system preference detection feeds an
explicit, scoped sample consumer; diagnostics consume existing detached automation
snapshots, and Designer retains simple accessibility metadata. The Gallery contains
five integrated pages. The late privacy, edit-session, native text-color and startup
performance corrections described above are included in this source.

| Final gate | Result at the implementation revision |
| --- | --- |
| Restore and complete Debug / Release builds | PASS; serial MSBuild, shared compilation disabled. Local SDK 10.0.401 follows the existing global.json latestFeature policy. |
| Nine test projects in each configuration | 3273/3273 Debug and 3273/3273 Release; zero failures or skipped tests. |
| API against exact merged baseline | 13/13 per configuration, with the four compiler representation exclusions and independent semantic checks described below. |
| Package validation | 11 nupkg and 10 snupkg at unchanged version 1.10.0; all repository package checks pass. |
| Fresh external package consumer | 68 assertions across seven scenarios; fresh private cache and package hashes, zero ProjectReference inputs. |
| Documentation | 32 script assertions, 1049 DocFX HTML pages, 0 DocFX warnings and four validated archives. |
| CI for implementation | [.NET build](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34706397152) PASS; the workflow installs SDK 10.0.201 and builds Release. |


The first consumer run passed all 68 runtime assertions and checked the installed
package hashes, but its final provenance projection retained only one package entry.
A separate rerun corrected that evidence projection and passed all 68 assertions
with all five distinct package hashes recorded. An initial supplemental invocation
used a relative export path and failed while saving the result after changing to
the external directory; the subsequent absolute-path invocation completed with exit
code 0. Original manifests and failed-command logs remain preserved. The final
documentation check verifies both original and supplemental evidence separately.

The test project totals in each configuration are Automation 160, Windows Automation
66, cross-platform sample 29, Designer 657, Testing 435, Core 1397, VSIX 26, Android
backend 358 and Windows backend 145. The full Windows suite includes real external
Text, Scroll and Grid/Calendar clients, in addition to managed provider checks.
The existing NU1902 SourceLink build-dependency warning remains as documented above;
neither zero-warning solution builds nor a dependency upgrade is claimed.

The unfiltered API attempt at `6a0a438` was retained as a **FAIL**, with 21 unique
attribute diagnostics. The authored `DataGridView.EndEdit` trimming annotation was
restored exactly in `bc160dc`; it is not excluded. Remaining differences concern
`IteratorStateMachineAttribute`, `CompilerGeneratedAttribute`, `NullableAttribute`
and `NullableContextAttribute`. The first two reflect private iterator identity and
the change from automatic to authored visibility accessors. The latter two changed
encoding placement, which was checked independently with SDK Roslyn metadata imports.

All 13 assembly/TFM pairs in **both** configurations must pass effective-nullability
comparison before filtered ApiCompat can run. Each configuration compares 22380
existing declarations across its TFM matrix, with zero differences or unresolved
types. Four checker controls prove detection of changed nullable returns, parameters
and generic arguments, and acceptance of an equivalent contract despite a changed
raw enclosing context. Structural, parameter-name and all other attribute rules
remain enabled, including trimming, nullable-flow and platform annotations. The
previous issue's AsyncStateMachine exclusion is not inherited. This is **filtered
ApiCompat plus independent semantic proof**, not an unfiltered PASS. Hashes bind the
comparisons to their binaries, references, helper, control cases and exact source.

The final-source Android APK at **bc160dcbbe8f7ea0acff1cdee18569f8bb8cd50f**, SHA256 **F8857B4AEC0917B2FB9A10C81613EE554B458A36CD9B41FEC9D13240947AE914**, passed **64/64 real Phase 4 instrumentation assertions on API 34 and 64/64 on API 36**. Both fresh ordinary launches completed (1647 ms/3139 ms, individual observations rather than a benchmark); ready native XML shows command counts 0, preference opt-in off and Running/Foreground generation 1. Final device readbacks confirm eight original settings exactly restored per emulator, absent contrast keys, matching installed APK hashes and ADB uid 2000; original accessibility-service settings are restored. The separate native preference scenario remains attributed to **f92759ae2bdc4a482cb7fe4c3c7aa95edc13565b**, APK SHA256 **81F1ABE0C41213192F2194EF153197DB86654FB62B6B13D26F40B9A6ABD51EFC**: 18/18 artifact assertions and four inspected PNGs establish actual High/1.3 propagation, opt-out theme retention, exact row-geometry restoration and preserved command count across recreation on both APIs. These OS mutations were not rerun at bc160dc. The intervening commits only fix Gallery API usage and restore EndEdit's trimming annotation. Physical devices, a new human TalkBack speech/navigation assessment and vendor/OEM coverage remain **NOT EXECUTED**; bound-service restoration is not speech evidence.

Windows ControlGallery at `bc160dc` passed 23 native UIA acceptance steps, including
all five tabs, real list/grid mutations, stable sorting identity, denied read-only
writes, retired rows, calendar popup lifetime, link/range actions, text selection,
protected input and two-axis reveal. The client and Gallery closed normally with
exit code 0. Five native window PNGs at 1082 by 756 were inspected. The principal
controls and action labels were readable; the long Viewport footer's last edge was
not clearly fully visible, and long sidebar/grid content clips within its viewport.
This is one captured size and light appearance, not a full DPI/theme or human
screen-reader matrix. Separate final-source native startup checks passed for the
cross-platform sample, ControlGallery and the unchanged DemoApp reference app;
all three windows responded and closed normally with exit code 0.

### Issue #59 acceptance disposition

| Live acceptance direction | Current implementable scope | Evidence / remaining qualification |
| --- | --- | --- |
| Useful common-control names, roles and state in native accessibility services | PASS | Existing controls and new logical peers are exercised by shared, Windows and Android checks. Universal physical-service usability remains unverified. |
| Keyboard focus changes reflected correctly | PASS | Existing focus route, native focus/events and text caret/lifetime tests remain canonical. Accessibility focus and UIA's own AutoSetFocus are distinguished from control scrolling. |
| Appropriate real actions and patterns | PASS | Existing Invoke/Toggle/Value/Selection/ExpandCollapse plus Scroll, Grid/Table and practical Text paths reach normal model operations. Unsupported Android windowless popup actions are not advertised. |
| Custom-rendered logical semantic children/actions | PASS | Existing virtual accessibility hooks remain the extension seam; real link, grid and calendar peers and custom-peer tests use it. |
| Platform-neutral public framework API | PASS | Shared contracts contain neutral types; native interop remains in backends. Compatibility evidence is qualified above. |
| Sensible defaults without explicit metadata | PASS | Central defaults and fallback names are retained; editor values never become implicit field labels, and explicit empty names remain meaningful. |
| Preserve existing rendering and control hierarchy | PASS | Text, grid, viewport, preference and diagnostic integrations use current documents, controls, ThemeManager and snapshots. |

Issue #59 remains **OPEN/PARTIAL** overall. Future recycled containers (#55), full
Developer Tools inspector UI (#61), general Android window/popup hosting (#72),
broader physical reliability (#69) and separate embedded-document adapters retain
their documented boundaries. Physical-device checks, a new human screen-reader
assessment and unavailable manual tooling are **NOT EXECUTED — environment unavailable**.
These boundaries do not prevent merging this completed, validated Phase 4 scope.
No release, tag, dependency/version bump, package publication or issue closure is
part of this change. The user's unrelated `.codex/config.toml` remains untouched.
