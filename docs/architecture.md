# Architecture

ModernFormsNext is designed as a layered, extensible UI framework.

The goal is to separate rendering, platform integration, and UI logic while keeping a simple, WinForms-like API.

---

## Overview

ModernFormsNext consists of several main layers:

```text
Application (User Code)
        ↓
ModernFormsNext (UI Framework)
        ↓
WindowKit (Platform Abstraction)
        ↓
Platform Backend (supported Windows, experimental Android)
        ↓
Rendering (SkiaSharp)
```

---

## UI Layer

This is the layer application developers interact with directly.

Examples:

- `Form`
- `Control`
- `Button`
- `TextBox`

Responsibilities:

- control hierarchy
- layout
- state
- events
- user-facing API

---

## WindowKit

WindowKit is responsible for platform abstraction.

Responsibilities:

- window creation
- keyboard and mouse input
- clipboard access
- native integration points
- backend communication

This layer allows the framework to keep a consistent API while delegating platform-specific behavior to dedicated implementations.

---

## Platform Backends

Each platform can provide its own backend. Windows is the supported full runtime backend; Android
is an experimental shared-control host. The repository does not currently provide supported Linux
or macOS application backends.

Current implementations:

- supported Windows backend;
- experimental Android backend foundation and Skia control surface.

Responsibilities:

- native window management
- OS-level input translation
- system services
- backend-specific integration

---

## Rendering

Rendering is handled using SkiaSharp.

Responsibilities:

- drawing controls
- text rendering
- shapes and borders
- visual styling
- invalidation and redraw flow

This keeps rendering separated from platform-specific code as much as possible.

## Themes and resources

`ThemeManager` resolves validated theme definitions into immutable snapshots and publishes cloned
working values through the existing dynamic-resource lookup. Application/window/control overrides
remain higher precedence than manager-owned defaults. Compatible transitions use the one shared UI
animation scheduler; platform theme and reduced-motion discovery stay behind backend services. See
[Themes](themes.md) and the [ThemeManager ADR](architecture/decisions/ADR-Theme-System.md).

---

## Event Flow

Typical event flow:

1. The operating system receives input.
2. The platform backend captures and translates it.
3. WindowKit forwards the event into the framework.
4. The appropriate control processes the event.
5. The UI updates state.
6. The renderer redraws affected areas.

Keyboard input keeps physical key events separate from resulting text. Controls use `KeyDown` and
`KeyUp` for navigation, special keys, and shortcuts, while printable and composed text is delivered
through the platform `TextInput`/IME path. Backends preserve `AltGraph` explicitly when a platform
also reports synthetic Control and Alt flags, so shared controls can distinguish international text
entry from Ctrl shortcuts without platform-specific checks.

---

## Extensibility

ModernFormsNext is designed to support:

- custom controls
- rendering improvements
- platform-specific services
- future architecture changes

---

## Future Direction

Planned and possible future areas include:

- improved platform support
- more advanced rendering features
- additional modern controls
- deeper customization of platform services

See [Known limitations](known-limitations.md) for the current platform, rendering, and tooling
boundaries rather than treating possible future backends as supported implementations.
