# Scroll layout consistency (#130)

## Contract and usage

`ScrollableControl.DisplayRectangle` is the logical layout viewport, inset by
scrollbars and presentation padding, with its origin translated by the current
scroll offset. Layout engines must place children relative to this origin.
Its size remains the MFN viewport size; `CommonProperties.LayoutBounds` separately
describes the content extent for built-in flow/table engines. Custom engines that
do not publish an extent use the existing child-bounds calculation.
Custom layouts that already subtract a scroll offset as a workaround must not
subtract it again from the translated display origin.

The invariant, for each active axis, is:

```text
canonical offset = scrollbar.Value - scrollbar.Minimum
display origin = unscrolled display origin - canonical offset
laid-out child position = unscrolled layout position - canonical offset
```

Read and change control state on the UI thread. A view can preserve its position
through ordinary sibling-panel navigation without special scroll handling:

```csharp
historyView.Visible = true;
historyView.BringToFront();
downloadsView.Visible = false;

downloadsView.Visible = true;
downloadsView.BringToFront();
historyView.Visible = false;
```

A valid offset survives visibility changes and repeated `PerformLayout()` calls.
Content or viewport changes can reduce the range: clamping must move the children
by the same delta as the canonical offset, including under a hidden ancestor.
When content fits, the offset becomes zero and the scrollbar disappears.
Accessibility uses the unscrolled `LogicalScrollViewport` for clipping and reveal;
children already contain the scroll displacement in their bounds. Screen-space
conversion therefore does not subtract the offset again, including for nested
scrollable controls.

## Confirmed cause and implementation

The initial native regression failed on framework HEAD
`edb9bed983d334234f6b244c5a64f316d680d5ee`. It used an actual MFN `Form`, sibling
views and fifteen plain panels with height 150 and bottom margin 14. No application
code or TestHost was involved.

| Stage | Value | Canonical Y | First child Top |
| --- | ---: | ---: | ---: |
| Initial | 0 | 0 | 0 |
| Scrolled | 480 | 480 | -480 |
| Hidden | 480 | 480 | 0 |
| Shown | 480 | 480 | 0 |
| PerformLayout | 480 | 480 | 0 |
| Native wheel after show | 485 | 485 | -5 |

`ScrollWindow` correctly applied a delta to child bounds and retained
`scroll_position`. However, `DisplayRectangle` omitted that offset.
`VisibleChanged -> Recalculate(true) -> ResumeLayout(true)` could run flow layout
again; `FlowLayout.LayoutCore` then recreated child bounds at the unscrolled origin.
The range was unchanged, so no `ValueChanged` event repaired the displacement.
Further wheel input applied only the next delta to already incorrect bounds.

The fix puts the canonical translation in the shared `DisplayRectangle` contract.
`ScrollWindow` remains the displacement path; layout does not apply a second
post-layout correction. The layout order and visibility event wiring are unchanged.
Local scrollbar visibility is used for viewport sizing and value synchronization,
so hiding an ancestor does not enlarge the layout viewport or suppress clamping.
Offset removal always uses `ScrollWindow`, rather than clearing state alone.

Flow's reverse-direction proxy subtracts two coordinates that both contain the
offset. It therefore restores the cancelled translation for `RightToLeft` and
`BottomUp`, using the existing internal canonical position. Non-wrapping flow also
avoids overflowing its measurement width when the origin is negative. No new
public member, framework dependency, or backend coupling is introduced.

Final review also reproduced a resize/clamping defect for `ScrollableControl`,
`Panel` and `UserControl`: moving children while reducing the range reinitialized
right/bottom anchor distances against the new container size before DefaultLayout
could apply the resize. During that existing Bounds-layout adjustment, displacement
now uses layout bounds (`BoundsSpecified.None`) and preserves the anchor cache.
Ordinary scroll input and custom layout engines retain their existing movement path.
The comparison tests cover both a smaller nonzero range and complete scrollbar
disappearance, checking every child against an equivalent unscrolled container.

## WinForms comparison

The reference reviewed on 2026-09-22 was `dotnet/winforms` main at
`799c47a1e0a69b9d0e15135e1b813a16677964e9`:

