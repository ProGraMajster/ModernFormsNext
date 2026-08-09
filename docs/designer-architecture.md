# ModernFormsNext Designer Architecture

ModernFormsNext designer support is split into small projects with separate responsibilities.
The goal is to keep the document model, code generation, reusable designer UI, standalone test
host, and Visual Studio integration independent from each other.

## Projects

### ModernFormsNext.Designing

`ModernFormsNext.Designing` owns the neutral designer document model. It contains the
`DesignDocument`, Form/UserControl root kind, control nodes, geometry types, property values, JSON serialization,
validation, metadata reader, selection service, and non-UI host services.

This project does not reference Visual Studio SDK and does not contain designer UI.

### ModernFormsNext.CodeGeneration

`ModernFormsNext.CodeGeneration` generates C# designer partial classes from
`DesignDocument`. It is the only place that should emit `.Designer.cs` code.

The same project also contains the conservative reverse-sync parser under the
`Reverse` namespace. That parser reads only the deterministic C# shape emitted by the
ModernFormsNext generator: literal control creation, supported property assignments,
event subscriptions, and `Controls.Add(...)` hierarchy statements. It does not parse or
execute arbitrary user C#.

Hosts such as the standalone playground and the Visual Studio extension must call this project
instead of implementing their own code generator or reverse parser.

### ModernFormsNext.Designer

`ModernFormsNext.Designer` owns reusable designer UI and designer interaction logic:

- toolbox
- document outline
- document tab
- designer surface
- property grid and events view
- output panel
- status bar
- command and file services
- designer layout, rendering, hit testing, drag, resize, clipping, and docking behavior

The public entry point is `ModernFormsDesignerShell`. Hosts create this shell, optionally pass
`ModernFormsDesignerOptions` and `IDesignerHostEnvironment`, and then load a `DesignDocument`.

Designer session paths are canonicalized around `.mfdesign` files. Opening `MainForm.cs`,
`MainForm.Designer.cs`, or `MainForm.mfdesign` maps to the same active designer document so a
host does not accidentally open parallel editors with divergent state.

### samples/ModernFormsNext.DesignerPlayground

`ModernFormsNext.DesignerPlayground` is only a standalone host/test application. It creates a
window, creates `ModernFormsDesignerShell`, loads the default document, and runs the app.

The playground must not contain its own copies of the property grid, designer surface, toolbox,
document outline, layout engine, hit testing, file service, or code-generation service.

### ModernFormsNext.VisualStudioExtension

`ModernFormsNext.VisualStudioExtension` is the future Visual Studio integration layer. The
user-facing entry point is the primary form/control code file, such as `MainForm.cs`, not the
metadata file. The extension detects designable ModernFormsNext C# files, exposes a safe
`View ModernFormsNext Designer` command, opens the companion `.mfdesign` document through a
designer editor pane, and generates a sibling `.Designer.cs` file.

The VSIX must not globally replace the C# editor for every `.cs` file. Only files that are
recognized as ModernFormsNext form/control files, have explicit ModernFormsNext project design
metadata, or have a valid companion `.mfdesign` file should expose the designer command.

The VSIX also contributes **ModernFormsNext Form** and **ModernFormsNext UserControl** C# item templates. Each item template creates
the user-authored `.cs` file, the generated `.Designer.cs` file, and the `.mfdesign` companion
document in one operation. It avoids `SubType=Form` for the same reason as the project template:
that value belongs to the classic Windows Forms designer.

Designable C# detection is intentionally conservative. A bare `class Form1 : Form`, a bare
`class MyControl : Control`, or `<SubType>Form</SubType>` is not enough because those shapes can
also represent classic Windows Forms files. The preferred project marker is:

```xml
<Compile Update="MainForm.cs">
  <ModernFormsNextDesigner>true</ModernFormsNextDesigner>
</Compile>
```

Do not use `SubType=Form` for ModernFormsNext forms. Visual Studio treats that value as the
classic Windows Forms designer marker and can try to load the built-in WinForms designer.
`ModernFormsNextDesigner=true`, `SubType=ModernFormsNextForm`, a valid `.mfdesign`, or a
ModernFormsNext project reference plus a ModernFormsNext base type/using is what makes the file
safe for the ModernFormsNext designer command.

## Flow

The intended Visual Studio file layout is the same for a Form or UserControl:

```text
MainForm.cs
    -> MainForm.Designer.cs
    -> MainForm.mfdesign
```

`MainForm.cs` is user-authored code and should contain the constructor and hand-written event
handlers. `MainForm.Designer.cs` is generated code. `MainForm.mfdesign` stores designer
metadata and state.

The intended workflow is:

```text
MainForm.cs
    -> View ModernFormsNext Designer
    -> MainForm.mfdesign
    -> DesignDocumentSerializer
    -> ModernFormsDesignerShell
    -> save .mfdesign
    -> ModernFormsNext.CodeGeneration
    -> MainForm.Designer.cs
```

Both the standalone playground and the Visual Studio extension must follow this same flow.

## Design Root Contract

