# Executable custom-control previews in the Windows Designer

Issue #40 tracks implementation and qualification for 1.12.0. PR #468 hardened
the data preview. Local qualification builds can use the executable renderer.
Ordinary VSIX builds use the data preview and exclude the executable worker.

## Existing boundaries

The normal preview reads .mfdesign and detached source/PE metadata. The Designer
owns the document, property grid, selection, hit testing, code generation and #41
recovery. Visual Studio hosts this Designer in an owned child process. That process
holds the user's authority to read and write project files, so it must continue
to handle only data and trusted framework controls. Standalone Designer follows
the same boundary.

## Execution model

- Keep data preview as the default. An explicit per-project opt-in applies to
  an identified build snapshot. Changing its artifacts or requested authority
  invalidates the previous executable approval. A design file, referenced
  binary and project property cannot opt themselves in. Revoke stops the worker
  and removes executable frames from the surface.
- Use an additional short-lived Windows worker. Launch a trusted worker binary,
  verify its restricted token and limits, then allow it to load only the staged
  snapshot. Its isolated directory contains read-only artifacts and any approved
  packaged resources and an inaccessible temporary-directory target. The default snapshot has
  no source tree, user profile, credentials or inherited environment secrets.
  Preview consumes existing build output. Build targets, package restore and
  source generators run only through the normal, separately invoked build flow.
- Use the ordinary control assembly and supported control contract inside the
  worker. Bind the worker to a compatible framework identity, resolve from a
  closed manifest and report missing dependencies. ALC may manage resolution
  but has the privileges of the worker. Supplying a design site after normal
  construction covers setters, layout and paint. Project controls that need
  constructor-time behavior need a separately documented opt-in runtime API or
  design-specific factory contract.
- Render an inert bounded raster. Return data keyed by session generation,
  request ID, build digest, document revision and node identity. Use the existing
  surface for DPI, authored geometry, clipping, selection and input. The worker
  cannot acquire an editor capability or supply a window, clipboard object,
  managed object, type converter, callback, path to open or code to evaluate.
  The first executable slice is a static preview. Paint completion is not a
  signal for the Designer to save or mutate authored data.
- Keep one project-code operation in flight per worker and coalesce obsolete
  requests. Before dispatch the controller records the node and operation.
  On managed exceptions capture one project method token from a bounded stack and record the phase. Constructors
  and static initialization can fail before an instance exists. Dispose only
  acquired instances, attempt outstanding cleanup and retire unhealthy workers.
  A process crash or deadline retires the whole worker and its request generation.
  Acknowledge phase transitions to refine the last known phase on hangs. Use
  one stable diagnostic per unchanged failed request and keep useful placeholders.
  Explicit retry or a repaired build starts a clean process. Healthy controls
  can render in fresh workers while the faulting node remains deferred.
- Use independently bounded startup, load/construct/properties/layout/paint/dispose
  and shutdown deadlines. The trusted Designer enforces them asynchronously outside the worker
  UI thread. Cancellation for a superseded render is best effort. A stuck worker
  is terminated by its job and cannot delay document editing or close. Late or
  duplicate bytes from a retired generation are discarded. Keep recovery and
  the document's dirty revision in the trusted Designer during any worker exit.

## Security boundary and proof of feasibility

Start with a no-network AppContainer and evaluate LPAC against the supported
Windows matrix and the .NET/Skia/ModernFormsNext worker. Use a job for memory,
CPU, process count and owned-tree retirement and set a restricted child-process
policy. Set the job and sandbox attributes before resuming the worker. Check
the actual token, job membership and inherited handle list before project code
runs. Fail closed to the default preview if the runtime, Windows configuration
or application needs rights outside the qualified policy. The initial design
does not grant network, loopback, COM broker, user files, credential stores,
external process handles, HWND attachment or subprocess access. Avoid a
build-time or runtime package dependency on the sandbox probe while evaluating.

Pass only narrowly scoped IPC handles or a local pipe with explicit rights for
the worker identity after validating the platform's AppContainer IPC rules.
Authenticate the session, cap frame sizes and diagnostic strings before
allocating, and validate lengths, dimensions, stride, pixel format and
generation. Treat replies as hostile. The broker accepts only results for
requests it issued and has no general file, registry, network or process service.
Crash cleanup owns only its process tree and staging directory. Revalidate
ownership before removal. Existing tests cover the trusted host, not the
worker sandbox.

