# Control finalization and ComboBox ownership audit (#149)

Audited on 2026-09-27 against `b574494e75c0a0f0b044623a20b6d5b5d06bacd9`.
Scope: [issue #149](https://github.com/ProGraMajster/ModernFormsNext/issues/149)
and directly equivalent managed/UI cleanup in `Dispose(false)`. Issues #144–#147,
WinForms API parity, package versions and general window-lifetime redesign are outside this change.

## Failure mechanism

Every `Control` inherits the `Control` finalizer. It invokes the most-derived
`Dispose(false)` on the GC finalizer thread. Before this fix, `ComboBox` called
`popup.Close()` and public `popup_listbox.Dispose()` regardless of `disposing`.
Public `Dispose()` enters `Control.Dispose(true)`, which starts a text-input tree
change. The list's `Parent` can still be a popup `ControlAdapter`, even after native
closure. That adapter leads to the popup's `ControlTextInputHost`.

`ControlTextInputHost.BeginTreeChange` verifies the owning thread **before**
checking for an active session. A closed host or a ListBox with no editable text
does not bypass that check. `Component.Disposed` observers can independently call
`Refresh`, producing additional failures. The aggregate escapes the finalizer and
terminates the process. Finalizer order is unspecified, so another control's
earlier finalization can mask the problem by marking the list disposed first.

The base implementation had an additional unsafe path: `Control.Dispose(false)`
released input/command bindings, effects, animations, resource/brush subscriptions,
back buffers, children and accessibility notifications. Thus guarding only the
ComboBox would leave other UI callbacks reachable from finalization.

## Disposal contract after the fix

- `Control.Dispose(false)` delegates only to `Component.Dispose(false)`. It does
  not traverse children, mutate managed UI state, notify observers, access IME or
  post cleanup to a dispatcher. `Control` holds no directly owned raw native handle;
  SkiaSharp bitmap/path wrappers retain their own native finalization responsibility.
- Explicit `Control.Dispose()` remains synchronous on the owning UI thread. It
  releases managed resources and calls public `Dispose()` on owned children, also
  suppressing their finalizers. Repeated disposal does not repeat `Disposed`.
- Explicit ComboBox disposal retires popup ownership before callbacks, detaches
  both list subscriptions, closes the popup, disposes its exclusively owned adapter
  tree (including implicit scroll controls) and disposes the owned list if still needed,
  then completes base cleanup. Cleanup continues after a failing popup observer;
  original errors are reported after independent cleanup, with multiple errors
  aggregated. Reentrant disposal cannot repeat cleanup or reopen the popup.
- Hiding a popup still retains it for reuse. Closing destroys its backend and
  retires text input. Native `PopupWindow.Close()` does not dispose borrowed controls;
  the ComboBox remains the owner of its list. A hosting layer may already have
  disposed that list during close, which is handled without another list disposal.
- Removing a control uses `ControlCollection.Remove` / `AssignParent`, including
  text-session retirement and normal layout/input notifications. Removal is not
  disposal. Finalization does not attempt this UI operation. This change does not
  add automatic removal to `Control.Dispose()`.

Custom controls should follow this pattern; callers dispose them on the UI thread:

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        // Release this control's managed subscriptions/resources here.
        // Children already in Controls are disposed by Control.Dispose.
    }
    base.Dispose(disposing);
}
```

Finalization is not an alternative to explicit UI cleanup. A live event publisher
or open native window can retain a control, preventing collection altogether.
Skipping managed cleanup from a finalizer does not promise to release live subscriptions.

## Audit inventory

Searched framework, WindowKit, backends, Designer, VS hosting, samples and tests for
all spacing variants of `Dispose(bool)`, finalizers and `Dispose(false)`; followed
child-disposal, popup/window close, text-input/IME, timer, event and dispatcher calls.

| Area | Finding and disposition |
| --- | --- |
| `Control` | Fixed unconditional managed cleanup/tree traversal; documented UI-thread and override requirements. |
| `ComboBox` | Fixed unconditional popup/list cleanup; made explicit ownership idempotent and resilient to reentrant/throwing close callbacks; detached retained Items callbacks. |
| `Button` | Fixed unconditional `CommandSource.Dispose`; custom `ICommand.CanExecuteChanged` removal accessors can execute arbitrary UI code. |
| `Shape`, `Polygon`, `Polyline` | Guarded geometry/point subscriptions and managed SKPath cache disposal with `disposing`. SKPath owns its native handle. |
| Android `AndroidAccessibilityNodeProvider` | Guarded dispatcher-based detach and managed accessibility-session disposal. Its Java peer finalizer previously dispatched UI work and removed semantic subscriptions. Native peer cleanup still runs through `base.Dispose(disposing)`. |
| `DataGridView`, `DateTimePicker`, nested `CalendarPopup`, `TextBox` | Already guard editor/composition/popup cleanup. Keep existing error aggregation and ownership rules. |
| `MenuBase`, `MenuDropDown`, `Ribbon`, `ToolBar` | Already guard menu activation, command items, borrowed contexts, tooltip timers/popups. |
| `DocumentViewer`, `MarkdownEditor`, `PictureBox`, `PrintPreviewControl`, `Label`, `LinkLabel` | Already guard managed caches, child editors, image operations/timers and visual subscriptions. |
| `BindingNavigator`, `BindingSource`, `HelpProvider`, `ImageList`, `NotifyIcon`, `NotifyIconContextMenu`, `NotifyIconMenuItem`, `Timer`, `ToolTip` | Managed teardown already guarded. `BindingSource` also records a terminal value-type state flag on finalization; it does not invoke UI callbacks there. |
| `WindowBase`, `PopupWindow` | Managed input bindings, insets and parent event detach already guarded. `WindowBase` derives from Component, not Control. No ownership redesign here. |
| Designer shell, safety banner, toolbox; VS host/VSIX overrides; sample overrides | Managed cleanup already guarded. |
| `BclStorageFile`, `BclStorageFolder` | Empty virtual disposal bodies; finalizers do not access UI. |
| `MicroComProxyBase` | Native COM reference release with explicit captured-context handling; distinct native ownership contract, unchanged. |
| `UnmanagedBlob`, debug `GCThreadDetector` | Native allocation release / diagnostic finalizer, no control-tree teardown; unchanged. |
| Windows `WindowFramebuffer` | Finalizer deallocates a native blob rather than disposing controls. Partial-construction concern below is a separate issue. |
| Android `AndroidSkiaHostView`, `AndroidChoreographerAnimationFrameSource` | Existing dedicated finalization paths with frame-dispatch/lifetime policy; deeper Java-peer audit below, unchanged. |

## Regression evidence

`ControlDisposalTests` covers off-thread `Dispose(false)` with a real managed text
host/session, open/hidden/closed/never-opened/removed ComboBoxes, owner close, repeated
explicit disposal, subscription retention, composition retirement, binding/child
preservation, custom command event accessors, reentrant/throwing popup close and
attempted reopening during/after disposal. `ShapeGeometryTests` additionally checks
that simulated finalization leaves live point/geometry subscriptions and cached paths
untouched, and that explicit disposal releases those paths.

`WindowsNativeComboDisposalTests` launches the existing `UiAutomationHost` in its
own process. The scenario creates and uses a native Form/ComboBox/popup, starts a
text-input composition, closes the popup and owner, abandons references, yields to
pending native work and forces GC/finalization. It requires collection plus an
observed `Dispose(false)` on a different thread; absence of a crash alone is not
success. It also checks normal list selection, open/hidden popup disposal, native
HWND destruction, single list disposal and stale-session revocation.

The initial deterministic run against unchanged framework code reproduced the
owning-UI-thread exception and also terminated its test host through the actual
`Control.Finalize -> ComboBox.Dispose -> ListBox.Dispose -> ControlTextInputHost`
path. The native GC test deliberately does not require a particular finalizer order;
the deterministic tests protect the precise unsafe mechanism independently.

A negative mutation check removed only the ComboBox finalizer guard, rebuilt the
native host and reran its test. The child process terminated with exit code
`-532462766` and the expected `ControlTextInputHost` owning-thread exception from
`Control.Finalize`. Restoring the guard made the same native test pass. This
confirms that the process regression detects the crash rather than merely reaching
an unrelated successful GC cycle.

## Separate follow-up proposals

These are proposals, not newly opened issues or implemented lifecycle changes:

1. **Clarify and test native window/control-tree ownership on Close versus Dispose.**
   `WindowBase.Close` retires backend/text-input/command lifetimes but does not dispose
   the caller's entire managed control tree. Headless host cleanup has stronger ownership.
   Changing native Form ownership requires compatibility/design review; #149 makes
   subsequent finalization safe without redefining that contract.
2. **Audit Android Java-peer finalization independently of UI availability.**
   `AndroidSkiaHostView.Dispose(false)` still clears managed connection callbacks and
   releases animation registration under its existing catch-all policy; Choreographer
   teardown posts native frame removal. Verify Java/managed collection, disposed Handler,
   Activity destruction and a blocked UI loop on an Android host before changing that policy.
3. **Make Windows framebuffer finalization safe after partial construction.**
   `WindowFramebuffer` calls `Deallocate()` from its finalizer and dereferences
   `_bitmapBlob`; dimension validation/allocation can throw before assignment. This is a
   separate allocation/partial-construction failure, not the ComboBox/UI-thread path.
4. **Investigate animation tick/cancellation terminal-state races.** During a repeat
   full Debug run, the existing `ControlAnimationLifecycleTests.DisposingControlCancelsItsOwnedDefaultAnimation`
   failed once with `Only a terminal animation can release its retained references`
   in `AnimationEntry.FinishTerminal`, reached from `AnimationScheduler.CancelAll`.
   `ProcessTick` checks terminal state and subsequently writes `Running` outside the
   scheduler lock; the fallback dispatcher can run ticks on the timer thread before
   a native UI dispatcher exists. This is a plausible concurrent overwrite of cancellation.
   The scheduler, entry, dispatcher and original test sources are unchanged by #149.
   The three lifecycle tests passed on isolated rerun. A separate checkout of the
   exact base revision passed all 1464 existing Core tests; two bounded stress probes
   did not reproduce the exception (500,000 default-scheduler iterations and 5,000,000
   iterations with a 1 ms timer and the existing scheduler override). Therefore its
   pre-existing nature is a source-based diagnosis, not a reproduced baseline failure.
   No scheduler workaround or test exclusion is part of this change. Preserve this
   observation for a dedicated concurrency investigation even if reruns pass.

Native Windows automation is distinct from manual ControlGallery verification.
Android native/device finalization and template/UI appearance are not established
by these regression tests. No template or visible presentation behavior is changed.

## Changed files

- `ModernFormsNext/Control.cs`: finalizer boundary, explicit child finalizer suppression,
  idempotent disposal notification and XML lifetime documentation.
- `ModernFormsNext/ComboBox.cs`: owned popup/adapter/list teardown, subscription release,
  callback failure handling, reentrancy and documented post-disposal opening rejection.
- `ModernFormsNext/Button.cs`, `Shape.cs`, `Polygon.cs`, `Polyline.cs`: analogous managed cleanup guards.
- `ModernFormsNext.WindowKit.Backend.Android/Platform/Accessibility/AndroidAccessibilityNodeProvider.cs`:
  Java-peer finalization guard for managed/dispatcher cleanup.
- `ModernFormsNext.Testing.Tests/ControlDisposalTests.cs`: 17 deterministic lifecycle cases.
- `ModernFormsNext.Tests/ShapeGeometryTests.cs`: two geometry/finalization cases.
- `ModernFormsNext.WindowKit.Backend.Windows.Tests/WindowsNativeComboDisposalTests.cs`:
  isolated native-process regression.
- `ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost/ComboDisposalScenario.cs`
  and `Program.cs`: real Windows scenario and its command-line entry point.
- `docs/application-lifecycle.md` and this audit: user-facing lifetime guidance,
  failure analysis, inventory, verification and bounded follow-up proposals.

## Validation commands and limits

Run from the feature worktree, using SDK 10.0.401:

```powershell
dotnet restore .\ModernFormsNext.slnx -m:1 /p:UseSharedCompilation=false
dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore /p:EnableWindowsTargeting=true -m:1 /p:UseSharedCompilation=false
dotnet build .\ModernFormsNext.slnx --configuration Release --no-restore --verbosity normal -m:1 /p:UseSharedCompilation=false
dotnet test .\ModernFormsNext.slnx --configuration Debug --no-build --no-restore -m:1 /p:UseSharedCompilation=false --logger trx --results-directory artifacts\issue-149\debug-confirmation-tests
dotnet test .\ModernFormsNext.Testing.Tests\ModernFormsNext.Testing.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~ControlDisposalTests
dotnet test .\ModernFormsNext.Tests\ModernFormsNext.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~FinalizerPathPreservesPointAndGeometrySubscriptionsUntilExplicitDisposal
dotnet test .\ModernFormsNext.WindowKit.Backend.Windows.Tests\ModernFormsNext.WindowKit.Backend.Windows.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~WindowsNativeComboDisposalTests
git diff --check
```

Both solution configurations built with **0 warnings / 0 errors**, including the
native Android target. All **20/20 new cases** passed in Release (17 managed lifecycle,
2 shape, 1 native Windows process). The native process regression also passed Debug
and rejected the temporary guard-removal mutation as described above.

The final full Debug confirmation run passed **3645/3645**, with zero failures and
zero skipped tests. This includes all 20 new cases. An earlier full run passed too;
the intermediate run had the single animation failure documented in follow-up 4.
The final run did not exclude or change that test. `git diff --check` passed.
TRX evidence is under `artifacts/issue-149/debug-confirmation-tests` and
`artifacts/issue-149/release-regressions`; the baseline comparison logs/probe are
retained under `artifacts/issue-149/baseline-comparison` (local, ignored artifacts).

Manual ControlGallery appearance/input review and Android device/emulator finalization:
**NOT EXECUTED**. Native Windows regression automation was executed. No template/startup
change required manual template verification. No API removal, dependency, version or
package-metadata change is included. These are local validation results; the pull request
tracks subsequent CI and merge status separately.
