# Changelog

All notable ModernFormsNext changes are documented in this file.

ModernFormsNext follows semantic versioning. Git tags use a `v` prefix, while NuGet package versions do not.

## [1.6.0] - 2026-06-27

Compared with [1.5.0], this release adds more WinForms-compatible controls and dialog APIs while expanding the framework's custom rendering surface. It introduces `GroupBox`, a highly customizable `Switch`, printing dialogs and print preview infrastructure, gradient text rendering, and template/package hygiene updates for the next published package set.

Published packages:

- `ModernFormsNext`
- `ModernFormsNext.Templates`
- `ModernFormsNext.WindowKit`
- `ModernFormsNext.WindowKit.Backend`
- `ModernFormsNext.WindowKit.Backend.Windows`

### Added

- Added `GroupBox`, a SkiaSharp-rendered container control for visually grouping related child controls.
- Added `GroupBox` caption customization, including caption font size, foreground color, background color, background brush/gradient, border color, border width, border radius, content background color, content background brush/gradient, border styling, AutoSize support, layout-aware display rectangle behavior, and accessibility exposure.
- Added `Switch`, a fully custom-rendered switch control with two-state Boolean mode and three-position `-1`, `0`, `1` mode.
- Added `Switch` activation and interaction options, including automatic toggling, drag support, drag-time value updates, pointer-position activation, cycle activation, keyboard activation, `IsToggled`, `Value`, `Toggled`, and `ValueChanged`.
- Added `Switch` visual customization for track color/brush/gradient, thumb color/brush/gradient, borders, corner radii, thumb size/inset, built-in icons, custom bitmap icons, icon colors, animation duration, animation speed, and easing.
- Added `Switch` renderer support for smoother rounded fills/borders and direct color interpolation for three-state transitions so `-1 -> 1` animations no longer flash through the neutral state color.
- Added gradient text rendering support through `Control.TextBrush` and renderer plumbing so controls can draw text with solid, linear, radial, sweep, or glass brush content where supported.
- Added printing dialog compatibility APIs, including `PrintDocument`, `PrintDialog`, `PageSetupDialog`, `PrintPreviewDialog`, `PrintPreviewControl`, `PrinterSettings`, `PageSettings`, paper/printer model types, print events, and platform conversion helpers.
- Added platform print dialog service contracts in `ModernFormsNext.WindowKit` and a Windows backend implementation for the system print dialog path.
- Added ControlGallery pages and examples for `GroupBox`, `Switch`, gradient text, and printing workflows.

### Changed

- Bumped the shared package version from `1.5.0` to `1.6.0`.
- Updated the packaged application template to reference `ModernFormsNext` `1.6.0`.
- Updated template and getting-started documentation examples to use `ModernFormsNext` `1.6.0`.
- Improved template package build hygiene so template source files are packaged as content without being compiled into the template package assembly.
- Improved compatibility behavior for component-style types such as `ImageList`, `NotifyIcon`, and `Timer`.

### Fixed

- Fixed three-state `Switch` animated color transitions so direct `-1` to `1` changes blend between the source and target states instead of deriving colors from the middle thumb position.
- Fixed jagged-looking `Switch` rounded fills and borders by drawing solid rounded fills directly and adjusting border radii for stroke width.
- Fixed `GroupBox` content fill clipping so content backgrounds and gradients stay inside the framed border.

### Removed

- No public APIs were removed in this release.

### Compatibility Notes

- This release is backward-compatible with `1.5.0` from a public API perspective. It is a minor release because it adds new public framework API.
- The printing surface includes both ModernFormsNext-rendered dialogs and a system-dialog path. The system path currently depends on backend support; the Windows backend provides the first implementation.
- `Switch` bitmap icon properties store `SKBitmap` references and do not take ownership of them. Keep assigned bitmaps alive while the control can render them and dispose them after they are no longer assigned.
- `GroupBox` shadow customization APIs are present, but ControlGallery keeps shadows disabled while the rendering behavior is refined.

### Validation

- Local restore completed successfully.
- Local Debug build completed successfully.
- Local Release build completed successfully.
- Local package generation completed successfully.
- Local `dotnet test` completed successfully.
- Local ControlGallery smoke run completed successfully.
- Local ModernFormsNext.DemoApp smoke run completed successfully.

## [1.5.0] - 2026-06-26

Compared with [1.4.0], this release focuses on WinForms compatibility, accessibility, and text rendering polish. It adds a new `MaskedTextBox` control, introduces a WinForms-style accessibility surface, exposes that accessibility model through the Windows MSAA backend, and fixes font-style rendering so controls honor bold, italic, and underline style information more consistently.

Published packages:

- `ModernFormsNext`
- `ModernFormsNext.Templates`
- `ModernFormsNext.WindowKit`
- `ModernFormsNext.WindowKit.Backend`
- `ModernFormsNext.WindowKit.Backend.Windows`

