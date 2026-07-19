# Paint and gradients

ModernFormsNext uses one public visual-value hierarchy rooted at
`ModernFormsNext.Drawing.Brush`. The name is retained for compatibility with the existing controls,
Designer, and generated code; there is no parallel `Paint` hierarchy. Brushes are mutable,
shareable, observable, and rendered through the same Skia adapter on Windows and the experimental
Android surface host.

For the architectural rationale and migration analysis, see
[Paint and gradient architecture](architecture/paint-and-gradients.md) and
[ADR: Evolve Brush as the canonical paint value](architecture/decisions/ADR-Paint-And-Gradient-System.md).

## Available brush types

| Type | Purpose |
| --- | --- |
| `SolidColorBrush` | One color, including alpha. |
| `LinearGradientBrush` | A gradient between two relative points. |
| `RadialGradientBrush` | A circular gradient with an optional separate focal origin. |
| `SweepGradientBrush` | An angular gradient around a relative center. |
| `GlassBrush` | The existing layered glass treatment. |
| `NoBrush` | An explicit request to paint no fill. |

`Brush.Opacity` multiplies the alpha of every produced color. `Brush.Transform` applies a
platform-neutral `Matrix3x2` to the resolved brush coordinate space. Both affect rendering only;
they do not request layout.

## Solid colors

Use `System.Drawing.Color` through the platform-neutral `PaintColor` member in new code:

```csharp
using ModernFormsNext;
using ModernFormsNext.Drawing;
using Color = System.Drawing.Color;

var card = new Panel
{
    BackgroundBrush = new SolidColorBrush(Color.CornflowerBlue)
    {
        Opacity = 0.8f
    }
};
```

The existing Skia-compatible `Color` member and constructor remain available. `Color` and
`PaintColor` are views of the same backing value, not independent settings.

## Linear gradients

`Start` and `End` use relative coordinates resolved against the current paint bounds. `(0, 0)` is
the top-left corner and `(1, 1)` is the bottom-right corner. The renderer resolves these points for
every current bounds and does not apply DPI scaling a second time.

```csharp
using System.Drawing;
using ModernFormsNext.Drawing;

var gradient = new LinearGradientBrush
{
    Start = new PointF(0f, 0f),
    End = new PointF(1f, 0f),
    SpreadMode = GradientSpreadMode.Pad
};

gradient.GradientStops.AddRange([
    new GradientStop(Color.MidnightBlue, 0f),
    new GradientStop(Color.DeepSkyBlue, 0.55f),
    new GradientStop(Color.White, 1f)
]);

panel.BackgroundBrush = gradient;
```

Finite coordinates outside `0..1` are accepted. They are useful when positioning a repeated or
reflected interval outside the painted area. Absolute coordinate mapping is not part of the current
contract.

## Radial and sweep gradients

A radial radius is a non-negative multiplier of the smaller bounds dimension. A zero radius is
valid and deterministically paints the final stop color. `GradientOrigin` defaults to following
`CenterPoint`; assigning it explicitly creates a focal/two-point radial gradient.

```csharp
var radial = new RadialGradientBrush
{
    CenterPoint = new PointF(0.55f, 0.5f),
    GradientOrigin = new PointF(0.25f, 0.3f),
    Radius = 0.7f
};

radial.GradientStops.AddRange([
    new GradientStop(Color.White, 0f),
    new GradientStop(Color.RoyalBlue, 0.5f),
    new GradientStop(Color.MidnightBlue, 1f)
]);
```

`SweepGradientBrush` uses `CenterPoint`, `StartAngle`, and `EndAngle`. Angles are finite clockwise
degrees. Sweep gradients use the same observable stops, opacity, and transform contract.

## Gradient stops and spread

Every stop offset must be finite and in the inclusive range `0..1`. Invalid values throw
`ArgumentOutOfRangeException`; values are not silently clamped. Stops may be added in any order.
Rendering uses a stable offset sort, so distinct stops with the same offset retain collection order
and can form a hard transition.

`GradientStopCollection` observes membership, order, and each contained `GradientStop`. `Add`,
`AddRange`, `Insert`, replacement by index, `Move`, `Remove`, `RemoveAt`, and `Clear` all update the
rendered result. The same stop instance cannot occur twice in one collection, but distinct instances
with equal values are supported.

Spread modes map consistently to the shared renderer:

- `Pad` extends the nearest edge color;
- `Repeat` repeats every interval in the same direction;
- `Reflect` mirrors alternating intervals.

Color interpolation currently follows Skia's standard color interpolation. A selectable linear-
light or color-space API is deferred until ThemeManager can define and validate it consistently.

## Opacity and transforms

