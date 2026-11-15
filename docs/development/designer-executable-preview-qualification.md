# Windows Designer executable preview qualification

Issue #486 tracks this qualification under #40 for 1.12.0. The ordinary VSIX
continues to use data preview. Executable preview remains in explicit local
qualification builds while platform and security checks are open.

## Enforcement

| Boundary | Enforcement |
| --- | --- |
| Worker launch | Windows creates a suspended process, applies the LPAC package SID with zero capabilities and blocks child creation. The broker verifies the token, package group access, Untrusted integrity, mandatory no-write-up and job membership before resume. |
| Resources | Windows Job Object limits the active process count to one, process memory to 384 MiB and CPU to 25 percent. Job UI and kill-on-close restrictions apply. The broker enforces startup and operation deadlines. |
| File access | NTFS discretionary ACLs grant read and execute on staged runtime and selected DLLs. The worker has no write grant in staging. The broker deletes the Windows-created bootstrap profile before resuming the process. Only one live worker can use that package SID. |
| Artifact identity | The broker captures the bytes of explicitly selected managed DLLs, rejects links that lead to a different source path, checks PE metadata and computes the SHA-256 manifest. The worker rehashes staged bytes. The digest includes the policy version. Source changes invalidate approval. |
| IPC | Windows inherits only two unnamed pipe ends. The broker correlates session, request, document revision and node. Header and intent determine limits before reading payloads. The broker checks the expected raster length, format, phase order, origin token and end of stream. The worker reads one bounded request through end of stream before loading project code. |
| Editor state | The Designer owns the document, history, selection, recovery and save operations. The worker returns pixels or a bounded failure record. The editor discards stale responses and retains placeholders after failures. |

## Storage investigation

Earlier tests on Windows build 26300.9711 recorded successful writes in a live
AppContainer profile. Changing its discretionary ACEs, even with explicit deny
entries for the package SID, left tested file writes available. Lowering the
worker integrity while keeping its profile also let a native
`CreateFileW(GENERIC_WRITE, OPEN_EXISTING)` obtain a handle in that profile.
The mandatory-integrity change is a separate defense. Profile retirement removes
the observed storage path.

Direct process creation with a derived SID and no provisioned profile failed
with Win32 error 2. A separate `WRITE_RESTRICTED` token experiment preserved an
extra restricting SID but terminated before managed startup with
`0xc0000142`. Neither variant is the shipping qualification path.

The selected path derives a package SID from a fresh approval name, creates its
bootstrap profile for each suspended process, verifies the identity, then calls
`DeleteAppContainerProfile` while the worker still has no running thread. The
broker requires the package folder to be absent before resuming and checks it
again after exit. Failed bootstrap launch retires the owned profile after
stopping the child. Policy drift or a worker that fails to exit stops further
launches for that approval. A trusted admission worker exercises file creation
under the removed profile, recreation through `CreateDirectoryW` and
`CreateAppContainerProfile`, and access to writable AppContainer registry
storage. The broker recreates the temporary bootstrap only after the prior
process is gone.

The Windows profile API covers its own folders and registry storage. Job write
notifications arrive after crossing a threshold and the supported Windows
versions do not provide Job Object I/O rate control through
`SetIoRateControlInformationJobObject`. Neither provides the hard persistent
storage limit required here. Removing the profile and withholding write grants
remove the known write routes for this test policy. Global absence of writable
objects under all supported host configurations and brokered calls still
requires qualification.

- [CreateAppContainerProfile](https://learn.microsoft.com/windows/win32/api/userenv/nf-userenv-createappcontainerprofile)
- [DeleteAppContainerProfile](https://learn.microsoft.com/windows/win32/api/userenv/nf-userenv-deleteappcontainerprofile)
- [Mandatory integrity](https://learn.microsoft.com/windows/win32/secauthz/mandatory-integrity-control)
- [Job write notifications](https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-jobobject_notification_limit_information)
- [Job I/O rate control availability](https://learn.microsoft.com/windows/win32/api/jobapi2/ns-jobapi2-jobobject_io_rate_control_information)

## Observations and remaining configurations

| Scope | Observation |
| --- | --- |
| Windows 11 Home 26H2 x64 build 26300.9711, .NET 10 runtime | Broker admission passed on a standard-user desktop. The worker was able to read its staged runtime. Native writes to read-only temporary storage, attempts to recreate the retired profile and writable profile-registry access failed. Hostile raster sizes and trailing frames are covered by protocol tests. |
| Controlled local network | Positive TCP and UDP controls on IPv4 loopback, one local IPv4 interface and IPv6 loopback reached broker-owned endpoints. Corresponding LPAC operations were denied. A broker-owned desktop named pipe accepted the unrestricted client and rejected the worker. |
| Real managed controls | A separately built control project rendered the expected Skia pixels under the new policy. Controlled constructor, property, layout, paint and Dispose failures returned their diagnostic phase. Paint timeout and abrupt process exit left a later healthy worker able to render. |
| Other Windows and Visual Studio configurations | Pending. Repeat the signed-package or local qualification-package workflow on supported Windows 11 x64 builds and Visual Studio versions. Record Windows edition and build, process architecture, Visual Studio build, .NET runtime version and exact VSIX hash. |
| Controlled remote IPv4 and IPv6, expanded OS broker tests | Pending. Use isolated test guests with owned TCP and UDP listeners and a disposable local account. Use synthetic markers for files, registry and shell or COM operations. Keep default host firewall rules and test the positive endpoint before testing the LPAC deny path. |

Use the installed-VSIX reproduction in
[the Designer isolation design](designer-executable-preview-isolation.md).
Keep the standard-package gate closed until the owner can review the complete
matrix and actual negative and positive controls.
