# MessageBoxForm

Is a managed Form used for message-style content. Use SystemMessageBox for standard platform message dialog services when they meet the application's needs.

Type `ModernFormsNext.MessageBoxForm` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Form`](form.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new MessageBoxForm("Reports", "The report was exported.");
```

## Configuration example

```csharp
control.MinimumSize = new Size(240, 120);
```

## Interaction example

```csharp
control.Shown += (_, _) => System.Diagnostics.Debug.WriteLine("Report message shown");
control.Show();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Message` | `string` | Empty for the parameterless form | Message displayed in the form. |
| `Text` | `string` | Empty for the parameterless form | Window title. |

## Methods and behavior

Use inherited Show or ShowDialog based on the interaction. Use the owned lifetime and the dialog result for a modal workflow.

## Events

Observe inherited Form lifecycle events. Close the window through the existing Form path.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Create this owned or specialized window in code. Designer data for a Form document does not open runtime dialogs.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep the message short and ensure the user has a keyboard path to dismiss it.

## Related guides and API

- [Platform specific features](../platform-specific-features.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/MessageBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.MessageBoxForm>)