Opacity is a finite value from `0` through `1` and multiplies the alpha already present in colors.
Transforms are finite `System.Numerics.Matrix3x2` values:

```csharp
using System.Numerics;

gradient.Opacity = 0.75f;
gradient.Transform =
    Matrix3x2.CreateScale(1.15f) *
    Matrix3x2.CreateRotation(0.15f) *
    Matrix3x2.CreateTranslation(8f, 0f);
```

Relative points are resolved against the current logical paint bounds before the transform is sent
to the renderer. Translation components therefore use the same logical coordinate space as those
bounds. Resize and DPI changes rebuild the bounds-specific shader; a cached shader is never reused
for a different size.

## Color fallback and no fill

The established color APIs remain supported. For a standard control background:

1. a non-null `BackgroundBrush` is rendered;
2. `BackgroundBrush == null` uses `BackColor`/the resolved `ControlStyle` background;
3. `BackgroundBrush = new NoBrush()` explicitly suppresses the control's fill so the already
   painted parent/background can show through.

Assigning `BackColor` does not discard an assigned brush. Clear `BackgroundBrush` to return to the
color fallback.

## Runtime mutation and dynamic resources

Brushes raise `Changed` synchronously after a rendered value changes. Standard brush-valued control
properties use weak, reference-counted subscriptions and invalidate only live controls that
currently reference that brush. Mutating one stop does not require replacing or reassigning the
brush.

```csharp
const string key = "Card.Background";

Application.Resources[key] = gradient;
card.SetResourceReference(nameof(Control.BackgroundBrush), key);

// Repaints card through the brush notification; the dictionary entry is unchanged.
gradient.GradientStops[0].PaintColor = Color.Teal;

// Re-resolves normal application/window/ancestor/control precedence.
card.Resources[key] = new SolidColorBrush(Color.Gold);
```

Resource replacement, fallback, clearing the reference, replacing the control property, and control
disposal detach the previous subscription. Multiple properties on one control that share a brush
use one handler with reference counting. A long-lived resource brush holds only a weak reference to
its consumers.

Resource and brush mutations that affect live controls must run on the UI/dispatcher thread. The
current model raises one notification per property or collection operation; it does not yet provide
a batch-update scope for coalescing animation frames.

## Designer compatibility

Designer documents and generated `.Designer.cs` code preserve opacity, transform, spread mode,
radial focal origin, geometry, and stops. Documents created before these fields existed are read
with compatible defaults: full opacity, identity transform, `Pad`, and a radial origin that follows
the center. The existing Skia-compatible point/color members remain readable and generated code
continues to use the established brush type names.

Changing `GradientStops` from `List<GradientStop>` to `GradientStopCollection` is a binary signature
change. Common source operations remain available, including indexing, enumeration, collection
initializers, `Add`, `AddRange`, `Remove`, and `Clear`. Code that explicitly requires `List<T>` must
accept `IList<GradientStop>` or `GradientStopCollection` instead.

## Planned JSON representation

There is no runtime brush JSON converter in this stage. Implementing half of ThemeManager's loading
and validation pipeline would create a second, premature contract. The planned schema direction is
renderer-neutral, camel-cased, and discriminated by `type`:

```json
{
  "type": "linearGradient",
  "opacity": 0.85,
  "transform": [1, 0, 0, 1, 8, 0],
  "spread": "reflect",
  "start": { "x": 0, "y": 0.5 },
  "end": { "x": 0.35, "y": 0.5 },
  "stops": [
    { "offset": 0, "color": "#FF165DAD" },
    { "offset": 1, "color": "#FFF8C24A" }
  ]
}
```

The intended discriminators are `solid`, `linearGradient`, `radialGradient`, `sweepGradient`, and
`none`. Eight-digit colors use `#AARRGGBB`; six-digit colors are opaque. A radial value adds
`center`, `origin`, and `radius`; a sweep value adds `center`, `startAngle`, and `endAngle`. The
future ThemeManager work must version this schema, validate unknown/missing fields, produce path-
specific errors, and test round trips before it becomes a supported file format.

## Current limitations

- Relative mapping is the only supported coordinate mode; absolute mapping is deferred.
- Radial gradients are circular and use one radius, not independent X/Y radii.
- Existing public Skia-compatible members remain for source compatibility; use the neutral members
  in new application, theme, and future shape code.
- Advanced color-space interpolation and brush animation helpers are not implemented.
- Shader objects are intentionally short-lived and disposed per rendering scope; there is no global
  bounds-dependent native shader cache.
- Android uses the same model and shader factory, but Android support as a whole remains
  experimental and still requires physical-device GPU validation.

See the **Paint & Gradients** page in `samples/ControlGallery` for manual resize, spread, transform,
runtime mutation, and dynamic-resource checks.