### Added

- Added `MaskedTextBox`, a full ModernFormsNext control built on top of the existing `TextBox` infrastructure.
- Added mask editing support backed by `System.ComponentModel.MaskedTextProvider`, including typed input, paste handling, deletion, selection replacement, prompt characters, literals, password display, overwrite mode, and validation.
- Added WinForms-compatible `MaskedTextBox` API surface for migration scenarios, including:
  - `Mask`, `MaskedTextProvider`, `MaskCompleted`, `MaskFull`
  - `TextMaskFormat`, `CutCopyMaskFormat`, `PromptChar`, `PasswordChar`, `UseSystemPasswordChar`
  - `AllowPromptAsInput`, `AsciiOnly`, `SkipLiterals`, `ResetOnPrompt`, `ResetOnSpace`
  - `InsertKeyMode`, `IsOverwriteMode`, `RejectInputOnFirstFailure`
  - `Culture`, `FormatProvider`, `ValidatingType`, `ValidateText`
  - `MaskInputRejected`, `TypeValidationCompleted`, `MaskChanged`, `IsOverwriteModeChanged`
- Added compatibility enums and event types used by `MaskedTextBox`:
  - `MaskFormat`
  - `InsertKeyMode`
  - `HorizontalAlignment`
  - `MaskInputRejectedEventArgs`
  - `MaskInputRejectedEventHandler`
  - `TypeValidationEventArgs`
  - `TypeValidationEventHandler`
- Added a `MaskedTextBox` page to ControlGallery with common examples such as phone numbers, dates, postal codes, license-style input, password-style masked input, and rejected-input feedback.
- Added a WinForms-style accessibility object model:
  - `AccessibleObject`
  - `AccessibleEvents`
  - `AccessibleNavigation`
  - `AccessibleRoles`
  - `AccessibleSelection`
  - `AccessibleStates`
  - `Control.ControlAccessibleObject`
  - `PlatformAccessibleObjectAdapter`
- Added accessibility-related control APIs and event plumbing so controls can expose accessible names, descriptions, roles, states, default actions, navigation, selection, and help.
- Added platform accessibility contracts in `ModernFormsNext.WindowKit`, including `IPlatformAccessibilityHost` and `IPlatformAccessibilityService`.
- Added a Windows MSAA bridge in the Windows backend so the ModernFormsNext accessibility model can be exposed to platform accessibility clients.
- Added WinForms-like help infrastructure:
  - `Help`
  - `HelpEventArgs`
  - `HelpEventHandler`
  - `HelpNavigator`
  - `HelpProvider`
- Added internal property-store support used by the new compatibility/accessibility surface.

### Changed

- Extended `TextBox` with protected editing hooks so derived controls can reuse rendering, caret movement, selection, focus, and clipboard routing while replacing text-editing behavior.
- Updated `TextBoxRenderer` to render the document display text, allowing derived text controls to show prompts, literals, password characters, and placeholders through the same rendering path.
- Improved font-style propagation through control styles, text measurement, text layout, and renderer paths.
- Updated font dialog/gallery behavior to reflect the improved font-style model.
- Updated several controls to participate in the new accessibility surface.
- Bumped the shared package version from `1.4.0` to `1.5.0`.

### Fixed

- Fixed `TextBoxDocument.MaxLength` so assigning a new value updates the backing maximum length correctly.
- Fixed text rendering paths so controls honor full font-style information more consistently.
- Fixed compatibility behavior for text display paths that need to distinguish raw text from rendered display text.

### Removed

- No public APIs were removed in this release.

### Compatibility Notes

- `MaskedTextBox` is intended to make WinForms migrations easier and intentionally includes several WinForms-compatible members that are currently compatibility stubs, such as `AcceptsTab`, `CanUndo`, `Undo`, `ClearUndo`, `ScrollToCaret`, and `WordWrap`.
- `MaskedTextBox.BeepOnError` is preserved as API, but ModernFormsNext does not yet expose a platform-neutral system-beep service. Rejected input still raises `MaskInputRejected`.
- Accessibility is now represented in shared framework code and exposed through the Windows backend. Other backend implementations may need additional platform-specific work before they expose the same accessibility behavior to native accessibility clients.
- The release is backward-compatible with `1.4.0` from a public API perspective. It is a minor release because it adds new public framework API.

### Validation

- Local restore completed successfully.
- Local Release build completed successfully.
- Local package generation completed successfully.
- Local `dotnet test` completed successfully.
- GitHub `.NET` workflow completed successfully for the release commit.
- GitHub `Release` workflow completed successfully for tag `v1.5.0`.
- NuGet public indexes show version `1.5.0` for all published ModernFormsNext packages.

[1.6.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.6.0
[1.5.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.5.0
[1.4.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.4.0