The design root is represented by `DesignDocument`, not by a special `DesignControlNode`.
`DesignRootKind.Form` preserves the existing window preview and generated Form initialization;
`DesignRootKind.UserControl` presents a control surface without window chrome. Selection uses
`SelectedNode == null` for either root, so deletion, movement, child editing, Property Grid routing,
and generated `this.Controls.Add(...)` calls stay on the shared infrastructure.

The optional JSON `rootKind` property is written only for UserControl documents. Its absence means
Form, which keeps all existing `.mfdesign` files readable without migration. `formName` retains its
historical JSON name for compatibility and represents the runtime root `Name`; only Form generation
also uses it as fallback title text.

Project-owned UserControls are discovered by parsing public, top-level, non-abstract, non-generic
class declarations under the active project. Abstract and generic project bases still participate
in inheritance discovery, but cannot be placed themselves. The designer deliberately does not load
the project assembly or instantiate a custom UserControl. In a parent design, a custom UserControl
is an atomic component boundary for outline traversal, hit testing, selection, container detection,
generation, and persistence. Its own children are editable only in its own design document.

### Safe embedded UserControl preview

`DesignerEmbeddedPreviewCache` associates a discovered source type with a `.mfdesign` file. The
sibling source/document path is preferred, while project-wide fallback matching requires one exact
namespace-qualified document identity; folder layout and namespace layout do not need to match.
The loaded document must declare `DesignRootKind.UserControl`. Stale class/namespace identities,
duplicate matches, invalid JSON, missing files, and Form-root documents are rejected with a
diagnostic and use the existing placeholder.

The cache stores source JSON by canonical path, `LastWriteTimeUtc`, and length, then materializes a
private document/layout projection per requested instance size. A source change replaces all size
projections. The materialized tree may be normalized by the existing layout helpers, but it is not
the identity document, is never attached to the parent session, and is never passed to save or code
generation. Project source discovery itself is refreshed by reopening the designer; no separate
watcher is introduced.

`DesignerSurfaceRenderer` paints the embedded root and its children through the same node rendering,
property application, layout, clipping, DPI, and coordinate mapping paths used by an ordinary
document. The preview root gets the parent instance dimensions, while the saved root dimensions
remain the reference size for `Dock` and `Anchor` deltas. This is live layout projection, not a
scaled screenshot and not a second renderer. Runtime rendering resolves only types from the
already-loaded ModernFormsNext framework assembly; it does not use assembly-qualified type loading
for preview.

Embedded nodes are renderer-private. Parent hit testing still traverses only the parent's
`DesignDocument`, so a click anywhere in the projection resolves to the outer custom control. The
same boundary keeps inner nodes out of selection, resize/drag, Property Grid, Document Outline,
serialization, and parent `.Designer.cs` generation. Nested custom controls reuse the cache and
renderer recursively. A canonical-type stack converts only a repeated edge into the placeholder,
covering direct and arbitrary-length cycles without aborting the rest of the frame.

Known limitations are deliberate: custom runtime properties that require user code are not
evaluated, binary-only controls are not source-discovered, and visual fidelity is limited to
properties and controls already understood by the shared designer renderer. Safety and
deterministic `.mfdesign` behavior take precedence over exact runtime side effects.

Direct self-reference is rejected by model validation. Add/paste operations also inspect
project-local `.mfdesign` dependencies and reject reachable transitive cycles, including longer
dependency chains, before generated constructors can recurse. This is intentionally a small
project-document guard rather than a general runtime dependency graph.

## Child Order and Z-Order Contract

The ordered `children` array in `.mfdesign` is the canonical child-order representation. For
ordinary containers, index 0 is the front-most child and later entries are progressively farther
back. Flow, table, and tab containers instead retain their sequential content order; if their
children overlap, the last child is front-most, as at runtime. Ordinary Z-order is mapped
explicitly to the runtime `Control.Controls` contract, whose last index is front-most:
`BringToFront()` moves a child to the last index, `SendToBack()` moves it to index 0, and
`Controls.Add(...)` appends a new front-most child.
The existing ordered array is sufficient, so documents do not require a separate `childOrder` or
`zIndex` field and older `.mfdesign` files remain compatible without migration.

Each stage preserves that same order:

- save and reload retain the `children` array order;
- C# generation emits ordinary Z-ordered containers from back to front so runtime `Controls.Add`
  reconstructs the document Z-order; flow, table, and tab containers stay in authored sequence;
- reverse synchronization performs the inverse mapping for ordinary containers and preserves
  invocation order for flow, table, and tab containers;
- document-outline move-up moves a child toward index 0 and move-down moves it toward the back;
- designer preview docking consumes document children from front to back, while runtime docking
  consumes the mapped control collection from its last index to zero;
- painting composites back to front and hit testing searches front to back in both representations.

Docking uses only the relevant authored size for the dock edge. `Top` and `Bottom` use the saved
height, `Left` and `Right` use the saved width, and `Fill` consumes the remaining rectangle. The
saved X/Y position does not choose the docking order; the ordered child collection does. Flow and
table and tab containers retain their authored child sequence. The generator applies a
container-aware mapping, not a global reversal.

