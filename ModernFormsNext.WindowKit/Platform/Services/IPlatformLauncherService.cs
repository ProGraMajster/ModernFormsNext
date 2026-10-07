using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext.WindowKit.Platform.Services;

/// <summary>Hands URIs or storage files to external applications.</summary>
/// <remarks>
/// Call on the UI thread. Operations perform an immediate OS handoff, never wait for an external
/// application or pump a nested loop. Invalid arguments throw. Android requires a resumed
/// Activity, visible handlers and content URIs for external files.
/// </remarks>
public interface IPlatformLauncherService
{
    /// <summary>Checks eligibility and handler visibility without launching anything.</summary>
    /// <param name="uri">An absolute URI; raw file URIs are backend-dependent.</param>
    bool CanOpenUri(Uri uri);
    /// <summary>Requests a handoff; success does not imply the target completed its work.</summary>
    /// <param name="uri">An absolute URI; tel opens a dialer rather than placing a call.</param>
    PlatformServiceStatus OpenUri(Uri uri);
    /// <summary>Requests a read-only file handoff; the caller retains ownership of the file.</summary>
    /// <param name="file">An accessible storage item; Android requires a content URI.</param>
    /// <param name="mimeType">An optional MIME type, never an extension glob.</param>
    PlatformServiceStatus OpenFile(IStorageFile file, string? mimeType = null);
}
