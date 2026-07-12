using System;
using SkiaSharp;

namespace ModernFormsNext.Documents;

/// <summary>
/// Defines visual styling for document layout and rendering.
/// </summary>
/// <remarks>
/// <para>
/// Values are expressed in logical pixels unless otherwise stated. <see cref="DocumentViewer"/>
/// scales them through the normal ModernFormsNext DPI pipeline before measuring or rendering
/// text.
/// </para>
/// <para>
/// Color properties are nullable so the default style can derive colors from the active
/// <see cref="Theme"/> and the viewer's <see cref="Control.CurrentStyle"/>.
/// </para>
/// </remarks>
public sealed class DocumentStyle
{
    private readonly DocumentCodeStyle codeStyle = new();
    private SKColor? foregroundColor;
    private SKColor? headingColor;
    private SKColor? linkColor;
    private SKColor? hoveredLinkColor;
    private SKColor? pressedLinkColor;
    private SKColor? codeForegroundColor;
    private SKColor? codeBackgroundColor;
    private SKColor? quoteForegroundColor;
    private SKColor? quoteBorderColor;
    private SKColor? horizontalRuleColor;
    private SKColor? imagePlaceholderColor;
    private SKColor? tableBorderColor;
    private SKColor? tableHeaderBackgroundColor;
    private SKColor? tableCellBackgroundColor;
    private SKColor? selectionBackgroundColor;
    private string codeFontFamily = "Consolas";
    private float heading1Scale = 2.0f;
    private float heading2Scale = 1.6f;
    private float heading3Scale = 1.35f;
    private float heading4Scale = 1.18f;
    private float heading5Scale = 1.08f;
    private float heading6Scale = 1.0f;
    private int paragraphSpacing = 8;
    private int headingTopSpacing = 14;
    private int headingBottomSpacing = 8;
    private int listIndent = 24;
    private int listItemSpacing = 4;
    private int codePadding = 8;
    private int quoteIndent = 14;
    private int quoteBorderWidth = 3;
    private int horizontalRuleSpacing = 12;
    private int horizontalRuleThickness = 1;
    private int imageSpacing = 8;
    private int tableCellPadding = 6;
    private int tableBorderThickness = 1;
    private bool codeBlockWrap;
    private bool showCodeBlockLanguage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStyle"/> class.
    /// </summary>
    public DocumentStyle()
    {
        codeStyle.Changed += (_, _) => OnChanged();
    }

