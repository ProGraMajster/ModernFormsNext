# Form

Owns a top-level window, child controls, lifecycle events and dialog state. Windows is the primary host. The Android host is bounded by the Activity model.

Type `ModernFormsNext.Form` in the `ModernFormsNext` assembly and NuGet package.
Inherits `WindowBase`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new Form { Text = "Reports", ClientSize = new Size(420, 240) };
```

## Configuration example

```csharp
control.Controls.Add(new Label { Text = "Latest reports" });
```

## Interaction example

```csharp
control.Closing += (_, _) => System.Diagnostics.Debug.WriteLine("Closing reports");
control.Show();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `UseSystemDecorations` | `bool` | false | Chooses native or managed decoration. |
| `WindowState` | `FormWindowState` | Normal | Current window state. |
| `DialogResult` | `DialogResult` | None | Result for a modal form. |

## Methods and behavior

Show() displays a modeless form. ShowDialog(owner) returns an asynchronous modal result. Close() requests the normal closing lifecycle.

## Events

Load, Shown, Closing and WindowStateChanged report stages on the UI thread. Keep closing handlers short and dispose resources the form owns.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Use the matching Designer item template as a design root. The standard VSIX previews its saved document and supported properties. Application behavior remains in your code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Check native activation, role and focus across runtime boundaries. Android has no independent desktop-style multi-window support.

## Related guides and API

- [Android windowing](../android-windowing.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.Form>)
