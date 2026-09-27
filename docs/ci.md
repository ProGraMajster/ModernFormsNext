# Continuous integration and release gates

## Required PR check

Every `pull_request` targeting `master` starts `.github/workflows/dotnet.yml`.
Its single job remains **`build`**, the context required by **Protect master**.
There are no workflow path filters or job-level conditions. Classification happens
inside the job; a light documentation run still executes and succeeds/fails as
`build`. Do not use `[skip ci]` on a PR that needs this required check.

The default checkout is GitHub's PR merge revision, with full history. The classifier
compares the event's exact base SHA with that checked-out merge SHA, including all
commits in the PR. It uses NUL-delimited local Git raw records without rename
detection, so filenames are not shell code, API pagination cannot truncate the
diff, and renaming code into Markdown cannot hide the deletion. Missing history,
empty diffs, unknown paths and unexpected records choose full validation; a checkout
SHA mismatch fails the job. Branch names are never used as artifact provenance.

### Documentation path

Only additions/modifications of ordinary non-executable files qualify:

- root `README.md`, `CHANGELOG.md`, `RELEASING.md`, `AGENTS.md`, `CONTRIBUTING.md`, `SECURITY.md`;
- Markdown (`.md`, case-sensitive) under `docs/`;
- `docs-site/index.md` and `docs-site/api-index.md`.

The job runs the CI classifier regression suite, checks changed Markdown encoding,
nonempty content, conflict markers and required release documentation entry points,
then runs the existing release-documentation script tests. It does not restore or
compile framework code. Full HTML/API/archive/link validation is still a release
gate; the light source check does not claim to replace DocFX validation.

The root README is package content but does not alter compiled code. A text edit
uses the light path; package contents are validated again in the independent
release gate. Deletions, renames, symlinks and executable-mode changes require full
CI because they may affect links, packaging inputs or build behavior.

No blanket `**/*.md` or `docs/**` exemption is used. Markdown in templates, sample
assets, test fixtures, scripts and `.github` requires full CI. So do licenses,
images, DocFX navigation, `.csproj`, `.props`, `.targets`, `.slnx`, SDK/tool/NuGet
configuration and every other unrecognized path. Extend this allowlist only after
auditing source consumers and adding regression cases.

### Full path

Code/build/metadata changes run the same Release solution build and all nine test
projects as before, with `-m:1 /p:UseSharedCompilation=false`. Tests use
`--no-build --no-restore`. No controls, Android targets, VSIX packaging, native tests
or test projects are excluded. CI classification and release-script tests also run.

`actions/setup-dotnet` reads the SDK version from `global.json`. Restore always
executes and evaluates current dependencies. Ordinary PRs omit `--force --no-cache`
and can use NuGet's normal caches on the runner. Release retains that clean-runner
restore. No `obj`, `bin`, assets files or earlier PR binaries are reused.

