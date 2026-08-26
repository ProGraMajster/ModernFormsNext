# Visual Studio Designer host contract

ModernFormsNext uses a Windows-specific, out-of-process host for its Visual Studio Designer. The
classic VSSDK package remains small and .NET Framework-compatible, while the Designer and renderer
run on modern .NET in `ModernFormsNext.VisualStudioDesignerHost`.

## Process and ownership model

Each open `.mfdesign` editor pane owns one Designer host process and one document. This one-pane,
one-process model gives every Visual Studio document an independent HWND, endpoint, lifetime, and
failure boundary. Opening two projects that both contain `Form1.mfdesign` cannot mix their windows
or commands.

The pane retains the exact `Process` object returned by `Process.Start`. It never searches for
`ModernFormsNext.VisualStudioDesignerHost.exe` by process name and never terminates another Visual
Studio instance's host. Closing the pane requests bounded shutdown over its private endpoint. If
that owned process is unresponsive, only that exact process may be terminated.

The process remains Windows/Visual Studio-specific. The shared Designer shell, document model,
layout, rendering, persistence, and recovery services do not reference VSSDK or Win32 hosting
types. The classic VSIX uses a small WinForms `UserControl` only because `WindowPane.Window`
requires an `IWin32Window`; that control supplies the pane HWND and a status label. It does not
paint or lay out the Designer. Designer rendering remains on the existing ModernFormsNext
Skia/WindowKit path inside the child process.

The lightweight `ModernFormsNext.VisualStudioExtension.Shared` assembly contains only the pure
command-status contract used by the modern extension build and the classic VSIX. It targets
`netstandard2.0`, has no VSSDK, WinForms, System.Drawing, Designer, native, package, or project
dependencies, and is consumed through normal project references rather than linked source files.

## Typed native-window contract

`WindowBase.PlatformHandle` is the supported boundary between a ModernFormsNext window and a native
host. It returns a non-owning `IPlatformHandle`; callers must inspect `HandleDescriptor` before
using the value. The Windows backend reports `HWND`.

The Visual Studio adapters no longer reflect over the private `WindowBase.window` field. Both the
in-process adapter used by contract tests and the shipped out-of-process host consume the typed
property. Visual Studio-specific style changes, `SetParent`, focus, and child-window sizing remain
inside the Visual Studio extension/host projects.

The handle belongs to ModernFormsNext and is valid only while the window is alive. A host must not
close it, cache it after window disposal, or assume that a non-Windows descriptor is an HWND.

## Open and readiness flow

The installed extension follows this sequence:

```text
View Designer / Shift+F7
  -> conservative ModernFormsNext file detection
  -> create or locate the companion .mfdesign file
  -> open the registered .mfdesign editor factory
  -> create a Visual Studio editor pane
  -> launch one owned DesignerHost process
  -> pass the pane HWND and a unique local endpoint
  -> host validates its typed HWND and parents it into the pane
  -> pane observes the child window and applies its current bounds
```

Readiness is bounded to ten seconds and does not use a fixed startup sleep. The pane searches only
its own child-window tree for a window owned by the exact launched PID. A missing executable,
immediate process exit, attachment timeout, invalid parent HWND, or backend that does not report an
HWND produces a visible pane diagnostic. The host log remains available at
`%TEMP%\ModernFormsNextDesignerHost-<pid>.log`. Designer diagnostic logs are likewise isolated per
process under `%LOCALAPPDATA%\ModernFormsNext\Designer` so simultaneous hosts cannot race one file.

The host derives the owning Visual Studio PID from the supplied parent HWND and monitors that exact
process. If Visual Studio exits unexpectedly, the child closes instead of remaining orphaned. If
Visual Studio recreates the pane HWND while docking or changing display state, the adapter stops
the process attached to the obsolete HWND and launches a clean replacement for the same document.

## Resize, DPI, and focus

Visual Studio owns the editor-pane bounds and chrome. Resize and parent-DPI notifications reapply
the pane's current device-pixel client size to the child HWND. Focus entering or clicking the pane
is forwarded to the child window so Designer keyboard and pointer input continue through the
ModernFormsNext input pipeline.

