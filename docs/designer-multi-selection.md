# Designer multi-selection and group editing

The Designer uses one ordered selection in `DesignerSelectionService`. `SelectedNodes` contains
distinct node identities; `SelectedNode` remains the primary member for existing hosts. The Form
or UserControl root is represented by an empty selection. Selection alone changes no model data,
dirty state or history. Use the session on its UI thread.

## Selecting controls

- **Ctrl-click** toggles a control. **Shift-click** adds a control and makes it primary; it does
  not select an outline range. AltGraph is not a Ctrl shortcut.
- Clicking an existing member retains the group and makes that member primary. Clicking an
  unselected control without modifiers replaces the selection.
- Drag an empty part of the design-root client area to create a marquee. Controls whose visible
  bounds are fully enclosed are selected in outline order on release. Ctrl/Shift makes this
  additive. Escape or capture/focus loss cancels the marquee without changing selection.
- Surface and Document Outline share membership and primary. A filled corner marker on the
  surface and an accent bar in the outline identify the primary. Group members have borders;
  resize handles remain a single-control operation.

Point and marquee hit testing share active-tab, clipping, visibility and project UserControl
boundaries with preview. Embedded component internals stay outside the owning document's
selection. Monitor DPI and fit-to-viewport scale use the existing coordinate mapper.

```csharp
session.Host.Selection.SelectNodes(new[] { saveButton, cancelButton }, saveButton);
session.MoveSelectedNodes(deltaX: 12, deltaY: 0);
session.ArrangeSelectedNodes(DesignerGroupArrangement.SameHeight);
```

`Select(node)` replaces a group even if that node is already primary. `SelectNodes` validates
the entire input before publication, deduplicates by reference identity and defaults primary
to the last distinct member. An explicit primary must be a member. Retained membership views
are immutable; nodes are borrowed model objects. Membership and enumerated input each use the
existing 10,000-node budget. Reentrant newer selection wins over a returning older notification.

## Moving and arranging

Dragging a member moves all selected roots by one common offset. Selected descendants travel
with their selected ancestor once. Independent source parents are supported; relative document
positions are retained. Docked controls and children whose position is owned by flow, table,
tab or page containers reject manual group movement.

The toolbar **Arrange** menu offers left/right/top/bottom alignment, same width/height/size,
and horizontal/vertical gap distribution. Alignment and size use the primary as reference.
These commands require independently selected siblings; distribution requires at least three.
Distribution keeps the first leading and last trailing edges, orders ties by outline order,
and apportions integer remainders from the leading edge. Negative gaps are supported. Size
commands reject incompatible minimum/maximum sizes or AutoSize for the whole group.

`ReparentSelectedNodes(destination)` and outline drops move the normalized group into one
container. Free-layout destinations preserve document-space positions; managed destinations
retain runtime placement rules. Table groups retain spans and use production automatic placement
after bounded preview admission. Page slots, structural split panels, cycles, depth and coordinate
arithmetic are checked before tree mutation. Runtime layout count limits are unchanged. Outline
up/down retains selected sibling order; move-out requires a common parent. Move-to-next-container
acts on the whole group and reports a rejected destination instead of silently trying another.

Each move, arrangement, reparent, delete, cut, paste or duplicate is one transaction. Undo/redo
and rollback restore ordered selection and primary with the model. Invalid operations change no
member. Modifier selection and marquee create no undo unit.

## Shared Property Grid

The grid shows the intersection of compatible properties and shared child properties. Differing
values have an explicit mixed state displaying **(multiple values)**; authored text equal to that
label remains an ordinary value. Mixed numeric, Boolean and enum editors begin as empty text.
Opening and closing an untouched mixed editor writes nothing. Enter a value to assign it to all
selected nodes, or explicitly clear the field to assign an empty string where valid. Every target
uses its existing converter and validator; any rejection rolls the complete edit back. Membership
changes cancel a stale editor even when primary stays the same.

Names, mutable complex/dialog values and collection editors require single-control editing.
Compatible scalar child properties remain available. Events are unavailable for a group; existing
single-control event editing is unchanged. Event navigation improvements belong to issue #37.

See [copy and paste](designer-copy-paste.md), [transactions](designer-transactions-and-undo.md) and
[preview resource policy](designer-preview-resources.md) for persistence, ownership and budgets.
This feature does not add runtime multi-selection to controls or imply an Android Designer.
