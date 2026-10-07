# Documentation

Start here for the documentation in this checkout. Windows is the primary runtime target;
Android is experimental. Use [platform status](platform-specific-features.md) and
[known limitations](known-limitations.md) to check the scope of a feature.

## Getting started

- [Installation, packages, templates, and Visual Studio setup](installation.md)
- [Your first application](getting-started.md)
- [Samples and current Windows screenshots](samples.md)
- [Application lifecycle and activation](application-lifecycle.md)
- [Windows system notifications](system-notifications.md)
- [System notification cross-platform contract review](design/system-notifications-cross-platform.md) — Windows implementation; future Android, macOS, iOS and Linux mappings.
- [Platform support](platform-specific-features.md)
- [Known limitations](known-limitations.md)

## Controls, input, and data

- [UserControls](user-controls.md)
- [Data binding](data-binding.md)
- [Commands, shortcuts, and input routing](commands.md)
- [Text input and composition](text-input.md)
- [RichTextBox](richtextbox.md)
- [Markdown and document viewing](markdown.md)
- [MarkdownEditor](markdown-editor.md)
- [Tooltips](tooltips.md)

## Appearance, rendering, and animation

- [Styling](styling.md)
- [Dynamic resources](dynamic-resources.md)
- [Themes](themes.md) and [theme JSON schema](theme-json-schema.md)
- [Paint and gradients](paint-and-gradients.md)
- [Shapes and vector geometry](shapes-and-vector-geometry.md)
- [High DPI and coordinate ownership](high-dpi.md)
- [UI animations](animations.md)
- [Composable animations and interaction effects](composable-animations.md)

## Designer

- [Designer architecture and document workflow](designer-architecture.md)
- [Visual Studio host](visual-studio-designer-host.md)
- [Transactions and undo/redo](designer-transactions-and-undo.md)
- [Copy, cut, paste, and duplicate](designer-copy-paste.md)
- [Autosave, recovery, and external changes](designer-autosave-and-recovery.md)
- [Animation and interaction-effect definitions](designer-animation-effects.md)

## Accessibility

- [Shared semantic model](accessibility/semantic-model.md)
- [Windows UI Automation backend](accessibility/windows-ui-automation.md)
- [Accessible editor text](accessibility-text.md)
- [Links and numeric controls](accessibility/current-controls.md)
- [Grids and calendars](accessibility/grids-and-calendars.md)
- [Viewports and scrolling](accessibility/scroll-viewports.md)
- [Preferences and authored themes](accessibility/preferences.md)
- [Snapshot diagnostics and Designer metadata](accessibility/diagnostics-and-designer.md)

## Android and shared applications

- [Android support matrix and requirements](platforms/android.md)
- [Backend implementation](android-backend.md)
- [Development environment](android-development.md)
- [ADB commands](android-adb.md)
- [Cross-platform sample](cross-platform-sample.md)
- [Manifests and permissions](android-permissions.md)
- [Hardware input and shortcuts](android-hardware-input.md)
- [Accessibility backend](android-accessibility.md)
- [Accessibility native checks](accessibility/android-phase4-validation.md)
- [Release validation matrix](android-release-validation.md)

## Testing, automation, and diagnostics

- [Deterministic headless TestHost](testing/testhost.md)
- [Application test template](testing/testhost-template.md)
- [In-process semantic automation](automation.md)
- [Windows live automation bridge](automation-live-bridge.md)
- [Performance diagnostics](performance-diagnostics.md)

## Architecture and contributing

- [Architecture guide and decision records](architecture/README.md)
- [CI and release gates](ci.md)
- [Repository validation inputs](repository-validation.md)
- [Development reports and audits](development/README.md)
- [Framework roadmap](roadmap/ModernFormsNext-Framework-Roadmap.md)
- [Screenshot provenance and refresh guidance](screenshots.md)

## Releases and migration

- [Changelog](../CHANGELOG.md)
- Release notes: [1.11.1](1.11.1-release-notes.md), [1.11.0](1.11.0-release-notes.md),
  [1.10.0](1.10.0-release-notes.md), [1.9.0](1.9.0-release-notes.md)
- Migration guides: [1.9.0 to 1.10.0](migrations/1.9.0-to-1.10.0.md),
  [1.8.0 to 1.9.0](migrations/1.8.0-to-1.9.0.md)
- [Historical 1.9.0 theme-system scope](1.9.0-theme-system.md)
- [Release process](../RELEASING.md)
- [Versioned offline documentation and API reference](releasing/versioned-documentation-artifacts.md)

The API reference is generated from the matching assemblies and XML comments in the versioned
offline documentation bundles. Reports and release notes describe their recorded revision;
proposed architecture and roadmap items do not imply implemented support.

## Documentation layout

User guides live in this directory and in the `accessibility/`, `platforms/`, and `testing/`
subdirectories. Architecture explanations and ADRs live in `architecture/`; development evidence
is indexed under `development/`; migration and release instructions live in `migrations/` and
`releasing/`. Existing document and image paths are retained so incoming links remain usable.

The tracked offline-site entry points are in `docs-site/`. When adding a guide, update this index
and the relevant group in `docs-site/toc.yml`. Link historical reports from the development index
and keep their date, source revision, and validation limits explicit.
