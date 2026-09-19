# Partial redraw regression after PR #122

## Audit and reproduction

Baseline: master `ec88634a5f4fead2d4a4cdb3b62f2616b1cf3951`, after PR #122.
The supplied recordings show shifted copies of entire window regions as well as
apparent row/animation ghosts. The fix must preserve the logical DPI model and
bounded hover repaint introduced by #120.

An acceptance probe hosts the actual ControlGallery MainForm, navigates to its
DataGridView and Animations and Interaction Effects pages, and captures both the
native framebuffer and the displayed client pixels. It compares a partial frame
with a forced full repaint, separately from comparing screen with framebuffer.

At 100%, DataGridView row scrolling, target transform and ordinary resize initially
matched a full repaint. Scrolling Gallery's navigation produced **46,503 mismatched
display pixels**, while the framebuffer still matched the full repaint exactly.
The displayed navigation included a shifted strip of the title bar. This establishes
a presentation defect rather than a missing old/new geometry union in that case.

The original native transfer tests used a DIB-section destination. A new regression
uses a device-dependent bitmap, the GDI path representative of HWND presentation.
With a 47x61 top-down source and damage `(0,12)-(19,61)` or `(0,12)-(47,61)`, the
existing transfer writes source row 0 at destination row 12. Both tests fail, while
the four original DIB tests and three other device-bitmap rectangles pass.

The existing `StretchDIBits` call translates source y to `height-top-damageHeight`.
This reaches `(xSrc,ySrc)=(0,0)` for the failing bottom-edge regions. Destination
paths do not select the same source rows for that cropped top-down bitmap. A correct
framebuffer therefore becomes a shifted/stale native image. Merely enlarging damage
to old/new bounds cannot correct that transfer.

The pixel suite also found an independent ownership defect: removing a child could
leave its pixels in the old owner's cache. The child had disappeared from
`NeedsPaint`, while a layout pass with unchanged remaining bounds requested no
repaint. Removing the button failed the rendered byte comparison at all seven DPI
scales. `ControlCollection.Remove` now invalidates the old owner after detachment.
Reparenting uses that same removal path. This is bounded to the affected owner's
composition, following existing geometry/visibility invalidation.

## Correction and pixel ownership

Present an explicit top-down band: offset the borrowed pixel pointer to the first
damaged row, retain the original row stride/bitmap width, and describe only the
damaged height in a local bitmap header. Source y is then zero for the complete
band on both GDI destination paths. No new backing allocation, full-window clear,
parallel renderer, or DataGridView-specific workaround is needed.

The audit also covers shared invalidation, batch unions, transformed visual bounds,
ancestor composition, clipping, source subsets, cached-surface lifetime, scrolling,
move/resize, visibility, removal/reparenting and intermediate animation frames.
The existing geometry contract remains: for old visual area A and new area B,
the composition owner restores background and siblings in A-B and draws the new
child in B. Existing batch/window unions combine intersecting damage. Do not clear
the whole native framebuffer to repair A. Render transforms map all four corners
of the existing presentation rectangle and union with the last composited area.
Clipping occurs against ancestors before the final device-to-logical conversion;
the native boundary rounds outward once. No second visual-bounds model was added.

| Pipeline stage | Audit result |
| --- | --- |
| Control invalidation and batching | Device-local invalidation marks cached content dirty; existing window batching unions logical damage. |
| Location, Size, Bounds, Dock/Anchor and parent resize | Existing bounds commit invalidates the affected parent and child; pixel tests cover exposed content. |
| Visibility | Existing parent invalidation on hide/show restores its composition. |
| Remove/reparent | Fixed missing old-owner invalidation when layout does not move another child. |
| LayoutTransition | Retains its existing conservative full-window presentation invalidation. This fix adds no new full-window fallback. TestClock intermediate frames verify correctness. |
| Render transforms | Existing presentation mapping and previous composited area include rotation, scale, translation and offscreen return. |
| Effects and transparency | Ripple, borders, focus and GroupBox shadow draw within existing control caches; transformed cache bounds are used for composition. Sibling alpha blending retains back-to-front order. No unsupported out-of-surface shadow overflow is promised. |
| Scrolling | DataGridView updates its viewport and invalidates; ScrollableControl moves children with the existing layout/invalidation path. Neither needs a special fix. |
| Ancestor/Skia composition | Partial clips and source subsets preserve backing pixels outside damage; cached child backgrounds clear their own surface before repaint. |
| WindowBase/framebuffer | Logical native damage becomes the physical canvas clip once; backing dimensions and 32-bit stride remain physical. |
| Native presentation | Fixed cropped top-down DIB source rows by lending a complete row band to GDI. DIB and device-bitmap destinations now share the correct source coordinates. |

## Repeatable validation