Persisting `~/.nuget/packages` with an exact SDK/dependency/configuration key was
tested and rejected: saving the 913 MB archive cost **257 s**; a later hit took
**31 s** plus **8 s** restore, saving only **34 s** against the **73 s** cold restore.
That requires about eight later hits to recover the initial saving cost. GitHub
scopes PR-created caches to their merge ref, so an unrelated PR cannot amortize
that expense. The final workflow has **no Actions cache step**. See the
[measured experiment](development/ci-performance-audit.md#persisted-nuget-cache-experiment)
before reintroducing one; do not add a cache based only on its restore-step timing.

Concurrency groups contain the workflow name and PR number, with
`cancel-in-progress: true`. Updating one PR supersedes its stale run; other PRs
and the release workflow have different lifecycles.

## Master and release

There is no ordinary `push -> master` build. This relies on the existing enforced
PR rule, strict required `build` check and absence of bypasses. If those protections
or merge methods change, revisit this policy. The final merge/rebase commit need
not have the same SHA as the checked PR merge revision, and it will not receive a
new push check. Do not describe it as a separately validated exact-SHA artifact.

Tags matching `v*.*.*` continue to run the independent release workflow. It has no
PR cancellation group or NuGet cache action. The workflow checks out the tag,
restores, builds Release, runs all tests, packs and validates NuGet, restores DocFX,
tests documentation scripts, builds and validates versioned bundles, then creates
the GitHub Release and publishes NuGet. The documentation gate verifies the exact
tag/commit before publication. No earlier run's branch-labelled artifacts are used.

Explicit `--no-restore` on release tests documents that the preceding restore/build
is authoritative; `--no-build` already implies it in the .NET CLI. This clarification
is **not** claimed as a measured release speedup. Publication gates are retained.

## MSBuild and tests

Keep `-m:1 /p:UseSharedCompilation=false` in CI and release. The
[architecture follow-up](development/msbuild-build-architecture.md) repairs the
MicroCom dependency graph, moves generation into per-instance intermediates,
isolates the complete VSIX host publication graph and normalizes Android packaging
properties at project-reference boundaries. Six local `-m:4` rebuilds passed their
output-isolation checks, with lower median build times in both configurations.

Qualification is still incomplete: one final Release test run failed an allocation
assertion. Two preliminary test runs and one baseline test run also had sporadic
failures. Native input tests ran on an actively used desktop; the allocation-test
failures remain unexplained.
Those results are retained, not replaced by retries. Test-process separation does
not isolate shared Windows desktop resources. Repeat qualification on an idle,
controlled desktop and investigate the allocation failure before enabling parallel
builds in CI; release needs its own evidence. No workflow parallelism is enabled.

## Reproducing measurements

Use GitHub's jobs API step timestamps for hosted comparisons, keeping runner setup
and cache download/save overhead in the total. For a local profile:

```powershell
dotnet restore ModernFormsNext.slnx --force --no-cache
dotnet build ModernFormsNext.slnx --configuration Release --no-restore -t:Rebuild -m:1 /p:UseSharedCompilation=false '-bl:artifacts/ci-audit/release.binlog;ProjectImports=None'
dotnet test ModernFormsNext.slnx --configuration Release --no-build --no-restore -m:1 /p:UseSharedCompilation=false --logger 'trx;LogFilePrefix=ci'
./scripts/tests/Test-CiChanges.ps1
./scripts/tests/Test-ReleaseDocumentation.ps1
```

Do not mix local warm-cache measurements with hosted cold builds to calculate an
improvement. A release benchmark must be a local dry-run unless actual publication
is explicitly intended; pushing a benchmark version tag would publish packages.

## References

- [GitHub required-check behavior](https://docs.github.com/en/pull-requests/how-tos/merge-pull-request/fix-blocked-merges/troubleshooting-required-status-checks)
- [GitHub strict branch checks](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
- [GitHub concurrency](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)
- [GitHub cache keys and scope](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching)
- [NuGet cache behavior](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders)

## Post-merge qualification, 26-27 September 2026

**Keep ordinary CI and release at `-m:1 /p:UseSharedCompilation=false`.**
PR #140 merged first, then #141 was rebased onto the new master and merged only
after a fresh required `build` passed. The repaired graph is on master at
`e20f7c4f7e5300f39574d86cb7ca4209fdd22622`.

A separate fixed matrix on that commit completed Debug/Release three times at
`-m:1` and five times at `-m:4`. All sixteen builds had zero warnings/errors and
zero reported shared writers; all sixteen test invocations eventually reported
3625/3625 and zero skipped. The required package/assembly/PDB/interop/VSIX payload
hashes agree across repeats and node counts.

**This attempt is not a completed stable qualification.** The fifth parallel
Debug test run crossed approximately 18 hours of host hibernation, verified by
Windows power events and TRX. Its eventual pass is retained as an interrupted
observation. The fifth parallel Release ran after the next-day resume. An earlier
local post-rebase Release run also failed native calendar UIA discovery on the
actively used desktop; the fresh hosted check passed, but does not erase that
local failure. No replacement run, retry, skipped collection or weakened assertion
was used.

Observed build medians were Debug 93.343 → 61.924 seconds (33.66% reduction) and
Release 131.843 → 94.329 seconds (28.45%). These local timings include every build,
including the slower and post-resume observations; they do not establish production
readiness. Allocation failures were not reproduced in this matrix or 800 isolated
calls, so their historical cause remains unresolved. A supplemental Android hash
variation was reproduced at `-m:1` and traced to changing resource-designer
reference metadata; Android reproducibility remains a separate limitation.

See the [complete follow-up](development/msbuild-build-architecture.md#post-merge-qualification-26-27-september-2026)
for all runs, min/max/mean values, diagnostics and evidence boundaries. A future
CI change needs a separately recorded, uninterrupted qualification on a controlled
Windows desktop. No PR enabling parallelism is created from this attempt; release
and unbounded `-m` remain outside the proposed scope.

## Final bounded parallel qualification

**27 September 2026: NOT QUALIFIED. Ordinary PR CI, tests and release retain
`-m:1 /p:UseSharedCompilation=false`.**

[PR #142](https://github.com/ProGraMajster/ModernFormsNext/pull/142) merged after
its required documentation check passed, with no unresolved review threads and
an up-to-date, mergeable head. The new attempt started from master
`dce9cd7bfbcf29150a30c89aa33956307e8a05c2` on a new qualification branch.
The previous unsafe-graph, repaired-graph and interrupted-attempt evidence above
remains historical evidence; none substitutes for this attempt.

Sequential preflight passed both rebuilds, all 3625 tests in each configuration,
build ownership and regression checks, package validation and strict ApiCompat.
However, **the first BEFORE Debug run failed at 3624/3625, zero skipped**:
`BrushInterpolationCompatibilityTests.PreparedPlanDoesNotAllocatePerIntermediateFrame`
measured **3136 bytes** against its unchanged maximum of **256 bytes**. Its build
used `-m:1`, took 98.926 seconds, and had zero warnings/errors and zero isolation
failures. Tests took 86.768 seconds. The matrix stopped there; no AFTER build ran.

Automatic idle sleep was inhibited with a temporary Windows execution-state
request. No sleep/resume event occurred during validation, the request was cleared
after diagnostics, and power-plan settings were unchanged. The user continued
using the desktop, so this was not an isolated desktop experiment. The observed
allocation failure has not been attributed to desktop activity, tiered JIT or
parallel MSBuild. Native calendar UIA and the other historical allocation test
passed in preflight and in the failed matrix suite.

Bounded diagnostics captured allocation/GC/JIT events in the real xUnit testhost
without changing test bodies, thresholds, collection settings or production
runtime configuration. The failure did not recur under instrumentation; those
passes do not replace the failed qualification. There is no new paired benchmark,
full-matrix determinism result or hosted parallel-build result from this attempt.

See the [final qualification report](development/msbuild-build-architecture.md#final-bounded-parallel-qualification)
for every executed and unexecuted stage, trace limitations and the next diagnostic
step. Only a documentation report is proposed. A CI-enabling PR requires a new
complete qualification after the allocation instability is understood; release
publication remains a separate qualification scope.