The feasibility spike must run framework rendering and demonstrate blocked
reads/writes of synthetic canary files outside staging, writes/reads in its
private scratch, denial of outbound and loopback connections, denial of process
creation and host handle access, and cleanup after both normal exit and a hung
or terminated worker. Use synthetic fixtures and launched children only.
Validate malformed/oversized IPC, repeat reloads, document changes in flight,
resource ceilings, two-window editing, and stable state on timeout/crash.
Document supported OS/runtime, denial observations and test exclusions before
enabling the executable path. Failing the spike leaves the feature disabled.

Primary platform references

- [Launch an AppContainer](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer)
- [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)
- [Process creation attributes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)
- [Named-pipe access rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)
- [.NET AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-runtime-loader-assemblyloadcontext)

## Windows feasibility probe — 2026-11-14

A synthetic x64 worker rendered a ModernFormsNext control into a Skia raster on
Windows build 26300.9711 and .NET 10.0.12. This local fixture was launched from
a staged self-contained copy and an empty-capability LPAC. Its control verified a
design site and an authored property before painting. A normal process ran the
same fixture as a positive control. No project assembly or editor ran inside the worker.

The broker configured the token and a job on the suspended process. Win32
`TokenIsLessPrivilegedAppContainer` returned error 87. The probe instead
compared two `AccessCheck` results with ACLs that differed only in their
package group. The ordinary AppContainer matched both S-1-15-2-1 and
S-1-15-2-2. The LPAC matched only S-1-15-2-2. The worker used just two
inherited anonymous-pipe ends. A third intentionally unlisted pipe was
unreadable. The run before and after a deliberately hung worker returned the
expected reply byte and raster. Closing the timed-out job ended the process and
closed its pipe. The temporary profile was deleted.

The probe could read the staged file and write to private scratch. Its read and
write attempts against a synthetic outside file and its write to the read-only
snapshot returned access denied. `File.Exists` could see outside file metadata.
The privileged handle attempt against the broker returned error 5, and child
creation returned error 367. TCP to a test listener via loopback and a local
non-loopback interface failed with Winsock 10107 under LPAC. Both addresses
worked in the ordinary process. An earlier ordinary AppContainer comparison
accepted a UDP send call to a test address, with delivery unobserved. The LPAC
result covers these local TCP paths. Remote network,
registry, brokered operations and broader filesystem access still require
paired positive and negative controls.

Two initial launches failed during process initialization with
`0xc0000142`. The managed worker ran after removing `CREATE_NO_WINDOW`
from the broker launch. Validate window visibility and startup behavior with the
packaged VSIX host. Complete hostile frame parsing, per-node constructor/setter/
paint/disposal diagnostics, timeouts, restart, resource ceilings and editor
recovery on supported Windows versions before enabling project code. Keep the
default data preview when admission checks fail.

The same scratch-only synthetic fixture also injected constructor, setter,
paint and disposal exceptions. It recorded the node, type, phase and originating
method for each one. The instances acquired before setter and paint exceptions
were disposed. The broker received end-of-stream after exceptions, abrupt exit
and a hung worker. The later healthy launch rendered again.
Production integration still needs to correlate those failures with the
editor's document revision and preserve the other controls and selection.

The local reproduction and raw transcript are retained in the audit workspace
under `artifacts/scope-reaudit-2026-11-12/issue-40/os-probe`.

