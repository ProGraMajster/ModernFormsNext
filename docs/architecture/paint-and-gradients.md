# Paint and gradient architecture

## Status and scope

This document records the state found after the 1.8.0 release and the target architecture for the
paint/brush hardening stage. The stage deliberately stops before ThemeManager, shapes, charts, a
new animation scheduler, or a theme JSON loader. Its output is a reusable visual-value contract for
those later features.

## 1. Current color, brush, and gradient system

ModernFormsNext already has a public `ModernFormsNext.Drawing.Brush` hierarchy. It contains
`SolidColorBrush`, `GradientBrush`, `LinearGradientBrush`, `RadialGradientBrush`,
`SweepGradientBrush`, and `GlassBrush`. A `GradientBrush` owns a mutable `List<GradientStop>`.
Controls expose `BackgroundBrush` and `TextBrush`; `GroupBox` and `Switch` expose additional
brush-valued properties. `BackColor`, `ForeColor`, `ControlStyle`, borders, and most specialized
renderers continue to use `SKColor`.

The shared text path supports brush-filled text by painting a text mask and applying the brush with
`SrcIn`. Background and state brushes are rendered by `SkiaExtensions.RenderBrushBackground`.
The Visual Studio Designer serializes the existing brush hierarchy into `.mfdesign` and generates
matching C# object initializers.

## 2. Existing types to reuse

- `Brush` and its existing derived types are the established user-facing vocabulary.
- `GradientStop` already provides the expected color/offset concept.
- `Control.BackgroundBrush`, `Control.TextBrush`, `GroupBox` brush properties, and `Switch` brush
  properties are existing integration points.
- `ResourceDictionary` and `Control.SetResourceReference` already provide application, window,
  ancestor-control, and local-control precedence through normal CLR setters.
- `SkiaExtensions.RenderBrushBackground` is the existing shared rendering entry point.
- `Control.Invalidate`, renderer clipping, and the per-control Skia back buffer are the existing
  redraw pipeline on Windows, Android, the Designer, and headless surfaces.
- Designer structured values and `CSharpLiteralWriter` are the existing serialization/code
  generation contract.

## 3. Problems in the current architecture

1. Brushes and stops are mutable but publish no change notification. Mutating a shared brush does
   not invalidate controls that use it.
2. `GradientStops` is a raw `List<T>`. Adding, removing, replacing, reordering, or mutating a stop
   cannot invalidate cached/rendered state safely.
3. Stops are sorted and copied with LINQ every time a gradient is painted.
4. Gradient spread is hard-coded to Skia `Clamp`; repeat and reflect are unavailable.
5. The public coordinate and color members use `SKPoint` and `SKColor`, coupling visual values to a
   renderer implementation.
6. Shader construction is mixed into `SkiaExtensions`, and every gradient path repeats ownership
   and single-stop handling.
7. `null` means "use the fallback color", so there is no explicit value for "paint nothing".
8. The current radial model has one circular radius and no separate gradient origin.
9. Existing control brush setters only invalidate on replacement and do not manage subscriptions.
10. Brush serialization does not preserve spread, opacity, or future transform data.

## 4. Naming consistency

The repository and its public control API consistently use `Brush`, while the low-level Skia type
is named `SKPaint`. Introducing a second public `Paint` hierarchy would make `BackgroundBrush` and
the existing Designer contract adapters to a competing abstraction. This stage therefore keeps
`Brush` as the canonical framework visual value. "Paint" is used as the general architectural
concept and for internal renderer operations, not as a duplicate public object model.

New platform-neutral members use .NET primitives (`System.Drawing.Color`, `PointF`, and
`System.Numerics.Matrix3x2`). Existing `SKColor`/`SKPoint` members remain compatibility adapters to
the same backing values. They are not a second source of truth.

## 5. Direct SkiaSharp dependencies

The existing dependency surface includes brush colors and points, `ControlStyle`, `Theme`, border
styles, paint event/canvas APIs, text measurement, control renderers, and the Designer's brush
editor. Removing every public Skia type is a separate breaking migration. This stage removes Skia
from the preferred brush coordinate/color/transform API while retaining the already-published Skia
members for source compatibility.

All new shader construction belongs in one internal Skia brush adapter. Specialized color pickers
may continue to construct their own procedural shaders because those shaders are control
implementation details rather than reusable `Brush` values.

## 6. Windows and Android impact

The model, validation, ordering, mutation notification, coordinate math, and resource behavior live
in the multi-targeted shared framework project. Windows and Android therefore use identical paint
semantics. The adapter consumes the bounds already supplied by the current Skia canvas pipeline and
does not apply DPI a second time. Relative points remain stable when controls resize and when a
window moves between DPI contexts.

Android remains experimental. This work does not change its lifecycle, windowing, input, AOT, or
publication status. Paint behavior must be verified through the shared headless Skia tests and an
Android target build; a physical-device GPU check remains manual.

## 7. Dynamic resources

A mutable brush may be stored at application, window, or control scope. Resource replacement still
uses the existing CLR property setter. Once assigned, a control holds one weak invalidation
subscription per distinct brush, reference-counted across its brush properties. Mutating a brush or
one of its stops invalidates only controls currently using it. Replacement, fallback, explicit
reference removal, and control disposal detach subscriptions. The brush's event must not keep a
forgotten, unreachable control alive.

Resource dictionaries continue to notify only when the resource entry changes. In-place brush
changes flow through the brush's own notification contract; no global resource broadcast is added.
Mutations have the same UI-thread affinity as current resource updates and control setters.

## 8. Future ThemeManager impact

