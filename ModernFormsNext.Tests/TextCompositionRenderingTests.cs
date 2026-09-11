using System.Drawing;
using ModernFormsNext.Renderers;
using ModernFormsNext.WindowKit.Input;
using SkiaSharp;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TextCompositionRenderingTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("rich")]
    [InlineData("markdown")]
    public void EachEditorPaintsOnlyTheProvisionalRangeAndFinishRemovesIt(string kind)
    {
        using var fixture = new Fixture(kind, "prefix middle suffix");
        fixture.Client.SetSelection(0, 0);
        using var before = fixture.Paint();
        Assert.True(fixture.Client.SetComposingRegion(7, 13));
        using var composing = fixture.Paint();
        var changed = Difference(before, composing);
        Assert.NotEmpty(changed);
        AssertInsideViewport(fixture.Editor, changed);
        AssertBetweenCarets(fixture, changed, 7, 13);

        Assert.True(fixture.Client.FinishComposition());
        using var finished = fixture.Paint();
        Assert.Empty(Difference(before, finished));
        Assert.Equal("prefix middle suffix", fixture.Editor.Text);
    }

    [Fact]
    public void EmojiRangeUsesUtf16OffsetsWithoutUnderliningTheFollowingCharacter()
    {
        using var fixture = new Fixture("plain", "A😀BC");
        using var before = fixture.Paint();
        fixture.Client.SetComposingRegion(1, 3);
        using var composing = fixture.Paint();
        var changed = Difference(before, composing);
        Assert.NotEmpty(changed);
        AssertBetweenCarets(fixture, changed, 1, 3);
    }

    [Fact]
    public void CrLfCompositionPaintsSeparateLinesUsingTheirActualBaselines()
    {
        using var fixture = new Fixture("plain", "AB\r\nCDEF", multiLine: true);
        using var before = fixture.Paint();
        fixture.Client.SetComposingRegion(1, 6);
        using var composing = fixture.Paint();
        var changed = Difference(before, composing);
        var block = fixture.Block;
        var origin = fixture.Editor.GetTextOrigin(block);
        Assert.Equal(2, block.Lines.Count);
        AssertInsideViewport(fixture.Editor, changed);
        foreach (var line in block.Lines)
            Assert.Contains(changed, pixel => pixel.Y >= origin.Y + line.YCoord + line.BaseLine - 1 &&
                pixel.Y < origin.Y + line.YCoord + line.Height);
        Assert.All(changed, pixel => Assert.Contains(block.Lines, line =>
            pixel.Y >= origin.Y + line.YCoord + line.BaseLine - 1 &&
            pixel.Y < origin.Y + line.YCoord + line.Height));
    }

    [Fact]
    public void RichCompositionFollowsWrappedMixedSizeRunsAndZoom()
    {
        using var fixture = new Fixture("rich", "small LARGE small words wrap across lines", width: 145, height: 240);
        var rich = (RichTextBox)fixture.Editor;
        rich.Select(6, 5);
        rich.SelectionFont = new ModernFormsNext.Font("Segoe UI", 23, FontStyle.Bold);
        rich.ZoomFactor = 1.25f;
        fixture.Client.SetSelection(0, 0);
        using var before = fixture.Paint();
        fixture.Client.SetComposingRegion(0, rich.Text.Length);
        using var composing = fixture.Paint();
        var changed = Difference(before, composing);
        Assert.True(fixture.Block.Lines.Count >= 3);
        AssertInsideViewport(rich, changed);
        var origin = rich.GetTextOrigin(fixture.Block);
        foreach (var line in fixture.Block.Lines)
            Assert.Contains(changed, pixel => pixel.Y >= origin.Y + line.YCoord + line.BaseLine - 1 &&
                pixel.Y < origin.Y + line.YCoord + line.Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScrolledCompositionIsClippedToTheExistingTextViewport(bool vertical)
    {
        string text = vertical ? string.Join("\r\n", Enumerable.Repeat("scrollable line", 14))
            : string.Concat(Enumerable.Repeat("scrollable ", 18));
        using var fixture = new Fixture("plain", text, width: 150, height: 74, multiLine: vertical);
        fixture.Client.SetSelection(text.Length, text.Length);
        using var before = fixture.Paint();
        var origin = fixture.Editor.GetTextOrigin(fixture.Block);
        Assert.True(vertical ? origin.Y < fixture.Editor.PaddedClientRectangle.Y
            : origin.X < fixture.Editor.PaddedClientRectangle.X);
        fixture.Client.SetComposingRegion(0, text.Length);
        using var composing = fixture.Paint();
        var changed = Difference(before, composing);
        Assert.NotEmpty(changed);
        AssertInsideViewport(fixture.Editor, changed);
        fixture.Client.FinishComposition();
        using var finished = fixture.Paint();
        Assert.Empty(Difference(before, finished));
    }

    [Fact]
    public void BidirectionalCompositionDoesNotUnderlineItsUnrelatedVisualNeighbor()
    {
        using var fixture = new Fixture("plain", "left אבגד right");
        using var before = fixture.Paint();
        fixture.Client.SetComposingRegion(5, 7);
        using var composing = fixture.Paint();
        var changed = Difference(before, composing);
        Assert.NotEmpty(changed);
        AssertBetweenCarets(fixture, changed, 5, 7);
        AssertInsideViewport(fixture.Editor, changed);
    }

    [Fact]
    public void RegionOnlyTransitionsInvalidateTheCachedSurfaceAndCancelRestoresItsPixels()
    {
        using var fixture = new Fixture("plain", "unchanged text");
        using var before = fixture.PaintSurface();
        int invalidations = 0;
        fixture.Surface.Invalidated += (_, _) => invalidations++;

        fixture.Client.SetComposingRegion(0, 9);
        Assert.True(invalidations > 0);
        using var composing = fixture.PaintSurface();
        Assert.NotEmpty(Difference(before, composing));
        invalidations = 0;
        fixture.Client.CancelComposition();
        Assert.True(invalidations > 0);
        using var canceled = fixture.PaintSurface();
        Assert.Empty(Difference(before, canceled));
    }

    private static void AssertBetweenCarets(Fixture fixture, List<Point> changed, int start, int end)
    {
        var block = fixture.Block;
        var origin = fixture.Editor.GetTextOrigin(block);
        var first = TextMeasurer.GetCursorLocation(block, origin,
            fixture.Editor.document.GetLayoutCodePointIndex(start), fixture.Editor.CurrentFontSize);
        var last = TextMeasurer.GetCursorLocation(block, origin,
            fixture.Editor.document.GetLayoutCodePointIndex(end), fixture.Editor.CurrentFontSize);
        Assert.All(changed, pixel => Assert.InRange(pixel.X,
            Math.Min(first.X, last.X) - 1, Math.Max(first.X, last.X) + 1));
    }

    private static void AssertInsideViewport(TextBox editor, List<Point> changed)
        => Assert.All(changed, pixel => Assert.True(editor.PaddedClientRectangle.Contains(pixel),
            $"Composition escaped the text viewport at {pixel}."));

    private static List<Point> Difference(SKBitmap before, SKBitmap after)
    {
        Assert.Equal(before.Info, after.Info);
        var changed = new List<Point>();
        for (int y = 0; y < before.Height; y++)
            for (int x = 0; x < before.Width; x++)
                if (before.GetPixel(x, y) != after.GetPixel(x, y))
                    changed.Add(new Point(x, y));
        return changed;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Panel root = new();
        internal TextBox Editor { get; }
        internal SkiaControlSurface Surface { get; }
        internal ITextInputClient Client { get; }
        internal TextBlock Block => Editor is RichTextBox rich ? rich.GetRichTextBlock() : Editor.document.GetTextBlock();
        private readonly Renderer renderer;
        private readonly int width;
        private readonly int height;

        internal Fixture(string kind, string text, int width = 400, int height = 180, bool multiLine = false)
        {
            this.width = width;
            this.height = height;
            Control content;
            if (kind == "markdown") {
                var markdown = new MarkdownEditor { Markdown = text, ShowToolbar = false };
                content = markdown;
                Editor = markdown.EditorSurface;
                renderer = new MarkdownEditorTextBoxRenderer();
            } else if (kind == "rich") {
                Editor = new RichTextBox { Text = text };
                content = Editor;
                renderer = new RichTextBoxRenderer();
            } else {
                Editor = new TextBox { Text = text, MultiLine = multiLine, TextAlign = ContentAlignment.TopLeft };
                content = Editor;
                renderer = new TextBoxRenderer();
            }
            content.Dock = DockStyle.Fill;
            root.Controls.Add(content);
            Surface = new SkiaControlSurface(root);
            Surface.Resize(width, height);
            Editor.Select();
            Assert.True(Editor.Selected);
            Client = Editor.QueryTextInputClient()!;
            Client.SetSelection(0, 0);
        }

        internal SKBitmap Paint()
        {
            var bitmap = new SKBitmap(Editor.Width, Editor.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            renderer.Render(Editor, new PaintEventArgs(bitmap.Info, canvas, 1));
            canvas.Flush();
            return bitmap;
        }

        internal SKBitmap PaintSurface()
        {
            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            Surface.Render(canvas);
            canvas.Flush();
            return bitmap;
        }

        public void Dispose()
        {
            Surface.Dispose();
            root.Dispose();
        }
    }
}
