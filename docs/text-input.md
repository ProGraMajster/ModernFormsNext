# Text input and composition

ModernFormsNext separates physical keys from text. `KeyDown`/`KeyUp` describe key
transitions and command gestures; raw committed text retains the existing `KeyPress`
route. Native composition uses `WindowKit.Input.ITextInputClient` over the editor's
existing document. No hidden native text control or additional focus manager is used.

## Keyboard hints

```csharp
using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;

var address = new TextBox
{
    TextInputOptions = new TextInputOptions
    {
        Scope = TextInputScope.Email,
        AutoCorrect = false,
        Capitalization = TextInputCapitalization.None,
        Action = TextInputAction.Next
    }
};

address.TextCompositionChanged += (_, e) =>
    status.Text = $"{e.Stage}: [{e.Start}, {e.End}), revision {e.Revision}";
```

Scopes are Text, Numeric, Email, Url, Phone and Password. They request native keyboard
behavior; they do not validate or sanitize the document. Existing `ReadOnly`,
`MultiLine` and `PasswordCharacter` determine effective capabilities. Password input
disables correction/capitalization hints and requests native privacy behavior.
Return actions include Default, Done, Go, Search, Send, Next and Previous. Next and
Previous use normal framework focus traversal; custom clients may handle actions.
Native OS/IME policy can ignore hints. An unsupported action returns false.

All mutable text-service operations and state queries require the owning UI thread.
Geometry and hint changes raise `StateChanged` as well as text/selection changes;
the document's `Revision` is specifically a text/range observation token.

## Composition is an editor operation

| Operation | Effect |
|---|---|
| `SetComposingText(text, cursor)` | Starts or replaces provisional text in the real document. The first update captures the replaced fragment. |
| `SetComposingRegion(start, end)` | Marks existing text without changing text or selection. Uses clipped absolute UTF-16 edges. |
| `CommitText(text, cursor)` | Replaces composition or selection, applies the requested caret and accepts the result. Empty text can delete a selection. |
| `FinishComposition()` | Accepts the current visible provisional text and drops composition markers. |
| `CancelComposition()` | Restores the original replaced fragment and oriented selection if the checkpoint still belongs to this edit. |
| `SetSelection(start, end)` | Changes oriented absolute UTF-16 selection without deriving text from keys. |
| `DeleteSurroundingText(before, after, inCodePoints)` | Deletes around the selection through the existing Unicode-safe editor operations. |

The cursor parameter follows native input-connection semantics: positive values are
relative to insertion end minus one; zero and negative values are relative to insertion
start. The editor clips the resulting caret. UTF-16 offsets preserve .NET/WinForms-like
indexing; semantic edits avoid splitting surrogate pairs. Full semantic input strings
must contain complete Unicode scalars. The existing raw UTF-16 character transport
continues to support platforms that deliver surrogate pairs in two character events.
MaxLength still counts UTF-16 units, but truncation cannot retain only half a pair.
Deletion keeps complete text elements, which can remove more units than requested when
a request intersects a combined glyph or emoji sequence.

TextBox, multiline TextBox, RichTextBox and MarkdownEditor share the same client and
document editing path. Ordinary virtual edits and legacy KeyPress/TextChanged callbacks
remain observable. Those legacy callbacks can see the intermediate selection used for
replacement; coherent text-service StateChanged and composition notifications follow
the final state. Nested semantic edits are rejected. Application callbacks that change
text, focus or ownership invalidate the pending edit, so obsolete completion cannot
overwrite another control or a later application assignment.

RichTextBox cancellation restores the affected typed formatting runs instead of
round-tripping the whole document through RTF. This does not add a general RichTextBox
undo system. Markdown uses its existing edit transaction and delta history: one
composition becomes one history edit with final selection; cancellation restores the
checkpoint without adding a transient undo entry. Unrelated programmatic edits/commands
finish or invalidate an existing checkpoint before proceeding.

The provisional range is underlined using the existing shaped text block, including
styled runs, wrapping, bidirectional ordering, zoom and the normal text viewport clip.
No second layout pass or document is created for this adornment. Finish/cancel and
composition-region changes invalidate rendering through the normal editor path.

## Ownership, popup and lifecycle behavior

`WindowBase.TextInputClient` and `SkiaControlSurface.TextInputClient` lend a **session**
for the current canonical focused control. Keep the captured instance for native callbacks.
A retired instance returns null state and false for edits; it never resolves a newer
focus target. Focus transfer, hiding/disabling/removing an ancestor, modal ownership,
host pause, native handoff and disposal retire the old session. Returning to the same
editor creates a fresh session.

Ordinary focus/host retirement preserves the currently visible provisional text through
FinishComposition. Explicit native cancellation or CancelComposition rolls back its still
current checkpoint. Removing or disposing a client cancels ownership of its native session;
it does not silently erase already visible Android text. Revocation precedes callbacks and
native cancellation, including reentrant teardown.

An editable Windows popup borrows the owner's native keyboard/IME transport because
normal framework popups do not activate their own HWND. The backend maps its caret into
owner coordinates and conditionally releases only its own lease. Raw key/text events use
the popup's existing adapter and command resolver while it has a live text client.
Noneditable menu/ComboBox routing retains its existing behavior. Hiding a popup or closing
a modal dialog permits the owner to acquire a fresh session; late popup callbacks cannot
clear a newer owner/other-popup lease.

For a custom embedded native host:

