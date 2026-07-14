using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FontResolutionMetricsCollection
{
    public const string Name = "Font resolution metrics";
}

[Collection(FontResolutionMetricsCollection.Name)]
public sealed class FontResolutionPerformanceTests
{
    [Fact]
    public void RepeatedFontRequestReusesSharedTypefaceAndRecordsFallback()
    {
        FontResolutionDiagnostics.ResetCounters();
        FontResolutionDiagnostics.SetEnabled(true);
        try
        {
            var font = new Font($"missing-family-{Guid.NewGuid():N}", 14, FontStyle.Bold | FontStyle.Italic);

            var first = font.ToTypeface();
            var second = font.ToTypeface();
            var metrics = FontResolutionDiagnostics.Snapshot();

            Assert.Same(first, second);
            Assert.False(string.IsNullOrWhiteSpace(first.FamilyName));
            Assert.True(metrics.TypefaceCacheHits >= 1 || metrics.CapacityFallbacks >= 1);
            Assert.True(metrics.FallbackResolutions >= 1);
            Assert.InRange(metrics.CachedTypefaces, 0, metrics.CacheCapacity);
        }
        finally
        {
            FontResolutionDiagnostics.SetEnabled(false);
        }
    }

    [Fact]
    public void TypefaceCacheIsThreadSafeForOneKey()
    {
        var font = new Font($"parallel-family-{Guid.NewGuid():N}", 13, FontStyle.Regular);
        var results = new SKTypeface[32];

        Parallel.For(0, results.Length, index => results[index] = font.ToTypeface());

        Assert.All(results, typeface => Assert.Same(results[0], typeface));
        Assert.InRange(TypefaceCache.Count, 0, TypefaceCache.Capacity);
    }

    [Fact]
    public void DisposingOneControlSurfaceDoesNotInvalidateSharedTypeface()
    {
        var font = new Font("sans-serif", 12, FontStyle.Regular);
        var first = font.ToTypeface();
        using (var adapter = new SkiaControlSurface(new Label { Font = font, Text = "first" }))
        {
            adapter.Resize(100, 30);
        }

        var reused = font.ToTypeface();

        Assert.Same(first, reused);
        Assert.False(string.IsNullOrWhiteSpace(reused.FamilyName));
    }

    [Fact]
    public void CacheNeverExceedsItsHardCapacity()
    {
        for (var index = 0; index < TypefaceCache.Capacity + 24; index++)
        {
            var font = new Font($"bounded-family-{index}-{Guid.NewGuid():N}", 12, FontStyle.Regular);
            Assert.NotNull(font.ToTypeface());
        }

        Assert.InRange(TypefaceCache.Count, 0, TypefaceCache.Capacity);
    }

    [Fact]
    public void RepeatedRenderDoesNotResolveNewTypefaceOrExplodeStyleTraversal()
    {
        var label = new Label
        {
            Width = 240,
            Height = 40,
            Text = "Zażółć gęślą jaźń 👋",
            Font = new Font("sans-serif", 14, FontStyle.Regular)
        };
        using var adapter = new SkiaControlSurface(label);
        using var nativeSurface = SKSurface.Create(new SKImageInfo(240, 40));
        adapter.Resize(240, 40);
        adapter.Render(nativeSurface.Canvas);

        FontResolutionDiagnostics.ResetCounters();
        FontResolutionDiagnostics.SetEnabled(true);
        try
        {
            for (var index = 0; index < 40; index++)
                adapter.Render(nativeSurface.Canvas);

            var metrics = FontResolutionDiagnostics.Snapshot();
            Assert.Equal(0, metrics.FromFamilyNameCalls);
            Assert.Equal(0, metrics.TypefaceCacheMisses);
            Assert.InRange(metrics.StyleResolverCalls, 1, 999);
            Assert.True(metrics.StyleNodesVisited >= metrics.StyleResolverCalls);
        }
        finally
        {
            FontResolutionDiagnostics.SetEnabled(false);
        }
    }
}
