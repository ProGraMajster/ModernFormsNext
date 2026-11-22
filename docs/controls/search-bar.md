# SearchBar

Combines search text, submit and clear buttons, optional suggestions, a suggestion provider and bounded local history.

Type `ModernFormsNext.SearchBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new SearchBar { Placeholder = "Find a customer" });
```

## Configuration example

```csharp
control.SuggestionsSource = new[] { "Acme", "Northwind" };
control.HistoryCapacity = 8;
```

## Interaction example

```csharp
control.SearchRequested += (_, args) => { if (string.IsNullOrWhiteSpace(control.Text)) args.Cancel = true; else form.Text = control.Text; };
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Text` | `string` | Empty | Current editor text. |
| `IsHistoryEnabled` | `bool` | true | Collects accepted submissions unless disabled. |
| `SuggestionProvider` | `Func<string, IList>` | null | Optional asynchronous source of suggestions. |

## Methods and behavior

SubmitSearch() goes through the normal request and command path. ClearText() resets the editor and RefreshSuggestions() requests updated suggestions.

## Events

SearchRequested and SuggestionChosen allow cancellation. SuggestionsOpened and SuggestionsClosed observe the popup lifetime.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Name the editor for assistive users and check keyboard navigation through suggestions on the chosen host. Set IsHistoryEnabled to false for sensitive queries. The default history is in memory.

## Related guides and API

- [Search and time](../search-and-time.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/SearchTimePanel.cs)
- [API reference](<xref:ModernFormsNext.SearchBar>)