- [AccessCheck](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-accesscheck)
- [Process handle inheritance](https://learn.microsoft.com/en-us/windows/win32/procthread/inheritance)

## Qualification build and reproduction

Use a disposable checkout and a fresh Visual Studio experimental profile. Build
the fixture and VSIX with the SDK from `global.json`.

```powershell
dotnet build scripts/tests/fixtures/DesignerExecutablePreview/ProjectFixture.csproj -m:1 /p:UseSharedCompilation=false
dotnet build ModernFormsNext.VisualStudioExtension.Vsix -c Debug -m:1 /p:UseSharedCompilation=false /p:ModernFormsNextExecutablePreviewQualification=true
```

Install `ModernFormsNext.VisualStudioExtension.Vsix/bin/Debug/net472/ModernFormsNextDesigner.vsix`
in the fresh profile with the Visual Studio SDK's `VSIXInstaller /RootSuffix:<suffix>`.
Open the fixture project and `PreviewExample.mfdesign`. The Designer starts in data
mode. Use **Isolated preview** in the Designer status bar, select the fixture's
`ProjectFixture.dll` from `bin/Debug/net10.0-windows`, review the assembly and build
SHA-256 digests, then select **Approve this build**. Close Settings to edit the
document. The same dialog offers **Retry preview** and **Turn off preview**.
Approval belongs to the current Designer session and project. A change to an
approved output revokes approval. After a rebuild, select and approve the new
outputs to resume. Build and package restore are separate Visual Studio actions.

The fixture renders green `custom C#` badges and exercises throws in the
constructor, property setter, layout, paint and Dispose, a process exit and a
hung paint. Local host output records the named control and phase. Managed
exceptions can also report a method resolved from the approved PE. Verify that
`PRIVATE_EXCEPTION_MESSAGE` stays absent. Add a button, wait for the worker
timeout and check that the button remains selected and can be undone and redone.
Modify the fixture, rebuild, observe revocation and reapprove. On close or
revocation, confirm that the owned worker, profile and staging data are retired.
Check the Visual Studio output and per-process Designer host log in the temporary
directory. Repeat while the artifact copy is in progress and while the consent
review is open. Closing must retire the staged copy in both cases.

Every frame has a session, request number, document revision and node number.
The request also carries the approved SHA-256 digest. A request is limited to
64 KiB and 32 bounded scalar properties. Rasters are capped at 1024×1024 pixels
(4 MiB). The worker handles public parameterless ModernFormsNext controls with
compatible managed references. The broker stages only the selected managed DLLs
under the current project directory. The worker supplies a design site after
construction, runs layout and paint, and returns pixels. Runtime services, native
dependencies and interactive input are outside this preview path. A failed frame
leaves the existing data preview or placeholder available.

The broker verifies the worker token and job before resuming every process.
Startup gets five seconds, each reported phase gets three seconds and exit gets
two seconds. The job allows one process, 384 MiB of process memory and 25 percent
CPU. Windows starts each worker suspended and the broker lowers its primary
integrity to Untrusted and verifies mandatory no-write-up. Before resuming, the
broker deletes the bootstrap AppContainer profile and checks the expected package
folder for absence. The worker receives read-only runtime and project files.
The probe's read grant to the temporary-directory canary is removed before
project loading. See [the LPAC qualification record](designer-executable-preview-qualification.md)
for the Windows observations and remaining checks.

## Initial qualification baseline — 2026-11-15

On Windows x64 build 26300.9711, the broker rendered a separately compiled C#
control project. Its pixel check passed before and after all five managed
failure phases, a timeout and a process exit. Paired IPv4 TCP and UDP checks
covered loopback and a local non-loopback interface. The broker verified the
restricted token, package SID, child policy and job. It exercised outside file
read/write denial, read-only snapshot enforcement, private probe scratch,
privileged broker handle denial and child creation denial.

An installed development VSIX in an isolated Visual Studio 18 profile showed the
real control project. Designer output confirmed successful frames, all five
failure phases, timeout and process exit. During a failure sequence the editor
retained a newly added and selected button. Undo and Redo succeeded, and the
original document SHA-256 stayed unchanged. A write notification for the fixture
assembly revoked approval and retired staging. This covers one Windows and
Visual Studio configuration.

Before enabling the ordinary VSIX, repeat qualification on supported Windows
builds. Verify remote network and IPv6 paths with controlled endpoints, registry
and brokered operations, cross-process UI and clipboard restrictions, and
AppContainer profile storage. Exercise the job ceilings, forced termination and
two-document editing with the installed candidate. Keep the build gate closed
until this evidence is reviewed.

## Issue scope

The existing #40 acceptance bullets cover document survival after a failing
custom control, useful placeholders, actionable origin, safe disposal and
reload. Its scope also explicitly includes DesignMode, suppression of
runtime-only effects, per-control constructor/property/render exceptions,
diagnostics, cancellation or timeout and investigation of a process boundary.
With the owner's clarification, qualify those criteria for a minimal executable
preview before closing #40. OS-level confinement is a prerequisite for that
preview. A successful #468 or a clean worker exit alone does not supply that
proof.

The parent retains end-to-end acceptance and remains an open 1.12.0 item. The
remaining qualification and enablement can be tracked as one child of #40.

Separate product work includes interactive previews with pointer/keyboard and
native children, live debugger/hot reload, custom converters/designers,
expanded per-project capability grants and brokers, advanced native or
multiple target-framework resolution, and automatic build/restore. Integrating
a new public constructor-time design contract also needs an explicit API review.
These are extensions. They do not reduce #40's existing five acceptance
criteria or the requirements of the minimal executable preview.
