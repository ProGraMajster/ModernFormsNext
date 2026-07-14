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

        var texts = Descendants(app.Root).OfType<Label>().Select(label => label.Text).ToArray();

        Assert.Contains("Platform: Test Android", texts);
        Assert.Contains("OS: Test OS", texts);
        Assert.Contains("Backend: Test backend", texts);
    }

    [Fact]
    public void SharedButtonUpdatesStateWithoutAPlatformSpecificPage()
    {
        var app = new App(new FakePlatformServices());
        var button = Descendants(app.Root).OfType<Button>()
            .Single(button => button.Text == "Run shared action");

        button.PerformClick();

        Assert.Equal(1, app.State.ClickCount);
        Assert.Equal("Run shared action: click received", app.State.LastAction);
    }

    [Fact]
    public void DispatcherButtonUsesInjectedDispatcher()
    {
        var platform = new FakePlatformServices();
        var app = new App(platform);
        var button = Descendants(app.Root).OfType<Button>()
            .Single(button => button.Text == "Post through UI dispatcher");

        button.PerformClick();

        Assert.Equal(1, platform.FakeDispatcher.PostCount);
        Assert.Equal(1, app.State.DispatcherCount);
        Assert.Equal("Dispatcher button: click received", app.State.LastAction);
        Assert.Equal("IPlatformDispatcher.Post invoked", app.State.LastServiceInvocation);
        Assert.Equal("Completed; UI access: True", app.State.LastServiceResult);
    }

    [Fact]
    public void NotDeclaredPermissionIsReportedAsServiceResult()
    {
        var platform = new FakePlatformServices
        {
            RequestStatus = PlatformPermissionStatus.NotDeclared
        };
        var app = new App(platform);
        var button = Descendants(app.Root).OfType<Button>()
            .Single(button => button.Text == "Request camera");

        button.PerformClick();

        Assert.Equal(1, platform.RequestCount);
        Assert.Equal("Request camera: click received", app.State.LastAction);
        Assert.Equal("IPermissionService.RequestAsync invoked", app.State.LastServiceInvocation);
        Assert.Equal("Completed: Not declared", app.State.LastServiceResult);
    }

    [Fact]
    public void ReattachingAHostCanReuseTheSameAppRootAndState()
    {
        var app = new App(new FakePlatformServices());
        var button = Descendants(app.Root).OfType<Button>()
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
        public PlatformPermissionStatus CheckStatus { get; set; } = PlatformPermissionStatus.Granted;
        public PlatformPermissionStatus RequestStatus { get; set; } = PlatformPermissionStatus.Granted;
        public int CheckCount { get; private set; }
        public int RequestCount { get; private set; }
        public IPlatformDispatcher Dispatcher => FakeDispatcher;
        public bool SupportsPermissionAction => true;
        public Task<PlatformPermissionStatus> CheckSamplePermissionAsync()
        {
            CheckCount++;
            return Task.FromResult(CheckStatus);
        }
        public Task<PlatformPermissionStatus> RequestSamplePermissionAsync()
        {
            RequestCount++;
            return Task.FromResult(RequestStatus);
        }
        public Task<bool> OpenApplicationSettingsAsync() => Task.FromResult(true);
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

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var descendant in Descendants(control))
                yield return descendant;
        }
    }
}