- [ScrollableControl](https://github.com/dotnet/winforms/blob/799c47a1e0a69b9d0e15135e1b813a16677964e9/src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollableControl.cs)
  exposes the scroll origin through `DisplayRectangle`, clamps through
  `SetDisplayRectLocation`, and treats local visibility separately from ancestors.
- [FlowLayoutPanel](https://github.com/dotnet/winforms/blob/799c47a1e0a69b9d0e15135e1b813a16677964e9/src/System.Windows.Forms/System/Windows/Forms/Panels/FlowLayoutPanel.cs)
  selects the flow engine.
- [FlowLayout](https://github.com/dotnet/winforms/blob/799c47a1e0a69b9d0e15135e1b813a16677964e9/src/System.Windows.Forms/System/Windows/Forms/Layout/FlowLayout.cs)
  lays out from the display rectangle and reports a translation-independent extent.
- [ContainerProxy](https://github.com/dotnet/winforms/blob/799c47a1e0a69b9d0e15135e1b813a16677964e9/src/System.Windows.Forms/System/Windows/Forms/Layout/FlowLayout.ContainerProxy.cs)
  restores the scroll translation lost during mirroring.
- [DefaultLayout](https://github.com/dotnet/winforms/blob/799c47a1e0a69b9d0e15135e1b813a16677964e9/src/System.Windows.Forms/System/Windows/Forms/Layout/DefaultLayout.cs)
  applies layout bounds with `BoundsSpecified.None` so anchor information is not
  reinitialized. Its original anchor algorithm stores positions relative to the
  display origin; the newer optional AnchorLayoutV2 is not part of this change.

MFN retains its own viewport-sized display rectangle, inclusive movement maximum,
custom scrollbars and managed child displacement. It does not adopt WinForms'
virtual display size, HWND child scrolling, or double-layout policy. The remaining
container RTL TODO is separate from the supported reverse flow directions.

## Regression coverage

`WindowsNativeScrollLayoutTests` runs `UiAutomationHost --scroll-layout` in an
isolated process. After HWND creation the client is set to exactly 800 by 500.
Its 64 checkpoints record Value, Maximum, LargeChange, canonical position,
first-child bounds, DisplayRectangle and LayoutBounds. Assertions inspect all
children and ensure populated content still intersects the viewport.

The native scenario covers:

- The minimal sibling-view hide/show sequence at offset 480.
- HWND `WM_MOUSEWHEEL`, thumb press/three drag moves/release, and track/page click
  through the normal Windows backend and control input routing.
- Two view round trips at zero, middle, near maximum and maximum, each with three
  explicit layouts; wheel at maximum and wheel back from maximum.
- Resize clamping; content shrinking while visible and while hidden; a fitting
  child; complete removal; scrollbar disappearance and reappearance.
- Accessibility percentage scrolling and `ScrollIntoView`, including repeated
  reveal after layout and stable accessible bounds, identity and offscreen state.

Synchronous HWND messages and dispatcher scheduling provide ordering; there are
no layout sleeps. The process timeout only bounds failure/cleanup.

`ScrollableControlRegressionTests` adds all four flow directions with padding and
margins, wrapping under a hidden ancestor, positive AutoSize measurement constraints
at a negative origin, and two-axis checks for table/default/custom engines.
Final-review coverage also compares Dock, six anchor combinations,
and AutoSize across ordinary scrollable controls, panels and user controls after
scrolling, hide/show and resize/clamping. Nested reveal checks both viewport offsets,
child bounds and screen-space accessible bounds through repeated relayouts.
Existing suites cover nested wheel routing and boundary bubbling, dynamic ranges,
DPI, accessibility actions, `ScrollWindow` reentry and touch input.

Run the native regression explicitly:

```powershell
dotnet test .\ModernFormsNext.WindowKit.Backend.Windows.Tests\ModernFormsNext.WindowKit.Backend.Windows.Tests.csproj --configuration Debug --filter FullyQualifiedName~WindowsNativeScrollLayoutTests -m:1 /p:UseSharedCompilation=false
```

The automated HWND test is decisive evidence for #130. It injects native messages;
it does not claim physical mouse, screen-reader, or Android-device acceptance.

## Local validation, 2026-09-22

The working diff on the HEAD above passed:

| Command / suite | Result |
| --- | --- |
| `dotnet restore .\ModernFormsNext.slnx` | Passed |
| Full solution Debug build | 0 warnings, 0 errors |
| Full solution Release build | 0 warnings, 0 errors |
| Focused core scroll/layout/touch tests | 190 passed, 0 failed, 0 skipped |
| Full solution Debug tests | 3601 passed, 0 failed, 0 skipped |
| Full solution Release tests | 3601 passed, 0 failed, 0 skipped |
| Windows backend suite, including native regression | 298 passed |
| Core framework suite | 1463 passed |
| Testing/accessibility suite | 532 passed |
| `git diff --check` | Passed |

Both builds used `--no-restore -m:1 /p:UseSharedCompilation=false
/p:EnableWindowsTargeting=true`. Full tests used both Debug and Release with
`--no-restore --no-build -m:1 /p:UseSharedCompilation=false`. Other solution suites
also passed: Automation 160, Windows Automation 66, CrossPlatform sample 29,
Designer 669, VSIX tests 26, Android backend unit tests 358.

The final native run reported `Maximum=1960`, `LargeChange=500` and:

| Stage | Value / canonical Y | First child Top / display Y |
| --- | ---: | ---: |
| Scrolled, hidden, shown, PerformLayout | 480 | -480 |
| Wheel after show | 485 | -485 |

During the initial implementation, ControlGallery was launched with `dotnet run --project
.\samples\ControlGallery\ControlGallery.csproj --configuration Debug --no-build
--no-restore`. Captured FlowLayoutPanel frames were visually inspected after native
wheel input and Add/Remove operations; visible content retained its scroll position.
This was automated input with image inspection, not physical mouse testing.
That visual check preceded the final anchor/clamping correction; it was not repeated
during final review. The final diff passed both complete automated suites above.
Template verification was not needed; startup/templates were unchanged.
Physical Android/screen-reader acceptance was not executed.
