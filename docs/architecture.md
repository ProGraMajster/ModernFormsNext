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
Platform Backend (Windows, Linux, etc.)
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

Each supported platform can provide its own backend.

Examples:

- Windows backend
- Linux backend
- macOS backend

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

---

## Event Flow

Typical event flow:

1. The operating system receives input.
2. The platform backend captures and translates it.
3. WindowKit forwards the event into the framework.
4. The appropriate control processes the event.
5. The UI updates state.
6. The renderer redraws affected areas.

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
- animation systems
- additional modern controls
- deeper customization of platform services
