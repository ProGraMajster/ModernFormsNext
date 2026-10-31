# Flow and table layout from right to left

`Control.RightToLeft` and `WindowBase.RightToLeft` (including `Form`) select the layout reading
direction through the existing control tree. The local default is `Inherit`; a root resolves to
`No`. An explicit `No` or `Yes` stops inheritance. The getter returns the effective direction.
Designer metadata and reset operations preserve the authored `Inherit` selection rather than
persisting a resolved value from the current parent.

```csharp
using var form = new Form { RightToLeft = RightToLeft.Yes };
var flow = form.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill });
flow.Controls.Add(new Button { Text = "مرحبا", Width = 110, TabIndex = 0 });
flow.Controls.Add(new Button { Text = "שלום", Width = 110, TabIndex = 1 });

// This container is placed by its RTL parent but arranges its own children from the left.
flow.Controls.Add(new FlowLayoutPanel { RightToLeft = RightToLeft.No });
```

This is a layout contract for FlowLayout and TableLayout. It does not automatically select a
culture, translate strings, mirror native window chrome, reverse text, or replace the existing
text shaping and accessibility implementations. Explicit text alignment and other containers'
ordinary Dock/Anchor behavior remain as authored. Localization integration remains [#14](https://github.com/ProGraMajster/ModernFormsNext/issues/14);
broader accessibility coverage remains [#59](https://github.com/ProGraMajster/ModernFormsNext/issues/59).

## Flow direction, wrapping and insets

FlowDirection is relative to the effective reading direction, following the
[WinForms flow contract](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/flowlayoutpanel-control-overview).
Changing it does not reorder the Controls collection or rewrite child TabIndex values.

| FlowDirection | RightToLeft.No | RightToLeft.Yes |
| --- | --- | --- |
| LeftToRight | Left to right, rows downward | Right to left, rows downward |
| RightToLeft | Right to left, rows downward | Left to right, rows downward |
| TopDown | Downward, columns to the right | Downward, columns to the left |
| BottomUp | Upward, columns to the right | Upward, columns to the left |

Wrapping still uses the physical viewport constraint. Ambient RTL reflects horizontal placement,
including horizontal alignment and asymmetric child margins. Explicit reverse flow retains its
existing no-margin-swap transformation before ambient mirroring. Padding remains the physical
viewport inset: Left is still the left inset, including in a mixed-direction tree. The transform
uses both edges of that padded rectangle, so nonzero borders, padding and scrolling do not erase
the layout origin.

## Table columns, spans and alignment

Column zero is visually rightmost in RTL. Row zero remains at the top. Column/row indexes,
GetControlFromPosition, placement, spans, collection order and stored metadata stay logical.
Horizontal margins and Anchor/Dock alignment inside a cell mirror with its logical column;
vertical alignment is unchanged. Explicit direction on a child affects its own subtree, not
which cell the parent assigns to it.

## Scrolling, input and semantics

ScrollableControl continues to own one positive physical offset. Zero is the physical left/top
endpoint; a direction change does not silently reset a valid scrollbar value. Reverse overflowing
Flow/Table layouts use their full content extent so every child can be revealed through the
existing scrolling/accessibility actions. Relayout and hide/show retain the canonical offset,
and range changes use the existing clamp/displacement path. Both padding edges contribute to
the scroll extent. There is no independent RTL scroll position or platform-specific layout path.

Tab and Shift+Tab retain authored logical traversal. ControlFocusScope still owns selection.
On Windows, unconsumed translated Tab reaches that existing root input route, including Shift
modifiers. A handled key-down continues to suppress its translated navigation character.
AccessibleObject retains names, runtime identities and logical child order, while bounds and hit
testing follow the actual mirrored geometry. A geometry or semantic test does not establish
screen-reader behavior or full bidirectional text support on every backend.

Direction changes run synchronously on the owning UI thread. Own layout and repaint invalidation
precede RightToLeftChanged; descendant reconciliation follows. Reparenting uses the same tree.
Reentrant changes retain the newest selection. Other still-current descendants are reconciled
before observer exceptions are reported; multiple exceptions form an AggregateException.
Window events use the window as sender and forward through its existing ControlAdapter.

## Designer and examples

Flow/Table preview extends the same bounded inert runtime-tree adapter used by page preview;
the production engines calculate geometry. Root direction and explicit nested boundaries enter
that tree. Metadata does not load application assemblies or execute application getters, setters,
constructors, converters, factories, bindings or event handlers.

External controls in Flow/Table use closed Control placeholders and authored sizes. Unknown
application properties are retained as data, while known base layout properties still validate.
Custom AutoSize/measure/layout code is not run, so its geometry is not claimed equivalent to
executing that application. Existing page preview rejects unsupported external page graphs.
The adapter keeps its limits of 256 nodes, depth 16, 2048 property values and 64 KiB combined property
text. Table work also follows the [Designer preview resource policy](designer-preview-resources.md),
without limiting runtime TableLayoutPanel or generated code. Invalid or unavailable inherited directions produce a diagnostic instead of invisible
interactive targets. An explicit valid boundary can resolve its own direction independently.

ControlGallery's existing FlowLayoutPanel and TableLayoutPanel pages expose RTL/LTR toggles,
Arabic/Hebrew text, asymmetric insets, a mixed LTR subtree and a spanning table cell. Native
certification uses the same production fixture on Windows and Android, including retained trees
after Activity replacement. See [layout parity](architecture/designer-runtime-layout-parity.md)
for the manifest, generated-code and device-edge checks.
