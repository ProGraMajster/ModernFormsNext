# Architecture guide

[Documentation index](../README.md)

Start with the [framework layers](../architecture.md) and
[platform boundaries](../platform-specific-code.md). The documents below explain how the existing
systems fit together; application-facing examples are linked from the documentation index.

## Implementation notes

- [UI animation scheduler](ui-animation-scheduler.md)
- [Composable animations and interaction effects](../composable-animations.md)
- [Animated layout](animated-layout.md)
- [Layout-aware visual-state metrics](layout-aware-visual-state-metrics.md)
- [Paint and gradients](paint-and-gradients.md)
- [Brush interpolation compatibility](brush-interpolation.md)
- [Android animation runtime](android-animation-runtime.md)
- [Designer/runtime layout parity](designer-runtime-layout-parity.md)
- [Designer document and code-generation pipeline](../designer-architecture.md)

## Architecture decision records

Each ADR carries its own status and scope. Consult that status before treating a design as an
available public API.

- [Dynamic resources](decisions/ADR-Dynamic-Resources.md)
- [Theme system](decisions/ADR-Theme-System.md)
- [Paint and gradient system](decisions/ADR-Paint-And-Gradient-System.md)
- [UI animation scheduler](decisions/ADR-UI-Animation-Scheduler.md)
- [Composable animations and interaction effects](decisions/ADR-Composable-Animations-And-Interaction-Effects.md)
- [Animation platform policy and Designer effects](decisions/ADR-Animation-Platform-Polish.md)

The following records are proposals:

- [Page and navigation architecture](decisions/ADR-Page-Navigation-Architecture.md)
- [Localization system](decisions/ADR-Localization-System.md)
- [Optional feature packages](decisions/ADR-Optional-Feature-Packages.md)

See the [roadmap](../roadmap/ModernFormsNext-Framework-Roadmap.md) for planned work and the
[development index](../development/README.md) for implementation audits and validation reports.
