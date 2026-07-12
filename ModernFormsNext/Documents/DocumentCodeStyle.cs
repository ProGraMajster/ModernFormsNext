using System;
using SkiaSharp;

namespace ModernFormsNext.Documents;

/// <summary>
/// Defines semantic colors used to render highlighted fenced code blocks.
/// </summary>
/// <remarks>
/// Nullable values derive from the active <see cref="Theme"/>. Mutating this object invalidates
/// every <see cref="DocumentViewer"/> that owns its parent <see cref="DocumentStyle"/>.
/// </remarks>
public sealed class DocumentCodeStyle
{
    private SKColor? keywordColor;
    private SKColor? stringColor;
    private SKColor? numberColor;
    private SKColor? commentColor;
    private SKColor? typeColor;
    private SKColor? propertyColor;
    private SKColor? punctuationColor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentCodeStyle"/> class.
    /// </summary>
    public DocumentCodeStyle()
    {
    }

    internal event EventHandler? Changed;

    /// <summary>Gets or sets the color used for language keywords.</summary>
    public SKColor? KeywordColor
    {
        get => keywordColor;
        set => SetValue(ref keywordColor, value);
    }

    /// <summary>Gets or sets the color used for string and character literals.</summary>
    public SKColor? StringColor
    {
        get => stringColor;
        set => SetValue(ref stringColor, value);
    }

    /// <summary>Gets or sets the color used for numeric literals.</summary>
    public SKColor? NumberColor
    {
        get => numberColor;
        set => SetValue(ref numberColor, value);
    }

    /// <summary>Gets or sets the color used for comments.</summary>
    public SKColor? CommentColor
    {
        get => commentColor;
        set => SetValue(ref commentColor, value);
    }

    /// <summary>Gets or sets the color used for built-in and declared type names.</summary>
    public SKColor? TypeColor
    {
        get => typeColor;
        set => SetValue(ref typeColor, value);
    }

    /// <summary>Gets or sets the color used for properties, attributes, and variables.</summary>
    public SKColor? PropertyColor
    {
        get => propertyColor;
        set => SetValue(ref propertyColor, value);
    }

    /// <summary>Gets or sets the color used for punctuation recognized by a highlighter.</summary>
    public SKColor? PunctuationColor
    {
        get => punctuationColor;
        set => SetValue(ref punctuationColor, value);
    }

    internal SKColor Resolve(DocumentSyntaxTokenKind kind, SKColor fallback, bool enabled)
    {
        if (!enabled)
            return fallback;

        return kind switch
        {
            DocumentSyntaxTokenKind.Keyword => KeywordColor ?? Theme.AccentColor2,
            DocumentSyntaxTokenKind.String => StringColor ?? Theme.AccentColor,
            DocumentSyntaxTokenKind.Number => NumberColor ?? Theme.WarningHighlightColor,
            DocumentSyntaxTokenKind.Comment => CommentColor ?? Theme.ForegroundDisabledColor,
            DocumentSyntaxTokenKind.Type => TypeColor ?? Theme.AccentColor,
            DocumentSyntaxTokenKind.Property => PropertyColor ?? Theme.AccentColor2,
            DocumentSyntaxTokenKind.Punctuation => PunctuationColor ?? fallback,
            _ => fallback
        };
    }

    private void SetValue<T>(ref T field, T value)
    {
        if (Equals(field, value))
            return;

        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
