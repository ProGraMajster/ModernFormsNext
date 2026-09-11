using Android.Views;
using Android.Views.InputMethods;
using ModernFormsNext.WindowKit.Input;
using ICharSequence = Java.Lang.ICharSequence;
using NativeKeyEvent = Android.Views.KeyEvent;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

public sealed partial class AndroidSkiaHostView
{
    private sealed class SharedInputConnection(AndroidSkiaHostView owner, ITextInputClient? client = null) : BaseInputConnection(owner, false)
    {
        private string? lastTextArgument;
        private readonly AndroidTextInputSession? session = client is null ? null : new(client);
        private bool revoked;
        private int legacyBatchDepth;
        private bool IsCurrent => !revoked && !owner.disposed && ReferenceEquals(owner.activeInputConnection, this) &&
            (session is null || session.GetState(0) is not null);

        internal void Revoke()
        {
            revoked = true;
            session?.Revoke();
            legacyBatchDepth = 0;
            lastTextArgument = null;
        }

        public override void CloseConnection()
        {
            Revoke();
            if (ReferenceEquals(owner.activeInputConnection, this))
            {
                owner.activeInputConnection = null;
                owner.inputStateNotificationPending = false;
            }
            if (OperatingSystem.IsAndroidVersionAtLeast(24)) base.CloseConnection();
        }

        // ModernFormsNext owns all editable state in TextBoxDocument. Returning null prevents
        // inherited helpers from silently creating BaseInputConnection's private fake Editable.
        public override global::Android.Text.IEditable? Editable => null;

        internal int BatchDepth => session?.BatchDepth ?? legacyBatchDepth;

        public override bool BeginBatchEdit()
            => Trace("BeginBatchEdit", "ImeInputConnection", string.Empty, () =>
            {
                if (session is not null) return session.BeginBatch();
                if (legacyBatchDepth == 256) return false;
                legacyBatchDepth++;
                return true;
            });

        public override bool EndBatchEdit()
            => Trace("EndBatchEdit", "ImeInputConnection", string.Empty, () =>
            {
                if (BatchDepth == 0)
                    return false;

                if (session is not null) session.EndBatch();
                else legacyBatchDepth--;
                owner.FlushTextStateNotification();
                return BatchDepth > 0;
            });

        public override bool CommitText(ICharSequence? text, int newCursorPosition)
        {
            if (!IsCurrent) return false;
            var value = text?.ToString() ?? string.Empty;
            return Trace(
                "CommitText",
                "ImeInputConnection",
                $"newCursorPosition={newCursorPosition}",
                () =>
                {
                    if (session is not null) return session.Edit(target => target.CommitText(value, newCursorPosition));
                    owner.PublishCommittedText(value, newCursorPosition);
                    return true;
                },
                value,
                newCursorPosition);
        }

        public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
        {
            if (!IsCurrent) return false;
            var value = text?.ToString() ?? string.Empty;
            return Trace(
                "SetComposingText",
                "ImeInputConnection",
                $"newCursorPosition={newCursorPosition}",
                () =>
                {
                    if (session is not null) return session.Edit(target => target.SetComposingText(value, newCursorPosition));
                    owner.PublishComposingText(value, newCursorPosition);
                    return true;
                },
                value,
                newCursorPosition);
        }

        public override bool SetComposingRegion(int start, int end)
            => Trace(
                "SetComposingRegion",
                "ImeInputConnection",
                $"start={start}; end={end}",
                () =>
                {
                    if (session is not null) return session.Edit(target => target.SetComposingRegion(start, end));
                    owner.PublishComposingRegion(start, end);
                    return true;
                });

