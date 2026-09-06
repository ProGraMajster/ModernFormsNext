# Android accessibility backend (issue #59, Phase 3)

## Audit before implementation

Baseline: `028b32da615632810d0eb7e62815d1ac0397bb3a` (Phase 2 PR #103),
verified against master and the open issue on 2026-09-05.

The Android backend has one `AndroidSkiaHostView` (`SKCanvasView`). The cross-platform
sample's `AndroidAppHost` borrows its process-owned `App.Root` through `SkiaControlSurface`.
The activity forwards attach/start/resume/pause/stop/configuration/dispose; recreation
preserves the shared tree and replaces the surface. Rendering scales the canvas once by
`Density`, while input converts physical pixels to logical coordinates. The surface uses
normal control selection/focus and its existing IME bridge. It does not implement
`IWindowBaseImpl` or the full Android application host contract.

There was no AccessibilityNodeProvider, delegate, virtual node mapping, touch exploration,
or accessibility event integration. Window-based notifications require `FindWindow()` and
therefore did not reach this windowless surface. Phase 3 adds an internal surface notification
route and exposes its existing root through `IPlatformAccessibilityHost`. The canonical
`AccessibleObject` -> `PlatformAccessibleObjectAdapter` transport from Phases 1/2 is reused.
The internal transport's historical UIA name does not imply Windows dependencies.

The Android project already has a plain net10.0 target for deterministic backend tests and
a native net10.0-android target. No new test project or dependency is required. The available
sample Release configuration enables AOT; minimum Android API is 23. The installed SDK
rolls forward from global.json's 10.0.201 to 10.0.400 under `latestFeature`.

The existing ComboBox popup requires a Form; the windowless Android host cannot expand it.
Phase 3 must not advertise an unavailable expansion action or implement #72 to supply it.
Full scroll, advanced text/IME (#62), application lifecycle (#63), and diagnostics (#61)
remain separate work.

Native mapping follows Android's [virtual descendant provider contract](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeProvider)
and [node information API](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo).

## Host integration and ownership

The sample sets `nativeSurface.AccessibilityHost = controlSurface` on the Android main thread.
The view borrows this `IPlatformAccessibilityHost`; it never owns or disposes application controls.
`SkiaControlSurface` implements that existing public interface explicitly. Its surface notification
route, peer subscriptions, and confirmation of existing ListBox selection removal are internal,
platform-neutral transport extensions. There are no Android types in shared public APIs and no
changes to the Windows UIA or MSAA contract. Android now references the existing WindowKit
abstraction project. The repository's existing `System.Formats.Nrbf` Android AOT workaround moves
from the cross-platform sample to the Android backend, so all hosts receive that dependency.
No new package or package version is introduced to the repository.

```csharp
var surface = new SkiaControlSurface(applicationRoot);
var view = new AndroidSkiaHostView(activity) { AccessibilityHost = surface };
// Keep forwarding the existing render/input/activity lifecycle events.
// Before disposing a borrowed surface, clear view.AccessibilityHost or dispose the native view.
```

The native host node uses Android's parent/window initialization, application package, physical
bounds and `android.view.ViewGroup` class, then replaces default View actions with supported
canonical actions. Its children are the canonical root's children. Control-backed and logical
children use the same provider; there is no native View per control and no reflected hierarchy.

Each `AndroidAccessibilitySession` assigns positive virtual IDs to canonical `RuntimeId` values;
`-1` represents the host, and `int.MinValue` is invalid. Reorder preserves IDs; duplicate item
occurrences are distinguished by the canonical model. Queries verify active parent membership.
Remove, Hidden, disposal, or detach invalidates old IDs. IDs are not reused on reattach; exhaustion
fails without wrapping. A recreated native View owns a new ID namespace, and the old provider
remains disconnected. The ID dictionary holds weak peer references and unsubscribes when detached.
It contains only queried/discovered identities, not another semantic tree or permanent native nodes.

Callbacks execute inline on the Android main thread, or through the existing dispatcher with a
two-second bound and cancellation of work that has not started. Already-running application code
must remain short; it cannot be preempted safely. Callback exceptions produce null/false and at
most one generic diagnostic per provider, without exception messages or values.

Attach/detach uses the existing View hooks. Explicit disposal removes the native View from its
parent before releasing its managed peer. This ordering is necessary because Activity.OnDestroy
can run before Android's final OnDetachedFromWindow callback. It fixes that narrow lifetime gap;
it does not introduce the full lifecycle system planned in #63.

## Android projection

| Canonical type | Android class / behavior |
| --- | --- |
| Button | `android.widget.Button`, Invoke |
| CheckBox / RadioButton / Switch | Native checkable widget class, Toggle or Select |
| Edit | `android.widget.EditText`, separate label and text, conditional SetText |
| ComboBox | `android.widget.Spinner`, only available canonical expansion actions |
| List / ListItem | `android.widget.ListView` / `android.widget.TextView` |
| Tree / TreeItem | `android.view.ViewGroup` / `android.widget.TextView`, logical hierarchy |
| Tab / TabItem | `android.widget.TabWidget` / `android.widget.TextView`, selection |
| Menu / MenuItem | `android.view.ViewGroup` / `android.widget.TextView`, command actions |
| Slider / ProgressBar | `android.widget.SeekBar` / `android.widget.ProgressBar`, RangeInfo |
| Window / Dialog | Dialog semantics; the actual native surface root remains a ViewGroup |
| Pane / Group / ToolBar | Structural ViewGroup |
| Text / Image | TextView / ImageView |
| Custom / Separator / ScrollBar without a viewport contract | Generic View |

Name is the explicit semantic label, not an alias for entered text. Edit nodes expose Value as
Text and Name as HintText on API 26+, with ContentDescription as the older-API label fallback.
Static text uses Text; other labelled controls use ContentDescription. Help/Description remains
supplemental metadata in `ModernFormsNext.Help` extras. AutomationId uses its own metadata extra,
not a fabricated Android resource ID. Mixed, expanded/collapsed, and distinct read-only values
use StateDescription (API 30+, compatibility extra earlier). API 36 uses the three-state checked
property; older Android uses Checked plus the mixed state description. The initial state words
are English; localized framework-wide announcements remain a follow-up.
Checkable widgets retain Android's native checked/on/off descriptions instead of exposing a
numeric canonical Value as StateDescription. Their value changes invalidate state; only edits
use the Text content-change flag, so a switch toggle does not become an empty text announcement.

Sensitive/Protected nodes are marked Password, and API 34+ also receives AccessibilityDataSensitive.
The mapper never calls their Value getter. No value is copied to Text, ContentDescription, range,
state description, diagnostics, search results, or event payloads. Explicit Name/Help/Description
are author-provided metadata: applications must not put secrets into those labels themselves.
SetText is permitted for an enabled, writable password editor, through canonical SetValue, and
readback remains redacted. ReadOnly/Enabled and normal TextChanged behavior are preserved.

States include enabled, focusable, input focused, selected, checkable/checked/mixed, editable,
password, and actual visibility. Clickable and scrollable derive from supported actions;
LongClickable is false because there is no implemented canonical long-click operation.
Default/Control/Content use Android's normal importance. Raw retains structural ancestry and is
not important on API 24+; API 23 has no per-node importance setter. Hidden never enters the active
tree. Android has one service tree, rather than Windows UIA's three query projections.

## Actions, focus and events

| Android action | Existing canonical path |
| --- | --- |
| Click | Invoke, otherwise Toggle or Select for checkable/selectable elements |
| Focus | Focus -> normal Control.Select; requests native host focus first |
| Select | Select |
| ClearSelection | Existing ListBox multi-selection flags; capability explicitly confirmed by adapter |
| Expand / Collapse | Corresponding action, only in the applicable state and never on a leaf |
| SetText | SetValue with a string, respecting writable/enabled state |
| SetProgress | SetValue with a finite, in-range number and writable range (API 24+) |
| ScrollForward / ScrollBackward | Increment / Decrement for a mutable range, within limits |
| ShowOnScreen | ScrollIntoView, where the control actually supports it |
| AccessibilityFocus / ClearAccessibilityFocus | Backend-only Android accessibility focus |

Argument-free actions ignore Android routing metadata in Bundle; only documented action arguments
are passed to the canonical API. Unsupported, stale, invalid, disabled, or detached requests return
false. Clear input focus, general viewport scrolling and scroll-to-position are not advertised.
ListBox/ListView/Tree/tab/radio selection uses the existing control state and single/multi-selection
rules. Collections are supplied only for a flat canonical List made exclusively of ListItems;
those children receive actual row indexes. No invented grid dimensions are attached to menus,
trees or tabs. Invalid/nonfinite ranges are omitted, and read-only progress has no setter actions.

Keyboard focus and accessibility focus are independent. The latter lives only in the session.
Touch exploration routes Android hover events through canonical HitTest; it does not synthesize
framework keyboard focus. Focus events are delivered synchronously after accessibility-focus
actions because ViewRootImpl uses them to track the virtual focused descendant.

Canonical Focus, selection, value, name/description, state, location and structure notifications
map to ViewFocused, ViewSelected and WindowContentChanged with appropriate Text,
ContentDescription, StateDescription or Subtree change flags. Successful accessible Click and
normal surface control Click produce ViewClicked. Custom logical peers are observed on demand.
Other events are coalesced per node/type over 50 ms, with a 128-entry bound and subtree fallback.
Events carry no Text, BeforeText, ContentDescription or user-value extras, including for passwords.
Dynamic add/remove/reorder invalidates the subtree without rebuilding the host View.

## Coordinates and visibility

In this windowless host, canonical PointToScreen values are surface-relative logical pixels.
The provider clips to the semantic ancestor bounds and Android's local visible rectangle, then
multiplies by Density exactly once and adds GetLocationOnScreen. This includes the actual native
host offset and window insets. The renderer already applies Density to its canvas; the provider
does not use font ScaledDensity as a second rendering scale. Edges round outward to integer
physical pixels with finite/overflow guards. BoundsInParent is relative to the semantic parent.

Logical tree/menu rows are not viewports: their own rectangles must not clip expanded descendants.
Real structural ancestors still clip children, including framework scroll offsets already reflected
by canonical bounds. Hidden/invisible/offscreen nodes, zero rectangles, detached peers, invisible
native ancestors and zero native alpha are not reported visible. A cached node with no visible
intersection has VisibleToUser=false. General native View rotation/nonuniform scale and precise
nonrectangular clipping are not claimed; the supported sample host uses translation plus density.

## Running native validation

Build/install the existing cross-platform sample using `scripts/android/Build-CrossPlatformSample.ps1`
and `Install-CrossPlatformSample.ps1`, or build with `EmbedAssembliesIntoApk=true` for adb deployment.
The deterministic Android test project exercises the same session and mapper without an emulator.
The sample contains an opt-in Instrumentation runner using the actual UiAutomation service connection:

```text
adb -s emulator-5554 shell am instrument -w com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.AccessibilityInstrumentation
adb -s emulator-5554 shell am start -n com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.MainActivity --ez ACCESSIBILITY_DEMO true
```

The instrumentation fixture checks native properties/actions, logical children, password readback
and search, event delivery/privacy, dynamic removal, bounds and real Activity recreation. It prints
only counts and fixed check categories. It is an integration check, not evidence of TalkBack speech
or physical-device behavior. Ordinary launches keep the existing sample page.

## TalkBack manual checklist

Every row remains **NOT EXECUTED** until a person performs it with TalkBack. Record device/API,
TalkBack version, density, and the tested commit. Check speech, focus location and resulting state.

| Check | Expected observation | Manual status |
| --- | --- | --- |
| Button | Name, role, activation once | NOT EXECUTED |
| CheckBox | Name, checked/mixed state, toggle | NOT EXECUTED |
| RadioButton | Name, selection, mutual exclusion | NOT EXECUTED |
| Switch | Name, checked state, toggle | NOT EXECUTED |
| TextBox | Label distinct from text, editing | NOT EXECUTED |
| Password | Password role, no plaintext speech/readback | NOT EXECUTED |
| ComboBox | Name/value; unavailable Form popup action absent | NOT EXECUTED |
| ListBox/ListView | Items, selected state, correct positions | NOT EXECUTED |
| TreeView | Parent/child order, expand/collapse | NOT EXECUTED |
| Tabs | Header order and selected page | NOT EXECUTED |
| TrackBar | Range and adjustable value | NOT EXECUTED |
| ProgressBar | Read-only range | NOT EXECUTED |
| Menu | Command activation; unavailable native popup not advertised | NOT EXECUTED |
| Logical children | Individually reachable without native Views | NOT EXECUTED |
| Custom semantic child | Logical action reachable and invoked | NOT EXECUTED |
| Focus | Touch exploration independent of keyboard focus | NOT EXECUTED |
| Selection | Single/multi-selection behavior | NOT EXECUTED |
| Expand/collapse | Accurate state and action availability | NOT EXECUTED |
| Dynamic add/remove | Refreshes order; removed item no longer reachable | NOT EXECUTED |
| Disabled/hidden | Disabled announced, Hidden omitted | NOT EXECUTED |

The compact fixture includes representative controls; full framework-wide coverage, localization,
diagnostics, virtualized controls (#97), full scroll and advanced Text/IME (#62) remain deferred.
The default generated DemoApp/template is unaffected. Issue #59 stays open; Phase 3 is only ready
for manual validation when automated checks pass, and is not declared COMPLETE without TalkBack.

### Recorded Android evidence (2026-09-05)

- Deterministic backend tests: 148/148, including mapper 51 and provider/session 25.
- Native instrumentation: 46/46 assertions passed on Pixel_8 emulator, Android API 34, including native
  service queries/actions/events, sensitive payload checks, same-View semantic replacement and actual Activity recreation.
- TalkBack 14.2.0.618048417: limited observed smoke only. The service started, and touch exploration
  placed its visible focus rectangle around the virtual CheckBox. Speech and the complete gesture/
  control checklist were **NOT EXECUTED**; double-tap activation was not confirmed. This observation
  does not turn any full manual checklist row into PASS. The prior accessibility settings were restored.
- Physical device: **NOT EXECUTED**.

Local execution artifacts are kept under `artifacts/issue-59-phase3/` (not packaged or committed).
