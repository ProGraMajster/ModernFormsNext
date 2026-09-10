# Autonomous issue development run

Started 2026-09-10. This report records current observations separately from historical issue/PR
claims. It is updated at each implementation/validation boundary. No release, tag, version bump,
NuGet/VSIX/Marketplace publication is authorized by this run.

## Starting repository and validation

- Fetched `origin`; `master == origin/master == ba396f95adab82564a0681bc922096599ba8c1ca`.
- Work branch: `codex/autonomous-runtime-queue`.
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

Commit/PR: pending implementation. Subsequent issues are audited, not yet implemented in this run.