        public override bool FinishComposingText()
            => Trace("FinishComposingText", "ImeInputConnection", string.Empty, () =>
            {
                if (session is not null) return session.Edit(target => target.FinishComposition());
                owner.PublishCompositionFinished();
                return true;
            });

        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
            => Trace(
                "DeleteSurroundingText",
                "ImeInputConnection",
                $"beforeLength={beforeLength}; afterLength={afterLength}",
                () =>
                {
                    if (beforeLength < 0 || afterLength < 0)
                        return false;
                    if (session is not null)
                        return session.Edit(target => target.DeleteSurroundingText(beforeLength, afterLength));
                    var state = owner.GetTextInputState();
                    owner.PublishDeletion(new AndroidTextDeletionRequest(
                        Math.Min(beforeLength, Math.Min(state.SelectionStart, state.SelectionEnd)),
                        Math.Min(afterLength, state.Text.Length - Math.Max(state.SelectionStart, state.SelectionEnd))));
                    return true;
                });

        public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
            => Trace(
                "DeleteSurroundingTextInCodePoints",
                "ImeInputConnection",
                $"beforeLength={beforeLength}; afterLength={afterLength}",
                () =>
                {
                    if (beforeLength < 0 || afterLength < 0)
                        return false;
                    if (session is not null)
                        return session.Edit(target => target.DeleteSurroundingText(beforeLength, afterLength, inCodePoints: true));

                    owner.PublishDeletion(owner.GetTextInputState().GetUtf16DeletionForCodePoints(beforeLength, afterLength));
                    return true;
                });

        public override ICharSequence? GetTextBeforeCursorFormatted(int length, GetTextFlags flags)
            => Trace(
                "GetTextBeforeCursor",
                "ImeInputConnection",
                $"length={length}; flags={flags}",
                () => ToJavaText(session is not null ? session.GetBefore(length) :
                    length < 0 ? null : owner.GetTextInputState().GetTextBeforeCursor(Math.Min(length, TextInputState.MaximumTextLength))),
                resultFormatter: value => value?.ToString());

        public override ICharSequence? GetTextAfterCursorFormatted(int length, GetTextFlags flags)
            => Trace(
                "GetTextAfterCursor",
                "ImeInputConnection",
                $"length={length}; flags={flags}",
                () => ToJavaText(session is not null ? session.GetAfter(length) :
                    length < 0 ? null : owner.GetTextInputState().GetTextAfterCursor(Math.Min(length, TextInputState.MaximumTextLength))),
                resultFormatter: value => value?.ToString());

        public override ICharSequence? GetSelectedTextFormatted(GetTextFlags flags)
            => Trace(
                "GetSelectedText",
                "ImeInputConnection",
                $"flags={flags}",
                () => ToJavaText(session is not null ? session.GetSelected() : GetLegacySelectedText()),
                resultFormatter: value => value?.ToString());

        public override ExtractedText? GetExtractedText(ExtractedTextRequest? request, GetTextFlags flags)
            => Trace(
                "GetExtractedText",
                "ImeInputConnection",
                $"token={request?.Token ?? 0}; flags={flags}; hintMaxChars={request?.HintMaxChars ?? 0}; " +
                  $"hintMaxLines={request?.HintMaxLines ?? 0}",
                () =>
                {
                    if (session is not null)
                    {
                        var limit = AndroidTextInputSession.NormalizeLimit(request?.HintMaxChars ?? 0);
                        if (((int)flags & 1) != 0 && request is not null)
                            session.MonitorExtractedText(request.Token, limit);
                        var snapshot = session.GetExtracted(limit);
                        if (snapshot is not null) session.MarkExtractedTextPublished(snapshot.Revision);
                        return CreateExtractedText(snapshot);
                    }
                    var inputState = owner.GetTextInputState();
                    if (inputState.Text.Length > TextInputState.MaximumTextLength) return null;
                    return new ExtractedText
                    {
                        Text = new Java.Lang.String(inputState.Text),
                        StartOffset = 0,
                        PartialStartOffset = -1,
                        PartialEndOffset = -1,
                        SelectionStart = inputState.SelectionStart,
                        SelectionEnd = inputState.SelectionEnd
                    };
                },
                resultFormatter: value => value is null
                    ? "null"
                    : $"text={value.Text}; selection={value.SelectionStart}..{value.SelectionEnd}; " +
                      $"partial={value.PartialStartOffset}..{value.PartialEndOffset}");

