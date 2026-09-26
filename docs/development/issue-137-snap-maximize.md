# Issue #137: native maximize geometry

Baseline: `dc55839b061485121a8e5dd8092ea2a79e4d7572` (1.11.1).
Scope: Windows custom non-client geometry. No public API, package version,
FormTitleBar layout, sample content or release changes.

## Investigation before implementation

The title-bar button assigns `Form.WindowState`, which delegates to
`WindowImpl.WindowState` and `ShowWindow(WindowState.Maximized)`. That path calls
native `ShowWindow(SW_MAXIMIZE)` and then `MaximizeWithoutCoveringTaskbar`, which
uses `MonitorFromWindow`, `GetMonitorInfo(rcWork)` and a second `SetWindowPos`.

Dragging `FormTitleBar` calls `Form.BeginMoveDrag`, releases backend mouse capture,
and enters Windows' move loop through `DefWindowProc(WM_NCLBUTTONDOWN, HTCAPTION)`.
Windows performs Snap maximization without calling the managed WindowState setter
or the second positioning helper. `SC_MAXIMIZE` exposes the same discrepancy.

The normal managed Form removes WS_CAPTION/WS_SYSMENU, retains the resizable
WS_THICKFRAME/WS_MAXIMIZEBOX styles, and requests extended client area with
NoChrome. `WM_NCCALCSIZE` previously returned zero without changing the proposed
rectangle, making the native resize frame part of the rendered client area.
Windows' normal maximized outer bounds include invisible offscreen frame pixels;
those pixels must not become the custom title bar's client origin.

An isolated probe on an owned HWND recorded input/output payloads for
WM_GETMINMAXINFO, WM_WINDOWPOSCHANGING/CHANGED, WM_NCCALCSIZE, WM_MOVE, WM_SIZE,
WM_DPICHANGED and WM_ENTERSIZEMOVE/EXITSIZEMOVE. At 100% DPI, before the fix:

| Entry point | Outer rectangle | Client screen origin / physical size |
| --- | --- | --- |
| Managed maximize | (0,0)-(1920,1080) | (0,0), 1920x1080 |
| Native SC_MAXIMIZE | (-7,-7)-(1927,1087) | (-7,-7), 1934x1094 |
| Actual pointer drag to top edge | (-7,-7)-(1927,1087) | (-7,-7), 1934x1094 |

The native trace starts with default MINMAXINFO size 1934x1094 and position
(-7,-7). WM_WINDOWPOSCHANGING passes through DefWindowProc, then WM_NCCALCSIZE
leaves that rectangle unchanged. DefWindowProc(WM_WINDOWPOSCHANGED) generates
WM_MOVE and WM_SIZE(Maximized), which report the oversized client. Only the
managed path subsequently corrects the entire HWND with SetWindowPos.
An executable native regression test failed at the SC_MAXIMIZE comparison before
production edits, with precisely the same (-7,-7) discrepancy.

WM_GETMINMAXINFO currently converts logical min/max tracking limits to physical
pixels; it does not set maximized position/size. WM_SIZE divides the physical
client size by RenderScaling once. WM_DPICHANGED stores DPI/96, applies the
physical suggested rectangle directly, and then notifies layout. The existing
PMv2 bootstrap and these conversions are not the cause of this issue.

## Fix and Windows contracts

The common fix belongs in WM_NCCALCSIZE, after Windows has selected the target
monitor and calculated its native outer bounds. For a maximized custom-client
top-level HWND, intersect the proposed client rectangle with that monitor's
physical `rcWork`. Select the monitor from the **proposed rectangle**, not the
previous HWND location, so monitor transitions use their destination.

Keep the outer frame, placement and native maximize/restore operation owned by
Windows. Remove the extra managed-only SetWindowPos operation. Both paths now
have identical outer rectangles and visible client geometry. It is intentional
for GetWindowRect to include invisible offscreen frame pixels; ClientToScreen(0,0)
and GetClientRect identify the visible content, including the managed title bar.

Do not set MINMAXINFO to the destination monitor's dimensions: Windows can add
the size difference from the primary monitor again. A two-monitor experiment
(1920x1080/100% and 5120x2880/225%) confirmed that behavior. Calculating the client
area after native placement avoids that heuristic and hard-coded border widths.

