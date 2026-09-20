# UserControls

`ModernFormsNext.UserControl` is a reusable, platform-neutral container control. It uses the same
custom rendering, layout, input, styling, and `Control.Controls` infrastructure as the rest of
ModernFormsNext; it does not use Windows Forms controls or `System.Windows.Forms.Design`.

## Create a designed UserControl

The Visual Studio extension contributes a **ModernFormsNext UserControl** item template. It creates
three sibling files:

```text
NavigationPanel.cs           # user-authored code
NavigationPanel.Designer.cs  # generated initialization
NavigationPanel.mfdesign     # designer source of truth
```

The user-authored file owns the constructor and application logic:

```csharp
using ModernFormsNext;

namespace MyApp;

public partial class NavigationPanel : UserControl
{
    public NavigationPanel()
    {
        InitializeComponent();
    }
}
```

Open `NavigationPanel.cs` with **View ModernFormsNext Designer**. Saving writes the `.mfdesign`
document and regenerates only `NavigationPanel.Designer.cs`; normal designer saves never rewrite
`NavigationPanel.cs`.

## Designer root model

Form and UserControl documents use one `DesignDocument`, `DesignerSession`, surface, selection
service, layout engine, Property Grid, serializer, and C# generator. The optional `.mfdesign`
`rootKind` field chooses only root-specific behavior:

```json
{
  "className": "NavigationPanel",
  "rootKind": "userControl",
  "formName": "NavigationPanel",
  "size": { "width": 480, "height": 320 },
  "controls": []
}
```

`rootKind` is omitted for Form documents. Documents created before UserControl support therefore
continue to deserialize as Form without migration. The historical `formName` JSON property is kept
for compatibility; for a UserControl it supplies the generated `Control.Name`, not a window title.

The UserControl root is selectable and resizable from its right and bottom edges. It is a container
for authored child controls, but it cannot be moved, copied, pasted, or deleted as an ordinary child.
Root properties such as `Text`, `Dock`, `Anchor`, `Padding`, `Margin`, `MinimumSize`, `MaximumSize`,
`AutoScroll`, and `AutoSize` are stored in the same root property dictionary used by Form documents.

## Use a custom UserControl in another design

The Toolbox scans public, top-level, non-abstract, non-generic C# classes in the active project and
follows direct or project-local inheritance from `UserControl`, including concrete classes derived
from abstract or generic project bases. Discovered controls appear under **My Project** and are
emitted using their namespace-qualified type name. Discovery parses source syntax; it does not load
the project assembly and does not run constructors, module initialization, timers, file I/O,
network calls, or other application logic.

When a custom UserControl is placed on a Form or another UserControl, the parent designer treats it
as one component. Selection and hit testing stop at that boundary. The surface resolves the
project type to its own `.mfdesign` document and renders a read-only visual projection of that
document with the normal designer renderer. It never constructs the custom type, loads the user
assembly for preview, or runs its constructor, `InitializeComponent`, event handlers, timers, or
other application code. Open the custom control's own `.cs`/`.mfdesign` document to edit its
internal children.

The preview root receives the size of the instance on the parent. Its child layout is recalculated
from data, so `Dock` and `Anchor` respond to resizing rather than scaling a bitmap. Rendering uses
the existing property/style path for framework controls. Properties that would require executing a
custom runtime implementation are deliberately not evaluated.

Preview nodes remain private to the projection. They are never inserted into the parent's
`DesignDocument`, Document Outline, selection model, hit-test tree, Property Grid, generated
`.Designer.cs`, or save operation. Clicking a visible child therefore selects and moves only the
outer custom UserControl instance, and saving the parent cannot rewrite the child's `.mfdesign`.

Nested project UserControls are projected recursively. A per-render type stack detects direct and
transitive cycles such as `A -> A`, `A -> B -> A`, and `A -> B -> C -> A`; only the recursive edge
falls back to the existing placeholder, so the rest of the parent still renders. A missing, empty,
invalid, stale, ambiguous, non-UserControl, or otherwise unreadable `.mfdesign` document also uses
that placeholder and writes a diagnostic instead of failing the designer.

The renderer keeps a small cache of parsed source text and per-instance-size layout projections.
The key includes the canonical document path, file timestamp/length, discovered type identity, and
requested size. A changed `.mfdesign` is read again on the next render. Adding, removing, or
renaming a project type is picked up with **Toolbox > Refresh**; this also clears the projection
cache. There is intentionally no preview-only file watcher.

The designer rejects a control that contains itself. Before adding a project UserControl it also
reads project-local `.mfdesign` dependencies and rejects reachable transitive cycles, including
`ControlA -> ControlB -> ControlA` and longer chains. The document validator repeats the direct
self-reference check so manually edited invalid documents cannot generate designer code.

## Current design-time boundaries

- Toolbox discovery covers public, concrete, top-level `Control`/`UserControl` subclasses from
  source and supported binary references. A public parameterless constructor must exist for
  generated initialization, but discovery never calls it. Closed subclasses of generic bases
  are supported; nested type declarations and open generic controls remain code-first.
