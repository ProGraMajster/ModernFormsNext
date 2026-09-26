# MSBuild instance isolation

This follow-up starts at `71a1b8b184ab512470ca98f3bd5d3e4263ba2bef`, on top of
[PR #140](https://github.com/ProGraMajster/ModernFormsNext/pull/140). At the start of
the work, that PR was open and `master` was `5f64cc1`. Its CI classification,
required `build` context, concurrency and cache decision are preserved.
The [original CI audit](ci-performance-audit.md) records the earlier unsafe graph;
it remains historical evidence, not a description of the repaired graph.

## Root causes confirmed with new binary logs

The baseline used three complete Debug and three complete Release rebuilds, each
with `-m:1 /p:UseSharedCompilation=false`. The SDK is `10.0.401`.
The logs include evaluation properties, project global properties and task events.

- The solution and `MicroCom.targets` requested both MicroCom tools with different
  global properties: no explicit `TargetFramework` versus `TargetFramework=net10.0`.
  Both requests wrote `obj/<Configuration>/net10.0` and `bin/<Configuration>/net10.0`.
- VSIX publication added `PublishDir`, `EnableWindowsTargeting=true`,
  `RuntimeIdentifier=win-x64`, `SelfContained=false` and an explicit host framework.
  The host's RID directory was distinct, but its libraries and tools reused normal
  outputs. Querying evaluated properties matters: RID isolation of the executable
  alone did not isolate its dependency graph.
- `DesignerHostPublishDir` was assigned before the SDK initialized
  `IntermediateOutputPath`. Its actual value was `DesignerHost/`, shared by Debug
  and Release in the VSIX source directory.
- Both Windows backend instances generated the same tracked interop file.
  Generator input globs included generated `obj` C# files. The generator also
  backdated its output to the IDL timestamp, making newer tool inputs invalidate
  generation on every build.
- The main framework's explicit XML documentation path bypassed the normal
  output-path selection. Changing only intermediate directories would miss it.
- Android SDK `36.1.69` passed application packaging properties (`_Outer*`,
  `_ComputeFilesToPublishForRuntimeIdentifiers`, `SkipCompilerExecution`, etc.)
  into referenced libraries. Those additional library requests skipped compiler
  execution but still ran bookkeeping tasks in shared directories. They must not
  be confused with additional real compiler executions.
- NuGet's pack-on-build queries also use extra property sets. Their output-group
  and reference queries are ordered after the owning build. They remain visible
  in the report; the timing gate checks their potential cache writers as well.

The patcher's target has been commented out since its first repository commit
(`93ad97c`). Repository-wide usage and history searches found no active invocation.
The patcher project remains in the repository and solution, while the unnecessary
backend dependency is removed. No patching behavior has been disabled.

## Output ownership

Normal framework, sample and test outputs retain their existing locations. The
VSIX publication request sets `ModernFormsNextBuildVariant=vsix-host`. This property
flows to its dependencies; `build/BuildVariants.props` establishes their paths
before SDK defaults and NuGet imports are evaluated.

| Evaluated property | Ordinary build | VSIX host publication graph |
| --- | --- | --- |
| `BaseIntermediateOutputPath` | `obj/` | `obj/vsix-host/` |
| `BaseOutputPath` | `bin/` | `bin/vsix-host/` |
| `IntermediateOutputPath` | `obj/<Configuration>/<TFM>/[RID/]` | `obj/vsix-host/<Configuration>/<TFM>/[RID/]` |
| `OutputPath` | `bin/<Configuration>/<TFM>/[RID/]` | `bin/vsix-host/<Configuration>/<TFM>/[RID/]` |
| `TargetPath` | assembly under ordinary `OutputPath` | assembly under variant `OutputPath` |
| `MSBuildProjectExtensionsPath` / `ProjectAssetsFile` | `obj/` / `obj/project.assets.json` | same restored inputs, read during build |

The SDK supplies configuration, framework and RID suffixes. Ordinary libraries
remain RID agnostic; the published host uses `net10.0-windows/win-x64`. Variant
package outputs and XML documentation follow the same paths. Default source globs
exclude **both** ordinary and variant `obj/bin` trees.

Restore runs before compilation. The variant consumes the same dependency graph
and existing assets; `VisualStudioDesignerHost.RuntimeIdentifiers` already requests
`win-x64` during normal restore. It does not perform a nested concurrent restore.
Running independent restore/build/clean commands simultaneously in one checkout
is outside this contract. Use separate checkouts for independent build processes.

VSIX staging is now `obj/<Configuration>/net472/DesignerHost/`, evaluated after SDK
imports. The publish and clean targets share the exact property string.
`dotnet clean ModernFormsNext.slnx -c Release` traverses the publication variant
and removes its recorded generated outputs and staging directory, retaining shared
restore inputs. Clean is configuration-specific, as with the standard SDK.
The old ignored source-tree `DesignerHost/` directory is no longer consumed;
existing copies from older checkouts are not automatically deleted.

The MicroCom generator stays in the solution. Its backend `ProjectReference` uses
normal framework negotiation instead of forcing a framework global property.
`OutputItemType` returns its actual assembly path, including a publication variant.
There is no hard-coded generator `bin/Debug/net10.0` executable path.

`build/BuildGraphBoundary.targets` removes Android application packaging state at
the `ProjectReference` boundary, only during the SDK's per-RID packaging phase.
It explicitly waits for `ResolvePackageDependenciesForBuild` before updating
references, so SDK-added transitive references receive the same metadata as direct
references. Merely updating items during evaluation, or using an unordered
`BeforeTargets` hook, misses those late additions. The already-built library
instance is reused. Normal framework/RID negotiation,
APK creation, trimming and AOT remain enabled. Real IDE design-time properties are
not stripped from ordinary builds. This adapter is tied to the inspected Android
SDK contract and must be re-audited after SDK/workload upgrades.

## Generated interop and IDE behavior

Every real Windows backend build compiles
`$(IntermediateOutputPath)/MicroCom/Windows.Interop.Generated.cs`. Thus normal and
publication builds, configurations and RID variants never generate into the same
source file. The target tracks the IDL, real generator sources (excluding `obj/bin`),
the generator project/targets, and its resolved DLL/JSON runtime payload.
Outputs keep their actual generation time. An unchanged incremental build skips
generation; a changed source or runtime input regenerates it.

The checked-in `Generated/Windows.Interop.Generated.cs` is a **read-only IDE
snapshot** during ordinary builds. Before the first build, design-time evaluation
uses this snapshot without building or running a tool. Once the per-instance file
exists, design-time compilation uses that file. Normal builds always use freshly
validated target inputs and never compile both sources.

Visual Studio receives explicit up-to-date inputs and outputs. The
`CollectMicroComDesignTimeInputs` hook resolves the tool with `GetTargetPath`, a
query that does not build it, and supplies its DLL/JSON payload to CPS's input
collection. This also covers a binary change without a generator source edit.

After intentionally changing the IDL or generator, refresh the reviewable snapshot:

```powershell
dotnet msbuild ModernFormsNext.WindowKit.Backend.Windows/ModernFormsNext.WindowKit.Backend.Windows.csproj -t:UpdateMicroComInteropSnapshot -p:Configuration=Debug
```

Review and commit the snapshot together with its source change. This explicit
maintainer target is the only build target that writes the snapshot. Do not run
it concurrently with an IDE snapshot read. The regression fixture compares generated
bytes with the snapshot and checks invalidation, clean and design-time behavior.

Generated files are listed in `FileWrites` for normal clean and `EmbeddedFiles`
for portable PDBs. An `obj` file has no repository URL: embedding its exact source
keeps symbol packages and debugging usable without inventing a Source Link URL.
Tracked sources continue to use the existing Source Link configuration.

## Auditing and regression checks

`scripts/BuildGraphAudit` is a standalone validation utility, not a solution/test
project or a framework dependency. It references the installed SDK's MSBuild reader
and has no package dependencies. It replays the log without loading project code,
exports all evaluated path/global-property sets, and fails on:

- distinct build owners sharing intermediate, binary, assembly or XML paths,
  including incremental builds that do not execute a compiler;
- overlapping potential write/cache tasks from distinct instances in the same
  intermediate directory, including SDK packaging queries;
- missing build path evidence or a log containing no real compilation (unless
  explicitly auditing an incremental build with `--allow-incremental`).

Android's `SkipCompilerExecution=true` queries are counted separately from actual
compilers. A directory check is deliberately conservative: a task might skip an
unchanged file. The utility is an audit aid for this graph, not an operating-system
file-I/O tracer or proof about arbitrary custom `Exec` commands. Generated-source,
publish/staging and package checks complement it.

```powershell
dotnet build scripts/BuildGraphAudit/BuildGraphAudit.csproj -c Release
dotnet scripts/BuildGraphAudit/bin/Release/net10.0/BuildGraphAudit.dll artifacts/build.binlog artifacts/build-report.json
./scripts/tests/Test-BuildGraphAudit.ps1 -AuditTool scripts/BuildGraphAudit/bin/Release/net10.0/BuildGraphAudit.dll
./scripts/tests/Test-BuildGraphBoundary.ps1
./scripts/tests/Test-MicroComBuild.ps1
```

The detector's fixtures deliberately create duplicate outputs and a documentation
file escaping otherwise isolated directories, including a collision without a
compiler execution; none can silently pass. The boundary fixture checks both direct
and SDK-added transitive references and preserves ordinary IDE properties. Fixtures
use temporary owned directories and do not modify real generator sources.

## Measurement and validation

### Preliminary implementation (superseded)

The first architecture iteration passed six rebuild timing gates, but its subsequent
incremental Release log exposed overlapping Android transitive-library writers in
WindowKit. WindowKit.Backend also retained multiple owners. This iteration is **not
qualified**. The final boundary target explicitly orders NuGet reference discovery
before normalization. The audit was strengthened to reject duplicate full-build
owners even without Csc or an observed timing overlap. It rejects the earlier
incremental log with eight duplicate-path findings and three overlapping operations.
The following preliminary results are retained so failed experiments remain visible;
the final implementation is measured separately below.

Raw binlogs, JSON reports, TRX files and benchmark logs are retained locally under
ignored `artifacts/msbuild-audit/`. They are not source or release assets.
Measurements use one Windows machine (Core i9-14900K, 24 cores/32 logical CPUs),
the same SDK, warm restored packages, alternating Debug/Release full rebuilds and
sequential full-solution tests. No sample, test project, packaging or AOT step is
removed. BEFORE is the unmodified starting revision; AFTER is the architecture
patch on that revision. Shared compilation stays disabled in both. Restore is
measured separately on every iteration; tests use `--no-build --no-restore -m:1`.
Total is restore + build + tests (it excludes subsequent binlog/hash analysis).
The user confirmed using the same desktop during the native tests. These local
warm-cache numbers are not predictions for hosted runner performance.

| Variant | Configuration | Run | Restore s | Rebuild s | Tests s | Total s | Tests passed / total |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| BEFORE `-m:1` | Debug | 1 | 2.154 | 86.781 | 73.551 | 162.486 | 3624 / 3625 |
| BEFORE `-m:1` | Debug | 2 | 1.535 | 85.527 | 68.925 | 155.987 | 3625 / 3625 |
| BEFORE `-m:1` | Debug | 3 | 1.330 | 85.687 | 68.716 | 155.733 | 3625 / 3625 |
| BEFORE `-m:1` | Release | 1 | 1.588 | 129.366 | 68.073 | 199.027 | 3625 / 3625 |
| BEFORE `-m:1` | Release | 2 | 1.379 | 139.454 | 82.771 | 223.604 | 3625 / 3625 |
| BEFORE `-m:1` | Release | 3 | 1.249 | 134.905 | 75.022 | 211.176 | 3625 / 3625 |
| AFTER `-m:4` | Debug | 1 | 1.330 | 62.123 | 70.970 | 134.423 | 3625 / 3625 |
| AFTER `-m:4` | Debug | 2 | 1.341 | 56.733 | 81.213 | 139.287 | 3624 / 3625 |
| AFTER `-m:4` | Debug | 3 | 1.327 | 59.308 | 68.066 | 128.701 | 3625 / 3625 |
| AFTER `-m:4` | Release | 1 | 1.217 | 85.308 | 70.740 | 157.265 | 3624 / 3625 |
| AFTER `-m:4` | Release | 2 | 1.230 | 79.684 | 68.859 | 149.773 | 3625 / 3625 |
| AFTER `-m:4` | Release | 3 | 1.178 | 73.676 | 71.816 | 146.670 | 3625 / 3625 |

| Configuration | BEFORE build median (min–max), s | AFTER build median (min–max), s | Median reduction |
| --- | ---: | ---: | ---: |
| Debug | 85.687 (85.527–86.781) | 59.308 (56.733–62.123) | 30.8% |
| Release | 134.905 (129.366–139.454) | 79.684 (73.676–85.308) | 40.9% |

All twelve builds succeeded with zero warnings/errors. All test runs included all
nine projects, 3,625 tests and zero skipped. Failures are retained in the table:

- BEFORE Debug 1: `WindowsUiaProviderTests.RealHwndGridAndCalendarExposeLiveTableEditSelectionAndPopupLifetime`
  could not discover the native calendar popup. A separately labelled diagnostic
  invocation passed; it does not replace the failed measurement.
- AFTER Debug 2: `WindowsKeyboardInputTests.UnicodePacketDoesNotInheritPreviousKeyConsumption`
  expected empty text but observed `x`. Active desktop use is a plausible input/focus
  interference source, not a proven diagnosis.
- AFTER Release 1: `PlatformPerformanceTransportTests.DisabledIngressDoesNotAllocateOrRetainAFrame`
  measured 7,216 allocated bytes instead of zero. The WindowKit and shared backend
  assemblies were byte-identical to the baseline. This narrows the investigation;
  it does not establish a cause or turn the failure into a pass.

Each of the six preliminary AFTER rebuild logs contained 51 actual compiler
invocations and passed the initial compiler/timing gate. That was insufficient:
the strengthened build-owner gate also detects the transitive Android aliases
described above. The generator and dormant patcher no longer compile twice
into the same directory. All twelve packable assembly payloads were byte-identical
across the three AFTER Release rebuilds. Both generated backend variants in every
configuration matched the unchanged tracked snapshot, SHA-256
`6BC11F7731A0AD45B24C11BDDFA59418284A1EF79313019E31BBF08A5B42DF5E`.

### Final implementation

After fixing the transitive-reference ordering, the complete measurement matrix
was run again with the same commands and all nine test projects. This is the final
AFTER comparison against the six BEFORE measurements above; it does not select the
fastest preliminary trials. All six final rebuilds had zero warnings/errors and
passed the stronger output-owner gate (51 compilations, 52 build owners, no aliases
or overlapping writers). Debug reports contain 1,211 project invocations; Release
reports contain 1,215. The twelve packable DLLs are byte-identical across the three
final Release rebuilds. Both generated variants retain the snapshot hash above.

| Final AFTER `-m:4` | Run | Restore s | Rebuild s | Tests s | Total s | Tests passed / total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Debug | 1 | 1.256 | 68.611 | 69.089 | 138.956 | 3625 / 3625 |
| Debug | 2 | 1.293 | 63.049 | 85.171 | 149.513 | 3625 / 3625 |
| Debug | 3 | 1.151 | 56.133 | 69.107 | 126.391 | 3625 / 3625 |
| Release | 1 | 1.193 | 84.949 | 73.631 | 159.773 | 3624 / 3625 |
| Release | 2 | 1.214 | 77.302 | 68.735 | 147.251 | 3625 / 3625 |
| Release | 3 | 1.289 | 99.667 | 84.829 | 185.785 | 3625 / 3625 |

| Configuration | BEFORE build median (min–max), s | Final AFTER median (min–max), s | Median reduction |
| --- | ---: | ---: | ---: |
| Debug | 85.687 (85.527–86.781) | 63.049 (56.133–68.611) | 26.4% |
| Release | 134.905 (129.366–139.454) | 84.949 (77.302–99.667) | 37.0% |

Final Release 1 failed
`BrushInterpolationCompatibilityTests.PreparedPlanDoesNotAllocatePerIntermediateFrame`:
7,320 bytes allocated versus the allowed 0–256. Neither that test nor interpolation
runtime code changed. The later prescribed runs passed; they do not replace this
failure. Allocation-test instability still requires investigation on a controlled
host. There were no skipped tests in any final run.

The last Debug and Release runs both passed the following complete suites:

| Test project | Debug passed / total | Release passed / total |
| --- | ---: | ---: |
| ModernFormsNext.Tests | 1464 / 1464 | 1464 / 1464 |
| ModernFormsNext.Designer.Tests | 669 / 669 | 669 / 669 |
| ModernFormsNext.Testing.Tests | 532 / 532 | 532 / 532 |
| ModernFormsNext.WindowKit.Backend.Android.Tests | 358 / 358 | 358 / 358 |
| ModernFormsNext.WindowKit.Backend.Windows.Tests | 321 / 321 | 321 / 321 |
| ModernFormsNext.Automation.Tests | 160 / 160 | 160 / 160 |
| ModernFormsNext.Automation.Windows.Tests | 66 / 66 | 66 / 66 |
| ModernFormsNext.CrossPlatform.Sample.Tests | 29 / 29 | 29 / 29 |
| ModernFormsNext.VisualStudioExtension.Vsix.Tests | 26 / 26 | 26 / 26 |
| **Total** | **3625 / 3625** | **3625 / 3625** |

### Additional validation of the final graph

All commands below ran sequentially as separate invocations, with shared compilation
disabled. The bounded build experiments used `-m:4`; tests and explicit package
collection used `-m:1`. These checks are additional to the measured matrix.

| Check | Result |
| --- | --- |
| Release incremental build, twice | PASS; 13.539 s / 10.022 s, zero isolation failures. The first regenerated the variant interop after the SDK refreshed the generator's runtime JSON inputs; the second ran zero compilers and zero MicroCom generation targets. |
| Debug solution clean | PASS; ordinary/variant interop and VSIX staging removed; tracked snapshot unchanged. |
| Standalone Windows backend after clean | PASS; automatically built the tool and generated interop, 6.931 s. |
| Full Debug rebuild after clean | PASS; 73.242 s, zero warnings/errors and isolation failures. |
| Standalone VSIX rebuilds | PASS; Debug 33.243 s / Release 35.860 s; existing manifest/template validator passed. |
| Standalone DesignerHost publish | PASS; Release `win-x64`, framework-dependent, 5.424 s; required executable, managed, native and runtime JSON payload present. |
| VSIX host payload comparison | PASS; all 72 published files in each Debug/Release VSIX match their staging files byte for byte. |
| Visual Studio MSBuild.exe backend rebuild | PASS; VS 2026 Community 18.10.2 / MSBuild 18.10.1, 9.902 s. |
| Actual Visual Studio CPS design-time targets | PASS; `CollectUpToDateCheckInputDesignTime` / `CollectUpToDateCheckOutputDesignTime` return generator/runtime inputs, one generated Compile item and the private output; zero diagnostics. |
| NuGet pack and `Validate-ReleasePackages.ps1` | PASS; 11 nupkg + 10 snupkg, unchanged version 1.11.1; local artifacts only. |
| SDK ApiCompat strict comparison | PASS for all 12 packable assembly/TFM pairs against the baseline, including parameter names and attributes. |
| Embedded generated source | PASS; exact snapshot bytes in ordinary/variant Debug/Release portable PDBs; packaged Release PDB matches the verified file. |
| `Test-MicroComBuild.ps1` | PASS; 24 assertions. |
| `Test-BuildGraphBoundary.ps1` | PASS; direct, late transitive and ordinary IDE scenarios. |
| `Test-BuildGraphAudit.ps1` | PASS; four scenarios, including duplicate writers without Csc. |
| Workflow syntax and diff whitespace | PASS; actionlint 1.7.12 and `git diff --check`. |

The first incremental generation was explained by the binlog: the SDK refreshed
the variant generator's `deps.json` and `runtimeconfig.json` after its assets cache
changed. Those are intentional generator inputs. No `obj/*.cs` file is a MicroCom
source input, and the following unchanged build skipped generation completely.

The SDK reader used by the audit supports binlog format 26. Visual Studio 18.10
emits format 27, which this utility rejects rather than silently accepting incomplete
events. The VS command and CPS results above passed; the VS-format log has **not**
passed the replay gate. All reported isolation gates use compatible `dotnet` logs.
Use a compatible reader when auditing a newer MSBuild version.

Manual Visual Studio navigation/fast-up-to-date UX and Android device/emulator
runtime checks were **NOT EXECUTED**. Automated native tests, command-line MSBuild
and CPS contract checks do not substitute for those observations. No visible UI or
template startup behavior changed, so a manual ControlGallery/template session was
not part of this build-system validation. No release or package was published.

## CI decision and remaining qualification

**Do not remove `-m:1` from either workflow.** `UseSharedCompilation=false` also
remains. Known build writers have been repaired and the local `-m:4` build experiment
passed, but the requested all-green repeated build/test qualification did not.
There is no retry loop, test weakening, skipped native suite or production workflow
change to conceal that limitation.

The minimal next step is an isolated, idle Windows desktop with the same SDK and
workload: investigate the allocation-test failure, rerun the complete Debug/Release
matrix with binlogs and hashes, and observe the actual Visual Studio IDE workflow.
Only after all runs and package checks pass should a separate change consider
`-m:4` in ordinary CI. Qualify release independently before changing publication.
Unbounded `-m` and simultaneous independent commands in one checkout are not
qualified by this experiment.

## References

- [MSBuild race diagnosis](https://learn.microsoft.com/en-us/visualstudio/msbuild/fix-intermittent-build-failures)
- [ProjectReference protocol](https://github.com/dotnet/msbuild/blob/main/documentation/ProjectReference-Protocol.md)
- [Incremental target inputs/outputs](https://learn.microsoft.com/en-us/visualstudio/msbuild/incremental-builds)
- [Visual Studio up-to-date inputs](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md)
- [Portable PDB embedded source](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#embedded-source-c-and-vb-compilers)