The calculation uses only physical virtual-screen coordinates, preserves negative
origins, respects smaller constrained windows and never enlarges them. Normal,
minimized, fullscreen and WS_CHILD windows are excluded. Fully native decorations
retain DefWindowProc's non-client calculation. Only the first RECT in either form
of the WM_NCCALCSIZE payload is changed; preservation rectangles and WINDOWPOS
data remain untouched. MonitorFromRect now declares its LPCRECT argument by
readonly reference, matching the native pointer contract on x86 as well as x64.

Relevant primary sources:

- [WM_NCCALCSIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize)
- [MonitorFromRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromrect)
- [WM_WINDOWPOSCHANGED](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-windowposchanged)
- [MINMAXINFO monitor adjustment](https://devblogs.microsoft.com/oldnewthing/20150501-00/?p=44964/)

## Verification

The Windows regression suite covers physical rectangle calculations at
100/125/150/175/200/225%, taskbars on each edge, positive/negative monitor origins,
constrained windows and disjoint transient rectangles. Native tests cover all
attached monitors, repeated managed/native maximize and restore in both orders,
system/custom/border-only decorations, resizable and nonresizable styles,
fullscreen, normal move/resize, topmost preservation, injected WM_DPICHANGED and
both NCCALCSIZE payload layouts. Injected DPI tests do not change monitor DPI.

Actual pointer-drag acceptance on the two attached monitors produced:

| Monitor | Entry points compared | Outer rectangle | Client origin / size |
| --- | --- | --- | --- |
| 1920x1080, 100% | Managed, SC_MAXIMIZE, Aero Snap | (-7,-7)-(1927,1087) | (0,0), 1920x1080 |
| 5120x2880, 225% | Managed, SC_MAXIMIZE, Aero Snap | (1907,-13)-(7053,2887) | (1920,0), 5120x2880 |

Restore returned to the original normal rectangle on both monitors. Local probe
source, before/after message traces, build logs and TRX results are retained under
the ignored `artifacts/issue-137/` directory.

ControlGallery was launched from `samples/ControlGallery` so its relative image
assets were available. An external acceptance script clicked the real title-bar
button through the native pointer-message path, performed an actual desktop
pointer drag to the top edge, restored with the button, then maximized again.
Both monitors passed geometry and restore checks. Full-client PNGs for button
versus Snap were byte-identical on each monitor (SHA-256):

- 100%: `b7bc98c83d7e9081e29bf3d21ff173a319d0979a238b354445f538a384330095`
- 225%: `ccbb694838bf1eace81915f2cafd549d5c15c3570b4e72f8206b018d5c4ef5e1`

Visual inspection confirmed that the title-bar icon stays entirely visible. This
was automated native desktop acceptance plus screenshot inspection, not a claim
that a person manually performed the gestures. The script restored the pointer
and closed its ControlGallery instance. A capture retry was needed after one
desktop gesture did not engage Snap; the final run passed without forcing repaint.

Physical 125/150/175/200% display configurations and taskbar
relocation are **NOT EXECUTED**; they have deterministic geometry coverage only.
No display settings were changed. Template verification is not needed because
startup, templates and the default application are unchanged.

Validation commands (PowerShell, repository root unless noted):

```powershell
dotnet restore ModernFormsNext.slnx -m:1
dotnet build ModernFormsNext.slnx -c Debug --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet build ModernFormsNext.slnx -c Release --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet test ModernFormsNext.WindowKit.Backend.Windows.Tests -c Debug --no-restore --filter FullyQualifiedName~WindowMaximizeGeometryTests -m:1 /p:UseSharedCompilation=false
dotnet test ModernFormsNext.slnx -c Debug --no-restore --no-build -m:1 /p:UseSharedCompilation=false
# Working directory: samples/ControlGallery
dotnet run --project ControlGallery.csproj -c Debug --no-build --no-restore
```

Restore and both full solution builds passed with zero warnings/errors. The
first Debug attempt found an old local Release VSIX (1.11.0) during cross-config
package validation; rebuilding Release refreshed it to the repository's existing
1.11.1, and the subsequent Debug build passed. No source metadata was changed.
Focused maximize regression tests: 23 passed, zero skipped.
Full Debug solution suite: **3625 passed, zero failed, zero skipped**, including
321 Windows backend tests and the existing native high-DPI/paint/input scenarios.
`git diff --check` passed. The working change consists of three Windows backend
files, one regression-test file and these two documentation files. No generated
outputs or unrelated local configuration are included.