        public override bool RequestCursorUpdates(int cursorUpdateMode)
            => Trace(
                "RequestCursorUpdates",
                "ImeInputConnection",
                $"cursorUpdateMode={cursorUpdateMode}",
                () =>
                {
                    if (session is null || !session.RequestCursorUpdates(cursorUpdateMode)) return false;
                    if ((cursorUpdateMode & 1) != 0) PublishCursorAnchor();
                    return true;
                });

        public override bool SetSelection(int start, int end)
            => Trace("SetSelection", "ImeInputConnection", $"start={start}; end={end}", () =>
            {
                if (session is not null) return session.Edit(target => target.SetSelection(start, end));
                var inputState = owner.GetTextInputState();
                if (start < 0 || end < 0 || start > inputState.Text.Length || end > inputState.Text.Length)
                    return false;

                owner.PublishSelection(start, end);
                return true;
            });

        [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
        public override SurroundingText? GetSurroundingText(int beforeLength, int afterLength, int flags)
            => Trace("GetSurroundingText", "ImeInputConnection", string.Empty, () =>
            {
                if (!OperatingSystem.IsAndroidVersionAtLeast(31) || session?.GetSurrounding(beforeLength, afterLength) is not { } state)
                    return null;
                return new SurroundingText(state.Text, state.SelectionStart - state.TextStart,
                    state.SelectionEnd - state.TextStart, state.TextStart);
            });

        public override global::Android.Text.CapitalizationMode GetCursorCapsMode(global::Android.Text.CapitalizationMode reqModes)
            => Trace<global::Android.Text.CapitalizationMode>("GetCursorCapsMode", "ImeInputConnection", string.Empty, () =>
            {
                if (session is null) return base.GetCursorCapsMode(reqModes);
                if (session.GetState() is not { } state || state.Options.Scope == TextInputScope.Password ||
                    state.Options.Capitalization == TextInputCapitalization.None) return 0;
                var offset = state.SelectionEnd - state.TextStart;
                if (offset < 0 || offset > state.Text.Length) return 0;
                return global::Android.Text.TextUtils.GetCapsMode(state.Text, offset, reqModes);
            });

        public override bool PerformEditorAction(ImeAction actionCode)
            => Trace("PerformEditorAction", "ImeInputConnection", $"action={actionCode}", () =>
            {
                if (session is null) return base.PerformEditorAction(actionCode);
                var action = actionCode switch
                {
                    ImeAction.Done => TextInputAction.Done,
                    ImeAction.Go => TextInputAction.Go,
                    ImeAction.Search => TextInputAction.Search,
                    ImeAction.Send => TextInputAction.Send,
                    ImeAction.Next => TextInputAction.Next,
                    ImeAction.Previous => TextInputAction.Previous,
                    ImeAction.None or ImeAction.Unspecified => TextInputAction.Default,
                    _ => (TextInputAction?)null
                };
                if (action is null) return false;
                var handled = session.Edit(target => target.PerformEditorAction(action.Value));
                if (!handled && action == TextInputAction.Done && IsCurrent)
                    return owner.SetKeyboardVisible(false);
                // Other actions belong to the existing host's focus/command policy. Never
                // synthesize an Enter key after a declined action and accidentally submit twice.
                return handled;
            });

        /// <inheritdoc/>
        public override bool PerformContextMenuAction(int id)
            => Trace("PerformContextMenuAction", "ImeInputConnection", $"id={id}", () =>
            {
                // IMEs can request Select all through this Android action instead of Ctrl+A.
                // Apply it to this connection's captured client, with no clipboard or focus lookup.
                // Other context actions keep BaseInputConnection's unsupported behavior.
                if (session is not null && id == global::Android.Resource.Id.SelectAll)
                    return session.SelectAll();
                return base.PerformContextMenuAction(id);
            });

        private static ICharSequence? ToJavaText(string? text)
            => text is null ? null : new Java.Lang.String(text);

        private string? GetLegacySelectedText()
        {
            var state = owner.GetTextInputState();
            return Math.Abs(state.SelectionEnd - state.SelectionStart) > TextInputState.MaximumTextLength
                ? null : state.GetSelectedText();
        }

        private static ExtractedText? CreateExtractedText(TextInputState? state)
            => state is null ? null : new ExtractedText
            {
                Text = new Java.Lang.String(state.Text),
                StartOffset = state.TextStart,
                PartialStartOffset = -1,
                PartialEndOffset = -1,
                SelectionStart = state.SelectionStart - state.TextStart,
                SelectionEnd = state.SelectionEnd - state.TextStart,
                Flags = state.Options.MultiLine ? 0 : ExtractedTextFlags.SingleLine
            };

        internal void PublishMonitoredState()
        {
            if (!IsCurrent || session is null || BatchDepth > 0) return;
            var state = session.GetState(0);
            if (state is not null && session.ShouldPublishExtractedText(state.Revision) &&
                session.ExtractedTextToken is { } token &&
                session.GetExtracted(session.ExtractedTextLimit) is { } snapshot &&
                CreateExtractedText(snapshot) is { } extracted)
            {
                using (extracted)
                {
                    owner.GetInputMethodManager()?.UpdateExtractedText(owner, token, extracted);
                    session.MarkExtractedTextPublished(snapshot.Revision);
                }
            }
            if (session.MonitorCursor || session.CursorUpdatePending) PublishCursorAnchor();
        }

        private void PublishCursorAnchor()
        {
            if (!IsCurrent || session?.GetState(0) is not { } state || !owner.IsAttachedToWindow) return;
            var caret = state.CaretRectangle;
            var bottom = caret.Y + caret.Height;
            var baseline = state.CaretBaseline ?? bottom;
            if (!double.IsFinite(bottom) || !double.IsFinite(baseline) ||
                Math.Abs(caret.X) > float.MaxValue || Math.Abs(caret.Y) > float.MaxValue ||
                Math.Abs(bottom) > float.MaxValue || Math.Abs(baseline) > float.MaxValue) return;
            var location = new int[2];
            owner.GetLocationOnScreen(location);
            using var matrix = new global::Android.Graphics.Matrix();
            matrix.SetScale(owner.Density, owner.Density);
            matrix.PostTranslate(location[0], location[1]);
            using var builder = new CursorAnchorInfo.Builder();
            builder.SetMatrix(matrix);
            builder.SetSelectionRange(state.SelectionStart, state.SelectionEnd);
            var visible = caret.X >= 0 && caret.Y >= 0 &&
                caret.X + caret.Width <= owner.Width / owner.Density &&
                caret.Y + caret.Height <= owner.Height / owner.Density;
            // Built-in editors supply the baseline of their actual rendered line. A custom
            // client may omit it; only then use the documented rectangle-bottom approximation.
            builder.SetInsertionMarkerLocation((float)caret.X, (float)caret.Y,
                (float)baseline, (float)bottom,
                visible ? CursorAnchorFlags.HasVisibleRegion : CursorAnchorFlags.HasInvisibleRegion);
            using var anchor = builder.Build();
            owner.GetInputMethodManager()?.UpdateCursorAnchorInfo(owner, anchor);
            session.MarkCursorPublished();
        }

        public override bool SendKeyEvent(NativeKeyEvent? e)
        {
            if (!IsCurrent) return false;
            var observation = e is null
                ? (KeyEventObservation?)null
                : owner.ObserveKeyEvent(e.KeyCode, e.Action == KeyEventActions.Down, "InputConnectionKeyEvent");
            return Trace(
                "SendKeyEvent",
                "ImeInputConnectionKeyEvent",
                e is null
                    ? "event=null"
                    : $"keyCode={e.KeyCode}; action={e.Action}; unicodeChar={e.UnicodeChar}; deviceId={e.DeviceId}",
                () =>
                {
                    // IMEs may send Shift+DPAD to extend a selection. Preserve those modifiers
                    // while explicitly retaining editing provenance, even for a real device id.
                    if (e is not null && (e.Action is KeyEventActions.Down or KeyEventActions.Up) &&
                        owner.PublishKey(e.KeyCode, e.Action == KeyEventActions.Down,
                        e, fromInputConnection: true))
                        return true;

                    if (!IsCurrent || !owner.CanRouteKeyboard || e is null) return false;
                    // BaseInputConnection queues a redispatch into ViewRoot without an IME
                    // pass. Preserve editing provenance if that event later reaches this view;
                    // never mutate the caller-owned native event or infer text from its key code.
                    using var editingEvent = NativeKeyEvent.ChangeFlags(e, e.Flags | KeyEventFlags.SoftKeyboard);
                    return base.SendKeyEvent(editingEvent);
                },
                operationKeyEvent: observation);
        }

        private T Trace<T>(
            string method,
            string source,
            string arguments,
            Func<T> operation,
            string? argumentText = null,
            int? newCursorPosition = null,
            Func<T, string?>? resultFormatter = null,
            KeyEventObservation? operationKeyEvent = null)
        {
            if (!IsCurrent) return default!;
            if (session is not null)
            {
                // StateChanged can run inside an editor callback. Defer Android notifications
                // until the shared edit has committed, and never flush a replacement connection.
                owner.nativeInputDepth++;
                try { return operation(); }
                finally
                {
                    owner.nativeInputDepth--;
                    if (IsCurrent && session.GetState(0) is { } current)
                    {
                        owner.lastTextInputRevision = current.Revision;
                        if (owner.nativeInputDepth == 0 && BatchDepth == 0 && owner.inputStateNotificationPending)
                            owner.NotifyClientStateChanged();
                    }
                    if (owner.EnableInputConnectionDiagnostics)
                    {
                        // The modern client has no legacy full-document snapshot. Keep this
                        // opt-in trace useful for native transport diagnosis without logging
                        // entered text; only identify exact newline payloads and text length.
                        var newline = argumentText switch { "\n" => "LF", "\r" => "CR", "\r\n" => "CRLF", _ => "none" };
                        // SendKeyEvent's legacy arguments contain printable key/Unicode codes.
                        // Keep those out of this metadata trace even when diagnostics are enabled.
                        var metadataArguments = method == nameof(SendKeyEvent) ? string.Empty : arguments;
                        var message = $"InputConnection {method}; source={source}; {metadataArguments}; textLength={argumentText?.Length}; newline={newline}; cursor={newCursorPosition}; active={!revoked}; batch={BatchDepth}";
                        global::Android.Util.Log.Info("MFN.InputConnection", message);
                        owner.InputConnectionDiagnosticSink?.Invoke(message);
                    }
                }
            }
            if (!owner.EnableInputConnectionDiagnostics)
                return operation();

            var before = owner.GetTextInputState();
            var batchDepthBefore = BatchDepth;
            var sameTextArgument = argumentText is not null && argumentText == lastTextArgument;
            T result = default!;
            string? exception = null;
            try
            {
                result = operation();
                return result;
            }
            catch (System.Exception error)
            {
                exception = error.ToString();
                throw;
            }
            finally
            {
                var after = owner.GetTextInputState();
                owner.WriteInputDiagnostic(
                    method,
                    source,
                    arguments,
                    before,
                    after,
                    batchDepthBefore,
                    BatchDepth,
                    exception is null ? resultFormatter?.Invoke(result) ?? result?.ToString() : null,
                    argumentText,
                    newCursorPosition,
                    sameTextArgument,
                    operationKeyEvent,
                    exception);
                if (argumentText is not null)
                    lastTextArgument = argumentText;
            }
        }
    }

}
