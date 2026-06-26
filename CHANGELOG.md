# Changelog

All notable ModernFormsNext changes are documented in this file.

ModernFormsNext follows semantic versioning. Git tags use a `v` prefix, while NuGet package versions do not.

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

[1.5.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.5.0
[1.4.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.4.0
