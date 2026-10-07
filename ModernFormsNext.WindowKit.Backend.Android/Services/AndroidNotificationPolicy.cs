using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

internal static class AndroidNotificationPolicy
{
    internal const string ChannelId = "mfn.local";
    internal static bool NeedsChannel(int api) => api >= 26;
    internal static PlatformServiceStatus Evaluate(int api, PlatformPermissionStatus permission, bool enabled, bool channelEnabled, bool shutdown)
    {
        if (shutdown) return PlatformServiceStatus.Shutdown;
        if (api >= 33 && permission == PlatformPermissionStatus.NotDeclared) return PlatformServiceStatus.NotDeclared;
        if (api >= 33 && permission != PlatformPermissionStatus.Granted) return PlatformServiceStatus.PermissionDenied;
        return enabled && (!NeedsChannel(api) || channelEnabled) ? PlatformServiceStatus.Success : PlatformServiceStatus.PermissionDenied;
    }
}
