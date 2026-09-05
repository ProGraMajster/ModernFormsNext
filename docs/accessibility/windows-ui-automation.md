# Windows UI Automation backend

ModernFormsNext exposes its canonical accessibility tree to Windows UI Automation (UIA) through
the Windows WindowKit backend. This implementation is Windows-only. It does not change the semantic
source of truth in `ModernFormsNext.Accessibility`, and it does not add public UIA types to the
shared framework API.

## Architecture

The data path is:

```text
Control
  -> ControlAccessibleObject / logical AccessibleObject
  -> PlatformAccessibleObjectAdapter
  -> WindowsUiaProvider
  -> UIAutomationCore.dll
  -> UIA client
```

`WM_GETOBJECT` keeps the existing `OBJID_CLIENT` MSAA response. A request for
`UiaRootObjectId` lazily creates a UIA fragment root and returns it through
`UiaReturnRawElementProvider`. Providers are not created merely because a window exists or a
semantic notification is raised.

The COM declarations and generated `ComWrappers` live entirely in the Windows backend. Their ABI
is shaped after the provider interfaces in `UIAutomationCore.h`; the product does not reference
WPF or WinForms UI controls and does not transitively require WindowsDesktop reference assemblies.

## Tree, identity, bounds, and views

Each provider wraps an existing canonical semantic object. It does not require a `Control`, so
logical list, tree, tab, menu, and custom children participate in the same fragment. Parent, child,
sibling, hit-test, and focus navigation delegate to the canonical object.

Provider wrappers are cached per HWND with weak semantic keys. A living semantic object keeps the
same provider and runtime ID across reorder operations. A detached child returns
`UIA_E_ELEMENTNOTAVAILABLE`; destroying the HWND invalidates the context and disconnects its root
provider after the inbound window message has returned.

Canonical Windows bounds already pass through the backend's scaled `PointToScreen` path and are
physical desktop pixels. The UIA converter therefore validates the rectangle but deliberately does
not apply DPI scaling a second time. Empty, invisible, offscreen, and zero-area objects report
`IsOffscreen`.

`AccessibilityView.Control` is a UIA control element, `Content` is both a control and content
element, while `Raw` and `Hidden` are excluded from normal control/content views. The raw fragment
still preserves canonical relationships for client tooling that explicitly requests it.

## Properties and patterns

The backend maps `Name`, `AutomationId`, canonical control type, enabled/focus state, screen bounds,
offscreen state, help text, password state, control/content view, class name, and
`FrameworkId = ModernFormsNext`. Control types come from `AccessibleControlType`, never CLR type
name inference.

Pattern availability follows canonical capabilities:

| UIA pattern | Canonical source |
| --- | --- |
| Invoke | `AccessibleActions.Invoke` |
| Toggle | `AccessibleActions.Toggle` and checked/mixed state |
| Value | Edit semantics; mutation requires `AccessibleActions.SetValue` |
| RangeValue | `AccessibleRangeValue`; progress is read-only |
| ExpandCollapse | expand/collapse actions and expanded/collapsed state |
| Selection | supported selection-container control types |
| SelectionItem | `AccessibleActions.Select` and the canonical parent |
| ScrollItem | `AccessibleActions.ScrollIntoView` |

`Invoke`, `Toggle`, value changes, range changes, expand/collapse, selection, scrolling into view,
and focus all route through the canonical action or selection path. They do not invoke framework
events or mutate control fields directly. List-box add/remove selection honors its real
single/multiple selection mode.

Full `TextPattern` is deferred because the shared model does not yet expose stable text ranges.
Full `ScrollPattern` is also deferred until the shared controls have a canonical scroll viewport
contract; advertising it now would create behavior that controls cannot honor. Logical items do
expose `ScrollItemPattern` where `ScrollIntoView` is real.

## Privacy, threading, and failures

Password controls report `IsPassword = true`. Their plaintext is never returned through the Value
property or Value pattern getter; the getter fails predictably, while an enabled editable password
control can still accept a replacement through the normal canonical `SetValue` action. Native
callbacks report `E_ACCESSDENIED` for password reads. UIA
diagnostics include exception types only and never include names or values.

All semantic reads and mutations cross the framework dispatcher. Calls already on the UI thread
execute directly; other callers use the existing synchronous dispatcher path with a bounded
timeout. Detached nodes, destroyed windows, shutdown dispatchers, disabled controls, unsupported
actions, and invalid values are translated to deterministic HRESULT failures at the generated COM
boundary instead of escaping through the native callback.

## Events

Canonical notifications map to UIA focus, property, selection, and structure notifications.
Name/help/value/range/bounds/enabled/toggle/expand/selection state are mapped to their corresponding
UIA property IDs. Password value notifications are suppressed. Reorder notifications use
an event-only snapshot of child runtime IDs to distinguish `ChildAdded`, `ChildRemoved`, bulk
changes, and `ChildrenReordered` without retaining semantic nodes or building another hierarchy.
The native runtime-ID argument is supplied only for `ChildRemoved`, as required by
`UiaRaiseStructureChangedEvent`; explicit show/hide notifications additionally map to
child-added/removed and offscreen changes. Native events are raised only after UIAutomationCore
reports a listening client.

## Automated validation

`WindowsUiaProviderTests` covers properties, control types, views, pattern availability and
behavior, logical/custom navigation, reorder/removal identity, password redaction, range metadata,
selection, focus, dispatcher routing, event mapping, COM interface exposure, and disposal.

The real integration smoke uses two test-only processes. The host creates and shows a real
ModernFormsNext HWND. An isolated Windows UIA client calls `AutomationElement.FromHandle`, finds a
semantic child, queries properties, obtains `InvokePattern`, invokes the control, sets focus, and
then the test closes the window and verifies a clean process exit. This intentionally avoids a
synthetic `WM_GETOBJECT` message.

## Manual inspection checklist

This checklist is evidence for a human validation session; repository builds and automated tests do
not mark it complete.

1. Start `samples/ControlGallery` on Windows.
2. Open Accessibility Insights for Windows or Inspect.exe.
3. Locate the ModernFormsNext top-level window.
4. Inspect raw, control, and content trees.
5. Verify Button name, type, bounds, focus, and Invoke.
6. Verify CheckBox Toggle and checked state.
7. Verify RadioButton selection.
8. Verify normal TextBox Value and editing.
9. Verify password TextBox redaction and `IsPassword`.
10. Verify ComboBox ExpandCollapse and selection.
11. Verify ListBox and ListView logical children and selection.
12. Verify TreeView hierarchy, expand/collapse, selection, and scroll-into-view.
13. Verify TabControl and tab-item selection.
14. Verify TrackBar RangeValue and mutation.
15. Verify ProgressBar read-only RangeValue.
16. Verify menu/submenu hierarchy and actions.
17. Observe keyboard focus events while tabbing.
18. Toggle enabled/disabled state and observe property changes.
19. Exercise single and multiple selection modes.
20. Add, remove, and reorder logical items and observe structure changes.
21. Inspect a custom semantic child that has no backing `Control`.
22. Close and reopen the window and confirm stale elements disappear without client or app crashes.

Screen-reader smoke testing remains a separate manual step. Android accessibility, TalkBack, Linux,
macOS, developer tooling, agent automation, and broader Phase 4 coverage are outside this backend.
