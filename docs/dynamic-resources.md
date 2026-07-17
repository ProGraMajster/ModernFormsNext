# Dynamic resources

Dynamic resources let a normal public control property follow a value stored at application,
window, ancestor-control, or control scope. They are the shared foundation for future themes,
localization, styles, and control defaults; they are not a second data-binding or property system.

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

## Lookup order and fallback

For a control, lookup proceeds from the most specific scope to the broadest:

1. the control's `Resources`;
2. each ancestor control's `Resources`;
3. the owning window's `Resources`;
4. `Application.Resources`.

If an override is removed, the next matching scope becomes effective automatically. If no scope
contains the key, the property returns to the value captured when `SetResourceReference` was first
called. Reparenting a control refreshes references for that subtree.

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

- Keys and values are runtime objects; JSON theme/localization loaders are planned separately.
- Property references use case-sensitive public CLR property names. Trimming/AOT metadata policy
  must be settled before enabling aggressive member trimming for applications.
- Merged dictionaries, explicit dictionary inheritance, resource factories, and transition
  animation are future ThemeManager work.
- Resource values are applied directly. General-purpose implicit conversion is intentionally not
  performed because it would make theme errors dependent on culture and converter availability.
