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

**Current evidence: final corrected-run validation pending.** API 34 and API 36
runs, final-source package validation and physical TalkBack/manual checks must be
reported separately. A successful instrumentation run does not establish speech order,
touch exploration, physical-device usability, or complete screen-reader parity.

See [current controls](current-controls.md), [grids and calendars](grids-and-calendars.md),
[text providers](../accessibility-text.md), [scroll viewports](scroll-viewports.md), and
[opt-in accessibility preferences](preferences.md).
