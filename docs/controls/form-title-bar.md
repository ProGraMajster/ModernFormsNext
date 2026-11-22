# FormTitleBar

Displays the managed title bar of a Form, including the caption icon and supported window actions. Access it through the owning Form where possible.

Type `ModernFormsNext.FormTitleBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.TitleBar;
```

## Configuration example

```csharp
control.ShowImage = false;
form.UseSystemDecorations = false;
```

## Interaction example

```csharp
form.WindowStateChanged += (_, _) => System.Diagnostics.Debug.WriteLine(form.WindowState);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AllowMaximize` | `bool` | false on a standalone title bar | Enables the maximize action when allowed by the form. |
| `AllowMinimize` | `bool` | false on a standalone title bar | Enables the minimize action. |
| `ShowImage` | `bool` | true | Displays the configured image. |

## Methods and behavior

Use the owning Form to change window state and choose system decorations. Configure managed title appearance through TitleBar and its Style.

## Events

Observe the Form's window-state events. The title bar does not own the native window lifecycle.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Configure the containing Form. The internal title bar of that window is not an item in the control toolbox.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Test keyboard, system commands and mixed-DPI positioning when using custom chrome on Windows.

## Related guides and API

- [Development/windows custom chrome maximize](../high-dpi.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TitleBarPanel.cs)
- [API reference](<xref:ModernFormsNext.FormTitleBar>)