    /// <summary>
    /// Occurs when a style value changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets or sets the foreground color for normal document text.
    /// </summary>
    public SKColor? ForegroundColor
    {
        get => foregroundColor;
        set => SetValue(ref foregroundColor, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for headings.
    /// </summary>
    public SKColor? HeadingColor
    {
        get => headingColor;
        set => SetValue(ref headingColor, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for normal links.
    /// </summary>
    public SKColor? LinkColor
    {
        get => linkColor;
        set => SetValue(ref linkColor, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for hovered links.
    /// </summary>
    public SKColor? HoveredLinkColor
    {
        get => hoveredLinkColor;
        set => SetValue(ref hoveredLinkColor, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for pressed links.
    /// </summary>
    /// <remarks>
    /// Pressed color has priority over hover color. A <see langword="null"/> value resolves to a
    /// theme-aware blend that remains visually distinct from the default hover color.
    /// </remarks>
    public SKColor? PressedLinkColor
    {
        get => pressedLinkColor;
        set => SetValue(ref pressedLinkColor, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for inline and block code.
    /// </summary>
    public SKColor? CodeForegroundColor
    {
        get => codeForegroundColor;
        set => SetValue(ref codeForegroundColor, value);
    }

    /// <summary>
    /// Gets or sets the background color for inline and block code.
    /// </summary>
    public SKColor? CodeBackgroundColor
    {
        get => codeBackgroundColor;
        set => SetValue(ref codeBackgroundColor, value);
    }

    /// <summary>
    /// Gets the semantic color style used by built-in fenced-code highlighters.
    /// </summary>
    /// <remarks>
    /// The returned instance remains stable for the lifetime of this style. Changing one of its
    /// properties raises <see cref="Changed"/> and invalidates attached document viewers.
    /// </remarks>
    public DocumentCodeStyle CodeStyle => codeStyle;

    /// <summary>
    /// Gets or sets the foreground color for quoted text.
    /// </summary>
    public SKColor? QuoteForegroundColor
    {
        get => quoteForegroundColor;
        set => SetValue(ref quoteForegroundColor, value);
    }

    /// <summary>
    /// Gets or sets the border color for quote blocks.
    /// </summary>
    public SKColor? QuoteBorderColor
    {
        get => quoteBorderColor;
        set => SetValue(ref quoteBorderColor, value);
    }

    /// <summary>
    /// Gets or sets the color used for horizontal rules.
    /// </summary>
    public SKColor? HorizontalRuleColor
    {
        get => horizontalRuleColor;
        set => SetValue(ref horizontalRuleColor, value);
    }

    /// <summary>
    /// Gets or sets the color used for image placeholders and failed image frames.
    /// </summary>
    /// <remarks>
    /// The placeholder is used while an image is loading or when it cannot be decoded. Text shown
    /// inside the placeholder still uses the normal document foreground color.
    /// </remarks>
    public SKColor? ImagePlaceholderColor
    {
        get => imagePlaceholderColor;
        set => SetValue(ref imagePlaceholderColor, value);
    }

    /// <summary>
    /// Gets or sets the border color used for native document tables.
    /// </summary>
    public SKColor? TableBorderColor
    {
        get => tableBorderColor;
        set => SetValue(ref tableBorderColor, value);
    }

    /// <summary>
    /// Gets or sets the background color used for table header cells.
    /// </summary>
    public SKColor? TableHeaderBackgroundColor
    {
        get => tableHeaderBackgroundColor;
        set => SetValue(ref tableHeaderBackgroundColor, value);
    }

    /// <summary>
    /// Gets or sets the background color used for normal table cells.
    /// </summary>
    public SKColor? TableCellBackgroundColor
    {
        get => tableCellBackgroundColor;
        set => SetValue(ref tableCellBackgroundColor, value);
    }

    /// <summary>
    /// Gets or sets the background color used to render selected document text.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value resolves to <see cref="Theme.TextSelectionBackgroundColor"/>.
    /// Changing this value invalidates the viewer so an existing selection is repainted.
    /// </remarks>
    public SKColor? SelectionBackgroundColor
    {
        get => selectionBackgroundColor;
        set => SetValue(ref selectionBackgroundColor, value);
    }

    /// <summary>
    /// Gets or sets the font family used for inline and block code.
    /// </summary>
    public string CodeFontFamily
    {
        get => codeFontFamily;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? "Consolas" : value;
            SetValue(ref codeFontFamily, value);
        }
    }

    /// <summary>
    /// Gets or sets the scale applied to level 1 headings relative to the viewer font size.
    /// </summary>
    public float Heading1Scale
    {
        get => heading1Scale;
        set => SetPositiveValue(ref heading1Scale, value, nameof(Heading1Scale));
    }

    /// <summary>
    /// Gets or sets the scale applied to level 2 headings relative to the viewer font size.
    /// </summary>
    public float Heading2Scale
    {
        get => heading2Scale;
        set => SetPositiveValue(ref heading2Scale, value, nameof(Heading2Scale));
    }

    /// <summary>
    /// Gets or sets the scale applied to level 3 headings relative to the viewer font size.
    /// </summary>
    public float Heading3Scale
    {
        get => heading3Scale;
        set => SetPositiveValue(ref heading3Scale, value, nameof(Heading3Scale));
    }

    /// <summary>
    /// Gets or sets the scale applied to level 4 headings relative to the viewer font size.
    /// </summary>
    public float Heading4Scale
    {
        get => heading4Scale;
        set => SetPositiveValue(ref heading4Scale, value, nameof(Heading4Scale));
    }

    /// <summary>
    /// Gets or sets the scale applied to level 5 headings relative to the viewer font size.
    /// </summary>
    public float Heading5Scale
    {
        get => heading5Scale;
        set => SetPositiveValue(ref heading5Scale, value, nameof(Heading5Scale));
    }

    /// <summary>
    /// Gets or sets the scale applied to level 6 headings relative to the viewer font size.
    /// </summary>
    public float Heading6Scale
    {
        get => heading6Scale;
        set => SetPositiveValue(ref heading6Scale, value, nameof(Heading6Scale));
    }

    /// <summary>
    /// Gets or sets the spacing after paragraphs in logical pixels.
    /// </summary>
    public int ParagraphSpacing
    {
        get => paragraphSpacing;
        set => SetNonNegativeValue(ref paragraphSpacing, value, nameof(ParagraphSpacing));
    }

    /// <summary>
    /// Gets or sets the spacing before headings in logical pixels.
    /// </summary>
    public int HeadingTopSpacing
    {
        get => headingTopSpacing;
        set => SetNonNegativeValue(ref headingTopSpacing, value, nameof(HeadingTopSpacing));
    }

    /// <summary>
    /// Gets or sets the spacing after headings in logical pixels.
    /// </summary>
    public int HeadingBottomSpacing
    {
        get => headingBottomSpacing;
        set => SetNonNegativeValue(ref headingBottomSpacing, value, nameof(HeadingBottomSpacing));
    }

    /// <summary>
    /// Gets or sets the indentation added for each list nesting level in logical pixels.
    /// </summary>
    public int ListIndent
    {
        get => listIndent;
        set => SetNonNegativeValue(ref listIndent, value, nameof(ListIndent));
    }

    /// <summary>
    /// Gets or sets the spacing between list items in logical pixels.
    /// </summary>
    public int ListItemSpacing
    {
        get => listItemSpacing;
        set => SetNonNegativeValue(ref listItemSpacing, value, nameof(ListItemSpacing));
    }

    /// <summary>
    /// Gets or sets the padding used inside code blocks in logical pixels.
    /// </summary>
    public int CodePadding
    {
        get => codePadding;
        set => SetNonNegativeValue(ref codePadding, value, nameof(CodePadding));
    }

    /// <summary>
    /// Gets or sets the indentation added to quote content in logical pixels.
    /// </summary>
    public int QuoteIndent
    {
        get => quoteIndent;
        set => SetNonNegativeValue(ref quoteIndent, value, nameof(QuoteIndent));
    }

    /// <summary>
    /// Gets or sets the quote border width in logical pixels.
    /// </summary>
    public int QuoteBorderWidth
    {
        get => quoteBorderWidth;
        set => SetNonNegativeValue(ref quoteBorderWidth, value, nameof(QuoteBorderWidth));
    }

    /// <summary>
    /// Gets or sets the vertical spacing around horizontal rules in logical pixels.
    /// </summary>
    public int HorizontalRuleSpacing
    {
        get => horizontalRuleSpacing;
        set => SetNonNegativeValue(ref horizontalRuleSpacing, value, nameof(HorizontalRuleSpacing));
    }

    /// <summary>
    /// Gets or sets the horizontal rule thickness in logical pixels.
    /// </summary>
    public int HorizontalRuleThickness
    {
        get => horizontalRuleThickness;
        set => SetNonNegativeValue(ref horizontalRuleThickness, value, nameof(HorizontalRuleThickness));
    }

    /// <summary>
    /// Gets or sets the spacing after image elements in logical pixels.
    /// </summary>
    public int ImageSpacing
    {
        get => imageSpacing;
        set => SetNonNegativeValue(ref imageSpacing, value, nameof(ImageSpacing));
    }

    /// <summary>
    /// Gets or sets the padding inside table cells in logical pixels.
    /// </summary>
    public int TableCellPadding
    {
        get => tableCellPadding;
        set => SetNonNegativeValue(ref tableCellPadding, value, nameof(TableCellPadding));
    }

    /// <summary>
    /// Gets or sets the table border thickness in logical pixels.
    /// </summary>
    public int TableBorderThickness
    {
        get => tableBorderThickness;
        set => SetNonNegativeValue(ref tableBorderThickness, value, nameof(TableBorderThickness));
    }

    /// <summary>
    /// Gets or sets a value indicating whether code blocks wrap long lines.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/> so code blocks preserve preformatted line semantics.
    /// When wrapping is disabled, long lines are clipped to the code block bounds by the renderer.
    /// </remarks>
    public bool CodeBlockWrap
    {
        get => codeBlockWrap;
        set => SetValue(ref codeBlockWrap, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether fenced code blocks display their language above the code.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. Indented code blocks and fences without a language
    /// identifier do not display a header.
    /// </remarks>
    public bool ShowCodeBlockLanguage
    {
        get => showCodeBlockLanguage;
        set => SetValue(ref showCodeBlockLanguage, value);
    }

    internal int Version { get; private set; }

    internal float GetHeadingScale(int level)
        => level switch
        {
            1 => Heading1Scale,
            2 => Heading2Scale,
            3 => Heading3Scale,
            4 => Heading4Scale,
            5 => Heading5Scale,
            6 => Heading6Scale,
            _ => 1f
        };

    internal SKColor ResolveCodeBackgroundColor(Control control)
        => CodeBackgroundColor ?? Theme.ControlMidColor;

    internal SKColor ResolveCodeForegroundColor(Control control)
        => control.Enabled ? CodeForegroundColor ?? control.CurrentStyle.GetForegroundColor() : Theme.ForegroundDisabledColor;

    internal SKColor ResolveForegroundColor(Control control)
        => control.Enabled ? ForegroundColor ?? control.CurrentStyle.GetForegroundColor() : Theme.ForegroundDisabledColor;

    internal SKColor ResolveHeadingColor(Control control)
        => HeadingColor ?? ResolveForegroundColor(control);

    internal SKColor ResolveHorizontalRuleColor(Control control)
        => HorizontalRuleColor ?? Theme.BorderLowColor;

    internal SKColor ResolveImagePlaceholderColor(Control control)
        => ImagePlaceholderColor ?? Theme.BorderMidColor;

    internal SKColor ResolveTableBorderColor(Control control)
        => TableBorderColor ?? Theme.BorderLowColor;

    internal SKColor ResolveTableCellBackgroundColor(Control control)
        => TableCellBackgroundColor ?? SKColors.Transparent;

    internal SKColor ResolveTableHeaderBackgroundColor(Control control)
        => TableHeaderBackgroundColor ?? Theme.ControlMidColor;

    internal SKColor ResolveLinkColor(Control control, bool hovered, bool pressed)
    {
        if (!control.Enabled)
            return Theme.ForegroundDisabledColor;

        if (pressed)
            return PressedLinkColor ?? Blend(Theme.AccentColor2, control.CurrentStyle.GetForegroundColor(), 0.35f);

        if (hovered)
            return HoveredLinkColor ?? Theme.AccentColor2;

        return LinkColor ?? Theme.AccentColor;
    }

    internal SKColor ResolveQuoteBorderColor(Control control)
        => QuoteBorderColor ?? Theme.BorderMidColor;

    internal SKColor ResolveQuoteForegroundColor(Control control)
        => QuoteForegroundColor ?? ResolveForegroundColor(control);

    internal SKColor ResolveSelectionBackgroundColor(Control control)
        => SelectionBackgroundColor ?? Theme.TextSelectionBackgroundColor;

    private void OnChanged()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static SKColor Blend(SKColor from, SKColor to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new SKColor(
            (byte)Math.Round(from.Red + ((to.Red - from.Red) * amount)),
            (byte)Math.Round(from.Green + ((to.Green - from.Green) * amount)),
            (byte)Math.Round(from.Blue + ((to.Blue - from.Blue) * amount)),
            (byte)Math.Round(from.Alpha + ((to.Alpha - from.Alpha) * amount)));
    }

    private void SetNonNegativeValue(ref int field, int value, string paramName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, "The value cannot be negative.");

        SetValue(ref field, value);
    }

    private void SetPositiveValue(ref float field, float value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, "The value must be greater than zero.");

        SetValue(ref field, value);
    }

    private void SetValue<T>(ref T field, T value)
    {
        if (Equals(field, value))
            return;

        field = value;
        OnChanged();
    }
}
