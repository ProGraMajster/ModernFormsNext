# Presentation lifetime and Windows UIA event subscriptions

This patch addresses two independent costs observed in a paged desktop application.
It preserves the canonical accessibility objects, runtime IDs, event mapping, control
tree and application data.

## Hidden render buffers

`Control.OnVisibleChanged` releases the control's renderer-owned bitmap when its
effective visibility becomes false. The existing parent-visibility notification
walk releases descendant buffers as well. User images and controls remain alive.
Showing the same page recreates buffers through `GetBackBuffer`, which marks them
dirty and repaints normally. This trades a repaint on return for bounded memory
across visited pages; it matters especially at high physical DPI. There is no new
public API or caller action required. Visibility changes remain UI-thread operations.

`HiddenControlBufferTests` verifies disposal of both ancestor and child buffers,
preserved control state/identity, and byte-identical rendered pixels after return.
The regression failed before the patch because both hidden buffers stayed allocated.

## Windows event subscriptions

The existing fragment root now implements the optional native
`IRawElementProviderAdviseEvents` COM interface. Subscription counts are maintained
per HWND fragment, event and property, including overlapping clients and all-property
subscriptions. Unmatched removals do not consume other subscriptions. Bookkeeping
does not dispatch to the UI thread and no subscription lock spans a native callback.
The shared semantic event mapping is unchanged.

Previously the process-global `UiaClientsAreListening` flag caused a fragment to
publish every mapped UIA event even if the listener concerned another window. An
800-control attachment reproducer emitted 1,600 property notifications and 800
structure notifications without a subscription to that fragment. It now emits none;
subscribed events still flow, including a property-change event received by a real
out-of-process Windows UIA client.

The SAFEARRAY input is borrowed, validated and copied, never destroyed by the provider.
The ABI follows the Windows SDK's IUnknown-based interface, rather than assuming the
managed WPF interface inheritance determines native vtable slots.

References: [Microsoft subscription contract](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nn-uiautomationcore-irawelementprovideradviseevents)
and [Windows SDK header](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomationCore.h).

Before/after reproducer logs and TRX are retained under `artifacts/mtd-stability` and
the consuming ModernTubeDownloader worktree's stability evidence directory. End-to-end
timings and physical-monitor memory measurements belong to that application's report;
these unit checks alone do not establish a latency improvement.

## MSAA target and visibility delivery

The remaining native stack was in `NotifyWinEvent` during `OnVisibleChanged`.
Descendant notifications used to address the window's `OBJID_CLIENT/CHILDID_SELF`;
thus hiding a page child announced that the whole client area was hidden. The
existing MSAA provider cache now assigns weak event IDs to canonical descendants.
Root events still use SELF; indexed child enumeration is unchanged. Reordering
does not change event identity, and detached targets reject lookups. No parallel
semantic hierarchy is created.

Canonical MSAA notifications are coalesced per dispatcher turn, with no timer or
timeout change. Repeated changes of a property publish its final value. A parent's
SHOW/HIDE covers child visibility and invisible-state notifications as required by
the [WinEvent contract](https://learn.microsoft.com/en-us/windows/win32/winauto/event-constants).
Focus, name, selection and other notification kinds remain distinct. Low-level
explicit native object IDs keep their direct delivery route. UIA subscriptions
and shared observers retain their independent canonical delivery.

The 800-child regression asserts one parent visibility event, preserved focus/name
events and a later standalone child state event. Native COM tests additionally
exercise the AdviseEvents vtable and caller-owned SAFEARRAY lifetime.

Original qualification on `5fe28ea`: full solution restore and Debug/Release builds succeeded; full tests
each passed 4,484/4,484 with zero failures/skips. Three pre-existing NU1902 warnings
concern MessagePack in Designer/VSIX projects, outside this runtime patch. ControlGallery
Release was launched, its real HWND captured, and closed with exit code 0; this is
a startup smoke, not a manual screen-reader audit. Original failing reproducers and
all later TRX/logs remain under artifacts/mtd-stability.

## Integration with the newer master

The patch was replayed on `32b4fe3` (54 commits beyond its original base), with
an identical stable patch ID and no textual conflicts. The integration retains
virtualized UIA child navigation and bounded structure snapshots, ToolTip role
mapping, native Tab transport, binding activation/reentrant creation guards,
inherited layout/RTL policy, typography refresh, and shadow/geometry disposal.
Designer and Android changes on master are retained rather than copied from the
older branch. The hidden-buffer change owns only the existing raster back buffer;
it neither releases caller images nor changes the newer shadow/filter ownership.

`NativePresentationPreservesIdentityBuffersAndExternalEventsFor100Cycles` drives
real HWNDs with 8 and 800 descendants through a separate UIA client. Each of 100
cycles subscribes, hides and reparents the same subtree, shows/repaints it,
rediscovers its stable runtime ID, and unsubscribes. A second HWND changes its name
independently. A native WinEvent hook verifies descendant visibility IDs and
bounded delivery, while the host records private/managed bytes, actual bitmap
counts/bytes, UI work and dispatcher heartbeat. Hidden buffers must be zero;
all 200 requested Name changes must reach the client exactly once. These are
bounded regression probes, not a screen-reader audit or an exhaustive native-heap
leak proof. Logs (including failed fixture-development attempts) remain under
`artifacts/native-presentation/` and `artifacts/master-integration/`.

Integration qualification (2026-11-08): full solution restore, Debug and Release
builds, and both complete test suites passed (5,859/5,859 each, zero failures or
skips). Builds retained three existing NU1902 warnings and six existing VSTHRD010
warnings in unchanged Designer/VSIX code. CI-change, release-documentation and
package-contract checks passed (45, 39 and 8 assertions). The bounded dynamic
resource publish probe passed both JIT and trimmed executions; its 60 other trim
warnings remain documented, and this is not a whole-framework trimming claim.
ControlGallery Release was launched, its Button sample selected and invoked by
an external UIA client, and its real window captured before normal shutdown.

The Release 800-descendant native probe delivered all 200 subscribed changes in
5.59 seconds across 100 cycles, with 100 descendant HIDE events, 200 SHOW events
and no root HIDE. All hidden subtree bitmap samples were zero; runtime identity
survived reparenting. The small-tree probe also passed. These elapsed values cover
the client-driven cycles, not process startup and initial discovery.

An explicitly local pre-merge ModernTubeDownloader consumer (application source
`25d13d5`, central pin still unchanged) passed 20 Classic/Modern transitions,
playlist selection/deduplication/restart checks, and the physical 5120 x 2880,
225% DPI scenario with 2,750 history entries, 2,617 jobs and 2,750 thumbnails.
Store hashes were unchanged and owned image/cache counts returned to zero on
shutdown. A predeclared fresh-process A/B run on that same fixture measured
first-HWND times of 3.55 seconds for the prior framework and 2.39 seconds for the
integration, with peak private memory 967.89 and 970.36 MiB. The earlier integration
launch took 4.22 seconds; all observations are retained. These warm OS/file-cache
single runs establish compatibility, not a statistically demonstrated speedup or
cold-boot result. Application evidence is retained under
`artifacts/mfn-master-integration-20261108` in its isolated dashboard worktree.
