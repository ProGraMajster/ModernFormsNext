# Samples

ModernFormsNext includes several sample applications that demonstrate how to use the framework.

These examples showcase layout, controls, rendering, and real-world UI scenarios.

---

## Explorer

A file explorer-style application demonstrating:

- layout system
- navigation
- file-like UI structure

### Run

```bash
cd samples/Explorer
dotnet run
```

---

## Outlaw

An Outlook-style application demonstrating:

- complex layouts
- toolbars and menus
- list + detail UI pattern

### Run

```bash
cd samples/Outlaw
dotnet run
```

---

## ControlGallery

The **Animations and Interaction Effects** page demonstrates pointer- and center-origin ripple,
rapid bounded waves, press scale, hover/focus/disabled transitions, sequence, parallel, timeline,
keyframes, repeat, auto-reverse, custom definitions and interpolators, cancellation, replacement
policies, reduced motion, animations disabled, and scheduler/ripple diagnostics. It is opt-in and
restores the animation policy when unloaded.

The `MarkdownEditor` page demonstrates Editor, Preview, and Split modes, the public command
toolbar, Ctrl+K, hosted link and image request dialogs built only from ModernFormsNext controls,
preview link forwarding, and optional proportional scroll synchronization. Its source includes
editable links and images, Unicode, local and HTTP image sources, and enough content for manual
scroll testing. Its hosted image dialog can insert a reference unchanged or choose a local raster
image with the ModernFormsNext file picker and copy it into `MarkdownEditorAssets` beside the
sample output. Collision handling is selectable and no source-repository directory is modified.

A showcase of available controls and components.

Includes:

- buttons
- inputs
- checked and selectable list controls
- rich text editing controls
- Markdown source editing with a grouped toolbar, list-aware Enter/Tab/Backspace behavior, AltGr-safe shortcuts, undo/redo, and native split preview
- menus
- tooltips
- containers
- layout elements

### Run

```bash
cd samples/ControlGallery
dotnet run
```

---

## ModernFormsNext.DemoApp

The template/reference application for the generated ModernFormsNext app experience.

Use this sample to validate that the default application structure remains clean, minimal, beginner-friendly, and aligned with `ModernFormsNext.Templates`. Do not use it as a playground for random controls or visual regression experiments.

### Run

```bash
cd samples/ModernFormsNext.DemoApp
dotnet run
```

---

## ModernFormsNext.CrossPlatform.Sample

One MAUI-like (but non-MAUI), multi-target project organized around shared `App` and `MainPage`
files plus thin `Platforms/Windows` and `Platforms/Android` hosts. Both targets use the same real
ModernFormsNext control tree. Android currently reaches that tree through the transitional
`SkiaControlSurface` rather than a complete Android `IWindowingPlatform`.

```powershell
.\scripts\windows\Run-CrossPlatformSample.ps1
.\scripts\android\Run-CrossPlatformSample.ps1 -DeviceId <serial>
```

See [Cross-platform sample](cross-platform-sample.md).

---

## Notes

- Samples are the best way to learn ModernFormsNext
- They reflect current framework capabilities
- `ControlGallery` is the preferred place for control demos and visual/manual regression checks
- `ModernFormsNext.DemoApp` represents the generated template application and should stay minimal
- `ModernFormsNext.CrossPlatform.Sample` validates the shared Windows/Android application pipeline
- Some features may still be experimental

---

## Screenshots

### Explorer (Windows)
![Explorer Windows](docs/explorer-windows.png)

### Explorer (Linux)
![Explorer Linux](docs/explorer-ubuntu.png)

### Explorer (macOS)
![Explorer macOS](docs/explorer-osx.png)

### Outlaw
![Outlaw](docs/outlaw-windows.png)

### ControlGallery
![ControlGallery](docs/controlgallery-windows.png)