## DPI and Coordinate Contract

Designer documents store root and control geometry in logical pixels. The layout engine,
hit-testing service, drag/resize operations, and `DesignerCoordinateMapper` also operate only in
logical document or logical designer-surface coordinates. The scale used to fit a form preview in
the available surface is a preview zoom; it is not Windows monitor DPI and is never persisted.

The Windows input pipeline routes mouse positions through scaled control bounds as device pixels.
`DesignerDpiCoordinateConverter` converts those positions to logical surface coordinates once,
before preview zoom is removed by `DesignerCoordinateMapper`. In the opposite direction, logical
surface rectangles cross the DPI boundary once immediately before they are sent to the Skia canvas,
whose coordinates are device pixels. Selection handle rendering uses the same boundary, while its
hit target remains a constant logical size.

Designer shell controls, including Toolbox, Document Outline, Solution Explorer, Property Grid,
Output, document tabs, toolbar, and status bar, also define their layout metrics in logical pixels.
`DesignerLogicalPaintScope` scales each panel's device-pixel backing canvas once and then exposes
paint arguments with a scale of 1. This keeps backgrounds, clipping rectangles, rows, scroll
indicators, and text in the same logical coordinate space instead of scaling only the text. Mouse
positions are converted back to logical panel pixels once before row, toolbar, tab, or editor hit
testing.

The logical paint scope covers only custom designer chrome. ModernFormsNext child controls already
own device-pixel backing bitmaps, so panels restore the scope before `base.OnPaint` composes buttons,
text boxes, and editors. Extending the scope across that composition would apply monitor DPI twice
and separate the visible control from its pointer hit bounds. On Windows the managed-decoration
form also requests `NoChrome`; the backend consequently uses zero DWM client-frame margin so the
native compositor does not add a second one-pixel edge outside the framework-drawn border.

Runtime preview controls are detached from a platform window and therefore report a runtime
`ScaleFactor` of 1. They are laid out and painted entirely in logical 96-DPI units. A single canvas
transform then composes preview zoom and monitor DPI for the destination bitmap. Passing monitor DPI
into a detached control's `PaintEventArgs` would scale fonts and renderer metrics without scaling its
`ClientRectangle`, which creates the smaller-control-inside-a-larger-rectangle artifact.

Saving `.mfdesign` or generating `.Designer.cs` never uses device-pixel or preview-scaled values.
The generated root size is assigned through `Size`, matching the canonical WinForms-like designer
contract for both Form and UserControl. Reverse import also accepts `ClientSize` from earlier Form
files, but new code and shipped `.Designer.cs` templates consistently emit `Size`.

The 1.8.0 regression suite exercises the coordinate and rendering boundaries at 100%, 125%, 150%,
175%, and 200% scaling, including surface hit testing, drag/resize, grid and snapping math,
selection adorners, resize handles, designer panel chrome, and runtime preview composition.

Auto-save is enabled by default in the shared designer options. Hosts can disable it through
`ModernFormsDesignerOptions.AutoSaveEnabled`, but when it is enabled the active `.mfdesign`
document and generated `.Designer.cs` file stay synchronized as edits happen.

Reverse synchronization is prepared as an explicit, conservative operation:

```text
MainForm.Designer.cs
    -> CSharpDesignerParser
    -> diagnostics
    -> DesignDocument
    -> optional user-approved update of MainForm.mfdesign
```

For now, `.mfdesign` remains the designer state source of truth, but `MainForm.cs` remains the
user-facing project item. A host should use reverse sync only when a generated `.Designer.cs`
file appears newer or manually edited, and it should report parser diagnostics before
overwriting the `.mfdesign` document.

## Visual Studio TODO

The Visual Studio extension hosts the shared shell through a small HWND adapter. The adapter
creates a lightweight ModernFormsNext form, places `ModernFormsDesignerShell` inside it, and
parents that HWND into the Visual Studio editor pane. This keeps the designer UI in
`ModernFormsNext.Designer` instead of copying property grid, surface, toolbox, or outline code
into the VSIX.

The Visual Studio extension still needs:

- hardening of the HWND adapter into a supported public hosting API in the framework or a
  dedicated embeddable WindowKit surface, so the VSIX no longer needs reflection to obtain the
  runtime HWND
- optional integration with Visual Studio's built-in `View Designer` and Shift+F7 routing for
  designable ModernFormsNext C# files
- optional Solution Explorer automation for existing projects that do not yet have dependent
  file metadata for `MainForm.Designer.cs` and `MainForm.mfdesign` under `MainForm.cs`

`ModernFormsNext.VisualStudioExtension` must remain a thin host and must not duplicate designer
UI or code generation.

## Repository Boundary

The designer projects currently stay in the main ModernFormsNext repository. This keeps runtime
API changes, designer metadata attributes, document serialization, code generation, templates,
and the VSIX integration versioned together while the designer is still evolving quickly.

A separate repository may make sense later if the designer becomes independently versioned,
supports multiple framework versions at once, or needs a different release cadence. Until then,
keeping it here reduces cross-repository churn and makes package compatibility easier to audit.
