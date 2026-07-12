using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.CrossPlatform.Sample.Tests;

public sealed class SampleApplicationTests
{
    [Fact]
    public void AppUsesInjectedPlatformFactsInTheSharedControlTree()
    {
        var platform = new FakePlatformServices("Test Android", "Test OS", "Test backend");
        var app = new App(platform);

        var texts = app.Root.Controls.OfType<Label>().Select(label => label.Text).ToArray();

        Assert.Contains("Platform: Test Android", texts);
        Assert.Contains("OS: Test OS", texts);
        Assert.Contains("Backend: Test backend", texts);
    }

    [Fact]
    public void SharedButtonUpdatesStateWithoutAPlatformSpecificPage()
    {
        var app = new App(new FakePlatformServices());
        var button = app.Root.Controls.OfType<Button>()
            .Single(button => button.Text == "Run shared action");

        button.PerformClick();

        Assert.Equal(1, app.State.ClickCount);
    }

    [Fact]
    public void DispatcherButtonUsesInjectedDispatcher()
    {
        var platform = new FakePlatformServices();
        var app = new App(platform);
        var button = app.Root.Controls.OfType<Button>()
            .Single(button => button.Text == "Post through UI dispatcher");

        button.PerformClick();

        Assert.Equal(1, platform.FakeDispatcher.PostCount);
        Assert.Equal(1, app.State.DispatcherCount);
    }

    [Fact]
    public void ReattachingAHostCanReuseTheSameAppRootAndState()
    {
        var app = new App(new FakePlatformServices());
        var button = app.Root.Controls.OfType<Button>()
            .Single(button => button.Text == "Run shared action");
        for (var index = 0; index < 4; index++)
            button.PerformClick();
        var firstHostRoot = app.Root;

        app.RefreshPlatformStatus();
        var recreatedHostRoot = app.Root;

        Assert.Same(firstHostRoot, recreatedHostRoot);
        Assert.Equal(4, app.State.ClickCount);
    }

    private sealed class FakePlatformServices(
        string platformName = "Windows",
        string operatingSystem = "Test OS",
        string backendName = "Fake backend") : ISamplePlatformServices
    {
        public string PlatformName { get; } = platformName;
        public string OperatingSystem { get; } = operatingSystem;
        public string BackendName { get; } = backendName;
        public string HostState => "Test host";
        public FakeDispatcher FakeDispatcher { get; } = new();
        public IPlatformDispatcher Dispatcher => FakeDispatcher;
        public bool SupportsPermissionAction => true;
        public Task<PlatformPermissionStatus> RequestSamplePermissionAsync()
            => Task.FromResult(PlatformPermissionStatus.Granted);
    }

    private sealed class FakeDispatcher : IPlatformDispatcher
    {
        public int PostCount { get; private set; }
        public bool CheckAccess() => true;
        public void Post(Action action)
        {
            PostCount++;
            action();
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(function());
        }
    }
}
