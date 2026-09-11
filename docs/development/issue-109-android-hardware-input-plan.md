# Issue #109 — Android hardware input audit and implementation plan

Audit date: 2026-09-11. Baseline: merged master
`61c15190fef81e0f986c63956220fe5658ca0dca` (PR #117). The tracked tree/index
was clean; the unrelated untracked `.codex/config.toml` remains untouched.
This plan is committed before implementation. The full current issue, all comments
(zero), formal dependencies (none), related issue bodies/comments and complete
timelines for #56/#62/#69/#72/#109 were read alongside source, tests and docs.

## Current implementation and gaps

The canonical `InputBindingResolver` already resolves the focused control, nearest
ancestors, root, optional window and Application. It handles unavailable inner
commands, exact modifiers, repeats, consumed releases, mutation and callback
lifetime. `CommandRouting` remains the existing routed command handler mechanism.
PRs #105/#106/#107/#110 completed the shared runtime; #109 was split from #110.
No Android-specific command registry, ancestor traversal or focus manager is needed.

`AndroidSkiaHostView.PublishKey` maps only seven editing/navigation values, returns
true regardless of subscriber handling, and uses an immutable observer event.
The sample's seven-key switch throws for unknown values. Extending that switch
alone would consume unhandled hardware letters and prevent Android text fallback.
`SkiaControlSurface.ProcessKeyDown/Up` return void and hide the event's handled
result. Its resolver only resets at disposal, so a lost release across native focus
loss or pause can leave stale suppression. Surface Enter synthesis and Tab routing
also need explicit handled/lifetime tests at this new native result boundary.

#62's shared text client, captured/revocable InputConnection, separate commit path,
composition exclusion and modifier-preserving software selection are now available.
They must remain canonical. The existing Android source rule is device ID >= 0,
no SoftKeyboard flag and no InputConnection origin. This indicates shortcut-route
eligibility, not proof of a physical keyboard: an emulator keyboard can satisfy it.
Right Alt remains conservatively AltGraph. Side-specific modifier bits require
normalization; dead-key metadata must not authorize text translation or a shortcut.

## Scope and dependencies

#56 is CLOSED and its runtime is implemented. #62 remains OPEN/PARTIAL for native
language/vendor/candidate breadth, which does not block this adapter. #69 owns the
broader physical device/release matrix, and #72 owns a general Android Application/
Form/windowing host; both are excluded implementation work. On the current Android
standalone surface the borrowed root is the surface scope; there is no fabricated
WindowBase. #108 Designer command serialization and VS shortcut work remain separate.
Existing Designer command metadata stays hidden/runtime-only.

## Additive implementation

1. Append explicit supported letters, digits, function and navigation keys (and
   a bounded standard numpad subset) to `AndroidInputKey`, preserving numeric values
   0–6. A pure backend mapper supplies the existing WindowKit key representation.
   Preserve the two-argument record constructor/deconstruction and editing default;
   add source/repeat/canceled/dead-key metadata and a derived platform key. Do not
   intercept Android Back, Home, power, volume or media keys, or claim every OEM key supported.
2. Add an optional synchronous `KeyInputHandler` returning bool on the native view.
   It is exclusive of legacy `KeyInput`; absent the new handler, the old event still
   receives only its original seven keys with its original consumption contract.
   Forward each native Down/repeat/Up once. Check captured attachment, focus, activity
   and disposal state before native fallback; callbacks cannot resume a stale route.
   Exceptions must not cause an automatic retry or duplicate native delivery.
3. Add `SkiaControlSurface.TryProcessKeyDown/Up` with caller-owned `KeyEventArgs`,
   retaining all existing void signatures. They use the current resolver and return
   final handling, including suppression and shared control behavior. Add a small
   platform-key factory on `KeyEventArgs` using the existing WindowKit mapper so the
   sample does not copy a second alphabet/function mapping. Document UI affinity,
   event ownership, repeats, exceptions and the lack of character translation.
4. Add a narrow surface keyboard-reset seam and native `KeyboardStateReset`
   notification. Clear consumed press state and cancel key interaction without
   ordinary release activation on pause/stop, native focus loss, detach/disposal
   and canceled input. Mandatory native cleanup completes even if an observer
   fails; finalization invokes no application handlers. Dead-key/AltGraph routes
   must retire stale suppression while preserving real modifier provenance.
5. Preserve shared control handling and prevent stale synthetic Enter/Tab work
   after callbacks change focus, ancestry or lifetime. Use existing focus traversal
   and control key processing. Any TextBox handled-state correction must have a
   concrete regression demonstrating this adapter boundary; do not rewrite editors
   or the resolver's established scope/repeat semantics.
6. Update the real Android sample adapter to use the primary handler and reset seam.
   Add a compact shared command demonstration with Ctrl+S, Ctrl+Shift+S, function
   keys, visible counts and unavailable inner fallback. Application registrations
   belong to the shared page lifetime and are removed by identity on page disposal;
   Activity recreation must not register duplicates. Do not modify DemoApp content.

## Validation and acceptance

Tests start with production Android mapping and its actual bridge into a real
`SkiaControlSurface`; they must not reproduce mapping or instantiate a second
resolver. Use existing test projects and the Android nonparallel UI collection.

| Acceptance criterion | Required evidence |
|---|---|
| Supported key/modifier breadth, Ctrl+S vs Ctrl+Shift+S | Exact mapping and modifier tests, native Android compilation, real sample command observations. |
| Down/Up/repeat/Handled/focus/lifetime, no duplicate actions/text | Shared and Android integration tests for repeats, unavailable/unhandled/control-handled paths, consumed releases, canceled input, reset, callback mutation/disposal/throwing; exclusive primary/legacy delivery. |
| AltGraph/dead-key/software/IME safety | Source classification, normalization, stale suppression, active composition and existing software Shift-selection/commit tests; separate native observations with exact source flags. |
| Scope and unavailable fallback | Real focused control, two ancestors, surface root, Application and existing Windows window tests through the canonical resolver. |
| Deterministic vs emulator vs physical observations | Preserve distinct records; emulator console EV_KEY input may traverse an eligible emulated device. `adb input keyevent` uses a virtual device and is not positive hardware evidence. Always release injected modifiers in cleanup. |
| API/device/layout limits and documentation | Exact supported matrix and observed APK/source/API/keyboard/layout provenance; retain unobserved limits. |

Run restore, serial full Debug/Release solution builds/tests with `-m:1` and
`UseSharedCompilation=false`, affected API comparison against the merged baseline,
package and isolated consumer checks, documentation scripts/DocFX/archive checks,
and native Android builds. Android backend is not packable, so package success
alone does not verify its native TFM. Shared input changes retain Windows/TestHost/
Designer regression coverage. Run the actual relevant samples and distinguish
startup, offscreen rendering, injected native events and observed interaction.

Available emulator configurations from #62 are API 34 / Gboard 12.4 and API 36 /
Gboard 15.1. Prior evidence is historical, not a #109 test result. Preserve user
settings, keyboard state and diagnostic opt-in boundaries. Physical keyboards,
additional vendor/layout/device combinations and unavailable CJK tests remain
**NOT EXECUTED — environment unavailable** unless actually observed in this run.

Update `docs/commands.md`, Android/sample guides, AND-07, the narrow roadmap note
and the durable issue report. Preserve experimental Android status, unrelated
limitations and released changelog/version/package metadata. No dependencies,
releases, tags, publishing, schema changes or public API removals are planned.
After final review and required CI, push a Ready PR and merge completed useful
scope even if remaining physical evidence keeps #109 OPEN/PARTIAL. Start #59
from the verified newly merged master.

## Primary native references inspected during audit

- [Android KeyEvent](https://developer.android.com/reference/android/view/KeyEvent)
- [Android KeyCharacterMap](https://developer.android.com/reference/android/view/KeyCharacterMap)
- [Android emulator console](https://developer.android.com/studio/run/emulator-console)
- [AOSP shell input implementation](https://android.googlesource.com/platform/prebuilts/fullsdk/sources/+/refs/heads/androidx-compose-integration-release/android-34/com/android/server/input/InputShellCommand.java)
