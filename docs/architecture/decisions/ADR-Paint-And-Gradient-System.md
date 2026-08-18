# ADR: Evolve Brush as the canonical paint value

- Status: Accepted; implemented
- Date: 2026-07-19
- Scope: `ModernFormsNext.Drawing`, control brush properties, dynamic resources, and Skia rendering

Implementation note (2026-08-18): ThemeManager, strict theme JSON, brush interpolation, and the
1.10.0 Shape/Geometry system now consume this completed paint foundation. Historical references to
future downstream consumers describe the stage boundary, not their current repository status.

## Context

ModernFormsNext 1.8.0 already exposes a public `Brush` hierarchy for solid, linear, radial, sweep,
and glass fills. Controls, ControlGallery, the Visual Studio Designer, `.mfdesign` serialization,
and C# generation use those types. The hierarchy is nevertheless Skia-coupled, does not notify
when mutable values change, stores gradient stops in a raw list, and supports only clamp spread.

The roadmap needs a reusable visual value for ThemeManager, dynamic resources, future shapes,
charts, control backgrounds, borders, and text. It must remain shared between Windows and the
experimental Android backend and must not introduce another rendering or property system.

## Problem

Choose whether the framework should replace `Brush` with `Paint`, keep two independent models, or
harden the existing hierarchy. The result must support runtime mutation and targeted invalidation,
retain color-based compatibility, centralize Skia shader ownership, and provide a preferred public
surface that is not defined in terms of Skia types.

## Options considered

1. Replace all existing brushes and control properties with a new `Paint` hierarchy.
2. Keep `Brush` for existing controls and add an independent `Paint` hierarchy for themes/shapes.
3. Make `Paint` a new base class and adapt every old brush into parallel new paint types.
4. Evolve `Brush` as the single model, add neutral preferred members and mutation notification,
   and retain existing Skia members only as compatibility adapters to the same backing values.
5. Make brushes immutable and require complete replacement for every change.

## Decision

Use option 4. `Brush` remains the canonical framework paint value. It becomes observable and
shareable, with opacity and a platform-neutral transform. Gradients gain an observable typed stop
collection and pad/repeat/reflect spread. Linear, radial, and sweep coordinates gain neutral .NET
properties. Existing `SKColor` and `SKPoint` properties remain adapters backed by the same values;
they do not create a second source of truth.

Controls subscribe to assigned brushes through weak, reference-counted invalidation subscriptions.
Dynamic-resource replacement continues to invoke ordinary property setters, while mutation inside
a shared brush raises its own `Changed` event. The shared Skia adapter creates bounds-specific
shaders, maps spread modes, and disposes every native shader after the draw. It caches only a
managed stable ordering of gradient stops.

`null` keeps its established meaning of using a color fallback. A distinct no-fill brush represents
"paint nothing". Relative coordinates remain the supported mapping mode; an absolute mode is
deferred until all framework paint surfaces share a documented logical-unit contract. JSON field
names are documented for future ThemeManager work, but no partial theme serializer is introduced.

## Consequences

- Existing control property names, brush types, color fallbacks, Designer documents, and common
  source code continue to work.
- New theme/shape code can use the same values as controls instead of converting between `Paint`
  and `Brush` objects.
- Preferred new values use `System.Drawing.Color`, `PointF`, and `Matrix3x2`; existing public Skia
  adapters cannot be removed before a separately planned breaking release.
- In-place mutation invalidates every live control sharing the brush, so mutations retain UI-thread
  affinity and future animation must use the UI dispatcher.
- An observable collection changes the binary type of `GradientStops`. This is the smallest
  necessary compatibility cost for reliable item and collection mutation.
- Strict offset validation surfaces bad theme/serialized data early rather than silently clamping.
- Bounds-specific shader allocation remains per draw. Avoiding it safely requires a later bounded
  cache informed by rendering diagnostics and animation workloads.
- Windows, Android, the Designer, and off-screen tests share all brush math and shader construction.

## Rejected alternatives

- Replacing `Brush` would break controls, generated code, Designer documents, and users for a naming
  preference rather than a missing capability.
- Independent `Paint` and `Brush` models would duplicate gradients, serialization, resources,
  invalidation, adapters, and precedence rules.
- A new `Paint` base with parallel derived types would still leave two public construction paths
  and force controls to accept a wider base solely for compatibility.
- Immutable brushes make sharing safe but require replacing an entire resource to animate one
  color or stop. That conflicts with targeted live theme updates and the requested runtime model.
