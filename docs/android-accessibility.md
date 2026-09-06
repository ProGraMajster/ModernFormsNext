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
The existing Android input connection also uses the keyboard-focused canonical peer's sensitivity
to request password input with no suggestions and, from API 26, no personalized learning. Otherwise
an IME could expose masked input through its own suggestion nodes. This narrowly configures the
existing editor connection; it does not introduce an additional text model or advanced IME support.

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
rules. For the existing ListBox multi-selection peers, Android Select adds an item without clearing
other selections; Click toggles that item, and ClearSelection removes it. Single-selection lists
retain their replace-selection behavior. Collections are supplied only for a flat canonical List made exclusively of ListItems;
those children receive actual row indexes. No invented grid dimensions are attached to menus,
trees or tabs. Invalid/nonfinite ranges are omitted, and read-only progress has no setter actions.

Keyboard focus and accessibility focus are independent. The latter lives only in the session.
Touch exploration routes Android hover events through canonical HitTest; it does not synthesize
framework keyboard focus. Focus events are delivered synchronously after accessibility-focus
actions because ViewRootImpl uses them to track the virtual focused descendant.
Programmatic input focus also deselects the previous windowless input target. Expanded tree-item
hit testing visits child rows outside the parent's own row rectangle, while the TreeView control
continues to enforce its viewport boundary.

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

Record device/API, TalkBack version, density, tested commit and execution method. A focus rectangle
or a direct provider-action test alone is insufficient: check actual TalkBack speech, gestures and
resulting control state. The following statuses describe the agent-operated emulator run on
2026-09-06, not a human usability assessment; see the evidence and method below.

| Check | Expected observation | Manual status |
| --- | --- | --- |
| Button | Name, role, activation once | PASS |
| CheckBox | Name, checked/unchecked state, toggle; mixed when offered | PASS |
| RadioButton | Name, selection, mutual exclusion | PASS |
| Switch | Name, checked state, toggle | PASS |
| TextBox | Label distinct from text, editing | PASS |
| Password | Password role, no plaintext speech/readback | PASS |
| ComboBox | Name/value; unavailable Form popup action absent | PASS |
| ListBox/ListView | Items, selected state, correct positions | PASS |
| TreeView | Parent/child order, expand/collapse | PASS |
| Tabs | Header order and selected page | PASS |
| TrackBar | Range and adjustable value | PASS |
| ProgressBar | Read-only range | PASS |
| Menu | Command activation; unavailable native popup not advertised | PASS |
| Logical children | Individually reachable without native Views | PASS |
| Custom semantic child | Logical action reachable and invoked | PASS |
| Focus | Touch exploration independent of keyboard focus | PASS |
| Selection | Single/multi-selection behavior | PASS |
| Expand/collapse | Accurate state and action availability | PASS |
| Dynamic add/remove | Refreshes order; removed item no longer reachable | PASS |
| Disabled/hidden | Disabled announced, Hidden omitted | PASS |

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

### Recorded TalkBack gesture evidence (2026-09-06)

The existing Pixel_8 AVD ran Android API 34 at 420 dpi (density 2.625), with TalkBack
14.2.0.618048417 and English US speech. The run began at `e163c01` and the final runtime fixes
were checked at `47283521b8b221f276228c3665d32740744fa099`. Evidence is in the local
`artifacts/issue-59-phase3/manual-2026-09-06/` directory. The 20 rows above are the original
acceptance list: 20 PASS, 0 FAIL, 0 NOT EXECUTED within this recorded emulator scope.

Execution method: an agent sent ordinary touchscreen events through the emulator (exploration,
double-tap, directional flick and the three-finger TalkBack menu gesture). These checks did not
call AccessibilityNodeInfo.PerformAction as a substitute for TalkBack activation. Actual emulator
audio was captured and transcribed locally; screenshots of TalkBack's own speech-output overlay
and the rendered control state were inspected. During finalization, the user also confirmed
hearing TalkBack during this validation. No formal human usability assessment was performed.
The transcriptions are qualitative evidence, not exact speech assertions.

- `final-button-*`, `final-checkbox-*`, `final-radio-*`, `final-other-radio-*`, `final-switch-*`:
  correct roles/states; double-tap in an empty gutter invokes the focused virtual node. The normal
  Button callback increments once; radio selection excludes its sibling; Switch says off/on.
- `final-editor-*`, `password-complete-readback*`, `password-final-*`, `focus-independent-switch*`:
  Gboard enters real text, label and value stay separate, password readback contains no plaintext,
  and the caret/keyboard stay in the editor while accessibility focus explores another control.
  Password keyboard suggestions are absent. SetText itself is separately covered by native
  instrumentation; an explicit TalkBack SetText command was not available in this interaction.
- `final-list*`, `final-tab-*`, `latest-tree-*`: individually reachable logical rows, selection,
  second-tab content, menu-driven expansion/collapse, and touch exploration of the expanded leaf.
- `slider-flick-*`, `progress-focus*`, `final-progress-*`: current values and slider role are spoken;
  adjustment changes 25 to 26 and back to 25. Native range limits and rejection of read-only
  progress writes are checked by instrumentation and deterministic mapper tests.
- `final-menu-*`, `final-custom-*`: normal Menu and custom logical-child callbacks update visible
  counters. The latter remains a canonical child without its own native View.
- `dynamic-*`, `reorder*`, `renamed-*`, `disabled-check-*`, `enabled-check-*`, `hidden-check-*`,
  `shown-check-*`, `latest-multi-*`: add/remove, positions after reorder, name, enabled/visible
  changes, independent multi-selection and per-item deselection all work without restarting.
- `recreated-button-*` and `activity-recreation-evidence.log`: changing font scale triggers actual
  Activity destruction/recreation in the same process; the new host accepts TalkBack focus and
  double-tap. The temporary font-scale setting was restored. Native instrumentation independently
  rejects stale identities after host replacement and Activity.Recreate.

The gesture run found and fixed five issues: numeric Switch state announcements, retained old
windowless input focus, password keyboard suggestions, expanded-child hit testing, and replacing
multi-selection instead of changing only the target item. Regression tests reproduce each cause.
The fixture adds visible callback counters and ordinary dynamic control operations; it adds no
alternate semantic hierarchy. Mixed CheckBox state remains mapper-tested; this fixture toggles
the ordinary two-state checkbox. Popup windows, advanced IME, full viewport scrolling, localization
and physical-device validation remain outside this run. ComboBox/menu popup behavior is
**BLOCKED BY DOCUMENTED LIMITATION**; their currently advertised semantics are what passed above.

Final automated regression after the runtime fixes: 1,837/1,837 tests passed, including Android
mapper 52, provider/session 30, Android total 154, core 909, Windows 64, Designer 622, Testing 46,
cross-platform sample 16 and VSIX 26. Fresh Debug and Release APK instrumentation both passed
49/49 assertions; Release used the existing trimming/AOT configuration. Both solution builds
and both Android builds completed with zero warnings/errors. ApiCompat passed four backward
comparisons; package validation passed 9 NuGet packages and 8 symbol packages without changing
versions or dependencies.

The final search found zero plaintext test-marker matches in this run's text logs, transcripts,
instrumentation results and artifacts. Detailed IME diagnostics remained disabled. TalkBack
Display speech output was restored to off; secure enabled_accessibility_services returned to
absent (`null`) and accessibility_enabled to `0`. The temporary system font_scale entry also
returned to absent. The task-started emulator and log collector were stopped after verification.
