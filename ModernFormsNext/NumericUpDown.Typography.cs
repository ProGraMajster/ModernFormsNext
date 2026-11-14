using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext;

public partial class NumericUpDown
{
    private bool authoredTypographyHeight;
    private bool changingTypographyHeight;
    private bool refreshingEditorTypography;
    private bool pendingEditorTypography;
    private (SKTypeface Typeface, Font? Descriptor, int Size, double LineHeight, double Spacing)? editorTypography;

    /// <inheritdoc/>
    protected override void OnFontChanged(EventArgs e)
    {
        RefreshEditorTypography();
        PerformLayout();
        base.OnFontChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        // This also supports the usual PerformLayout after a passive/inherited style edit,
        // and refreshes integer child bounds after fractional DPI changes without a resize.
        RefreshEditorTypography();
        base.OnLayout(e);
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        if (editorTypography != ReadEditorTypography()) RefreshEditorTypography();
    }

    /// <inheritdoc/>
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        // Dock republishes unchanged authored bounds. Only an actual explicit height change
        // pins the height; parent layout uses BoundsSpecified.None and retains its ownership.
        if (!changingTypographyHeight && height != Height && (specified & BoundsSpecified.Height) != 0)
            authoredTypographyHeight = true;
        base.SetBoundsCore(x, y, width, height, specified);
    }

    internal override Size GetPreferredSizeCore(Size proposedSize) => GetNaturalNumericSize();

    private (SKTypeface, Font?, int, double, double) ReadEditorTypography()
    {
        var style = CurrentStyle;
        return (style.GetFont(), style.GetResolvedTextFont(), style.GetFontSize(), style.GetLineHeight(), style.GetLetterSpacing());
    }

    private void RefreshEditorTypography()
    {
        if (editor is null || editor.IsDisposed || IsDisposed || Disposing) return;
        if (refreshingEditorTypography) { pendingEditorTypography = true; return; }
        refreshingEditorTypography = true;
        try {
            do {
                pendingEditorTypography = false;
                var typography = ReadEditorTypography();
                bool changed = editorTypography != typography;
                if (changed) {
                    editorTypography = typography;
                    // Copy only typography into owned editor states. Keep TextBox colors,
                    // borders, selection, document, composition and accessibility identity.
                    CopyTypography(editor.Style);
                    CopyTypography(editor.StyleHover);
                    CopyTypography(editor.StyleFocused);
                    CopyTypography(editor.StylePressed);
                    CopyTypography(editor.StyleDisabled);
                }
                bool stretched = Dock is DockStyle.Fill or DockStyle.Left or DockStyle.Right ||
                    (Dock == DockStyle.None && (Anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == (AnchorStyles.Top | AnchorStyles.Bottom));
                if (!stretched && (!authoredTypographyHeight || AutoSize)) {
                    int height = ApplySizeConstraints(Width, GetNaturalNumericSize().Height).Height;
                    if (Height != height) {
                        changingTypographyHeight = true;
                        try { SetBounds(Left, Top, Width, height, BoundsSpecified.Height); }
                        finally { changingTypographyHeight = false; }
                    }
                }
                if (IsDisposed || Disposing || editor.IsDisposed) return;
                UpdateEditorBounds();
                if (changed) editor.NotifyOwnerTypographyChanged();
                // Font/accessibility observers can replace the owner's typography. Reconcile
                // that newer state after the callback instead of dropping a nested refresh.
            } while (pendingEditorTypography && !IsDisposed && !Disposing && !editor.IsDisposed);
        }
        finally { refreshingEditorTypography = false; }
    }

    private void CopyTypography(ControlStyle destination)
    {
        var value = editorTypography!.Value;
        if (value.Descriptor is { } descriptor) destination.TextFont = descriptor;
        else destination.Font = value.Typeface;
        destination.FontSize = value.Size;
        destination.LineHeight = value.LineHeight;
        destination.LetterSpacing = value.Spacing;
    }

    private Size GetNaturalNumericSize()
    {
        if (editor is null) return DefaultSize;
        var style = CurrentStyle;
        SKSize Measure(ControlStyle typography)
        {
            var block = TextMeasurer.CreateTextBlock(editor.Text.Length == 0 ? "0" : editor.Text,
                typography.GetFont(), LogicalToDeviceUnits(typography.GetFontSize()), new Size(int.MaxValue, int.MaxValue),
                maxLines: 1, fontStyle: typography.GetFontStyle(), lineHeight: typography.GetLineHeight(),
                letterSpacing: typography.GetLetterSpacing() * Scaling);
            return new SKSize(block.MeasuredWidth, block.MeasuredHeight);
        }
        var text = Measure(style);
        if (!ReferenceEquals(style, Style) && (style.GetFont() != Style.GetFont() ||
            style.GetFontSize() != Style.GetFontSize() || style.GetFontStyle() != Style.GetFontStyle() ||
            style.GetLineHeight() != Style.GetLineHeight() || style.GetLetterSpacing() != Style.GetLetterSpacing())) {
            // A hover state may intentionally have smaller text than the persistent style.
            // Keep the normal font's reserved space so entering the spinner cannot collapse
            // its button under the pointer. Larger active typography may still grow the control.
            var normal = Measure(Style);
            text = new SKSize(Math.Max(text.Width, normal.Width), Math.Max(text.Height, normal.Height));
        }
        var border = editor.CurrentStyle.Border;
        var padding = editor.PresentationPadding;
        int extraHeight = LogicalToDeviceUnits(border.Top.GetWidth()) + LogicalToDeviceUnits(border.Bottom.GetWidth()) +
            LogicalToDeviceUnits(padding.Top) + LogicalToDeviceUnits(padding.Bottom) + LogicalToDeviceUnits(4);
        int extraWidth = LogicalToDeviceUnits(border.Left.GetWidth()) + LogicalToDeviceUnits(border.Right.GetWidth()) +
            LogicalToDeviceUnits(padding.Left) + LogicalToDeviceUnits(padding.Right);
        // The owner's device client edges are rounded inward onto the child's logical grid.
        // Include that same loss here, separately for each edge, to avoid fractional-DPI clips.
        int Edge(int width) => (int)Math.Ceiling(LogicalToDeviceUnits(width) / Scaling);
        int height = (int)Math.Ceiling((text.Height + extraHeight) / Scaling) +
            Edge(style.Border.Top.GetWidth()) + Edge(style.Border.Bottom.GetWidth());
        int width = (int)Math.Ceiling((text.Width + extraWidth) / Scaling) + 18 +
            Edge(style.Border.Left.GetWidth()) + Edge(style.Border.Right.GetWidth());
        return new Size(Math.Max(DefaultSize.Width, width), Math.Max(DefaultSize.Height, height));
    }
}
