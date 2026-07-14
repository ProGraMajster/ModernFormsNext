using System.Threading;
using SkiaSharp;

namespace ModernFormsNext;

// Font lookup is used by layout and rendering, so instrumentation stays allocation-free on the
// hot path. Tests and diagnostics can take an immutable snapshot outside rendering.
internal static class FontResolutionDiagnostics
{
    private static long styleResolverCalls;
    private static long styleNodesVisited;
    private static long typefaceRequests;
    private static long typefaceCacheHits;
    private static long typefaceCacheMisses;
    private static long fromFamilyNameCalls;
    private static long fallbackResolutions;
    private static long capacityFallbacks;
    private static int enabled;

    internal static bool IsEnabled => Volatile.Read(ref enabled) != 0;

    internal static void SetEnabled(bool value) => Volatile.Write(ref enabled, value ? 1 : 0);

    internal static void RecordStyleResolverCall()
    {
        if (IsEnabled)
            Interlocked.Increment(ref styleResolverCalls);
    }

    internal static void RecordStyleNodeVisited()
    {
        if (IsEnabled)
            Interlocked.Increment(ref styleNodesVisited);
    }

    internal static void RecordTypefaceRequest()
    {
        if (IsEnabled)
            Interlocked.Increment(ref typefaceRequests);
    }

    internal static void RecordTypefaceCacheHit()
    {
        if (IsEnabled)
            Interlocked.Increment(ref typefaceCacheHits);
    }

    internal static void RecordTypefaceCacheMiss()
    {
        if (IsEnabled)
            Interlocked.Increment(ref typefaceCacheMisses);
    }

    internal static void RecordFromFamilyNameCall()
    {
        if (IsEnabled)
            Interlocked.Increment(ref fromFamilyNameCalls);
    }

    internal static void RecordFallbackResolution()
    {
        if (IsEnabled)
            Interlocked.Increment(ref fallbackResolutions);
    }

    internal static void RecordCapacityFallback()
    {
        if (IsEnabled)
            Interlocked.Increment(ref capacityFallbacks);
    }

    internal static FontResolutionMetrics Snapshot()
        => new(
            Interlocked.Read(ref styleResolverCalls),
            Interlocked.Read(ref styleNodesVisited),
            Interlocked.Read(ref typefaceRequests),
            Interlocked.Read(ref typefaceCacheHits),
            Interlocked.Read(ref typefaceCacheMisses),
            Interlocked.Read(ref fromFamilyNameCalls),
            Interlocked.Read(ref fallbackResolutions),
            Interlocked.Read(ref capacityFallbacks),
            TypefaceCache.Count,
            TypefaceCache.Capacity);

    internal static void ResetCounters()
    {
        Interlocked.Exchange(ref styleResolverCalls, 0);
        Interlocked.Exchange(ref styleNodesVisited, 0);
        Interlocked.Exchange(ref typefaceRequests, 0);
        Interlocked.Exchange(ref typefaceCacheHits, 0);
        Interlocked.Exchange(ref typefaceCacheMisses, 0);
        Interlocked.Exchange(ref fromFamilyNameCalls, 0);
        Interlocked.Exchange(ref fallbackResolutions, 0);
        Interlocked.Exchange(ref capacityFallbacks, 0);
    }
}

internal readonly record struct FontResolutionMetrics(
    long StyleResolverCalls,
    long StyleNodesVisited,
    long TypefaceRequests,
    long TypefaceCacheHits,
    long TypefaceCacheMisses,
    long FromFamilyNameCalls,
    long FallbackResolutions,
    long CapacityFallbacks,
    int CachedTypefaces,
    int CacheCapacity);

// Cached typefaces are shared by controls for the process lifetime and are never disposed by an
// individual control. The hard capacity deliberately falls back instead of evicting and disposing
// an object that may still be referenced by a renderer on another thread.
internal static class TypefaceCache
{
    internal const int Capacity = 128;

    private static readonly object Sync = new();
    private static readonly Dictionary<TypefaceKey, SKTypeface> Typefaces = new(TypefaceKeyComparer.Instance);

    internal static int Count
    {
        get
        {
            lock (Sync)
                return Typefaces.Count;
        }
    }

    internal static SKTypeface GetOrCreate(
        string? familyName,
        SKFontStyleWeight weight,
        SKFontStyleSlant slant,
        SKTypeface regularFallback,
        SKTypeface boldFallback)
    {
        FontResolutionDiagnostics.RecordTypefaceRequest();
        var key = new TypefaceKey(familyName ?? string.Empty, weight, slant);

        lock (Sync)
        {
            if (Typefaces.TryGetValue(key, out var cached))
            {
                FontResolutionDiagnostics.RecordTypefaceCacheHit();
                return cached;
            }

            FontResolutionDiagnostics.RecordTypefaceCacheMiss();
            var fallback = weight >= SKFontStyleWeight.SemiBold ? boldFallback : regularFallback;
            if (Typefaces.Count >= Capacity)
            {
                FontResolutionDiagnostics.RecordCapacityFallback();
                FontResolutionDiagnostics.RecordFallbackResolution();
                return fallback;
            }

            var typeface = FromFamilyName(familyName, weight, slant);
            var usedPlatformFallback = typeface is null && familyName is not null;
            var substituted = typeface is not null && !string.IsNullOrWhiteSpace(familyName) &&
                !string.Equals(typeface.FamilyName, familyName, StringComparison.OrdinalIgnoreCase);

            if (typeface is null && familyName is not null)
                typeface = FromFamilyName(null, weight, slant);

            if (typeface is null)
                typeface = fallback;

            if (usedPlatformFallback || substituted || ReferenceEquals(typeface, fallback))
                FontResolutionDiagnostics.RecordFallbackResolution();

            Typefaces.Add(key, typeface);
            return typeface;
        }
    }

    private static SKTypeface? FromFamilyName(
        string? familyName,
        SKFontStyleWeight weight,
        SKFontStyleSlant slant)
    {
        FontResolutionDiagnostics.RecordFromFamilyNameCall();
        return SKTypeface.FromFamilyName(familyName, weight, SKFontStyleWidth.Normal, slant);
    }

    private readonly record struct TypefaceKey(
        string FamilyName,
        SKFontStyleWeight Weight,
        SKFontStyleSlant Slant);

    private sealed class TypefaceKeyComparer : IEqualityComparer<TypefaceKey>
    {
        internal static readonly TypefaceKeyComparer Instance = new();

        public bool Equals(TypefaceKey x, TypefaceKey y)
            => x.Weight == y.Weight && x.Slant == y.Slant &&
                StringComparer.OrdinalIgnoreCase.Equals(x.FamilyName, y.FamilyName);

        public int GetHashCode(TypefaceKey obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FamilyName),
                obj.Weight,
                obj.Slant);
    }
}
