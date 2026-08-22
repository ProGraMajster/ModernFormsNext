# Designer copy, cut, paste, and duplicate

The ModernFormsNext Designer supports detached, in-session editing of one selected control subtree.
Copy, Cut, Paste, and Duplicate operate on the design model and reuse the same typed transaction
manager as all other authored Designer changes. They do not introduce a second mutation or undo
system.

## Commands and history

| Command | Shortcut | Model effect | History effect |
| --- | --- | --- | --- |
| Copy | `Ctrl+C` | Stores a detached subtree; the document is unchanged. | None; dirty state and save revision are unchanged. |
| Cut | `Ctrl+X` | Stores the subtree, then removes it. | Exactly one undo unit. |
| Paste | `Ctrl+V` | Validates and inserts a new subtree. | Exactly one undo unit. |
| Duplicate | `Ctrl+D` | Creates and inserts a detached copy next to the selected node. | Exactly one undo unit; the clipboard is unchanged. |

Undo and redo restore the tree, selected node, dirty state, generated-code input, Document Outline,
Property Grid, and Designer surface through the normal transaction notifications. Copy remains
available only for an attached control node. The Form or UserControl design root and generated
`SplitContainer` panel nodes cannot be copied as independent child controls.

## Private versioned payload

The internal clipboard stores deterministic JSON with format identifier `ModernFormsNext.Designer`
and version `1`. The payload contains only persisted design data:

- the root control and ordered descendant tree;
- type and control names, bounds, and member visibility;
- sorted property values and event-handler names;
- null, string, Boolean, `Int32`, `Double`, enum, and recursively structured property values.

This is a private Designer interchange contract, not `.mfdesign` and not a public serialization API.
It deliberately contains no live `Control` or `DesignControlNode` reference, runtime handle,
selection, history, dispatcher, delegate, event subscription, or project service. Deserialization
does not resolve types, load assemblies, construct controls, or execute project code. It uses
`System.Text.Json`; unsafe general object serialization such as `BinaryFormatter` is not used.

Before any transaction starts, the reader verifies the exact format and version, required fields,
known JSON members, C# identifiers, safe type-name text, canonical property representations,
non-negative sizes, and resource bounds. Payloads larger than 4 MiB,
deeper than 96 levels, or containing more than 10,000 controls are rejected. Empty, corrupt,
unsupported, incomplete, ambiguous, or malicious payloads leave the model, selection, history, and
dirty state unchanged.

The clipboard belongs to one `DesignerSession`. It therefore works between documents open in the
same session and remains valid after the source document is closed, but it is not persisted into
`.mfdesign` and does not use the operating-system clipboard. Closing the Designer session clears it.

## Paste target policy

The insertion target is resolved before mutation:

1. When the selected node is a container, Paste inserts inside that container.
2. When the selected node is a leaf, Paste inserts into the selected node's parent collection.
3. When the design root is selected, Paste inserts into the root control collection.
4. An unattached, structural, or incompatible target is rejected without history.

`TabControl` and `SplitContainer` use their Designer-owned child collections: controls paste into
the selected tab page or first split panel, while a `TabPage` can only be pasted or duplicated
directly within a `TabControl`. This preserves hierarchy invariants rather than creating a cyclic or
corrupt tree.

## Names, layout, and complex data

Every pasted or duplicated node receives a document-wide, case-insensitively unique C# identifier.
The remap covers the entire subtree, including structural split-panel names and display metadata.
Existing numeric suffixes advance naturally (`button1` becomes `button2`), and occupied names are
skipped deterministically. Event bindings preserve handler names; the operation neither creates a
delegate nor generates a handler method. There is currently no general control-reference property
schema to rewrite beyond the structural metadata represented by the design model.

Absolute, non-docked roots receive a 16-logical-pixel offset. Docked controls keep their bounds
because runtime layout owns their final position. `FlowLayoutPanel` and tab containers preserve
sequence without an artificial coordinate offset. `TableLayoutPanel` assigns the next available
cell and resets copied row/column spans to one while retaining the rest of the subtree data.
Padding, Margin, minimum/maximum sizes, gradients, geometry, interaction effects, layout
transitions, tab pages, children, event bindings, and supported structured collections are deep
copied with no shared mutable Designer value graph.

Project UserControls and unavailable custom control type names cross the clipboard as data-only
nodes. This preserves the existing safe-preview boundary: copy/paste never instantiates them or
loads executable project code. Normal reference-cycle validation still runs before insertion.

## Current boundaries

- Selection remains single-control; multi-selection and group editing are tracked separately in
  [#35](https://github.com/ProGraMajster/ModernFormsNext/issues/35).
- The operating-system/runtime clipboard contract is separate work tracked in
  [#57](https://github.com/ProGraMajster/ModernFormsNext/issues/57).
- Non-visual components cannot be pasted into a component tray because the tray is not implemented.
- General cross-property control-reference remapping will require an explicit reference schema;
  clipboard code does not guess from arbitrary strings.
- Complete Flow/Table right-to-left behavior remains part of
  [#89](https://github.com/ProGraMajster/ModernFormsNext/issues/89).
- Default-event method creation and richer event workflows remain part of
  [#37](https://github.com/ProGraMajster/ModernFormsNext/issues/37).

See [Designer transactions and undo/redo](designer-transactions-and-undo.md) for atomicity,
save-marker, retention, and replay semantics.
