using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

// Native adapter and deterministic tests share these exact semantic choices. Android has no
// public retry label or question icon resource; Retry uses the backend's localized resource.
internal sealed record AndroidMessageDialogPlan(int Positive, int Negative, int Neutral, int Cancel, AndroidMessageIcon Icon)
{
    internal static T RequireHost<T>(T? host, bool shutdown, bool busy) where T : class
    {
        var status = shutdown ? PlatformServiceStatus.Shutdown : busy ? PlatformServiceStatus.Busy :
            host is null ? PlatformServiceStatus.Unavailable : PlatformServiceStatus.Success;
        if (status != PlatformServiceStatus.Success) throw new PlatformServiceException(status);
        return host!;
    }

    internal static AndroidMessageDialogPlan Create(PlatformMessageDialogRequest request) => new(
        0, request.Buttons == MessageBoxButtons.OK ? -1 : 1,
        request.Buttons == MessageBoxButtons.YesNoCancel ? 2 : -1,
        request.CancelButtonIndex,
        request.Icon switch {
            MessageBoxIcon.Information => AndroidMessageIcon.Information,
            MessageBoxIcon.Warning or MessageBoxIcon.Error => AndroidMessageIcon.Alert,
            _ => AndroidMessageIcon.None
        });
}
internal enum AndroidMessageIcon { None, Information, Alert }
