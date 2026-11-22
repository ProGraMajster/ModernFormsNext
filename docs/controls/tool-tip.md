# ToolTip

Associates help text with controls and opens a managed tooltip when the hover policy allows or when requested. The component owns the active presentation.

Type `ModernFormsNext.ToolTip` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Component`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new ToolTip();
form.Disposed += (_, _) => control.Dispose();
var anchor = form.Controls.Add(new Button { Text = "Save" });
```

## Configuration example

```csharp
control.SetToolTip(anchor, "Write changes to disk.");
control.AutoPopDelay = 5000;
```

## Interaction example

```csharp
control.Popup += (_, args) => args.Cancel = !anchor.Enabled;
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Active` | `bool` | true | Enables associated tooltips. |
| `OwnerDraw` | `bool` | false | Routes custom painting through Draw when enabled. |
| `ShowAlways` | `bool` | false | Allows display for inactive targets when enabled. |

## Methods and behavior

SetToolTip(control, text) manages associated text. Show and Hide control explicit presentation. Dispose releases the component and its active popup.

## Events

Popup may cancel presentation. Draw allows owner painting. Use the tooltip guide for animation/fading endpoints and native presentation behavior.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Use supported component tray metadata when available. Configure runtime data, handlers and resources in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep essential instructions in the UI itself because a hover tooltip may be unavailable to keyboard or touch users.

## Presentation policy

Choose the initial delay, reshow delay and visible duration for the
interaction. `UseAnimation` and `UseFading` govern separate transitions.
`ShowAlways` controls whether passive display can continue on an inactive
owner. See the [presentation contract](../tooltips.md#presentation-and-lifetime)
for dismissal and owner-draw details.

```csharp
control.InitialDelay = 400;
control.ReshowDelay = 100;
control.UseAnimation = true;
control.UseFading = true;
```

## Related guides and API

- [Tooltips](../tooltips.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ToolTipPanel.cs)
- [API reference](<xref:ModernFormsNext.ToolTip>)
