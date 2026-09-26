# CI performance audit — 2026-09-26

## Baseline and protection

Audited source: `5f64cc12ebf9921b3fb8ea078d0f455360b729c0` (then-current `master`).
The working branch is `codex/ci-performance-audit`. No framework versions, public APIs,
test expectations, platform implementations or repository rules are changed.

The live **Protect master** ruleset (ID `19173230`) requires a PR, resolution of review
threads and the GitHub Actions `build` check (integration `15368`), with strict status
checks enabled. It has no bypass actors; the authenticated user cannot bypass it.
Allowed merge methods are merge and rebase. These settings were read, not modified.

The solution contains **38 projects**, including **9 test projects** and three test
helper executables. The baseline Release suite passed **3,625 tests, zero skipped**.
The SDK requested by both workflows and `global.json` is `10.0.401`; the local SDK
also reports `10.0.401`. `global.json` retains its existing `latestFeature` roll-forward.

## Actual GitHub Actions baseline

Seconds below come from the jobs API's step `started_at` / `completed_at`, not log
estimates. Job wall time includes runner setup and post steps. Workflow elapsed time
also includes scheduling and completion overhead and is listed separately. Step
times need not sum exactly to job wall time. This small sample is not a controlled
performance experiment: hosted runner load, SDK preinstallation and network vary.

