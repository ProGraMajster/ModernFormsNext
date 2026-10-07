namespace ModernFormsNext.WindowKit.Platform.Services;

/// <summary>Opens a system sharing chooser for text and accessible content URIs.</summary>
/// <remarks>
/// Call on the UI thread. Android waits for chooser return without proving target selection or
/// delivery; user dismissal also returns Success. Caller cancellation throws, and native faults
/// propagate. No uploads, private raw paths or ownership transfer are implied.
/// </remarks>
public interface IPlatformShareService
{
    /// <summary>Gets backend support, independent of current Activity eligibility.</summary>
    bool IsSupported { get; }
    /// <summary>Shows the chooser for a snapshot of the request.</summary>
    /// <param name="request">Text and/or content URI attachments.</param>
    /// <param name="cancellationToken">Cancels managed waiting; native UI may remain visible.</param>
    Task<PlatformServiceStatus> ShareAsync(PlatformShareRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Describes text and optional read-only attachments for a system chooser.</summary>
public sealed class PlatformShareRequest
{
    /// <summary>Gets or sets the optional chooser caption.</summary>
    public string? Title { get; set; }
    /// <summary>Gets or sets text, including ordinary web links.</summary>
    public string? Text { get; set; }
    /// <summary>Gets or sets accessible content URI attachments; ownership is not transferred.</summary>
    public IReadOnlyList<Uri> Items { get; set; } = Array.Empty<Uri>();
    /// <summary>Gets or sets a MIME type; null uses text/plain or a generic attachment type.</summary>
    public string? MimeType { get; set; }
}
