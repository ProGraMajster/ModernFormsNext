using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class BackendRegistrationTests
{
    [Fact]
    public void PlatformServiceRegistryPublishesOneImplementation()
    {
        var service = new TestService();

        var registered = PlatformServiceRegistry.Register<ITestService>(service);

        Assert.Same(service, registered);
        Assert.Same(service, PlatformServiceRegistry.GetRequiredService<ITestService>());
        Assert.Throws<InvalidOperationException>(
            () => PlatformServiceRegistry.Register<ITestService>(new TestService()));
    }

    [Fact]
    public void RegisterInitializesOneBackendAndRejectsAnotherPlatform()
    {
        var first = new TestBackend("Test Android");

        var registered = WindowKitBackendRegistry.Register(first);
        var duplicate = WindowKitBackendRegistry.Register(new TestBackend("Test Android"));

        Assert.Same(first, registered);
        Assert.Same(first, duplicate);
        Assert.True(first.IsInitialized);
        Assert.Equal(1, first.InitializeCount);

        var exception = Assert.Throws<InvalidOperationException>(
            () => WindowKitBackendRegistry.Register(new OtherTestBackend()));
        Assert.Contains("already active", exception.Message);
    }

    private sealed class TestBackend(string platformName) : IWindowKitBackend
    {
        public string PlatformName { get; } = platformName;

        public bool IsInitialized { get; private set; }

        public int InitializeCount { get; private set; }

        public void Initialize()
        {
            InitializeCount++;
            IsInitialized = true;
        }
    }

    private sealed class OtherTestBackend : IWindowKitBackend
    {
        public string PlatformName => "Other";

        public bool IsInitialized => false;

        public void Initialize()
        {
        }
    }

    private interface ITestService
    {
    }

    private sealed class TestService : ITestService
    {
    }
}
