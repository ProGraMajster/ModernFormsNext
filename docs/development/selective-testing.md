# Selective testing

Use the repository's SDK 10 pin, PowerShell 7 on Windows and the tests that cover the
changed behavior. The test projects use VSTest, Microsoft.NET.Test.Sdk
18.3.0, xUnit 2.9.3 and xunit.runner.visualstudio 3.1.5. Projects with the
coverage collector use coverlet.collector 6.0.0. Keep each build, test run
and platform check tied to the source, TFM and configuration it exercises.

## Run from the checkout

```powershell
$test = './scripts/Invoke-SelectiveTests.ps1'

# One named test
& $test -Project ModernFormsNext.Automation.Tests/ModernFormsNext.Automation.Tests.csproj `
    -Filter 'FullyQualifiedName=ModernFormsNext.Automation.Tests.ActionTests.InvokePreservesClickBeforeDelegateCommand'

# One class
& $test -Component Core -Filter 'FullyQualifiedName~FileSystemDialogInitialDirectoryTests'

# One test project
& $test -Project ModernFormsNext.Automation.Tests/ModernFormsNext.Automation.Tests.csproj

# One component. Automation selects the portable and Windows bridge projects.
& $test -Component Automation

# The native Windows backend, restricted to DPI and keyboard tests
& $test -Component Windows -Filter 'FullyQualifiedName~WindowsNativeDpiChangedTests|FullyQualifiedName~WindowsKeyboardInputTests'

# Android backend managed tests on the development machine
& $test -Component Android -Filter 'FullyQualifiedName~AndroidApplicationLifecycleReducerTests'

# Designer source/serialization coverage
& $test -Component Designer -Filter 'FullyQualifiedName~CSharpDesignerRoundTripTests'

# Full local Release suite, when explicitly needed and already built
& $test -Full -Configuration Release
```

The default uses the existing Release binaries with `--no-build --no-restore`.
Use `-Configuration Debug` to select Debug. Add `-Build` when the
binaries are stale and the restore graph still matches. Add `-Restore` when
the graph changed or assets are missing. It also builds. If an input such as
the SDK or a shared build target affects the solution, restore/build the
affected graph once, then select the needed tests. Build and run Windows
native tests on a Windows desktop. For a physical Android run use the
[Android release validation script and runbook](../platforms/android.md).
The Android managed project exercises portable logic and has no device coverage.

Use direct VSTest on other hosts for compatible test projects. The direct
equivalent for a class is:

```powershell
dotnet test ModernFormsNext.Tests/ModernFormsNext.Tests.csproj -c Release --no-build --no-restore `
    --filter 'FullyQualifiedName~FileSystemDialogInitialDirectoryTests'
```

## Choose tests for a change

Review the diff against the current PR target. Use
`git diff --name-status origin/develop/2.0.0` for 2.0.0 work and
`git diff --name-status origin/master` for a stable-line fix. Include
`git ls-files --others --exclude-standard` and check project references
and runtime dependencies. Use this mapping as the starting point.

| Changed area | Start with | Expand when |
| --- | --- | --- |
| Core control, layout, input or style | `Core`, `Testing`, affected classes | Native interaction changes also reach `Windows`, and platform-neutral changes reachable from Android may need the sample or device |
| Windows backend or native resource | `Windows` with the relevant class | Changes to the shared WindowKit contracts also reach Android, automation and the Designer |
| Android mapping, activity or renderer | `Android` and the relevant device scenario | Changes to package assets, SDK or AOT also require isolated package build and launch |
| Designer schema or generated C# | `Designer`, round-trip/serialization classes | Host, IPC or packaging changes also require `Vsix` and the ordinary installed Designer smoke |
| Automation or test host | `Automation` or `Testing` | Contract callers or the owned native bridge change |
| SDK, TFM, shared build inputs, public contracts | Changed consumer/build graph and representative platform checks | Run the wider Release matrix in the required PR checks and the final candidate gates |

There is no automatic affected-test selection. Filename matching misses
transitive dependencies, partial classes, platform branches and native
callers. Use the related projects as a fallback when impact is uncertain.
The existing CI classifier only recognizes the repository's documentation-only
diffs. Release qualification continues to use the complete required suite.
[CI policy](../ci.md)

## Read the evidence

Every run writes `summary.json`, the original TRX, and per-target stdout and
stderr below `artifacts/selective-tests/<run>` by default. A zero-match
filter fails. The summary reports passed tests, test failures, skips
and other reported outcomes. Projects later in a component remain `NotRun`
if the first project fails. Tests outside the requested filter or project
were not run. The summary records that scope and excludes those tests from
the count. A crash or timeout can leave incomplete TRX. Use the process
status and logs as the primary evidence in that case.

