# TabStrip

Displays tab headers independently of content. Use it when your application manages the active content region itself.

Type `ModernFormsNext.TabStrip` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TabStrip());
control.Tabs.Add(new TabStripItem("Output"));
```

## Configuration example

```csharp
control.Tabs.Add(new TabStripItem("Errors"));
control.SelectedIndex = 0;
```

## Interaction example

```csharp
control.SelectedTabChanged += (_, _) => form.Text = control.SelectedTab?.Text ?? "Output";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Tabs` | `TabStripItemCollection` | Empty | Header item collection. |
| `SelectedIndex` | `int` | -1 when empty | Selected header. |
| `ShowCloseButtons` | `bool` | false | Enables optional close affordances. |

## Methods and behavior

Add TabStripItem entries and update the associated content when SelectedTab changes. Apply close requests to the model you own.

## Events

SelectedTabChanged reports the active header. TabCloseButtonClicked requests a close; the application owns the corresponding content.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep labels distinguishable, including for an icon-heavy strip. TabControl uses an implicit strip when it owns content.

## Related guides and API

- [Shadows and typography](../shadows-and-typography.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TabStripPanel.cs)
- [API reference](<xref:ModernFormsNext.TabStrip>)
