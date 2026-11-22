# TreeView

Displays hierarchical TreeViewItem models. Populate children directly or create a branch as the user expands it.

Type `ModernFormsNext.TreeView` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TreeView());
var root = new TreeViewItem { Text = "Projects" };
control.Items.Add(root);
```

## Configuration example

```csharp
root.Items.Add(new TreeViewItem { Text = "Client" });
```

## Interaction example

```csharp
control.ItemSelected += (_, _) => form.Text = control.SelectedItem?.Text ?? "Projects";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `VirtualMode` | `bool` | false | Enables lazy expansion behavior. |
| `SelectedItem` | `TreeViewItem` | null | Current node selection. |
| `ShowDropdownGlyph` | `bool` | true | Shows branch expansion glyphs. |

## Methods and behavior

GetItemAtLocation(point) uses displayed coordinates. Populate virtual branches in BeforeExpand and avoid duplicating existing children.

## Events

BeforeExpand precedes expansion; ItemSelected follows selection. DrawNode supports a custom draw mode.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Expose meaningful node text, particularly for branches whose icons convey status. Tree virtual mode has its own contract.

## Populate lazy branches

In virtual mode, add children to the existing node on expansion. Use stable
node identity and a guard against adding the same children on a later
expansion. TreeView's lazy branch contract differs from virtualized flat
collection recycling.

The gallery shows basic, lazy and custom-drawn trees. Each node owns its
child model collection. Use selection rather than hit testing when an
operation targets the currently selected node.

## Related guides and API

- [Accessibility/semantic model](../accessibility/semantic-model.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TreeViewPanel.cs)
- [API reference](<xref:ModernFormsNext.TreeView>)
