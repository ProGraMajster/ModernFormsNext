# ADR: ThemeManager built on dynamic resources and the shared scheduler

- **Status:** Accepted
- **Date:** 2026-07-20

## Context

ModernFormsNext already has CLR properties backed by `PropertyStore`, `ControlStyle`, hierarchical
dynamic resources, observable solid/gradient/glass brushes, the static `Theme` compatibility API,
and one dispatcher-aware animation scheduler. A theme system must extend those foundations without
adding dependency properties, XAML, native controls, a second resource tree, or another timer.

The static `Theme` property bag cannot by itself express custom tokens, inheritance, validation,
versioned JSON, atomic switching, or local overrides. Publishing theme values directly into
`Application.Resources` would also overwrite application-owned entries and make the application
scope indistinguishable from the framework default.

## Decision

### Lifetime and threading

`ThemeManager.Current` is a lazily-created application-lifetime singleton. A definition is cloned
at the request boundary. Validation, inheritance resolution, and immutable snapshot construction
may run on the caller thread. The resource/legacy-theme commit, rollback, transition callbacks,
and public events run through the UI dispatcher. Requests are thread-safe, the most recent queued
request wins, and user callbacks are never invoked while a ThemeManager lock is held.

Internal dispatcher, environment, legacy-store, clock, tick-source, and lifecycle seams keep the
pipeline deterministic in headless tests. The manager does not create a worker, watcher, or timer.

### Model and inheritance

`ThemeDefinition` is the mutable authoring model. It contains identity and metadata, one optional
base-theme ID, variant intent, typed category dictionaries, and a closed custom-resource union.
Registered base themes are copied. Resolution walks one base chain from root to leaf, enforces a
depth limit, rejects a missing base or cycle, and lets the leaf override selected tokens. Category
namespaces are independent (`Spacing.Small` and `Corner.Small` can coexist). A custom resource may
not change its allow-listed value kind across inheritance.

The result is `ThemeResolvedSnapshot`. Scalar and immutable values are exposed through read-only
collections. The source definition and base definitions are never mutated. `System` is resolved to
`Light` or `Dark` by a platform provider; an explicit Light/Dark apply fallback is used when the
provider is unavailable.

### Brush ownership

Brushes are mutable so that their existing `Changed` event can invalidate current consumers. The
following clone boundaries prevent accidental sharing:

1. registration and `ThemeDefinition.Clone()` deep-clone supported brushes;
2. inheritance resolution clones into private snapshot storage;
3. snapshot `Get`/`TryGet` returns a new brush clone;
4. publication creates a separate applied working clone;
5. a transition mutates that one working clone in place for all frames.

Only exact built-in `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`,
`SweepGradientBrush`, `GlassBrush`, and `NoBrush` types are accepted by themes. This is deliberate:
unknown subclasses could contain state that cannot be cloned or safely serialized. A transition
does not allocate a new brush per frame; it captures source/target snapshots once and updates the
working brush, preserving `Brush.Changed` notifications.

### Resource precedence

Theme values use stable keys in the form `Theme.{Category}.{Name}` and live in a dedicated,
manager-owned dictionary. Lookup from a control is:

1. the control's resources;
2. nearest parent resources, walking outward;
3. the owning window's resources;
4. `Application.Resources`;
5. `Application.ThemeResources`;
6. the CLR value captured when a dynamic reference was established.

This preserves existing application overrides and prevents a theme apply from deleting unrelated
application resources. Existing dynamic references receive normal CLR setter calls without
recreating controls. The dictionary state is swapped under one lock before deferred keyed
notifications are published, so every observer sees the complete new resource snapshot. Static
resource values remain ordinary assigned CLR values and do not follow later changes.

### Atomic apply and rollback

An apply performs:

1. definition clone and option validation;
2. definition and value validation;
3. inheritance resolution;
4. immutable target snapshot construction;
5. target resource and legacy compatibility projection preparation;
6. `ThemeChanging` on the UI thread;
7. one locked resource replacement plus the static `Theme` compatibility projection;
8. deferred dynamic-resource notification and one legacy `ThemeChanged` propagation;
9. optional transition start through `AnimationScheduler` owner/key replacement;
10. `ThemeChanged`, which means committed rather than animation-complete.

If a commit notification or event handler throws, the previous resource dictionary, active
definition/snapshot, and static `Theme` values are restored before a failed result and
`ThemeApplyFailed` are reported. A rollback notification is published so controls return to the
old effective values. Validation failure never enters the commit. `ThemeTransitionCompleted`
and `ThemeTransitionHandle.Completion` separately report transition completion, cancellation, or
fault.

