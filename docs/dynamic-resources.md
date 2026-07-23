# Dynamic resources

Dynamic resources let a normal public control property follow a value stored at theme,
application, window, ancestor-control, or control scope. They are the shared foundation for
themes, localization, styles, and control defaults; they are not a second data-binding or property
system.

## Define and reference a resource

```csharp
Application.Resources["Button.Primary.Background"] = SKColors.DodgerBlue;

var button = new Button { Text = "Save" };
button.SetResourceReference(
    nameof(Control.BackColor),
    "Button.Primary.Background");
```

Changing the resource invokes the existing CLR property setter. A visual property therefore uses
the control's normal invalidation behavior, while a layout property uses its normal layout behavior.
The resource system does not invalidate the whole application.

```csharp
Application.Resources["Button.Primary.Background"] = SKColors.MediumSeaGreen;
```

Resources may contain any assignable type, not just colors. Brushes, fonts, `Thickness`, styles,
animation configuration, and localized strings can use the same mechanism.

### Observable brushes

Brushes are observable resource values. Once a brush is assigned to a standard brush-valued control
property, changing its color, opacity, transform, gradient geometry, stop collection, or an
individual stop invalidates only controls that still consume it. Replacing the dictionary entry is
not required:

```csharp
var brush = new LinearGradientBrush();
brush.GradientStops.AddRange([
    new GradientStop(System.Drawing.Color.MidnightBlue, 0f),
    new GradientStop(System.Drawing.Color.CornflowerBlue, 1f)
]);

Application.Resources["Card.Background"] = brush;
card.SetResourceReference(nameof(Control.BackgroundBrush), "Card.Background");

brush.GradientStops[0].PaintColor = System.Drawing.Color.Teal;
```

Control subscriptions are weak and reference-counted. Replacing a brush, applying a fallback,
clearing a reference, or disposing a control detaches the obsolete subscription. Other arbitrary
mutable resource objects do not become observable automatically; they still need replacement or a
future type-specific change contract.

## Lookup order and fallback

For a control, lookup proceeds from the most specific scope to the broadest:

1. the control's `Resources`;
2. each ancestor control's `Resources`;
3. the owning window's `Resources`;
4. `Application.Resources`;
5. the read-only, ThemeManager-owned `Application.ThemeResources`.

If an override is removed, the next matching scope becomes effective automatically. If no scope
contains the key, the property returns to the value captured when `SetResourceReference` was first
called. Application resources deliberately override theme defaults, so applying a theme cannot
erase application-owned entries. Reparenting a control refreshes references for that subtree.

```csharp
Application.Resources["Spacing.Page"] = 16;
form.Resources["Spacing.Page"] = 20;
panel.Resources["Spacing.Page"] = 24;

panel.SetResourceReference(nameof(Control.Padding), "Spacing.Page");
```

The example above is illustrative: the resource value must be the exact type accepted by the
target property (`Padding` for `Control.Padding`), not an `int` that happens to look convertible.

## Errors and ownership

An incompatible value found while a reference is created throws `InvalidOperationException` and
does not leave an active reference. If a later runtime replacement has the wrong type, the control
restores its captured fallback and raises `ResourceReferenceFailed`. Property-setter exceptions are
reported through the same event.

```csharp
button.ResourceReferenceFailed += (_, e) =>
    logger.LogError(e.Exception, "Resource {Key} cannot update {Property}",
        e.ResourceKey, e.PropertyName);
```

Directly assigning a referenced property does not remove the reference. Clear it first when manual
assignment should take ownership:

```csharp
button.ClearResourceReference(nameof(Control.BackColor));
button.BackColor = SKColors.CornflowerBlue;
```

References use weak global registrations and are explicitly detached during control disposal.
Removing an undisposed control from the visual tree changes its lookup ancestry but does not dispose
it; application and local resources can still apply while the caller owns the detached control.

## Threading

Dictionary collection operations are protected against corruption, but a mutation immediately
runs affected CLR property setters on the calling thread. Update resources used by live controls on
the UI/dispatcher thread, for example with `Application.RunOnUIThread` when the caller starts on a
worker thread.

## Current limits

- Application/window/control keys and values remain arbitrary runtime objects. Theme JSON uses the
  stricter typed key and value allow-list documented in [theme JSON schema](theme-json-schema.md).
- Property references use case-sensitive public CLR property names. Trimming/AOT metadata policy
  must be settled before enabling aggressive member trimming for applications.
- Merged dictionaries and resource factories remain future work. Theme inheritance resolves before
  publication, while compatible theme values transition through the shared animation scheduler.
- In-place updates are currently observed for the framework brush hierarchy only, not for every
  mutable resource object.
- Resource values are applied directly. General-purpose implicit conversion is intentionally not
  performed because it would make theme errors dependent on culture and converter availability.
