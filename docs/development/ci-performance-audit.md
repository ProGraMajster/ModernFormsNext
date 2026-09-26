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
