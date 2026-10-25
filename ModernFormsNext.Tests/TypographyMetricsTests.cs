using System.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TypographyMetricsTests
{
    [Fact]
    public void SharedTypographyAdmissionBoundsApplyToTokensAndControls()
    {
        using var control = new Label();
        Assert.Throws<ArgumentOutOfRangeException>(() => control.LineHeight = 17);
        Assert.Throws<ArgumentOutOfRangeException>(() => control.LineHeight = double.Epsilon);
        Assert.Throws<ArgumentOutOfRangeException>(() => control.LetterSpacing = 1025);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThemeTypography("sans-serif", 14, lineHeight: 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThemeTypography("sans-serif", 14, letterSpacing: -1025));
        control.LineHeight = ThemeTypography.MaximumLineHeight;
        control.LetterSpacing = ThemeTypography.MaximumLetterSpacing;
        Assert.Equal(16d, control.LineHeight);
        Assert.Equal(1024d, control.LetterSpacing);
    }

    [Fact]
    public void MetricChangesRetireEditorGeometryWithoutRaisingFontChanged()
    {
        using var editor = new LayoutProbeEditor { Text = "Spaced caret", Width = 300 };
        int fontChanges = 0;
        editor.FontChanged += (_, _) => fontChanges++;
        var before = editor.AccessibleTextLayout;
        float caretBefore = before.GetCaretInfo(new Topten.RichTextKit.CaretPosition(6)).CaretRectangle.Left;
        editor.GeometryInvalidations = 0;
        editor.LetterSpacing = 2;
        var after = editor.AccessibleTextLayout;
        Assert.True(editor.GeometryInvalidations > 0);
        Assert.True(after.GetCaretInfo(new Topten.RichTextKit.CaretPosition(6)).CaretRectangle.Left > caretBefore);
        Assert.Equal(0, fontChanges);
    }

    [Fact]
    public void EditorCanDisposeDuringTypographyGeometryInvalidation()
    {
        using var editor = new LayoutProbeEditor { Text = "Retired editor", Width = 200 };
        editor.DisposeOnInvalidation = true;
        editor.LetterSpacing = 1;
        Assert.True(editor.IsDisposed);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void InvalidLineHeightDoesNotReplaceExistingValue(float value)
    {
        using var control = new Label { LineHeight = 1.4f };
        Assert.Throws<ArgumentOutOfRangeException>(() => control.LineHeight = value);
        Assert.Equal(1.4f, control.LineHeight);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidLetterSpacingDoesNotReplaceExistingValue(float value)
    {
        using var control = new Label { LetterSpacing = 0.5f };
        Assert.Throws<ArgumentOutOfRangeException>(() => control.LetterSpacing = value);
        Assert.Equal(0.5f, control.LetterSpacing);
    }

    [Fact]
    public void CyclicStyleInheritanceResolvesMetricsWithoutRecursion()
    {
        var a = new ControlStyle(null);
        var b = new ControlStyle(a) { LineHeight = 1.7f, LetterSpacing = -0.25f };
        a.ParentStyle = b;
        Assert.Equal(1.7f, a.GetLineHeight());
        Assert.Equal(-0.25f, a.GetLetterSpacing());
        b.LineHeight = null;
        b.LetterSpacing = null;
        Assert.Equal(Theme.LineHeight, a.GetLineHeight());
        Assert.Equal(Theme.LetterSpacing, a.GetLetterSpacing());
    }

    [Fact]
    public void ControlMeasurementUsesTheSameMetricsAsItsTextBlock()
    {
        using var control = new Label { Text = "First line\nSecond line", LineHeight = 1.5f, LetterSpacing = 1.25f };
        var bounds = new Size(320, 240);
        var style = control.CurrentStyle;
        var block = TextMeasurer.CreateTextBlock(control.Text, style.GetFont(), style.GetFontSize(), bounds,
            fontStyle: style.GetFontStyle(), lineHeight: 1.5f, letterSpacing: 1.25f);
        var measured = TextMeasurer.MeasureText(control.Text, control, bounds);
        Assert.Equal(block.MeasuredWidth, measured.Width);
        Assert.Equal(block.MeasuredHeight, measured.Height);
        var natural = TextMeasurer.CreateTextBlock(control.Text, style.GetFont(), style.GetFontSize(), bounds);
        Assert.True(block.MeasuredHeight > natural.MeasuredHeight);
        Assert.True(block.MeasuredWidth > natural.MeasuredWidth);
    }

    [Fact]
    public void TextBoxReusesUnchangedBlockAndRebuildsForMetricsIncludingDirectStyleChanges()
    {
        using var editor = new TextBox { Text = "Text with spaces", Width = 300, LineHeight = 1, LetterSpacing = 0 };
        var before = editor.document.GetTextBlock();
        Assert.Same(before, editor.document.GetTextBlock());
        editor.LetterSpacing = 2;
        var after = editor.document.GetTextBlock();
        Assert.NotSame(before, after);
        Assert.True(after.MeasuredWidth > before.MeasuredWidth);
        Assert.Same(after, editor.document.GetTextBlock());
        editor.Style.LineHeight = 1.8f;
        var taller = editor.document.GetTextBlock();
        Assert.NotSame(after, taller);
        Assert.True(taller.MeasuredHeight > after.MeasuredHeight);
        Assert.Same(taller, editor.document.GetTextBlock());
    }

    [Fact]
    public void DynamicResourceReplacementAndRemovalInvalidateMetricsAndRestoreFallback()
    {
        using var editor = new TextBox { Text = "Bound spacing", Width = 300, LetterSpacing = 0.25f };
        editor.Resources["Spacing"] = 1d;
        editor.SetResourceReference(nameof(Control.LetterSpacing), "Spacing");
        var first = editor.document.GetTextBlock();
        editor.Resources["Spacing"] = 2d;
        var second = editor.document.GetTextBlock();
        Assert.Equal(2f, editor.LetterSpacing);
        Assert.NotSame(first, second);
        Assert.True(second.MeasuredWidth > first.MeasuredWidth);
        editor.ClearResourceReference(nameof(Control.LetterSpacing));
        Assert.Equal(0.25f, editor.LetterSpacing);
        Assert.True(editor.document.GetTextBlock().MeasuredWidth < first.MeasuredWidth);
    }

    [Theory]
    [InlineData("Zażółć gęślą")]
    [InlineData("مرحبا بالعالم")]
    [InlineData("שלום עולם")]
    [InlineData("Cafe\u0301 👩‍💻")]
    public void SharedTextPaintingAppliesMetricsWithoutReplacingTheShapingEngine(string text)
    {
        using var control = new Label { Text = text, ForeColor = SKColors.Black, LineHeight = 1, LetterSpacing = 0 };
        using var natural = new SKBitmap(480, 80);
        using var spaced = new SKBitmap(480, 80);
        natural.Erase(SKColors.Transparent);
        spaced.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(natural))
            canvas.DrawText(text, new Rectangle(0, 0, 480, 80), control, ContentAlignment.TopLeft);
        control.LetterSpacing = 2;
        using (var canvas = new SKCanvas(spaced))
            canvas.DrawText(text, new Rectangle(0, 0, 480, 80), control, ContentAlignment.TopLeft);
        Assert.Contains(natural.Bytes, value => value != 0);
        Assert.Contains(spaced.Bytes, value => value != 0);
        Assert.False(natural.Bytes.SequenceEqual(spaced.Bytes));
    }

    [Fact]
    public void WarmInheritedMetricResolutionDoesNotAllocate()
    {
        var style = new ControlStyle(new ControlStyle(null) { LineHeight = 1.25f, LetterSpacing = 0.5f });
        for (int i = 0; i < 1000; i++) { _ = style.GetLineHeight(); _ = style.GetLetterSpacing(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        double result = 0;
        for (int i = 0; i < 1000; i++) result += style.GetLineHeight() + style.GetLetterSpacing();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1750f, result);
        Assert.Equal(0, allocated);
    }

    private sealed class LayoutProbeEditor : TextBox
    {
        internal int GeometryInvalidations;
        internal bool DisposeOnInvalidation;
        protected override void InvalidateAccessibleTextLayout()
        {
            GeometryInvalidations++;
            if (DisposeOnInvalidation) { DisposeOnInvalidation = false; Dispose(); }
        }
    }
}
