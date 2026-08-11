using System;
using System.Drawing;
using ModernFormsNext.Renderers;
using ModernFormsNext.WindowKit.Input.Platform;
using Topten.RichTextKit;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a TextBox control.
    /// </summary>
    public class TextBox : ScrollControl
    {
        internal readonly TextBoxDocument document;

        private bool is_highlighting;
        private int selection_anchor = -1;
        private int scroll_x = 0;
        private int scroll_y = 0;
        private ContentAlignment text_align = ContentAlignment.MiddleLeft;

        /// <summary>
        /// Initializes a new instance of the TextBox class.
        /// </summary>
        public TextBox ()
        {
            Cursor = Cursors.IBeam;

            document = new TextBoxDocument (this);

            VerticalScrollBar.Enabled = false;
            VerticalScrollBar.ValueChanged += (o, e) => DoScroll (0, (o as VerticalScrollBar)!.Value - scroll_y);
        }

        /// <summary>
        /// Copies the selected text of the TextBox to the clipboard.
        /// </summary>
        public void Copy ()
        {
            if (!document.IsTextSelected)
                return;

            var text = GetSelectedTextForClipboard ();
            AsyncHelper.RunSync (() => ModernFormsNext.WindowKit.AvaloniaGlobals.GetRequiredService<IClipboard> ().SetTextAsync (text));
        }

        // The scaled height of the current font.
        internal int CurrentFontSize => LogicalToDeviceUnits (CurrentStyle.GetFontSize ());

        /// <summary>
        /// Copies the selected text of the TextBox to the clipboard and removes it from the TextBox.
        /// </summary>
        public void Cut ()
        {
            if (!document.IsTextSelected)
                return;

            var text = GetSelectedTextForClipboard ();
            AsyncHelper.RunSync (() => ModernFormsNext.WindowKit.AvaloniaGlobals.GetRequiredService<IClipboard> ().SetTextAsync (text));

            DeleteSelectedText ();
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (1, 0, 0, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 25);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        // Scrolls the TextBox by the specified amounts.
        private void DoScroll (int x, int y)
        {
            scroll_x += x;
            scroll_y += y;

            Invalidate ();
        }

        // Gets the index of the character at the specified location.
        /// <summary>
        /// Gets the UTF-16 text index nearest to a client-coordinate location.
        /// </summary>
        /// <param name="location">The location in control client coordinates.</param>
        /// <returns>The nearest UTF-16 index in <see cref="Text"/>.</returns>
        /// <remarks>
        /// Derived source editors use this hook for gestures such as word selection while the
        /// shared text document remains responsible for hit testing and DPI-aware text layout.
        /// Overrides that render a separate styled text block must hit-test that same block and
        /// convert its layout code-point index back to the control's UTF-16 document index.
        /// </remarks>
        protected virtual int GetTextIndexFromPosition (Point location)
        {
            if (!document.Text.HasValue ())
                return 0;

            return document.GetUtf16IndexFromPosition (location.X - TextOrigin.X, location.Y - TextOrigin.Y);
        }

        /// <summary>
        /// Deletes text from the control.
        /// </summary>
        /// <param name="forward">
        /// <see langword="true"/> to delete the character after the caret; <see langword="false"/> to delete the character before the caret.
        /// </param>
        /// <param name="wholeWord">
        /// <see langword="true"/> when the caller requested word-based deletion. The base <see cref="TextBox"/> currently treats this as character deletion.
        /// </param>
        /// <returns><see langword="true"/> when text was removed; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Derived text controls override this method when they need to keep an internal document model synchronized with keyboard deletion.
        /// Implementations should invalidate rendering when the displayed text changes.
        /// </remarks>
        protected virtual bool DeleteText (bool forward, bool wholeWord) => ApplyTextEdit (() => document.DeleteText (forward, wholeWord));

        /// <summary>
        /// Deletes the selected text from the control.
        /// </summary>
        /// <returns><see langword="true"/> when selected text was removed; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// This method is used by keyboard editing and clipboard cut operations. Derived controls should override it when deleting selected text
        /// requires additional validation or model updates.
        /// </remarks>
        protected virtual bool DeleteSelectedText () => ApplyTextEdit (() => document.DeleteSelection ());

        // Handles key down events.
        private bool HandleKeyDown (KeyEventArgs e)
        {
            var need_refresh = false;
            var shortcut_control = e.IsShortcutControlPressed;

            try {
                switch (e.KeyData & Keys.KeyCode) {
                    case Keys.Left:
                        need_refresh = document.MoveCursor (ArrowDirection.Left, e.Shift, shortcut_control, false);
                        return true;
                    case Keys.Right:
                        need_refresh = document.MoveCursor (ArrowDirection.Right, e.Shift, shortcut_control, false);
                        return true;
                    case Keys.Home:
                        need_refresh = document.MoveCursor (ArrowDirection.Left, e.Shift, shortcut_control, true);
                        return true;
                    case Keys.End:
                        need_refresh = document.MoveCursor (ArrowDirection.Right, e.Shift, shortcut_control, true);
                        return true;
                    case Keys.Up:
                        need_refresh = document.MoveCursor (ArrowDirection.Up, e.Shift, shortcut_control, false);
                        return true;
                    case Keys.Down:
                        need_refresh = document.MoveCursor (ArrowDirection.Down, e.Shift, shortcut_control, false);
                        return true;
                    case Keys.Delete:
                        need_refresh = DeleteText (true, shortcut_control);
                        return true;
                    case Keys.Back:
                        need_refresh = DeleteText (false, shortcut_control);
                        return true;
                    case Keys.C:
                        if (shortcut_control)
                            Copy ();

                        return shortcut_control;
                    case Keys.X:
                        if (shortcut_control)
                            Cut ();

                        return shortcut_control;
                    case Keys.V:
                        if (shortcut_control)
                            Paste ();

                        return shortcut_control;
                    case Keys.A:
                        if (shortcut_control)
                            document.SelectAll ();

                        return shortcut_control;

                }
            } finally {
                if (need_refresh)
                    ScrollToCaret ();
            }

            return false;
        }

        /// <summary>
        /// Gets the text that should be placed on the clipboard for the current selection.
        /// </summary>
        /// <returns>The text to copy, or an empty string when there is no active selection.</returns>
        /// <remarks>
        /// The base implementation returns the visible selected text. Masking and formatting controls can override this method to preserve
        /// WinForms-compatible clipboard formatting.
        /// </remarks>
        protected virtual string GetSelectedTextForClipboard () => document.SelectedText;

        /// <summary>
        /// Inserts text at the current caret position.
        /// </summary>
        /// <param name="text">The text to insert.</param>
        /// <returns><see langword="true"/> when the document changed; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Derived controls override this method to validate or transform user input before it reaches the text document. Implementations should
        /// keep the caret valid and invalidate rendering when displayed text changes.
        /// </remarks>
        protected virtual bool InsertText (string text) => ApplyTextEdit (() => document.InsertText (text));

        // TextBoxDocument owns caret and selection state, while TextBox owns the
        // public control contract. Keep invalidation and TextChanged centralized
        // so keyboard, paste, and cut/delete edits behave like setting Text.
        private bool ApplyTextEdit (Func<bool> edit)
        {
            var old_text = document.Text;
            var changed = edit ();

            if (!changed)
                return false;

            Invalidate ();

            if (old_text != document.Text)
                OnTextChanged (EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Processes a key-down event after the public <see cref="Control.KeyDown"/> event has been raised.
        /// </summary>
        /// <param name="e">The key event data.</param>
        /// <returns><see langword="true"/> when the key was handled by the text editing model; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Override this method instead of <see cref="OnKeyDown"/> when a derived text control wants to reuse the normal event order while
        /// replacing the editing behavior.
        /// </remarks>
        protected virtual bool ProcessTextBoxKeyDown (KeyEventArgs e) => HandleKeyDown (e);

        /// <summary>
        /// Processes a key-press event after the public <see cref="Control.KeyPress"/> event has been raised.
        /// </summary>
        /// <param name="e">The key-press event data.</param>
        /// <returns><see langword="true"/> when text was inserted; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The base implementation inserts printable text and new lines for multiline text boxes. Derived controls can override this method to
        /// enforce custom input rules while preserving rendering and caret behavior.
        /// </remarks>
        protected virtual bool ProcessTextBoxKeyPress (KeyPressEventArgs e)
        {
            // Enter = 13
            if (e.KeyChar == 13 && MultiLine) {
                if (InsertText ("\n")) {
                    ScrollToCaret ();
                    return true;
                }
            }

            // Printable characters (except backspace)
            if (e.KeyChar >= 32 && e.KeyChar != 127) {
                if (InsertText (e.Text)) {
                    ScrollToCaret ();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum length of text the TextBox can hold.
        /// </summary>
        public int MaxLength {
            get => document.MaxLength;
            set => document.MaxLength = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if the TextBox supports multiple lines of text.
        /// </summary>
        public bool MultiLine {
            get => document.IsMultiline;
            set {
                if (document.IsMultiline != value) {

                    if (Padding == DefaultPadding)
                        Padding = new Padding (value ? 4 : 1, 0, 0, 0);

                    document.IsMultiline = value;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            document.Deselect ();
        }

        /// <inheritdoc/>
        protected override void OnEnabledChanged (EventArgs e)
        {
            base.OnEnabledChanged (e);

            document.Enabled = Enabled;
        }

        /// <inheritdoc/>
        protected override void OnFontChanged(EventArgs e)
        {
            document.InvalidateTextBlock();
            base.OnFontChanged(e);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            e.Handled = ProcessTextBoxKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyPress (KeyPressEventArgs e)
        {
            base.OnKeyPress (e);

            ProcessTextBoxKeyPress (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (e.Button != MouseButtons.Left)
                return;

            var anchor = GetPointerSelectionAnchor();
            SetCursorToCharIndex (GetTextIndexFromPosition (e.Location));

            is_highlighting = true;
            selection_anchor = e.Shift ? anchor : document.CursorIndex;
            UpdatePointerSelection();

            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (is_highlighting) {
                SetCursorToCharIndex (GetTextIndexFromPosition (e.Location));
                UpdatePointerSelection();

                Invalidate ();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (e.Button != MouseButtons.Left)
                return;

            SetCursorToCharIndex (GetTextIndexFromPosition (e.Location));

            is_highlighting = false;
            UpdatePointerSelection();

            Invalidate ();
        }

        private int GetPointerSelectionAnchor()
        {
            if (document.SelectionStart < 0 || document.SelectionEnd < 0)
                return document.CursorIndex;

            // The cursor is the active edge. Shift+click extends from the opposite logical edge,
            // including reverse selections whose anchor is numerically greater than the cursor.
            return document.CursorIndex == document.SelectionStart
                ? document.SelectionEnd
                : document.SelectionStart;
        }

        private void UpdatePointerSelection()
        {
            if (document.CursorIndex == selection_anchor) {
                document.SelectionStart = -1;
                document.SelectionEnd = -1;
            } else {
                document.SelectionStart = selection_anchor;
                document.SelectionEnd = document.CursorIndex;
            }
        }

        internal override void CancelPointerInteraction (int? pointerId = null)
        {
            is_highlighting = false;
            base.CancelPointerInteraction (pointerId);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        protected override void OnParentChanged (EventArgs e)
        {
            base.OnParentChanged (e);

            // Changing parent may mean changing scaling, which
            // means we need to recalculate the document.
            document.Reset ();
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            document.Width = PaddedClientRectangle.Width;
        }

        /// <inheritdoc/>
        internal override void OnPresentationContentMetricsChanged ()
        {
            base.OnPresentationContentMetricsChanged ();
            document.Width = PaddedClientRectangle.Width;
        }

        /// <summary>
        /// Gets or sets a character to display instead of the actual text.
        /// </summary>
        public char? PasswordCharacter {
            get => document.PasswordCharacter;
            set => document.PasswordCharacter = value;
        }

        /// <summary>
        /// Inserts any text on the clipboard into the TextBox.
        /// </summary>
        public void Paste ()
        {
            if (document.ReadOnly)
                return;

            var text = AsyncHelper.RunSync (() => ModernFormsNext.WindowKit.AvaloniaGlobals.GetRequiredService<IClipboard> ().GetTextAsync ());

            if (!string.IsNullOrEmpty (text) && InsertText (text))
                    ScrollToCaret ();
        }

        /// <summary>
        /// Gets or sets text to display if the TextBox contains no text.
        /// </summary>
        public string Placeholder {
            get => document.Placeholder;
            set => document.Placeholder = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if the text can be edited.
        /// </summary>
        public bool ReadOnly {
            get => document.ReadOnly;
            set => document.ReadOnly = value;
        }

        /// <summary>
        /// Scrolls the TextBox so that the caret is visible.
        /// </summary>
        public void ScrollToCaret ()
        {
            var caret = TextMeasurer.GetCursorLocation (
                document.GetTextBlock (),
                TextOrigin,
                document.CursorLayoutCodePointIndex,
                CurrentFontSize);

            if (caret.IsEmpty)
                return;

            caret.Offset (scroll_x, scroll_y);

            var dx = 0;
            var dy = 0;
            var viewport = TextViewport;

            if (caret.Top < viewport.Top)
                dy = caret.Top - viewport.Top - 1;
            else if (caret.Bottom > viewport.Bottom)
                dy = caret.Bottom - viewport.Bottom + 3;

            if (caret.Left < viewport.Left)
                dx = caret.Left - viewport.Left - 1;
            else if (caret.Right > viewport.Right)
                dx = caret.Right - viewport.Right + 3;

            DoScroll (dx, dy);
        }

        /// <summary>
        /// Gets or sets a value indicating the end of the TextBox's selected text.
        /// </summary>
        public int SelectionEnd {
            get => document.SelectionEnd;
            set => document.SelectionEnd = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the start of the TextBox's selected text.
        /// </summary>
        public int SelectionStart {
            get => document.SelectionStart;
            set => document.SelectionStart = value;
        }

        /// <summary>
        /// Selects all text in the TextBox.
        /// </summary>
        public void SelectAll() => document.SelectAll();

        // Sets cursor to specified character index and scrolls TextBox to cursor.
        private void SetCursorToCharIndex (int index)
        {
            if (document.SetCursorToCharIndex (index))
                ScrollToCaret ();
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override string Text { 
            get => document.Text; 
            set {
                if (document.Text != value) {
                    document.Text = value;
                    ScrollToCaret ();
                    OnTextChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets how text is aligned within the <see cref="TextBox"/>.
        /// </summary>
        /// <remarks>
        /// The horizontal component of the <see cref="ContentAlignment"/> value controls
        /// left, center, or right alignment. The vertical component controls whether the
        /// visible text block is placed at the top, middle, or bottom of the padded
        /// client area when the text is shorter than the available height.
        ///
        /// If multiline text is taller than the viewport, the text remains top-anchored
        /// and the vertical scroll position determines which lines are visible.
        ///
        /// Changing this property recalculates the text layout, invalidates the control,
        /// and scrolls the caret back into view. It does not change the text, selection,
        /// or whether the control is single-line or multi-line.
        /// </remarks>
        /// <example>
        /// <code>
        /// var amount = new TextBox
        /// {
        ///     TextAlign = ContentAlignment.MiddleRight
        /// };
        /// </code>
        /// </example>
        public ContentAlignment TextAlign {
            get => text_align;
            set {
                if (text_align == value)
                    return;

                text_align = value;
                document.Alignment = TextMeasurer.GetTextAlign (value);
                ScrollToCaret ();
            }
        }

        // Where the text starts, taking scrolling and vertical alignment into account.
        internal Point TextOrigin => GetTextOrigin (document.GetTextBlock ());

        internal Point GetTextOrigin (TextBlock block)
        {
            var y = PaddedClientRectangle.Y;
            var text_height = GetTextBlockHeight (block);
            var extra_height = PaddedClientRectangle.Height - text_height;

            if (extra_height > 0) {
                switch (text_align) {
                    case ContentAlignment.MiddleLeft:
                    case ContentAlignment.MiddleCenter:
                    case ContentAlignment.MiddleRight:
                        y += extra_height / 2;
                        break;
                    case ContentAlignment.BottomLeft:
                    case ContentAlignment.BottomCenter:
                    case ContentAlignment.BottomRight:
                        y += extra_height;
                        break;
                }
            }

            return new Point (PaddedClientRectangle.X - scroll_x, y - scroll_y);
        }

        private int GetTextBlockHeight (TextBlock block)
        {
            var measured_height = (int)Math.Ceiling (block.MeasuredHeight);

            if (measured_height > 0)
                return measured_height;

            return CurrentFontSize + 2;
        }

        // The virtual bounds of what is currently shown to the user.
        private Rectangle TextViewport => new Rectangle (new Point (PaddedClientRectangle.Location.X + scroll_x, PaddedClientRectangle.Location.Y + scroll_y), PaddedClientRectangle.Size);

        // Enables and recalculates scrollbars as needed.
        internal void UpdateScrollBars (TextBlock block)
        {
            // TODO: Horizontal scrollbar not supported
            // Something about the document changed, so we need to update the scrollbars
            if ((int)block.MeasuredHeight - PaddedClientRectangle.Height > 0) {
                VerticalScrollBar.Enabled = true;
                VerticalScrollBar.Maximum = (int)block.MeasuredHeight - PaddedClientRectangle.Height;
                VerticalScrollBar.LargeChange = PaddedClientRectangle.Height;
                VerticalScrollBar.SmallChange = CurrentFontSize * 3;

                var new_value = Math.Min (scroll_y, VerticalScrollBar.Maximum);

                if (VerticalScrollBar.Value != new_value)
                    VerticalScrollBar.Value = new_value;
            } else {
                if (scroll_y > 0)
                    DoScroll (0, -scroll_y);

                VerticalScrollBar.Enabled = false;
            }
        }
    }
}
