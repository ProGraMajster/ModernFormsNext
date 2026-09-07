# Issue #56 Phase 2 input audit

Baseline: `574c5f7880b04440948c9f0e1b6fcc55f62477c4`, verified master = origin/master.
Scope: input bindings, gestures, scopes and precedence only. Issue #56 stays open.

| Area | Existing behavior | Phase 2 requirement | Proposed integration |
| --- | --- | --- | --- |
| Windows key creation | WindowImpl maps WM_KEYDOWN/WM_SYSKEYDOWN and key-up messages to WindowKit RawKeyEventArgs. Key repeats arrive as ordinary KeyDown events; no repeat flag is exposed. | Use this pipeline without hooks. | Execute once per delivered KeyDown, including repeats. No RegisterHotKey or native global hooks. |
| Window dispatch | WindowBase.OnInput maps WindowKit.Key to Keys, calls window KeyDown first, then ControlAdapter.SelectedControl.RaiseKeyDown. It currently does not copy the managed key's Handled value back to the raw event. | Preserve preview and consume successful shortcuts at the backend. | Keep window preview first; resolve bindings before focused control input; propagate handled state. |
| Focus/control input | ControlAdapter stores the selected control; controls receive direct input, not a bubbling command route. Button activation occurs on Enter/Space KeyUp. | Local and ancestor lookup, no double activation. | Snapshot matching bindings from the starting focus ancestry, then window/application. Consume the release of a shortcut press so Button KeyUp cannot execute a second action after focus changes. |
| Text/editing | TextBox/MarkdownEditor handle editing KeyDown separately from committed text. Tab focus navigation lives in ControlAdapter.RaiseKeyPress. Existing editing shortcuts exclude AltGraph. | Preserve typing, composition and ordinary navigation when no binding executes. | Do not match text/IME commits; reserve unmodified/Shift-only printable gestures for text. Explicit configured shortcuts precede normal control KeyDown. |
| Modifiers | WindowKit.Input.KeyModifiers already has Control, Shift, Alt, Meta and AltGraph. Keys has Control/Shift/Alt/AltGraph flags, but FromInputModifiers drops Meta. | Reuse canonical flags without changing existing values. | KeyGesture uses existing Keys + KeyModifiers. Add only the missing Keys.Meta flag and map the existing raw Meta bit. |
| AltGr | WindowsKeyboardDevice marks physical RightAlt as AltGraph, preserving synthetic Control+Alt. Existing international text tests include Polish characters. | Ctrl+Alt must not steal AltGr typing. | Never match an AltGraph event to a command gesture. Real Control+Alt without AltGraph can match. No keyboard-layout detection or IME redesign. |
| Mnemonics/menu | IsMnemonic and label mnemonic rendering exist; menu actions use their existing pointer/event paths. No general ProcessCmdKey-style accelerator resolver was found. | Avoid changing menu/Designer contracts. | Add scoped binding lookup only; leave menu ownership, mnemonics, toolbar actions and VS Designer command targets unchanged. |
| Form shortcuts | FormShortcutsPanel subscribes to window KeyDown/Up/Press and marks them handled. | Preserve existing application handlers. | A handled window preview prevents binding lookup and control dispatch. |
| Application lifetime | Application already owns shared resources, Run/Exit, an OnExit event and UI dispatcher. | Application scope and cleanup without a new lifecycle subsystem. | Lazy application collection; clear binding references on existing shutdown paths. |
| Window/control lifetime | Controls dispose recursively. WindowBase Close and native Closed terminate window lifetime; closing can be cancelled. | No retained command/parameter references after successful closure/disposal. | Clear owned collections on disposal/actual close; leave cancelled close intact. No static control dictionaries or CanExecuteChanged subscriptions for nonvisual bindings. |
| Android hardware keys | AndroidSkiaHostView.KeyInput publishes only Backspace/Delete/Enter/arrows; AndroidAppHost maps them into SkiaControlSurface.ProcessKeyDown/Up. Modifiers/source are currently discarded. | Bindings where hardware key events already exist. | Preserve hardware modifiers/source on the existing event and use the shared surface resolver. Letters/digits/function keys are not yet forwarded by this native adapter; do not claim full Android shortcut parity. |
| Android IME | Commit/composition/deletion APIs and InputConnection.SendKeyEvent use editing paths. | Software input must not become a shortcut stream. | Tag existing view hardware events separately; IME editing-key dispatch explicitly skips bindings. No new IME features or #62 work. |
| TestHost | Existing headless host supplies native-window abstractions and dispatcher draining; it intentionally lacks keyboard simulation. Existing core tests can invoke internal input boundaries and SkiaControlSurface. | Deterministic lookup and real pipeline regression tests. | Test existing boundaries directly; do not add TestHost input simulation or #64 Phase 2 APIs. |
| Phase 1 | ICommand, DelegateCommand and shared CommandSource already guard execution and preserve Click semantics. | Reuse direct command semantics. | Fresh CanExecute/current parameter check, then DelegateCommand's internal guarded action core or ordinary ICommand.Execute. Keyboard does not synthesize Button.Click. |

## Decisions before implementation

KeyGesture is a value object using the existing platform-neutral enums. InputBinding/KeyBinding
contain a concrete command, parameter and gesture; no target, routed command or handler route.
Control, WindowBase (including Form), and Application own collections. Search order is focused
control, nearest ancestors, window, application; first added available match wins in one scope.
Unavailable/invalid bindings do not consume an otherwise unconsumed press. Successful bindings
consume key down/text and its corresponding key up. Repeated KeyDown events reevaluate current
availability. Mutating execution stops after the one selected action; predicates must be pure,
and changes to a candidate registration invalidate that evaluation.

Collections and attached bindings use the existing command-source UI-thread affinity policy.
Diagnostics are opt-in per collection and cover invalid/duplicate registration, matches and
unavailable commands. No priority framework, parser, chord sequences, global requery, routing,
async helpers, Developer Tools UI, Designer serialization, #85 or #97 work is included.