| Scenario / run | Checkout | SDK setup | Restore | Build | Tests | Job wall | Workflow elapsed |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| [PR #139, code](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/36249917693) | 5 | 2 | 109 | 215 | 104 | 447 | 451 |
| [PR #135, release changes](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/35769818044) | 8 | 53 | 76 | 341 | 178 | 674 | 677 |
| [PR #136, only CHANGELOG.md](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/35771240041) | 9 | 58 | 158 | 349 | 134 | 724 | 727 |
| [Master after #136](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/35772638682) | 6 | 33 | 56 | 336 | 156 | 602 | 607 |
| [Release v1.11.1](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/35773866445) | 6 | 31 | 51 | 339 | 169 | 750 | 755 |

Release-only steps: documentation tool restore **18 s**, pack **5 s**, package
validation **2 s**, documentation script tests **3 s**, documentation build **71 s**,
documentation validation **6 s**, GitHub Release creation **15 s**, NuGet login
**7 s**, NuGet publication **14 s**. `.NET Info` costs 3–6 s in this sample.

The master and release runs both used **`dc55839b061485121a8e5dd8092ea2a79e4d7572`**.
The PR run used the PR integration revision (its reported head was `675249d...`);
it must not be described as the same commit ID as the final merge commit. Strict
checks validate integration with the current base. The duplicate master job adds
602 s after the PR, before the release's independent 750 s publication gate.

Build is the largest step, followed by tests and restore. On the changelog PR,
restore/build/tests consume **641 s** despite no compiled-code changes. PR revisions
also rerun in full without cancellation. Restore always forces reevaluation and
disables HTTP caching even though package dependencies are normally unchanged.

## Workflow history

- `176cd39` (2026-03-28) added `--force --no-cache` and changed the SDK selection.
- `b17a188` (2026-03-29) added `-m:1 /p:UseSharedCompilation=false`.
- `build/MicroCom.targets` is unchanged since `93ad97c` (2026-03-16).
- `f3f1fe5` (2026-08-16) added the tag-bound documentation publication gates.
- `48d1382` (2026-09-19) added the current full regression step.
- `28751ce` (2026-09-20) updated the SDK to `10.0.401`.

## MSBuild and MicroCom investigation

A local forced Release rebuild captured `baseline-release.binlog` with
`-t:Rebuild -m:1 /p:UseSharedCompilation=false` and binary logging. It completed in
**143.43 s**, with zero warnings/errors. The preceding `--force --no-cache` restore
took **2.77 s** using the existing global package directory; that flag does **not**
empty NuGet's global packages folder. Local test wall time was **82.55 s**.
These are warm local-machine measurements, not substitutes for hosted-runner times.

The binary log was replayed using the installed SDK's `BinaryLogReplayEventSource`.
Actual `Csc` task executions, project global-property sets and target durations
were examined, rather than counting every project query as a compilation.

### Confirmed shared writers

1. Both MicroCom tools are solution projects **and** `ProjectReference`s in
   `build/MicroCom.targets`. Each tool compiles twice in the forced rebuild.
   The solution instance has no global `TargetFramework`; the reference forces
   `TargetFramework=net10.0` and suppresses normal framework discovery.
2. MSBuild identifies project instances by path and global properties. The two
   instances nonetheless both evaluate to `obj/Release/net10.0/` and
   `bin/Release/net10.0/`. This was verified with `-getProperty` for both property
   sets. Different instances can therefore write the same compiler outputs.
3. The VSIX target `PublishModernFormsNextDesignerHost` invokes `Publish` with
   `RuntimeIdentifier=win-x64`, `SelfContained=false`, `EnableWindowsTargeting=true`
   and a shared `PublishDir`. The binlog shows a third MicroCom property set with
   `PublishDir` and `EnableWindowsTargeting`; those properties do not isolate its
   `obj`/`bin`. They happen not to recompile the tools during this sequential run.
4. Windows interop generation writes the tracked
   `Generated/Windows.Interop.Generated.cs`, shared by configuration/property
   variants. The target executed twice (0.73 s and 0.70 s) in the rebuild.
5. The generator source glob includes `**/*.cs`, including generated tool `obj`
   sources. Rebuilds can invalidate generation through assembly-info timestamps.
   Generator binary/dependency changes are not explicit target inputs. The
   `UpToDateCheckInput` list has the same broad glob, but no corresponding generated
   `UpToDateCheckOutput`. Visual Studio FastUpToDateCheck was inspected statically;
   no manual Visual Studio claim is made.
6. The patcher's `ProjectReference` still builds it although `PatchMicroComAssembly`
   is commented out. No patching execution appears in the binary log.

The installed SDK's `Microsoft.Common.CurrentVersion.targets` explicitly undefines
`TargetFramework` for single-target references during normal framework discovery to
avoid separate evaluation. Removing the forced metadata would address one instance
mismatch, but would not isolate the VSIX property variants or generated interop.
Similarly, changing only `IntermediateOutputPath` leaves shared tool `bin` and
the hard-coded generator executable path unresolved. A safe parallel-build change
needs the whole tool/host/output contract handled together, including standalone
backend builds, design-time builds, both configurations and generated-source inputs.

**Decision:** retain `-m:1` and disabled shared compilation. No unsafe parallel
build was attempted, and there is no claim that the output races are fixed.
Concurrent independent builds in the same checkout remain unsupported. Repeated
successful sequential builds are validation, not proof of parallel safety.

### Where the local build spends time

The long serialized chain includes the two Android sample build graphs: assembly
resolution/trimming, per-ABI IL linking, APK assembly and AOT. Largest observed
targets include `_ResolveAssemblies` (21.59 s and 15.91 s), `_RunILLink` (10.34 s,
9.97 s, 7.63 s, 7.36 s), `_BuildApkEmbed` (7.57 s and 5.67 s) and
`_RunAotForAllRIDs` (4.64 s and 3.75 s). Targets nest; do not add these as exclusive
CPU time. This is a serialized elapsed-time profile, not a projected parallel DAG.

MicroCom compiler work totals only **3.67 s**, including duplicates. VSIX host
publication takes **3.19 s** and container creation **5.36 s** locally. Compiling
the framework twice is expected for `net10.0` and `net10.0-windows`; Android ABI
variants and the RID-specific designer host also have legitimate distinct builds.
Removing Android, VSIX, generation, packing or validation to save time was rejected.

## Test isolation investigation

| Test project | Baseline tests | Isolation considerations |
| --- | ---: | --- |
| Automation.Tests | 160 | Assembly parallelism disabled; one process-global TestHost. |
| Automation.Windows.Tests | 66 | Assembly parallelism disabled; child processes and live Windows automation. |
| CrossPlatform.Sample.Tests | 29 | Shared preference collection; sample/framework services. |
| Designer.Tests | 669 | Native HWND host processes, recovery files, diagnostic paths and persistence. |
| Testing.Tests | 532 | Assembly parallelism disabled; deterministic process-global TestHost. |
| ModernFormsNext.Tests | 1,464 | Serialized clipboard, input, animation, font/performance and semantic collections. |
| VisualStudioExtension.Vsix.Tests | 26 | net472, editor/host contracts, temporary files and native hosting. |
| WindowKit.Backend.Android.Tests | 358 | Serialized accessibility/text-input collections; deterministic mapping tests. |
| WindowKit.Backend.Windows.Tests | 321 | Serialized Windows UI services; separate UIA host/client processes and HWNDs. |

Separate processes isolate managed statics but not the desktop clipboard, foreground
activation, cursor, system UI Automation or shared diagnostic locations. Unique
GUID/PID temp paths exist in many tests but are not a blanket guarantee. The
largest local costs include live automation (~27 s) and framework tests (~21 s).
Running the native suites together on one desktop was rejected without a proven
desktop/resource partition. Splitting jobs would require rebuilding or transporting
the full test/helper/VSIX outputs and adds runner setup. No test attributes, skips,
timeouts or retry loops are changed. The full solution suite remains sequential.

## Measurement artifacts

Raw jobs API responses, ruleset snapshot, local logs, binary log, replay analysis
and TRX files are under ignored `artifacts/ci-audit/`. They are local diagnostics,
not release assets or committed binaries. This report contains the reviewable
results and canonical Actions links. The operational contract and final comparison
are documented in [CI policy](../ci.md).

## Validation after implementation

At implementation commit `e855f70a09fa407f366b4e6c6ebc5385639dfce3`:

- Debug and Release builds: zero warnings/errors; VSIX creation and its existing
  validation target passed in both configurations.
- Debug **3,625/3,625**, Release **3,625/3,625**, nine TRX files per configuration,
  zero failed and zero skipped tests. No test implementation or attribute changed.
- CI classification: **45 assertions** using real Git fixtures, including mixed
  changes, code-to-Markdown and docs renames, deleted inputs, executable/symlink
  modes, spaces/Unicode/literal shell-like filenames, more than 300 files, empty
  diffs, missing history and malformed documentation. The actual historical
  `ab54cac... -> dc55839...` diff correctly selects only `CHANGELOG.md`; the current
  branch correctly selects full CI.
- Release documentation scripts: **32 assertions**.
- Package validation: **11 NuGet packages and 10 symbol packages**, still version
  `1.11.1`. No package was published.
- DocFX **2.78.5** restored; all **four** versioned documentation archives built and
  validated with expected local tag and exact commit metadata.
- **actionlint 1.7.12**, PowerShell syntax parsing and `git diff --check`: passed.
- Protect master was reread and compared with the baseline snapshot: unchanged.
- No visible framework behavior or startup changed. Manual Visual Studio/device/UI
  and generated-template launches were not required and were not performed.

Local profile (seconds; warm package cache, same machine, forced Release rebuild):

| Stage | Before | After | Change |
| --- | ---: | ---: | ---: |
| Restore | 2.77 | 1.81 | 34.5% less |
| Release rebuild | 143.43 | 146.53 | 2.2% more |
| Release tests | 82.55 | 82.81 | 0.3% more |
| Sum of those three commands | 228.74 | 231.15 | 1.1% more |

The new classification/script gates add time to full CI; no compiler or test-runner
speedup is claimed. Debug build/test took 66.04 / 78.94 s. Release pack took 3.01 s;
documentation build/validation took 100.01 / 9.99 s. These are distinct from hosted
release timings and were run without publishing or creating a version tag.

The local replay of the actual PR #136 diff plus the complete new lightweight gates
took **10.82 s** (classification 0.18 s, classifier regression tests 10.37 s,
changed-document checks 0.01 s, existing release-script tests for the remainder).
Restore, framework build and framework tests are all **0 s** on that path. This
does not include hosted checkout/startup and is not a hosted end-to-end benchmark.

Hosted image drift also matters: #136 ran `windows-2025-vs2026` image
`20260907.229.1`; #139 ran `20260922.246.2`. Even two baseline jobs on September 26
vary: the PR #139 build/test steps took 215/104 s, while its successful
[master run](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/36250490355)
took 338/154 s (restore 64 s, total job 578 s). Do not attribute this existing
runner variance to the workflow changes.

## Persisted NuGet cache experiment

The initial implementation was benchmarked on
[PR #140, attempt 1](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/36252204602/attempts/1).
The whole job passed in **870 s**: checkout 6 s, SDK setup 3 s, classification 1 s,
classifier tests 13 s, restore **73 s**, build **349 s**, framework tests **153 s**.
All **3,625** framework tests passed with zero skipped. Release-script tests also
passed. The first cache lookup took 1 s and its post-job save took **257 s**.

The cache stored downloaded/expanded NuGet packages only, never `bin`/`obj` or
binaries from this repository. Its exact key included OS, architecture, global.json,
all projects/solutions/props/targets, NuGet configuration, lock/config files and the
tool manifest, with no fallback key. The compressed archive was **913,227,745 bytes**.
The log shows GNU tar with `zstd -T0`; archive creation dominated, while the transfer
reported 266.6 MB/s. This was a measured serialization cost, not a speculative
network explanation.

Attempt 2 of the same run and source revision hit that exact cache. Cache download
and extraction took **31 s**, and restore still ran successfully in **8 s**. The
combined dependency preparation cost was therefore **39 s**, against **73 s**
without a hit: **46.6% less**, but only **34 s** saved per later hit. Paying 257 s
upfront needs approximately eight later hits within the **same PR** to break even.
The audited examples have far fewer revisions; #136 had only a single docs change.

**Decision:** remove the persisted cache from the final workflow, retain ordinary
NuGet caching within a runner, and keep release's fresh-runner/no-cache restore.
The final PR was updated while attempt 2 was active to exercise cancellation of a
stale PR revision. The partial warm-cache experiment is not reported as a completed
full test run. It demonstrates why caching was not accepted solely on a green first
run or the shorter restore step.
