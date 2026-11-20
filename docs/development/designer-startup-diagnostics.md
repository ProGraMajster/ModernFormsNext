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

## Guest collector bundle from 20 November 2026

All nine files in `20261120-190528-31c5842e.zip` were inspected, including all
137 process samples and the complete ActivityLog. The collector checkout is
`04469bce973f7df4908adbdbd91be2907b0a7da8`. The installed qualification VSIX
matches SHA-256 `64CE1C28A9E6BF301745094C6E80E95F1C33100E90FA2B12C631A0DD0F17EA6F`.
VS reports file version `17.14.37906.3`; the reported product version is
17.14.45. Windows reports Professional, 24H2, build 26100, UBR 1. The legacy
registry ProductName value is not used to infer the Windows marketing version.

| Probe | PID | Bootstrap complete (`+01:00`) | IPC / form ready | Exit |
| --- | --- | --- | --- | --- |
| Standalone | 7956 | 19:05:30.9981950 | 19:05:33.8778753 / 19:05:33.9534597 | 0 |
| Integrated native parent | 6832 | 19:05:34.3754994 | 19:05:35.7138009 / 19:05:35.7246726 | 0 |
| Standalone restart | 10536 | 19:05:36.0605082 | 19:05:37.3708311 / 19:05:37.4543670 | 0 |

These three direct probes passed startup, responsive DIRTY and clean shutdown.
Their full logs include the paired backend stages. `GLOBAL_EXCEPTION_DIAGNOSTICS_READY`
means that handlers were installed; it is not an exception report.

The IDE probe remains **BLOCKED**. Its 79 samples cover PID 10428 for about
20 seconds. No owned Designer host, editor log or Designer package load appears.
The 635 ActivityLog entries contain 568 Information, 46 Warning and 21 Error
entries. The errors concern other VS resources, MEF components, Extension Manager,
source-control options and extensibility metadata. The three ModernFormsNext
mentions concern gallery/version metadata. None records our editor factory or a
backend exception. The application-event query found no matching events; this
does not establish that a crash was impossible.

The old collector passed only the copied `.mfdesign` to devenv, allowing the
default editor association to select JSON. It also left the `.csproj` and C#
source in the repository, so project discovery beside the copied document no
longer matched a normal project open. These are collector defects. The bundle
does **not** reproduce the earlier VS-owned host stopping at DPI_SETUP_BEGIN,
and the successful direct probes do **not** prove that earlier problem fixed.

## Instrumentation

Backend stages use `System.Diagnostics.Trace`, category `ModernFormsNext.Startup`.
Only the Designer installs a listener that forwards this category to its existing
per-process log. No new public framework API or platform service is introduced.
Records include PID, managed thread ID, native Windows thread ID and the process
creation FILETIME. The collector and process-test readiness checks use this
creation value to reject older runs after Windows reuses a PID. Assembly
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
copied controlled project in the existing experimental IDE with `/Log`. The copied
JSON and C# files retain their bytes; the copied `.csproj` has its framework
ProjectReference rebased to the same source target. The originals are untouched.
Source/copy hashes and the reference target are recorded in `identity.json`.

A separate STA PowerShell helper binds the ROT moniker for the exact IDE PID,
after checking its creation FILETIME, executable and experimental profile. It
loads only the selected VS installation's public interop contracts. It calls
`IVsShell.LoadPackage` for the installed trusted Designer package and
`IVsUIShellOpenDocument.IsDocumentInAProject` to obtain project context without
opening a document. It then calls `OpenSpecificEditor`
with `OSPE_SKIPDEFERRAL`, factory `C61567C8-F5AC-4F9E-9C6E-B4EC99C7AB31` and
Designer logical view `7651A702-06E5-11D1-8EBD-00A0C90F26EA`. It verifies the returned frame's factory and document
moniker and shows the frame. A JSON/text frame is a
failure, even if the shell returned S_OK. Neither file placement nor a user editor
association is treated as evidence that the registered factory ran.

`editor-open.json`, helper stdout/stderr and `result.json` separate explicit
editor selection from host startup. Readiness requires a live matching installed
host, FORM_SHOWN, IPC_READY and responsive DIRTY as well as verified editor
selection. An unavailable SDK contract or IDE automation deadline is BLOCKED;
an actual editor-call error is FAIL. Busy/ROT-not-ready polling is bounded and
does not retry a factory failure. The parent can stop its helper if an RPC hangs.
It never changes editor associations, machine settings or product deadlines.

It records
process CPU, handles, RAM, native thread states/wait reasons, host/editor logs and
relevant application error events. A timeout is recorded before terminating an
owned direct-probe host. The IDE is left open for inspection.

The observation interval is 20 seconds for evidence collection. The product's
startup and IPC deadlines retain their existing values. If VS shows a blocking
dialog, leave it visible until collection ends. Preserve BLOCKED/FAIL and the
dialog; do not manually open the document to turn that run into PASS. Close the
experimental IDE after inspection and rerun into a new directory. Manual inspection and
VSIX validation on the failing guest remain required.

The routing fix changes collector scripts and tests only. For the next guest
comparison, keep the existing installed qualification VSIX with digest
`64CE1C28A9E6BF301745094C6E80E95F1C33100E90FA2B12C631A0DD0F17EA6F`;
rebuilding or reinstalling it is unnecessary. Check out the updated diagnostic
PR commit and use the path to those retained package bytes:

```powershell
pwsh -NoProfile -File scripts/Test-DesignerStartup.ps1 -VsixPath '<retained qualification VSIX path>' -ExpectedSha256 64CE1C28A9E6BF301745094C6E80E95F1C33100E90FA2B12C631A0DD0F17EA6F -Commit (git rev-parse HEAD) -RootSuffix MFN486
```

Here `-Commit` identifies the collector checkout. The independent VSIX digest
identifies the installed product candidate built from `04469bc`. Keep both in
the returned evidence. No build or approval of executable project controls is
part of this startup comparison. Inspect the Designer, then close the IDE to
flush ActivityLog and return the complete new directory, including the copied
fixture, `editor-open.json` and helper logs. A save prompt for the temporary
solution concerns that controlled copy. Do not alter the source fixture.

The revised collector passed all four probes locally against the retained
installed VSIX on Windows 11 Home 26H2 26300.9711 / VS 18.12.12405.243. The
fresh project open produced EDITOR_FACTORY_CREATE, PANE_HANDLE_CREATE,
HOST_LAUNCH and the matching host's FORM_SHOWN/IPC_READY, with responsive DIRTY.
This validates the collector on that configuration. The repeat with VS 2022
17.14.45 on the reported 24H2 guest remains **NOT EXECUTED** for this revision.

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
- [Querying the document's project context](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.interop.ivsuishellopendocument.isdocumentinaproject?view=visualstudiosdk-2022)
- [Opening the selected editor directly](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.interop.ivsuishellopendocument.openspecificeditor?view=visualstudiosdk-2022)
- [Designer isolation and private qualification builds](designer-executable-preview-isolation.md)

The guest startup failure and LPAC qualification remain **BLOCKED** until the
instrumented package is exercised on the reported configuration and its evidence
identifies the failing operation. Issues #40 and #486 retain their acceptance criteria.
