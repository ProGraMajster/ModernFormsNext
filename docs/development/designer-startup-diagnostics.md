# Designer startup diagnostics for #40 and #486

## Supplied Windows 24H2 evidence

The investigation starts from master `762b2b82cfe40b6dea33058fbd1ff5f1a3b78c79`.
The reported guest has Windows 11 Pro 24H2 x64 build 26100.1, Visual Studio
Community 2022 17.14.45, SDK 10.0.404, 16 GB RAM and eight virtual CPUs.
The supplied logs provide no candidate commit, VSIX digest or WER events.
Qualification of that image and an updated 24H2 installation requires separate results.

All four input files were read. The ActivityLog contains 1710 entries, consisting
of 932 Information, 758 Warning and 20 Error entries. Its clock aligns with UTC
when the package-load event is matched to the editor's offset-bearing timestamp.
The following times use the editor's `+01:00` offset.

| Source | Observation |
| --- | --- |
| Editor 14884 | First document registered at 19:50:40.837. Closing began at 19:50:43.295. Owned PID 12988 ended after shutdown at 19:50:46.784. A second open registered at 19:51:29.542, closed at 19:51:31.549 and retired PID 14852 at 19:51:35.049. The corresponding host logs were absent from the supplied files. |
| Editor 15980 | Pane created at 19:56:19.3148096, RDT registration at 19:56:19.3250883. Document closing at 19:56:21.5166010 and host shutdown at 19:56:21.5212272. Shutdown ended at 19:56:25.3284333 with code -1. |
| Host 8244 | START at 19:56:19.4455600, exception handlers installed, arguments parsed, DPI_SETUP_BEGIN at 19:56:19.4841994. No later marker or exception. Document closure began 2.032 seconds after that last marker. |
| ActivityLog | Records 1630 and 1631 show package load completing at 19:56:19.278. Record 1632 reports the missing localized package string. No error identifies our editor's E_FAIL or a host crash. |

`DPI_SETUP_BEGIN` surrounds `FrameworkBootstrap.EnsureInitialized`. This includes
assembly discovery, bootstrap construction and selection, settings/WinRT calls,
native initialization, COM registration and service registration. The supplied
log places the incomplete call inside this interval. It does not locate a
particular API, establish a deadlock or prove an unhandled native exception.

The editor's startup deadline is ten seconds. The observed close occurs earlier.
The logs do not identify who requested document closure. `StopOwnedHost` requests
IPC shutdown, waits for exit and can call `Process.Kill`. Exit code -1 can follow
an explicit process termination or our managed exception path. The old log does
not record which route occurred.

## Visual Studio messages

| Message | Assessment from the supplied evidence |
| --- | --- |
| PkgDefMgmt.dll, 0x8007007E, records 1296 and 1617 | The stack belongs to Extension Manager incompatibility/update discovery before the Designer open. A native library or dependency is unavailable in that VS process. It merits a targeted installation check if it persists, while the logs provide no causal link to the external backend call. |
| HrLoadNativeUILibrary, 0x800a006f, records 1306–1313 | These identify CSharpVsInteractiveWindowPackage, Windows Forms Remote Designer Hosting Package and XamlLanguagePackage. They are earlier VS resource-loading failures. Our package later loads and creates a pane. |
| ModernFormsDesignerPackage LANG 0415 ID 101, record 1632 | A real missing localized string warning. Editor construction, RDT registration and host launch continue after it. No supplied exception attributes E_FAIL to that resource. |
| 681 PkgDef collisions | No collision entry mentions ModernFormsNext, its package GUID or mfdesign. They describe competing profile registrations and provide no direct evidence of our host's failure. |
| Missing MEF assemblies and SCC options | Seven errors reference other installed VS components. The two SCC option errors occur after host shutdown. Preserve them for an installation comparison. |

There is insufficient evidence for a Windows, VirtualBox or Visual Studio repair.
First compare the instrumented candidate in the same guest. An independently
updated 24H2 snapshot can then distinguish image-specific behavior. Retain each
image's evidence and package identity before considering a targeted VS Installer
repair of a named missing component. A profile reset would discard the comparison.

## Instrumentation