```csharp
surface.AttachTextInputMethod(nativeTextMethod); // optional ITextInputMethod
surface.SetTextInputActive(false);                // pause or lend focus to a native editor
surface.SetTextInputActive(true);                 // reacquire current framework selection
bool accepted = surface.RequestSoftwareKeyboard(true);
```

Native methods and control trees are borrowed. Detach/disposal clears native connections,
but the surface does not dispose its caller-owned root or native view. The Android sample
ties this handoff to its existing Activity/Skia host lifecycle. These seams prepare future
native hosted editors; they do not implement WebView, Media or a native control hierarchy.

## Bounded surrounding text and caret geometry

`GetState(maximumTextLength, textStart)` returns an immutable `TextInputState`. Its default
window is near the caret; an explicit absolute origin requests another document portion.
The hard maximum is 65,536 UTF-16 units, and a zero-length request returns metadata/geometry
without text. Actual `TextStart` can move to preserve scalar boundaries. `DocumentLength`,
selection and composition use **absolute** offsets, including ranges outside the returned
slice. Never mistake `Text.Length` for the whole document length.

CaretRectangle and optional CaretBaseline come from the same shaped plain/styled block
used by rendering. A control client reports local logical pixels. The host session applies
existing ancestor/presentation/scroll transforms and reports logical host-client pixels;
the native adapter applies actual DPI/density and screen origin. Baseline comes from the
shaped line rather than the caret rectangle's lower edge. Arbitrarily transformed editors
are represented by a bounding caret rectangle; this is not a complete character-layout API.

Custom document/code controls override protected `Control.GetTextInputClient()` and return
a stable adapter over their own existing editor. They need not inherit TextBox or expose
their complete document. Implement the bounded query, semantic operations, capability and
geometry notifications; keep platform types out of the adapter. The host remains responsible
for focus/session identity and coordinate conversion.

The old `ControlSurfaceTextInputState` and Android event/provider APIs remain available for
existing integrations. The old surface snapshot is a full TextBox snapshot; new native hosts
should attach ITextInputMethod and use bounded queries. Once the Android host is configured
with a shared client, configured-null means no editor; it never falls back to legacy events
and accidentally targets another control.

## Platform boundaries

Windows uses the existing IMM32 message path and a borrowed HWND input context. Handled
result flags are removed before default processing so a result is inserted exactly once.
Preedit, result, empty result and cancellation are distinct; candidate/composition position
updates use host caret geometry. IMM32 input-context access is balanced across failures.
This is IMM32-compatible text service support, **not a TSF text store**. Full TSF document
locking, reconversion, handwriting, dictation and touch-keyboard integration are not supplied.
`RequestSoftwareKeyboard` returns false on this Windows adapter. Password/read-only/nontext
clients disassociate the native IME context; ordinary password WM_CHAR entry remains available.
Consequently Windows IME composition in password fields is intentionally unavailable.
See Microsoft's [IMM32 composition contract](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-composition),
[TSF overview](https://learn.microsoft.com/en-us/windows/win32/tsf/text-services-framework)
and [input-scope password limits](https://learn.microsoft.com/en-us/windows/win32/api/inputscope/ne-inputscope-inputscope).

Android uses a captured InputConnection session with bounded before/after/selected/extracted
queries, selection notifications, EditorInfo hints/actions, batch coalescing and connection
retirement. API-specific calls are guarded. Surrounding/extracted results preserve their
document origin; an oversized selected range that cannot fit the supported bounded protocol
returns unavailable instead of invented clipped selection. CursorAnchorInfo reports the
insertion marker/selection; unsupported character/editor/line-bound filters return false.
See Android's [InputConnection](https://developer.android.com/reference/android/view/inputmethod/InputConnection)
and [CursorAnchorInfo](https://developer.android.com/reference/android/view/inputmethod/CursorAnchorInfo.Builder).

Native keyboard visibility remains OS policy. Framework `WindowInsets.Ime` is informational;
general automatic keyboard avoidance is not added. An application can combine the existing
inset, current caret and its normal scrolling policy, as the cross-platform sample does.
API 23–29 still lacks the typed IME inset used by the newer adapter.

## Diagnostics and verification

TextInputDiagnostics is detached metadata: generation, ownership/capability flags, revision,
ranges, scope and at most 64 event kinds. It contains no control references, control names,
surrounding/selected/composing text or password values. TextInputState intentionally carries
text for native editing and must not be logged as diagnostics. Native sensitive tracing, if
explicitly enabled, is a separate opt-in facility.

ControlGallery's **Text input and IME** page offers real editors, hints, a metadata display,
explicit preview/finish/cancel operations and popup/modal fields. Preview buttons exercise
shared operations; their success is not evidence of an installed native CJK IME. The Android
cross-platform sample provides real shared editor consumers and the native input adapter.

TestHost's `Input.SetComposingText`, `CommitComposition`, `FinishComposition` and
`CancelComposition` call the production session/editor. `Input.TextInputClient` can be retained
to test rejected late callbacks. `Input.TextInput` remains the ordinary raw-text test helper.
These tests do not emulate OS IME candidate selection, physical keyboards or vendor behavior.

The evolving [issue run report](development/codex-autonomous-issue-run.md) records exact
validated source, builds, tests and native observations. Until those observations are recorded,
this guide is an implementation contract, not a passing native-language/device compatibility
matrix. Physical-device, vendor IME, language/layout and manual candidate-window acceptance
remain separate from deterministic protocol tests and native injected-message tests.
