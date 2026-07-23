using System;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Defines the style of a control.
    /// </summary>
    public class ControlStyle
    {
        private SKTypeface? font;
        private Font? textFont;

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the static DefaultStyle property.
        /// </summary>
        /// <param name="parent">The style from which unset values are inherited, or <see langword="null"/> for a root style.</param>
        /// <param name="setDefaults">The callback that assigns this style's defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setDefaults"/> is <see langword="null"/>.</exception>
        public ControlStyle (ControlStyle? parent, Action<ControlStyle> setDefaults)
        {
            ArgumentNullException.ThrowIfNull(setDefaults);

            ParentStyle = parent;

            Border = new BorderStyle (parent?.Border);

            setDefaults (this);

            Theme.ThemeChanged += (o, e) => setDefaults (this);
        }

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the instance Style property.
        /// </summary>
        /// <param name="parent">The style from which unset values are inherited, or <see langword="null"/> for a detached root style.</param>
        public ControlStyle (ControlStyle? parent)
        {
            ParentStyle = parent;

            Border = new BorderStyle (parent?.Border);
        }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public SKColor? BackgroundColor { get; set; }

        /// <summary>Gets or sets the optional state-specific background brush.</summary>
        /// <remarks>
        /// The control-level <see cref="Control.BackgroundBrush"/> takes precedence. Compatible
        /// built-in brushes are interpolated by visual-state transitions.
        /// </remarks>
        public Brush? BackgroundBrush { get; set; }

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

        /// <summary>Gets or sets the optional state-specific text brush.</summary>
        public Brush? ForegroundBrush { get; set; }

        /// <summary>Gets or sets the optional state-specific border brush.</summary>
        public Brush? BorderBrush { get; set; }

        /// <summary>Gets or sets the state opacity multiplier, or null for 1.</summary>
        public float? Opacity { get; set; }

        /// <summary>Gets or sets the state horizontal translation added at render time.</summary>
        public float? TranslationX { get; set; }

        /// <summary>Gets or sets the state vertical translation added at render time.</summary>
        public float? TranslationY { get; set; }

        /// <summary>Gets or sets the state horizontal scale multiplier, or null for 1.</summary>
        public float? ScaleX { get; set; }

        /// <summary>Gets or sets the state vertical scale multiplier, or null for 1.</summary>
        public float? ScaleY { get; set; }

        /// <summary>Gets or sets the state rotation added in degrees at render time.</summary>
        public float? Rotation { get; set; }

        /// <summary>
        /// Gets the style from which unset values are inherited.
        /// </summary>
        /// <remarks>
        /// A root or detached style can have no parent. Value resolution does not require an
        /// owning control, visual parent, form, window, or initialized platform backend. Cyclic
        /// chains are treated as exhausted after every unique style has been inspected once.
        /// </remarks>
        public ControlStyle? ParentStyle { get; internal set; }

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
        public SKColor GetBackgroundColor ()
            => ResolveValue(TryGetBackgroundColor, static () => Theme.ControlMidColor);

        /// <summary>
        /// Gets the computed font.
        /// </summary>
        /// <remarks>
        /// When neither this style nor an inherited style provides a typeface, the framework UI
        /// font is returned. The theme guarantees that this fallback is non-null on every backend.
        /// </remarks>
        public SKTypeface GetFont ()
            => ResolveValue(TryGetFont, static () => Theme.UIFont);

        /// <summary>
        /// Gets the family name of the computed font.
        /// </summary>
        public string GetFontFamily () => GetFont ().FamilyName;

        /// <summary>
        /// Gets the computed font size.
        /// </summary>
        public int GetFontSize ()
            => ResolveValue(TryGetFontSize, static () => Math.Max(1, Theme.FontSize));

        /// <summary>
        /// Gets the computed ModernFormsNext font style flags.
        /// </summary>
        /// <remarks>
        /// The value is derived from <see cref="TextFont"/> when available. When a caller uses the
        /// lower-level <see cref="Font"/> property, only typeface properties available from
        /// <see cref="SKTypeface"/> can be inferred.
        /// </remarks>
        public FontStyle GetFontStyle()
            => ResolveValue(TryGetFontStyle, static () => GetFontStyle(Theme.UIFont));

        /// <summary>
        /// Gets the numeric SkiaSharp weight of the computed font.
        /// </summary>
        /// <remarks>
        /// The returned value uses the same numeric scale as <see cref="SKTypeface.FontWeight"/>.
        /// </remarks>
        public int GetFontWeight () => GetFont ().FontWeight;

        /// <summary>
        /// Gets the computed foreground color.
        /// </summary>
        public SKColor GetForegroundColor ()
            => ResolveValue(TryGetForegroundColor, static () => Theme.ForegroundColor);

        internal Brush? GetResolvedBackgroundBrush()
            => ResolveReferenceValue(static style => style.BackgroundBrush);

        internal Brush? GetResolvedForegroundBrush()
            => ResolveReferenceValue(static style => style.ForegroundBrush);

        internal Brush? GetResolvedBorderBrush()
            => ResolveReferenceValue(static style => style.BorderBrush);

        internal float? GetResolvedOpacity()
            => ResolveNullableValue(static style => style.Opacity);

        internal float? GetResolvedTranslationX()
            => ResolveNullableValue(static style => style.TranslationX);

        internal float? GetResolvedTranslationY()
            => ResolveNullableValue(static style => style.TranslationY);

        internal float? GetResolvedScaleX()
            => ResolveNullableValue(static style => style.ScaleX);

        internal float? GetResolvedScaleY()
            => ResolveNullableValue(static style => style.ScaleY);

        internal float? GetResolvedRotation()
            => ResolveNullableValue(static style => style.Rotation);

        internal int GetInheritanceTraversalLimit ()
            => StyleInheritanceTraversal.GetLimit(this, static style => style.ParentStyle);

        internal static FontStyle GetFontStyle(SKTypeface? typeface)
        {
            if (typeface is null)
                return FontStyle.Regular;

            var style = FontStyle.Regular;

            if (typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold)
                style |= FontStyle.Bold;

            if (typeface.FontSlant == SKFontStyleSlant.Italic || typeface.FontSlant == SKFontStyleSlant.Oblique)
                style |= FontStyle.Italic;

            return style;
        }

        private T ResolveValue<T> (TryGetStyleValue<T> tryGetValue, Func<T> getFallback)
        {
            FontResolutionDiagnostics.RecordStyleResolverCall();
            var remaining = GetInheritanceTraversalLimit ();
            ControlStyle? current = this;

            while (current is not null && remaining-- > 0)
            {
                FontResolutionDiagnostics.RecordStyleNodeVisited();
                if (tryGetValue(current, out var value))
                    return value;

                current = current.ParentStyle;
            }

            return getFallback ();
        }

        private T? ResolveReferenceValue<T>(Func<ControlStyle, T?> getValue)
            where T : class
        {
            var remaining = GetInheritanceTraversalLimit();
            ControlStyle? current = this;
            while (current is not null && remaining-- > 0)
            {
                if (getValue(current) is { } value)
                    return value;
                current = current.ParentStyle;
            }
            return null;
        }

        private T? ResolveNullableValue<T>(Func<ControlStyle, T?> getValue)
            where T : struct
        {
            var remaining = GetInheritanceTraversalLimit();
            ControlStyle? current = this;
            while (current is not null && remaining-- > 0)
            {
                if (getValue(current) is { } value)
                    return value;
                current = current.ParentStyle;
            }
            return null;
        }

        private static bool TryGetBackgroundColor (ControlStyle style, out SKColor value)
        {
            if (style.BackgroundColor is { } color)
            {
                value = color;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryGetFont (ControlStyle style, out SKTypeface value)
        {
            if (style.font is { } typeface)
            {
                value = typeface;
                return true;
            }

            value = null!;
            return false;
        }

        private static bool TryGetFontSize (ControlStyle style, out int value)
        {
            if (style.FontSize is { } size)
            {
                value = size;
                return true;
            }

            if (style.textFont is { } textStyle)
            {
                value = Math.Max(1, (int)Math.Round(textStyle.SizeInPoints));
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryGetFontStyle (ControlStyle style, out FontStyle value)
        {
            if (style.textFont is { } textStyle)
            {
                value = textStyle.Style;
                return true;
            }

            if (style.font is { } typeface)
            {
                value = GetFontStyle(typeface);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryGetForegroundColor (ControlStyle style, out SKColor value)
        {
            if (style.ForegroundColor is { } color)
            {
                value = color;
                return true;
            }

            value = default;
            return false;
        }

        private delegate bool TryGetStyleValue<T> (ControlStyle style, out T value);
    }
}
