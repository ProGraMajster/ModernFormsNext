# ModernFormsNext: release roadmap (1.12 → 2.x)

> Planning snapshot: **2026-10-17**. Targets are candidates, not a promise that every proposed issue will ship in that release. Do not introduce artificial breaking changes simply to justify the 2.0.0 version number.

## Release lanes

| Release lane | Open issues | Scope | GitHub tracking |
| --- | ---: | --- | --- |
| **1.12.0** | 38 | Finish previously committed work, critical correctness/Designer fixes, Android and release checks | Existing milestone **ModernFormsNext 1.12.0** |
| **2.0.0** | 47 | New-generation runtime, GPU/diagnostics, tools, API evolution and modern control essentials | `target: 2.0.0` |
| **2.1.0** | 58 | ImageViewer, docking, progress/controls, services and polish | `target: 2.1.0` |
| **2.2.0** | 17 | Advanced visualization, optional codecs/templates and progress extensibility | `target: 2.2.0` |
| **Later** | 10 | High-uncertainty, experimental or platform-expansion proposals | `roadmap: later` |

### Live issue filters

- [1.12.0 milestone](https://github.com/ProGraMajster/ModernFormsNext/issues?q=is%3Aissue%20is%3Aopen%20milestone%3A%22ModernFormsNext%201.12.0%22)
- [2.0.0 candidates](https://github.com/ProGraMajster/ModernFormsNext/issues?q=is%3Aissue%20is%3Aopen%20label%3A%22target%3A%202.0.0%22)
- [2.1.0 candidates](https://github.com/ProGraMajster/ModernFormsNext/issues?q=is%3Aissue%20is%3Aopen%20label%3A%22target%3A%202.1.0%22)
- [2.2.0 candidates](https://github.com/ProGraMajster/ModernFormsNext/issues?q=is%3Aissue%20is%3Aopen%20label%3A%22target%3A%202.2.0%22)
- [Unscheduled / later](https://github.com/ProGraMajster/ModernFormsNext/issues?q=is%3Aissue%20is%3Aopen%20label%3A%22roadmap%3A%20later%22)

## 1.12.0: complete and stabilize

- Prioritize observable regressions: #99 Designer Ctrl+S, #138 DPI title-bar icon, #162 scroll events, #164 ParentChanged, #165 form size constraints, #168 profiler-test allocations, #204/#205 typography, #269/#270 notification transport, #272 Label ellipsis.
- Dependency **#162 → #163**: canonical scroll semantics before scroll-into-view.
- Dependency **#40 → #75**: safe isolation of design-time execution before generalized animation/easing serialization and previews.
- Complete #12 navigation and #13 collection controls. For the remaining collection/utility children keep a sequential implementation order, including #275 before #276.
- Finish the committed Designer, Android, theme, animation, geometry, documentation and release-validation items already in the 1.12.0 milestone. Gate on passing CI, smoke tests and known-limitations review.

## 2.0.0: architecture and showcase

**Rendering and diagnostics:** #46 GPU capability/selection/fallback, #58 runtime profiling, #114 PerformanceLab. Benchmark measured scenarios before claiming acceleration.

**Developer experience:** #43 Hot Reload; #59 accessibility foundations, #61 runtime Inspector and #64 TestHost before external agent bridge #97 (MCP remains an optional adapter).

**New APIs:** #194 style-property precedence **before** #177/#178 selectors; #175/#176 modern binding; #179 templates feeding #191 BindableLayout; #173/#174 cross-cutting composition; #172 optional declarative UI style. Existing imperative API and Visual Studio Designer remain supported.

**Platform/application infrastructure:** #57 data transfer/Drag & Drop, #14 JSON localization, #45 system accent integration, #182 optional app builder/DI, #188 safe custom drawing, #189 FlexLayout and #192 toolkit/core-package policy. #20 is a companion WebView integration: keep external browser dependencies optional and avoid coupling release readiness to unrelated WebView-platform work.

**Demonstrable controls:** #206 → #207 → #208 → #209 → #210 → #211 segmented ProgressBar; #220 ProgressRing and #234 Skeleton; #250/#251 grid features; #254–#257 responsive navigation/toolbars; #277–#280 and #283/#284 new reusable controls.

**Release gates:** stability, performance baselines, migration guide for any intentional incompatibility, supported-platform matrix, accessibility coverage, versioned documentation, real demos and validated release automation. Treat exceptionally large items as deferrable rather than keeping 2.0 permanently unreleasable.

## 2.1.0: broaden application capabilities

- Docking workspace #21 only after 1.12 navigation #12 and 2.0 data transfer #57.
- ImageViewer foundation **#235 → #236**; then #237/#238; decoder **#242 → #243/#244**, with #241 Designer/test/docs after the features it covers.
- Progress-model foundation **#212 → #213**; task views, rate/ETA, orientation, buffer and animations. Animated progress architecture **#223 → #224/#225**.
- Modern reusable controls and enhancements #249, #252/#253, #258/#259, #261–#267, #282, #285–#288, #290–#296. WheelPicker **#285 → #290**, FAB **#287 → #291**.
- #86/#87 editors, #108/#109 tooling/input, #116 in-app notifications and #183–#187 platform/device services.

## 2.2.0: advanced/optional

- Charts and diagrams #16 after drawing surface #188.
- Templated-control architecture #180 after established style/binding/template contracts.
- ImageViewer extras #239/#240 and codecs #245/#247 only after 2.1 viewport/decoder primitives and dependency/security reviews.
- Advanced progress documentation/effects/history #222, #226–#229, #231/#233, and specialized #260/#281/#289/#297.

## Later, without a committed version

- Backend expansion to macOS/Linux #80, optional older .NET multi-targeting #44.
- PDF/document provider architecture #15, specialized image formats #246/#248.
- Full WinForms source-compatibility proposals #143–#147: intentionally deferred; actual native defects can ship earlier on their merits.

## Release planning and GitHub Projects conventions

1. **Labels are the planning target.** A GitHub Milestone is a stricter commitment. Create and populate one only after the proposed scope is reviewed, or keep a candidate label if the feature is speculative.
2. Project #3 (**ModernFormsNext Roadmap**) should group the items by planned version. Preserve each item's existing work status, priority and other custom fields; changing the target release must not reset **Backlog/In Progress/Done**.
3. Check prerequisite chains before promoting an item from a future label into a release milestone. Do not bulk-delete, close, duplicate or relabel closed issues as a side effect of synchronization.
4. On promotion to stable release, re-audit the current default branch and issue state, then update acceptance criteria, examples and release notes accordingly.