### Animated transitions

Theme transitions use only `AnimationScheduler.Default`, with ThemeManager as owner and one stable
key. A newer switch replaces the older scheduler entry. Explicit cancellation snaps the committed
theme to its target; replacement keeps the current interpolated values and starts the next
transition from there. `AnimationReplacementMode.IgnoreNew` cancels the newer apply and allows the
current transition to continue. A stale public handle can only cancel its own transition.

Changed colors, closed custom numeric resources, solid brushes, and compatible linear/radial/sweep
gradients animate. Compatible gradients include opacity, transform, geometry, stop colors, and
stop offsets; discrete spread mode takes the target at completion. Different concrete brush types,
unsupported animation brush types, and different gradient-stop counts switch immediately.
Spacing, padding, sizing, corners, border thickness, typography, and other layout-affecting values
switch at commit to avoid layout churn on every frame.

The platform reduced-motion preference can suppress transition creation, while the scheduler's
central `AnimationsEnabled`, `ReducedMotion`, and duration policy remains authoritative and can
complete active work immediately. Scheduler lifecycle pause/resume excludes background time, so
Android backgrounding or a missing host causes no time jump. When the transition ends, no handle or
tick-source work remains.

### JSON trust boundary

`ThemeJsonSerializer` uses `System.Text.Json` with schema version 1. It supports string, stream, and
explicit file APIs, including asynchronous stream/file operations. Serialization is deterministic
for dictionaries and optionally indented. Deserialization rejects unknown and duplicate
properties, comments, trailing commas, unsupported schema versions, malformed enums/colors/keys,
non-finite values, arbitrary CLR type names, and unrecognized Brush/resource discriminators.

Defaults limit UTF-8 input to 1 MiB, JSON depth to 64, combined tokens to 4096, gradient stops per
brush to 64, strings to 512 characters, and inheritance to 16 definitions. Theme JSON never loads
a referenced base or neighboring file. Base IDs are resolved only against definitions explicitly
registered with the manager. An internal allow-listed schema-migration interface exists for a
future release; no migration is currently registered, so every version except 1 is rejected.

### Compatibility and Designer

The public static `Theme` API remains. `Theme.SetBuiltInTheme` delegates to ThemeManager with an
immediate apply, and semantic tokens project to the legacy color/font names. Existing code that
sets individual static Theme properties remains valid, although a later manager apply replaces the
projected theme-owned values.

The Designer gets deterministic fallback behavior. It does not query runtime platform services or
start transitions, file watchers, or background services. Complex authoring collections are hidden
from the generic property grid and from automatic designer serialization; authors use code or the
stable JSON serializer. No theme file hot reload is implemented in this stage.

### Platform boundaries

Windows registers `IPlatformThemeSettings`, reading the current application light/dark preference
and client-animation setting on demand. ThemeManager still requires an explicit apply to follow a
later OS theme change; automatic Windows change notifications are not implemented.

The Android backend remains experimental. It does not yet register a platform theme provider, so
`System` uses the explicit apply fallback. Theme values, stream loading, runtime switching, the
shared scheduler, and lifecycle pause/resume remain platform-neutral. Device/emulator validation is
still required before claiming Android runtime parity.

## Consequences

- Existing resource, Brush, style, invalidation, dispatcher, and lifecycle systems remain the
  source of truth.
- Applications gain custom tokens and local overrides without a parallel property system.
- A committed target snapshot may coexist briefly with interpolated working resources; the
  transition handle makes that state explicit.
- Layout values deliberately do not animate.
- Typography line height and letter spacing are stored and serialized but are only consumed by
  controls/renderers that already support them; the global text renderer does not claim support.
- Shadows and file hot reload are deferred because the current renderer and lifecycle ownership do
  not yet provide a complete shared contract.

## Rejected alternatives

- Expanding only the static Theme property bag cannot provide custom typed tokens or safe JSON.
- Publishing into `Application.Resources` would erase the distinction between application
  overrides and theme defaults.
- Per-control ThemeManager subscriptions would duplicate dynamic-resource lookup and tree walks.
- A transition timer or manager-specific animation loop would duplicate the shared scheduler.
- Reflection-based polymorphic JSON would admit arbitrary types across an untrusted input boundary.
- Replacing `ControlStyle`, renderers, or CLR properties with a foreign styling/dependency-property
  system would be a broad breaking redesign.
