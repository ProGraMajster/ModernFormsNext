using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.WindowKit.Input.Platform;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a text box that uses a mask to distinguish required input, optional input, and literal characters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MaskedTextBox"/> is intended as the ModernFormsNext equivalent of the WinForms masked text box. The
    /// control is built on top of <see cref="TextBox"/> for rendering, caret movement, selection, and clipboard routing,
    /// while <see cref="System.ComponentModel.MaskedTextProvider"/> supplies the mask parsing and validation rules.
    /// </para>
    /// <para>
    /// When <see cref="Mask"/> is empty, the control behaves like a normal single-line <see cref="TextBox"/>. When a mask
    /// is set, typed, pasted, and programmatic input is validated against the provider before it reaches the displayed
    /// document. The control is UI-thread affine like other ModernFormsNext controls.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var phone = new MaskedTextBox("(999) 000-0000")
    /// {
    ///     PromptChar = '_',
    ///     TextMaskFormat = MaskFormat.IncludeLiterals
    /// };
    ///
    /// phone.MaskInputRejected += (_, e) =>
    /// {
    ///     Console.WriteLine($"Rejected at {e.Position}: {e.RejectionHint}");
    /// };
    /// </code>
    /// </example>
    public class MaskedTextBox : TextBox
    {
        private const int DefaultMaxLength = 32767;
        private const char DefaultPasswordChar = '\0';
        private const char DefaultPromptChar = '_';
        private const char SystemPasswordChar = '\u25CF';

        private bool allow_prompt_as_input = true;
        private bool ascii_only;
        private bool beep_on_error;
        private CultureInfo culture = CultureInfo.CurrentCulture;
        private MaskFormat cut_copy_mask_format = MaskFormat.IncludeLiterals;
        private IFormatProvider? format_provider;
        private bool hide_prompt_on_leave;
        private InsertKeyMode insert_key_mode = InsertKeyMode.Default;
        private bool is_overwrite_mode;
        private string mask = string.Empty;
        private MaskedTextProvider? masked_text_provider;
        private int max_length = DefaultMaxLength;
        private char password_char = DefaultPasswordChar;
        private char prompt_char = DefaultPromptChar;
        private bool reject_input_on_first_failure;
        private bool reset_on_prompt = true;
        private bool reset_on_space = true;
        private bool skip_literals = true;
        private MaskFormat text_mask_format = MaskFormat.IncludeLiterals;
        private bool use_system_password_char;
        private Type? validating_type;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaskedTextBox"/> class.
        /// </summary>
        public MaskedTextBox ()
        {
            base.MultiLine = false;
            base.PasswordCharacter = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaskedTextBox"/> class with the specified input mask.
        /// </summary>
        /// <param name="mask">The mask that defines editable and literal positions.</param>
        public MaskedTextBox (string mask)
            : this ()
        {
            Mask = mask;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaskedTextBox"/> class from an existing provider.
        /// </summary>
        /// <param name="maskedTextProvider">The provider whose mask, culture, prompt, password, and assigned text are copied.</param>
        /// <exception cref="ArgumentNullException"><paramref name="maskedTextProvider"/> is <see langword="null"/>.</exception>
        public MaskedTextBox (MaskedTextProvider maskedTextProvider)
            : this ()
        {
            ArgumentNullException.ThrowIfNull (maskedTextProvider);

            ApplyProvider ((MaskedTextProvider)maskedTextProvider.Clone ());
            UpdateDisplayText ();
        }

        /// <summary>
        /// Occurs when the <see cref="AcceptsTab"/> property changes.
        /// </summary>
        /// <remarks>
        /// This event is included for WinForms API compatibility. WinForms does not support changing
        /// <see cref="AcceptsTab"/> on <see cref="MaskedTextBox"/>, so handlers are not retained and the event is never raised.
        /// </remarks>
        public event EventHandler? AcceptsTabChanged {
            add { }
            remove { }
        }

        /// <summary>
        /// Occurs when <see cref="IsOverwriteMode"/> changes.
        /// </summary>
        public event EventHandler? IsOverwriteModeChanged;

        /// <summary>
        /// Occurs when the <see cref="Mask"/> property changes.
        /// </summary>
        public event EventHandler? MaskChanged;

        /// <summary>
        /// Occurs when typed or pasted input cannot be accepted by the current mask.
        /// </summary>
        public event MaskInputRejectedEventHandler? MaskInputRejected;

        /// <summary>
        /// Occurs when the <see cref="Multiline"/> property changes.
        /// </summary>
        public event EventHandler? MultilineChanged;

        /// <summary>
        /// Occurs when the <see cref="TextAlign"/> property changes.
        /// </summary>
        public event EventHandler? TextAlignChanged;

        /// <summary>
        /// Occurs after <see cref="ValidateText"/> attempts to convert the current text to <see cref="ValidatingType"/>.
        /// </summary>
        public event TypeValidationEventHandler? TypeValidationCompleted;

        /// <summary>
        /// Gets or sets a value indicating whether the TAB key is accepted as input instead of moving focus.
        /// </summary>
        /// <remarks>
        /// This WinForms-compatible property is not supported by <see cref="MaskedTextBox"/>. The getter always returns
        /// <see langword="false"/> and the setter has no effect.
        /// </remarks>
        public bool AcceptsTab {
            get => false;
            set { }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the prompt character can be entered as valid input.
        /// </summary>
        public bool AllowPromptAsInput {
            get => allow_prompt_as_input;
            set {
                if (allow_prompt_as_input == value)
                    return;

                allow_prompt_as_input = value;
                RecreateProvider (preserveText: true);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the mask accepts only ASCII characters.
        /// </summary>
        public bool AsciiOnly {
            get => ascii_only;
            set {
                if (ascii_only == value)
                    return;

                ascii_only = value;
                RecreateProvider (preserveText: true);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control should request an audible cue when input is rejected.
        /// </summary>
        /// <remarks>
        /// The property is provided for WinForms migration compatibility. ModernFormsNext does not yet expose a
        /// platform-neutral system-beep service, so rejected input raises <see cref="MaskInputRejected"/> but does not play
        /// a sound.
        /// </remarks>
        public bool BeepOnError {
            get => beep_on_error;
            set => beep_on_error = value;
        }

        /// <summary>
        /// Gets a value indicating whether an undo snapshot is available.
        /// </summary>
        /// <remarks>
        /// This WinForms-compatible property is not supported by <see cref="MaskedTextBox"/> and always returns
        /// <see langword="false"/>.
        /// </remarks>
        public bool CanUndo => false;

        /// <summary>
        /// Gets or sets the culture used by the mask provider for culture-sensitive literals.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public CultureInfo Culture {
            get => culture;
            set {
                ArgumentNullException.ThrowIfNull (value);

                if (Equals (culture, value))
                    return;

                culture = value;
                RecreateProvider (preserveText: true);
            }
        }

        /// <summary>
        /// Gets or sets how selected masked text is formatted when it is copied or cut to the clipboard.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is not a valid <see cref="MaskFormat"/> value.</exception>
        public MaskFormat CutCopyMaskFormat {
            get => cut_copy_mask_format;
            set {
                ValidateMaskFormat (value, nameof (value));
                cut_copy_mask_format = value;
            }
        }

        /// <summary>
        /// Gets or sets the format provider used by <see cref="ValidateText"/>.
        /// </summary>
        /// <remarks>
        /// Assign <see langword="null"/> to use the target type's default conversion behavior, matching WinForms.
        /// </remarks>
        public IFormatProvider? FormatProvider {
            get => format_provider;
            set => format_provider = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether prompt characters are hidden when the control does not have focus.
        /// </summary>
        public bool HidePromptOnLeave {
            get => hide_prompt_on_leave;
            set {
                if (hide_prompt_on_leave == value)
                    return;

                hide_prompt_on_leave = value;
                UpdateDisplayText ();
            }
        }

        /// <summary>
        /// Gets or sets how typed characters are inserted into occupied editable positions.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is not a valid <see cref="InsertKeyMode"/> value.</exception>
        public InsertKeyMode InsertKeyMode {
            get => insert_key_mode;
            set {
                if (!Enum.IsDefined (typeof (InsertKeyMode), value))
                    throw new ArgumentOutOfRangeException (nameof (value));

                if (insert_key_mode == value)
                    return;

                insert_key_mode = value;
                SetOverwriteMode (value == InsertKeyMode.Overwrite);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the control is currently in overwrite mode.
        /// </summary>
        public bool IsOverwriteMode => insert_key_mode == InsertKeyMode.Overwrite || (insert_key_mode == InsertKeyMode.Default && is_overwrite_mode);

        /// <summary>
        /// Gets or sets the input mask.
        /// </summary>
        /// <remarks>
        /// Changing the mask recreates the underlying <see cref="MaskedTextProvider"/>, invalidates the displayed text,
        /// raises <see cref="MaskChanged"/>, and raises <see cref="Control.TextChanged"/> when the formatted text changes.
        /// An empty mask disables masking and leaves the control in normal <see cref="TextBox"/> editing mode.
        /// </remarks>
        public string Mask {
            get => mask;
            set {
                value ??= string.Empty;

                if (mask == value)
                    return;

                var old_text = Text;
                var text_to_preserve = GetRawText ();
                mask = value;

                var new_provider = CreateProvider (mask);
                masked_text_provider = new_provider;

                if (new_provider is not null && text_to_preserve.Length > 0) {
                    if (!new_provider.Set (text_to_preserve, out var test_position, out var result_hint))
                        OnMaskInputRejected (test_position, result_hint);
                } else if (new_provider is null) {
                    base.Text = text_to_preserve;
                }

                UpdateDisplayText ();
                MaskChanged?.Invoke (this, EventArgs.Empty);

                if (old_text != Text)
                    OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets a value indicating whether all required input positions have been filled.
        /// </summary>
        public bool MaskCompleted => masked_text_provider?.MaskCompleted ?? false;

        /// <summary>
        /// Gets the provider that validates and formats masked text, or <see langword="null"/> when <see cref="Mask"/> is empty.
        /// </summary>
        public MaskedTextProvider? MaskedTextProvider => masked_text_provider;

        /// <summary>
        /// Gets a value indicating whether all editable positions have been filled.
        /// </summary>
        public bool MaskFull => masked_text_provider?.MaskFull ?? false;

        /// <summary>
        /// Gets or sets the maximum number of characters accepted when <see cref="Mask"/> is empty.
        /// </summary>
        /// <remarks>
        /// When a mask is active the mask length determines the editable surface, matching WinForms behavior.
        /// </remarks>
        public new int MaxLength {
            get => max_length;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value));

                max_length = value == 0 ? int.MaxValue : value;
                base.MaxLength = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control accepts multiple lines of text.
        /// </summary>
        /// <remarks>
        /// The property is included for WinForms API compatibility. Masked input is single-line; setting this property
        /// affects the inherited <see cref="TextBox.MultiLine"/> rendering behavior but masks should not contain line
        /// breaks.
        /// </remarks>
        public bool Multiline {
            get => base.MultiLine;
            set {
                if (base.MultiLine == value)
                    return;

                base.MultiLine = value;
                MultilineChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the password character used to hide assigned editable characters.
        /// </summary>
        public char PasswordChar {
            get => password_char;
            set {
                if (password_char == value)
                    return;

                password_char = value;
                ApplyPasswordSettings ();
                UpdateDisplayText ();
            }
        }

        /// <summary>
        /// Gets or sets the character displayed for editable positions that have not yet been assigned input.
        /// </summary>
        public char PromptChar {
            get => prompt_char;
            set {
                if (!System.ComponentModel.MaskedTextProvider.IsValidInputChar (value))
                    throw new ArgumentException ("PromptChar must be a valid mask input character.", nameof (value));

                if (prompt_char == value)
                    return;

                prompt_char = value;

                if (masked_text_provider is not null)
                    masked_text_provider.PromptChar = value;

                UpdateDisplayText ();
            }
        }

        /// <inheritdoc/>
        public new bool ReadOnly {
            get => base.ReadOnly;
            set => base.ReadOnly = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether paste operations stop after the first rejected character.
        /// </summary>
        public bool RejectInputOnFirstFailure {
            get => reject_input_on_first_failure;
            set => reject_input_on_first_failure = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether entering the prompt character resets the current editable position.
        /// </summary>
        public bool ResetOnPrompt {
            get => reset_on_prompt;
            set {
                if (reset_on_prompt == value)
                    return;

                reset_on_prompt = value;

                if (masked_text_provider is not null)
                    masked_text_provider.ResetOnPrompt = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether entering a space resets the current editable position when the mask allows it.
        /// </summary>
        public bool ResetOnSpace {
            get => reset_on_space;
            set {
                if (reset_on_space == value)
                    return;

                reset_on_space = value;

                if (masked_text_provider is not null)
                    masked_text_provider.ResetOnSpace = value;
            }
        }

        /// <summary>
        /// Gets or sets the current selected text.
        /// </summary>
        /// <remarks>
        /// For masked text, the getter honors <see cref="CutCopyMaskFormat"/>. The setter replaces the current selection
        /// through the same mask validation path used for typed input.
        /// </remarks>
        public string SelectedText {
            get => GetSelectedTextForClipboard ();
            set {
                if (ReadOnly)
                    return;

                DeleteSelectedText ();
                InsertText (value ?? string.Empty);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether literal characters are skipped when user input reaches them.
        /// </summary>
        public bool SkipLiterals {
            get => skip_literals;
            set {
                if (skip_literals == value)
                    return;

                skip_literals = value;

                if (masked_text_provider is not null)
                    masked_text_provider.SkipLiterals = value;
            }
        }

        /// <summary>
        /// Gets or sets the lines of text in the control.
        /// </summary>
        /// <remarks>
        /// This property is included for WinForms API compatibility. Masked text boxes are intended to be single-line;
        /// when a mask is active, assigning this property joins the supplied lines and validates the resulting text
        /// through <see cref="Text"/>.
        /// </remarks>
        public string[] Lines {
            get => document.Text.Replace ("\r\n", "\n", StringComparison.Ordinal).Split ('\n');
            set => Text = value is null ? string.Empty : string.Join (Environment.NewLine, value);
        }

        /// <inheritdoc/>
        public override string Text {
            get {
                if (masked_text_provider is null)
                    return base.Text;

                return GetMaskedText (text_mask_format);
            }
            set {
                value ??= string.Empty;

                var old_text = Text;

                if (masked_text_provider is null) {
                    base.Text = value;
                    return;
                }

                SetMaskedText (value);

                if (old_text != Text)
                    OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the horizontal alignment of the displayed text.
        /// </summary>
        public new HorizontalAlignment TextAlign {
            get {
                return base.TextAlign switch {
                    ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter => HorizontalAlignment.Center,
                    ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Left
                };
            }
            set {
                if (!Enum.IsDefined (typeof (HorizontalAlignment), value))
                    throw new ArgumentOutOfRangeException (nameof (value));

                if (TextAlign == value)
                    return;

                base.TextAlign = value switch {
                    HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                    HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                    _ => ContentAlignment.MiddleLeft
                };

                TextAlignChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the length of the text returned by <see cref="Text"/>.
        /// </summary>
        public int TextLength => Text.Length;

        /// <summary>
        /// Gets or sets how the <see cref="Text"/> property formats prompt and literal characters.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is not a valid <see cref="MaskFormat"/> value.</exception>
        public MaskFormat TextMaskFormat {
            get => text_mask_format;
            set {
                ValidateMaskFormat (value, nameof (value));

                if (text_mask_format == value)
                    return;

                var old_text = Text;
                text_mask_format = value;

                if (old_text != Text)
                    OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the platform password character is used for assigned editable characters.
        /// </summary>
        public bool UseSystemPasswordChar {
            get => use_system_password_char;
            set {
                if (use_system_password_char == value)
                    return;

                use_system_password_char = value;
                ApplyPasswordSettings ();
                UpdateDisplayText ();
            }
        }

        /// <summary>
        /// Gets or sets the type used by <see cref="ValidateText"/> to convert the current text.
        /// </summary>
        public Type? ValidatingType {
            get => validating_type;
            set => validating_type = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether text wraps inside the control.
        /// </summary>
        /// <remarks>
        /// This WinForms-compatible property is not supported by <see cref="MaskedTextBox"/>. The getter always returns
        /// <see langword="false"/> and the setter has no effect.
        /// </remarks>
        public bool WordWrap {
            get => false;
            set { }
        }

        /// <summary>
        /// Clears the current undo snapshot.
        /// </summary>
        /// <remarks>
        /// This method is included for WinForms API compatibility. Undo is not supported for masked text editing.
        /// </remarks>
        public void ClearUndo ()
        {
        }

        /// <summary>
        /// Gets the displayed character at the specified point.
        /// </summary>
        /// <param name="pt">The point, in control client coordinates.</param>
        /// <returns>The character at the point, or the null character when the point is outside the displayed text.</returns>
        public char GetCharFromPosition (Point pt)
        {
            var index = GetCharIndexFromPosition (pt);
            var display_text = document.Text;

            if (index < 0 || index >= display_text.Length)
                return '\0';

            return display_text[index];
        }

        /// <summary>
        /// Gets the character index nearest to the specified point.
        /// </summary>
        /// <param name="pt">The point, in control client coordinates.</param>
        /// <returns>The nearest character index.</returns>
        public int GetCharIndexFromPosition (Point pt)
        {
            if (document.Text.Length == 0)
                return 0;

            return document.GetUtf16IndexFromPosition (pt.X - TextOrigin.X, pt.Y - TextOrigin.Y);
        }

        /// <summary>
        /// Gets the index of the first character in the specified line.
        /// </summary>
        /// <param name="lineNumber">The zero-based line number.</param>
        /// <returns>The first character index, or -1 when the line does not exist.</returns>
        public int GetFirstCharIndexFromLine (int lineNumber)
        {
            if (lineNumber < 0)
                return -1;

            if (lineNumber == 0)
                return 0;

            var current_line = 0;
            var text = document.Text;

            for (var i = 0; i < text.Length; i++) {
                if (text[i] != '\n')
                    continue;

                current_line++;

                if (current_line == lineNumber)
                    return i + 1;
            }

            return -1;
        }

        /// <summary>
        /// Gets the index of the first character in the line that contains the caret.
        /// </summary>
        /// <returns>The first character index of the current line.</returns>
        public int GetFirstCharIndexOfCurrentLine () => GetFirstCharIndexFromLine (GetLineFromCharIndex (document.CursorIndex));

        /// <summary>
        /// Gets the line number that contains the specified character index.
        /// </summary>
        /// <param name="index">The character index.</param>
        /// <returns>The zero-based line number.</returns>
        public int GetLineFromCharIndex (int index)
        {
            var line = 0;
            var text = document.Text;
            var length = Math.Clamp (index, 0, text.Length);

            for (var i = 0; i < length; i++) {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        /// <summary>
        /// Gets the client position of the caret for the specified character index.
        /// </summary>
        /// <param name="index">The character index.</param>
        /// <returns>The top-left caret position for the character index.</returns>
        public Point GetPositionFromCharIndex (int index)
        {
            var block = document.GetTextBlock ();
            var caret = TextMeasurer.GetCursorLocation (
                block,
                TextOrigin,
                document.GetLayoutCodePointIndex (Math.Clamp (index, 0, document.Text.Length)),
                CurrentFontSize);
            return caret.IsEmpty ? Point.Empty : caret.Location;
        }

        /// <summary>
        /// Returns the formatted text for the current mask.
        /// </summary>
        /// <returns>
        /// The current mask contents with prompt and literal characters included, or the base string representation when no mask is active.
        /// </returns>
        public override string ToString ()
        {
            if (masked_text_provider is null)
                return base.ToString () ?? string.Empty;

            return masked_text_provider.ToString (
                ignorePasswordChar: true,
                includePrompt: true,
                includeLiterals: true,
                startPosition: 0,
                length: masked_text_provider.Length);
        }

        /// <summary>
        /// Scrolls the control to the caret.
        /// </summary>
        /// <remarks>
        /// This method is included for WinForms API compatibility. Masked text boxes are single-line controls and this
        /// method has no effect.
        /// </remarks>
        public new void ScrollToCaret ()
        {
        }

        /// <summary>
        /// Restores the last text-editing snapshot if one is available.
        /// </summary>
        /// <remarks>
        /// This method is included for WinForms API compatibility. Undo is not supported for masked text editing.
        /// </remarks>
        public void Undo ()
        {
        }

        /// <summary>
        /// Converts the current text to <see cref="ValidatingType"/> and raises <see cref="TypeValidationCompleted"/>.
        /// </summary>
        /// <returns>The converted value, or <see langword="null"/> when validation is disabled or conversion fails.</returns>
        public object? ValidateText ()
        {
            if (validating_type is null)
                return null;

            object? value = null;
            var is_valid = false;
            var message = string.Empty;

            if (masked_text_provider is not null && !masked_text_provider.MaskCompleted) {
                message = "The current text does not satisfy all required mask positions.";
            } else {
                try {
                    value = ConvertText (GetValidationText (), validating_type);
                    is_valid = true;
                } catch (Exception ex) when (ex is FormatException or InvalidCastException or NotSupportedException or ArgumentException) {
                    message = ex.Message;
                }
            }

            var args = new TypeValidationEventArgs (validating_type, is_valid, value, message);
            TypeValidationCompleted?.Invoke (this, args);

            return args.Cancel ? null : value;
        }

        /// <inheritdoc/>
        protected override bool DeleteSelectedText ()
        {
            if (masked_text_provider is null)
                return DeleteUnmaskedSelection ();

            if (!document.IsTextSelected || ReadOnly)
                return false;

            var old_text = Text;
            var start = Math.Min (document.SelectionStart, document.SelectionEnd);
            var end = Math.Max (document.SelectionStart, document.SelectionEnd) - 1;

            if (end < start)
                return false;

            if (!masked_text_provider.RemoveAt (start, end, out var test_position, out var result_hint)) {
                OnMaskInputRejected (test_position, result_hint);
                return false;
            }

            document.SelectionStart = -1;
            document.SelectionEnd = -1;
            UpdateDisplayText (start);

            if (old_text != Text)
                OnTextChanged (EventArgs.Empty);

            return true;
        }

        /// <inheritdoc/>
        protected override bool DeleteText (bool forward, bool wholeWord)
        {
            if (masked_text_provider is null)
                return DeleteUnmaskedText (forward, wholeWord);

            if (ReadOnly)
                return false;

            if (document.IsTextSelected)
                return DeleteSelectedText ();

            var target = GetDeletePosition (forward);

            if (target < 0)
                return false;

            var old_text = Text;
            if (!masked_text_provider.RemoveAt (target, target, out var test_position, out var result_hint)) {
                OnMaskInputRejected (test_position, result_hint);
                return false;
            }

            UpdateDisplayText (target);

            if (old_text != Text)
                OnTextChanged (EventArgs.Empty);

            return true;
        }

        /// <inheritdoc/>
        protected override string GetSelectedTextForClipboard ()
        {
            if (!document.IsTextSelected)
                return string.Empty;

            if (masked_text_provider is null)
                return document.SelectedText;

            if (masked_text_provider.IsPassword)
                return string.Empty;

            var start = Math.Min (document.SelectionStart, document.SelectionEnd);
            var end = Math.Max (document.SelectionStart, document.SelectionEnd);
            var length = Math.Max (0, end - start);

            if (length == 0)
                return string.Empty;

            return GetMaskedText (cut_copy_mask_format, start, length);
        }

        /// <inheritdoc/>
        protected override bool InsertText (string text)
        {
            text ??= string.Empty;

            if (masked_text_provider is null)
                return InsertUnmaskedText (text);

            if (ReadOnly || text.Length == 0)
                return false;

            var old_text = Text;
            var caret = Math.Clamp (document.CursorIndex, 0, masked_text_provider.Length);

            if (document.IsTextSelected) {
                var start = Math.Min (document.SelectionStart, document.SelectionEnd);
                var end = Math.Max (document.SelectionStart, document.SelectionEnd) - 1;

                if (end >= start)
                    masked_text_provider.RemoveAt (start, end, out _, out _);

                document.SelectionStart = -1;
                document.SelectionEnd = -1;
                caret = start;
            }

            var accepted_any = false;

            foreach (var c in text) {
                if (c is '\r' or '\n')
                    continue;

                if (c == '\t')
                    continue;

                if (!InsertMaskedCharacter (c, ref caret)) {
                    if (reject_input_on_first_failure)
                        break;

                    continue;
                }

                accepted_any = true;
            }

            if (!accepted_any) {
                return false;
            }

            UpdateDisplayText (caret);

            if (old_text != Text)
                OnTextChanged (EventArgs.Empty);

            return true;
        }

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);
            UpdateDisplayText ();
        }

        /// <inheritdoc/>
        protected override void OnGotFocus (EventArgs e)
        {
            base.OnGotFocus (e);
            UpdateDisplayText ();
        }

        /// <inheritdoc/>
        protected override bool ProcessTextBoxKeyDown (KeyEventArgs e)
        {
            if ((e.KeyData & Keys.KeyCode) == Keys.Insert) {
                if (insert_key_mode == InsertKeyMode.Default)
                    SetOverwriteMode (!is_overwrite_mode);

                return true;
            }

            return base.ProcessTextBoxKeyDown (e);
        }

        /// <inheritdoc/>
        protected override bool ProcessTextBoxKeyPress (KeyPressEventArgs e)
        {
            if (masked_text_provider is null)
                return base.ProcessTextBoxKeyPress (e);

            if (e.KeyChar >= 32) {
                var result = InsertText (e.Text);
                e.Handled = true;
                return result;
            }

            return false;
        }

        private void ApplyPasswordSettings ()
        {
            var effective_password_char = GetEffectivePasswordChar ();

            if (masked_text_provider is null) {
                base.PasswordCharacter = effective_password_char == DefaultPasswordChar ? null : effective_password_char;
                return;
            }

            masked_text_provider.PasswordChar = effective_password_char;
            masked_text_provider.IsPassword = effective_password_char != DefaultPasswordChar;
        }

        private void ApplyProvider (MaskedTextProvider provider)
        {
            masked_text_provider = provider;
            mask = provider.Mask;
            culture = provider.Culture;
            allow_prompt_as_input = provider.AllowPromptAsInput;
            ascii_only = provider.AsciiOnly;
            prompt_char = provider.PromptChar;
            password_char = provider.PasswordChar;
            reset_on_prompt = provider.ResetOnPrompt;
            reset_on_space = provider.ResetOnSpace;
            skip_literals = provider.SkipLiterals;
            ApplyPasswordSettings ();
        }

        private void ApplyProviderSettings ()
        {
            if (masked_text_provider is null)
                return;

            masked_text_provider.ResetOnPrompt = reset_on_prompt;
            masked_text_provider.ResetOnSpace = reset_on_space;
            masked_text_provider.SkipLiterals = skip_literals;
            masked_text_provider.PromptChar = prompt_char;
            ApplyPasswordSettings ();
        }

        private MaskedTextProvider? CreateProvider (string providerMask)
        {
            if (string.IsNullOrEmpty (providerMask))
                return null;

            var provider = new MaskedTextProvider (providerMask, culture, allow_prompt_as_input, prompt_char, GetEffectivePasswordChar (), ascii_only);
            provider.ResetOnPrompt = reset_on_prompt;
            provider.ResetOnSpace = reset_on_space;
            provider.SkipLiterals = skip_literals;
            provider.IsPassword = GetEffectivePasswordChar () != DefaultPasswordChar;
            return provider;
        }

        private object? ConvertText (string text, Type targetType)
        {
            if (targetType == typeof (string))
                return text;

            var converter = TypeDescriptor.GetConverter (targetType);
            var converter_culture = format_provider as CultureInfo ?? culture;

            if (converter.CanConvertFrom (typeof (string)))
                return converter.ConvertFrom (null, converter_culture, text);

            if (targetType.IsEnum)
                return Enum.Parse (targetType, text);

            return Convert.ChangeType (text, targetType, format_provider);
        }

        private bool DeleteUnmaskedSelection ()
        {
            return base.DeleteSelectedText ();
        }

        private bool DeleteUnmaskedText (bool forward, bool wholeWord)
        {
            return base.DeleteText (forward, wholeWord);
        }

        private int GetDeletePosition (bool forward)
        {
            if (masked_text_provider is null)
                return -1;

            if (forward)
                return masked_text_provider.FindEditPositionFrom (document.CursorIndex, true);

            return masked_text_provider.FindEditPositionFrom (Math.Min (document.CursorIndex - 1, masked_text_provider.Length - 1), false);
        }

        private char GetEffectivePasswordChar () => use_system_password_char ? SystemPasswordChar : password_char;

        private string GetDisplayText ()
        {
            if (masked_text_provider is null)
                return base.Text;

            if (ReadOnly || (hide_prompt_on_leave && !Selected))
                return masked_text_provider.ToString (includePrompt: false, includeLiterals: true);

            return masked_text_provider.ToDisplayString ();
        }

        private string GetMaskedText (MaskFormat format)
        {
            if (masked_text_provider is null)
                return base.Text;

            return GetMaskedText (format, 0, masked_text_provider.Length);
        }

        private string GetMaskedText (MaskFormat format, int startPosition, int length)
        {
            if (masked_text_provider is null)
                return base.Text;

            var include_prompt = format is MaskFormat.IncludePrompt or MaskFormat.IncludePromptAndLiterals;
            var include_literals = format is MaskFormat.IncludeLiterals or MaskFormat.IncludePromptAndLiterals;
            return masked_text_provider.ToString (
                ignorePasswordChar: true,
                includePrompt: include_prompt,
                includeLiterals: include_literals,
                startPosition: startPosition,
                length: length);
        }

        private string GetRawText ()
        {
            if (masked_text_provider is null)
                return base.Text;

            return masked_text_provider.ToString (
                ignorePasswordChar: true,
                includePrompt: false,
                includeLiterals: false,
                startPosition: 0,
                length: masked_text_provider.Length);
        }

        private string GetValidationText ()
        {
            if (masked_text_provider is null)
                return base.Text;

            var include_literals = text_mask_format is MaskFormat.IncludeLiterals or MaskFormat.IncludePromptAndLiterals;
            return masked_text_provider.ToString (includePrompt: false, includeLiterals: include_literals);
        }

        private bool InsertMaskedCharacter (char c, ref int caret)
        {
            if (masked_text_provider is null)
                return false;

            var position = Math.Clamp (caret, 0, masked_text_provider.Length);
            var result = IsOverwriteMode
                ? masked_text_provider.Replace (c, position, out var test_position, out var result_hint)
                : masked_text_provider.InsertAt (c, position, out test_position, out result_hint);

            if (!result) {
                OnMaskInputRejected (test_position, result_hint);
                return false;
            }

            caret = GetNextCaretPosition (test_position + 1);
            return true;
        }

        private bool InsertUnmaskedText (string text)
        {
            return base.InsertText (text);
        }

        private int GetNextCaretPosition (int position)
        {
            if (masked_text_provider is null)
                return position;

            var next = masked_text_provider.FindEditPositionFrom (Math.Clamp (position, 0, masked_text_provider.Length), true);
            return next >= 0 ? next : masked_text_provider.Length;
        }

        private void OnMaskInputRejected (int position, MaskedTextResultHint rejectionHint)
        {
            MaskInputRejected?.Invoke (this, new MaskInputRejectedEventArgs (position, rejectionHint));
        }

        private void RecreateProvider (bool preserveText)
        {
            var old_text = Text;
            var text_to_preserve = preserveText ? GetRawText () : string.Empty;

            masked_text_provider = CreateProvider (mask);

            if (masked_text_provider is not null && text_to_preserve.Length > 0)
                masked_text_provider.Set (text_to_preserve, out _, out _);

            UpdateDisplayText ();

            if (old_text != Text)
                OnTextChanged (EventArgs.Empty);
        }

        private void SetMaskedText (string value)
        {
            if (masked_text_provider is null)
                return;

            if (!masked_text_provider.Set (value, out var test_position, out var result_hint))
                OnMaskInputRejected (test_position, result_hint);

            UpdateDisplayText (GetNextCaretPosition (0));
        }

        private void SetOverwriteMode (bool value)
        {
            if (is_overwrite_mode == value)
                return;

            is_overwrite_mode = value;
            IsOverwriteModeChanged?.Invoke (this, EventArgs.Empty);
        }

        private void UpdateDisplayText (int? cursorIndex = null)
        {
            var display_text = GetDisplayText ();
            var cursor = Math.Clamp (cursorIndex ?? document.CursorIndex, 0, display_text.Length);

            document.Text = display_text;
            document.SetCursorToCharIndex (cursor);
            base.ScrollToCaret ();
        }

        private static void ValidateMaskFormat (MaskFormat value, string parameterName)
        {
            if (!Enum.IsDefined (typeof (MaskFormat), value))
                throw new ArgumentOutOfRangeException (parameterName);
        }
    }
}
