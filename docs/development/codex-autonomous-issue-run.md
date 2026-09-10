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
contracts are reused. Future #64 integration with lifecycle/navigation/virtualization must be
recorded explicitly until #63/#12/#55 exist; no parallel testing runtime will fill those gaps.

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
PR: [#113](https://github.com/ProGraMajster/ModernFormsNext/pull/113) (draft, base master). Subsequent issues are audited,
not yet implemented.

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

Future activation/navigation/virtualization acceptance remains dependent on #63/#12/#55. Those
criteria will be revisited after their actual runtime contracts exist; they are not silently
removed from #64. Native Windows/Android/IME/accessibility evidence remains separate.

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
| Phase 4 activation/lifecycle integration with #63 | BLOCKED (dependency) | Current fake exposes the existing coarse backend contract; richer application runtime not yet implemented |
| Phase 4 navigation and shared virtualization integration | BLOCKED (dependencies #12/#55) | Those canonical runtime subsystems do not yet exist; revisit after implementing them |
| Future touch, drag-and-drop and focus-scope helpers | NOT APPLICABLE to current phases | Explicit future directions; no excluded feature implemented |

Overall issue status: **PARTIAL**, with the remaining ecosystem coverage **BLOCKED** on actual
later queue dependencies. This is not permission to close #64. Implementation of #63 may proceed
after PR finalization, and the dependent #64 rows must be revisited later. Documentation validation
also corrected 44 source links in the initial audits to verified baseline GitHub permalinks; no
audit findings were changed.

Manual Windows visual review, Android emulator/physical-device checks, TalkBack and Visual Studio
interaction for this phase: **NOT EXECUTED — environment unavailable**. The ControlGallery check
was an automated native startup/close smoke; the inspected PNG was headless raster output.
Template/reference-app validation: NOT APPLICABLE; generated application startup was unchanged.

### #63 — refreshed audit and implementation plan

Baseline master remains `ba396f95adab82564a0681bc922096599ba8c1ca` after fetch.
Full current issue and all comments (zero) were read again. Work is on
`codex/issue-63-lifecycle-activation`, stacked on #64 PR #113 at `311fa82`; #64 is
not represented as merged. The earlier all-queue audit supplies related issue/history context.

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
