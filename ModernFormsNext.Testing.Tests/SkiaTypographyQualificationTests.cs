using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaQualification;
using SkiaSharp;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

// The fixture asserts stable invariants on every host. Per-font results are evidence for matched
// environments since installed glyph coverage and hinting vary across test machines.
public sealed class SkiaTypographyQualificationTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    [InlineData(2.25d)]
    public void CorpusUsesSharedMeasurementAndClippedShaping(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { BackColor = SKColors.White };
        var window = host.Show(root, new TestViewport(460, 960, scale));
        var font = new ModernFormsNext.Font("Arial", 18);
        using var probe = new SKBitmap(252, 95);
        using var canvas = new SKCanvas(probe);
        var ink = new Rectangle(5, 5, 225, 80);
        var metrics = new List<object>();
        for (int index = 0; index < TextCorpus.Cases.Length; index++)
        {
            var sample = TextCorpus.Cases[index];
            var label = root.Controls.Add(new Label
            {
                Bounds = new(6, 5 + index * 56, 440, index == TextCorpus.Cases.Length - 1 ? 110 : 55),
                Font = font,
                Text = sample.Text,
                Multiline = true
            });
            canvas.Clear(SKColors.White);
            canvas.DrawText(sample.Text, font.ToTypeface(), 18, ink, SKColors.Black, ContentAlignment.TopLeft);
            Assert.Equal(SKColors.White, probe.GetPixel(235, 25));
            Assert.Equal(SKColors.White, probe.GetPixel(20, 90));

            var measured = TextMeasurer.MeasureText(sample.Text, label, new Size((int)(228 * scale), int.MaxValue));
            Assert.True(float.IsFinite(measured.Width) && measured.Width > 0, sample.Id);
            Assert.True(float.IsFinite(measured.Height) && measured.Height > 0, sample.Id);

            // RichTextKit surfaces the actual resolved run typefaces. Compare the same style
            // inputs as TextMeasurer, without inferring cluster coverage from pixels or .notdef boxes.
            var block = new TextBlock { MaxWidth = 228 };
            block.AddText(sample.Text, new Style { FontFamily = font.ToTypeface().FamilyName, FontSize = 18 });
            Assert.True(block.LineCount >= 1, sample.Id);
            var hit = block.HitTest(10, 7);
            Assert.InRange(hit.ClosestCodePointIndex, 0, sample.Text.EnumerateRunes().Count());
            if (sample.Id == "multiline")
                Assert.True(block.LineCount >= 3);
            if (sample.Id == "mixed-bidi")
                Assert.Contains(block.FontRuns, run => run.Direction == TextDirection.RTL);
            if (sample.Id == "latin-kerning")
            {
                var ligature = new TextBlock();
                ligature.AddText("ffi", new Style { FontFamily = font.ToTypeface().FamilyName, FontSize = 24 });
                Assert.True(ligature.MeasuredWidth > 0);
                metrics.Add(new { Id = "ffi", ligature.MeasuredWidth, carets = ligature.CaretIndicies.ToArray() });
            }
            metrics.Add(new
            {
                sample.Id, measured.Width, measured.Height, block.LineCount,
                carets = block.CaretIndicies.ToArray(),
                runs = block.FontRuns.Select(run => new
                {
                    run.Start, run.Length, font = run.Typeface.FamilyName, direction = run.Direction.ToString(),
                    glyphIds = run.Glyphs.ToArray(), run.Width
                }).ToArray()
            });
        }
        using var image = window.CaptureRenderedSnapshot();
        using var repeated = window.CaptureRenderedSnapshot();
        Assert.Equal(image.CopyPixels(), repeated.CopyPixels());
        Export("corpus", scale, image, metrics);
    }

    [Fact]
    public void EditorsKeepUtf16SelectionAndCompositionAcrossSupplementaryText()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var plain = root.Controls.Add(new TextBox { Bounds = new(8, 8, 280, 70), MultiLine = true });
        var rich = root.Controls.Add(new RichTextBox { Bounds = new(8, 83, 280, 70) });
        var viewer = root.Controls.Add(new MarkdownViewer { Bounds = new(310, 8, 270, 145) });
        var markdown = root.Controls.Add(new MarkdownEditor { Bounds = new(8, 165, 572, 270) });
        const string text = "👩🏽‍💻 A\u030A العربية שלום\n選択";
        viewer.Markdown = "**Zażółć** 👩🏽‍💻 العربية\n\n日本語";
        markdown.Markdown = viewer.Markdown;
        var window = host.Show(root, new TestViewport(600, 450, 1.5));
        foreach (var editor in new TextBox[] { plain, rich })
        {
            editor.Text = text;
            Assert.True(window.Input.Focus(editor));
            var client = window.Input.TextInputClient!;
            int start = text.IndexOf('A');
            Assert.True(client.SetSelection(start, start + 2));
            var selection = client.GetState()!;
            Assert.Equal(start, selection.SelectionStart);
            Assert.Equal(start + 2, selection.SelectionEnd);
            Assert.True(selection.CaretRectangle.Height > 0);
            Assert.True(double.IsFinite(selection.CaretRectangle.X));
            Assert.True(client.SetComposingText("かな"));
            Assert.True(client.GetState()!.HasComposition);
            Assert.True(client.CancelComposition());
            Assert.Equal(text, editor.Text);
        }

        using var image = window.CaptureRenderedSnapshot();
        Export("editors", 1.5, image, new { utf16Length = text.Length, viewer.Markdown });
    }

    [Fact]
    public void ImageDecodeAndRepeatedSoftwareCaptureKeepOwnershipAndPixelEdges()
    {
        using var host = ModernFormsTestHost.Create();
        using var original = new SKBitmap(12, 8);
        original.Erase(SKColors.DarkBlue);
        using (var canvas = new SKCanvas(original))
        using (var paint = new SKPaint { Color = SKColors.Gold })
            canvas.DrawRect(0, 0, 6, 8, paint);
        using var encoded = original.Encode(SKEncodedImageFormat.Png, 100);
        using var decoded = SKBitmap.Decode(encoded);
        Assert.Equal(SKColors.Gold, decoded.GetPixel(2, 2));
        Assert.Equal(SKColors.DarkBlue, decoded.GetPixel(8, 2));

        var root = new Panel { BackColor = SKColors.White };
        var picture = root.Controls.Add(new PictureBox { Image = decoded, Bounds = new(20, 18, 160, 70) });
        var window = host.Show(root, new TestViewport(220, 112, 1.5));
        using var initial = window.CaptureRenderedSnapshot();
        using (var copy = SKBitmap.Decode(initial.EncodePng()))
        {
            Assert.Equal(initial.PixelWidth, copy.Width);
            Assert.Equal(initial.GetPixel(32, 30), copy.GetPixel(32, 30));
            Assert.NotEqual(SKColors.White, initial.GetPixel(32, 30));
            Assert.Equal(SKColors.White, initial.GetPixel(18, 18));
        }

        // Move the same borrowed image through new pixels on each iteration. Capture
        // latency includes TestHost raster and copying, with no native present timing.
        var samples = new double[24];
        using var process = Process.GetCurrentProcess();
        double cpuStart = process.TotalProcessorTime.TotalMilliseconds;
        long managedStart = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < samples.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            picture.Left = i % 2 == 0 ? 30 : 20;
            using var frame = window.CaptureRenderedSnapshot();
            Assert.Equal(i % 2 == 0 ? SKColors.White : initial.GetPixel(32, 30), frame.GetPixel(32, 30));
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        process.Refresh();
        window.Close();
        Assert.Equal(SKColors.Gold, decoded.GetPixel(2, 2));
        Export("image-paint", 1.5, initial, new
        {
            samples, cpuMs = process.TotalProcessorTime.TotalMilliseconds - cpuStart,
            managedBytes = GC.GetTotalAllocatedBytes(precise: true) - managedStart,
            process.WorkingSet64, process.PrivateMemorySize64
        });
    }

    private static void Export(string scenario, double scale, RenderedSnapshot image, object metrics)
    {
        if (Environment.GetEnvironmentVariable("MFN_SKIA_QUALIFICATION_OUTPUT") is not { Length: > 0 } root)
            return;

        Directory.CreateDirectory(root);
        var prefix = System.IO.Path.Combine(root, FormattableString.Invariant($"{scenario}-{scale:g}"));
        byte[] png = image.EncodePng();
        File.WriteAllBytes(prefix + ".png", png);
        File.WriteAllText(prefix + ".json", JsonSerializer.Serialize(new
        {
            corpus = TextCorpus.Version,
            runtime = RuntimeInformation.FrameworkDescription,
            skia = typeof(SKCanvas).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            native = SkiaSharpVersion.Native.ToString(),
            richTextKit = typeof(TextBlock).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            image.PixelWidth, image.PixelHeight, scale,
            pngSha256 = Convert.ToHexString(SHA256.HashData(png)),
            metrics
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
