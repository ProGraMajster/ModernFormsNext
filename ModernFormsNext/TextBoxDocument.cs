using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext
{
    class TextBoxDocument
    {
        private readonly TextBox textbox;

        private string text = string.Empty;
        private string placeholder = string.Empty;

        private TextBlock? cached_text_block;
        private int[]? code_point_to_utf16_offsets;
        
        private bool enabled = true;
        private int cursor_index = 0;
        private int composition_start = -1;
        private int composition_end = -1;
        private bool read_only = false;
        private int selection_start = -1;
        private int selection_end = -1;
        private int max_length = int.MaxValue;
        private bool multiline = false;
        private long revision;
        private char? password_char;
        private int width = -1;
        private SKTypeface font = Theme.UIFont;
        private TextAlignment alignment = TextAlignment.Left;
        private SKColor placeholder_font_color = Theme.ForegroundDisabledColor;
        private SKColor selection_color = Theme.TextSelectionBackgroundColor;

        private static readonly string[] invalid_singleline_characters = new[] { "\r", "\n" };

        internal TextBoxDocument (TextBox textbox)
        {
            this.textbox = textbox;
            width = textbox.PaddedClientRectangle.Width;
        }

        public TextAlignment Alignment {
            get => alignment;
            set {
                if (alignment != value) {
                    alignment = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public bool AtBeginning => cursor_index == 0;

        public bool AtEnd => cursor_index == text.Length;

        public int CursorIndex => cursor_index;

        // The document and public TextBox API use UTF-16 offsets. RichTextKit shapes UTF-32 code
        // points, so every index crossing that boundary must be translated explicitly.
        public int CursorLayoutCodePointIndex => GetLayoutCodePointIndex (cursor_index);

        public int CompositionStart => composition_start;

        public int CompositionEnd => composition_end;

        public bool HasComposition => composition_start >= 0 && composition_end >= 0;

        public bool DeleteSelection ()
        {
            if (!IsTextSelected || read_only)
                return false;

            ClearComposition ();

            var start = Math.Min (selection_start, selection_end);
            var end = Math.Max (selection_start, selection_end);

            SetCursorToCharIndex (start);

            RemoveText (start, end - start);

            Deselect ();

            return true;
        }

        public bool DeleteText (bool forward, bool wholeWord)
        {
            // TODO: wholeWord not implemented
            if (read_only)
                return false;

            ClearComposition ();

            if (DeleteSelection ())
                return true;

            if (forward && !AtEnd) {
                // Delete one Unicode text element rather than one UTF-16 code unit. Android IMEs
                // commonly commit surrogate pairs, combining marks, and emoji modifier sequences;
                // splitting those values would leave invalid or visibly corrupted text behind.
                var length = StringInfo.GetNextTextElement (text, cursor_index).Length;
                RemoveText (cursor_index, length);
                return true;
            }

            if (!forward && !AtBeginning) {
                var previous = StringInfo.ParseCombiningCharacters (text)
                    .Last (index => index < cursor_index);
                var length = cursor_index - previous;
                SetCursorToCharIndex (previous);
                RemoveText (previous, length);

                return true;
            }

            return false;
        }

        public bool Deselect ()
        {
            if (!IsTextSelected)
                return false;

            selection_start = -1;
            selection_end = -1;
            revision++;

            return true;
        }

        public string DisplayText => text.Length == 0 ? placeholder :
                                     password_char.HasValue ? new string (password_char.Value, text.Length) : 
                                     text;

        public bool Enabled {
            get => enabled;
            set {
                if (enabled != value) {
                    enabled = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public SKTypeface Font {
            get => font;
            set {
                if (font != value) {
                    font = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public int GetUtf16IndexFromPosition (int x, int y)
        {
            var hit = GetTextBlock ().HitTest (x, y);
            return GetUtf16IndexFromLayoutCodePointIndex (hit.ClosestCodePointIndex);
        }

        public TextSelection GetTextSelection ()
        {
            if (!IsTextSelected)
                return TextSelection.Empty;

            var start = Math.Min (selection_start, selection_end);
            var end = Math.Max (selection_start, selection_end);
            return new TextSelection (
                GetLayoutCodePointIndex (start),
                GetLayoutCodePointIndex (end),
                selection_color);
        }

        public TextBlock GetTextBlock ()
        {
            if (cached_text_block != null)
                return cached_text_block;

            // Single-line text still needs the viewport width so Center/Right alignment
            // has a real box to align within. TextMeasurer keeps long single-line text
            // unwrapped when it does not fit, preserving horizontal scrolling behavior.
            var max_size = new Size (width, int.MaxValue);
            var color = !Enabled ? Theme.ForegroundDisabledColor :
                        Text.HasValue () ? textbox.CurrentStyle.GetForegroundColor () : 
                                placeholder_font_color;

            return cached_text_block = TextMeasurer.CreateTextBlock(DisplayText, textbox.CurrentStyle.GetFont(), textbox.CurrentFontSize, max_size, alignment, color, MaxLines, fontStyle: textbox.CurrentStyle.GetFontStyle());
        }

        public bool InsertText (string str)
        {
            if (read_only)
                return false;

            ClearComposition ();

            // Delete any currently selected text
            DeleteSelection ();

            str = StripInvalidCharacters (str);

            if (text.Length + str.Length > max_length)
                str = str.Substring (0, max_length - text.Length);

            text = text.Insert (cursor_index, str);
            cached_text_block = null;
            code_point_to_utf16_offsets = null;
            if (str.Length > 0)
                revision++;

            // Inserted text is kept intact so the cursor remains on the boundary after the
            // complete IME commit, including surrogate pairs and composed text.
            SetCursorToCharIndex (cursor_index + str.Length);

            return true;
        }

        public void Invalidate ()
        {
            textbox.Invalidate ();
        }

        internal void InvalidateTextBlock()
        {
            cached_text_block = null;
        }

        public bool IsMultiline {
            get => multiline;
            set {
                if (multiline != value) {
                    multiline = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public bool IsTextSelected => selection_start >= 0 && selection_end >= 0 && SelectionLength != 0;

        // Every text, caret, selection, or composition mutation advances this value. Platform input
        // bridges use it only as an observation token; TextBoxDocument remains the editable owner.
        public long Revision => revision;

        public ImeTextReplacement BeginImeTextReplacement ()
        {
            var start = HasComposition
                ? Math.Min (composition_start, composition_end)
                : IsTextSelected
                    ? Math.Min (selection_start, selection_end)
                    : cursor_index;
            var end = HasComposition
                ? Math.Max (composition_start, composition_end)
                : IsTextSelected
                    ? Math.Max (selection_start, selection_end)
                    : cursor_index;

            ClearComposition ();
            SetSelectionCore (start, end);
            return new ImeTextReplacement (start, text.Length - (end - start));
        }

        public void CompleteImeTextReplacement (
            ImeTextReplacement replacement,
            int newCursorPosition,
            bool keepComposition)
        {
            var insertedLength = Math.Clamp (
                text.Length - replacement.RetainedTextLength,
                0,
                text.Length - replacement.Start);
            var insertedEnd = replacement.Start + insertedLength;
            var requestedCursor = newCursorPosition > 0
                ? (long)insertedEnd + newCursorPosition - 1
                : (long)replacement.Start + newCursorPosition;
            var cursor = (int)Math.Clamp (requestedCursor, 0, text.Length);

            SetSelectionCore (cursor, cursor);
            if (keepComposition && insertedLength > 0)
                SetCompositionCore (replacement.Start, insertedEnd);
            else
                ClearComposition ();
        }

        public bool FinishComposition () => ClearComposition ();

        public bool SetCompositionRegion (int start, int end)
        {
            start = Math.Clamp (start, 0, text.Length);
            end = Math.Clamp (end, 0, text.Length);
            if (start > end)
                (start, end) = (end, start);

            if (start == end)
                return ClearComposition ();

            return SetCompositionCore (start, end);
        }

        public void SetImeSelection (int start, int end)
        {
            if (start < 0 || end < 0 || start > text.Length || end > text.Length)
                throw new ArgumentOutOfRangeException (nameof (start), "Selection indexes must be within the document text.");

            SetSelectionCore (start, end);
        }

        public int MaxLength {
            get => max_length == int.MaxValue ? 0 : max_length;
            set => max_length = value == 0 ? int.MaxValue : value;
        }

        private int? MaxLines => multiline ? (int?)null : 1;

        public bool MoveCursor (ArrowDirection direction, bool select, bool wholeWord, bool end)
        {
            if (!select)
                Deselect ();

            var new_index = -1;
            var block = GetTextBlock ();
            var current_code_point = CursorLayoutCodePointIndex;
            var current_caret = block.GetCaretInfo (new CaretPosition (current_code_point));
            
            switch (direction) {
                case ArrowDirection.Left:

                    // Ctrl-Home - Go to the beginning of the document
                    if (end && wholeWord)
                        new_index = GetUtf16IndexFromLayoutCodePointIndex (block.CaretIndicies.First ());
                    // Home - Go to the beginning of the current line
                    else if (end)
                        new_index = GetUtf16IndexFromLayoutCodePointIndex (
                            block.HitTest (0, current_caret.CaretRectangle.MidY).ClosestCodePointIndex);
                    // Ctrl-Left - Go left one word
                    else if (wholeWord)
                        new_index = TextMeasurer.FindNextSeparator (text, cursor_index, false);
                    // Left - Go left one character
                    else
                        new_index = GetAdjacentCaretUtf16Index (block, current_code_point, forward: false);

                    break;

                case ArrowDirection.Up:

                    // Multiline - Go up one line
                    if (multiline)
                        new_index = GetUtf16IndexFromPosition (
                            (int)current_caret.CaretXCoord,
                            (int)current_caret.CaretRectangle.MidY - textbox.CurrentFontSize);
                    // Single line - Go left one character
                    else
                        new_index = GetAdjacentCaretUtf16Index (block, current_code_point, forward: false);

                    break;

                case ArrowDirection.Right:

                    // Ctrl-End - Go to the end of the document
                    if (end && wholeWord)
                        new_index = GetUtf16IndexFromLayoutCodePointIndex (block.CaretIndicies.Last ());
                    // End - Go to the end of the current line
                    else if (end)
                        new_index = GetUtf16IndexFromLayoutCodePointIndex (
                            block.HitTest (int.MaxValue, current_caret.CaretRectangle.MidY).ClosestCodePointIndex);
                    // Ctrl-Right - Go right one word
                    else if (wholeWord)
                        new_index = TextMeasurer.FindNextSeparator (text, cursor_index, true);
                    // Right - Go right one character
                    else
                        new_index = GetAdjacentCaretUtf16Index (block, current_code_point, forward: true);

                    break;

                case ArrowDirection.Down:

                    // Multiline - Go down one line
                    if (multiline)
                        new_index = GetUtf16IndexFromPosition (
                            (int)current_caret.CaretXCoord,
                            (int)current_caret.CaretRectangle.MidY + textbox.CurrentFontSize);
                    // Single line - Go left one character
                    else
                        new_index = GetAdjacentCaretUtf16Index (block, current_code_point, forward: true);

                    break;
            }

            if (new_index != -1 && new_index != cursor_index) {
                var prev_index = cursor_index;
                SetCursorToCharIndex (new_index);

                if (!select || CursorIndex == SelectionStart) {
                    SelectionStart = -1;
                    SelectionEnd = -1;
                } else {
                    SelectionStart = (SelectionStart < 0 ? prev_index : SelectionStart);
                    SelectionEnd = new_index;
                }

                return true;
            }

            return false;
        }

        public char? PasswordCharacter {
            get => password_char;
            set {
                if (password_char != value) {
                    password_char = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public string Placeholder {
            get => placeholder;
            set {
                if (placeholder != value) {
                    placeholder = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public SKColor PlaceholderFontColor {
            get => placeholder_font_color;
            set {
                if (placeholder_font_color != value) {
                    placeholder_font_color = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public bool ReadOnly {
            get => read_only;
            set {
                if (read_only != value) {
                    read_only = value;
                    Invalidate ();
                }
            }
        }

        private void RemoveText (int start, int length)
        {
            text = text.Remove (start, length);
            cached_text_block = null;
            code_point_to_utf16_offsets = null;
            if (length > 0)
                revision++;
        }

        public void Reset () => cached_text_block = null;

        public void SelectAll ()
        {
            ClearComposition ();
            var changed = selection_start != 0 || selection_end != text.Length;
            selection_start = 0;
            selection_end = text.Length;
            if (changed)
                revision++;

            Invalidate ();
        }

        public string SelectedText => IsTextSelected ? text.Substring (Math.Min (selection_start, selection_end), SelectionLength) : string.Empty;

        public SKColor SelectionColor {
            get => selection_color;
            set {
                if (selection_color != value) {
                    selection_color = value;
                    Invalidate ();
                }
            }
        }

        public int SelectionEnd {
            get => selection_end;
            set {
                if (selection_end != value) {
                    ClearComposition ();
                    selection_end = value;
                    revision++;
                    Invalidate ();
                }
            }
        }

        public int SelectionLength => Math.Abs (selection_end - selection_start);

        public int SelectionStart {
            get => selection_start;
            set {
                if (selection_start != value) {
                    ClearComposition ();
                    selection_start = value;
                    revision++;
                    Invalidate ();
                }
            }
        }

        public bool SetCursorToCharIndex (int index)
        {
            ClearComposition ();
            if (cursor_index == index)
                return false;

            cursor_index = index;
            revision++;

            return true;
        }

        private string StripInvalidCharacters (string text)
        {
            if (multiline)
                return text;

            foreach (var c in invalid_singleline_characters)
                text = text.Replace (c, string.Empty);

            return text;
        }

        public string Text {
            get => text;
            set {
                if (text != value) {
                    ClearComposition ();
                    text = value;
                    cached_text_block = null;
                    code_point_to_utf16_offsets = null;
                    revision++;

                    // If the Text property is changed, we need to reset the cursor to the top
                    SetCursorToCharIndex (0);
                    Invalidate ();
                }
            }
        }

        public int Width {
            get => width;
            set {
                if (width != value) {
                    width = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        private bool ClearComposition ()
        {
            if (!HasComposition)
                return false;

            composition_start = -1;
            composition_end = -1;
            revision++;
            return true;
        }

        private bool SetCompositionCore (int start, int end)
        {
            if (composition_start == start && composition_end == end)
                return false;

            composition_start = start;
            composition_end = end;
            revision++;
            return true;
        }

        private void SetSelectionCore (int start, int end)
        {
            var storedStart = start == end ? -1 : start;
            var storedEnd = start == end ? -1 : end;
            if (cursor_index == end && selection_start == storedStart && selection_end == storedEnd)
                return;

            cursor_index = end;
            selection_start = storedStart;
            selection_end = storedEnd;
            revision++;
        }

        public int GetLayoutCodePointIndex (int utf16Index)
        {
            utf16Index = Math.Clamp (utf16Index, 0, text.Length);

            // Password rendering currently emits one mask glyph per UTF-16 unit. Preserve that
            // established display contract while normal text is translated to UTF-32 indices.
            if (password_char.HasValue)
                return utf16Index;

            var offsets = GetCodePointToUtf16Offsets ();
            var position = Array.BinarySearch (offsets, utf16Index);
            return position >= 0 ? position : Math.Max (0, ~position - 1);
        }

        public int GetUtf16IndexFromLayoutCodePointIndex (int codePointIndex)
        {
            if (password_char.HasValue)
                return Math.Clamp (codePointIndex, 0, text.Length);

            var offsets = GetCodePointToUtf16Offsets ();
            return offsets[Math.Clamp (codePointIndex, 0, offsets.Length - 1)];
        }

        private int GetAdjacentCaretUtf16Index (TextBlock block, int currentCodePoint, bool forward)
        {
            // CaretIndicies is ordered by logical code-point index. Binary search keeps a single
            // arrow-key press logarithmic even in large RichTextBox and Markdown documents.
            var low = 0;
            var high = block.CaretIndicies.Count - 1;
            while (low <= high) {
                var middle = low + ((high - low) / 2);
                if (block.CaretIndicies[middle] <= currentCodePoint)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            if (!forward && high >= 0 && block.CaretIndicies[high] == currentCodePoint)
                high--;

            var adjacentIndex = forward ? low : high;
            if (adjacentIndex < 0)
                return 0;
            if (adjacentIndex >= block.CaretIndicies.Count)
                return text.Length;

            return GetUtf16IndexFromLayoutCodePointIndex (block.CaretIndicies[adjacentIndex]);
        }

        private int[] GetCodePointToUtf16Offsets ()
        {
            if (code_point_to_utf16_offsets is not null)
                return code_point_to_utf16_offsets;

            var offsets = new List<int> { 0 };
            var utf16Offset = 0;
            foreach (var rune in text.EnumerateRunes ()) {
                utf16Offset += rune.Utf16SequenceLength;
                offsets.Add (utf16Offset);
            }

            return code_point_to_utf16_offsets = offsets.ToArray ();
        }

        public readonly record struct ImeTextReplacement (int Start, int RetainedTextLength);
    }
}
