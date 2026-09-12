using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Text;
using ModernFormsNext.WindowKit.Input;
using Topten.RichTextKit;
using Rect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext;

public partial class TextBox
{
    private readonly int textInputThreadId = Environment.CurrentManagedThreadId;
    private TextBoxInputClient? textInputClient;
    private TextInputOptions textInputOptions = new();

    /// <summary>Gets or sets native keyboard hints for this existing text editor.</summary>
    /// <remarks>
    /// Call on the UI thread. ReadOnly, MultiLine and PasswordCharacter remain authoritative;
    /// password input disables correction and capitalization. Hints do not validate text or
    /// guarantee native keyboard behavior. Changing hints refreshes the current input connection.
    /// </remarks>
    public TextInputOptions TextInputOptions
    {
        get { VerifyTextInputAccess(); return GetEffectiveTextInputOptions(); }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!Enum.IsDefined(value.Scope) || !Enum.IsDefined(value.Capitalization) || !Enum.IsDefined(value.Action))
                throw new ArgumentOutOfRangeException(nameof(value));
            VerifyTextInputAccess();
            if (textInputOptions == value) return;
            FinishTextInputBeforeExternalChange();
            bool wasSensitive = IsAccessibilitySensitive;
            textInputOptions = value;
            if (wasSensitive != IsAccessibilitySensitive) AccessibleTextPrivacyChanged();
            NotifyTextInputStateChanged();
        }
    }

    /// <summary>Occurs after a composition start, update, commit, finish or cancellation.</summary>
    /// <remarks>
    /// Raised on the UI thread after the final document ranges are applied. Event data contains
    /// metadata only. Native services obtain text separately through a bounded client snapshot.
    /// Nested semantic editing from this notification is rejected; schedule a later operation.
    /// </remarks>
    public event EventHandler<TextCompositionEventArgs>? TextCompositionChanged;

    /// <inheritdoc/>
    protected override ITextInputClient GetTextInputClient()
    {
        VerifyTextInputAccess();
        return textInputClient ??= new(this);
    }

    private void VerifyTextInputAccess()
    {
        if (Environment.CurrentManagedThreadId != textInputThreadId)
            throw new InvalidOperationException("Text input requires the editor's UI thread.");
    }

    /// <summary>Gets the shaped block used to paint and position this editor's text.</summary>
    /// <returns>The existing cached layout block; the caller does not own it.</returns>
    /// <remarks>Override when a derived editor renders a different styled block. Called on the UI thread.</remarks>
    protected virtual TextBlock GetTextInputLayoutBlock() => document.GetTextBlock();

    /// <summary>Captures derived formatting for the fragment replaced by a composition.</summary>
    /// <param name="start">Absolute UTF-16 fragment start.</param>
    /// <param name="length">Original fragment length.</param>
    /// <returns>An optional opaque, editor-owned checkpoint, never passed to the native backend.</returns>
    /// <remarks>Capture only affected formatting; do not clone or serialize the entire document.</remarks>
    protected virtual object? CaptureTextInputFormatting(int start, int length) => null;

    /// <summary>Restores a canceled composition's original fragment in the existing document.</summary>
    /// <param name="start">Current fragment start.</param>
    /// <param name="length">Current provisional fragment length.</param>
    /// <param name="text">Original text.</param>
    /// <param name="formatting">Opaque checkpoint returned by CaptureTextInputFormatting.</param>
    /// <remarks>Derived editors restore their formatting through the same document model. UI thread only.</remarks>
    protected virtual void RestoreTextInputFragment(int start, int length, string text, object? formatting)
    {
        var old = Text;
        document.RestoreTextInputFragment(start, length, text);
        Invalidate();
        if (old != Text) OnTextChanged(EventArgs.Empty);
    }

    internal virtual void BeginTextInputComposition() { }
    internal virtual void EndTextInputComposition(bool canceled) { }
    internal virtual void TextInputSelectionApplied() { }

    internal void EnterTextInputCallback() => textInputClient?.EnterCallback();
    internal void LeaveTextInputCallback() => textInputClient?.LeaveCallback();

    internal void BeforeTextInputDocumentMutation(bool forceExternal = false)
        => textInputClient?.BeforeDocumentMutation(forceExternal);

    internal void FinishTextInputBeforeExternalChange() => textInputClient?.ExternalChange();
    internal void NotifyTextInputStateChanged() => textInputClient?.NotifyStateChanged();

    private TextInputOptions GetEffectiveTextInputOptions()
    {
        var password = PasswordCharacter.HasValue || textInputOptions.Scope == TextInputScope.Password;
        return textInputOptions with {
            ReadOnly = ReadOnly,
            MultiLine = MultiLine,
            Scope = password ? TextInputScope.Password : textInputOptions.Scope,
            AutoCorrect = !password && textInputOptions.AutoCorrect,
            Capitalization = password ? TextInputCapitalization.None : textInputOptions.Capitalization
        };
    }

    private Rect GetTextInputCaretRectangle()
    {
        var block = GetTextInputLayoutBlock();
        var caret = TextMeasurer.GetCursorLocation(block, GetTextOrigin(block),
            document.CursorLayoutCodePointIndex, CurrentFontSize);
        // TextBox rendering uses its device-scaled client canvas. The shared input contract
        // lends local logical geometry; only the host converts this into window/screen space.
        var scale = ScaleFactor;
        return new Rect(caret.X / (double)scale.Width, caret.Y / (double)scale.Height,
            Math.Max(1, caret.Width) / (double)scale.Width, Math.Max(1, caret.Height) / (double)scale.Height);
    }

    private double? GetTextInputCaretBaseline()
    {
        var block = GetTextInputLayoutBlock();
        if (block.MeasuredHeight == 0) return null;
        var caret = block.GetCaretInfo(new CaretPosition(document.CursorLayoutCodePointIndex));
        if (caret.IsNone || caret.LineIndex < 0 || caret.LineIndex >= block.Lines.Count) return null;
        var line = block.Lines[caret.LineIndex];
        return (GetTextOrigin(block).Y + line.YCoord + line.BaseLine) / (double)ScaleFactor.Height;
    }

    /// <inheritdoc/>
    protected override void OnInvalidated(EventArgs<Rectangle> e)
    {
        try { base.OnInvalidated(e); }
        finally {
            try { NotifyTextInputStateChanged(); }
            finally { PublishAccessibleTextChanges(); }
        }
    }

    /// <inheritdoc/>
    protected override void OnTextChanged(EventArgs e)
    {
        textInputClient?.EnterCallback();
        try { base.OnTextChanged(e); }
        finally {
            textInputClient?.LeaveCallback();
            try { NotifyTextInputStateChanged(); }
            finally { PublishAccessibleTextChanges(); }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        try {
            if (disposing) {
                accessibleTextObserved = false;
                accessibleTextPublished = null;
                textInputClient?.Retire();
                TextCompositionChanged = null;
            }
        }
        finally { base.Dispose(disposing); }
    }

    private sealed class TextBoxInputClient : ITextInputClient
    {
        private readonly WeakReference<TextBox> owner;
        private readonly int threadId;
        private Checkpoint? checkpoint;
        private bool retired;
        private bool editing;
        private bool invalidEdit;
        private bool finishPending;
        private bool publishing;
        private bool notificationPending;
        private int callbackDepth;
        private Control? editParent;
        private WindowBase? editWindow;
        private bool editSelected;
        private (long Revision, Rect Caret, double? Baseline, TextInputOptions Options)? published;

        internal TextBoxInputClient(TextBox owner)
        {
            this.owner = new(owner);
            threadId = owner.textInputThreadId;
        }
        public event EventHandler? StateChanged;
        public event EventHandler<TextCompositionEventArgs>? CompositionChanged;

        internal void VerifyAccess()
        {
            if (Environment.CurrentManagedThreadId != threadId)
                throw new InvalidOperationException("Text input requires the editor's UI thread.");
        }

        private TextBox? LiveOwner(bool allowDisposing = false)
            => !retired && owner.TryGetTarget(out var control) && !control.IsDisposed && (allowDisposing || !control.Disposing)
                ? control : null;

        internal bool CanContinueEdit => !editing || (LiveOwner() is { } control && IsCurrent(control));
        private bool IsCurrent(TextBox control)
            => !invalidEdit && !retired && !control.IsDisposed && !control.Disposing && control.Enabled &&
               control.Visible && !control.ReadOnly && ReferenceEquals(control.Parent, editParent) &&
               ReferenceEquals(control.FindWindow(), editWindow) && control.Selected == editSelected &&
               editWindow?.InputBindingsClosed != true;

        internal void EnterCallback() => callbackDepth++;
        internal void LeaveCallback() => callbackDepth--;

        public TextInputState? GetState(int maximumTextLength = 4096, int? textStart = null)
        {
            VerifyAccess();
            if (maximumTextLength < 0 || maximumTextLength > TextInputState.MaximumTextLength)
                throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
            var control = LiveOwner();
            if (control is null) return null;
            var document = control.document;
            var text = document.Text;
            if (textStart is < 0 || textStart > text.Length)
                throw new ArgumentOutOfRangeException(nameof(textStart));
            var cursor = Math.Clamp(document.CursorIndex, 0, text.Length);
            var start = textStart ?? Math.Max(0, cursor - maximumTextLength / 2);
            start = TextBoxDocument.NormalizeUtf16Boundary(text, start, forward: false);
            var end = TextBoxDocument.NormalizeUtf16Boundary(text,
                start + Math.Min(maximumTextLength, text.Length - start), forward: false);
            var selectionStart = document.SelectionStart >= 0 ? document.SelectionStart : cursor;
            var selectionEnd = document.SelectionEnd >= 0 ? document.SelectionEnd : cursor;
            return new TextInputState(text.Substring(start, end - start), start, text.Length,
                Math.Clamp(selectionStart, 0, text.Length), Math.Clamp(selectionEnd, 0, text.Length),
                document.CompositionStart, document.CompositionEnd, document.Revision,
                control.GetTextInputCaretRectangle(), control.GetEffectiveTextInputOptions(), control.GetTextInputCaretBaseline());
        }

        public bool CommitText(string text, int newCursorPosition = 1)
            => ReplaceText(text, newCursorPosition, composing: false);
        public bool SetComposingText(string text, int newCursorPosition = 1)
            => ReplaceText(text, newCursorPosition, composing: true);

        private bool ReplaceText(string text, int newCursorPosition, bool composing)
        {
            ValidateUnicode(text);
            return Edit(control => {
                bool started = checkpoint is null;
                if (composing) EnsureCheckpoint(control);
                var replacement = control.document.BeginImeTextReplacement();
                if (!IsCurrent(control)) return false;
                if (text.Length > 0)
                    control.RaiseKeyPress(new KeyPressEventArgs(text));
                else
                    control.DeleteSelectedText();
                if (!IsCurrent(control)) return false;
                control.document.CompleteImeTextReplacement(replacement, newCursorPosition, composing);
                control.TextInputSelectionApplied();
                if (!IsCurrent(control)) return false;
                if (checkpoint is { } current) {
                    current.Length = Math.Max(0, control.Text.Length - current.RetainedLength);
                    current.Revision = control.document.Revision;
                }
                if (!composing) EndCheckpoint(control, canceled: false);
                control.ScrollToCaret();
                if (!IsCurrent(control)) return false;
                Publish(control, composing ? started ? TextCompositionStage.Started : TextCompositionStage.Updated
                    : TextCompositionStage.Committed);
                return true;
            });
        }

        public bool SetComposingRegion(int start, int end) => Edit(control => {
            var length = control.Text.Length;
            start = Math.Clamp(start, 0, length);
            end = Math.Clamp(end, 0, length);
            if (start > end) (start, end) = (end, start);
            if (start == end) start = end = TextBoxDocument.NormalizeUtf16Boundary(control.Text, start, false);
            else {
                start = TextBoxDocument.NormalizeUtf16Boundary(control.Text, start, false);
                end = TextBoxDocument.NormalizeUtf16Boundary(control.Text, end, true);
            }
            // A different native region starts a fresh checkpoint after accepting the old one.
            if (checkpoint is not null) EndCheckpoint(control, canceled: false);
            bool started = !control.document.HasComposition;
            control.document.SetCompositionRegion(start, end);
            if (start != end) EnsureCheckpoint(control, start, end);
            Publish(control, started ? TextCompositionStage.Started : TextCompositionStage.Updated);
            return true;
        });

        public bool SetSelection(int start, int end) => Edit(control => {
            control.document.SetImeSelection(start, end);
            if (checkpoint is { } current) current.Revision = control.document.Revision;
            control.TextInputSelectionApplied();
            control.ScrollToCaret();
            return true;
        }, writable: false);

        public bool FinishComposition()
        {
            VerifyAccess();
            // Native focus retirement can occur in a KeyPress/TextChanged callback. The old
            // session is already revoked; finish after that callback unwinds, never recursively.
            if (editing || publishing) {
                invalidEdit = true;
                finishPending = true;
                return false;
            }
            return Edit(control => {
                bool active = control.document.HasComposition || checkpoint is not null;
                control.document.FinishComposition();
                EndCheckpoint(control, canceled: false);
                if (active) Publish(control, TextCompositionStage.Finished);
                return true;
            }, writable: false, terminal: true);
        }

        public bool CancelComposition() => Edit(control => {
            if (checkpoint is not { } current) {
                control.document.FinishComposition();
                return true;
            }
            if (current.Revision != control.document.Revision) {
                control.document.FinishComposition();
                EndCheckpoint(control, canceled: false);
                return false;
            }
            // Detach the checkpoint before callbacks; a recursive external edit must not restore
            // it a second time. The editor's existing history transaction is still open here.
            checkpoint = null;
            try {
                control.RestoreTextInputFragment(current.Start, current.Length, current.Text, current.Formatting);
                if (!IsCurrent(control)) return false;
                control.document.SetImeSelection(current.Anchor, current.Caret);
                control.TextInputSelectionApplied();
            }
            finally { control.EndTextInputComposition(canceled: !invalidEdit); }
            control.ScrollToCaret();
            if (!IsCurrent(control)) return false;
            Publish(control, TextCompositionStage.Canceled);
            return true;
        });

        public bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(beforeLength);
            ArgumentOutOfRangeException.ThrowIfNegative(afterLength);
            return Edit(control => {
                control.document.FinishComposition();
                EndCheckpoint(control, canceled: false);
                var document = control.document;
                int anchor = document.SelectionStart >= 0 ? document.SelectionStart : document.CursorIndex;
                int caret = document.SelectionEnd >= 0 ? document.SelectionEnd : document.CursorIndex;
                bool reverse = anchor > caret;
                int start = Math.Min(anchor, caret), end = Math.Max(anchor, caret);
                document.SetImeSelection(start, start);
                for (int remaining = beforeLength; remaining > 0 && !document.AtBeginning;) {
                    string previous = control.Text;
                    int old = document.CursorIndex;
                    if (!control.DeleteText(false, false) || !IsCurrent(control)) return IsCurrent(control);
                    int removed = old - document.CursorIndex;
                    if (removed <= 0) break;
                    remaining -= inCodePoints ? CountScalars(previous.AsSpan(document.CursorIndex, removed)) : removed;
                }
                int delta = start - document.CursorIndex;
                start -= delta;
                end -= delta;
                document.SetImeSelection(end, end);
                for (int remaining = afterLength; remaining > 0 && !document.AtEnd;) {
                    string previous = control.Text;
                    if (!control.DeleteText(true, false) || !IsCurrent(control)) return IsCurrent(control);
                    int removed = previous.Length - control.Text.Length;
                    if (removed <= 0) break;
                    remaining -= inCodePoints ? CountScalars(previous.AsSpan(end, removed)) : removed;
                }
                document.SetImeSelection(reverse ? end : start, reverse ? start : end);
                control.TextInputSelectionApplied();
                control.ScrollToCaret();
                return IsCurrent(control);
            });
        }

        public bool PerformEditorAction(TextInputAction action)
        {
            VerifyAccess();
            if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (action is TextInputAction.Next or TextInputAction.Previous or TextInputAction.Done) return false;
            return Edit(control => {
                control.document.FinishComposition();
                EndCheckpoint(control, canceled: false);
                var args = new KeyEventArgs(Keys.Enter);
                control.RaiseKeyDown(args);
                if (IsCurrent(control) && !args.Handled)
                    control.RaiseKeyPress(new KeyPressEventArgs("\r"));
                return IsCurrent(control);
            });
        }

        private void EnsureCheckpoint(TextBox control, int? regionStart = null, int? regionEnd = null)
        {
            if (checkpoint is not null) return;
            var document = control.document;
            int anchor = Math.Clamp(document.SelectionStart >= 0 ? document.SelectionStart : document.CursorIndex, 0, control.Text.Length);
            int caret = Math.Clamp(document.SelectionEnd >= 0 ? document.SelectionEnd : document.CursorIndex, 0, control.Text.Length);
            bool reverse = anchor > caret;
            if (anchor == caret) anchor = caret = TextBoxDocument.NormalizeUtf16Boundary(control.Text, anchor, false);
            else {
                anchor = TextBoxDocument.NormalizeUtf16Boundary(control.Text, anchor, reverse);
                caret = TextBoxDocument.NormalizeUtf16Boundary(control.Text, caret, !reverse);
            }
            int start = regionStart ?? (document.HasComposition ? document.CompositionStart : Math.Min(anchor, caret));
            int end = regionEnd ?? (document.HasComposition ? document.CompositionEnd : Math.Max(anchor, caret));
            checkpoint = new(start, end - start, control.Text.Substring(start, end - start),
                control.Text.Length - (end - start), anchor, caret, document.Revision,
                control.CaptureTextInputFormatting(start, end - start));
            control.BeginTextInputComposition();
        }

        private void EndCheckpoint(TextBox control, bool canceled)
        {
            if (checkpoint is null) return;
            checkpoint = null;
            control.EndTextInputComposition(canceled);
        }

        internal void BeforeDocumentMutation(bool forceExternal)
        {
            if (forceExternal || callbackDepth > 0) {
                if (editing) invalidEdit = true;
                ExternalChange();
            }
            else if (!editing) ExternalChange();
        }

        internal void ExternalChange()
        {
            if (LiveOwner() is not { } control) return;
            if (editing && callbackDepth > 0) invalidEdit = true;
            bool active = checkpoint is not null || control.document.HasComposition;
            control.document.FinishComposition();
            EndCheckpoint(control, canceled: false);
            if (active && !editing) Publish(control, TextCompositionStage.Finished);
        }

        private bool Edit(Func<TextBox, bool> action, bool writable = true, bool terminal = false)
        {
            VerifyAccess();
            if (editing || publishing)
                return false;
            // Focus retirement accepts the already visible text even after an ancestor was
            // hidden/disabled or disposal started. This exception permits only terminal Finish;
            // selection and all text mutations still require an available live editor.
            if (LiveOwner(allowDisposing: terminal) is not { } control ||
                (!terminal && (!control.Enabled || !control.Visible || (writable && control.ReadOnly))))
                return false;
            editing = true;
            using var accessibleChange = control.BeginAccessibleTextChange();
            invalidEdit = false;
            editParent = control.Parent;
            editWindow = control.FindWindow();
            editSelected = control.Selected;
            var previousComposition = (control.document.CompositionStart, control.document.CompositionEnd);
            Exception? failure = null;
            bool result = false;
            try { result = action(control); }
            catch (Exception exception) { failure = exception; invalidEdit = true; }
            finally {
                try {
                    if (invalidEdit || finishPending || !ReferenceEquals(control.Parent, editParent) || control.Selected != editSelected ||
                        control.IsDisposed || (!terminal && control.Disposing) || editWindow?.InputBindingsClosed == true) {
                        control.document.FinishComposition();
                        if (!control.IsDisposed && !control.Disposing) EndCheckpoint(control, canceled: false);
                        else checkpoint = null;
                        result = false;
                    }
                }
                catch (Exception cleanup) { failure = Combine(failure, cleanup); }
                finishPending = false;
                try {
                    // Region-only changes and failure cleanup do not change Text, but they
                    // must repaint the provisional underline after the final range is known.
                    if (!control.IsDisposed && !control.Disposing && previousComposition !=
                        (control.document.CompositionStart, control.document.CompositionEnd))
                        control.Invalidate();
                }
                catch (Exception invalidation) { failure = Combine(failure, invalidation); }
                editing = false;
                editParent = null;
                editWindow = null;
                try { NotifyStateChanged(); }
                catch (Exception notification) { failure = Combine(failure, notification); }
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }

        internal void NotifyStateChanged()
        {
            if (editing || publishing) { notificationPending = true; return; }
            Exception? failure = null;
            int attempts = 0;
            do {
                notificationPending = false;
                if (LiveOwner() is not { } control) break;
                var value = (control.document.Revision, control.GetTextInputCaretRectangle(),
                    control.GetTextInputCaretBaseline(), control.GetEffectiveTextInputOptions());
                if (published == value) break;
                published = value;
                publishing = true;
                EnterCallback();
                try {
                    foreach (var handler in StateChanged?.GetInvocationList() ?? [])
                        try { ((EventHandler)handler)(this, EventArgs.Empty); }
                        catch (Exception exception) { failure = Combine(failure, exception); }
                }
                finally { LeaveCallback(); publishing = false; }
                try { CompletePendingFinish(); }
                catch (Exception cleanup) { failure = Combine(failure, cleanup); }
                if (failure is not null) {
                    // Native observers must not leave a half-live composition after rejecting
                    // the updated state. Accept visible text, release rollback/history, then fail.
                    failure = FinishAfterNotificationFailure(control, failure);
                    break;
                }
                if (++attempts >= 32 && notificationPending) {
                    failure = new InvalidOperationException("Text input state did not settle after 32 notifications.");
                    failure = FinishAfterNotificationFailure(control, failure);
                    break;
                }
            } while (notificationPending);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private Exception FinishAfterNotificationFailure(TextBox control, Exception failure)
        {
            publishing = true;
            try {
                control.document.FinishComposition();
                if (!control.IsDisposed && !control.Disposing) {
                    EndCheckpoint(control, canceled: false);
                    control.Invalidate();
                }
                else checkpoint = null;
            }
            catch (Exception cleanup) { failure = Combine(failure, cleanup); }
            finally { publishing = false; notificationPending = false; finishPending = false; }
            return failure;
        }

        private void Publish(TextBox control, TextCompositionStage stage)
        {
            var args = new TextCompositionEventArgs(stage, control.document.CompositionStart,
                control.document.CompositionEnd, control.document.Revision);
            Exception? failure = null;
            bool wasPublishing = publishing;
            publishing = true;
            EnterCallback();
            try {
                // Metadata-only transitions (including external focus retirement) must also
                // invalidate the adornment. Coalesce native state notifications until done.
                try {
                    if (!control.IsDisposed && !control.Disposing) control.Invalidate();
                }
                catch (Exception exception) { failure = Combine(failure, exception); }
                foreach (var handler in CompositionChanged?.GetInvocationList() ?? [])
                    try { ((EventHandler<TextCompositionEventArgs>)handler)(this, args); }
                    catch (Exception exception) { failure = Combine(failure, exception); }
                foreach (var handler in control.TextCompositionChanged?.GetInvocationList() ?? [])
                    try { ((EventHandler<TextCompositionEventArgs>)handler)(control, args); }
                    catch (Exception exception) { failure = Combine(failure, exception); }
            }
            finally { LeaveCallback(); publishing = wasPublishing; }
            if (!editing && !publishing) {
                try { CompletePendingFinish(); }
                catch (Exception cleanup) { failure = Combine(failure, cleanup); }
                try { NotifyStateChanged(); }
                catch (Exception notification) { failure = Combine(failure, notification); }
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void CompletePendingFinish()
        {
            if (!finishPending || editing || publishing) return;
            // A native observer can retire focus while publishing a state change, after the
            // edit has already ended. Release its checkpoint as soon as observers unwind too.
            finishPending = false;
            FinishComposition();
        }

        internal void Retire()
        {
            retired = true;
            invalidEdit = true;
            finishPending = false;
            checkpoint = null;
            owner.SetTarget(null!);
            StateChanged = null;
            CompositionChanged = null;
            published = null;
        }

        private static Exception Combine(Exception? first, Exception next)
            => first is null ? next : new AggregateException("Text input and mandatory cleanup both failed.", first, next);

        private static void ValidateUnicode(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            for (int i = 0; i < text.Length; i++) {
                if (!char.IsSurrogate(text[i])) continue;
                if (!char.IsHighSurrogate(text[i]) || i + 1 >= text.Length || !char.IsLowSurrogate(text[++i]))
                    throw new ArgumentException("Text input must contain complete Unicode scalars.", nameof(text));
            }
        }

        private static int CountScalars(ReadOnlySpan<char> value)
        {
            int count = 0;
            foreach (var rune in value.EnumerateRunes()) count++;
            return count;
        }

        private sealed class Checkpoint(int start, int length, string text, int retainedLength,
            int anchor, int caret, long revision, object? formatting)
        {
            internal int Start { get; } = start;
            internal int Length { get; set; } = length;
            internal string Text { get; } = text;
            internal int RetainedLength { get; } = retainedLength;
            internal int Anchor { get; } = anchor;
            internal int Caret { get; } = caret;
            internal long Revision { get; set; } = revision;
            internal object? Formatting { get; } = formatting;
        }
    }
}
