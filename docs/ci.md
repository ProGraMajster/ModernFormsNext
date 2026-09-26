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

Keep sequential builds. The [audit](development/ci-performance-audit.md) identifies
different MicroCom global-property instances writing identical `obj`/`bin`, shared
generated interop and VSIX publish variants. Changing only `-m` or a single output
property does not solve the whole graph. Likewise, test-process separation does
not isolate shared Windows desktop resources. No new parallelism is enabled.

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
