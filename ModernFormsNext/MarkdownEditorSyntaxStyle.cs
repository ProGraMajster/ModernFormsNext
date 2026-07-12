using System;
using SkiaSharp;

namespace ModernFormsNext;

/// <summary>
/// Defines theme-aware colors used to highlight Markdown source syntax.
/// </summary>
/// <remarks>
/// Color values are nullable. A <see langword="null"/> value uses the current
/// <see cref="Theme"/> and editor <see cref="Control.CurrentStyle"/>. Changing a value
/// refreshes source presentation without changing the Markdown text or selection.
/// </remarks>
public sealed class MarkdownEditorSyntaxStyle
{
    private SKColor? editorBackgroundColor;
    private SKColor? editorForegroundColor;
    private SKColor? caretColor;
    private SKColor? selectionBackgroundColor;
    private SKColor? headingMarkerColor;
    private SKColor? emphasisMarkerColor;
    private SKColor? codeMarkerColor;
    private SKColor? quoteMarkerColor;
    private SKColor? listMarkerColor;
    private SKColor? linkTextColor;
    private SKColor? linkTargetColor;
    private SKColor? imageMarkerColor;

    /// <summary>
    /// Occurs when a syntax style value changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets or sets the source editor background color.
    /// </summary>
    public SKColor? EditorBackgroundColor
    {
        get => editorBackgroundColor;
        set => SetValue(ref editorBackgroundColor, value);
    }

    /// <summary>
    /// Gets or sets the normal source text color.
    /// </summary>
    public SKColor? EditorForegroundColor
    {
        get => editorForegroundColor;
        set => SetValue(ref editorForegroundColor, value);
    }

    /// <summary>
    /// Gets or sets the caret color.
    /// </summary>
    public SKColor? CaretColor
    {
        get => caretColor;
        set => SetValue(ref caretColor, value);
    }

    /// <summary>
    /// Gets or sets the selection background color.
    /// </summary>
    public SKColor? SelectionBackgroundColor
    {
        get => selectionBackgroundColor;
        set => SetValue(ref selectionBackgroundColor, value);
    }

    /// <summary>
    /// Gets or sets the color of heading markers.
    /// </summary>
    public SKColor? HeadingMarkerColor
    {
        get => headingMarkerColor;
        set => SetValue(ref headingMarkerColor, value);
    }

    /// <summary>
    /// Gets or sets the color of emphasis and strikethrough markers.
    /// </summary>
    public SKColor? EmphasisMarkerColor
    {
        get => emphasisMarkerColor;
        set => SetValue(ref emphasisMarkerColor, value);
    }

    /// <summary>
    /// Gets or sets the color of inline-code and fenced-code markers.
    /// </summary>
    public SKColor? CodeMarkerColor
    {
        get => codeMarkerColor;
        set => SetValue(ref codeMarkerColor, value);
    }

    /// <summary>
    /// Gets or sets the color of block-quote markers.
    /// </summary>
    public SKColor? QuoteMarkerColor
    {
        get => quoteMarkerColor;
        set => SetValue(ref quoteMarkerColor, value);
    }

    /// <summary>
    /// Gets or sets the color of ordered, unordered, and task-list markers.
    /// </summary>
    public SKColor? ListMarkerColor
    {
        get => listMarkerColor;
        set => SetValue(ref listMarkerColor, value);
    }

    /// <summary>
    /// Gets or sets the color of visible link labels.
    /// </summary>
    public SKColor? LinkTextColor
    {
        get => linkTextColor;
        set => SetValue(ref linkTextColor, value);
    }

    /// <summary>
    /// Gets or sets the color of link destinations.
    /// </summary>
    public SKColor? LinkTargetColor
    {
        get => linkTargetColor;
        set => SetValue(ref linkTargetColor, value);
    }

    /// <summary>
    /// Gets or sets the color of image source markers.
    /// </summary>
    public SKColor? ImageMarkerColor
    {
        get => imageMarkerColor;
        set => SetValue(ref imageMarkerColor, value);
    }

    internal SKColor ResolveBackground(Control control)
        => EditorBackgroundColor ?? control.CurrentStyle.GetBackgroundColor();

    internal SKColor ResolveCaret(Control control)
        => CaretColor ?? EditorForegroundColor ?? control.CurrentStyle.GetForegroundColor();

    internal SKColor ResolveForeground(Control control)
        => control.Enabled
            ? EditorForegroundColor ?? control.CurrentStyle.GetForegroundColor()
            : Theme.ForegroundDisabledColor;

    internal SKColor ResolveSelectionBackground()
        => SelectionBackgroundColor ?? Theme.TextSelectionBackgroundColor;

    internal SKColor Resolve(MarkdownSourceSpanKind kind, Control control)
        => kind switch
        {
            MarkdownSourceSpanKind.HeadingMarker => HeadingMarkerColor ?? Theme.AccentColor,
            MarkdownSourceSpanKind.EmphasisMarker => EmphasisMarkerColor ?? Theme.AccentColor2,
            MarkdownSourceSpanKind.CodeMarker => CodeMarkerColor ?? Theme.ForegroundDisabledColor,
            MarkdownSourceSpanKind.QuoteMarker => QuoteMarkerColor ?? Theme.BorderHighColor,
            MarkdownSourceSpanKind.ListMarker => ListMarkerColor ?? Theme.AccentColor2,
            MarkdownSourceSpanKind.LinkText => LinkTextColor ?? Theme.AccentColor,
            MarkdownSourceSpanKind.LinkTarget => LinkTargetColor ?? Theme.ForegroundDisabledColor,
            MarkdownSourceSpanKind.ImageMarker => ImageMarkerColor ?? Theme.AccentColor2,
            _ => ResolveForeground(control)
        };

    private void SetValue(ref SKColor? field, SKColor? value)
    {
        if (Nullable.Equals(field, value))
            return;

        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
