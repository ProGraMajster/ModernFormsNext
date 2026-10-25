using System.ComponentModel;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private (double LineHeight, double LetterSpacing) lastThemeTextMetrics = (Theme.LineHeight, Theme.LetterSpacing);

    private void RefreshTypographyAfterThemeChange()
    {
        var current = (Theme.LineHeight, Theme.LetterSpacing);
        if (lastThemeTextMetrics == current) return;
        lastThemeTextMetrics = current;
        // Color-only theme changes retain the existing repaint-only path. A global typography
        // metric change additionally retires measurement caches and remeasures auto-sized text.
        OnPresentationContentMetricsChanged();
        if (IsDisposed || Disposing) return;
        LayoutTransaction.DoLayoutIf(AutoSize, Parent, this, PropertyNames.PreferredSize);
    }
    /// <summary>Gets or sets a local line-height multiplier for shared text measurement and painting.</summary>
    /// <remarks>
    /// Null uses the current style and theme; 1 uses the font's natural line height. Positive
    /// values through 16 must remain positive after conversion to Single. A change
    /// invalidates text layout, repaints and requests parent measurement when AutoSize is enabled.
    /// Set on the UI thread. RichTextBox, MarkdownEditor and custom text renderers retain their
    /// document-specific styling. This does not raise FontChanged because the font is unchanged.
    /// </remarks>
    /// <example><code>label.LineHeight = 1.4f;</code></example>
    [DefaultValue(null), Category("Appearance")]
    public double? LineHeight
    {
        get => Style.LineHeight;
        set
        {
            if (Style.LineHeight == value) return;
            Style.LineHeight = value;
            NotifyTypographyMetricsChanged();
        }
    }

    /// <summary>Gets or sets local additional character spacing in logical pixels.</summary>
    /// <remarks>
    /// Null uses the current style and theme. Zero preserves natural glyph advances; finite
    /// values from -1024 through 1024 are allowed. The shared shaping engine preserves ligatures, combining
    /// characters and bidirectional text. A change has the same layout and thread requirements
    /// as <see cref="LineHeight"/>. Character spacing is scaled once for the control's DPI.
    /// </remarks>
    /// <example><code>button.LetterSpacing = 0.5f;</code></example>
    [DefaultValue(null), Category("Appearance")]
    public double? LetterSpacing
    {
        get => Style.LetterSpacing;
        set
        {
            if (Style.LetterSpacing == value) return;
            Style.LetterSpacing = value;
            NotifyTypographyMetricsChanged();
        }
    }

    private void NotifyTypographyMetricsChanged()
    {
        OnPresentationContentMetricsChanged();
        if (IsDisposed || Disposing) return;
        LayoutTransaction.DoLayoutIf(AutoSize, Parent, this, PropertyNames.PreferredSize);
        Invalidate();
    }
}
