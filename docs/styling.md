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

## Font resolution and lifetime

Font inheritance is iterative and cycle-bounded. A detached control, a style without an owner, a
missing parent style, or a cyclic style graph resolves to the framework default instead of
depending on a window host or platform theme manager. This behavior is identical on Windows,
Android, and in headless tests.

`Font.ToTypeface()` returns a shared typeface. Resolved family/weight/slant combinations are kept
in a thread-safe process cache with a hard capacity; when the capacity is reached, uncached
families use the stable regular or bold framework fallback. Controls and callers must not dispose
the returned typeface. The cache avoids repeated `SKTypeface.FromFamilyName` calls during layout
and painting, while the fixed capacity prevents unbounded growth.

Internal counters can measure style traversal, cache requests/hits/misses, platform family lookup,
and fallback use. They are disabled by default and add no diagnostic string allocation to the
render path. Regression tests enable them explicitly to ensure repeated rendering reuses an
already-resolved typeface and keeps style traversal bounded.
