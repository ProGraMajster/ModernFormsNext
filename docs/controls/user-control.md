# UserControl

Packages a reusable control tree with its own public configuration and behavior. Good custom controls expose task-specific properties while owning the details of layout.

Type `ModernFormsNext.UserControl` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`ScrollableControl`](scrollable-control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new UserControl());
```

## Configuration example

```csharp
var reload = control.Controls.Add(new Button { Text = "Reload" });
control.Dock = DockStyle.Top;
```

## Interaction example

```csharp
reload.Click += (_, _) => form.Text = "Reload requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AutoScroll` | `bool` | false | Inherited viewport scrolling. |
| `Style` | `ControlStyle` | Per-instance | Controls the surface appearance. |

## Methods and behavior

Add children during initialization and expose the properties or commands callers need. Dispose resources you own through the existing control lifetime.

## Events

Child events remain attached to the child. Surface a composed event for reusable action semantics.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Use the matching Designer item template as a design root. The standard VSIX previews its saved document and supported properties. Application behavior remains in your code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Preserve accessible names, tab order and validation on the actual interactive descendants.

## Related guides and API

- [User controls](../user-controls.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.UserControl>)
