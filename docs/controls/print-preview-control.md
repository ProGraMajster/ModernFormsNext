# PrintPreviewControl

Displays pages rendered by a PrintDocument. Use it to embed preview in an application-specific layout.

Type `ModernFormsNext.PrintPreviewControl` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new PrintPreviewControl());
```

## Configuration example

```csharp
control.AutoZoom = true;
control.Dock = DockStyle.Fill;
```

## Interaction example

```csharp
control.StartPageChanged += (_, _) => form.Text = $"Preview page {control.StartPage + 1}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Document` | `PrintDocument` | null | PrintDocument used to render preview. |
| `AutoZoom` | `bool` | true | Fits preview automatically. |
| `StartPage` | `int` | 0 | First page displayed. |

## Methods and behavior

Set Document and call InvalidatePreview() when print content changes. Keep PrintPage generation repeatable because preview can render it again.

## Events

StartPageChanged observes movement through available pages. Printing owns query and print-page events on PrintDocument.

## Platform support

Windows is the primary printing path. The availability of native print services depends on the backend and printer configuration. Android printer and preview parity is unqualified.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Offer accessible page navigation and test printing with the intended backend and printer configuration.

## Related guides and API

- [Platform specific features](../platform-specific-features.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PrintingPanel.cs)
- [API reference](<xref:ModernFormsNext.PrintPreviewControl>)
