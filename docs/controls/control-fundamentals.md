# Control fundamentals

ModernFormsNext builds its UI in C#. A Form owns a window and exposes a
control collection. Each Control can own children and resolve context such as
font, resources, validation and visibility through its parent chain.

## Example setup

The guide fragments are successive statements in a method that already has a
`Form form` on its UI thread. Add these imports as needed. For a complete
program and its startup, see [getting started](../getting-started.md).

```csharp
using System;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Drawing;
using SkiaSharp;
```

Attach a visual control with `form.Controls.Add(new Button { Text = "Save" })`.
The collection returns the added control, so the caller can configure it and
subscribe to events. Use `ContentPage.Content`, `PageHost.Page`, or the page
collections where a host defines a more specific ownership contract.

## Lifecycle and ownership

Create, attach, update and dispose controls on the UI thread. A parent
disposes controls it still owns when it is disposed. Removing a child and
disposing a child are separate operations. Check `Parent` before adding a
control to a second tree and use the navigation host's APIs for moving pages.
`ParentChanged`, `ControlAdded` and `ControlRemoved` report actual tree changes.

Keep non-visual components, bitmaps, timers, subscriptions and native peers
alive for as long as the control uses them. Use a form-owned component
container or dispose explicitly from the owning lifetime. A local `using`
is appropriate for a synchronous operation or an awaited modal operation
that finishes within that scope. For an event-driven component, the method
that wires it often returns before the component is used.

## Layout and scaling

- `Dock` reserves an edge or fills the remaining region. Put a filling surface
  and its edge siblings in the same host so the layout engine can allocate
  their space together.
- `Anchor` relates the child to selected parent edges when undocked. Use
  `Margin` for spacing managed by the parent layout and `Padding` for spacing
  inside the control.
- Enable `AutoSize` where the control supports a content-driven size. Respect
  `MinimumSize` and `MaximumSize`. To measure, call
  `GetPreferredSize(proposedSize)` with the intended constraints and allow its
  owning layout to assign final bounds.
- Use `SuspendLayout()` and `ResumeLayout()` around a batch of structural
  changes. `PerformLayout()` requests layout. `Invalidate()` schedules
  rendering of changed visual content.
- Specify geometry in logical units unless the relevant API asks for device
  pixels. Use [high DPI](../high-dpi.md) and
  [right-to-left layout](../right-to-left-layout.md) when interacting with native
  coordinates or logical cell order.

## Input, focus, validation and events

Focus and keyboard traversal follow the active host, effective visibility,
enabled state and tab configuration. Give interactive controls meaningful
`AccessibleName` values when visible text is insufficient. Use `TabIndex` and
`TabStop`, inspect `CanSelect` before requesting focus through `Select()`, and
test with the window actually active.

Pointer events include mouse down, move, up, wheel and click. A drag can use
`Capture` to follow the pointer outside the bounds. Release capture at the end
of the interaction and account for capture loss. Use the control's existing
scrolling and gesture paths for touch. The [Android support
matrix](../platforms/android.md) records the target-specific coverage.

Events usually run synchronously on the UI thread and may be raised by
programmatic changes. Use the control's typed event to distinguish selection,
commit and request. Only handlers with a supported cancellable event can
veto an operation. `Validating` can cancel validation where the focus transfer
requires it. `Validated` follows accepted validation, and `CausesValidation`
controls whether a participating target requests it. See the
[focus and validation audit](../development/issue-161-validation-audit.md)
for the qualified Windows paths.

For action routing and keyboard shortcuts use [commands](../commands.md).
For editor input, composition and commit use [text input](../text-input.md).
For roles, logical children, actions and range metadata see the
[accessibility model](../accessibility/semantic-model.md).

## Styling, resources and culture

Use [control styles](../styling.md), [themes](../themes.md) and
[brushes and gradients](../paint-and-gradients.md) for base and state-specific
appearance. [Typography metrics and shadow options](../shadows-and-typography.md)
describe inherited font/line spacing and compositing. Use
[dynamic resources](../dynamic-resources.md) to resolve shared paint or font
from the control or app scope. Keep layout and style animation within the
documented [animation](../animations.md) and
[composition](../composable-animations.md) contracts.

Localize labels, window titles and accessibility text in the app. Use a
control's format or culture option where it exposes one, and choose a
culture-aware parser for user input. Review the
[localization architecture](../architecture/decisions/ADR-Localization-System.md)
and [RTL layout](../right-to-left-layout.md) for text and mirrored layout.

## Binding, threading and resources

Use [data binding](../data-binding.md) for typed model properties and
BindingSource currency. Use [item virtualization](../virtualization.md) for
large templated lists, where visual children can be recycled and must reset
per-item state. Keep data notifications that touch controls on the UI thread.
Use the UI dispatcher to deliver results from background work, and check that
the target is still attached and alive before updating it. Unsubscribe from
longer-lived sources when disposing a view.

## Designer integration

The standard VSIX reads design data and metadata. Use serialized design
properties for stable appearance and layout. Configure runtime data,
services, delegates and native peer factories in code. Unavailable project
controls can remain represented by editable placeholders. See
[Designer host and compatibility](../visual-studio-designer-host.md) and
[preview resources](../designer-preview-resources.md).
