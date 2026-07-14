using System;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Describes a font family, point size, and style selected by ModernFormsNext APIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ModernFormsNext renders text through SkiaSharp, so this type stores the portable
    /// information that can be mapped to an <see cref="SKTypeface"/> without exposing
    /// backend-specific font handles.
    /// </para>
    /// <para>
    /// The size is expressed in points to match common desktop font dialogs and WinForms
    /// migration expectations. Most ModernFormsNext controls currently use integer font
    /// sizes in their <see cref="ControlStyle"/>, so callers should round or clamp the
    /// value when assigning it to <see cref="ControlStyle.FontSize"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var font = new Font("Segoe UI", 12, FontStyle.Bold | FontStyle.Italic);
    ///
    /// label.Font = font;
    /// </code>
    /// </example>
    public sealed class Font : IEquatable<Font>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Font"/> class with regular styling.
        /// </summary>
        /// <param name="familyName">The font family name, such as <c>Segoe UI</c>.</param>
        /// <param name="emSize">The font size in points.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="familyName"/> is empty or only whitespace.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="emSize"/> is less than or equal to zero.
        /// </exception>
        public Font(string familyName, float emSize)
            : this(familyName, emSize, FontStyle.Regular)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Font"/> class.
        /// </summary>
        /// <param name="familyName">The font family name, such as <c>Segoe UI</c>.</param>
        /// <param name="emSize">The font size in points.</param>
        /// <param name="style">The style flags applied to the font.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="familyName"/> is empty or only whitespace.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="emSize"/> is less than or equal to zero.
        /// </exception>
        public Font(string familyName, float emSize, FontStyle style)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                throw new ArgumentException("The font family name cannot be empty.", nameof(familyName));

            if (emSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(emSize), "The font size must be greater than zero.");

            FamilyName = familyName;
            SizeInPoints = emSize;
            Style = style;
        }

        /// <summary>
        /// Gets a value indicating whether the font style includes <see cref="FontStyle.Bold"/>.
        /// </summary>
        public bool Bold => Style.HasFlag(FontStyle.Bold);

        /// <summary>
        /// Gets the font family name.
        /// </summary>
        public string FamilyName { get; }

        /// <summary>
        /// Gets a value indicating whether the font style includes <see cref="FontStyle.Italic"/>.
        /// </summary>
        public bool Italic => Style.HasFlag(FontStyle.Italic);

        /// <summary>
        /// Gets the font family name.
        /// </summary>
        /// <remarks>
        /// This alias mirrors the familiar WinForms <c>Font.Name</c> property and returns
        /// the same value as <see cref="FamilyName"/>.
        /// </remarks>
        public string Name => FamilyName;

        /// <summary>
        /// Gets the font size in points.
        /// </summary>
        public float Size => SizeInPoints;

        /// <summary>
        /// Gets the font size in points.
        /// </summary>
        public float SizeInPoints { get; }

        /// <summary>
        /// Gets a value indicating whether the font style includes <see cref="FontStyle.Strikeout"/>.
        /// </summary>
        public bool Strikeout => Style.HasFlag(FontStyle.Strikeout);

        /// <summary>
        /// Gets the style flags applied to this font.
        /// </summary>
        public FontStyle Style { get; }

        /// <summary>
        /// Gets a value indicating whether the font style includes <see cref="FontStyle.Underline"/>.
        /// </summary>
        public bool Underline => Style.HasFlag(FontStyle.Underline);

        /// <summary>
        /// Gets a shared <see cref="SKTypeface"/> for the family and typeface style represented by this font.
        /// </summary>
        /// <returns>A SkiaSharp typeface suitable for assigning to <see cref="ControlStyle.Font"/>.</returns>
        /// <remarks>
        /// <see cref="FontStyle.Underline"/> and <see cref="FontStyle.Strikeout"/> are not part
        /// of SkiaSharp typeface selection and are therefore not represented in the returned
        /// <see cref="SKTypeface"/>. Use <see cref="Control.Font"/> or
        /// <see cref="ControlStyle.TextFont"/> when applying a ModernFormsNext font to controls
        /// that should render those text effects. When the requested family is unavailable, the
        /// framework uses the platform's default family with the requested weight and slant, then
        /// falls back to SkiaSharp's guaranteed non-null default typeface. Returned typefaces are
        /// held in a bounded process-wide cache and can be shared by many controls. Callers must
        /// not dispose the returned instance.
        /// </remarks>
        public SKTypeface ToTypeface()
        {
            var weight = Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

            return Theme.CreateTypefaceOrDefault(FamilyName, weight, slant);
        }

        /// <inheritdoc/>
        public bool Equals(Font? other)
        {
            return other is not null
                && string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal)
                && SizeInPoints.Equals(other.SizeInPoints)
                && Style == other.Style;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as Font);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(FamilyName, SizeInPoints, Style);

        /// <inheritdoc/>
        public override string ToString() => $"{FamilyName}, {SizeInPoints:g}pt, {Style}";
    }
}