- Custom UserControls use a data-only `.mfdesign` projection in a parent preview and remain atomic;
  their user constructors and project assemblies are not executed or loaded to render the parent.
- Preview fidelity is limited to data and framework properties understood by the existing designer
  renderer. Runtime-only custom property behavior and user code side effects are intentionally
  absent. Binary controls without a matching data document use the safe placeholder when a source
  document cannot be identified unambiguously.
- Property/event edits use the existing document transactions and Undo/Redo. Refresh does not
  change saved values, selection, dirty state or history, including values whose metadata vanished.
- The Visual Studio designer and interactive preview are currently Windows-first. `UserControl`
  itself remains in the shared, platform-neutral framework project.

Source `.mfdesign` changes are re-read on the next render through the preview cache key. After a
rebuild, restore or reference change, click **Refresh** in Toolbox. Hosts can call
`DesignerSession.RefreshToolbox()` on the UI thread. Discovery is synchronous file inspection;
it does not build the project or evaluate MSBuild targets. See [Known limitations](known-limitations.md).

## Safe custom properties, events and references

Discovery reads Roslyn source/PE symbols into the existing Designer catalog, then adapts detached
metadata to the same property editors and `DesignPropertyValue` document values. It never loads
user assemblies or invokes startup, static initialization, constructors, getters, setters,
attribute constructors, converters or delegates. Reflection-based `DesignMetadataReader` remains
the trusted-framework path; do not use it to inspect untrusted application types in the Designer.

```csharp
[DesignableControl("Counter", Category = "My Widgets")]
public class Counter : UserControl
{
    [Category("Data"), DefaultValue(10)]
    public int Limit { get; set; } = 10;

    public event EventHandler? LimitReached;
}
```

Add `using ModernFormsNext;`, `using ModernFormsNext.Designing;` and
`using System.ComponentModel;` to this example. `DefaultValue` describes a default; it does not
initialize the runtime property. Discovery displays encoded defaults only, never constructor or
getter results. Derived properties/events and closed generic base members are inspected without
instantiation. ModernFormsNext design attributes take precedence over standard display/category,
description, browsable, read-only and serialization metadata.

The custom-member editor supports strings, booleans, Int32, finite Double, declared enum members
and nullable value types in that set. The `(Type, string)` DefaultValue overload uses these same
allow-listed conversions. Arbitrary TypeConverter code is ignored with an Output diagnostic.
Single, decimal, object graphs and executable-only values remain read-only: the existing primitive
document format cannot preserve every CLR numeric type through generation/reverse sync. Framework
structured editors and layout properties retain their existing behavior. Custom metadata does not
replace canonical layout fields or the framework's built-in property editors.

Public `void` events with ordinary value parameters receive typed handler stubs using symbol type
names. Non-void, ref/out, pointer and unresolved signatures are not bindable in the grid; bind them
in source. Existing event bindings survive refresh. Custom code still runs normally when the
generated application is deliberately started, outside discovery and safe preview.

References are resolved from literal `Reference/HintPath`, conventional built `ProjectReference`
outputs, and restored package compile assets in `obj/project.assets.json`. Debug is the default
configuration unless an unconditional Configuration is specified. A multi-target project uses its
first target, or a referenced target matching the consumer, with a diagnostic. Conditional/imported
project evaluation, custom build targets, linked-source inclusion rules and arbitrary property
functions are not evaluated. Supply an explicit built DLL HintPath for unsupported project layouts.
Missing/invalid DLLs, ambiguous types, unresolved dependencies and stale project outputs/assets
produce actionable Output messages. A refresh copies PE bytes and releases file handles, so rebuilding
or removing a DLL does not require restarting the Designer. No previous binary snapshot is reused.

## Manual Visual Studio smoke test

1. Add a **ModernFormsNext UserControl** named `MyUserControl1` and place, in order, a `Label`,
   `TextBox`, `Label`, `TextBox`, and `Button`. Give at least one child `Dock` or right/bottom
   `Anchor` behavior, then save the control.
2. Add a **ModernFormsNext Form** named `Form2`, open its designer, and drag `MyUserControl1` from
   **Toolbox > My Project** onto the form.
3. Confirm that the five inner controls are visible. Resize `MyUserControl1` and confirm that their
   dock/anchor layout is recalculated rather than bitmap-scaled.
4. Click each visible Label, TextBox, and Button. Every click must select only
   `MyUserControl1`; its resize handles and properties must remain the active ones.
5. Confirm that Document Outline for `Form2` contains `MyUserControl1` but none of its preview
   children, and that Property Grid shows only the outer instance.
6. Save and reopen both designers. Confirm that the preview returns and that `Form2.mfdesign` and
   `Form2.Designer.cs` contain only the `MyUserControl1` instance, while the five children remain
   exclusively in `MyUserControl1.mfdesign`/`MyUserControl1.Designer.cs`.
7. Temporarily make `MyUserControl1.mfdesign` unavailable or invalid. Confirm that `Form2` stays
   usable, shows the placeholder, and reports a preview fallback diagnostic; restore the file and
   click **Toolbox > Refresh**.
