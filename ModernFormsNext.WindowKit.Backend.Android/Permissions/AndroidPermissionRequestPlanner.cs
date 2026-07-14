using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

// Terminal permission states must never reach Android's runtime-dialog coordinator. In
// particular, NotDeclared is a manifest error reported to the caller, not a requestable state.
internal static class AndroidPermissionRequestPlanner
{
    internal static bool ShouldContinueRequestFlow(PlatformPermissionStatus status)
        => status is not (
            PlatformPermissionStatus.NotDeclared or
            PlatformPermissionStatus.NotSupported or
            PlatformPermissionStatus.Granted or
            PlatformPermissionStatus.PermanentlyDenied);
}
