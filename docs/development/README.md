# Development reports and audits

[Documentation index](../README.md)

These documents retain the reasoning, implementation plans, and validation evidence for particular
changes. Read each report's date, commit, and limitations: a historical test result is not a
fresh result for the current checkout. For application guidance, use the documentation index.

## Build and repository maintenance

- [CI and release gates](../ci.md)
- [Repository validation inputs](../repository-validation.md)
- [MSBuild instance isolation and qualification](msbuild-build-architecture.md)
- [CI performance audit](ci-performance-audit.md)
- [Versioned documentation artifact workflow](../releasing/versioned-documentation-artifacts.md)
- [Screenshot capture and provenance](../screenshots.md)

## Runtime investigations

- [Rendering backend foundation: issue 46](issue-46-rendering-foundation-report.md)
- [High DPI: issue 120](issue-120-high-dpi.md)
- [Snap/maximize geometry: issue 137](issue-137-snap-maximize.md)
- [Scroll layout consistency: issue 130](scroll-layout-regression.md)
- [Partial redraw regression](partial-redraw-regression.md)
- [Performance diagnostics plan: issue 58](issue-58-performance-diagnostics-plan.md)
- [Text composition plan: issue 62](issue-62-text-input-plan.md)
- [Accessibility Phase 4 plan: issue 59](issue-59-accessibility-phase4-plan.md)
- [Android hardware input plan: issue 109](issue-109-android-hardware-input-plan.md)

## Commands and automation audits

- Commands: [Phase 1](../commands-phase1-audit.md), [Phase 2](../commands-phase2-audit.md),
  [Phase 3](../commands-phase3-audit.md), [Phase 4](../commands-phase4-audit.md),
  [final acceptance](../commands-phase4-acceptance.md)
- [Semantic automation core: Phase 1a](../automation-phase1a-audit.md)

## Release evidence

- [1.11.0 release readiness](1.11.0-release-readiness.md)
- [1.11.0 roadmap completion](1.11.0-roadmap-completion.md)
- [1.10.0 documentation and limitations audit](../audits/1.10.0-documentation-and-limitations-audit.md)

## Historical issue queues

- [Runtime queue: initial audit](codex-audit-series1.md)
- [Designer queue: series 2](codex-audit-series2.md)
- [Series 3: issues 20–28](codex-audit-series3a.md)
- [Series 3: issues 85–87](codex-audit-series3b.md)
- [Autonomous issue development run](codex-autonomous-issue-run.md)

Keep reports at their existing paths so issue and pull-request links remain valid. Add new reports
to the relevant group here, and put user-facing behavior in the corresponding guide rather than
leaving it only in a validation report.
