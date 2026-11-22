# PopupWindow

Creates an owned popup attached to a Form. Use it for custom transient content that needs the framework window and input contracts.

Type `ModernFormsNext.PopupWindow` in the `ModernFormsNext` assembly and NuGet package.
Inherits `WindowBase`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new PopupWindow(form);
var anchor = form.Controls.Add(new Button { Text = "Open popup" });
```

## Configuration example

```csharp
control.Size = new Size(160, 80);
```

## Interaction example

```csharp
anchor.Click += (_, _) => control.Show(anchor, 0, anchor.Height);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Size` | `Size` | 100 by 100 logical units | Popup extent. |
| `Style` | `ControlStyle` | Instance style | Managed popup appearance. |

## Methods and behavior

Show(Control, int, int) positions relative to a control. Show(Point) takes screen coordinates. Close when the content is no longer needed.

## Events

Use inherited Shown and visibility events for lifecycle. Keep the popup owner live for the duration of the interaction.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Create this owned or specialized window in code. Designer data for a Form document does not open runtime dialogs.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Test focus return and available bounds on the actual window host. Android popup support depends on the bounded Form host.

## Related guides and API

- [Android windowing](../android-windowing.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.PopupWindow>)
