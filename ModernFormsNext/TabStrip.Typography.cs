using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext;

public partial class TabStrip
{
    private bool authoredHeight;
    private bool changingTypographyHeight;
    private bool refreshingTypography;
    private (SKTypeface Font, int Size, FontStyle Style, double LineHeight, double Spacing)? lastTypography;

    // Only the implicit TabControl strip borrows its owner's typography. Colors, borders and
    // interaction state remain the strip's own; standalone and Ribbon strips retain their style.
    internal TabControl? TypographyOwner { get; set; }
    internal ControlStyle HeaderStyle => TypographyOwner?.CurrentStyle ?? CurrentStyle;

    internal FontStyle GetHeaderFontStyle(bool selected)
        => HeaderStyle.GetFontStyle() | (selected ? FontStyle.Bold : FontStyle.Regular);

    internal SKSize MeasureHeaderText(string text, bool selected)
    {
        var style = HeaderStyle;
        var block = TextMeasurer.CreateTextBlock(text, style.GetFont(), LogicalToDeviceUnits(style.GetFontSize()),
            new Size(int.MaxValue, int.MaxValue), maxLines: 1,
            fontStyle: GetHeaderFontStyle(selected), lineHeight: style.GetLineHeight(),
            letterSpacing: style.GetLetterSpacing() * Scaling);
        return new SKSize(block.MeasuredWidth, block.MeasuredHeight);
    }

    internal void RefreshHeaderTypography()
    {
        if (refreshingTypography || IsDisposed || Disposing) return;
        refreshingTypography = true;
        try { PerformLayout(); Invalidate(); }
        finally { refreshingTypography = false; }
    }

    /// <inheritdoc/>
    protected override void OnFontChanged(EventArgs e)
    {
        RefreshHeaderTypography();
        base.OnFontChanged(e);
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        // Keep a color-only theme switch on the existing repaint path.
        if (lastTypography != ReadTypography()) RefreshHeaderTypography();
    }

    internal override void OnPresentationContentMetricsChanged()
    {
        base.OnPresentationContentMetricsChanged();
        RefreshHeaderTypography();
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (Tabs is not null && !changingTypographyHeight) {
            lastTypography = ReadTypography();
            bool stretchesVertically = Dock is DockStyle.Fill or DockStyle.Left or DockStyle.Right ||
                (Dock == DockStyle.None && (Anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == (AnchorStyles.Top | AnchorStyles.Bottom));
            // A vertically stretched strip belongs to its parent's geometry contract. Natural
            // text size is a preference, not permission to undo dock/anchor layout repeatedly.
            if (!stretchesVertically && (!authoredHeight || AutoSize)) {
                int height = ApplySizeConstraints(Width, GetNaturalHeaderSize().Height).Height;
                if (Height != height) {
                    // Height changes reenter parent docking and this layout. The scoped guard
                    // preserves an authored height and prevents recursive metric measurement.
                    changingTypographyHeight = true;
                    try { SetBounds(Left, Top, Width, height, BoundsSpecified.Height); }
                    finally { changingTypographyHeight = false; }
                }
            }
            LayoutTabs();
        }
        base.OnLayout(e);
    }

    /// <inheritdoc/>
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        // Switching Dock republishes the unchanged specified rectangle with BoundsSpecified.All.
        // As with the public Height setter's no-op, that is not a newly authored height.
        if (!changingTypographyHeight && height != Height && (specified & BoundsSpecified.Height) != 0) authoredHeight = true;
        base.SetBoundsCore(x, y, width, height, specified);
    }

    internal override Size GetPreferredSizeCore(Size proposedSize) => GetNaturalHeaderSize();

    private Size GetNaturalHeaderSize()
    {
        var metrics = MeasureHeaderText("Mg", selected: false);
        int deviceHeight = Math.Max(LogicalToDeviceUnits(31), (int)Math.Ceiling(metrics.Height) + LogicalToDeviceUnits(8));
        long deviceWidth = 0;
        if (Tabs is not null) foreach (var item in Tabs) {
            var size = item.GetPreferredSize(Size.Empty);
            deviceWidth += size.Width + item.Margin.Horizontal;
            deviceHeight = Math.Max(deviceHeight, size.Height + item.Margin.Vertical);
        }
        var border = CurrentStyle.Border;
        return new Size((int)Math.Min(int.MaxValue, Math.Ceiling(deviceWidth / Scaling) + border.Left.GetWidth() + border.Right.GetWidth()),
            (int)Math.Ceiling(deviceHeight / Scaling) + border.Top.GetWidth() + border.Bottom.GetWidth());
    }

    private (SKTypeface, int, FontStyle, double, double) ReadTypography()
    {
        var style = HeaderStyle;
        return (style.GetFont(), style.GetFontSize(), style.GetFontStyle(), style.GetLineHeight(), style.GetLetterSpacing());
    }
}
