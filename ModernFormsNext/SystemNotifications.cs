using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.Notifications;

namespace ModernFormsNext;

/// <summary>Resolves OS notifications through the canonical platform service registry, independently of in-app UI.</summary>
/// <remarks>Register the Windows notification provider explicitly during startup. This facade does not install
/// an identity, initialize COM, download images, or create a second application lifetime.</remarks>
/// <example><code>
/// var result = await SystemNotifications.ShowAsync(new SystemNotification
/// {
///     Id = "download-42", Title = "Download complete", Message = "Example video"
/// });
/// </code></example>
public static class SystemNotifications
{
    /// <summary>Gets the current optional service, owned by the host that registered it.</summary>
    public static ISystemNotificationService? Service => PlatformServiceRegistry.GetService<ISystemNotificationService>();
    /// <summary>Gets native capability information without initializing or registering a backend.</summary>
    public static SystemNotificationCapabilities Capabilities => Service?.Capabilities ?? SystemNotificationCapabilities.Unavailable;
    /// <summary>Gets whether a registered provider supports basic notifications.</summary>
    public static bool IsSupported => Capabilities.IsSupported;
    /// <summary>Submits native notification content, returning Unsupported when no service is registered.</summary>
    public static Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        return Service?.ShowAsync(notification, cancellationToken)
            ?? Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported));
    }
    /// <summary>Queries current application access without prompting or initializing a provider.</summary>
    public static Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Service?.GetStatusAsync(cancellationToken) ?? Task.FromResult(SystemNotificationAccess.Unavailable);
    }
    /// <summary>Explicitly requests authorization on platforms that require it; Windows only reports current settings.</summary>
    public static Task<SystemNotificationAccess> RequestPermissionAsync(SystemNotificationPermissionRequest? request = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Service?.RequestPermissionAsync(request, cancellationToken) ?? Task.FromResult(SystemNotificationAccess.Unavailable);
    }
}
