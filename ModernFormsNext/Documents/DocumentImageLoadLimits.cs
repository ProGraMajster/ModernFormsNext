using System;

namespace ModernFormsNext.Documents;

internal readonly record struct DocumentImageLoadLimits(
    int MaxDownloadBytes,
    long MaxDecodedPixels,
    TimeSpan RequestTimeout)
{
    public static DocumentImageLoadLimits Default { get; } = new(
        DocumentImageLoadOptions.DefaultMaxDownloadBytes,
        DocumentImageLoadOptions.DefaultMaxDecodedPixels,
        TimeSpan.FromSeconds(15));
}
