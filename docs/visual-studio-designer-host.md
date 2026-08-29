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

The shipped host is a real child-window contract, not merely a top-level window positioned over
the editor. Before it can become visible, the host removes `WS_POPUP`, caption, frame, system-menu,
minimize, and maximize styles, adds `WS_CHILD | WS_CLIPSIBLINGS`, removes top-level extended styles,
calls `SetParent`, and applies `SWP_FRAMECHANGED`. The ModernFormsNext title bar, border, move drag,
resize drag, minimize, and maximize affordances are disabled because Visual Studio owns all chrome.
The original and effective `GWL_STYLE`/`GWL_EXSTYLE`, parent, owner, bounds, and DPI are written to
the per-process diagnostic log.

This ordering follows the Windows contract: [`SetParent`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setparent)
does not change `WS_CHILD` or `WS_POPUP`; cached frame data is refreshed with
[`SetWindowPos`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos).
The Windows backend recognizes an already converted `WS_CHILD` HWND as externally hosted and does
not reapply its ordinary nullable Form owner during the first native show. The handle is therefore
created hidden, converted and attached, and only then shown inside the pane; it is never exposed as
a movable top-level window.

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
The in-process editor contract writes save, RDT, HRESULT, and disposal markers to
`%TEMP%\ModernFormsNextDesignerEditor-<visual-studio-pid>.log`.

The host derives the owning Visual Studio PID from the supplied parent HWND and monitors that exact
process. If Visual Studio exits unexpectedly, the child closes instead of remaining orphaned. If
Visual Studio recreates the pane HWND while docking or changing display state, the adapter first
hides and parks the child under `HWND_MESSAGE`. The new pane handle is then sent to the same host,
which reparents and resizes the same Designer HWND. This prevents destruction of the old parent
from destroying the child, avoids a top-level flash, and preserves the live document/session.

## Resize, DPI, and focus

Visual Studio owns the editor-pane bounds and chrome. Resize and parent-DPI notifications reapply
the pane's current device-pixel client rectangle to the child HWND at `(0, 0)`. The host reads the
native parent client bounds and passes those physical values directly to `SetWindowPos`; it does not
apply a second logical-DPI conversion. Visibility is synchronized explicitly and also follows the
normal Windows child/ancestor visibility rule.

Windows documents that cross-process `SetParent` can reset the child process DPI-awareness context.
The host therefore completes application DPI setup before creating the form, logs both parent and
child DPI after every attach/resize, and treats the parent client rectangle as authoritative device
pixels. Parent-DPI notifications request a fresh native bounds read instead of scaling the previous
child size, which avoids double scaling after a monitor transition.

Focus entering or clicking the pane is requested over the private IPC endpoint and executed on the
Designer UI thread. The VSIX must not call cross-process `SetFocus` directly because
[`SetFocus`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setfocus) requires the
target to be attached to the caller's input queue. Pointer and keyboard input still flow through
the existing ModernFormsNext input pipeline after the child receives focus.

The in-process lifecycle controller uses explicit `Detached`, `Attached`, `Faulted`, and `Disposed`
states. Failed attachment rolls back partial parent/style changes. Resize and focus failures are
captured as diagnostics instead of unwinding synchronous Visual Studio window notifications.

## Save, reload, dirty state, and close

The editor pane and its host use a private, document-scoped named pipe for document and native-host
control operations:

- `OPEN` reloads or reattaches the pane's canonical `.mfdesign` document;
- `SAVE` invokes the existing Designer save and generated-code path and returns a typed
  `SAVED`, `CANCELED`, or `FAILED` result instead of collapsing every refusal into `E_FAIL`;
- `DIRTY` lets Visual Studio participate in save prompts;
- `DISCARD` applies Visual Studio's explicit Don't Save decision through the existing issue #41
  recovery cleanup before the host is shut down;
- `SHUTDOWN` closes the owned host after Visual Studio has resolved the document close decision;
- `ATTACH` applies a recreated Visual Studio pane HWND;
- `PARK` hides the child and moves it to the message-only window tree before parent destruction;
- `RESIZE` reapplies the current parent client rectangle;
- `SHOW` and `HIDE` synchronize native visibility;
- `FOCUS` requests focus from the Designer UI thread.

