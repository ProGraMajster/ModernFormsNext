# Draft release notes: composable animations and interaction effects

This is an unreleased, version-neutral draft. It does not change package or assembly versions and
does not authorize publication.

## Highlights

- Public `AnimationDefinition`, `AnimationRun`, `AnimationContext`, and typed
  `PropertyAnimation<T>` APIs build on the existing shared `AnimationScheduler`.
- `Animation.Sequence`, `Animation.Parallel`, `AnimationTimeline`, and scheduler-backed delay
  compose work without new timers or blocking the UI thread.
- `KeyframeAnimation<T>` supports bounded typed keyframes, segment easing, explicit duplicate
  policies, exact endpoints, and deterministic `Sample`/`Seek`.
- Finite repeat, required-cancellation infinite repeat, and auto-reverse reuse the existing
  monotonic clock, lifecycle pause, owner/key replacement, and reduced-motion policy.
- Controls now resolve `Normal`, `Hover`, `Pressed`, `Focused`, and `Disabled` state styles with
  latest-state-wins transitions for compatible colors, Brushes, opacity, and render transforms.
- Reusable interaction effects add bounded ripple and press-scale feedback for mouse, keyboard,
  and experimental Android touch input.
- The common render order is background, effects below content, content, effects above content,
  then the focus ring. Built-in clipping respects control bounds and corner radius.
- One application visual-invalidation batch wraps every scheduler tick, coalescing repaint per
  window without forcing layout for visual-only frames.
- Existing `FadeToAsync`, `TranslateToAsync`, `ScaleToAsync`, and `RotateToAsync` helpers remain
  source-compatible.

## Quality and compatibility

Deterministic tests use the manual clock, tick source, and dispatcher. They cover composition,
fault/cancellation propagation, repeat and idle behavior, keyframes, all visual states, theme
refresh, ripple/press input, clipping, resize, multi-touch, Designer behavior, disposal, render
ordering, multiple windows, repaint batching, and absence of layout during visual frames.

The new **Animations and Interaction Effects** ControlGallery page provides manual controls for
Start, Cancel, Rapid x5, reduced motion, animations disabled, diagnostics, Replace, IgnoreNew,
composition, keyframes, repeat, auto-reverse, custom animation, and custom interpolation.

## Platform status and limitations

Windows remains the primary runtime target. Android touch integration is experimental and requires
device/emulator validation for frame pacing, background/foreground transitions, multi-touch
rendering, and orientation changes. Native reduced-motion discovery is not yet automatic.
Visual-state layout metrics switch immediately rather than interpolate, incompatible Brush
structures switch discretely, ripple currently uses oldest-first eviction, and complex
interaction-effect collections remain code-first in the Designer.

See [UI animations](animations.md) and the
[composable-animation ADR](architecture/decisions/ADR-Composable-Animations-And-Interaction-Effects.md).
