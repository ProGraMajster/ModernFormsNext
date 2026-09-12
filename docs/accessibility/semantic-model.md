# Shared accessibility semantic model

ModernFormsNext exposes one canonical, platform-neutral semantic model in
`ModernFormsNext.Accessibility`. The existing `AccessibleObject` remains its root abstraction. A
control normally maps through this chain:

```text
Control
  -> ControlAccessibleObject
  -> AccessibleObject
  -> thin platform adapter
  -> native accessibility backend
```

The model does not depend on Windows UI Automation, MSAA, Android accessibility, native handles,
IPC, or an automation transport. A backend may use an internal data-transfer type, but applications
and diagnostics or automation features consume the canonical `AccessibleObject`
surface instead of defining another public semantic domain.

## Compatibility role and normalized control type

`AccessibleRole` retains WinForms/MSAA-compatible role values. It remains useful to the existing
Windows MSAA bridge and to applications migrating custom accessible objects from WinForms.

`AccessibleControlType` is the normalized ModernFormsNext classification. It describes concepts such
as button, edit, list item, tree item, tab item, slider, progress bar, and window without copying a
native platform enum. `Control.AccessibleControlType` defaults to `Default`, allowing
`ControlAccessibleObject` to infer the type. A custom control can explicitly set that property or
override `AccessibleObject.ControlType`.

A top-level `Form` is a window by default. While it is shown through `ShowDialog`, its root maps to
the compatibility `Dialog` role and normalized `Dialog` control type.

Both properties are intentional:

- `AccessibleRole` is the compatibility/legacy role.
- `AccessibleControlType` is the canonical normalized semantic type.

## Tree views and visibility

`AccessibilityView` classifies an object into progressively more user-relevant projections:

- `Raw` appears only in a complete raw tree.
- `Control` also appears in an interactive-control projection.
- `Content` also appears in a content projection.
- `Hidden` is excluded from active trees.
- `Default` lets a control infer the appropriate value.

These values classify semantic nodes; they are not Windows UIA `TreeScope` or native view values.
Active child enumeration filters `Hidden`; native adapters and bounded automation queries use
the existing view classification. For the standard mapping, `Default` resolves labels, images, shapes,
and progress indicators to `Content`, and other controls to `Control`.

Setting `Control.AccessibilityView` to `Hidden` changes only the accessibility tree; it does not
change the visual tree or `Control.Visible`. Standard invisible and disposed controls report a
hidden view and are omitted from their parent's active child sequence. `AccessibleStates.Invisible`
and `AccessibleStates.Offscreen` remain available when an object is queried directly.

## Names, values, and sensitive data

`Name` labels an object. `Value` represents editable text, a current selection, or a numeric value.
They are separate semantic fields.

Standard name fallbacks are deliberately conservative:

- buttons, check boxes, radio buttons, switches, labels, and similar labelled controls use
  `AccessibleName`, then visible `Text`, then `Control.Name`;
- text boxes and numeric up-down controls use `AccessibleName`, then `Control.Name`, and never use entered text as their name;
- a form root uses its window title;
- logical list, tree, tab, and menu items use the corresponding item text.

`AccessibleObject.IsSensitive` marks values that must be redacted from accessibility snapshots,
diagnostics, and logs. A password `TextBox` reports `IsSensitive == true`, retains its label/name and
edit control type, and returns an empty semantic `Value`. Setting the text through a supported action
does not make the underlying password readable through `Value`.

Both `PasswordCharacter` and the shared `TextInputOptions.Scope == Password` mark a text editor
sensitive. Text ranges, lengths, selection and formatting metadata are unavailable for protected
text. Native adapters and automation capture also check ancestors: descendant names, descriptions,
IDs and values cannot expose data from a protected group. A password field's own explicitly
authored label remains available. See [text privacy](../accessibility-text.md) and
[snapshot diagnostics](diagnostics-and-designer.md) for current lifetime and redaction rules.

Sensitivity protects payload disclosure; it is not a blanket denial of native assistive actions.
Ordinary button activation/focus and a password field's supported SetText route remain available.
Protected text/range/selection capabilities stay unavailable. AutomationSession applies its own
stricter action policy independently of the native accessibility bridge.

## Actions

`AccessibleActions` is the only public action vocabulary. `SupportedActions` is a flags value used
for capability inspection; `PerformAction` accepts exactly one action and an optional parameter.
It returns `false` for an unsupported action, an incompatible parameter shape, an unavailable object, or a
rejected state change. Capability flags describe the current state: disabled, invisible, hidden,
detached, or otherwise unavailable elements do not advertise actions, and expandable controls
advertise `Expand` or `Collapse` according to their current state rather than both at once.
Normal control validation and application callbacks can still throw; for example, the date
picker's existing parser rejects malformed date text. Native adapters contain these exceptions
at their platform boundary rather than pretending that the action succeeded.

Standard actions call normal framework behavior:

