using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ModernFormsNext;

/// <summary>
/// Hosts the shared RichTextBox editing core for <see cref="MarkdownEditor"/>.
/// </summary>
internal sealed class MarkdownEditorTextBox : RichTextBox
{
    // RichTextKit currently lays out every presentation run eagerly. Keeping a single run for
    // very large sources preserves responsive editing until viewport-based rich-text layout is
    // available in the shared editing core.
    internal const int MaximumHighlightedSourceLength = 200_000;
    private readonly MarkdownEditor owner;
    private readonly MarkdownSourceHighlighter highlighter = new();
    private bool acceptsReturn = true;
    private bool suppressMarkdownEnterKeyPress;
    private bool suppressMarkdownTabKeyPress;
    private bool wordWrap = true;

    public MarkdownEditorTextBox(MarkdownEditor owner)
    {
        this.owner = owner;
        Dock = DockStyle.Fill;
        DetectUrls = false;
    }

    public bool AcceptsReturn
    {
        get => acceptsReturn;
        set => acceptsReturn = value;
    }

    public SkiaSharp.SKColor CaretColor => owner.SyntaxStyle.ResolveCaret(this);

    public bool WordWrap
    {
        get => wordWrap;
        set
        {
            if (wordWrap == value)
                return;

            wordWrap = value;
            UpdateDocumentWidth();
            RefreshSyntaxHighlighting();
        }
    }

    internal bool IsDetailedSyntaxHighlightingActive { get; private set; } = true;

    public void RefreshSyntaxHighlighting()
    {
        Style.BackgroundColor = owner.SyntaxStyle.ResolveBackground(this);
        Style.ForegroundColor = owner.SyntaxStyle.ResolveForeground(this);
        document.SelectionColor = owner.SyntaxStyle.ResolveSelectionBackground();

        if (Text.Length == 0)
        {
            IsDetailedSyntaxHighlightingActive = true;
            SetPresentationRuns(Array.Empty<RichTextBoxTextRun>());
            return;
        }

        var defaultStyle = CreateSourceStyle(MarkdownSourceSpanKind.LinkTarget, useDefaultColor: true);
        if (Text.Length > MaximumHighlightedSourceLength)
        {
            IsDetailedSyntaxHighlightingActive = false;
            SetPresentationRuns(new[] { new RichTextBoxTextRun(0, Text.Length, defaultStyle) });
            return;
        }

        IsDetailedSyntaxHighlightingActive = true;

        var spans = highlighter.Highlight(Text)
            .OrderBy(span => span.Start)
            .ThenByDescending(span => span.Length)
            .ToArray();
        var runs = new List<RichTextBoxTextRun>(spans.Length * 2 + 1);
        var cursor = 0;
        var syntaxStyles = Enum.GetValues<MarkdownSourceSpanKind>()
            .ToDictionary(kind => kind, kind => CreateSourceStyle(kind, useDefaultColor: false));

        foreach (var span in spans)
        {
            var spanEnd = Math.Min(Text.Length, span.End);
            if (span.Start >= Text.Length || spanEnd <= cursor)
                continue;

            if (span.Start > cursor)
                runs.Add(new RichTextBoxTextRun(cursor, span.Start - cursor, defaultStyle));

            var start = Math.Max(cursor, span.Start);
            runs.Add(new RichTextBoxTextRun(
                start,
                spanEnd - start,
                syntaxStyles[span.Kind]));
            cursor = spanEnd;
        }

        if (cursor < Text.Length)
            runs.Add(new RichTextBoxTextRun(cursor, Text.Length - cursor, defaultStyle));

        SetPresentationRuns(runs);
    }

    protected override bool DeleteSelectedText()
        => owner.TrackSurfaceEdit(() => base.DeleteSelectedText(), MarkdownEditKind.Delete);

    protected override bool DeleteText(bool forward, bool wholeWord)
    {
        if (SelectionLength == 0 && !wholeWord)
        {
            var caret = document.CursorIndex;
            if (forward
                && caret + 1 < Text.Length
                && char.IsHighSurrogate(Text[caret])
                && char.IsLowSurrogate(Text[caret + 1]))
            {
                Select(caret, 2);
                return owner.TrackSurfaceEdit(() => base.DeleteSelectedText(), MarkdownEditKind.Delete);
            }

            if (!forward
                && caret >= 2
                && char.IsHighSurrogate(Text[caret - 2])
                && char.IsLowSurrogate(Text[caret - 1]))
            {
                Select(caret - 2, 2);
                return owner.TrackSurfaceEdit(() => base.DeleteSelectedText(), MarkdownEditKind.Delete);
            }
        }

        return owner.TrackSurfaceEdit(() => base.DeleteText(forward, wholeWord), MarkdownEditKind.Delete);
    }

    protected override bool InsertText(string text)
        => owner.TrackSurfaceEdit(() => base.InsertText(text), MarkdownEditKind.Typing);

