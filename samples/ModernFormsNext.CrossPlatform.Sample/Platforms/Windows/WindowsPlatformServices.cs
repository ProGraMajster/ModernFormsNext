using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.CrossPlatform.Sample;

internal sealed class WindowsPlatformServices : ISamplePlatformServices
{
    private readonly WindowsSampleDispatcher dispatcher = new();

    public string PlatformName => "Windows";

    public string OperatingSystem => Environment.OSVersion.VersionString;

    public string BackendName => WindowKitBackendRegistry.Current?.PlatformName ?? "Windows (initializing)";

    public string HostState => "Desktop window active";

    public IPlatformDispatcher Dispatcher => dispatcher;

    public bool SupportsPermissionAction => false;

    public Task<PlatformPermissionStatus> CheckSamplePermissionAsync()
        => Task.FromResult(PlatformPermissionStatus.NotSupported);

    public Task<PlatformPermissionStatus> RequestSamplePermissionAsync()
        => Task.FromResult(PlatformPermissionStatus.NotSupported);

    public Task<bool> OpenApplicationSettingsAsync() => Task.FromResult(false);

    private sealed class WindowsSampleDispatcher : IPlatformDispatcher
    {
        public bool CheckAccess() => ModernFormsNext.WindowKit.Threading.Dispatcher.UIThread.CheckAccess();

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ModernFormsNext.WindowKit.Threading.Dispatcher.UIThread.Post(action);
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            return ModernFormsNext.WindowKit.Threading.Dispatcher.UIThread
                .InvokeAsync(action, DispatcherPriority.Default, cancellationToken)
                .GetTask();
        }

        public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(function);
            return ModernFormsNext.WindowKit.Threading.Dispatcher.UIThread
                .InvokeAsync(function, DispatcherPriority.Default, cancellationToken)
                .GetTask();
        }
    }
}
