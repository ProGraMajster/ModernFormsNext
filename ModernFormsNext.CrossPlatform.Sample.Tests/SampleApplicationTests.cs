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
        using var root = app.Root;

        var texts = Descendants(app.Root).OfType<Label>().Select(label => label.Text).ToArray();

        Assert.Contains("Platform: Test Android", texts);
        Assert.Contains("OS: Test OS", texts);
        Assert.Contains("Backend: Test backend", texts);
    }

    [Fact]
    public void SharedButtonUpdatesStateWithoutAPlatformSpecificPage()
    {
        var app = new App(new FakePlatformServices());
        using var root = app.Root;
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
        using var root = app.Root;
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
        using var root = app.Root;
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
        using var root = app.Root;
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

    [Fact]
    public void SharedKeyboardCommandsUseAvailabilityFallbackAndSurviveSurfaceRecreation()
    {
        var originalGlobalBindings = Application.InputBindings.ToArray();
        var app = new App(new FakePlatformServices());
        using var root = app.Root;
        var editor = Descendants(root).OfType<TextBox>().Single(control => control.Name == "ImeSingleLine");
        var available = Descendants(root).OfType<CheckBox>().Single(control => control.Name == "LocalSaveAvailable");
        var counts = Descendants(root).OfType<Label>().Single(control => control.Name == "KeyboardCommandCounts");
        Assert.Equal(originalGlobalBindings.Length + 1, Application.InputBindings.Count);

        using (var first = new SkiaControlSurface(root))
        {
            first.Resize(480, 960);
            editor.Select();
            Press(first, Keys.Control | Keys.S);
            available.Checked = false;
            Press(first, Keys.Control | Keys.S);
            Press(first, Keys.Control | Keys.Shift | Keys.S);
            Press(first, Keys.F1);
            Assert.Equal("Commands: editor=1; page=1; save-as=1; help=1", counts.Text);
        }

        Assert.Equal(originalGlobalBindings.Length + 1, Application.InputBindings.Count);
        using (var second = new SkiaControlSurface(root))
        {
            second.Resize(480, 960);
            editor.Select();
            Press(second, Keys.F1);
            Press(second, Keys.Control | Keys.S);
            Assert.Equal("Commands: editor=1; page=2; save-as=1; help=2", counts.Text);
        }
        root.Dispose();
        Assert.Equal(originalGlobalBindings, Application.InputBindings.ToArray());

        static void Press(SkiaControlSurface surface, Keys key)
        {
            Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(key)));
            Assert.True(surface.TryProcessKeyUp(new KeyEventArgs(key)));
        }
    }

    [Fact]
    public void SharedSaveButtonsUseTheSameCommandsAndRemoveOnlyTheirOwnApplicationBinding()
    {
        var originalGlobalBindings = Application.InputBindings.ToArray();
        var unrelated = new KeyBinding(new DelegateCommand(() => { }), new KeyGesture(Keys.F12));
        Application.InputBindings.Add(unrelated);
        try
        {
            var app = new App(new FakePlatformServices());
            using var root = app.Root;
            var counts = Descendants(root).OfType<Label>().Single(control => control.Name == "KeyboardCommandCounts");
            Descendants(root).OfType<Button>().Single(control => control.Name == "KeyboardSave").PerformClick();
            Descendants(root).OfType<Button>().Single(control => control.Name == "KeyboardSaveAs").PerformClick();
            Assert.Equal("Commands: editor=1; page=0; save-as=1; help=0", counts.Text);
            root.Dispose();
            Assert.Contains(unrelated, Application.InputBindings);
            Assert.Equal(originalGlobalBindings.Length + 1, Application.InputBindings.Count);
        }
        finally { Application.InputBindings.Remove(unrelated); }
        Assert.Equal(originalGlobalBindings, Application.InputBindings.ToArray());
    }

    [Fact]
    public void ApplicationHelpUsesTheCurrentSurfaceRouteWhenAnotherPageRetainsSelection()
    {
        var firstApp = new App(new FakePlatformServices());
        using var firstRoot = firstApp.Root;
        using var firstSurface = new SkiaControlSurface(firstRoot);
        firstSurface.Resize(480, 960);
        Descendants(firstRoot).OfType<TextBox>().Single(control => control.Name == "ImeSingleLine").Select();
        var secondApp = new App(new FakePlatformServices());
        using var secondRoot = secondApp.Root;
        using var secondSurface = new SkiaControlSurface(secondRoot);
        secondSurface.Resize(480, 960);
        Descendants(secondRoot).OfType<TextBox>().Single(control => control.Name == "ImeSingleLine").Select();
        Assert.True(secondSurface.TryProcessKeyDown(new KeyEventArgs(Keys.F1)));
        Assert.True(secondSurface.TryProcessKeyUp(new KeyEventArgs(Keys.F1)));
        Assert.Equal("Commands: editor=0; page=0; save-as=0; help=0",
            Descendants(firstRoot).OfType<Label>().Single(control => control.Name == "KeyboardCommandCounts").Text);
        Assert.Equal("Commands: editor=0; page=0; save-as=0; help=1",
            Descendants(secondRoot).OfType<Label>().Single(control => control.Name == "KeyboardCommandCounts").Text);
    }

    [Fact]
    public void FailedPageConstructionDoesNotRetainAnApplicationBinding()
    {
        var bindings = Application.InputBindings;
        var original = bindings.ToArray();
        try
        {
            Assert.Throws<InvalidOperationException>(() => new App(new FakePlatformServices { ThrowOnBackendQuery = true }));
            Assert.Equal(original, bindings.ToArray());
        }
        finally
        {
            foreach (var binding in bindings.Except(original).ToArray()) bindings.Remove(binding);
        }
    }

    [Fact]
    public void FailingRegistrationDiagnosticRollsBackOnlyTheNewPageBinding()
    {
        var bindings = Application.InputBindings;
        var original = bindings.ToArray();
        var existing = new KeyBinding(new DelegateCommand(() => { }), new KeyGesture(Keys.F1));
        bindings.Add(existing);
        EventHandler<InputBindingDiagnosticEventArgs> diagnostic = (_, _) => throw new InvalidOperationException("diagnostic");
        bindings.Diagnostic += diagnostic;
        try
        {
            Assert.Throws<InvalidOperationException>(() => new App(new FakePlatformServices()));
            Assert.Equal(original.Append(existing), bindings.ToArray());
        }
        finally
        {
            bindings.Diagnostic -= diagnostic;
            foreach (var binding in bindings.Except(original).ToArray()) bindings.Remove(binding);
        }
    }

    [Fact]
    public void ThrowingChildDisposalCannotRetainThePageApplicationBinding()
    {
        var bindings = Application.InputBindings;
        var original = bindings.ToArray();
        var app = new App(new FakePlatformServices());
        var child = Descendants(app.Root).OfType<Button>().Single(control => control.Name == "KeyboardSave");
        EventHandler failure = (_, _) => throw new InvalidOperationException("child disposal");
        child.Disposed += failure;
        try
        {
            Assert.ThrowsAny<Exception>(app.Root.Dispose);
            Assert.Equal(original, bindings.ToArray());
        }
        finally
        {
            child.Disposed -= failure;
            app.Root.Dispose();
            foreach (var binding in bindings.Except(original).ToArray()) bindings.Remove(binding);
        }
    }

    private sealed class FakePlatformServices(
        string platformName = "Windows",
        string operatingSystem = "Test OS",
        string backendName = "Fake backend") : ISamplePlatformServices
    {
        public string PlatformName { get; } = platformName;
        public string OperatingSystem { get; } = operatingSystem;
        public bool ThrowOnBackendQuery { get; set; }
        public string BackendName => ThrowOnBackendQuery ? throw new InvalidOperationException("platform facts") : backendName;
        public string HostState => "Test host";
        public string AnimationRuntimeStatus => "Test animation runtime";
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
