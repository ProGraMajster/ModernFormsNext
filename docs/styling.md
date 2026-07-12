# Styling

ModernFormsNext controls use `ControlStyle` as their primary styling model. A control has a normal style through `Control.Style` and a hover style through `Control.StyleHover`; renderers resolve the active visual state through the control's current style.

## Compatibility Color Properties

`Control.BackColor` and `Control.ForeColor` are compatibility-oriented properties for code that is being migrated from Windows Forms-style APIs. They are supported public API, but they do not introduce a second color system.

`BackColor` maps to `Control.Style.BackgroundColor`, and `ForeColor` maps to `Control.Style.ForegroundColor`. Their getters use the same effective style resolution as the style system, so parent/default style values are still honored when the local style value is not set.

```csharp
var label = new Label
{
    ForeColor = SKColors.White,
    BackColor = SKColors.Black
};
```

The equivalent direct style-system code is:

```csharp
var label = new Label();
label.Style.ForegroundColor = SKColors.White;
label.Style.BackgroundColor = SKColors.Black;
```

Both examples update the same `ControlStyle` instance. Setting `BackColor` or `ForeColor` affects only the normal style and does not synchronize, copy, or modify `StyleHover`. Configure hover colors explicitly when a control needs a different hover appearance.
