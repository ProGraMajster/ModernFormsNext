# Android Phase 4 native checks

The cross-platform sample includes a separate, explicitly enabled Phase 4 fixture and
instrumentation runner. Both use real shared controls, the existing Android Skia host,
the canonical accessibility objects and Android's `UiAutomation` service connection.
The fixture does not manufacture a semantic tree. The existing `ACCESSIBILITY_DEMO`
panel, Phase 3 instrumentation and historical results remain separate.

After building and installing the current standalone sample APK, invoke the runner on
the chosen emulator or device:

```powershell
adb -s <serial> shell am instrument -w `
  com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.AccessibilityPhase4Instrumentation
```

The runner launches `MainActivity` with `ACCESSIBILITY_PHASE4=true`. That explicit
intent can also open the fixture in an already running sample; Activity recreation
borrows the existing shared panel. The ordinary sample launch does not run these
checks. The runner deliberately changes only its fixture controls, including text,
selection, range values, row order and scroll offsets. It recreates the sample Activity
to verify native-node retirement; it does not alter system accessibility settings.
Preference checks compare the real native configuration before and after recreation.
Recreation waits within one five-second deadline for a different resumed, attached
Activity, the same shared application, a new preference notification and the new
native link node. Polling observes actual state on the instrumentation thread; no
fixed recreation delay substitutes for host readiness. The old native node must
still reject refresh after the replacement is available.
Changing OS font scale or contrast, observing an opted-in application update, and
restoring the original settings are a separate dynamic validation procedure.

The native contract checks cover:

| Area | Checks |
| --- | --- |
| Links and numeric input | Real link Invoke callback and nonempty visible bounds; native range metadata and SetProgress; the existing decimal value/step path while the implicit text editor remains read-only. |
| Date input | Checkbox state/action, rejection of stepping while unchecked, the normal increment path, and no advertised or executable calendar popup in the Android windowless host. |
| Text | Native selection metadata and actions, character movement across a UTF-16 surrogate pair, nonempty native control bounds, and the existing shared renderer's text-range rectangles. |
| Privacy | An explicitly labelled password retains its own label and supported SetText action while its value/search result are redacted; sensitive ancestors redact descendant labels, descriptions and IDs without blocking an ordinary button's normal activation. Protected text/range/selection remain unavailable, and sensitive native events contain no text payload. |
| Grid | Native collection dimensions and cell coordinates, the normal cell edit/commit path, real heading Invoke/sort, retained row identity with updated coordinates, ShowOnScreen, and rejection of a removed cell's stale native node. |
| Scrolling | Native ScrollForward changes the real viewport offset; ShowOnScreen reveals a distant existing button and invokes its normal callback. |
| Recreation | The old native link node becomes invalid while a newly attached native node acts on the same shared control. |
| Preferences | The canonical optional settings provider reads the actual resumed Activity's font scale; Activity recreation keeps the provider, rebinds its UI-thread notifications, and reads the replacement Activity's configuration. The runner releases its native observation subscription. |

Sensitivity is a payload-redaction contract for native assistive technology, not a
general authorization boundary. The protected group's real button still receives its
normal click. Its real text editor rejects text-selection and movement requests, and
its numeric input exposes no range and rejects SetProgress; these checks also verify
that the canonical selection/value did not change. Own-password SetText remains a
supported write-only action. The opt-in `AutomationSession` applies its separate,
more restrictive automation action policy; that policy does not redefine native
accessibility Click or Focus behavior.

Text geometry here combines a native **control-bounds** check with a shared
**text-range geometry** check. It does not claim Android character-location extra-data
support or an Android equivalent of Windows UIA TextPattern. A native assertion failure
stops the run; counts and constant categories describe completed work without dumping
node contents, Bundle values, document text or exception messages. The private test
payload is generated per panel and is never written to the result stream.

The result stream is `ANDROID_ACCESSIBILITY_PHASE4_PASS` or
`ANDROID_ACCESSIBILITY_PHASE4_FAIL` plus the completed assertion count. Failures also
write a constant check category or stage and exception type to the
`MFN.Accessibility.Phase4` log tag. Record the tested source revision, installed APK
hash, API level, device identity and complete result alongside any claimed outcome.

## Recorded native validation

The final-source APK built from **bc160dcbbe8f7ea0acff1cdee18569f8bb8cd50f**
(SHA256 **F8857B4AEC0917B2FB9A10C81613EE554B458A36CD9B41FEC9D13240947AE914**)
passed **64/64 native instrumentation assertions on API 34 and 64/64 on API 36**.
Fresh ordinary cold launches completed on both devices; ready XML shows command
counts 0, preference following unchecked and Running/Foreground generation 1.
The observed launch times were 1647 ms and 3139 ms, not a benchmark. Final readbacks
confirm eight original settings restored per device, contrast key absent, installed
APK hashes matching and shell identity uid 2000. The separate dynamic preference
matrix below remains evidence from **f92759ae2bdc4a482cb7fe4c3c7aa95edc13565b**,
APK SHA256 **81F1ABE0C41213192F2194EF153197DB86654FB62B6B13D26F40B9A6ABD51EFC**:
18 assertions against preference artifacts and four inspected PNGs are not relabeled
as a rerun of those OS mutations on bc160dc.

| Native preference observation | API 34 | API 36 |
| --- | --- | --- |
| Initial opt-in | NoPreference, text scale 1, command count 1 | NoPreference, text scale 1, command count 1 |
| System contrast 1.0 and font_scale 1.3 | High/1.3; host generation 2 | High/1.3; host generation 3 |
| Layout response | Header 105→145, count row 127→146 and preference checkbox 90→100 screen pixels | Same measured height changes |
| Opt-out before restoring OS values | Following stops; six larger header/control rectangles retained exactly | Same result |
| Re-enable with original OS values | NoPreference/1; six rectangles return exactly to initial bounds; generation 4 | Same restoration; generation 5 |
| Retained application state | Editor command count 1 throughout | Editor command count 1 throughout |

Both High and restored native PNGs were inspected on each API. Visible command and
preference labels fit their rows, and larger wrapped text returns to its original
size after restoration. The lower diagnostics remain scrollable; these four images
do not establish full-page visual coverage or measured contrast-ratio compliance.
The first API 36 High hierarchy capture was unavailable during Activity recreation;
the recorded result uses the later actual ready hierarchy, without replaying the
preference mutation.

All eight previously recorded settings were restored exactly on each emulator;
`contrast_level` was absent again, both installed APK hashes matched the tested
artifact, and ADB shell identity remained uid 2000. API 34's original TalkBack
configuration was restored and the service was confirmed bound and enabled.
That last check proves restoration, not a new TalkBack speech/navigation evaluation.

The devices were API 34 `google/sdk_gphone64_x86_64/emu64xa:14/UE1A.230829.050/12077443:userdebug/dev-keys`
and API 36 `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys`,
both user 0 with 1080x2400 hierarchy pixels. Raw instrumentation logs, original/final
settings records, XML, PNGs and hashes are retained in the ignored audit artifacts.
The preference review has 18 artifact assertions across both devices; those are
separate from the 64 native instrumentation assertions per API.

Between those APK revisions, `6a0a4386c3cdc1d5b79b55c84cd57e1004ffb0ec`
only fixes ControlGallery API usage, and `bc160dcbbe8f7ea0acff1cdee18569f8bb8cd50f`
only restores the existing public `DataGridView.EndEdit` trimming annotation.
Both source deltas were checked. Final managed/package/API compatibility and
documentation gates have their own provenance in the implementation report.

These observations are emulator evidence. **Physical-device checks and a new
human TalkBack speech/navigation assessment are NOT EXECUTED in this Phase 4 run.**
The earlier Phase 3 TalkBack evidence remains historical. A successful instrumentation
run does not establish speech order, touch exploration, vendor/OEM coverage or
complete screen-reader parity. Some sample surface diagnostics in these captures
show stale dimensions/attachment labels; those labels are not used as proof of actual
surface allocation or lifetime.

See [current controls](current-controls.md), [grids and calendars](grids-and-calendars.md),
[text providers](../accessibility-text.md), [scroll viewports](scroll-viewports.md), and
[opt-in accessibility preferences](preferences.md).
