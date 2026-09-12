# Accessible viewports and scrolling

Viewport accessibility extends the existing `AccessibleObject` and the control's
ordinary scrolling implementation. It does not add a second item tree or a separate
scroll position. Read live peers and perform actions on the owning UI thread.

`AccessibleObject.ScrollInfo` returns optional immutable geometry. Each axis contains
its current offset, inclusive minimum/maximum, actual visible length, and small/page
movement in logical control units. `ViewportBounds` uses the same coordinate space
as `AccessibleObject.Bounds`; native adapters apply their existing host transforms.
The visible length is independent of `ScrollBar.LargeChange`, whose existing getter
can clamp a page to the movement range. A disabled viewport still reports geometry,
but rejects movement. Sensitive peers and descendants of protected peers do not
export viewport extents through native or automation adapters.

```csharp
var peer = panel.AccessibilityObject;
if (peer.ScrollInfo is { Vertical.IsScrollable: true })
{
    peer.PerformAction(AccessibleActions.Scroll,
        AccessibleScrollRequest.ToPercent(horizontal: null, vertical: 100));
}

// Reveal an ordinary child through its ScrollableControl ancestors without
// changing the selected control, keyboard focus, or the child's runtime identity.
button.AccessibilityObject.PerformAction(AccessibleActions.ScrollIntoView);
```

Use `ByAmount` for control-defined small/page steps, `ToPercent` for absolute values
from zero through 100, or `ByViewport` for signed finite viewport fractions. A null
percentage, `None` amount, or zero viewport fraction leaves that axis unchanged.
Native sentinel values do not belong in this public request. Invalid, nonfinite or
out-of-range requests are rejected before movement; requesting an unavailable axis
also fails before either axis changes. Large legal requests clamp to an endpoint.
An already reached endpoint succeeds without a false movement notification.

`ScrollableControl` uses its real logical client area minus each visible scrollbar
once. Current presentation padding, including styled and animated values, remains
part of its logical content calculation and is not scaled a second time. Ordinary descendants
outside every visible intersection are `Offscreen`, independently of `Invisible`,
and remain discoverable. Reveal aligns an offscreen edge; a child that already
covers a smaller viewport does not oscillate between opposite edges.

`DataGridView`, `ListBox` and `TreeView` retain their existing row/item boundary
scrolling. Their metadata converts those actual boundaries into logical distances;
fractional requests round in the direction of movement. Grid calculations account
for actual row heights, fixed headers, both scrollbars and the current first row.
When no whole row fits, later rows can still be reached. A single row/item taller
than its viewport cannot be scrolled internally by these existing index-based
controls. This limitation is not represented as pixel scrolling support.

`TextBox`, `RichTextBox` and `MarkdownEditorTextBox` use their current shaped document
and real text origin. Scrolling does not alter selection or focus. Existing caret
margin outside the glyph box is included in the reported current range. Sensitive
editors expose no scroll geometry. `DocumentViewer` and `MarkdownViewer` project
their committed document scrollbar/layout metrics without initiating image loading
or a new document layout from an accessibility getter.

Position/range notifications use `AccessibleEvents.ScrollChanged` after the state
commit. Duplicate geometry is suppressed. Synchronous callbacks may mutate or
detach controls; subsequent axes stop when the original ancestry or lifetime is
invalidated. Notification reentry is bounded. Exceptions do not authorize retrying
an action against a replacement owner. Internal negative event IDs are not sent as
raw legacy MSAA WinEvents.

## Native projections

Windows exposes UIA `ScrollPattern` on viewport owners and retains `ScrollItem` on
revealable children. The six scroll properties describe both axes. An unavailable
axis has native percent `-1` and view size `100`; geometric scrollability does not
change merely because the control is disabled. Native `ScrollAmount` values are
explicitly translated, and `SetScrollPercent(-1, value)` leaves the first axis alone.
The canonical action preserves focus. A native UIA client can additionally request SetFocus before
the action through Windows' default AutoSetFocus policy; see the [Windows guide](windows-ui-automation.md).
Retained providers must still belong to their captured window root, including when
their immediate parent exists inside a detached container.

Android advertises only directions that can currently move. Forward/backward use
the vertical axis when it is scrollable, otherwise horizontal. API 35 granular
requests use positive viewport fractions; positive infinity means the requested
endpoint. Negative values and NaN are rejected. `TYPE_VIEW_SCROLLED` contains numeric
offsets and maximum offsets after the normal host-density conversion, with no text
or item payload. Numeric RangeValue controls keep their independent actions.

These mappings follow the official [UIA scroll provider contract](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nn-uiautomationcore-iscrollprovider)
and [Android accessibility node actions](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo).

## Automation

Detached `AutomationNodeSnapshot.ScrollInfo` can be inspected after the UI callback
has returned. Use `AutomationActionValue.FromScroll(request)` with the existing
`AccessibleActions.Scroll` operation. The session retains its normal capability,
privacy, cancellation, bounded traversal and current-target checks; strings and
untyped numbers are not accepted as scroll requests.

The authenticated Windows bridge keeps protocol v1 and adds an optional typed
`scroll` payload and optional snapshot metadata. Absent fields remain compatible
with older payloads, and unknown/malformed request modes fail as `InvalidRequest`.
An unknown mutation outcome still requires reconciliation rather than automatic
repetition. No Android external automation bridge is introduced.

## Validation scope

In `ControlGallery`, open **Accessibility → Viewport**. The inner two-axis viewport has
the stable ID `controlgallery.accessibility.viewport`; its far button ends in
`viewport-target`. The page provides ordinary scrollbars and buttons that call typed
Scroll and canonical ScrollIntoView. Its status reports percentages and whether the
far target was focused, without copying editor text. **Grid / Date** and **Text**
provide the corresponding real grid and editor viewport examples. Each tab page can
also scroll when the Gallery window is small.

Phase 4 includes focused tests for logical/DPI geometry, inclusive endpoints,
two-axis validation, callback reentry and detached owners, native amount/action
mapping, retained UIA ancestry, sensitive metadata and typed protocol compatibility.
Current build/test results and native observations are recorded in the phase
acceptance report when executed. Native screen-reader/physical-device checks must
not be inferred from pure mapper or headless tests.