The in-process lifecycle controller uses explicit `Detached`, `Attached`, `Faulted`, and `Disposed`
states. Failed attachment rolls back partial parent/style changes. Resize and focus failures are
captured as diagnostics instead of unwinding synchronous Visual Studio window notifications.

## Save, reload, dirty state, and close

The editor pane and its host use a private, document-scoped named pipe for four control operations:

- `OPEN` reloads or reattaches the pane's canonical `.mfdesign` document;
- `SAVE` invokes the existing Designer save and generated-code path;
- `DIRTY` lets Visual Studio participate in save prompts;
- `SHUTDOWN` closes the owned host after Visual Studio has resolved the document close decision.

This channel is an internal VSIX/host implementation detail, not the `.mfdesign` format and not a
public runtime API. The Designer UI thread executes document mutations. Malformed or unknown
commands return an error and do not terminate the listener.

Save, autosave recovery, and external-change handling remain owned by the existing Designer
persistence coordinator from issue #41. The VSIX does not create a second recovery store or a
parallel file watcher. If an alive host temporarily cannot answer a dirty query, the pane reports
dirty conservatively so Visual Studio does not silently discard work. If the process has already
exited, the recovery artifacts remain available when the document is reopened.

## View Designer and project metadata

The package registers both its explicit **View ModernFormsNext Designer** command and Visual
Studio's standard `ViewForm` command used by **View Designer** and Shift+F7. The standard command is
claimed only when conservative detection says the selected/active file is a supported
ModernFormsNext Form or UserControl. For every other file it reports unsupported so command routing
continues to the normal C#, WinForms, or other project-system handler.

The `ModernFormsNext` NuGet package ships `buildTransitive/ModernFormsNext.targets`. SDK-style
projects therefore receive conventional nesting automatically:

```text
MainForm.cs
  MainForm.Designer.cs
  MainForm.mfdesign
```

The target fills only missing `DependentUpon` metadata and only when the expected primary/companion
file exists. Explicit project metadata remains authoritative, unrelated `.Designer.cs` files are
not claimed merely because the package is installed, and SDK default items are updated rather than
duplicated. The Visual Studio item templates continue to create all three files atomically and use
ModernFormsNext-specific subtypes rather than `SubType=Form`, which belongs to the classic WinForms
Designer.

## Interactive Visual Studio validation checklist

Automated tests cover the typed handle, command routing, parent argument validation, IPC command
validation, lifecycle state transitions, attach failure rollback, close/reopen, DPI/resize/focus
routing, item-template shape, and packaged nesting target. The following behavior still requires an
observed Experimental Instance:

1. Build and install the Debug VSIX into the `Exp` hive, then create a project from the
   ModernFormsNext template.
2. Select `MainForm.cs`; verify **View Designer** is available and Shift+F7 opens the embedded
   Designer pane. Verify `Program.cs` still routes normally and does not expose this Designer.
3. Add a **ModernFormsNext Form** and **ModernFormsNext UserControl**. Verify `.Designer.cs` and
   `.mfdesign` appear nested below their primary `.cs` item without hand-editing the project file.
4. Open two Designer documents. Close the first and verify the second remains responsive and keeps
   its own process/window state.
5. Resize, dock, float, minimize/restore, and move Visual Studio between monitors with different
   DPI. Verify the child fills the pane, accepts pointer input, and keeps readable rendering.
6. Move focus between Solution Explorer, code, Property Grid, and the Designer. Verify keyboard
   shortcuts return to the Designer after its pane is focused.
7. Modify a property, use Visual Studio **Save**, and verify both `.mfdesign` and generated
   `.Designer.cs` are updated. Change the file externally and verify the existing #41 conflict or
   reload workflow remains authoritative.
8. Close a dirty document and exercise Save, Don't Save, and Cancel. Reopen after Save/Don't Save
   and verify the expected persisted/recovery state.
9. Terminate the exact child host process. Verify only its pane shows a failure, then close/reopen
   the document and verify a new host attaches.
10. Temporarily remove the packaged host executable and verify the pane reports the missing path;
    restore it and verify close/reopen succeeds.

Do not report this checklist as passed unless each behavior was observed in Visual Studio. An
automated build or host-contract test is not interactive evidence.
