# Composable animations and interaction effects in ModernFormsNext 1.9.0

This document records the composable animation and interaction-effect scope included in
ModernFormsNext 1.9.0. It does not change the release status or authorize publication.

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
- Ripple overflow is explicit and deterministic: remove oldest, remove newest, ignore the new wave,
  or replace all active waves. The existing eviction property remains source-compatible.
- Windows observes the native client-area animation preference without polling. Experimental
  Android reads the global animator and transition scales on startup/foreground entry.
- The Designer Property Grid has detached, scheduler-free editors for ordered interaction effects,
  layout transitions, and visual-state transitions with stable `.mfdesign` and generated-code
  round trips. Explicitly attributed project effects are discovered from source without loading
  the application assembly.
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
Start, Cancel, rapid animation replacement, application and native reduced motion, animations
disabled, platform diagnostics, all ripple overflow policies, composition, keyframes, repeat,
auto-reverse, custom animation, and custom interpolation.

## Platform status and limitations

Windows remains the primary runtime target. Android touch integration is experimental and requires
device/emulator validation for frame pacing, background/foreground transitions, multi-touch
rendering, platform setting changes, and orientation changes. Android refreshes platform animation
scales on foreground entry rather than maintaining a live settings observer. Visual-state layout
metrics switch immediately rather than interpolate and incompatible Brush structures switch
discretely. Designer documents serialize only built-in easing identifiers; custom delegate easing
and general `AnimationDefinition` activation remain code-first. The Designer still has no global
undo/redo stack and does not execute or preview project effect code.

See [UI animations](animations.md) and the
[composable-animation ADR](architecture/decisions/ADR-Composable-Animations-And-Interaction-Effects.md),
plus the [animation platform polish ADR](architecture/decisions/ADR-Animation-Platform-Polish.md).
Designer serialization and isolation are detailed in
[Designer animation and interaction-effect definitions](designer-animation-effects.md).
