# Executable custom-control previews in the Windows Designer

Status: proposed for #40. PR #468 is the data-preview hardening stage.
The release scope remains 1.12.0. This design does not enable project-code execution.

## Existing boundaries

The normal preview reads .mfdesign and detached source/PE metadata. The Designer
owns the document, property grid, selection, hit testing, code generation and #41
recovery. Visual Studio hosts this Designer in an owned child process. That process
holds the user's authority to read and write project files, so it must continue
to handle only data and trusted framework controls. Standalone Designer follows
the same boundary.

## Proposed execution model

- Keep data preview as the default. An explicit per-project opt-in applies to
  an identified build snapshot. Changing its artifacts or requested authority
  invalidates the previous executable approval. A design file, referenced
  binary and project property cannot opt themselves in. Revoke stops the worker
  and removes executable frames from the surface.
- Use an additional short-lived Windows worker. Launch a trusted worker binary,
  verify its restricted token and limits, then allow it to load only the staged
  snapshot. Its isolated directory contains read-only artifacts and any approved
  packaged resources plus disposable private scratch. The default snapshot has
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
  On managed exceptions capture a bounded method stack and phase. Constructors
  and static initialization can fail before an instance exists. Dispose only
  acquired instances, attempt outstanding cleanup and retire unhealthy workers.
  A process crash or deadline retires the whole worker and its request generation.
  Acknowledge phase transitions to refine the last known phase on hangs. Use
  one stable diagnostic per unchanged failed request and keep useful placeholders.
  Explicit retry or a repaired build starts a clean process. Healthy controls
  can be requested on that worker while the faulting node remains deferred.
- Use independently bounded startup, construct/apply/layout/render and shutdown
  deadlines. The trusted Designer enforces them asynchronously outside the worker
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

## Proposed issue structure

The existing #40 acceptance bullets cover document survival after a failing
custom control, useful placeholders, actionable origin, safe disposal and
reload. Its scope also explicitly includes DesignMode, suppression of
runtime-only effects, per-control constructor/property/render exceptions,
diagnostics, cancellation or timeout and investigation of a process boundary.
With the owner's clarification, qualify those criteria for a minimal executable
preview before closing #40. OS-level confinement is a prerequisite for that
preview. A successful #468 or a clean worker exit alone does not supply that
proof.

Use implementation subissues under #40 for the confined worker/negative
security tests, executable raster/diagnostics and worker recovery/integration.
The parent retains end-to-end acceptance and remains an open 1.12.0 item. This
document proposes the breakdown without creating or retargeting GitHub issues.

Separate product work includes interactive previews with pointer/keyboard and
native children, live debugger/hot reload, custom converters/designers,
expanded per-project capability grants and brokers, advanced native or
multiple target-framework resolution, and automatic build/restore. Integrating
a new public constructor-time design contract also needs an explicit API review.
These are extensions. They do not reduce #40's existing five acceptance
criteria or the requirements of the minimal executable preview.
