# Accessibility preferences and authored themes

The preference path is opt-in. An application keeps ownership of its colors, theme,
explicit control fonts and rich-text run fonts. Applying a theme without options keeps
the previous behavior and a text multiplier of 1.

The optional `IPlatformAccessibilitySettings` capability lives on the existing
`IPlatformSettings` provider. A legacy provider remains valid. Missing capability,
`ColorValues == null`, and `TextScale == null` mean detection is unavailable; they do
not mean that a normal preference was measured. Text scale is independent of display
density. The snapshot copies its color values and contains no native Activity or window.

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;

// Run on the platform UI thread after backend initialization.
var preferences = AvaloniaGlobals.GetService<IPlatformSettings>()
    as IPlatformAccessibilitySettings;
var detected = preferences?.GetAccessibilityPreferences();
bool highContrast = detected?.ColorValues?.ContrastPreference
    == ColorContrastPreference.High;

// These are application-authored ThemeDefinitions, including Body/Caption/Heading.
ThemeDefinition selected = highContrast ? accessibleTheme : normalTheme;
ThemeApplyResult result = ThemeManager.Current.Apply(selected,
    new ThemeApplyOptions { TextScale = detected?.TextScale ?? 1d });
```

The fallback to 1 above is an explicit application choice. It is useful to show
“unknown” separately from a measured value of 1 in diagnostics. System palette
`BackgroundColor` and `ForegroundColor` are nullable; applications may author their
own accessible palette when the platform does not provide those colors. Contrast
should include focus, selection, borders and disabled labels as well as normal text.

`ThemeApplyOptions.TextScale` scales each final typography token once, after base-theme
inheritance merges. It leaves the authoring definitions unchanged. Reapplying the same
definition and factor does not compound the result. `ActiveSnapshot.TextScale` records
the factor already applied. Existing atomic validation and commit handle errors; an
invalid factor throws when setting the option, and an unrepresentable effective font
size fails theme resolution before resources or legacy defaults change. Line-height
and letter-spacing hints remain authored, as do custom resources and layout metrics.

Missing typography tokens keep their existing legacy behavior. The framework does not
multiply the font currently installed globally. Explicit `Control.Font`, explicit
style font sizes, and explicit rich-text run fonts remain application-authored. An app
that wants complete scaling should define its typography roles and give its layout
enough space for the resulting text. Android's native nonlinear large-text policy is
not reproduced by this explicit, uniform theme multiplier.

For runtime changes, own the event subscription and marshal/coalesce application work
through the existing platform dispatcher. Read the latest snapshot inside the posted
callback, check that the consumer is still alive, and unsubscribe before disposing its
controls. A queued callback must become inert after disposal. Do not restore an old
theme during unsubscription over a newer theme deliberately applied by the app.

The deterministic test host exposes the same capability as
`host.Services.Settings.SetPreferences(colors, textScale)`. Its initial values are
unknown; it changes no OS settings and does not automatically apply a theme. Host
cleanup revokes captured service scopes, clears subscribers and restores the borrowed
theme with its original applied text multiplier.

## Native sources and subscription lifetime

Windows extends the registered `Win32PlatformSettings`. It reads the high-contrast
flag through `SPI_GETHIGHCONTRAST`, copies the actual system window/text/highlight
palette, and queries `Windows.UI.ViewManagement.UISettings.TextScaleFactor`. A scoped
WinRT `TextScaleFactorChanged` subscription posts to the existing UI dispatcher.
The existing backend message window refreshes colors after settings, theme and system
color messages. The last optional subscriber releases the WinRT event token and owned
COM wrapper; already-posted delivery checks the subscription generation. Unsupported
activation or detection keeps that field unknown. There is no preference HWND or
second service registration. The system window palette does not claim to describe
every application's authored dark theme.

Android registers the same optional capability on its `IPlatformSettings`. It reads
`Configuration.FontScale` from the current resumed Activity obtained from the existing
weak activity tracker. API 34 and later use `UiModeManager.Contrast` and its change
listener. Positive native contrast maps to `High`; zero and negative levels map to
`NoPreference`. Older APIs have unknown contrast. Android does not provide a system
window/text palette through this path, so those nullable colors remain unknown.
Configuration and contrast listeners are active only while a live resumed host and
subscribers exist. Background/no-host state retires listeners; recreation reads the
current Activity rather than retaining its predecessor. Native callbacks report errors
without throwing through the OS; explicit managed preference reads report observer
failure after committing the detected snapshot.

For reproducible Android validation, the backing contrast setting is
`Settings.Secure.CONTRAST_LEVEL`, key `contrast_level`, for the current user.
It is separate from `high_text_contrast_enabled`. API 34's SystemUI contrast dialog
writes this key for its current user; API 36's Color contrast screen writes the same
setting. The service observes it and reads it for `mCurrentUser`.
[AOSP API 34 dialog](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/android14-release/packages/SystemUI/src/com/android/systemui/contrast/ContrastDialog.kt),
[API 36 selector](https://raw.githubusercontent.com/aosp-mirror/platform_packages_apps_settings/android16-release/src/com/android/settings/display/ContrastSelectorPreferenceController.java),
[service](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/android16-release/services/core/java/com/android/server/UiModeManagerService.java).

A test that changes system preferences should first record the device, user, exact
raw values and whether each key exists. Use the same explicit user for mutation
and restoration, and verify the provider's actual observed state as well as the
setting readback. Restore an originally absent key by deleting it; writing the
default value is not exact restoration. The sample and instrumentation never
change OS preferences automatically.

## Executable sample and evidence

The cross-platform sample's **Use system accessibility preferences** checkbox starts
unchecked. It owns a subscription only while selected, shows unknown fields explicitly,
and applies its authored normal/high-contrast profile through the existing ThemeManager.
It coalesces posted updates, rechecks lifetime after theme callbacks, and unsubscribes
before child/global-command teardown. Activity recreation borrows the same page and
does not duplicate that subscriber. Turning the checkbox off retains the currently
chosen theme and stops following. Closing the page never restores an obsolete theme
over a newer application choice.

After opt-in, labels use measured multiline rows. Single-line buttons and checkboxes
keep their normal renderer and receive enough width for their text; on a narrow viewport,
the existing horizontal scrollbar exposes any overflow. Flow button groups receive
space for larger labels. This is an authored sample layout policy, not automatic
resizing of arbitrary applications or an override of their explicit fonts.

## Recorded validation and limits

Deterministic theme scaling, DPI parity and scoped preference-consumer tests passed
during implementation. Native Android observation is now recorded for the
standalone sample APK at **f92759ae2bdc4a482cb7fe4c3c7aa95edc13565b**, SHA256
**81F1ABE0C41213192F2194EF153197DB86654FB62B6B13D26F40B9A6ABD51EFC**:

- API 34 and API 36 both observed actual `contrast_level=1.0` plus
  `font_scale=1.3` as `High`/`1.3` after explicit sample opt-in.
- On both emulators, native hierarchy bounds show larger header/count/checkbox
  rows. Four native High/restored PNGs were inspected for visible text wrapping
  and clipping.
- Turning following off retains the larger application theme despite restoring
  OS preferences. Re-enabling it restores `NoPreference`/`1` and exactly the
  original six measured header/control rectangles.
- The ordinary editor Save count remains 1 across host recreation: generations
  1→2→4 on API 34 and 1→3→5 on API 36.
- Eight original settings per device are restored exactly, the contrast key is
  absent again, installed hashes match and shell identity remains uid 2000.
  API 34's original TalkBack service is again enabled and bound.

The same APK passed 64 Phase 4 native instrumentation assertions on each API;
those fixture checks and the 18 assertions against recorded preference artifacts
are different evidence sets. See [Android Phase 4 validation](android-phase4-validation.md)
for device fingerprints, the native matrix and limitations.

The later ControlGallery-only fix at `6a0a4386c3cdc1d5b79b55c84cd57e1004ffb0ec`
and `EndEdit` trimming-annotation restoration at
`bc160dcbbe8f7ea0acff1cdee18569f8bb8cd50f` have separate managed validation
provenance; they do not change the recorded APK's source identity. Final solution,
package/API compatibility, Windows provider and documentation gates are recorded
separately in the implementation report.

The combined OS changes establish native propagation and recreation; they do not
isolate every callback or independently prove each authored font is unchanged.
The implementation's explicit-font and noncompounding guarantees also rely on the
separate deterministic tests. **Physical-device checks, a new human TalkBack
speech/navigation assessment and a vendor/OEM matrix are NOT EXECUTED here.**
The restored TalkBack service is a cleanup check, not a screen-reader usability claim.
Windows system-setting mutation and manual high-contrast usability are not implied
by these Android observations. Measured contrast ratios and unseen portions of the
sample are outside these four screenshot observations.

See also the [canonical control semantics](current-controls.md),
[viewport contracts](scroll-viewports.md), [text provider](../accessibility-text.md),
and [bounded diagnostics and Designer](diagnostics-and-designer.md).

The native signals are documented by Microsoft for
[UISettings.TextScaleFactor](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.textscalefactor)
and by Android for
[UiModeManager contrast](https://developer.android.com/reference/android/app/UiModeManager#getContrast()).