`-TimeoutSeconds` bounds each invocation. A private Windows job cleans up
the runner and its descendants on timeout, Ctrl+C or ordinary completion.
`-HangTimeout 2min` additionally asks VSTest for its hang diagnostics and
a mini dump. Give that collector time to finish before the outer limit.
Native tests retain their own deadlines and child cleanup. An interrupted
run or unfinished dump collection can leave partial evidence. Keep only
the run directories needed for diagnosis.

## .NET 11 runner comparison

VSTest stays the default. The bounded experiment compared the same linked
`ActionTests.cs` and `AutomationFixture.cs` on SDK 11.0.100 / CLR 11.0.0.
It used the existing Release outputs of Automation and Testing. Thirty-four
cases passed in each runner. The existing xUnit 2 project cannot become
an MTP project by setting a CLI switch. The experiment used xUnit.net v3
package version 4.0.3 with MTP and TRX reporter 2.5.3. It rejected the
fixture's legacy `CollectionBehavior.DisableTestParallelization` attribute.
The isolated MTP copy omitted that attribute and selected one xUnit test
class, which runs its cases serially. The source tests and CI runner are
unchanged.

| SDK 11 runner | First selected invocation | Subsequent invocations | Cases per invocation |
| --- | ---: | ---: | ---: |
| VSTest / xUnit 2 | 2.340 s | 1.332, 1.290, 1.261 s | 34 |
| MTP 2.5.3 / xUnit 3 | 2.900 s | 1.764, 1.796, 2.032 s | 34 |

These are wall-clock measurements from a single machine with existing
test binaries. They include CLI and adapter startup, discovery and TRX.
No OS cache flush was performed. The experiment does not establish a
speedup or equivalence for large native Windows or Designer suites.
[Microsoft MTP CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp) ·
[xUnit MTP integration](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)

### Reproduce the isolated experiment

1. Build `ModernFormsNext.Automation.Tests` in Release with the repository
   SDK. Keep its Automation and Testing dependencies built.
2. Create two scratch directories outside the checkout or under
   `artifacts` with empty local `Directory.Build.props` and
   `Directory.Build.targets`. Select SDK 11.0.100 in a scratch
   `global.json`. Copy `ActionTests.cs` and `AutomationFixture.cs` from
   `ModernFormsNext.Automation.Tests` into each project.
3. Create SDK-style `Comparison.csproj` files targeting `net11.0` with
   nullable and implicit usings enabled. Add absolute project references to
   `ModernFormsNext.Automation.csproj` and `ModernFormsNext.Testing.csproj`.
   For VSTest set `IsTestProject` and use the three VSTest/xUnit package
   references from the existing test project. For MTP add `OutputType=Exe`,
   `UseMicrosoftTestingPlatformRunner=true`, `xunit.v3` 4.0.3,
   `Microsoft.Testing.Platform` 2.5.3 and
   `Microsoft.Testing.Extensions.TrxReport` 2.5.3. Remove the old assembly
   parallelization attribute only from the MTP copy.
4. Run `dotnet restore` and
   `dotnet build -c Release --no-restore -m:1 -p:BuildProjectReferences=false -p:UseSharedCompilation=false`
   in each scratch project. Measure the following commands in their
   respective directories. Use a different results directory for each run.

```powershell
$env:DOTNET_TEST_RUNNER = 'VSTest'
dotnet test Comparison.csproj -c Release --no-build --no-restore `
    --filter 'FullyQualifiedName~ModernFormsNext.Automation.Tests.ActionTests' `
    --results-directory results-vstest --logger 'trx;LogFileName=result.trx'

$env:DOTNET_TEST_RUNNER = 'Microsoft.Testing.Platform'
dotnet test --project Comparison.csproj -c Release --no-build --no-restore `
    --timeout 30s --minimum-expected-tests 1 --no-progress `
    --results-directory results-mtp --results-directory-layout per-module `
    -- --filter-class ModernFormsNext.Automation.Tests.ActionTests --report-trx
Remove-Item Env:DOTNET_TEST_RUNNER
```

SDK 11 exposed `--timeout`, `--maximum-failed-tests`, progress controls,
and the per-module layout in MTP mode. The one-test filter uses
`--filter-method` after `--`. The failure limit is a run cancellation
threshold. A three-case failing theory completed all three before
cancellation took effect and the runner returned exit 13. Read the TRX
counts and overall run outcome together. A synthetic blocking test with
`--timeout 2s` took about 32 seconds to abort, including cleanup. Use an
outer process deadline for a hard limit. Keep VSTest's own filter and
`--blame-hang` syntax for the repository projects.