This channel is an internal VSIX/host implementation detail, not the `.mfdesign` format and not a
public runtime API. The Designer UI thread executes document mutations. Malformed or unknown
commands return an error and do not terminate the listener.

Save, autosave recovery, and external-change handling remain owned by the existing Designer
persistence coordinator from issue #41. The VSIX does not create a second recovery store or a
parallel file watcher. A short UI-thread timer observes only the host's authoritative dirty value.
When that value changes, the pane calls `IVsRunningDocumentTable4.UpdateDirtyState` for its RDT
cookie. Visual Studio then refreshes the tab-caption asterisk and its standard Save and Save All
command state by calling `IsDocDataDirty`; no global Ctrl+S handler is installed. If a query is
temporarily unavailable, the last confirmed value is retained. A host exit publishes dirty
conservatively so Visual Studio cannot silently discard work, while issue #41 recovery artifacts
remain available when the document is reopened.

`SaveDocData` follows the VSSDK editor contract. Ordinary and silent saves first call
`IVsQueryEditQuerySave2.QuerySaveFile`, then delegate to `IVsUIShell.SaveDocDataToFile`, which calls
the pane's `IPersistFileFormat.Save`. That final method sends exactly one `SAVE` request to the
host, so canonical `.mfdesign` persistence, generated-code output, recovery cleanup, external
conflict checks, and the issue #33 transaction guard all remain in the existing Designer
persistence coordinator. A successful save updates the saved history revision and publishes clean
state to the RDT. An active gesture, unresolved conflict, canceled query-save, or other deliberate
refusal reports save canceled (`OLE_E_PROMPTSAVECANCELLED`, `0x8004000C`) rather than the generic
`E_FAIL` (`0x80004005`). The pane stays open and dirty.

`IVsPersistDocData.Close` does not dispose the child process. Visual Studio can call it while the
document view is still unwinding, and `WindowPane` already owns disposal of its `Window` object.
The later, idempotent pane disposal unsubscribes dirty callbacks and performs exactly one bounded
host shutdown. A canceled close never reaches that disposal path.

## View Designer and project metadata

The package registers both its explicit **View ModernFormsNext Designer** command and Visual
Studio's standard `ViewForm` command used by **View Designer** and Shift+F7. The standard command is
claimed only when conservative detection says the selected/active file is a supported
ModernFormsNext Form or UserControl. For every other file it reports unsupported so command routing
continues to the normal C#, WinForms, or other project-system handler.

The `.mfdesign` editor factory is single-view. It registers `LOGVIEWID_Designer` with an empty
physical-view string and maps both `LOGVIEWID_Primary` and `LOGVIEWID_Designer` to that same view.
This registration is required before `OpenDocumentWithSpecificEditor` can resolve the View Designer
request; the factory does not implement `IVsMultiViewDocumentView` or create a secondary tab.

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
routing, item-template shape, and packaged nesting target. A real-process Win32 regression also
asserts child/extended styles, parent and owner relationships, top-level enumeration exclusion,
exact parent-relative bounds, visibility, host-thread focus, parent recreation, and two independent
hosts. The following behavior still requires an observed Experimental Instance:

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
7. Modify a property, verify the tab gains `*`, use Ctrl+S and Visual Studio **Save**, and verify
   both `.mfdesign` and generated `.Designer.cs` are updated and `*` disappears. Change the file
   externally and verify the existing #41 conflict or reload workflow remains authoritative.
8. Open two Designer documents, make both dirty, use **Save All**, and verify both become clean.
   Repeat for a directly opened `.mfdesign` and `MainForm.cs [Design]`.
9. Close clean and dirty documents. For dirty close exercise Save, Don't Save, and Cancel; verify
   Cancel leaves the pane and host alive, while successful close shuts down the host once and never
   displays an unspecified-error dialog.
10. Terminate the exact child host process. Verify only its pane shows a failure, then close/reopen
   the document and verify a new host attaches.
11. Temporarily remove the packaged host executable and verify the pane reports the missing path;
    restore it and verify close/reopen succeeds.

Do not report this checklist as passed unless each behavior was observed in Visual Studio. An
automated build or host-contract test is not interactive evidence.
