namespace ModernFormsNext.WindowKit.Platform.Services;

/// <summary>Provides explicit capability behavior for backends without sharing or notifications.</summary>
public sealed class UnsupportedPlatformServices : IPlatformShareService, IPlatformNotificationService
{
    /// <inheritdoc/>
    public bool IsSupported => false;
    /// <inheritdoc/>
    public PlatformServiceStatus Status => PlatformServiceStatus.NotSupported;
    /// <inheritdoc/>
    public PlatformServiceStatus Show(PlatformNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Id);
        return Status;
    }
    /// <inheritdoc/>
    public PlatformServiceStatus Dismiss(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Status;
    }
    /// <inheritdoc/>
    public Task<PlatformServiceStatus> ShareAsync(PlatformShareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Status);
    }
}
