using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using Topten.RichTextKit;

namespace ModernFormsNext;

public partial class TextBox
{
    private bool accessibleTextObserved;
    private int accessibleTextDepth;
    private bool accessibleTextPublishing;
    private bool accessibleTextPending;
    private long accessibleFormatRevision;
    private (long Text, int Anchor, int Caret, long Format, Point Origin, Size Size)? accessibleTextPublished;

    internal bool IsAccessibilitySensitive => PasswordCharacter.HasValue || textInputOptions.Scope == TextInputScope.Password;

    internal AccessibleTextProvider CreateAccessibleTextProvider(AccessibleObject peer)
    {
        VerifyTextInputAccess();
        accessibleTextObserved = true;
        _ = document.AccessibilityEdits;
        accessibleTextPublished ??= CaptureAccessibleTextState();
        return new TextBoxAccessibleTextProvider(this, peer);
    }

    internal void VerifyAccessibleTextAccess() => VerifyTextInputAccess();
    internal TextBlock AccessibleTextLayout => GetTextInputLayoutBlock();
    internal Rectangle AccessibleTextViewport => PaddedClientRectangle;
    internal Point AccessibleTextOrigin => GetTextOrigin(GetTextInputLayoutBlock());
    internal int AccessibleTextHitTest(Point point) => GetTextIndexFromPosition(point);

    internal AccessibleTextChangeScope BeginAccessibleTextChange()
    {
        accessibleTextDepth++;
        return new(this);
    }

    internal readonly struct AccessibleTextChangeScope(TextBox owner) : IDisposable
    {
        public void Dispose()
        {
            owner.accessibleTextDepth--;
            owner.PublishAccessibleTextChanges();
        }
    }

    private (long Text, int Anchor, int Caret, long Format, Point Origin, Size Size) CaptureAccessibleTextState()
        => (document.AccessibilityEdits.Revision,
            document.SelectionStart >= 0 ? document.SelectionStart : document.CursorIndex,
            document.SelectionEnd >= 0 ? document.SelectionEnd : document.CursorIndex,
            accessibleFormatRevision, AccessibleTextOrigin, PaddedClientRectangle.Size);

    internal void AccessibleTextFormattingChanged()
    {
        accessibleFormatRevision++;
        PublishAccessibleTextChanges();
    }

    private void AccessibleTextPrivacyChanged()
    {
        if (!accessibleTextObserved) return;
        document.AccessibilityEdits.Reset();
        accessibleTextPublished = null;
        NotifyAccessibilityClients(AccessibleEvents.StateChange);
    }

    internal void PublishAccessibleTextChanges()
    {
        if (accessibleTextDepth != 0 || accessibleTextPublishing) { accessibleTextPending = true; return; }
        Exception? failure = null;
        try { NotifyAccessibleScrollChanged(); }
        catch (Exception exception) { failure = exception; }
        if (!accessibleTextObserved || IsDisposed || Disposing || IsAccessibilitySensitive) {
            if (IsAccessibilitySensitive) accessibleTextPublished = null;
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            return;
        }
        for (int pass = 0; pass < 16; pass++) {
            accessibleTextPending = false;
            var current = CaptureAccessibleTextState();
            var previous = accessibleTextPublished;
            accessibleTextPublished = current;
            accessibleTextPublishing = true;
            try {
                void Notify(AccessibleEvents kind)
                {
                    if (IsDisposed || Disposing || IsAccessibilitySensitive) return;
                    try { NotifyAccessibilityClients(kind); }
                    catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
                }
                if (previous is not null && previous.Value.Text != current.Text) Notify(AccessibleEvents.TextChanged);
                if (previous is not null && (previous.Value.Anchor != current.Anchor || previous.Value.Caret != current.Caret))
                    Notify(AccessibleEvents.TextSelectionChanged);
                if (previous is not null && (previous.Value.Format != current.Format || previous.Value.Origin != current.Origin || previous.Value.Size != current.Size))
                    Notify(AccessibleEvents.TextAttributesChanged);
            }
            finally { accessibleTextPublishing = false; }
            if (!accessibleTextPending || IsDisposed || Disposing || IsAccessibilitySensitive) break;
            if (pass == 15) {
                var unsettled = new InvalidOperationException("Accessible text notifications did not settle after 16 changes.");
                failure = failure is null ? unsettled : new AggregateException(failure, unsettled);
            }
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal bool SetAccessibleTextSelection(int start, int end)
    {
        VerifyTextInputAccess();
        if (IsDisposed || Disposing || !Enabled || !Visible || IsAccessibilitySensitive) return false;
        var lifetime = new AccessibilityControlLifetime(this);
        using var change = BeginAccessibleTextChange();
        FinishTextInputBeforeExternalChange();
        if (IsDisposed || Disposing || !Enabled || !Visible || IsAccessibilitySensitive ||
            !lifetime.IsCurrent) return false;
        // Finished is an application callback. It can protect an ancestor while retaining this
        // editor and its tree identity, so the earlier provider check no longer authorizes a
        // subsequent accessibility selection. Accept the visible preedit but stop that mutation.
        if (TextBoxAccessibleTextProvider.HasSensitiveAncestor(AccessibilityObject)
            || !lifetime.IsCurrent || !Enabled || !Visible || IsAccessibilitySensitive) return false;
        // The stable editor adapter retains derived selection hooks and Unicode normalization;
        // it is not the host's expiring focus proxy and cannot retarget another control.
        return QueryTextInputClient()!.SetSelection(start, end);
    }

    internal bool ScrollAccessibleText(RectangleF rectangle, bool alignToTop)
    {
        VerifyTextInputAccess();
        if (IsDisposed || Disposing || !Enabled || !Visible || IsAccessibilitySensitive) return false;
        var viewport = PaddedClientRectangle;
        if (viewport.Width <= 0 || viewport.Height <= 0) return true;
        int dx = rectangle.Left < viewport.Left ? (int)Math.Floor(rectangle.Left - viewport.Left)
            : rectangle.Right > viewport.Right ? (int)Math.Ceiling(rectangle.Right - viewport.Right) : 0;
        int dy = (int)Math.Round(alignToTop ? rectangle.Top - viewport.Top : rectangle.Bottom - viewport.Bottom);
        var block = GetTextInputLayoutBlock();
        int maxX = Math.Max(0, (int)Math.Ceiling(block.MeasuredWidth) - viewport.Width);
        int maxY = Math.Max(0, (int)Math.Ceiling(block.MeasuredHeight) - viewport.Height);
        int x = (int)Math.Clamp((long)scroll_x + dx, 0, maxX);
        int y = (int)Math.Clamp((long)scroll_y + dy, 0, maxY);
        DoScroll(x - scroll_x, y - scroll_y);
        return true;
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        if (document is null) { base.OnThemeChanged(e); return; }
        using var change = BeginAccessibleTextChange();
        document.Reset();
        InvalidateAccessibleTextLayout();
        base.OnThemeChanged(e);
        if (!IsDisposed && !Disposing) {
            document.Reset();
            InvalidateAccessibleTextLayout();
            UpdateScrollBars(GetTextInputLayoutBlock());
            AccessibleTextFormattingChanged();
            Invalidate();
        }
    }

    /// <summary>Invalidates a derived editor's cached styled layout after effective theme changes.</summary>
    /// <remarks>Called on the UI thread before accessible geometry is republished.</remarks>
    protected virtual void InvalidateAccessibleTextLayout() { }
}