ThemeManager can publish shared brushes under stable dynamic-resource keys. Runtime changes can
update a brush in place for targeted invalidation, while atomic theme replacement can replace the
resource value. Opacity, spread mode, stable stop ordering, and neutral values form a future JSON
schema without embedding Skia names. This stage documents that schema direction but does not add a
partial theme serializer.

Advanced color interpolation is intentionally not exposed until ThemeManager can define and
validate color spaces consistently. The initial renderer preserves Skia's current sRGB-compatible
color behavior.

## 9. Future Shape impact

Shapes will be able to reuse `Brush` for fills and, later, stroke paint without depending on a
control. Relative gradient coordinates are resolved against the geometry's paint bounds by the
same adapter. The model does not reference `Control`, Windows, Android, or the Designer. Stroke
caps, joins, dashes, and geometry are outside this stage.

## 10. Target architecture

- `Brush` is mutable, shareable, observable, and platform-neutral through its preferred API.
- `Brush.Opacity` is in the inclusive range 0..1 and affects color alpha at render time.
- `Brush.Transform` is a finite `Matrix3x2`; it prepares translate, scale, rotate, and matrix
  composition without exposing `SKMatrix`.
- `NoBrush` explicitly suppresses a fill; `null` retains its existing fallback-color meaning.
- `SolidColorBrush.PaintColor` is the preferred neutral color property. Legacy `Color` maps to the
  same backing value.
- `GradientBrush` owns `GradientStopCollection`, `SpreadMode`, and a cached stable ordered snapshot.
- `GradientStop` is observable, validates offsets strictly, and exposes a neutral preferred color
  alongside the legacy Skia adapter.
- Linear, radial, and sweep brushes expose neutral points. Existing Skia point properties delegate
  to the same fields.
- Relative coordinates are the only public mapping mode in this stage. Absolute coordinates are
  deferred until the framework has a single documented logical-coordinate contract across normal,
  Designer, export, and off-screen surfaces.
- Radial gradients add a neutral gradient origin while retaining the compatible circular radius.
- A single internal Skia adapter maps spread modes, coordinates, alpha, transforms, and stop
  snapshots to shaders. Bounds-dependent shaders are short-lived and disposed after drawing.

## 11. Migration plan

Existing `BackgroundBrush`, `TextBrush`, `CaptionBackgroundBrush`, `ContentBackgroundBrush`, and
switch brush assignments continue to accept the same brush types. Existing `BackColor` and
`ForeColor` remain fallback colors; an assigned brush takes precedence. Clearing a brush restores
the existing color path. `NoBrush` differs deliberately by painting no fill.

New code should use `PaintColor`, neutral point members, `SpreadMode`, `Opacity`, and `Transform`.
The Designer may continue reading older structured values containing `Color`, `StartPoint`,
`EndPoint`, `Center`, and `Radius`; newly saved values add optional properties with backward-
compatible defaults. Generated code remains valid C# and uses the existing brush hierarchy.

## 12. Potential breaking changes

Changing `GradientStops` from `List<GradientStop>` to `GradientStopCollection` changes the binary
property signature and removes reliance on `List<T>`-specific APIs. It is required to observe all
collection and item mutations. Common source operations (`Add`, collection initializers, indexing,
enumeration, `Remove`, and `Clear`) remain compatible, and `AddRange` is provided.

Out-of-range or non-finite offsets will throw `ArgumentOutOfRangeException` instead of being
silently clamped. This prevents invalid serialized/theme data from producing ambiguous gradients.
The legacy Skia color and point properties remain available, so no deliberate source break is made
for those members.

## 13. Performance risks

Shared mutable brushes can invalidate many controls, which is intentional but can be expensive if
callers mutate every property separately during animation. A future update scope may coalesce
notifications. The stop collection caches a stable offset-ordered snapshot so render frames do not
repeat LINQ sorting. Color and position arrays are still produced for Skia shader creation; shader
creation itself dominates and cannot be eliminated safely for bounds-dependent gradients without a
bounded cache policy.

Text brushes require a temporary alpha layer as before. Animated brush properties rebuild shaders
and should be driven by the future UI-thread animation scheduler, not worker-thread callbacks.

## 14. Native memory and shader cache risks

`SKShader` and `SKPaint` own native resources. The adapter returns explicit shader ownership to a
single rendering scope, which disposes the shader and paint after drawing. Replacing a shader on a
paint never abandons an undisposed shader. No global shader cache is introduced: linear/radial/
sweep shaders depend on current bounds, spread, transform, colors, and offsets, and a shared brush
can be rendered at multiple sizes simultaneously.

Only managed, renderer-neutral stop ordering is cached on the brush. A collection or stop mutation
invalidates that cache before it raises `Changed`. This avoids stale native state and keeps Windows
CPU surfaces, Android GPU/CPU surfaces, the Designer, and tests on the same path.

## 15. Completion criteria

This stage is complete when:

- solid, no-fill, linear, radial, and sweep brushes share one observable hierarchy;
- opacity, stable validated stops, pad/repeat/reflect, relative bounds, radial origin, and neutral
  transforms render through one Skia adapter;
- preferred new brush APIs do not require Skia types, while existing brush code remains usable;
- control, group-box, and switch properties subscribe weakly, invalidate on nested mutation, and
  detach on replacement/disposal;
- application/window/control resource precedence, type replacement, in-place mutation, fallback,
  and garbage collection are tested;
- Designer round trips and generated C# preserve the supported brush contract;
- focused rendering tests cover bounds, alpha, spread, stop edge cases, and shader disposal scopes;
- ControlGallery demonstrates solid, linear, radial, spread, runtime mutation, and a dynamic brush
  resource without changing the default template application;
- shared, Windows, Android, Designer, VSIX, samples, tests, and local packages pass release
  validation; and
- the roadmap marks only paint/gradient hardening complete and recommends UI animation scheduler
  hardening next.
