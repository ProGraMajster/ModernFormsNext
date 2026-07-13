using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.CrossPlatform.Sample;

internal sealed class AndroidPlatformServices(AndroidWindowKitBackend backend) : ISamplePlatformServices
{
    private readonly AndroidWindowKitBackend backend = backend ?? throw new ArgumentNullException(nameof(backend));

    public string PlatformName => "Android";

    public string OperatingSystem => $"Android API {backend.PlatformInfo.SdkVersion}";

    public string BackendName => backend.PlatformName;

    public string HostState => backend.ActivityTracker.State switch
    {
        AndroidApplicationLifecycleState.Foreground => "Activity resumed",
        AndroidApplicationLifecycleState.Created => "Activity created",
        AndroidApplicationLifecycleState.Background => "Activity paused/stopped",
        AndroidApplicationLifecycleState.NoActivity => "No live activity",
        _ => "Activity unknown"
    };

    public IPlatformDispatcher Dispatcher => backend.Dispatcher;

    public bool SupportsPermissionAction => true;

    public async Task<PlatformPermissionStatus> CheckSamplePermissionAsync()
        => (await backend.Permissions.CheckAsync(PlatformPermission.Camera)).Status;

    public async Task<PlatformPermissionStatus> RequestSamplePermissionAsync()
        => (await backend.Permissions.RequestAsync(PlatformPermission.Camera)).Status;

    public Task<bool> OpenApplicationSettingsAsync()
        => backend.Permissions.OpenApplicationSettingsAsync();
}