- `Button`: `Invoke` through `PerformClick`;
- `CheckBox` and `Switch`: `Toggle`;
- `RadioButton`: `Select` with the normal mutually-exclusive update;
- editable `TextBox`: `SetValue`;
- `ComboBox`: `Expand` and `Collapse`, with `Select` on logical items;
- list/list-view items: `Select`, and list-box items also support `ScrollIntoView`;
- tree items: `Select`, `Expand`, `Collapse`, and `ScrollIntoView`;
- tab items: `Select`;
- `TrackBar`, `NumericUpDown`, and `ScrollBar`: `SetValue`, `Increment`, and `Decrement`;
- `LinkLabel` link children: `Invoke` through the existing link activation path and normal `Focus`;
- viewport owners: typed `Scroll`, with `ScrollIntoView` on supported descendants and items;
- grid cells/headers: normal selection, value editing, reveal and sortable-header activation;
- date pickers: value parsing, checkbox toggle, stepping and expansion when an actual popup host exists;
- command menu items: `Invoke`; submenu items advertise expand/collapse;
- focusable controls: `Focus` through the normal framework focus path.

Unsupported behavior is not advertised. For example, a read-only text box omits `SetValue`, a
`ProgressBar` exposes range information without a write action, and a separator exposes no action.
Actions do not call private event-handler delegates; bound grid editing reuses the existing
model-property conversion and commit path.

## Values and ranges

`AccessibleRangeValue` reports `Value`, `Minimum`, `Maximum`, `SmallChange`, `LargeChange`, and
`IsReadOnly`. It validates finite, ordered range metadata. `TrackBar` exposes a writable range;
`NumericUpDown` and `ScrollBar` also expose writable ranges, and `ProgressBar` exposes a read-only
range. The representation uses the control's logical value units, not pixels. See the
[composite control contract](current-controls.md) for decimal precision, embedded editor read-only
behavior, and the distinction between a scrollbar range and a scrollable viewport.

`AccessibleObject.ScrollInfo`, `GridProvider`/`GridCell`, and `TextProvider` add optional
capabilities to the same objects. They describe the actual viewport, managed row/cell model and
editor document; they do not create another scroll state, virtualized collection or text document.
See [viewport requests](scroll-viewports.md), [grids and calendars](grids-and-calendars.md), and
[Text/TextRange](../accessibility-text.md) for parameters, units and supported operations.

## Identity

Every `AccessibleObject` receives a positive process-session `RuntimeId`. Allocation is atomic,
thread-safe, and fails explicitly if the signed 64-bit identifier space is ever exhausted rather
than wrapping into duplicate or negative identifiers. The identifier remains stable for that object
but is not persistent across application runs.

Logical-item peers are cached for the life of their represented element, so moving an item does not
change its runtime identity. `ListBox` and `ComboBox` assign identity per collection occurrence:
inserting the same object instance more than once produces distinct semantic elements, and moving
either occurrence preserves the corresponding peer and runtime identifier.

`AutomationId` is a separate developer-facing semantic identifier. `Control.AccessibleAutomationId`
can set it explicitly and otherwise falls back to `Control.Name`. Reordering a control does not alter
either identifier.

## Logical and custom children

`GetChildCount` and `GetChild` can return objects that are not `Control` instances. The protected
`ControlAccessibleObject.GetAccessibilityChildren()` method lets a custom control combine or replace
visual control children with logical children while retaining the standard filtering behavior.

The built-in Phase 1 mapping creates logical peers on demand for:

- `ComboBox` and `ListBox` items;
- `ListViewItem` instances;
- hierarchical `TreeViewItem` instances;
- `TabPage` tab headers;
- `MenuItem` instances and nested menu items.

Phase 4 additionally exposes real `LinkLabel.Link` ranges, the actual `NumericUpDown` editor
and two logical spinner buttons, current grid rows/cells/headers, and the date picker's existing
checkbox/buttons/calendar. These additions retain the same identity and action paths;
[control details](current-controls.md) and [grids and calendars](grids-and-calendars.md) describe
attachment and presentation-generation boundaries.

These peers use weak owner/item references where retaining an object would extend UI lifetime. The
control-to-peer notification route also uses a weak owner reference, so an externally retained peer
does not keep its control alive. Item move operations preserve runtime identity. Removal or owner
disposal detaches the peer from its parent and makes it hidden. Tree selection is cleared when a
selected subtree is removed. The implementation does not materialize controls for logical items and
does not implement the separate virtualization roadmap work.

Example custom semantic child:

```csharp
sealed class PaintedGroupAccessibleObject : Control.ControlAccessibleObject
{
    private readonly AccessibleObject action;

    public PaintedGroupAccessibleObject(Control owner, AccessibleObject action)
        : base(owner)
    {
        this.action = action;
    }

    public override AccessibleControlType ControlType => AccessibleControlType.Group;

    protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
    {
        yield return action;
    }
}
```

The logical child's `Parent` should return the group object, and the same child instance should be
returned while the painted item remains alive.

## State and focus

The existing `AccessibleStates` enum remains canonical. Enabled and unchecked states are represented
by the absence of `Unavailable` and `Checked`/`Mixed`; disabled, invisible, focusable, focused,
checked, mixed, selected, expanded, collapsed, read-only, protected, and offscreen states use the
existing flags.

