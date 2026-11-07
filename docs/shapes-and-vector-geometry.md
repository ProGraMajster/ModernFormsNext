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

`Geometry` is mutable and shareable. The model contains `LineGeometry`, `RectangleGeometry`,
`EllipseGeometry`, `PathGeometry`, `GeometryGroup`, and `CombinedGeometry`. A path owns observable `PathFigure`
objects containing `LineSegment`, `QuadraticBezierSegment`, `BezierSegment`, or `ArcSegment` values. A figure's
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

Coordinates are local logical pixels. `Geometry.GetBounds()` computes tight transformed path bounds,
excluding a control's stroke. It uses the same converter as rendering and hit testing; cache repeated
queries against `Version`. There is no separate shape layout engine.

### Endpoint arcs, groups and filled-area operations

`ArcSegment` takes an endpoint, finite non-negative X/Y radii (`Size`), ellipse-axis rotation in
degrees, `IsLargeArc`, and `SweepDirection`. Positive Y points down. Coincident endpoints omit an
arc; a zero radius produces a line; undersized radii are enlarged uniformly to connect the endpoints.
The preceding segment or figure start supplies the start point. See the
[SVG endpoint arc conventions](https://www.w3.org/TR/SVG/paths.html#PathDataEllipticalArcCommands).

`GeometryGroup.Children` contributes ordered contours using the group's `FillRule`. It does not
union their filled regions: overlapping contours can cancel under even-odd fill. Each child's
transform precedes the group transform. Repeated children are legal; cycles are rejected before
mutating ownership or subscriptions. Removing the last reference detaches its change listener.
Designer persistence stores values rather than reference identities: repeated borrowed children
become separate value copies after a round trip. Define shared runtime instances explicitly in
code when subsequent mutations must affect multiple operands or controls together.

`CombinedGeometry` performs `Union`, `Intersect`, `Xor`, or `Exclude` (first minus second) on filled
areas. Null is empty, open contours are implicitly closed, and stroke widths do not participate.
Operand transforms precede the operation; the combined transform follows it. A failed native
operation raises `InvalidOperationException`; invalid transformed coordinates raise `ArgumentException`.

```csharp
var first = new EllipseGeometry(new RectangleF(0, 0, 60, 60));
var second = new EllipseGeometry(new RectangleF(30, 0, 60, 60));
var crescent = new CombinedGeometry(GeometryCombineMode.Exclude, first, second);
var shape = new ModernFormsNext.Path
{
    Size = new Size(180, 100), Data = crescent,
    Stretch = GeometryStretch.Uniform, Fill = new SolidColorBrush(Color.Gold)
};
```

### Bounded path-data import

`PathGeometry.Parse(string)` accepts invariant SVG-compatible **path data**, not an SVG XML document.
Supported absolute/relative commands are `M L H V C S Q T A Z`; repeated parameter sets, smooth-curve
reflection, signs, decimal/exponent syntax, commas and whitespace are supported. Arc flags are `0`
or `1`; parsed negative radii are normalized to positive. Typed `ArcSegment` radii must be non-negative.

The parser admits at most 262,144 UTF-16 characters, 16,384 segments and 1,024 figures, before native
work. Non-finite values and relative/reflected arithmetic overflow fail with `FormatException`.
`ToPathData()` writes deterministic absolute `M/L/Q/C/A/Z` commands with round-trip Single precision.
`FillRule` and `Transform` remain separate properties; text output does not encode them. Large typed
runtime paths may produce text beyond the parser's fixed ceilings.

```csharp
var outline = PathGeometry.Parse("M10 40 Q40 0 70 40 A20 20 0 0 1 110 40 Z");
string data = outline.ToPathData();
```

XML, SVG resources, URLs, styles, text, gradients, `<use>` and external references are not imported.
Parsing performs no file or network access. Use the existing Brush APIs for fills and strokes.

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
control's back-buffer bounds remain clipped.

`Shape.Stretch` defaults to `GeometryStretch.None`, preserving raw coordinates and their origin.
`Fill` scales non-degenerate axes independently; `Uniform` fits and centers the whole geometry;
`UniformToFill` covers the available area and clips excess pixels. Mapping uses tight transformed
geometry bounds and `PaddedClientRectangle`, reserving half `StrokeThickness` on each edge even
without a stroke brush. Stroke thickness is not stretched. A degenerate axis is centered without
inventing an area; the remaining axis determines uniform scale. Wide miters may still be clipped.
Size, padding, density, geometry and stroke-metric changes participate in cache selection.

`Control.ClipGeometry` borrows reusable geometry in local logical coordinates. It clips the complete
composition, including descendants, effects and shadow, through the same path and presentation
mapping as pointer hit testing. Null removes it; empty geometry clips everything. Changes repaint
the composition owner without changing layout or keyboard focus. Accessibility bounds and Designer
selection remain rectangular. NativeViewHost islands suspend through their existing unsupported
composition mechanism while they or an ancestor have a geometry clip.

```csharp
panel.ClipGeometry = new EllipseGeometry(new RectangleF(0, 0, 200, 120));
```

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
figures, `StartPoint`, segment type and coordinates, arc radii/rotation/flags, `IsClosed`, `FillRule`, and transform. The
inline editor remains available for quick entry: points use `x,y; x,y; ...`, and geometry uses a
compact command form:

```text
path evenodd M 8,88 Q 36,4 72,34 C 96,4 132,22 152,88 Z
```

`line`, `rectangle`, `ellipse`, and an optional
`transform(m11,m12,m21,m22,m31,m32)` prefix are supported. Path commands use the runtime parser.
The same dialog also displays the canonical path and draggable nodes for the selected figure,
with control handles for its selected quadratic/cubic segment. The view stays fixed during a drag.
Escape, capture cancellation or focus loss restores the coordinate present at mouse-down; Cancel
discards the entire private draft. A singular transform disables dragging and retains numeric editing.
Arc endpoints are draggable; radii, rotation and flags use the structured fields. Groups and Boolean
operands remain lossless structured data authored in code; the graphical dialog edits PathGeometry.

`.mfdesign` stores platform-neutral values; code generation emits the public API;
and the reverse parser understands the generator's supported output. Numeric display, parsing, JSON,
and generated C# use invariant decimal syntax independently of the current UI culture.

Extended vectors, `ClipGeometry` and `Stretch` use format **3**, which also supports inherited designs.
Ordinary documents stay format 1; inherited designs without extended vectors stay format 2. The
writer chooses the capability marker without changing caller metadata. Older readers reject format
3 rather than silently dropping an arc or clip; downgrading the marker is rejected by current readers.

Before allocation/conversion, the Designer bounds expanded typed visits, neutral property values,
nesting and text. Shared references consume the expansion budget on each occurrence. Its hard vector
work estimate is 65,536 shared across the document (or the supplied property sequence), with Boolean output conservatively charged as
`sum + 6 * sum * sum` for both operands, including their self intersections. Line/quadratic/cubic
degree weights are 1/2/3, and an arc reserves four cubics. Projected coordinate extent is bounded at one billion
logical pixels. These admission estimates can reject a complex project even if its final area is
small. They are independent of ordinary table preview settings and cannot be disabled by `.mfdesign`.
Typed runtime geometry and runtime TableLayoutPanel counts receive no Designer limits. Invalid data
produces a diagnostic; preview never loads application types or runs application code for geometry.

Open **Shapes** in ControlGallery for solid and gradient fills, gradient and solid strokes, caps,
joins, arcs, grouped/combined areas, Stretch, a shared mutable geometry clip, a vector transform,
and stroke-safe control-bound smoke tests.

## Current limitations

- Full SVG document import is unsupported; the bounded path-data subset is explicit above.
- Groups and Boolean operand trees do not have a graphical composition editor. Edit their typed
  definitions; the PathGeometry canvas does not replace or flatten them.
- Windows and Android share the Skia renderer. The [Android release matrix](android-release-validation.md)
  records bounded emulator/physical ellipse and Bezier animation screenshots and software callback
  profiles. Physical touch ergonomics, GPU/cache profiling and native-density child raster quality
  remain unverified; a package build alone does not supply that evidence.

See [Known limitations](known-limitations.md) and the
[1.10.0 audit proposals](audits/1.10.0-documentation-and-limitations-audit.md#p3-extend-vector-geometry-and-path-designer-tooling)
for issue-ready scope without claiming those features are implemented.
