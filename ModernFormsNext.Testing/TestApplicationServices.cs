using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext.Testing;

/// <summary>Deterministic launcher, sharing and notification substitutes scoped to a test host.</summary>
/// <remarks>
/// All operations require the host UI thread. Configure NextStatus or NextException to exercise
/// success, unavailable, permission denial, caller cancellation or native failure paths. Each
/// configured outcome is consumed once. No OS UI or fake Android Activity is created.
/// </remarks>
public sealed class TestApplicationServices
    : IPlatformLauncherService, IPlatformShareService, IPlatformNotificationService
{
    private readonly UiTestDispatcher dispatcher;
    private readonly Dictionary<string, PlatformNotification> notifications = new(StringComparer.Ordinal);
    private bool disposed;
    internal TestApplicationServices(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;
    /// <summary>Gets or sets the next operation outcome; it resets to Success after use.</summary>
    public PlatformServiceStatus NextStatus { get; set; } = PlatformServiceStatus.Success;
    /// <summary>Gets or sets a one-shot exception, including OperationCanceledException.</summary>
    public Exception? NextException { get; set; }
    /// <summary>Gets the last launched URI without starting an application.</summary>
    public Uri? LastOpenedUri { get; private set; }
    /// <summary>Gets a detached notification snapshot keyed by exact application IDs.</summary>
    public IReadOnlyDictionary<string, PlatformNotification> Notifications
    { get { Verify(); return new Dictionary<string, PlatformNotification>(notifications); } }
    /// <inheritdoc/>
    public bool IsSupported { get { Verify(); return true; } }
    /// <inheritdoc/>
    public PlatformServiceStatus Status { get { Verify(); return NextStatus; } }
    /// <inheritdoc/>
    public bool CanOpenUri(Uri uri) { Validate(uri); return NextStatus == PlatformServiceStatus.Success; }
    /// <inheritdoc/>
    public PlatformServiceStatus OpenUri(Uri uri)
    {
        Validate(uri);
        var result = Complete();
        if (result == PlatformServiceStatus.Success) LastOpenedUri = uri;
        return result;
    }
    /// <inheritdoc/>
    public PlatformServiceStatus OpenFile(IStorageFile file, string? mimeType = null)
    { ArgumentNullException.ThrowIfNull(file); return OpenUri(file.Path); }
    /// <inheritdoc/>
    public Task<PlatformServiceStatus> ShareAsync(PlatformShareRequest request, CancellationToken cancellationToken = default)
    {
        Verify(); ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Complete());
    }
    /// <inheritdoc/>
    public PlatformServiceStatus Show(PlatformNotification notification)
    {
        Verify(); ArgumentNullException.ThrowIfNull(notification); ArgumentException.ThrowIfNullOrWhiteSpace(notification.Id);
        var result = Complete();
        if (result == PlatformServiceStatus.Success) notifications[notification.Id] = notification;
        return result;
    }
    /// <inheritdoc/>
    public PlatformServiceStatus Dismiss(string id)
    {
        Verify(); ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var result = Complete();
        if (result == PlatformServiceStatus.Success) notifications.Remove(id);
        return result;
    }
    private PlatformServiceStatus Complete()
    {
        Verify();
        var error = NextException; NextException = null;
        var result = NextStatus; NextStatus = PlatformServiceStatus.Success;
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return result;
    }
    private void Validate(Uri uri)
    {
        Verify(); ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri) throw new ArgumentException("An absolute URI is required.", nameof(uri));
    }
    private void Verify() { dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(disposed, this); }
    internal void Dispose() { disposed = true; notifications.Clear(); LastOpenedUri = null; NextException = null; }
}
