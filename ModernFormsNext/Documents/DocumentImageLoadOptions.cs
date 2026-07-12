using System;

namespace ModernFormsNext.Documents;

/// <summary>
/// Configures resource limits used while loading images for a <see cref="DocumentViewer"/>.
/// </summary>
/// <remarks>
/// Mutating an option cancels pending image loads owned by the viewer and restarts those pending
/// resources with a new options snapshot. Images that have already loaded successfully remain
/// cached. A zero <see cref="RequestTimeout"/> disables the per-request timeout.
/// </remarks>
public sealed class DocumentImageLoadOptions
{
    /// <summary>
    /// Gets the default maximum encoded image size: 10 MiB.
    /// </summary>
    public const int DefaultMaxDownloadBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Gets the default maximum decoded image size: 32 million pixels.
    /// </summary>
    public const long DefaultMaxDecodedPixels = 32_000_000;

    private int maxDownloadBytes = DefaultMaxDownloadBytes;
    private long maxDecodedPixels = DefaultMaxDecodedPixels;
    private TimeSpan requestTimeout = TimeSpan.FromSeconds(15);

    internal event EventHandler? Changed;

    /// <summary>
    /// Initializes a new options instance with limits of 10 MiB encoded data, 32 million decoded
    /// pixels, and a 15 second request timeout.
    /// </summary>
    public DocumentImageLoadOptions()
    {
    }

    /// <summary>
    /// Gets or sets the maximum encoded byte count allowed for one image resource.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not greater than zero.</exception>
    public int MaxDownloadBytes
    {
        get => maxDownloadBytes;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "The value must be greater than zero.");

            SetValue(ref maxDownloadBytes, value);
        }
    }

    /// <summary>
    /// Gets or sets the maximum decoded pixel count allowed for one image resource.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not greater than zero.</exception>
    public long MaxDecodedPixels
    {
        get => maxDecodedPixels;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "The value must be greater than zero.");

            SetValue(ref maxDecodedPixels, value);
        }
    }

    /// <summary>
    /// Gets or sets the timeout for one HTTP or HTTPS image request.
    /// </summary>
    /// <remarks>Set this property to <see cref="TimeSpan.Zero"/> to disable the timeout.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public TimeSpan RequestTimeout
    {
        get => requestTimeout;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "The value cannot be negative.");

            SetValue(ref requestTimeout, value);
        }
    }

    internal DocumentImageLoadLimits ToLimits()
        => new(MaxDownloadBytes, MaxDecodedPixels, RequestTimeout);

    private void SetValue<T>(ref T field, T value)
    {
        if (Equals(field, value))
            return;

        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
