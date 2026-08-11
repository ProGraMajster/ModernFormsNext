using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ModernFormsNext.Renderers;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a multiline text-editing control that supports character formatting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ModernFormsNext implements <see cref="RichTextBox"/> as a platform-neutral control. It
    /// reuses the existing <see cref="TextBox"/> editing, selection, scrolling, clipboard, focus,
    /// mouse, touch, and keyboard behavior, then stores rich formatting in a separate range model.
    /// Rendering is performed by SkiaSharp and RichTextKit; no native WinForms or RichEdit window
    /// is created.
    /// </para>
    /// <para>
    /// This first shared implementation supports plain text editing, selection formatting,
    /// common RTF/plain text load-save operations, and WinForms-style search APIs. Native RichEdit
    /// features such as OLE objects, protected ranges, automatic URL detection, and IME-specific
    /// language behavior are represented only where a portable ModernFormsNext behavior exists.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var editor = new RichTextBox
    /// {
    ///     Dock = DockStyle.Fill,
    ///     Text = "ModernFormsNext rich text"
    /// };
    ///
    /// editor.Select(0, 15);
    /// editor.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
    /// editor.SelectionColor = SKColors.DodgerBlue;
    /// editor.DeselectAll();
    /// </code>
    /// </example>
    public class RichTextBox : TextBox
    {
        private readonly List<RichTextBoxTextRun> runs = new List<RichTextBoxTextRun>();
        private readonly RichTextBoxTextStyle insertionStyle = new RichTextBoxTextStyle();
        private const float MinimumZoomFactor = 0.015625f;
        private const float MaximumZoomFactor = 64f;
        private const float MouseWheelZoomStep = 1.1f;
        private const string TrailingLineBreakCaretMarker = "\u200B";
        private Rectangle lastContentsRectangle = Rectangle.Empty;
        private RichTextBoxScrollBars richScrollBars = RichTextBoxScrollBars.Vertical;
        private TextBlock? cachedRichTextBlock;
        private float zoomFactor = 1f;
        private bool suppressNextEnterKeyPress;
        private bool suppressNextTabKeyPress;
        private bool acceptsTab = true;
        private bool autoWordSelection;
        private bool detectUrls;
        private bool selectionBullet;
        private bool selectionProtected;
        private int selectionCharOffset;
        private int selectionHangingIndent;
        private int selectionIndent;
        private int selectionRightIndent;
        private int[] selectionTabs = Array.Empty<int>();
        private bool showSelectionMargin;
        private RichTextBoxLanguageOptions languageOption;
        private RichTextBoxWordPunctuations wordPunctuations = RichTextBoxWordPunctuations.Level1;

        /// <summary>
        /// Initializes a new instance of the <see cref="RichTextBox"/> class.
        /// </summary>
        public RichTextBox()
        {
            MultiLine = true;
            TextAlign = ContentAlignment.TopLeft;
            base.ScrollBars = ModernFormsNext.ScrollBars.Vertical;
            VerticalScrollBar.ValueChanged += (_, _) => OnVScroll(EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected override int GetTextIndexFromPosition(Point location)
        {
            if (Text.Length == 0)
                return 0;

            // Pointer hit testing must use the same styled block and origin as rendering. The
            // plain TextBoxDocument block can wrap at different positions when a RichTextBox run
            // changes font family, size, weight, or other glyph metrics.
            var block = GetRichTextBlock();
            var origin = GetTextOrigin(block);
            var hit = block.HitTest(location.X - origin.X, location.Y - origin.Y);
            return document.GetUtf16IndexFromLayoutCodePointIndex(hit.ClosestCodePointIndex);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the TAB key inserts a tab character.
        /// </summary>
        /// <remarks>
        /// When this value is <see langword="true"/>, the shared input adapter routes TAB to the
        /// editor instead of moving focus to the next control. The inserted character is a normal
        /// tab stored in <see cref="Text"/> and participates in selection, clipboard, and RTF
        /// serialization like other text.
        /// </remarks>
        public bool AcceptsTab
        {
            get => acceptsTab;
            set => acceptsTab = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether word selection should expand automatically.
        /// </summary>
        /// <remarks>
        /// The value is stored for API compatibility. The current shared input model still uses
        /// character-based drag selection.
        /// </remarks>
        public bool AutoWordSelection
        {
            get => autoWordSelection;
            set => autoWordSelection = value;
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding(4);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(100, 96);

        /// <summary>
        /// Gets or sets a value indicating whether URLs should be detected automatically.
        /// </summary>
        /// <remarks>
        /// URL detection is not yet implemented by the shared renderer. The property is retained
        /// so migrated code can configure it without introducing a platform dependency.
        /// </remarks>
        public bool DetectUrls
        {
            get => detectUrls;
            set => detectUrls = value;
        }

        /// <summary>
        /// Occurs when the measured content rectangle changes.
        /// </summary>
        /// <remarks>
        /// The event is raised after text, formatting, size, or zoom changes cause the rendered
        /// document size to change. The rectangle uses logical pixels.
        /// </remarks>
        public event ContentsResizedEventHandler? ContentsResized;

        /// <summary>
        /// Occurs when the horizontal scroll bar is activated.
        /// </summary>
        /// <remarks>
        /// Horizontal scrolling is reserved for future RichTextBox work. The event is exposed for
        /// source compatibility but is not raised by the current renderer.
        /// </remarks>
        public event EventHandler? HScroll;

        /// <summary>
        /// Occurs when the selected text range changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Occurs when the vertical scroll bar is activated.
        /// </summary>
        public event EventHandler? VScroll;

        /// <summary>
        /// Gets or sets language and IME-related options.
        /// </summary>
        /// <remarks>
        /// The value is currently stored only. Backend-specific IME behavior is intentionally not
        /// implemented in the shared control.
        /// </remarks>
        public RichTextBoxLanguageOptions LanguageOption
        {
            get => languageOption;
            set => languageOption = value;
        }

        /// <summary>
        /// Gets or sets the RTF representation of the control contents.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext reads and writes a portable RTF subset: text, basic font style, font
        /// family, font size, foreground color, and background color. Unsupported RTF destinations
        /// are ignored rather than routed through a native Windows RichEdit control.
        /// </remarks>
        [DefaultValue("")]
        public string Rtf
        {
            get => RichTextBoxRtf.Create(Text, runs, CreateDefaultTextStyle());
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var parsed = RichTextBoxRtf.Parse(value, CreateDefaultTextStyle());
                SetTextAndRuns(parsed.Text, parsed.Runs);
            }
        }

        /// <summary>
        /// Gets or sets how scroll bars are displayed.
        /// </summary>
        /// <remarks>
        /// Values are mapped to the existing ModernFormsNext scroll bar controls. Forced horizontal
        /// modes are accepted but horizontal text scrolling is not yet implemented.
        /// </remarks>
        public new RichTextBoxScrollBars ScrollBars
        {
            get => richScrollBars;
            set
            {
                if (!Enum.IsDefined(typeof(RichTextBoxScrollBars), value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(RichTextBoxScrollBars));

                if (richScrollBars == value)
                    return;

                richScrollBars = value;
                base.ScrollBars = ToScrollBars(value);
                InvalidateRichText();
            }
        }

        /// <summary>
        /// Gets or sets the horizontal alignment for the selected paragraphs.
        /// </summary>
        /// <remarks>
        /// Paragraph-level alignment is represented as the control-wide text alignment in the
        /// current shared implementation.
        /// </remarks>
        public HorizontalAlignment SelectionAlignment
        {
            get => TextAlign switch {
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => HorizontalAlignment.Center,
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };
            set
            {
                if (!Enum.IsDefined(typeof(HorizontalAlignment), value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(HorizontalAlignment));

                TextAlign = value switch {
                    HorizontalAlignment.Center => ContentAlignment.TopCenter,
                    HorizontalAlignment.Right => ContentAlignment.TopRight,
                    _ => ContentAlignment.TopLeft
                };
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the current selection should be formatted as a bullet list.
        /// </summary>
        /// <remarks>
        /// Bullet rendering is not implemented yet. The value is stored so code that configures
        /// it can compile and preserve state.
        /// </remarks>
        public bool SelectionBullet
        {
            get => selectionBullet;
            set => selectionBullet = value;
        }

        /// <summary>
        /// Gets or sets the vertical character offset for the current selection in logical pixels.
        /// </summary>
        /// <remarks>
        /// Superscript and subscript rendering are reserved for future work. The value is stored
        /// for compatibility.
        /// </remarks>
        public int SelectionCharOffset
        {
            get => selectionCharOffset;
            set => selectionCharOffset = value;
        }

        /// <summary>
        /// Gets or sets the foreground color applied to the current selection.
        /// </summary>
        /// <remarks>
        /// When the selection is empty, assigning this property changes the formatting used by
        /// subsequently inserted text. Use <see cref="SKColor.Empty"/> to clear an explicit
        /// foreground color and return to the control or theme default.
        /// </remarks>
        public SKColor SelectionColor
        {
            get => GetCommonStyleColor(style => style.ForeColor);
            set => ApplySelectionStyle(style => style.ForeColor = value == SKColor.Empty ? null : value);
        }

        /// <summary>
        /// Gets or sets the background color applied to the current selection.
        /// </summary>
        /// <remarks>
        /// Use <see cref="SKColor.Empty"/> to clear an explicit background color.
        /// </remarks>
        public SKColor SelectionBackColor
        {
            get => GetCommonStyleColor(style => style.BackColor);
            set => ApplySelectionStyle(style => style.BackColor = value == SKColor.Empty ? null : value);
        }

        /// <summary>
        /// Gets or sets the font applied to the current selection.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> when a non-empty selection contains mixed fonts.
        /// </remarks>
        public Font? SelectionFont
        {
            get => GetCommonStyleFont();
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                ApplySelectionStyle(style => style.Font = value);
            }
        }

        /// <summary>
        /// Gets or sets the hanging indent for the current selection in logical pixels.
        /// </summary>
        /// <remarks>
        /// Paragraph indentation is stored for compatibility but not rendered yet.
        /// </remarks>
        public int SelectionHangingIndent
        {
            get => selectionHangingIndent;
            set => selectionHangingIndent = value;
        }

        /// <summary>
        /// Gets or sets the indent for the current selection in logical pixels.
        /// </summary>
        /// <remarks>
        /// Paragraph indentation is stored for compatibility but not rendered yet.
        /// </remarks>
        public int SelectionIndent
        {
            get => selectionIndent;
            set => selectionIndent = value;
        }

        /// <summary>
        /// Gets or sets the number of selected characters.
        /// </summary>
        public int SelectionLength
        {
            get => document.SelectionLength;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Selection length cannot be negative.");

                Select(SelectionStart, value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the current selection is protected.
        /// </summary>
        /// <remarks>
        /// Protected ranges are not enforced by the shared editor yet. The value is stored for
        /// API compatibility.
        /// </remarks>
        public bool SelectionProtected
        {
            get => selectionProtected;
            set => selectionProtected = value;
        }

        /// <summary>
        /// Gets or sets the right indent for the current selection in logical pixels.
        /// </summary>
        /// <remarks>
        /// Paragraph indentation is stored for compatibility but not rendered yet.
        /// </remarks>
        public int SelectionRightIndent
        {
            get => selectionRightIndent;
            set => selectionRightIndent = value;
        }

        /// <summary>
        /// Gets or sets the RTF representation of the current selection.
        /// </summary>
        public string SelectedRtf
        {
            get
            {
                var (start, length) = GetSelectionRange();
                if (length == 0)
                    return RichTextBoxRtf.Create(string.Empty, Array.Empty<RichTextBoxTextRun>(), CreateDefaultTextStyle());

                var selectedRuns = GetRunsInRange(start, length)
                    .Select(run => new RichTextBoxTextRun(run.Start - start, run.Length, run.Style))
                    .ToList();

                return RichTextBoxRtf.Create(Text.Substring(start, length), selectedRuns, CreateDefaultTextStyle());
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var parsed = RichTextBoxRtf.Parse(value, CreateDefaultTextStyle());
                ReplaceSelectionWithRichText(parsed.Text, parsed.Runs);
            }
        }

        /// <summary>
        /// Gets or sets the currently selected plain text.
        /// </summary>
        public string SelectedText
        {
            get => document.SelectedText;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (InsertText(value))
                    ScrollToCaret();
            }
        }

        /// <summary>
        /// Gets or sets tab stop positions for the current selection in logical pixels.
        /// </summary>
        /// <remarks>
        /// Tab stop rendering is not implemented yet. The array is copied on assignment and when
        /// returned so callers cannot mutate internal state by accident.
        /// </remarks>
        public int[] SelectionTabs
        {
            get => selectionTabs.ToArray();
            set => selectionTabs = value?.ToArray() ?? Array.Empty<int>();
        }

        /// <summary>
        /// Gets the type of content currently selected.
        /// </summary>
        public RichTextBoxSelectionTypes SelectionType
        {
            get
            {
                if (SelectionLength == 0)
                    return RichTextBoxSelectionTypes.Empty;

                var result = RichTextBoxSelectionTypes.Text;
                if (SelectionLength > 1)
                    result |= RichTextBoxSelectionTypes.MultiChar;

                return result;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a selection margin should be shown.
        /// </summary>
        /// <remarks>
        /// The current renderer does not draw a separate selection margin. The value is stored
        /// for compatibility.
        /// </remarks>
        public bool ShowSelectionMargin
        {
            get => showSelectionMargin;
            set => showSelectionMargin = value;
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <summary>
        /// Gets or sets the plain text of the control.
        /// </summary>
        /// <remarks>
        /// Setting this property replaces all rich formatting with the control's default style.
        /// This mirrors the normal RichTextBox behavior where plain text assignment is not an RTF
        /// formatting operation.
        /// </remarks>
        public override string Text
        {
            get => document.Text;
            set
            {
                value ??= string.Empty;

                if (document.Text == value) {
                    ResetInsertionStyle();
                    runs.Clear();
                    runs.AddRange(CreateRunsForPlainText(value));
                    InvalidateRichText();
                    RaiseContentsResizedIfNeeded();
                    return;
                }

                SetTextAndRuns(value, CreateRunsForPlainText(value));
            }
        }

        /// <summary>
        /// Gets or sets the text alignment used by the rich text layout.
        /// </summary>
        /// <remarks>
        /// This property hides <see cref="TextBox.TextAlign"/> so rich text layout can invalidate
        /// its own formatted text cache when the alignment changes.
        /// </remarks>
        public new ContentAlignment TextAlign
        {
            get => base.TextAlign;
            set
            {
                if (base.TextAlign == value)
                    return;

                base.TextAlign = value;
                InvalidateRichText();
            }
        }

        /// <summary>
        /// Gets or sets the word punctuation table identifier.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext currently uses its shared word separator logic for navigation and
        /// whole-word search. This value is retained for compatibility.
        /// </remarks>
        public RichTextBoxWordPunctuations WordPunctuations
        {
            get => wordPunctuations;
            set
            {
                if (!Enum.IsDefined(typeof(RichTextBoxWordPunctuations), value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(RichTextBoxWordPunctuations));

                wordPunctuations = value;
            }
        }

        /// <summary>
        /// Gets or sets the text zoom factor.
        /// </summary>
        /// <remarks>
        /// The factor multiplies the rendered font size only. It does not change the stored
        /// <see cref="SelectionFont"/> values or the control's base <see cref="Control.Font"/>.
        /// </remarks>
        public float ZoomFactor
        {
            get => zoomFactor;
            set
            {
                if (value < MinimumZoomFactor || value > MaximumZoomFactor)
                    throw new ArgumentOutOfRangeException(nameof(value), "ZoomFactor must be between 0.015625 and 64.");

                if (Math.Abs(zoomFactor - value) < float.Epsilon)
                    return;

                zoomFactor = value;
                InvalidateRichText();
                RaiseContentsResizedIfNeeded();
            }
        }

        /// <summary>
        /// Appends plain text to the end of the document.
        /// </summary>
        /// <param name="text">The text to append.</param>
        public void AppendText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            Select(Text.Length, 0);
            SelectedText = text;
        }

        /// <summary>
        /// Clears all text and formatting.
        /// </summary>
        public void Clear() => Text = string.Empty;

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        public void DeselectAll()
        {
            if (SelectionLength == 0)
                return;

            document.Deselect();
            Invalidate();
            OnSelectionChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Searches for text and selects the match when found.
        /// </summary>
        /// <param name="str">The text to find.</param>
        /// <returns>The zero-based match index, or -1 when the text is not found.</returns>
        public int Find(string str) => Find(str, 0, Text.Length, RichTextBoxFinds.None);

        /// <summary>
        /// Searches for text using the specified options.
        /// </summary>
        /// <param name="str">The text to find.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The zero-based match index, or -1 when the text is not found.</returns>
        public int Find(string str, RichTextBoxFinds options) => Find(str, 0, Text.Length, options);

        /// <summary>
        /// Searches for text from the specified start index.
        /// </summary>
        /// <param name="str">The text to find.</param>
        /// <param name="start">The index where searching begins.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The zero-based match index, or -1 when the text is not found.</returns>
        public int Find(string str, int start, RichTextBoxFinds options) => Find(str, start, Text.Length, options);

        /// <summary>
        /// Searches for text inside the specified range.
        /// </summary>
        /// <param name="str">The text to find.</param>
        /// <param name="start">The start index.</param>
        /// <param name="end">The exclusive end index.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The zero-based match index, or -1 when the text is not found.</returns>
        public int Find(string str, int start, int end, RichTextBoxFinds options)
        {
            ArgumentNullException.ThrowIfNull(str);
            ValidateFindRange(start, end);

            if (str.Length == 0)
                return -1;

            var comparison = options.HasFlag(RichTextBoxFinds.MatchCase) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var match = options.HasFlag(RichTextBoxFinds.Reverse)
                ? FindReverse(str, start, end, comparison, options)
                : FindForward(str, start, end, comparison, options);

            if (match >= 0 && !options.HasFlag(RichTextBoxFinds.NoHighlight))
                Select(match, str.Length);

            return match;
        }

        /// <summary>
        /// Searches for the first character from a set.
        /// </summary>
        /// <param name="characterSet">The character set to search for.</param>
        /// <returns>The zero-based match index, or -1 when none of the characters are found.</returns>
        public int Find(char[] characterSet) => Find(characterSet, 0, Text.Length);

        /// <summary>
        /// Searches for the first character from a set starting at the specified index.
        /// </summary>
        /// <param name="characterSet">The character set to search for.</param>
        /// <param name="start">The start index.</param>
        /// <returns>The zero-based match index, or -1 when none of the characters are found.</returns>
        public int Find(char[] characterSet, int start) => Find(characterSet, start, Text.Length);

        /// <summary>
        /// Searches for the first character from a set inside the specified range.
        /// </summary>
        /// <param name="characterSet">The character set to search for.</param>
        /// <param name="start">The start index.</param>
        /// <param name="end">The exclusive end index.</param>
        /// <returns>The zero-based match index, or -1 when none of the characters are found.</returns>
        public int Find(char[] characterSet, int start, int end)
        {
            ArgumentNullException.ThrowIfNull(characterSet);
            ValidateFindRange(start, end);

            for (var i = start; i < end; i++) {
                if (Array.IndexOf(characterSet, Text[i]) >= 0) {
                    Select(i, 1);
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Loads the control contents from a file as RTF.
        /// </summary>
        /// <param name="path">The file path.</param>
        public void LoadFile(string path) => LoadFile(path, RichTextBoxStreamType.RichText);

        /// <summary>
        /// Loads the control contents from a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="fileType">The expected stream type.</param>
        public void LoadFile(string path, RichTextBoxStreamType fileType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using var stream = File.OpenRead(path);
            LoadFile(stream, fileType);
        }

        /// <summary>
        /// Loads the control contents from a stream.
        /// </summary>
        /// <param name="data">The input stream.</param>
        /// <param name="fileType">The expected stream type.</param>
        public void LoadFile(Stream data, RichTextBoxStreamType fileType)
        {
            ArgumentNullException.ThrowIfNull(data);

            switch (fileType) {
                case RichTextBoxStreamType.RichText:
                case RichTextBoxStreamType.RichNoOleObjs:
                    using (var reader = new StreamReader(data, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true)) {
                        Rtf = reader.ReadToEnd();
                    }
                    break;
                case RichTextBoxStreamType.UnicodePlainText:
                    using (var reader = new StreamReader(data, Encoding.Unicode, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true)) {
                        Text = reader.ReadToEnd();
                    }
                    break;
                case RichTextBoxStreamType.PlainText:
                case RichTextBoxStreamType.TextTextOleObjs:
                    using (var reader = new StreamReader(data, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true)) {
                        Text = reader.ReadToEnd();
                    }
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(fileType), (int)fileType, typeof(RichTextBoxStreamType));
            }
        }

        /// <summary>
        /// Saves the control contents to a file as RTF.
        /// </summary>
        /// <param name="path">The file path.</param>
        public void SaveFile(string path) => SaveFile(path, RichTextBoxStreamType.RichText);

        /// <summary>
        /// Saves the control contents to a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="fileType">The output stream type.</param>
        public void SaveFile(string path, RichTextBoxStreamType fileType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using var stream = File.Create(path);
            SaveFile(stream, fileType);
        }

        /// <summary>
        /// Saves the control contents to a stream.
        /// </summary>
        /// <param name="data">The output stream.</param>
        /// <param name="fileType">The output stream type.</param>
        public void SaveFile(Stream data, RichTextBoxStreamType fileType)
        {
            ArgumentNullException.ThrowIfNull(data);

            switch (fileType) {
                case RichTextBoxStreamType.RichText:
                case RichTextBoxStreamType.RichNoOleObjs:
                    using (var writer = new StreamWriter(data, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1024, leaveOpen: true)) {
                        writer.Write(Rtf);
                    }
                    break;
                case RichTextBoxStreamType.UnicodePlainText:
                    using (var writer = new StreamWriter(data, Encoding.Unicode, bufferSize: 1024, leaveOpen: true)) {
                        writer.Write(Text);
                    }
                    break;
                case RichTextBoxStreamType.PlainText:
                case RichTextBoxStreamType.TextTextOleObjs:
                    using (var writer = new StreamWriter(data, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1024, leaveOpen: true)) {
                        writer.Write(Text);
                    }
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(fileType), (int)fileType, typeof(RichTextBoxStreamType));
            }
        }

        /// <summary>
        /// Selects a range of text.
        /// </summary>
        /// <param name="start">The zero-based start index.</param>
        /// <param name="length">The number of selected characters.</param>
        public void Select(int start, int length)
        {
            ValidateSelectionRange(start, length);
            SetSelection(start, start + length);
        }

        /// <inheritdoc/>
        protected override bool DeleteSelectedText()
            => ApplyRichTextEdit(() => document.DeleteSelection());

        /// <inheritdoc/>
        protected override bool DeleteText(bool forward, bool wholeWord)
            => ApplyRichTextEdit(() => document.DeleteText(forward, wholeWord));

        /// <inheritdoc/>
        protected override string GetSelectedTextForClipboard() => SelectedText;

        /// <inheritdoc/>
        protected override bool InsertText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var style = insertionStyle.Clone();
            if (style.Font is null && style.ForeColor is null && style.BackColor is null)
                style = GetStyleAtInsertionPoint().Clone();

            return ApplyRichTextEdit(() => document.InsertText(text), style);
        }

        /// <inheritdoc/>
        internal override bool WantsTabKey => AcceptsTab;

        /// <inheritdoc/>
        protected override bool ProcessTextBoxKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && AcceptsTab && !e.Control && !e.Alt) {
                if (InsertText("\t"))
                    ScrollToCaret();

                suppressNextTabKeyPress = true;
                e.SuppressKeyPress = true;
                return true;
            }

            if (e.KeyCode == Keys.Enter && MultiLine) {
                // Some backends report Enter as a non-text key and do not follow it with a
                // KeyPress event. Insert the newline on KeyDown, then suppress a possible
                // trailing KeyPress so Windows-style backends do not insert two line breaks.
                if (InsertText("\n"))
                    ScrollToCaret();

                suppressNextEnterKeyPress = true;
                e.SuppressKeyPress = true;
                return true;
            }

            return base.ProcessTextBoxKeyDown(e);
        }

        /// <inheritdoc/>
        protected override bool ProcessTextBoxKeyPress(KeyPressEventArgs e)
        {
            if (suppressNextEnterKeyPress) {
                suppressNextEnterKeyPress = false;

                if (e.KeyChar is '\r' or '\n') {
                    e.Handled = true;
                    return true;
                }
            }

            if (suppressNextTabKeyPress) {
                suppressNextTabKeyPress = false;

                if (e.KeyChar == '\t') {
                    e.Handled = true;
                    return true;
                }
            }

            if (e.KeyChar == '\t' && AcceptsTab) {
                if (InsertText("\t"))
                    ScrollToCaret();

                e.Handled = true;
                return true;
            }

            return base.ProcessTextBoxKeyPress(e);
        }

        /// <inheritdoc/>
        protected override void OnDeselected(EventArgs e)
        {
            var old = GetSelectionSnapshot();
            base.OnDeselected(e);
            RaiseSelectionChangedIfNeeded(old);
        }

        /// <inheritdoc/>
        protected override void OnFontChanged(EventArgs e)
        {
            InvalidateRichText();
            base.OnFontChanged(e);
        }

        /// <inheritdoc/>
        protected override void OnEnabledChanged(EventArgs e)
        {
            InvalidateRichText();
            base.OnEnabledChanged(e);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            var old = GetSelectionSnapshot();
            base.OnKeyDown(e);
            RaiseSelectionChangedIfNeeded(old);
        }

        /// <inheritdoc/>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            var old = GetSelectionSnapshot();
            base.OnKeyPress(e);
            RaiseSelectionChangedIfNeeded(old);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            var old = GetSelectionSnapshot();
            base.OnMouseDown(e);
            RaiseSelectionChangedIfNeeded(old);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var old = GetSelectionSnapshot();
            base.OnMouseMove(e);
            RaiseSelectionChangedIfNeeded(old);
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e.IsShortcutControlPressed && e.Delta.Y != 0) {
                AdjustZoomFromMouseWheel(e.Delta.Y);
                return;
            }

            base.OnMouseWheel(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            var old = GetSelectionSnapshot();
            base.OnMouseUp(e);
            RaiseSelectionChangedIfNeeded(old);
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateRichText();
            RaiseContentsResizedIfNeeded();
        }

        /// <inheritdoc/>
        internal override void OnPresentationContentMetricsChanged()
        {
            base.OnPresentationContentMetricsChanged();
            cachedRichTextBlock = null;
        }

        /// <summary>
        /// Raises the <see cref="ContentsResized"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnContentsResized(ContentsResizedEventArgs e)
            => ContentsResized?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="HScroll"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnHScroll(EventArgs e)
            => HScroll?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="SelectionChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnSelectionChanged(EventArgs e)
            => SelectionChanged?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="VScroll"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnVScroll(EventArgs e)
            => VScroll?.Invoke(this, e);

        internal RichTextBoxTextStyle CreateDefaultTextStyle()
            => new RichTextBoxTextStyle {
                Font = Font,
                ForeColor = Enabled ? CurrentStyle.GetForegroundColor() : Theme.ForegroundDisabledColor
            };

        // Syntax-aware source editors use the same RichTextKit layout as RichTextBox. Replacing
        // presentation runs does not modify Text, caret, selection, clipboard data, or raise
        // TextChanged; it only invalidates the cached visual text block.
        internal void SetPresentationRuns(IReadOnlyList<RichTextBoxTextRun> presentationRuns)
        {
            ArgumentNullException.ThrowIfNull(presentationRuns);

            runs.Clear();
            runs.AddRange(presentationRuns
                .Where(run => run.Length > 0 && run.Start >= 0 && run.Start < Text.Length)
                .Select(run => new RichTextBoxTextRun(
                    run.Start,
                    Math.Min(run.Length, Text.Length - run.Start),
                    run.Style)));
            EnsureRunCoverage();
            InvalidateRichText();
        }

        internal TextBlock GetRichTextBlock()
        {
            if (Text.Length == 0)
                return document.GetTextBlock();

            if (cachedRichTextBlock is not null)
                return cachedRichTextBlock;

            EnsureRunCoverage();

            var block = new TextBlock {
                MaxWidth = MultiLine ? PaddedClientRectangle.Width : null,
                MaxHeight = null,
                Alignment = TextMeasurer.GetTextAlign(TextAlign),
                MaxLines = MultiLine ? null : 1,
                EllipsisEnabled = false
            };

            foreach (var run in runs) {
                if (run.Length <= 0)
                    continue;

                block.AddText(Text.Substring(run.Start, run.Length), ToRichTextKitStyle(run.Style));
            }

            if (Text[^1] is '\n' or '\r') {
                // RichTextKit reports the caret at the end of a trailing line break on the
                // previous visual line unless the block has a real next-line run. A zero-width
                // marker gives the empty final line measurable height without adding a character
                // to the RichTextBox document model.
                block.AddText(TrailingLineBreakCaretMarker, ToRichTextKitStyle(GetStyleAtInsertionPoint()));
            }

            return cachedRichTextBlock = block;
        }

        private bool ApplyRichTextEdit(Func<bool> edit, RichTextBoxTextStyle? insertedStyle = null)
        {
            var oldText = Text;
            var oldSelection = GetSelectionSnapshot();
            var changed = edit();

            if (!changed)
                return false;

            var diff = CalculateDiff(oldText, Text);
            ReplaceRunRange(diff.Start, diff.RemovedLength, diff.InsertedLength, insertedStyle ?? GetStyleAtInsertionPoint());
            InvalidateRichText();

            if (oldText != Text)
                OnTextChanged(EventArgs.Empty);

            RaiseSelectionChangedIfNeeded(oldSelection);
            RaiseContentsResizedIfNeeded();

            return true;
        }

        private void ApplySelectionStyle(Action<RichTextBoxTextStyle> apply)
        {
            ArgumentNullException.ThrowIfNull(apply);
            var (start, length) = GetSelectionRange();

            if (length == 0) {
                apply(insertionStyle);
                return;
            }

            EnsureRunCoverage();
            SplitRunAt(start);
            SplitRunAt(start + length);

            foreach (var run in runs.Where(run => run.Start >= start && run.End <= start + length))
                apply(run.Style);

            MergeAdjacentRuns();
            InvalidateRichText();
            RaiseContentsResizedIfNeeded();
        }

        private void AdjustZoomFromMouseWheel(int deltaY)
        {
            var steps = Math.Max(1, Math.Abs(deltaY));
            var multiplier = Math.Pow(MouseWheelZoomStep, steps);
            var next = deltaY > 0 ? zoomFactor * multiplier : zoomFactor / multiplier;
            ZoomFactor = Math.Clamp((float)next, MinimumZoomFactor, MaximumZoomFactor);
        }

        private static (int Start, int RemovedLength, int InsertedLength) CalculateDiff(string oldText, string newText)
        {
            var prefix = 0;
            while (prefix < oldText.Length && prefix < newText.Length && oldText[prefix] == newText[prefix])
                prefix++;

            var oldSuffix = oldText.Length - 1;
            var newSuffix = newText.Length - 1;
            while (oldSuffix >= prefix && newSuffix >= prefix && oldText[oldSuffix] == newText[newSuffix]) {
                oldSuffix--;
                newSuffix--;
            }

            return (prefix, oldSuffix - prefix + 1, newSuffix - prefix + 1);
        }

        private List<RichTextBoxTextRun> CreateRunsForPlainText(string text)
        {
            if (text.Length == 0)
                return new List<RichTextBoxTextRun>();

            return new List<RichTextBoxTextRun> {
                new RichTextBoxTextRun(0, text.Length, new RichTextBoxTextStyle())
            };
        }

        private void EnsureRunCoverage()
        {
            if (Text.Length == 0) {
                runs.Clear();
                return;
            }

            if (runs.Count == 0) {
                runs.Add(new RichTextBoxTextRun(0, Text.Length, new RichTextBoxTextStyle()));
                return;
            }

            runs.Sort((left, right) => left.Start.CompareTo(right.Start));
            var cursor = 0;

            for (var i = 0; i < runs.Count; i++) {
                var run = runs[i];

                if (run.Start > cursor) {
                    runs.Insert(i, new RichTextBoxTextRun(cursor, run.Start - cursor, new RichTextBoxTextStyle()));
                    i++;
                } else if (run.Start < cursor) {
                    var trim = cursor - run.Start;
                    run.Start = cursor;
                    run.Length -= trim;
                }

                if (run.Length <= 0) {
                    runs.RemoveAt(i--);
                    continue;
                }

                if (run.End > Text.Length)
                    run.Length = Text.Length - run.Start;

                cursor = run.End;
            }

            if (cursor < Text.Length)
                runs.Add(new RichTextBoxTextRun(cursor, Text.Length - cursor, new RichTextBoxTextStyle()));

            MergeAdjacentRuns();
        }

        private int FindForward(string str, int start, int end, StringComparison comparison, RichTextBoxFinds options)
        {
            var index = Text.IndexOf(str, start, end - start, comparison);
            while (index >= 0) {
                if (!options.HasFlag(RichTextBoxFinds.WholeWord) || IsWholeWord(index, str.Length))
                    return index;

                var nextStart = index + 1;
                if (nextStart >= end)
                    return -1;

                index = Text.IndexOf(str, nextStart, end - nextStart, comparison);
            }

            return -1;
        }

        private int FindReverse(string str, int start, int end, StringComparison comparison, RichTextBoxFinds options)
        {
            var searchText = Text.Substring(start, end - start);
            var index = searchText.LastIndexOf(str, comparison);
            while (index >= 0) {
                var absolute = start + index;
                if (!options.HasFlag(RichTextBoxFinds.WholeWord) || IsWholeWord(absolute, str.Length))
                    return absolute;

                if (index == 0)
                    return -1;

                index = searchText.LastIndexOf(str, index - 1, comparison);
            }

            return -1;
        }

        private SKColor GetCommonStyleColor(Func<RichTextBoxTextStyle, SKColor?> selector)
        {
            var styles = GetSelectedStyles();
            SKColor? result = null;
            var hasValue = false;

            foreach (var style in styles) {
                var color = selector(style);
                if (!hasValue) {
                    result = color;
                    hasValue = true;
                } else if (!Nullable.Equals(result, color)) {
                    return SKColor.Empty;
                }
            }

            return result ?? SKColor.Empty;
        }

        private Font? GetCommonStyleFont()
        {
            var styles = GetSelectedStyles();
            Font? result = null;
            var hasValue = false;

            foreach (var style in styles) {
                var font = style.Font ?? Font;
                if (!hasValue) {
                    result = font;
                    hasValue = true;
                } else if (!Equals(result, font)) {
                    return null;
                }
            }

            return result ?? Font;
        }

        private (int Start, int Length) GetSelectionRange()
        {
            if (SelectionLength == 0)
                return (Math.Clamp(document.CursorIndex, 0, Text.Length), 0);

            var start = Math.Min(SelectionStart, SelectionEnd);
            return (start, SelectionLength);
        }

        private (int Start, int End, int Cursor) GetSelectionSnapshot()
            => (SelectionStart, SelectionEnd, document.CursorIndex);

        private IEnumerable<RichTextBoxTextStyle> GetSelectedStyles()
        {
            var (start, length) = GetSelectionRange();

            if (length == 0)
                return new[] { GetStyleAtInsertionPoint() };

            return GetRunsInRange(start, length).Select(run => run.Style);
        }

        private RichTextBoxTextStyle GetStyleAtInsertionPoint()
        {
            EnsureRunCoverage();

            if (runs.Count == 0)
                return new RichTextBoxTextStyle();

            var index = Math.Clamp(document.CursorIndex, 0, Math.Max(0, Text.Length - 1));
            return runs.FirstOrDefault(run => index >= run.Start && index < run.End)?.Style.Clone()
                ?? runs.Last().Style.Clone();
        }

        private IEnumerable<RichTextBoxTextRun> GetRunsInRange(int start, int length)
        {
            EnsureRunCoverage();
            var end = start + length;

            foreach (var run in runs) {
                var overlapStart = Math.Max(start, run.Start);
                var overlapEnd = Math.Min(end, run.End);

                if (overlapEnd > overlapStart)
                    yield return new RichTextBoxTextRun(overlapStart, overlapEnd - overlapStart, run.Style);
            }
        }

        private void InvalidateRichText()
        {
            cachedRichTextBlock = null;
            Invalidate();
        }

        private bool IsWholeWord(int index, int length)
        {
            var before = index == 0 || TextMeasurer.IsWordSeparator(Text[index - 1]);
            var afterIndex = index + length;
            var after = afterIndex >= Text.Length || TextMeasurer.IsWordSeparator(Text[afterIndex]);
            return before && after;
        }

        private void MergeAdjacentRuns()
        {
            runs.Sort((left, right) => left.Start.CompareTo(right.Start));

            for (var i = runs.Count - 1; i > 0; i--) {
                var current = runs[i];
                var previous = runs[i - 1];

                if (previous.End == current.Start && previous.Style.Equals(current.Style)) {
                    previous.Length += current.Length;
                    runs.RemoveAt(i);
                }
            }
        }

        private void RaiseContentsResizedIfNeeded()
        {
            var block = GetRichTextBlock();
            var rectangle = new Rectangle(
                PaddedClientRectangle.X,
                PaddedClientRectangle.Y,
                Math.Max(0, (int)Math.Ceiling(block.MeasuredWidth)),
                Math.Max(0, (int)Math.Ceiling(block.MeasuredHeight)));

            if (rectangle == lastContentsRectangle)
                return;

            lastContentsRectangle = rectangle;
            OnContentsResized(new ContentsResizedEventArgs(rectangle));
        }

        private void RaiseSelectionChangedIfNeeded((int Start, int End, int Cursor) oldSelection)
        {
            var current = GetSelectionSnapshot();
            if (oldSelection != current)
                OnSelectionChanged(EventArgs.Empty);
        }

        private void ReplaceRunRange(int start, int removedLength, int insertedLength, RichTextBoxTextStyle insertedStyle)
        {
            EnsureRunCoverage();
            SplitRunAt(start);
            SplitRunAt(start + removedLength);

            for (var i = runs.Count - 1; i >= 0; i--) {
                var run = runs[i];
                if (run.Start >= start && run.End <= start + removedLength)
                    runs.RemoveAt(i);
            }

            var delta = insertedLength - removedLength;
            foreach (var run in runs.Where(run => run.Start >= start + removedLength))
                run.Start += delta;

            if (insertedLength > 0)
                runs.Add(new RichTextBoxTextRun(start, insertedLength, insertedStyle));

            MergeAdjacentRuns();
            EnsureRunCoverage();
        }

        private void ReplaceSelectionWithRichText(string text, IReadOnlyList<RichTextBoxTextRun> newRuns)
        {
            var (start, removedLength) = GetSelectionRange();
            var oldText = Text;
            var oldSelection = GetSelectionSnapshot();

            if (!document.DeleteSelection() && removedLength > 0)
                return;

            if (text.Length > 0)
                document.InsertText(text);

            var diff = CalculateDiff(oldText, Text);
            ReplaceRunRange(diff.Start, diff.RemovedLength, diff.InsertedLength, new RichTextBoxTextStyle());

            if (text.Length > 0) {
                SplitRunAt(start);
                SplitRunAt(start + text.Length);
                runs.RemoveAll(run => run.Start >= start && run.End <= start + text.Length);

                foreach (var run in newRuns)
                    runs.Add(new RichTextBoxTextRun(start + run.Start, Math.Min(run.Length, text.Length - run.Start), run.Style));

                EnsureRunCoverage();
            }

            InvalidateRichText();

            if (oldText != Text)
                OnTextChanged(EventArgs.Empty);

            RaiseSelectionChangedIfNeeded(oldSelection);
            RaiseContentsResizedIfNeeded();
            ScrollToCaret();
        }

        private void SetSelection(int start, int end)
        {
            var old = GetSelectionSnapshot();

            document.SelectionStart = start;
            document.SelectionEnd = end;
            document.SetCursorToCharIndex(end);

            Invalidate();
            RaiseSelectionChangedIfNeeded(old);
            ScrollToCaret();
        }

        private void SetTextAndRuns(string text, List<RichTextBoxTextRun> newRuns)
        {
            var oldSelection = GetSelectionSnapshot();
            var oldText = document.Text;

            document.Text = text;
            ResetInsertionStyle();
            runs.Clear();
            runs.AddRange(newRuns.Where(run => run.Length > 0).Select(run => run.Clone()));
            EnsureRunCoverage();
            InvalidateRichText();
            ScrollToCaret();

            if (oldText != text)
                OnTextChanged(EventArgs.Empty);

            RaiseSelectionChangedIfNeeded(oldSelection);
            RaiseContentsResizedIfNeeded();
        }

        private void ResetInsertionStyle()
        {
            insertionStyle.BackColor = null;
            insertionStyle.Font = null;
            insertionStyle.ForeColor = null;
        }

        private void SplitRunAt(int index)
        {
            if (index <= 0 || index >= Text.Length)
                return;

            for (var i = 0; i < runs.Count; i++) {
                var run = runs[i];
                if (index <= run.Start || index >= run.End)
                    continue;

                var rightLength = run.End - index;
                run.Length = index - run.Start;
                runs.Insert(i + 1, new RichTextBoxTextRun(index, rightLength, run.Style));
                return;
            }
        }

        private Style ToRichTextKitStyle(RichTextBoxTextStyle textStyle)
        {
            var font = textStyle.Font ?? Font;
            var fontSize = LogicalToDeviceUnits(Math.Max(1, (int)Math.Round(font.SizeInPoints * zoomFactor)));
            var style = font.Style;

            return new Style {
                FontFamily = font.FamilyName,
                FontSize = fontSize,
                TextColor = Enabled ? textStyle.ForeColor ?? CurrentStyle.GetForegroundColor() : Theme.ForegroundDisabledColor,
                BackgroundColor = textStyle.BackColor ?? SKColor.Empty,
                FontWeight = style.HasFlag(FontStyle.Bold) ? (int)SKFontStyleWeight.Bold : (int)SKFontStyleWeight.Normal,
                FontItalic = style.HasFlag(FontStyle.Italic),
                Underline = style.HasFlag(FontStyle.Underline) ? UnderlineStyle.Solid : UnderlineStyle.None,
                StrikeThrough = style.HasFlag(FontStyle.Strikeout) ? StrikeThroughStyle.Solid : StrikeThroughStyle.None
            };
        }

        private static ModernFormsNext.ScrollBars ToScrollBars(RichTextBoxScrollBars value)
        {
            var horizontal = (value & RichTextBoxScrollBars.Horizontal) == RichTextBoxScrollBars.Horizontal;
            var vertical = (value & RichTextBoxScrollBars.Vertical) == RichTextBoxScrollBars.Vertical;

            return (horizontal, vertical) switch {
                (true, true) => ModernFormsNext.ScrollBars.Both,
                (true, false) => ModernFormsNext.ScrollBars.Horizontal,
                (false, true) => ModernFormsNext.ScrollBars.Vertical,
                _ => ModernFormsNext.ScrollBars.None
            };
        }

        private void ValidateFindRange(int start, int end)
        {
            if (start < 0 || start > Text.Length)
                throw new ArgumentOutOfRangeException(nameof(start));

            if (end < start || end > Text.Length)
                throw new ArgumentOutOfRangeException(nameof(end));
        }

        private void ValidateSelectionRange(int start, int length)
        {
            if (start < 0 || start > Text.Length)
                throw new ArgumentOutOfRangeException(nameof(start));

            if (length < 0 || start + length > Text.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
        }
    }
}
