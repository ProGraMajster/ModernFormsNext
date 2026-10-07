using System.ComponentModel;
using System.Diagnostics;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext.WindowKit.Backend.Windows;

// Shell execution stays in the Windows backend. Existing Help resolution/navigation is shared.
internal sealed class WindowsLauncherService : IPlatformLauncherService
{
    public bool CanOpenUri(Uri uri)
    {
        Validate(uri);
        if (uri.IsFile) return File.Exists(uri.LocalPath) || Directory.Exists(uri.LocalPath);
        // Query associations without opening anything. Unknown/custom schemes may have no handler.
        using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(uri.Scheme);
        return key?.GetValue("URL Protocol") is not null;
    }
    public PlatformServiceStatus OpenUri(Uri uri)
    {
        Validate(uri);
        try
        {
            using var process = Process.Start(new ProcessStartInfo(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri) { UseShellExecute = true });
            return PlatformServiceStatus.Success;
        }
        catch (Win32Exception e) when (e.NativeErrorCode is 2 or 3 or 31 or 1155) { return PlatformServiceStatus.NoHandler; }
        catch (UnauthorizedAccessException) { return PlatformServiceStatus.PermissionDenied; }
    }
    public PlatformServiceStatus OpenFile(IStorageFile file, string? mimeType = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.Path.IsAbsoluteUri && file.Path.IsFile ? OpenUri(file.Path) : PlatformServiceStatus.NotSupported;
    }
    private static void Validate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri) throw new ArgumentException("An absolute URI is required.", nameof(uri));
    }
}
