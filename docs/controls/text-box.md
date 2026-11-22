# TextBox

Provides plain text editing, selection and clipboard interaction. Use the same control for single-line fields and multiline input.

Type `ModernFormsNext.TextBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`ScrollControl`](scroll-control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TextBox { Placeholder = "Project name" });
```

## Configuration example

```csharp
control.MaxLength = 80;
control.AccessibleName = "Project name";
```

## Interaction example

```csharp
control.TextChanged += (_, _) => form.Text = control.Text;
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `MultiLine` | `bool` | false | Enables multiline input. |
| `ReadOnly` | `bool` | false | Prevents user edits when enabled. |
| `Placeholder` | `string` | Empty | Text rendered when the input is empty. |

## Methods and behavior

SelectAll() selects the content. Copy(), Cut() and Paste() use platform clipboard support. ScrollToCaret() reveals the active text position.

## Events

TextChanged observes text updates. TextCompositionChanged reports the current composition session separately from committed text.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Give the field a stable name. Composition and native candidate behavior follow the Windows IMM32 or Android InputConnection path.

## Validate committed input

Handle `Validating` for a form field that must contain a committed value.
This validation can cancel a participating focus transition. Keep live
composition changes separate from accepted text.

```csharp
control.Validating += (_, args) =>
{
    args.Cancel = string.IsNullOrWhiteSpace(control.Text);
    if (args.Cancel)
        form.Text = "Project name is required";
};
```

## Related guides and API

- [Text input](../text-input.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TextBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.TextBox>)
