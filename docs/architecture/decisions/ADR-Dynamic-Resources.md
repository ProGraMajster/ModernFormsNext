# ADR: Dynamic resources

- Status: Accepted and implemented as a foundation
- Date: 2026-07-17
- Scope: `ModernFormsNext`

## Context

Themes, localization, page-level overrides, and future controls need values that can change after a
control tree has been created. ModernFormsNext currently uses ordinary CLR properties, a compact
internal `PropertyStore`, `ControlStyle`, existing property setters, and explicit invalidation/layout
calls. It does not have dependency properties and must not gain a competing property engine merely
to support resources.

Controls are deliberately lightweight. Resource dictionaries and reference collections therefore
must remain lazy. A source must not keep a removed or otherwise unreachable control alive.

## Problem

Provide arbitrary typed resources with application, window, ancestor-control, and control scopes;
nearest-scope fallback; runtime updates; safe subscription lifetime; and targeted property updates.
The design must continue to use normal property setters so each existing control retains ownership
of its render/layout side effects.

## Options considered

1. Introduce dependency properties with registered metadata, default values, inheritance, and
   invalidation flags.
2. Reuse the existing data-binding engine with a synthetic resource data source.
3. Bind a resource key to a public writable CLR property by name and invoke its existing setter.
4. Broadcast a global theme/resource event and invalidate every open form.

## Decision

Use `ResourceDictionary` at `Application.Resources`, `WindowBase.Resources`, and
`Control.Resources`. `Control.TryFindResource` resolves the nearest value in that order. A call to
`Control.SetResourceReference(propertyName, key)` captures the current property value as final
fallback and binds the resource to a public writable non-indexed CLR property.

References are registered in a process-local weak hub grouped by resource key. A dictionary change
only visits listeners for that key. Each listener first verifies that the changed dictionary is in
its target's current hierarchy, resolves the effective value again, and invokes the property setter
only when the effective value changed. Reparenting refreshes the affected subtree.

Initial type errors fail fast. Later incompatible replacements restore the captured fallback and
raise `ResourceReferenceFailed`. Values are not implicitly converted. Disposal unregisters eagerly;
weak registrations prevent a missed disposal from retaining a control.

## Consequences

- Existing setters remain the only authority for invalidation and layout.
- Controls without local resources or references pay only two static `PropertyStore` keys, not per-
  instance dictionary fields.
- Theme and localization systems can share one update mechanism and arbitrary value model.
- The CLR-name API is compatible with the existing data-binding style but depends on reflection
  metadata. A future source generator or property descriptor cache may optimize AOT/trimming without
  changing resource lookup semantics.
- Direct property assignment does not automatically clear a reference; callers must use
  `ClearResourceReference`.
- Updates run synchronously on the mutation thread, so live resources have UI-thread affinity.
- A resource object mutated in place does not generate a dictionary change. Replace the value or add
  a future observable resource type when in-place updates are required.

## Rejected alternatives

- Dependency properties were rejected because they would duplicate the current CLR-property,
  `PropertyStore`, data-binding, and invalidation architecture before the framework has a broader
  need for such a breaking foundation.
- Synthetic data binding was rejected because resource scope and fallback are visual-tree concepts,
  while the current binding engine models data sources and member paths.
- Whole-application invalidation was rejected because it performs unnecessary work and cannot
  distinguish layout-affecting setters from visual-only setters.
