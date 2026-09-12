# Accessibility for links and numeric controls

These controls extend the existing `AccessibleObject` tree. Native backends and
`AutomationSession` consume the same objects and actions. Phase 4 implementation is under
validation; the examples below describe the shared contract, not a claim of physical-device or
screen-reader coverage.

## LinkLabel

The label retains its text container. Each nonempty, currently attached `LinkLabel.Link` has one
cached `Hyperlink` child with the compatibility `Link` role. `Name` is the displayed substring,
`AutomationId` is `Link.Name`, and `Value` is absent. `LinkData` and `Tag` are never inspected.
Offsets remain UTF-16; CRLF offsets are preserved, and a range containing only half a surrogate
pair is not an independently actionable link.

```csharp
var links = new LinkLabel { Text = "Read docs or help", Width = 280 };
links.Links.Clear();
var docs = links.Links.Add(5, 4);
docs.Name = "documentation";
links.Links.Add(13, 4).Name = "help";
links.LinkClicked += (_, e) => ShowHelp(e.Link.Name);
```

`Invoke` calls the existing activation path: the link becomes visited, the current link changes,
and `LinkClicked` runs once. `Focus` selects the label through normal framework focus and selects
the link. Disabled, empty, detached, disposed, or closed-window targets reject actions. A callback
that removes or reparents the target invalidates the pending operation; reentrant activation is
rejected. Application event exceptions still propagate, while committed visited state is retained.

A link may belong to only one collection. Remove it before moving it between labels. Duplicate
ownership and overlapping ranges are rejected before changing either collection. Reordering a live
link preserves its runtime identity. Removing it makes its retained peer inactive; that peer does
not keep the label or application payloads alive.

Bounds are calculated on demand by the same renderer layout used for painting, including font,
scale, padding, alignment, and CRLF. A multiline link reports union bounds; hit testing checks its
individual clipped fragments, so gaps between fragments are not links. Reading bounds before the
first paint does not focus a control or create a native popup. Link range, name, enabled, visited,
focus, and collection changes use existing accessibility notifications.

## NumericUpDown

The owner is a `Spinner` with a writable numeric range. Its name comes from `AccessibleName`, then
`Name`, rather than the current numeric text. Children are the **existing embedded TextBox**, an
increase button, and a decrease button. The text box retains its own selection, text input,
read-only state, and text provider. No second editor is created.

```csharp
var quantity = new NumericUpDown {
    AccessibleName = "Quantity",
    Minimum = -10m,
    Maximum = 100m,
    DecimalPlaces = 2,
    Increment = 0.25m,
    Value = 1.25m,
    ReadOnly = true
};
// ReadOnly affects typing in the editor. Spinner buttons and the owner range remain writable.
quantity.AccessibilityObject.PerformAction(AccessibleActions.Increment);
```

`ReadOnly` and `AllowManualEdit` describe the embedded editor. They do not make the owner's range
read-only, because existing spinner buttons remain usable. Semantic writes call `Value`,
`UpButton`, or `DownButton`, preserving configured decimal rounding, inclusive limits, culture-aware
formatting, and normal value events. The buttons saturate safely even at decimal extrema. Invalid,
nonfinite, out-of-range, or nonnumeric action payloads are rejected without parsing arbitrary
objects or localized strings.

Storage stays `decimal`. The common range contract uses finite `double` values; Android may project
those values to `float`. Native clients therefore cannot distinguish every representable decimal.
Typed decimal and integral writes preserve their precision before normal control rounding;
floating-point writes use their available precision, with advertised minimum/maximum endpoints
mapped to the exact decimal limits. Applications needing exact text entry should use the existing
editor. No backend-specific numeric storage is introduced.

`RangeValueChanged` reports changed limits, steps, or precision after state is committed. It does
not invent a public `ValueChanged` when only metadata changes. Callback failures do not leave the
editor's formatting guard enabled, and disposing the control invalidates retained button actions.

## ScrollBar

Horizontal and vertical scrollbars expose their orientation and an integral writable range.
The range is inclusive, including `Int32.MinValue` through `Int32.MaxValue`. `SetValue` accepts only
finite integral numeric values within the current limits. Small-step actions clamp without integer
overflow. Equal minimum/maximum and zero steps are supported.

Programmatic and semantic value assignments preserve `ValueChanged`; they do not synthesize a
public `Scroll` gesture event. Metadata changes use `RangeValueChanged`. A scrollbar's `LargeChange`
is its existing bounded increment value, not a viewport measurement. The scrollbar does not claim
the viewport `Scroll` capability: that belongs to its `ScrollableControl` owner.

## Threading and validation

Read live properties and invoke actions on the owning UI thread. A retained semantic object is not
a dispatcher or a window-lifetime handle. Closing the host window, disposing a control, or removing
a logical item denies further actions. Explicit application labels and user event handlers retain
their normal ownership and exception behavior.

`AccessibilityCurrentControlTests` exercises these shared contracts using the production controls
and deterministic TestHost, including 100%, 150%, and 200% geometry. Native UIA/TalkBack and physical
screen-reader results must be reported separately in the issue validation matrix. This document
does not claim that those checks were performed by the shared tests.