`WindowsFramebufferDamageTests` checks every pixel for 140 cases: seven scales,
two GDI destination types and ten rectangles. They include logical (200,150),
bottom-edge damage with zero source origin, clipping, empty/outside rectangles
and unchanged destination pixels. The original shared framebuffer header must
remain unchanged.

`PartialRedrawPixelTests` uses one persistent surface behind TestHost's production
paint callback. It applies actual pending damage, then compares all bytes with a
forced full composition. It also checks specific restored background/underlying
pixels. Coverage includes movement, shrink, hide/show, remove/reparent, overlap,
alpha, rotation/scale, offscreen movement, Dock/Anchor, parent resize, intermediate
TestClock animations and layout transitions, grid/flow scrolling, shadow, ripple
and focus. The 5K hover guard checks partial coverage, no layout and no allocations.
Each geometric/effect scenario runs at 100/125/150/175/200/225/250%.

The explicit Gallery runner exercises actual pages and native windows on the
connected FullHD/100% and 5K/225% displays:

```powershell
dotnet build scripts/validation/PartialRedrawGallery/PartialRedrawGallery.csproj -c Release -m:1 /p:UseSharedCompilation=false
dotnet scripts/validation/PartialRedrawGallery/bin/Release/net10.0-windows/PartialRedrawGallery.dll artifacts/partial-redraw-gallery
```

It compares all client RGB pixels against the actual native framebuffer and
compares partial/full frames separately. It moves and maximizes the same window,
scrolls both navigation and grid, runs the page's real animation scheduler,
mutates overlapping translucent controls, resizes and captures a ComboBox popup.
The timer only sequences acceptance actions and allows real animation ticks; it
does not request periodic repaint. Capture failure never triggers a repair repaint.
Screen occlusion by another app/system popup invalidates that capture; keep the
test window unobstructed. `--case=second-full-5k` isolates a display case;
`--observe` records failed comparisons only for diagnosis, not acceptance.

The existing `--high-dpi-physical` host measures hover through PerformanceProfiler
and now compares the entire displayed client, not a small sample inside the button.
`RenderInfo.PresentationCpuTime` adds the Windows GDI submission interval to the
same recorder. It is CPU wall time, not a DWM/GPU completion timestamp; unmeasured
backends retain null. Recording remains optional and hidden.

Raw pre-fix evidence is under the ignored `artifacts/autonomous-audit/partial-redraw/`:
recording frame sheets, `gallery-before-3/`, and `gdi-before.log`. Video contents are
reproduction evidence; the attached task text defines the authorized work.

## Local measurements (2026-09-19)

Replaying the expanded Gallery runner against the saved master assemblies
(`gallery-baseline-pages`, diagnostic observe mode) reproduced both reported page
families: 44,302 wrong display pixels after navigation scrolling on DataGridView,
210,215 on entry to the animation page, and 44,752 after its navigation scroll.
Removing the translucent control left 2,374 stale framebuffer pixels. This probe's
window dimensions differ from the initial 46,503-pixel reproduction above.

With the fix, `gallery-matrix-4/results.json` contains 426 records across normal and
maximized FullHD, normal and maximized 5K, exact 5120x2880, and return to FullHD.
All screen/framebuffer and partial/full comparisons are zero. The recorded
animation translation and rotation change through intermediate values on each
display. Popup captures use the actual popup HWND. Two earlier attempts were
invalidated by an overlaid Windows quick-settings panel; those captures remain
separate evidence, not renderer acceptance.

Release hover measurements use the existing physical host and PerformanceProfiler,
16 hover/leave inputs with the first two frames discarded. PNG capture and export
are outside the measured interval. These are local elapsed times, not fixed budgets.

| Exact 5K / 225% hover metric | Master after #122 | Fixed |
| --- | ---: | ---: |
| Mean native frame | 1.11 ms | 1.17 ms |
| Maximum native frame | 1.31 ms | 1.23 ms |
| Mean shared render | 1.03 ms | 1.09 ms |
| Mean GDI CPU submission | Not recorded by the old profiler | 0.04 ms |
| Mean damage | 46,004 pixels | 46,004 pixels |
| Layout passes per measured frame | 0 | 0 |
| Surface allocations / backing recreations during hover | 0 / 0 | 0 / 0 |
| Backing width x height / stride / bytes | 5120x2880 / 20,480 / 58,982,400 | Same |

Thus the hover still touches about 0.31% of the framebuffer, with no return to the
pre-optimization full-5K redraw cost. Raw profiler exports are in `physical-before`
and `physical-after`. The new null-on-unsupported submission field is additive;
no public member, DPI coordinate contract, release version or package identity
changes. Automated native messages/captures were executed on the connected
displays; manual hardware-pointer observation is not claimed.

Focused validation passed 140 GDI pixel cases and the seven-scale shared pixel
suite (including effects), plus native profiler and TestHost coverage. Final
Debug/Release solution tests, API/package/documentation checks and CI results are
recorded in the dedicated regression PR; acceptance must pass before merging.
