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
as one component. Selection and hit testing stop at that boundary, and the preview uses a safe
placeholder instead of constructing the user type. Open the custom control's own `.cs`/`.mfdesign`
document to edit its internal children.

The designer rejects a control that contains itself. Before adding a project UserControl it also
reads project-local `.mfdesign` dependencies and rejects reachable transitive cycles, including
`ControlA -> ControlB -> ControlA` and longer chains. The document validator repeats the direct
self-reference check so manually edited invalid documents cannot generate designer code.

## Current design-time boundaries

- Automatic Toolbox discovery covers public, non-abstract UserControls declared in the active
  project's C# source tree. Nested and open generic controls, and controls supplied only by
  referenced binary assemblies, remain code-first.
- Custom UserControls use an atomic placeholder in a parent preview; their user constructors are not
  executed to render the parent.
- The designer has copy/paste and duplicate operations for child controls, but no general
  transaction-based undo/redo stack or multi-select support yet.
- The Visual Studio designer and interactive preview are currently Windows-first. `UserControl`
  itself remains in the shared, platform-neutral framework project.
