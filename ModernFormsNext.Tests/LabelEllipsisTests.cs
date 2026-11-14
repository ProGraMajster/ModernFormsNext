using System.Drawing;
using System.Reflection;
using ModernFormsNext.Layout;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class LabelEllipsisTests
{
    public static IEnumerable<object[]> Scales()
    {
        foreach (double dpi in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d })
            foreach (double textScale in new[] { 1d, 1.5d, 2d })
                yield return [dpi, textScale];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultMeasurementHasNoArbitraryThousandPixelWidthOrHeight(bool multiline)
    {
        using var font = SKTypeface.FromFamilyName("Segoe UI");
        string text = multiline ? string.Join('\n', Enumerable.Repeat("Line", 100))
            : string.Join(' ', Enumerable.Repeat("A wide natural line", 30));
        using var label = new Label { Text = text, Font = new Font("Segoe UI", 20) };
        var reference = new TextBlock();
        reference.AddText(text, new Style { FontFamily = font.FamilyName, FontSize = 20 });
        var fontResult = TextMeasurer.MeasureText(text, font, 20);
        var controlResult = TextMeasurer.MeasureText(text, label);

        Assert.True(multiline ? reference.MeasuredHeight > 1000 : reference.MeasuredWidth > 1000);
        Assert.Equal(reference.MeasuredWidth, fontResult.Width);
        Assert.Equal(reference.MeasuredHeight, fontResult.Height);
        Assert.Equal(reference.MeasuredWidth, controlResult.Width);
        Assert.Equal(reference.MeasuredHeight, controlResult.Height);
    }

    [Theory]
    [InlineData(TextAlignment.Left)]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.Right)]
    public void UnlimitedSingleLineDoesNotAlignAgainstAnIntMaxValueWidth(TextAlignment alignment)
    {
        using var font = SKTypeface.FromFamilyName("Segoe UI");
        var block = TextMeasurer.CreateTextBlock("A natural line", font, 27,
            new Size(int.MaxValue, int.MaxValue), alignment, maxLines: 1, ellipsis: true);
        Assert.Null(block.MaxWidth);
        Assert.Null(block.MaxHeight);
        var caret = block.GetCaretInfo(new CaretPosition(0)).CaretRectangle;
        Assert.InRange(caret.Left, -1, block.MeasuredWidth + 1);
    }

    [Fact]
    public void ExplicitFiniteWidthStillWrapsAndSingleLineHeightRemainsBounded()
    {
        const string text = "Text with spaces that must wrap within the explicit width. ";
        using var font = SKTypeface.FromFamilyName("Segoe UI");
        using var label = new Label { Font = new Font("Segoe UI", 20) };
        var bounds = new Size(180, int.MaxValue);
        var reference = new TextBlock { MaxWidth = bounds.Width };
        reference.AddText(string.Concat(Enumerable.Repeat(text, 10)), new Style { FontFamily = font.FamilyName, FontSize = 20 });
        var actual = TextMeasurer.MeasureText(string.Concat(Enumerable.Repeat(text, 10)), font, 20, bounds);
        Assert.InRange(actual.Width, 1, bounds.Width);
        Assert.True(actual.Height > 100);
        Assert.Equal(reference.MeasuredWidth, actual.Width);
        Assert.Equal(reference.MeasuredHeight, actual.Height);
        Assert.Equal(actual, TextMeasurer.MeasureText(string.Concat(Enumerable.Repeat(text, 10)), label, bounds));
        var singleLine = TextMeasurer.CreateTextBlock(text, font, 20, new Size(180, 65), maxLines: 1, ellipsis: true);
        Assert.Equal(180f, singleLine.MaxWidth);
        Assert.Equal(65f, singleLine.MaxHeight);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void FittingCaptionsKeepTheirNaturalWidthAndDoNotEllipsize(double dpi, double textScale)
    {
        using var window = CreateWindow(dpi);
        using var label = new Label { Font = new Font("Segoe UI", (float)(27 * textScale), FontStyle.Bold),
            AutoEllipsis = true, ForeColor = SKColors.Black, LetterSpacing = .25 };
        window.Controls.Add(label);
        foreach (string text in new[] { "Integration Sample", "שלום עולם", string.Join(' ', Enumerable.Repeat("Integration Sample framework label", 4)) })
        {
            label.Text = text;
            var reference = Natural(label);
            label.Size = new Size((int)Math.Ceiling(reference.MeasuredWidth / dpi) + 40,
                (int)Math.Ceiling(reference.MeasuredHeight / dpi) + 16);
            foreach (var alignment in new[] { ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter, ContentAlignment.MiddleRight })
            {
                label.TextAlign = alignment;
                var layout = TextImageLayoutEngine.Layout(label);
                int expectedWidth = (int)Math.Ceiling(reference.MeasuredWidth);
                Assert.Equal(expectedWidth, layout.TextBounds.Width);
                int expectedLeft = alignment switch {
                    ContentAlignment.MiddleLeft => layout.Field.Left,
                    ContentAlignment.MiddleCenter => layout.Field.Left + (layout.Field.Width - expectedWidth) / 2,
                    _ => layout.Field.Right - expectedWidth
                };
                Assert.Equal(expectedLeft, layout.TextBounds.Left);
                // Compare the actual renderer's text path with an unellipsized natural-width line.
                // A capped reference through the same layout would hide the original defect.
                using var actual = Paint(label, layout.TextBounds, ellipsis: true);
                using var expected = Paint(label, layout.TextBounds, ellipsis: false);
                Assert.Contains(actual.Bytes, value => value != 0);
                Assert.Equal(expected.Bytes, actual.Bytes);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ActualOverflowUsesTheAvailableFieldAndProducesEllipsis(double dpi, double textScale)
    {
        using var window = CreateWindow(dpi);
        using var label = new Label { Text = "Integration Sample cannot fit in this narrow field",
            Font = new Font("Segoe UI", (float)(27 * textScale), FontStyle.Bold), Size = new Size(180, 100),
            Padding = new Padding(7, 3, 11, 5), AutoEllipsis = true, ForeColor = SKColors.Black };
        window.Controls.Add(label);
        var layout = TextImageLayoutEngine.Layout(label);
        Assert.True(Natural(label).MeasuredWidth > layout.Field.Width);
        Assert.Equal(layout.Field.Width, layout.TextBounds.Width);
        using var ellipsized = Paint(label, layout.TextBounds, ellipsis: true);
        using var clipped = Paint(label, layout.TextBounds, ellipsis: false);
        Assert.Contains(ellipsized.Bytes, value => value != 0);
        Assert.False(ellipsized.Bytes.SequenceEqual(clipped.Bytes));
    }

    [Theory]
    [InlineData(ContentAlignment.MiddleLeft)]
    [InlineData(ContentAlignment.MiddleCenter)]
    [InlineData(ContentAlignment.MiddleRight)]
    public void OriginalDashboardRecipeKeepsItsFractionalGlyphAdvance(ContentAlignment alignment)
    {
        using var label = new Label { Text = "Integration Sample", Size = new Size(1506, 48),
            Font = new Font("Segoe UI", 27, FontStyle.Bold), TextAlign = alignment, AutoEllipsis = true };
        float width = Natural(label).MeasuredWidth;
        Assert.NotEqual(MathF.Floor(width), width);
        var layout = TextImageLayoutEngine.Layout(label);
        Assert.Equal((int)Math.Ceiling(width), layout.TextBounds.Width);
        using var actual = Paint(label, layout.TextBounds, true);
        using var expected = Paint(label, layout.TextBounds, false);
        Assert.Contains(actual.Bytes, value => value != 0);
        Assert.Equal(expected.Bytes, actual.Bytes);
    }

    [Theory]
    [InlineData(TextImageRelation.ImageBeforeText)]
    [InlineData(TextImageRelation.TextBeforeImage)]
    public void ImageAndPaddingReserveSpaceBeforeEllipsis(TextImageRelation relation)
    {
        using var image = new SKBitmap(32, 32);
        using var label = new Label { Text = "A long caption that cannot fit next to this image",
            Font = new Font("Segoe UI", 27, FontStyle.Bold), Size = new Size(240, 64),
            Image = image, TextImageRelation = relation, TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = relation == TextImageRelation.ImageBeforeText ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
            Padding = new Padding(9), AutoEllipsis = true };
        var layout = TextImageLayoutEngine.Layout(label);
        Assert.True(layout.TextBounds.Width < layout.Field.Width);
        Assert.False(layout.TextBounds.IntersectsWith(layout.ImageBounds));
        Assert.True(layout.Field.Contains(layout.TextBounds));
        using var ellipsized = Paint(label, layout.TextBounds, true);
        using var clipped = Paint(label, layout.TextBounds, false);
        Assert.Contains(ellipsized.Bytes, value => value != 0);
        Assert.False(ellipsized.Bytes.SequenceEqual(clipped.Bytes));
    }

    [Fact]
    public void MultilineRenderingStillUsesTheAvailableField()
    {
        using var label = new Label { Text = string.Join(' ', Enumerable.Repeat("Wrapped label", 40)),
            Font = new Font("Segoe UI", 27, FontStyle.Bold), Size = new Size(180, 85),
            Multiline = true, AutoEllipsis = true, Padding = new Padding(5) };
        var layout = TextImageLayoutEngine.Layout(label);
        Assert.Equal(layout.Field, layout.TextBounds);
        using var ellipsized = Paint(label, layout.TextBounds, true);
        using var clipped = Paint(label, layout.TextBounds, false);
        Assert.Contains(ellipsized.Bytes, value => value != 0);
        Assert.False(ellipsized.Bytes.SequenceEqual(clipped.Bytes));
        Assert.Equal(0, ellipsized.GetPixel(0, 0).Alpha);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(1.75d)]
    [InlineData(2d)]
    public void SharedImageTextLayoutKeepsEllipsisRegionsInsideTheField(double dpi)
    {
        using var window = CreateWindow(dpi);
        using var image = new SKBitmap(32, 32);
        foreach (var relation in new[] { TextImageRelation.ImageBeforeText, TextImageRelation.TextBeforeImage,
            TextImageRelation.ImageAboveText, TextImageRelation.TextAboveImage })
        foreach (var alignment in new[] { ContentAlignment.TopLeft, ContentAlignment.MiddleCenter, ContentAlignment.BottomRight })
        foreach (int size in new[] { 40, 80, 1000 })
        {
            using var label = new Label { Text = "Long image caption for ellipsis", Image = image,
                Font = new Font("Segoe UI", 27, FontStyle.Bold), Size = new Size(size, size), Padding = new Padding(3),
                AutoEllipsis = true, TextImageRelation = relation, ImageAlign = alignment, TextAlign = alignment };
            using var button = new Button { Text = label.Text, Image = image, Font = label.Font,
                Size = label.Size, Padding = label.Padding, AutoEllipsis = true,
                TextImageRelation = relation, ImageAlign = alignment, TextAlign = alignment };
            foreach (Control control in new Control[] { label, button })
            {
                window.Controls.Add(control);
                var layout = TextImageLayoutEngine.Layout(control);
                Assert.True(layout.TextBounds.Width >= 0 && layout.TextBounds.Height >= 0);
                Assert.True(layout.Field.Contains(layout.TextBounds), $"{control.GetType().Name} {relation} {alignment} {size}: {layout.TextBounds} / {layout.Field}");
                Assert.True(layout.Field.Contains(layout.ImageBounds));
                if (layout.TextBounds.Width > 0 && layout.TextBounds.Height > 0)
                    Assert.False(layout.TextBounds.IntersectsWith(layout.ImageBounds));
                window.Controls.Remove(control);
            }
        }
    }

    private static TextBlock Natural(Label label)
    {
        var style = label.CurrentStyle;
        var block = new TextBlock();
        block.AddText(label.Text, new Style { FontFamily = style.GetFont().FamilyName,
            FontSize = label.LogicalToDeviceUnits(style.GetFontSize()), FontWeight = (int)SKFontStyleWeight.Bold,
            LineHeight = (float)style.GetLineHeight(), LetterSpacing = (float)(style.GetLetterSpacing() * label.Scaling) });
        return block;
    }

    private static SKBitmap Paint(Label label, Rectangle bounds, bool ellipsis)
    {
        var bitmap = new SKBitmap(label.ScaledWidth, label.ScaledHeight);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        canvas.DrawText(label.Text, bounds, label, label.TextAlign, maxLines: label.Multiline ? null : 1, ellipsis: ellipsis);
        return bitmap;
    }

    private static DpiWindow CreateWindow(double scale)
    {
        var implementation = DispatchProxy.Create<IWindowBaseImpl, DpiProxy>();
        ((DpiProxy)(object)implementation).Scale = scale;
        return new DpiWindow(implementation);
    }

    private sealed class DpiWindow(IWindowBaseImpl implementation) : WindowBase(implementation)
    {
        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) adapter.Dispose(); }
        }
    }

    public class DpiProxy : DispatchProxy
    {
        public double Scale { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name is "get_RenderScaling" or "get_DesktopScaling") return Scale;
            Type result = method?.ReturnType ?? typeof(void);
            return result == typeof(void) || !result.IsValueType ? null : Activator.CreateInstance(result);
        }
    }
}
