using System;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Defines the style of a control.
    /// </summary>
    public class ControlStyle
    {
        internal readonly ControlStyle? _parent;
        private SKTypeface? font;
        private Font? textFont;

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the static DefaultStyle property.
        /// </summary>
        public ControlStyle (ControlStyle? parent, Action<ControlStyle> setDefaults)
        {
            _parent = parent;

            Border = new BorderStyle (parent?.Border);

            setDefaults (this);

            Theme.ThemeChanged += (o, e) => setDefaults (this);
        }

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the instance Style property.
        /// </summary>
        public ControlStyle (ControlStyle parent)
        {
            _parent = parent;

            Border = new BorderStyle (parent?.Border);
        }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public SKColor? BackgroundColor { get; set; }

        /// <summary>
        /// Provides access to border style properties.
        /// </summary>
        public BorderStyle Border { get; }

        /// <summary>
        /// Gets or sets the SkiaSharp typeface used to render text.
        /// </summary>
        /// <remarks>
        /// This low-level property is kept for existing renderer and theme code. It can describe
        /// the typeface family, weight, and italic slant, but it cannot carry text effects such as
        /// underline or strikeout. Prefer <see cref="TextFont"/> when applying a
        /// <see cref="ModernFormsNext.Font"/> selected by <see cref="FontDialog"/>.
        /// </remarks>
        public SKTypeface? Font
        {
            get => font;
            set
            {
                font = value;
                textFont = null;
            }
        }

        /// <summary>
        /// Gets or sets the font size.
        /// </summary>
        public int? FontSize { get; set; }

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        public SKColor? ForegroundColor { get; set; }

        /// <summary>
        /// Gets or sets the ModernFormsNext font used to render text.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Font"/>, this property preserves the full
        /// <see cref="ModernFormsNext.FontStyle"/> value, including
        /// <see cref="ModernFormsNext.FontStyle.Underline"/> and
        /// <see cref="ModernFormsNext.FontStyle.Strikeout"/>. Assign fonts returned from
        /// <see cref="FontDialog"/> here when text effects should be rendered by framework
        /// controls.
        /// </remarks>
        /// <example>
        /// <code>
        /// var dialog = new FontDialog();
        /// if (await dialog.ShowDialog(form) == DialogResult.OK)
        /// {
        ///     label.Style.TextFont = dialog.Font;
        ///     label.Invalidate();
        /// }
        /// </code>
        /// </example>
        public Font? TextFont
        {
            get => textFont;
            set
            {
                textFont = value;
                font = value?.ToTypeface();
            }
        }

        /// <summary>
        /// Gets the computed background color.
        /// </summary>
        public SKColor GetBackgroundColor () => BackgroundColor ?? _parent?.GetBackgroundColor () ?? Theme.ControlMidColor;

        /// <summary>
        /// Gets the computed font.
        /// </summary>
        public SKTypeface GetFont () => font ?? _parent?.GetFont () ?? Theme.UIFont;

        /// <summary>
        /// Gets the computed font size.
        /// </summary>
        public int GetFontSize ()
            => FontSize
                ?? (textFont is null ? null : (int?)Math.Max(1, (int)Math.Round(textFont.SizeInPoints)))
                ?? _parent?.GetFontSize ()
                ?? Theme.FontSize;

        /// <summary>
        /// Gets the computed ModernFormsNext font style flags.
        /// </summary>
        /// <remarks>
        /// The value is derived from <see cref="TextFont"/> when available. When a caller uses the
        /// lower-level <see cref="Font"/> property, only typeface properties available from
        /// <see cref="SKTypeface"/> can be inferred.
        /// </remarks>
        public FontStyle GetFontStyle()
        {
            if (textFont is not null)
                return textFont.Style;

            if (font is not null)
                return GetFontStyle(font);

            return _parent?.GetFontStyle() ?? GetFontStyle(Theme.UIFont);
        }

        /// <summary>
        /// Gets the computed foreground color.
        /// </summary>
        public SKColor GetForegroundColor () => ForegroundColor ?? _parent?.GetForegroundColor () ?? Theme.ForegroundColor;

        private static FontStyle GetFontStyle(SKTypeface typeface)
        {
            var style = FontStyle.Regular;

            if (typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold)
                style |= FontStyle.Bold;

            if (typeface.FontSlant == SKFontStyleSlant.Italic || typeface.FontSlant == SKFontStyleSlant.Oblique)
                style |= FontStyle.Italic;

            return style;
        }
    }
}
