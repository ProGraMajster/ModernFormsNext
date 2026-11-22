# TabControl

Hosts TabPage content and manages selection, close actions and pinned tabs. Use it for documents, editors and settings panels.

Type `ModernFormsNext.TabControl` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TabControl());
var page = control.AddTab("Overview");
```

## Configuration example

```csharp
page.Controls.Add(new Label { Text = "Overview content" });
control.ShowCloseButtons = true;
```

## Interaction example

```csharp
control.TabClosing += (_, args) => { if (form.Text == "Unsaved") args.Cancel = true; };
control.AddClosableTab("Draft");
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `ShowCloseButtons` | `bool` | false | Shows close buttons on eligible tabs when enabled. |
| `SelectedIndex` | `int` | -1 when empty | Selected tab index. |
| `TabPages` | `TabPageCollection` | Empty | Collection of tab pages. |

## Methods and behavior

AddTab creates a standard tab, AddClosableTab a removable one, AddPinnedTab a pinned one. CloseTab(page) reports whether an eligible close succeeded.

## Events

TabClosing can cancel a close. TabClosed observes completion. SelectedIndexChanged tracks selection outside the Close action too.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Give tabs distinct names and provide a non-pointer path to important close or navigation actions.

## Pinned and editable tabs

Keep pinned tabs for fixed destinations. Choose the parent control's close
path for transient pages so the normal cancellable notification can run.

```csharp
control.AddPinnedTab("Overview", select: false);
var draft = control.AddClosableTab("New draft");
draft.Controls.Add(new TextBox { MultiLine = true, Dock = DockStyle.Fill });
control.TabClosed += (_, _) => form.Text = "Draft tab closed";
var closeDraft = form.Controls.Add(new Button { Text = "Close draft" });
closeDraft.Click += (_, _) => control.CloseTab(draft);
```

## Related guides and API

- [Shadows and typography](../shadows-and-typography.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TabControlPanel.cs)
- [API reference](<xref:ModernFormsNext.TabControl>)
