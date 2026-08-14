# Shapes and vector geometry

ModernFormsNext provides platform-neutral vector controls built on the same control, Brush, Skia
surface, layout, invalidation, and presentation-transform systems as the rest of the framework.
The public model never exposes `SKPath` or `SKPaint`.

## Control hierarchy

`Shape` is the common abstract `Control` base. Its concrete controls are `Ellipse`, `Circle`,
`Line`, `Polygon`, `Polyline`, and `Path`. Ellipse fits its centered stroke inside its client bounds;
Circle does the same while centering itself in the smaller client dimension; Line uses `StartPoint`
and `EndPoint`; Polygon automatically closes
its observable `Points`; Polyline remains open and stroke-only; and `Path.Data` renders reusable
`Geometry`. `Polygon.FillRule` selects winding or even-odd behavior for overlapping and
self-intersecting polygon regions.

Shapes participate in normal Bounds, Margin, Padding, Anchor, Dock, minimum/maximum size,
visibility, enabled state, parent clipping, accessibility bounds, and Designer selection. Vector
properties invalidate rendering rather than layout. Resizing Ellipse or Circle updates its defining
geometry automatically.

## Fill and stroke

`Fill` and `Stroke` accept the existing shareable `ModernFormsNext.Drawing.Brush` hierarchy:
`SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, `SweepGradientBrush`, and
`GlassBrush`. A null brush or `NoBrush` skips that operation. `Line.Fill` is hidden because an open
line has no fill semantics.

`StrokeThickness` is measured in logical pixels. `StrokeLineCap` supports Flat, Round, and Square;
`StrokeLineJoin` supports Miter, Round, and Bevel; and `MiterLimit` controls maximum miter length.
Undefined enum values, negative or non-finite thicknesses, and non-finite points are rejected.
All built-in shapes use the same anti-aliased Skia paint factory for both fill and stroke. Geometry
coordinates remain floating-point through native path construction and are scaled only at the
logical-to-device rendering boundary.

```csharp
var ellipse = new Ellipse
{
    Bounds = new Rectangle(24, 24, 180, 96),
    Fill = new SolidColorBrush(Color.CornflowerBlue),
    Stroke = new SolidColorBrush(Color.MidnightBlue),
    StrokeThickness = 4
};
```

## Geometry model and sharing

`Geometry` is mutable and shareable. The focused initial model contains `LineGeometry`,
`RectangleGeometry`, `EllipseGeometry`, and `PathGeometry`. A path owns observable `PathFigure`
objects containing `LineSegment`, `QuadraticBezierSegment`, or `BezierSegment` values. A figure's
`IsClosed` flag provides explicit closure, while `GeometryFillRule` selects winding or even-odd
fill containment.

Every rendered mutation increments `Geometry.Version` and raises `Geometry.Changed` synchronously.
Each Path uses a weak subscription, so one geometry invalidates multiple consumers without a
long-lived geometry retaining disposed controls. Geometry and collections should be mutated on the
UI thread while in use.

```csharp
var geometry = new PathGeometry { FillRule = GeometryFillRule.EvenOdd };
var figure = new PathFigure(new PointF(8, 88), isClosed: true);
figure.Segments.Add(new QuadraticBezierSegment(new PointF(36, 4), new PointF(72, 34)));
figure.Segments.Add(new BezierSegment(
    new PointF(96, 4), new PointF(132, 22), new PointF(152, 88)));
geometry.Figures.Add(figure);

var path = new ModernFormsNext.Path
{
    Data = geometry,
    Fill = new SolidColorBrush(Color.Gold),
    Stroke = new SolidColorBrush(Color.DarkOrange),
    StrokeThickness = 3
};
```

Coordinates are local logical pixels. The first version intentionally has no WPF-style Stretch,
arc segment, geometry group, SVG-string core model, or separate shape layout engine.

`ModernFormsNext.Path` intentionally follows the conventional vector-control name. Code that also
uses `System.IO.Path` should qualify the file-system type or introduce an alias:

```csharp
using IOPath = System.IO.Path;

var assetPath = IOPath.Combine("Assets", "logo.png");
var vectorPath = new ModernFormsNext.Path { Data = geometry };
```

## Transforms, clipping, and hit testing

`Geometry.Transform` is a finite `Matrix3x2` applied before the owning control's normal translation,
scale, rotation, and opacity presentation transform. Rendering and hit testing use the same native
path and inverse presentation mapping, so a transformed shape does not retain its old hit region.

Shape pixels render into the normal per-control back buffer and compose through the existing
ancestor pipeline. This provides hard control and parent clipping without a second clip system.
Ellipse and Circle automatically inset their contour by half `StrokeThickness`, retaining their
complete centered stroke and anti-aliased fringe inside the back buffer. Explicit Line, Polygon,
Polyline, and Path coordinates are not rewritten; portions intentionally placed outside the
control's back-buffer bounds remain clipped. Geometry is not currently exposed as a generic Control
clip source.

`Geometry.Transform` is applied while the path is still vector data, before rasterization. A
Control presentation transform operates later on the complete control backbuffer, consistently
with every other control. Prefer `Geometry.Transform` when the vector itself should be rasterized
at its final transformed coordinates; use Control transforms when the whole control, including its
children and visual effects, should move as one presentation layer.

Hit testing checks filled path containment when a visible Fill exists and the stroked path when a
visible Stroke and positive thickness exist. Lines receive an additional two-logical-pixel pointer
tolerance. Transparent corners of an ellipse do not target it. Designer selection stays bounds-based
so move and resize handles remain reliable.

## Rendering and cache lifetime

All concrete controls build platform-neutral geometry; one converter creates an owned `SKPath` and
one renderer applies Fill and Stroke. Each control caches the native path by geometry reference,
geometry version, and density scale. Stroke hit paths have a separate cache keyed by stroke metrics.
Mutating points, segments, transforms, or bounds disposes stale native caches. Temporary paints and
shaders are disposed after each draw; cached paths are disposed on invalidation or control disposal.

Windows and the experimental Android backend both use this Skia control surface. Android applies
device density before the shared renderer, retaining logical-pixel geometry and stroke semantics.
An Android build is not evidence of emulator/device visual or touch validation.

## Designer support

All concrete shapes appear in the **Shapes** Toolbox category and use runtime-safe preview rendering.
The Property Grid exposes Appearance and Geometry values. `PointCollection` has an ordered dialog
with Add, Remove, Up, Down, and separate X/Y fields. `PathGeometry` has a structured dialog for
figures, `StartPoint`, segment type and coordinates, `IsClosed`, `FillRule`, and transform. The
inline editor remains available for quick entry: points use `x,y; x,y; ...`, and geometry uses a
compact command form:

```text
path evenodd M 8,88 Q 36,4 72,34 C 96,4 132,22 152,88 Z
```

`line`, `rectangle`, `ellipse`, and an optional
`transform(m11,m12,m21,m22,m31,m32)` prefix are supported. The Designer does not include a graphical
Bezier canvas. `.mfdesign` stores platform-neutral values; code generation emits the public API;
and the reverse parser understands the generator's supported output. Numeric display, parsing, JSON,
and generated C# use invariant decimal syntax independently of the current UI culture.

Open **Shapes** in ControlGallery for solid and gradient fills, gradient and solid strokes, caps,
joins, a reusable Bezier path, a vector transform, and stroke-safe control-bound smoke tests.
