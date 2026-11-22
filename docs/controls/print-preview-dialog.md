# PrintPreviewDialog

Hosts PrintPreviewControl in a dedicated managed form. Use the embedded control property to configure page presentation.

Type `ModernFormsNext.PrintPreviewDialog` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Form`](form.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new PrintPreviewDialog();
```

## Configuration example

```csharp
control.UseAntiAlias = true;
control.PrintPreviewControl.AutoZoom = true;
```

## Interaction example

```csharp
var preview = form.Controls.Add(new Button { Text = "Preview" });
preview.Click += (_, _) => { if (control.Document is not null) control.Show(); };
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Document` | `PrintDocument` | null | Document to preview. |
| `PrintPreviewControl` | `PrintPreviewControl` | Owned control | Configurable page preview surface. |
| `UseAntiAlias` | `bool` | See preview control | Controls preview antialiasing. |

## Methods and behavior

Configure Document before opening. Dispose the dialog and any independently owned PrintDocument when finished.

## Events

The dialog uses inherited Form events; its preview control exposes StartPageChanged.

## Platform support

Windows is the primary printing path. The availability of native print services depends on the backend and printer configuration. Android printer and preview parity is unqualified.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Create this owned or specialized window in code. Designer data for a Form document does not open runtime dialogs.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep window navigation and page controls keyboard-accessible. Preview and print services require target printer qualification.

## Related guides and API

- [Platform specific features](../platform-specific-features.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PrintingPanel.cs)
- [API reference](<xref:ModernFormsNext.PrintPreviewDialog>)
