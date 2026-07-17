# ADR: Localization as providers plus dynamic resource references

- Status: Proposed
- Date: 2026-07-17

## Context

ModernFormsNext has culture-aware controls and a data-binding engine but no UI localization service.
Applications need JSON catalogs, culture fallback, pluralization, formatting, RTL, external
providers, missing-key diagnostics, validation, and live language changes.

## Problem

Support both immediate lookup (`Localizer["Navigation.Home"]`) and a control reference that updates
automatically after culture changes, without teaching every control a localization-specific event
or rebuilding the control tree.

## Options considered

1. Load one JSON dictionary and require applications to assign all text again after a culture change.
2. Model localized values as a special case inside `Control.Text`.
3. Use provider-based catalog resolution and publish resolved text through dynamic resources.
4. Use `.resx` exclusively and expose only generated strongly typed resources.

## Decision

Add `ILocalizationProvider`, `LocalizationManager`, and a default JSON provider in the main package.
Providers return messages and metadata for a namespaced key and culture. Resolution follows exact
culture, neutral culture, configured default culture, then a missing-key policy. Formatting and
plural selection use the active `CultureInfo` and an explicit plural-rules service rather than
English-only singular/plural branches.

`Localizer[key]` performs immediate lookup. `SetLocalizedText(key)` is a convenience over a dynamic
resource reference to a localization-owned resource key. On culture change, the manager resolves
only known requested keys, updates those resource values on the UI dispatcher, and lets normal CLR
setters update affected controls. External libraries can register providers with an ordered
namespace prefix.

Flow direction is not inferred per string. `LocalizationManager` exposes culture direction, while
window/page hosts opt into automatic `RightToLeft`/`FlowDirection` updates once layout support is
verified.

## Consequences

- Localization shares resource lifetime and targeted updates with themes.
- JSON is the default transport, not the only provider format.
- Strongly typed keys can be generated as an optional build-time tool without becoming a runtime
  dependency.
- Plural rules and ICU-like message syntax require a deliberately scoped grammar and validation;
  raw `string.Format` alone is insufficient.
- RTL affects layout, hit testing, navigation, and text alignment and must not be claimed complete
  until those systems pass mirrored tests on Windows and Android.

## Rejected alternatives

- Reassigning every label is error-prone and cannot support reusable controls.
- Special-casing `Text` prevents localization of tooltips, accessibility text, formats, and future
  non-string culture resources.
- `.resx`-only support would make external packages and JSON hot reload unnecessarily difficult;
  a `.resx` provider can still be added later.
