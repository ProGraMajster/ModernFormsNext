using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext;

/// <summary>
/// Configures validation, destination mapping, and collision handling for a local Markdown image
/// asset.
/// </summary>
/// <remarks>
/// The host application owns project structure decisions. Both directory paths must be configured
/// explicitly and should normally be fully qualified. The generated Markdown path is relative to
/// <see cref="MarkdownBaseDirectory"/> and always uses forward slashes.
/// </remarks>
public sealed class MarkdownImageAssetOptions
{
    /// <summary>
    /// Gets the default maximum source file size: 10 MiB.
    /// </summary>
    public const long DefaultMaxFileBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyList<string> defaultAllowedExtensions
        = Array.AsReadOnly([".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"]);
    private IReadOnlyList<string> allowedExtensions = defaultAllowedExtensions;
    private long maxFileBytes = DefaultMaxFileBytes;

    /// <summary>
    /// Gets or sets the image extensions accepted by the asset processor.
    /// </summary>
    /// <remarks>
    /// Extensions are normalized to lowercase and a leading period is optional. SVG is not in the
    /// default set because the current SkiaSharp bitmap loader does not decode SVG documents.
    /// </remarks>
    public IReadOnlyList<string> AllowedExtensions
    {
        get => allowedExtensions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var normalized = value
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalized.Length == 0)
                throw new ArgumentException("At least one image extension must be allowed.", nameof(value));

            allowedExtensions = normalized;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether an absolute Markdown path may be returned when a
    /// relative path cannot be formed, for example across different Windows drive roots.
    /// </summary>
    /// <remarks>The default is <see langword="false"/> to avoid leaking local machine paths.</remarks>
    public bool AllowAbsoluteMarkdownSource { get; set; }

    /// <summary>
    /// Gets or sets how an existing destination filename is handled.
    /// </summary>
    public MarkdownImageAssetCollisionBehavior CollisionBehavior { get; set; }
        = MarkdownImageAssetCollisionBehavior.GenerateUniqueName;

    /// <summary>
    /// Gets or sets the fully qualified directory into which the image is copied.
    /// </summary>
    public string DestinationDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified directory used as the base for the generated Markdown path.
    /// </summary>
    public string MarkdownBaseDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum source file size in bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not greater than zero.</exception>
    public long MaxFileBytes
    {
        get => maxFileBytes;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "The value must be greater than zero.");

            maxFileBytes = value;
        }
    }

    /// <summary>
    /// Gets or sets an optional preferred destination filename without changing the source
    /// extension.
    /// </summary>
    public string? PreferredFileName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the source magic bytes must match its extension.
    /// </summary>
    public bool ValidateImageSignature { get; set; } = true;

    internal static string NormalizeExtension(string extension)
    {
        extension = extension.Trim();
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        return extension.ToLowerInvariant();
    }
}
