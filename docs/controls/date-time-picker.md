# DateTimePicker

Edits a date/time value using independently editable parts and a calendar popup or step controls. Optional checkbox state can represent an absent business value.

Type `ModernFormsNext.DateTimePicker` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new DateTimePicker());
```

## Configuration example

```csharp
control.MinDate = new DateTime(2020, 1, 1);
control.ShowCheckBox = true;
```

## Interaction example

```csharp
control.ValueChanged += (_, _) => form.Text = control.Value.ToShortDateString();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Value` | `DateTime` | Current date and time at construction | Selected value within the configured bounds. |
| `ShowCheckBox` | `bool` | false | Exposes an optional checked state. |
| `ShowUpDown` | `bool` | false | Uses stepping instead of the dropdown calendar when true. |

## Methods and behavior

Assign a valid range before restoring the selected value. Read Checked independently when your model permits an empty date.

## Events

ValueChanged observes changes to the stored value. DropDown and CloseUp delimit calendar popup interaction.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

The date and calendar peers expose platform-specific semantics. The popup needs a usable Form host on Android.

## Related guides and API

- [Accessibility/grids and calendars](../accessibility/grids-and-calendars.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/DateTimePickerPanel.cs)
- [API reference](<xref:ModernFormsNext.DateTimePicker>)