Backend stages use `System.Diagnostics.Trace`, category `ModernFormsNext.Startup`.
Only the Designer installs a listener that forwards this category to its existing
per-process log. No new public framework API or platform service is introduced.
Records include PID, managed thread ID and native Windows thread ID. Assembly
load/activation failures retain their previous discovery behavior and report their
exception type and HRESULT. Stage payloads contain framework identities and status
values. They include no source text, DLL contents or project exception payloads.

Markers distinguish discovery, assembly load, constructor, CanInitialize,
Windows bootstrap/backend, settings and WinRT activation, DPI, message class,
message HWND, COM, service registration, form/native window, parent attach,
dispatcher loop and IPC readiness. Missing completion preserves the last entered
stage. Exceptions retain the host's existing exception diagnostics and exit code.

The VS editor records factory creation, pane handle creation, process start,
window readiness, observed process exit, shutdown acknowledgement, forced kill
request and its outcome. This identifies a termination requested by our editor.
An external termination still requires event/process evidence.

## Run on the guest

Check out the diagnostic PR's exact commit. Build the qualification package and
install it into `MFN486` while that experimental instance is closed. The existing
isolation guide describes the private VSIX installation. Use PowerShell 7.2 or
later. Build with the repository's global.json selection.

```powershell
dotnet build ModernFormsNext.VisualStudioExtension.Vsix -c Debug -m:1 /p:UseSharedCompilation=false /p:ModernFormsNextExecutablePreviewQualification=true
```

After installing those exact package bytes, run this single collection command
from the repository root.

```powershell
pwsh -NoProfile -File scripts/Test-DesignerStartup.ps1 -VsixPath ModernFormsNext.VisualStudioExtension.Vsix/bin/Debug/net472/ModernFormsNextDesigner.vsix -ExpectedSha256 (Get-FileHash ModernFormsNext.VisualStudioExtension.Vsix/bin/Debug/net472/ModernFormsNextDesigner.vsix -Algorithm SHA256).Hash -Commit (git rev-parse HEAD) -RootSuffix MFN486
```

The collector checks the commit and synthetic fixture source, records Windows,
SDK, VS and package identities and compares the deployed shell and DesignerHost
files with the VSIX entries, including worker/runtime files. A wrong installation
or missing dependency yields BLOCKED and a report. Multiple VS installations can
be disambiguated with `-VisualStudioPath` pointing to the installation directory.

It starts the installed host outside VS in Standalone and Integrated modes, using
an owned hidden parent with a pumped message queue. It checks a responsive DIRTY
command, requests clean shutdown and repeats Standalone startup. It then opens a
copied controlled document in the existing experimental IDE with `/Log`. It records
process CPU, handles, RAM, native thread states/wait reasons, host/editor logs and
relevant application error events. A timeout is recorded before terminating an
owned direct-probe host. The IDE is left open for inspection.

The observation interval is 20 seconds for evidence collection. The product's
startup and IPC deadlines retain their existing values. If VS shows a blocking
dialog, leave it visible until collection ends. If no document opens automatically,
record the result, then use File/Open/File on the copied `Fixture/PreviewExample.mfdesign`
and rerun collection after closing the experimental IDE. Manual inspection and
VSIX validation on the failing guest remain required.

Each run creates a new `artifacts/designer-startup/<timestamp>-<id>` directory.
`result.json` contains PASS, FAIL or BLOCKED. PASS requires readiness and responsive
IPC in all probes. It covers startup only. Collection never approves project DLLs.
After inspecting and closing the experimental IDE to flush ActivityLog, send the
entire directory. Raw failures, partial events and evidence gaps are retained.
An empty/missing WER result does not establish the absence of a native crash.
Repeated thread wait states do not establish a deadlock.

## References

- [Visual Studio activity logging](https://learn.microsoft.com/visualstudio/ide/reference/log-devenv-exe)
- [Windows process termination and exit values](https://learn.microsoft.com/windows/win32/procthread/terminating-a-process)
- [Designer isolation and private qualification builds](designer-executable-preview-isolation.md)

The guest startup failure and LPAC qualification remain **BLOCKED** until the
instrumented package is exercised on the reported configuration and its evidence
identifies the failing operation. Issues #40 and #486 retain their acceptance criteria.
