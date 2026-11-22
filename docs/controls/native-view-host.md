# NativeViewHost

Hosts a platform-created HWND or Android View peer where the shared surface needs native integration. The platform capability contract determines what the overlay can do.

Type `ModernFormsNext.NativeViewHost` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new NativeViewHost());
```

## Configuration example

```csharp
control.Dock = DockStyle.Fill;
control.AccessibleName = "Platform preview";
```

## Interaction example

```csharp
control.Layout += (_, _) => System.Diagnostics.Debug.WriteLine(control.HostingDiagnostics);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `PeerFactory` | `INativeViewFactory` | null | Creates or supplies the peer when a site is available. |
| `HostingCapabilities` | `NativeViewCapabilities` | Backend-specific | Reports compositing limits. |
| `HostingDiagnostics` | `NativeViewHostDiagnostics` | Current host state | Describes attach, clipping and ordering status. |

## Methods and behavior

Configure PeerFactory in the platform project and follow the peer ownership contract. Let detachment clean up the child surface and native visibility.

## Events

Shared Layout observes managed bounds. Inspect hosting diagnostics after attaching and moving a native peer.

## Platform support

Windows supplies HWND peers. The experimental Android backend supplies View peers. Check the backend's supported clipping, focus and airspace capabilities before attaching; keep peer factories and platform dependencies in the platform project.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Native peers have separate platform access, input and accessibility. Test layering and focus with overlapping managed content.

## Related guides and API

- [Native view hosting](../native-view-hosting.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/NativeViewHostingPanel.cs)
- [API reference](<xref:ModernFormsNext.NativeViewHost>)
