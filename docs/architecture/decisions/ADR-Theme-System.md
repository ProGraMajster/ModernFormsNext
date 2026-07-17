# ADR: Theme system built on dynamic resources

- Status: Proposed
- Date: 2026-07-17

## Context

The current static `Theme` stores mostly colors plus fonts and sizes in a concurrent dictionary.
`ControlStyle` subscribes default styles to a global `ThemeChanged` event, and forms propagate a
coarse theme notification through their control trees. ModernFormsNext already has solid and
gradient brushes, style inheritance, renderers, and simple control animations, but no serializable
theme object, dynamic resource identity, state-style model, system theme service, or validation.

## Problem

Themes must represent colors, paints, gradients, typography, spacing, sizing, radii, borders,
shadows, control/state styles, animation values, easing, and platform overrides. They must load from
JSON, inherit, validate, switch at runtime, optionally transition, follow light/dark/system state,
and support application/window/control overrides without rebuilding the UI.

## Options considered

1. Expand the static `Theme` class with one property and event per token.
2. Make every control subscribe directly to a `ThemeManager` event and manually copy values.
3. Materialize a validated `ThemeDefinition` into dynamic resources and let controls reference
   stable semantic keys.
4. Replace `ControlStyle` with an imported WPF/MAUI/Avalonia styling engine.

## Decision

Introduce a `ThemeManager` that owns an immutable validated `ThemeDefinition` and publishes its
tokens into `Application.Resources`. Stable keys such as `Color.Accent`,
`Control.Button.Background.Normal`, and `Motion.Duration.Short` are the public contract. Theme
inheritance is resolved before publication. Platform-qualified values are selected through a small
platform-neutral environment contract implemented by backends.

`ControlStyle` remains the renderer-facing style object. State-style records and typed theme tokens
will project into it or be referenced by control CLR properties; they will not replace renderers or
introduce native controls. Existing static `Theme` properties remain as a compatibility facade and
delegate to the active theme/resource values during a migration period.

System light/dark notification belongs behind a WindowKit platform service. Theme transitions use a
UI-dispatcher animation scheduler and typed interpolators. Non-interpolable values switch at a
defined transition boundary. Hot reload is a development service that validates a complete new
definition before atomically publishing it.

## Consequences

- Existing controls can migrate incrementally without a public API break.
- Resource overrides naturally provide application/window/control precedence.
- JSON parsing and validation remain separate from rendering and can be tested headlessly.
- A typed token catalog or generated keys is needed to avoid string drift.
- The current animation loop applies callbacks after `ConfigureAwait(false)` and is not yet a safe
  theme-transition scheduler; dispatcher affinity must be fixed before animated theme switching.
- Existing strong static theme subscriptions must be replaced or carefully unwired to prevent
  long-lived default style graphs from retaining transient objects.

## Rejected alternatives

- Extending the static property bag cannot express inheritance, validation, state styles, or local
  override scopes cleanly.
- Per-control theme event handlers duplicate resource lookup and tend toward global invalidation.
- Replacing the styling architecture would be a broad, breaking rewrite and would discard working
  renderer and `ControlStyle` behavior.
