# Android hardware input and shortcuts

The Android hardware adapter forwards supported key identities and modifiers to the existing
ModernFormsNext input resolver. The resolver selects commands from the focused control, its
ancestors, the surface root and Application. Windows retains its additional WindowBase scope.
This extends the command system completed in [#56](https://github.com/ProGraMajster/ModernFormsNext/issues/56);
it does not add a second command or focus system.

> [!IMPORTANT]
> The expanded adapter has **deterministic and scoped API 34 emulator validation**. Supported
> routes and observed limits are recorded below. Physical hardware keyboards are **NOT EXECUTED —
> environment unavailable**. Android remains Experimental; implementation coverage is not a
> declaration of parity across devices, layouts or Android versions.

## Keys and source eligibility

The adapter uses explicit key identities, not committed text, to select gestures.

| Family | Supported `AndroidInputKey` identities |
|---|---|
| Existing editing keys | Backspace, Delete, Enter, Left, Up, Right, Down |
| Letters and top-row digits | A–Z, D0–D9 |
| Function keys | F1–F12 |
| Navigation/editing | Tab, Escape, Space, Home, End, PageUp, PageDown, Insert |
| Numeric keypad | NumPad0–NumPad9, Divide, Multiply, Subtract, Add, Decimal, Separator; native numpad Enter uses Enter |
| Modifier transitions | Left/Right Shift, Ctrl, Alt and Meta |
| OEM identities | OemComma, OemPeriod, OemTilde, OemMinus, OemPlus, OemOpenBrackets, OemCloseBrackets, OemPipe, OemSemicolon, OemQuotes, OemQuestion |

Ctrl+S and Ctrl+Shift+S are distinct exact-modifier gestures. Ctrl, Shift, Alt and Meta retain
their existing meanings; Meta is not an automatic alias for Ctrl. Plain/Shift-only printable
gestures remain unavailable so ordinary typing retains its text path. Modifier-only keys
describe transitions; they are not valid KeyGesture commands. OEM identities do not promise
the same resulting punctuation on every keyboard layout.

Android system Back/Home, power, volume, media and unsupported vendor keys are not commandeered
as framework shortcuts. The editing Home key is distinct from Android's system Home action.
Native policy can reserve a combination before it reaches the view.
Mapping support therefore does not guarantee that every device/launcher delivers that key.

`AndroidInputKeyEvent` keeps its original `(AndroidInputKey, bool)` constructor and two-value
deconstruction. Existing values 0–6 remain Backspace, Delete, Enter, Left, Up, Right and Down.
The old constructor defaults to editing input. `PlatformKey` computes the corresponding existing
WindowKit key identity. `RepeatCount` is zero for an initial transition, `IsCanceled` and
`IsDeadKey` default to false, and the two-argument constructor gives `DeviceId = -1`. Existing
`Modifiers` and `IsHardwareKey` remain available. These values describe a key transition, not
surrounding or committed text.

`IsHardwareKey` means that the source is eligible for command lookup: a device-backed native
view event, without the soft-keyboard flag and without InputConnection origin. It is not proof
of a physical keyboard: an emulator device can satisfy this rule. Virtual-device events and
InputConnection editing remain excluded even when they contain Ctrl/Shift/Alt/Meta or a plausible
device ID. Synthetic Shift navigation still extends text selection.

Side-specific modifier bits are normalized into the existing modifier flags. Right Alt is
conservatively marked AltGraph, including when Android also reports Ctrl+Alt. AltGraph and dead
keys bypass command lookup; real Ctrl+Alt without AltGraph can match. A dead-key flag never
authorizes the adapter to insert an accent or synthesize committed text. Android's text services
and the existing [composition client](text-input.md) retain that responsibility.

## Handled input and compatibility

`AndroidSkiaHostView.KeyInputHandler` is the primary synchronous bool-returning key route. When
configured, it receives the supported transitions instead of the legacy `KeyInput` event. A
native view event handled by the shared route is consumed; an unhandled one can reach native
fallback while the captured host remains active and attached. A callback that moves focus,
replaces the host or disposes it cannot resume work against a stale owner. Exceptions propagate;
the adapter does not retry the command or automatically deliver it through a second route.

Android offers hardware events to the IME before delivering them to the application; the IME
can consume them or forward them. See Android's [input-method dispatch guidance](https://developer.android.com/develop/ui/views/touch-and-input/creating-input-method).
Returning false from the shared handler therefore does not guarantee character insertion.
The Skia host view has no native editable KeyListener, and its base View fallback does not
translate key identities into document text. Hardware typing depends on the IME/text service
delivering semantic text operations, or on an application's explicit text integration. A layout
or IME that forwards unhandled printable keys without committing text may leave those keys
without inserted characters. The shortcut adapter does not add a second layout/dead-key translator.

Without the new handler, `KeyInput` retains its original seven-key subset and legacy consumption
contract. Existing consumers need not handle newly appended enum values. A host should select
one primary transport, rather than subscribing two callbacks that both execute commands.

InputConnection has a different return contract: acceptance of an IME editing request is not
the shared key's Handled state. A mapped request is accepted once even when no command or control
handles that key, preventing native fallback from redispatching it through the view. An unmapped
fallback preserves the software-input marker if Android routes it through the view later.
`SendKeyEvent` for a letter is not a promise to translate that letter into text; IMEs should use
the semantic composing/commit operations for text. Modifier preservation must not convert these
editing requests into shortcut input.

## Shared surface bridge

`KeyEventArgs.FromPlatformKey(ModernFormsNext.WindowKit.Input.Key, KeyModifiers)` uses the existing shared key
conversion. It does not translate characters. Unknown/unmapped keys become `Keys.None`; invalid
modifier flags are rejected. The result is owned by the caller and can be inspected after routing.

`SkiaControlSurface.TryProcessKeyDown(KeyEventArgs, bool isTextInput = false, bool isDeadKey = false)`
and `TryProcessKeyUp(KeyEventArgs, bool isTextInput = false, bool isCanceled = false)` return the
final handled/suppressed state through the same resolver used by the existing void APIs.
Create a fresh event for each transition and call on the owning UI thread. Null events are
rejected before callbacks. The methods preserve ordinary control input when no binding handles
the key; they do not execute a second native command path.

For an Android host that owns the native view and surface, retain the delegates for teardown:

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;

Func<AndroidInputKeyEvent, bool> routeKey = input =>
{
    var e = KeyEventArgs.FromPlatformKey(input.PlatformKey, input.Modifiers);
    return input.IsDown
        ? surface.TryProcessKeyDown(e, isTextInput: !input.IsHardwareKey,
            isDeadKey: input.IsDeadKey)
        : surface.TryProcessKeyUp(e, isTextInput: !input.IsHardwareKey,
            isCanceled: input.IsCanceled);
};
EventHandler resetKeys = (_, _) => surface.ResetKeyboardState();
nativeView.KeyInputHandler = routeKey;
nativeView.KeyboardStateReset += resetKeys;
```

Before disposing or replacing that host's surface, remove its registrations on the UI thread:

```csharp
try
{
    if (nativeView.KeyInputHandler == routeKey)
        nativeView.KeyInputHandler = null;
}
finally
{
    nativeView.KeyboardStateReset -= resetKeys;
}
```

Replacing the handler requests a reset and can propagate a subscriber failure. The owning host
must still finish its other surface/native cleanup and preserve those failures; the sample's
Dispose method collects cleanup failures before throwing.

The sample's `AndroidKeyboardInput` joins these same APIs and is also used by its deterministic
integration tests. Do not add a second alphabet mapping or infer text from the platform key.

Each delivered repeated KeyDown is evaluated once. A successful binding consumes its matching
release even if focus or availability changes; KeyUp does not execute a binding. Commands such
as Save should tolerate repetition or use availability to decline it. After an Execute exception,
the caller-owned event still exposes the suppression set before application code ran. Do not
use an exception as a reason to redispatch input.

For the primary hardware View route, the native host pairs presses by key and device ID within
its current lifetime. A release without a matching press is forwarded as `IsCanceled`; an orphan
repeat is ignored until a new initial KeyDown (`RepeatCount = 0`). Reset clears this pairing before
notifying subscribers. The bounded capacity is 256 simultaneous pairs; saturation clears native
and shared keyboard state and consumes the current event without retry. Legacy seven-key events
and InputConnection editing keep their existing unpaired delivery behavior.

The existing `ProcessKeyDown(Keys[, bool])` and `ProcessKeyUp(Keys[, bool])` void overloads remain
available and delegate to the same implementation. Text/IME input uses `isTextInput: true` so
editing modifiers survive while command lookup is bypassed. A dead key clears obsolete
suppression for its key and returns unhandled without invoking command lookup, control KeyDown,
editing or activation; an already handled/suppressed event remains consumed. A canceled release
retires its pending interaction without invoking ordinary control KeyUp, Click or a release
ripple. PressScale can animate its return to rest; independent pointer capture and pointer
press effects remain intact.

## Focus, reset and ownership

Wire `AndroidSkiaHostView.KeyboardStateReset` to `SkiaControlSurface.ResetKeyboardState()`.
Reset clears held/consumed keyboard state and transient keyboard interaction. It does not
delete text, change focus, remove bindings or synthesize a release that could activate a Button.
The native adapter requests reset on native view/window focus loss, lifecycle pause/stop,
detach, handler replacement and managed disposal; canceled input retires the affected interaction.
Reset invokes all subscribed handlers even if one fails. Explicit host operations finish mandatory
cleanup and propagate the original or aggregated failures. Native view/window focus callbacks
instead log a reset failure's exception type after cleanup so Android's focus bookkeeping can
finish; they do not log editor contents. Reentrant resets invalidate the old route and coalesce
without recursively delivering another reset. Finalization does not invoke application handlers.
Reset and routing require the UI thread; detached `KeyEventArgs` creation alone can occur on any
thread.

The host owns these subscriptions and must detach them before disposing its surface. The
cross-platform sample demonstrates the concrete adapter. Activity recreation borrows the same
application tree; it does not create another set of Application bindings. The page registers its
Application binding only after construction succeeds and rolls back a failed registration.
Disposal removes that exact entry from the captured collection before disposing children, even
when a child callback fails or Application has already exited. The general Android
Application/Form/windowing host remains [#72](https://github.com/ProGraMajster/ModernFormsNext/issues/72).

## Sample

Run the existing [cross-platform sample](cross-platform-sample.md#hardware-command-section).
Its first editor has a local Ctrl+S command, the page supplies the unavailable-command fallback,
Ctrl+Shift+S selects Save As, and F1 selects an Application InputBinding for a RoutedCommand.
The Help CommandBinding is on the page in the current input route; another page retaining
Selected state cannot take over this command. Buttons use the same domain commands. Separate
counts expose double delivery without logging editor text or writing files. A focused field plus
the availability toggle exercises the real resolver. The default input status records source
category, down/up, handled result and repeat count, without printable key identities or payloads.

Command properties and bindings remain runtime-only and hidden/nonserializable in Designer.
No `.mfdesign` change is needed; declarative command discovery is separate
[#108](https://github.com/ProGraMajster/ModernFormsNext/issues/108).

## Validation matrix

Record the exact source commit and APK hash with each native observation. Include Android API,
device/emulator model, keyboard/layout, native device ID/flags, action/repeat/cancel metadata and
the observed command/text result. Keep diagnostic records free of typed or selected text and
password contents. Save only the minimum metadata needed to distinguish routes.

| Evidence lane | Current result | Scope |
|---|---|---|
| Expanded deterministic mapping and real-surface integration | **PASS** | Android 314, core 1285 and sample 22 tests include production mapping/bridge, fallback, repeats/releases, handled result, reset, stale callbacks, IME and AltGraph safety. |
| Debug/Release solution and native Android build | **2944/2944 tests PASS in each configuration** | Nine projects, zero failed/skipped; native Android TFM and the exact-source APK compile. Compilation is not native hardware observation. |
| API compatibility, packages and executable consumer | **PASS** | 13 assembly/TFM comparisons per configuration, no attribute exclusions; 11 packages, 10 symbol packages, 40 isolated consumer assertions. Original enum values/constructors/void APIs remain. Android backend is source-built, not a standalone NuGet package. |
| API 34 emulator | **PASS scoped shortcuts; PARTIAL hardware text** | Pixel_8, Gboard 12.4.05.482060964-preload-x86_64, existing AT keyboard device 2 / Generic.kl, native Keyboard source with FromSystem: F1, Ctrl+S, Ctrl+Shift+S, focused editor/outer fallback and repeats observed. Ctrl+RightAlt+S does not execute a command. Ordinary A and Shift+A reach the View unhandled but do not insert text in this configuration. |
| API 36 emulator | **PASS virtual-source exclusion; positive route NOT EXECUTED — environment unavailable** | Android 16 emulator, Gboard 15.1.08.726012951-preload-x86_64. Virtual F1/Ctrl+S do not execute commands. The tested console transport reports acceptance but produces no native keyboard delivery; this cannot establish positive shortcut behavior or a framework failure. |
| Physical Android device with hardware keyboard | **NOT EXECUTED — environment unavailable** | No USB/Bluetooth physical-keyboard behavior is inferred from emulator input. |
| Additional vendor/layout/CJK combinations | **NOT EXECUTED — environment unavailable** | No broad compatibility result is inferred; preserve separate language, candidate and software-IME acceptance under #62. |

These #109 results use production/test source `8fd369650ebeba53ec78e299483c8db8a0216701`
and APK SHA-256 `564A74A2F23E4107A4B58C87BFA830B43566B84A0C5959E0E672B2B14638EE2D`.
The [durable acceptance report](development/codex-autonomous-issue-run.md#issue-109--validated-source-and-local-artifact-gates)
separates command observations, text limits, restoration, exact-source artifacts and later
documentation-only commits. #109 remains OPEN/PARTIAL for the recorded native breadth.

An `adb input keyevent` using a virtual source cannot establish the positive hardware route.
Eligible emulated-device injection can validate the emulator path, with exact provenance and
all injected modifier releases cleaned up afterward. It does not establish physical-device
reliability. Preserve prior #56/#62 observations under their original source identities in the
[issue run report](development/codex-autonomous-issue-run.md).

The broader device/performance/reliability matrix remains
[#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69). This work does not add OS-global
hotkeys, shortcut chords, a TSF engine, a general Android window host or a Designer command catalog.