    protected override void OnDoubleClick(MouseEventArgs e)
    {
        base.OnDoubleClick(e);

        if (e.Button != MouseButtons.Left || Text.Length == 0)
            return;

        var index = Math.Clamp(GetTextIndexFromPosition(e.Location), 0, Text.Length);
        if (index == Text.Length)
            index--;

        if (char.IsHighSurrogate(Text[index]) && index + 1 < Text.Length && char.IsLowSurrogate(Text[index + 1]))
        {
            Select(index, 2);
            return;
        }

        if (char.IsLowSurrogate(Text[index]) && index > 0 && char.IsHighSurrogate(Text[index - 1]))
        {
            Select(index - 1, 2);
            return;
        }

        var start = index;
        var end = index + 1;
        var wordCharacter = IsWordCharacter(Text[index]);

        while (start > 0 && IsWordCharacter(Text[start - 1]) == wordCharacter)
            start--;
        while (end < Text.Length && IsWordCharacter(Text[end]) == wordCharacter)
            end++;

        Select(start, end - start);
    }

    protected override bool ProcessTextBoxKeyDown(KeyEventArgs e)
    {
        if (e.IsShortcutControlPressed && e.KeyCode == Keys.Z)
        {
            if (e.Shift)
                owner.Redo();
            else
                owner.Undo();

            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.KeyCode == Keys.Y)
        {
            owner.Redo();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.Shift && e.KeyCode == Keys.X)
        {
            owner.ToggleStrikethrough();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && !e.Shift && e.KeyCode == Keys.X)
        {
            owner.Cut();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.KeyCode == Keys.V)
        {
            owner.Paste();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.KeyCode == Keys.B)
        {
            owner.ToggleBold();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.KeyCode == Keys.I)
        {
            owner.ToggleItalic();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && !e.Shift && e.KeyCode == Keys.K)
        {
            if (!owner.ReadOnly)
                owner.RequestInsertLink();

            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.Shift && e.KeyCode == Keys.D7)
        {
            owner.ToggleOrderedList();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && e.Shift && e.KeyCode == Keys.D8)
        {
            owner.ToggleUnorderedList();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.IsShortcutControlPressed && !e.Shift && e.KeyCode == Keys.Oemtilde)
        {
            owner.ToggleInlineCode();
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.KeyCode == Keys.Tab && !e.Control && !e.Alt && AcceptsTab
            && owner.TryHandleListIndent(e.Shift))
        {
            suppressMarkdownTabKeyPress = true;
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.KeyCode == Keys.Enter && AcceptsReturn && owner.TryHandleMarkdownEnter())
        {
            suppressMarkdownEnterKeyPress = true;
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.KeyCode == Keys.Back && !e.Control && !e.Alt && owner.TryHandleMarkdownBackspace())
        {
            e.SuppressKeyPress = true;
            return true;
        }

        if (e.KeyCode == Keys.Enter && !AcceptsReturn)
            return false;

        return base.ProcessTextBoxKeyDown(e);
    }

    protected override bool ProcessTextBoxKeyPress(KeyPressEventArgs e)
    {
        if (suppressMarkdownEnterKeyPress)
        {
            suppressMarkdownEnterKeyPress = false;
            if (e.KeyChar is '\r' or '\n')
            {
                e.Handled = true;
                return true;
            }
        }

        if (suppressMarkdownTabKeyPress)
        {
            suppressMarkdownTabKeyPress = false;
            if (e.KeyChar == '\t')
            {
                e.Handled = true;
                return true;
            }
        }

        if (!AcceptsReturn && e.KeyChar is '\r' or '\n')
        {
            e.Handled = true;
            return true;
        }

        return base.ProcessTextBoxKeyPress(e);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateDocumentWidth();
    }

    protected internal override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        RefreshSyntaxHighlighting();
    }

    private RichTextBoxTextStyle CreateSourceStyle(MarkdownSourceSpanKind kind, bool useDefaultColor)
    {
        var baseFont = Font;
        var fontStyle = kind switch
        {
            MarkdownSourceSpanKind.HeadingMarker => baseFont.Style | FontStyle.Bold,
            MarkdownSourceSpanKind.LinkText => baseFont.Style | FontStyle.Underline,
            _ => baseFont.Style
        };
        var family = kind == MarkdownSourceSpanKind.CodeMarker ? "Consolas" : baseFont.FamilyName;

        return new RichTextBoxTextStyle
        {
            Font = new Font(family, baseFont.SizeInPoints, fontStyle),
            ForeColor = useDefaultColor
                ? owner.SyntaxStyle.ResolveForeground(this)
                : owner.SyntaxStyle.Resolve(kind, this)
        };
    }

    private static bool IsWordCharacter(char value)
        => char.IsLetterOrDigit(value) || value == '_';

    private void UpdateDocumentWidth()
    {
        document.Width = WordWrap ? PaddedClientRectangle.Width : int.MaxValue;
        document.InvalidateTextBlock();
        Invalidate();
    }
}
