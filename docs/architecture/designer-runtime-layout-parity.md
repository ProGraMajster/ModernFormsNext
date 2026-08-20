# Designer/runtime layout parity

The Designer/runtime layout parity suite protects the semantic geometry stored in `.mfdesign`
documents. It compares the production `DesignerLayoutEngine` result with an equivalent tree of
real ModernFormsNext controls laid out by the runtime engine. The suite is headless and compares
structured values; it does not render or compare bitmap snapshots.

## Source of truth and execution paths

Runtime layout is the semantic source of truth. The runtime side creates the same controls and
properties that generated `InitializeComponent` code creates, attaches parents before descendants,
and invokes the public `Control.PerformLayout` path. This exercises `DefaultLayout`,
`FlowLayoutPanel`, `TableLayoutPanel`, `ScrollableControl.DisplayRectangle`, constraints, and real
control collection order. The harness does not contain a second Dock or Anchor algorithm.

The Designer side invokes `DesignerLayoutEngine.Layout` on the equivalent `DesignDocument`.
Property-edit scenarios commit text through `DesignerPropertyGridState`, root resize scenarios use
the same `DesignerSession` path as the property grid, and hit-test checks use
`DesignerHitTestService`. Representative pipeline tests also serialize and reopen `.mfdesign`,
compile and instantiate generated `.Designer.cs`, and reverse-parse generated code.

The internal test model records one `LayoutNodeSnapshot` per stable name path, for example
`ParityControl.card.content.button1`. A node contains:

- type and absolute logical `Bounds`;
- client and display rectangles for containers;
- ancestor-clipped visible bounds;
- the rectangle size available to children.

Integer geometry must match exactly. A failure identifies the scenario, node path, property,
runtime value, and Designer value. There is no general epsilon.

## Covered matrix

The fast matrix in `DesignerRuntimeLayoutParityTests` covers:

- ordinary children and all Dock edges, Fill, mixed/repeated edge sequences, and child
  reorder/remove/add behavior;
- zero, uniform, asymmetric, negative-normalized, nested, and UserControl-root Padding, including
  the issue #31 asymmetric Padding regression;
- default and asymmetric Margin where the runtime container uses Margin;
- all representative Anchor combinations, parent width/height/both resize, and repeated root
  resize without persisted drift;
- MinimumSize, MaximumSize, combined constraints, and constrained Dock.Fill;
- nested Panel and UserControl chains, a UserControl used as a child, and Form and UserControl
  roots;
- basic left-to-right, top-down, and wrapped FlowLayoutPanel behavior, plus the supported default
  auto-strip TableLayoutPanel subset;
- hidden and toggled controls, hidden Dock controls, clipping, and Z-order;
- Property Grid edits for Padding, Margin, Dock, Anchor, location, size, constraints, and visibility;
- logical Shape bounds with a deterministic zero-duration LayoutTransition;
- logical-to-device edge conversion at 100%, 125%, 150%, and 200% DPI;
- representative save/reopen, generated-code execution, reverse-parser, and final-geometry hit-test
  cases.

Form documents store the editable client surface size, so generated Forms use `ClientSize`.
UserControl roots continue to generate `Size`. This distinction avoids comparing Form window chrome
with Designer client geometry.

## Logical and presentation geometry

The suite compares the committed logical layout target exposed through runtime bounds. It does not
compare an interpolated presentation rectangle, rendered transforms, selection handles, adorners,
the dotted grid, or the Designer Form-title mockup. Animation coverage uses a zero-duration layout
transition so no clock, timer, active window, or `Thread.Sleep` can affect the result.

## Adding a scenario

1. Add a small `DesignDocument` factory to `DesignerRuntimeLayoutParityTests` and register it in
   `CoreParityScenarios`, or in one of the representative pipeline data sources.
2. Use stable, unique control names. Keep authored bounds and properties equivalent to generated
   runtime initialization.
3. Extend the harness control factory only when the production control type adds meaningful layout
   semantics. Do not implement that control's layout inside the harness.
4. Prefer exact structured assertions. If a representation cannot be normalized without hiding a
   real difference, document the boundary and keep a focused regression outside the equality
   matrix.
5. Run the focused parity filter, all Designer tests, and the full repository validation.

## Known exclusions and follow-up boundaries

- Oversized Padding can produce negative runtime `DisplayRectangle` dimensions, while
  `DesignBounds` intentionally cannot represent negative width or height. The focused
  `OversizedPaddingDoesNotProduceNegativeDesignerBounds` regression preserves the Designer safety
  contract; this case is not normalized into a false equality assertion.
- Full FlowLayoutPanel/TableLayoutPanel style matrices, AutoSize combinations, spanning edge
  cases, and right-to-left layout remain outside the fast foundation. In particular, complete RTL
  parity remains tracked by issue #89.
- Visual editing of inherited Forms and UserControls remains tracked by issue #39. The parity
  harness should add inherited scenarios when that production path exists; it does not emulate the
  missing feature.
- Project UserControls remain data-only atomic preview boundaries. Arbitrary application code is
  not loaded to obtain parity.
- Designer-wide undo/redo transactions remain issue #33. Current property-grid cases validate the
  resulting layout state; after #33 they should also be reused to verify undo and redo geometry.
- Monitor-derived DPI, interactive Visual Studio hosting, rendered pixels, accessibility, and
  platform-device observation are separate validation layers.

These exclusions are explicit extension points, not alternate layout implementations.