The model reuses normal framework keyboard focus. It does not introduce an accessibility focus manager.
`Focus` calls the regular control selection/focus path, and `Focused` reports that framework state.
Android's independent accessibility-focus state stays in its existing native provider; it does not
replace the control's keyboard focus or focus-bound IME session.

## Notifications

The existing `NotifyClients`/`AccessibleEvents` path remains the only notification mechanism.
Semantic changes map as follows:

| Semantic change | Existing event |
| --- | --- |
| name | `NameChange` |
| value | `ValueChange` |
| enabled, checked, expanded, read-only, or other state | `StateChange` |
| keyboard focus | `Focus` plus `StateChange` |
| selection | `Selection`, `SelectionRemove`, or `SelectionWithin` |
| child add, remove, reorder, or dispose | `Reorder` |
| bounds | `LocationChange` |
| numeric limits, precision, or step metadata | `RangeValueChanged` |
| committed viewport position or extent | `ScrollChanged` |
| committed document content | `TextChanged` |
| text selection, independently of item selection | `TextSelectionChanged` |
| text formatting or shaped geometry | `TextAttributesChanged` |

Control and logical-item collection mutations update the queried tree without recreating the whole
control hierarchy. Notifications remain platform-neutral; Windows and Android translate them
through their existing provider paths. Framework-only negative event IDs are not passed as raw
MSAA notifications. Text notifications contain metadata, not document payloads.

## Threading and lifetime

Control state is UI-thread-affine. Read mutable semantic properties and call `PerformAction` on the
owning UI thread. Windows and Android native callbacks dispatch before querying or mutating
controls and reject retired owners. Detached snapshots and diagnostic reports can be read after
capture without accessing live UI state.

`Control.AccessibilityObject` is lazy and cached. Merely constructing controls does not build a
semantic snapshot or native provider. Standard accessible objects keep a weak reference to their
control owner. Consumers may retain an accessible object after removal, but it then reports no active
parent and cannot perform control actions.

## Current implementation and remaining boundaries

`PlatformAccessibleObjectAdapter` remains a thin internal bridge to the existing public WindowKit
transport. Existing `IPlatformAccessibleObject` members remain compatible; optional internal
capabilities carry scroll, grid and text data without duplicating the public semantic enums.
The Windows `WM_GETOBJECT`/MSAA path continues to
consume names, roles, states, values, bounds, hierarchy, navigation, selection, and default actions.

The issue phases and their evidence remain distinct:

- **Phase 2 — Windows:** native UI Automation provider, common patterns, UIA event translation, and
  automated real-HWND/client coverage are implemented. Accessibility Insights, Inspect.exe, and
  screen-reader validation remain manual.
- **Phase 3 — Android:** `AccessibilityNodeProvider` mapping, native events/focus, deterministic
  tests and emulator instrumentation are implemented; the 20-point TalkBack gesture checklist passed
  on Pixel_8/API 34. Physical-device and broader platform coverage remain deferred.
  See [Android accessibility backend](../android-accessibility.md).
- **Phase 4 — current implementation under final validation:** link/numeric/scrollbar composites,
  real viewport scrolling and reveal, managed grid/table and date/calendar peers, editor text
  ranges, opt-in native accessibility preferences and theme typography scaling, bounded snapshot
  diagnostics, Designer metadata round trips, and representative samples. Windows adds Scroll,
  Grid/Table and Text patterns; Android adds corresponding viewport/grid metadata/actions and
  text selection/granularity. The [Phase 4 Android runner](android-phase4-validation.md) records
  its own source-specific result; historical Phase 3 success is not a Phase 4 result.

Related infrastructure is already available: [TestHost](../testing/testhost.md) supplies real
input/focus/layout, controlled time, scoped services and detached control/raster snapshots;
[AutomationSession](../automation.md) supplies bounded canonical semantic snapshots/actions and
waits, with a separately enabled [Windows IPC bridge](../automation-live-bridge.md).
[Commands](../commands.md), [shared IME composition](../text-input.md),
[application lifecycle](../application-lifecycle.md), and
[Android hardware-key routing](../android-hardware-input.md) use the existing control/backend paths.
None of these creates a second accessibility tree.

Issue [#59](https://github.com/ProGraMajster/ModernFormsNext/issues/59) remains open.
Future recycled item containers belong to [#55](https://github.com/ProGraMajster/ModernFormsNext/issues/55),
the full inspector/picker UI to [#61](https://github.com/ProGraMajster/ModernFormsNext/issues/61),
general Android Form/popup hosting to [#72](https://github.com/ProGraMajster/ModernFormsNext/issues/72),
and physical/broader Android reliability to [#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69).
Rich embedded-document adapters and an MCP transport are separate work. Final-source native,
screen-reader and physical evidence must be recorded independently; the current implementation
does not certify universal accessibility compliance or complete cross-platform parity.

See [opt-in preferences](preferences.md), [diagnostics and Designer](diagnostics-and-designer.md),
and the [validation record](../development/codex-autonomous-issue-run.md) for the current scope.
