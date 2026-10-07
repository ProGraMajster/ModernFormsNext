namespace ModernFormsNext.WindowKit.Platform.Services;

/// <summary>Posts, replaces and dismisses basic local system notifications.</summary>
/// <remarks>
/// Extends the original placeholder. No push, scheduling, actions or background services.
/// Android uses application context and allows calls from any thread without an Activity.
/// Request permission explicitly through IPermissionService. Windows reports NotSupported;
/// its richer notification work is tracked separately in #158.
/// </remarks>
public interface IPlatformNotificationService
{
    /// <summary>Gets permission/channel availability without showing UI.</summary>
    PlatformServiceStatus Status { get; }
    /// <summary>Posts or replaces the notification with the same ordinal ID.</summary>
    /// <param name="notification">A stable ID and plain title/body; content is never logged.</param>
    PlatformServiceStatus Show(PlatformNotification notification);
    /// <summary>Dismisses only this ID; an absent ID is a successful no-op.</summary>
    /// <param name="id">The stable ID used to show the notification.</param>
    PlatformServiceStatus Dismiss(string id);
}

/// <summary>Defines basic local notification content.</summary>
/// <param name="Id">Nonempty identity, stable across updates and process starts.</param>
/// <param name="Title">The title displayed by the OS.</param>
/// <param name="Body">The body displayed by the OS.</param>
public sealed record PlatformNotification(string Id, string Title, string Body);
