using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModernFormsNext.DataBinding;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Threading;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TestPlatformServicesTests
{
    [Fact]
    public async Task ProductionClipboardUsesHostServiceAndClearsBetweenHosts()
    {
        IClipboard? baseline = AvaloniaGlobals.GetService<IClipboard>();
        using (var host = ModernFormsTestHost.Create())
        {
            Assert.Same(host.Services.Clipboard, AvaloniaGlobals.GetRequiredService<IClipboard>());
            await Clipboard.SetTextAsync("Zażółć 😀 日本語");
            Assert.Equal("Zażółć 😀 日本語", await host.Services.Clipboard.GetTextAsync());
            await host.Services.Clipboard.SetTextAsync("replacement");
            Assert.Equal("replacement", await Clipboard.GetTextAsync());
            await Clipboard.ClearAsync();
            Assert.Null(await Clipboard.GetTextAsync());
        }

        Assert.Same(baseline, AvaloniaGlobals.GetService<IClipboard>());
        using var next = ModernFormsTestHost.Create();
        Assert.Null(await Clipboard.GetTextAsync());
    }

    [Fact]
    public async Task ClipboardSnapshotsFormatsAndMutableValues()
    {
        using var host = ModernFormsTestHost.Create();
        var bytes = new byte[] { 1, 2, 3 };
        var names = new[] { "one", "two" };
        var data = new DataObject();
        data.Set("bytes", bytes);
        data.Set("names", names);
        data.Set(DataFormats.Text, "original");
        await host.Services.Clipboard.SetDataObjectAsync(data);
        bytes[0] = 9;
        names[0] = "changed";
        data.Set(DataFormats.Text, "changed");

        Assert.Equal("original", await Clipboard.GetTextAsync());
        Assert.Equal(new[] { DataFormats.Text, "bytes", "names" }, await host.Services.Clipboard.GetFormatsAsync());
        var returned = Assert.IsType<byte[]>(await host.Services.Clipboard.GetDataAsync("bytes"));
        Assert.Equal(new byte[] { 1, 2, 3 }, returned);
        returned[0] = 8;
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(await host.Services.Clipboard.GetDataAsync("bytes")));
        Assert.Equal(new[] { "one", "two" }, Assert.IsType<string[]>(await host.Services.Clipboard.GetDataAsync("names")));
        Assert.Null(await host.Services.Clipboard.GetDataAsync("missing"));
    }

    [Fact]
    public async Task NullTextClearsAllFormatsAndEmptyTextRemainsAvailable()
    {
        using var host = ModernFormsTestHost.Create();
        var data = new DataObject();
        data.Set("binary", new byte[] { 1 });
        await host.Services.Clipboard.SetDataObjectAsync(data);
        await host.Services.Clipboard.SetTextAsync(string.Empty);
        Assert.Equal(new[] { DataFormats.Text }, await host.Services.Clipboard.GetFormatsAsync());
        Assert.Equal(string.Empty, await Clipboard.GetTextAsync());
        await host.Services.Clipboard.SetTextAsync(null);
        Assert.Empty(await host.Services.Clipboard.GetFormatsAsync());
        Assert.Null(await Clipboard.GetTextAsync());
    }

    [Fact]
    public async Task UnsupportedObjectRejectsWholeClipboardWrite()
    {
        using var host = ModernFormsTestHost.Create();
        await Clipboard.SetTextAsync("preserved");
        var data = new DataObject();
        data.Set(DataFormats.Text, "partial replacement");
        data.Set("runtime", new object());
        await Assert.ThrowsAsync<ArgumentException>(() => host.Services.Clipboard.SetDataObjectAsync(data));
        Assert.Equal("preserved", await Clipboard.GetTextAsync());
        Assert.Equal(new[] { DataFormats.Text }, await host.Services.Clipboard.GetFormatsAsync());
    }

    [Fact]
    public async Task ThrowingDataGetterPreservesClipboard()
    {
        using var host = ModernFormsTestHost.Create();
        await Clipboard.SetTextAsync("preserved");
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Services.Clipboard.SetDataObjectAsync(new ThrowingDataObject()));
        Assert.Equal("preserved", await Clipboard.GetTextAsync());
    }

    [Fact]
    public async Task DuplicateFormatsAndFormatLimitRejectAtomically()
    {
        using var host = ModernFormsTestHost.Create();
        await Clipboard.SetTextAsync("preserved");
        await Assert.ThrowsAsync<ArgumentException>(() => host.Services.Clipboard.SetDataObjectAsync(new RepeatedFormatDataObject()));
        var many = new DataObject();
        for (var index = 0; index < 257; index++)
            many.Set(index.ToString(System.Globalization.CultureInfo.InvariantCulture), index);
        await Assert.ThrowsAsync<ArgumentException>(() => host.Services.Clipboard.SetDataObjectAsync(many));
        Assert.Equal("preserved", await Clipboard.GetTextAsync());
    }

    [Fact]
    public async Task InvalidAndOversizedInputsPreserveClipboard()
    {
        using var host = ModernFormsTestHost.Create();
        await Clipboard.SetTextAsync("preserved");
        await Assert.ThrowsAsync<ArgumentNullException>(() => host.Services.Clipboard.SetDataObjectAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => host.Services.Clipboard.GetDataAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => host.Services.Clipboard.SetTextAsync(new string('x', 4 * 1024 * 1024 + 1)));
        Assert.Equal("preserved", await Clipboard.GetTextAsync());
    }

    [Fact]
    public void LifecycleUsesExistingStateAndEventContract()
    {
        using var host = ModernFormsTestHost.Create();
        IPlatformApplicationLifecycle lifecycle = PlatformServiceRegistry.GetRequiredService<IPlatformApplicationLifecycle>();
        Assert.Same(host.Services.Lifecycle, lifecycle);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, lifecycle.State);
        var transitions = new List<(PlatformApplicationLifecycleState, PlatformApplicationLifecycleState)>();
        lifecycle.StateChanged += (sender, args) =>
        {
            Assert.Same(lifecycle, sender);
            Assert.Equal(args.CurrentState, lifecycle.State);
            transitions.Add((args.PreviousState, args.CurrentState));
        };
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.NoHost);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Unknown);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        Assert.Equal(new[]
        {
            (PlatformApplicationLifecycleState.Foreground, PlatformApplicationLifecycleState.Background),
            (PlatformApplicationLifecycleState.Background, PlatformApplicationLifecycleState.NoHost),
            (PlatformApplicationLifecycleState.NoHost, PlatformApplicationLifecycleState.Unknown),
            (PlatformApplicationLifecycleState.Unknown, PlatformApplicationLifecycleState.Foreground)
        }, transitions);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Services.Lifecycle.SetState((PlatformApplicationLifecycleState)100));
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, lifecycle.State);
    }

    [Fact]
    public void LifecycleFailureKeepsPublishedStateAndReentrantTransitionWins()
    {
        using var host = ModernFormsTestHost.Create();
        var lifecycle = host.Services.Lifecycle;
        EventHandler<PlatformApplicationLifecycleChangedEventArgs> throwing = (_, _) => throw new InvalidOperationException("observer");
        lifecycle.StateChanged += throwing;
        Assert.Throws<InvalidOperationException>(() => lifecycle.SetState(PlatformApplicationLifecycleState.Background));
        Assert.Equal(PlatformApplicationLifecycleState.Background, lifecycle.State);
        lifecycle.StateChanged -= throwing;
        lifecycle.StateChanged += (_, args) =>
        {
            if (args.CurrentState == PlatformApplicationLifecycleState.NoHost)
                lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        };
        lifecycle.SetState(PlatformApplicationLifecycleState.NoHost);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, lifecycle.State);
    }

    [Fact]
    public void ProductionThemeManagerReadsFakeSystemPreferenceAndDynamicResources()
    {
        using var host = ModernFormsTestHost.Create();
        host.Services.ThemeSettings.PreferredVariant = PlatformColorScheme.Dark;
        host.Services.ThemeSettings.ReducedMotion = true;
        var theme = new ThemeDefinition("test.system", "System fixture") { Variant = ThemeVariant.System };
        var result = ThemeManager.Current.Apply(theme, new ThemeApplyOptions { Transition = new ThemeTransitionOptions { Duration = TimeSpan.Zero } });
        Assert.True(result.Success);
        Assert.Equal(ThemeVariant.Dark, ThemeManager.Current.ActiveSnapshot!.Variant);

        const string resourceKey = "TestPlatformServices.Caption";
        Application.Resources[resourceKey] = "first";
        var label = new Label();
        label.SetResourceReference(nameof(Label.Text), resourceKey);
        host.Show(label);
        Application.Resources[resourceKey] = "updated";
        Assert.Equal("updated", label.Text);
        host.Services.ThemeSettings.PreferredVariant = PlatformColorScheme.Unknown;
        host.Services.ThemeSettings.ReducedMotion = null;
        Assert.Null(PlatformServiceRegistry.GetRequiredService<IPlatformThemeSettings>().GetReducedMotion());
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Services.ThemeSettings.PreferredVariant = (PlatformColorScheme)99);
    }

    [Fact]
    public async Task ClipboardTextCanBeConsumedThroughProductionBindingAndResourceSetters()
    {
        using var host = ModernFormsTestHost.Create();
        var model = new CaptionModel { Caption = "initial" };
        using var source = new BindingSource { DataSource = new BindingList<CaptionModel> { model } };
        var text = new TextBox();
        var binding = text.DataBindings.Add(nameof(TextBox.Text), source, nameof(CaptionModel.Caption), formattingEnabled: true);
        host.Show(text);
        text.BindingContext = new BindingContext();
        await Clipboard.SetTextAsync("copied through binding");
        model.Caption = (await Clipboard.GetTextAsync())!;
        binding.ReadValue();
        host.ProcessPendingWork();
        Assert.Equal("copied through binding", text.Text);
    }

    [Fact]
    public void ScopeRestoresExistingRegistrationsAfterException()
    {
        var clipboard = AvaloniaGlobals.GetService<IClipboard>();
        var lifecycle = PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>();
        var settings = PlatformServiceRegistry.GetService<IPlatformThemeSettings>();
        var animationSettings = PlatformServiceRegistry.GetService<IPlatformAnimationSettings>();
        var dispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var host = ModernFormsTestHost.Create();
            throw new InvalidOperationException("test body");
        }));
        Assert.Same(clipboard, AvaloniaGlobals.GetService<IClipboard>());
        Assert.Same(lifecycle, PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>());
        Assert.Same(settings, PlatformServiceRegistry.GetService<IPlatformThemeSettings>());
        Assert.Same(animationSettings, PlatformServiceRegistry.GetService<IPlatformAnimationSettings>());
        Assert.Same(dispatcher, PlatformServiceRegistry.GetService<IPlatformDispatcher>());
    }

    [Fact]
    public void NestedServiceScopesRestoreOuterServices()
    {
        using var host = ModernFormsTestHost.Create();
        var nested = new TestPlatformServices(host.Dispatcher);
        Assert.Same(nested.Clipboard, AvaloniaGlobals.GetRequiredService<IClipboard>());
        nested.Dispose();
        nested.Dispose();
        Assert.Same(host.Services.Clipboard, AvaloniaGlobals.GetRequiredService<IClipboard>());
        Assert.Same(host.Services.Lifecycle, PlatformServiceRegistry.GetRequiredService<IPlatformApplicationLifecycle>());
    }

    [Fact]
    public void ExpiredCapturedContextCannotResolveOrRetainFakeServices()
    {
        var baselineClipboard = AvaloniaGlobals.GetService<IClipboard>();
        var baselineLifecycle = PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>();
        (ExecutionContext context, WeakReference clipboard, WeakReference lifecycle) = CaptureExpiredContext();
        ExecutionContext.Run(context, _ =>
        {
            Assert.Same(baselineClipboard, AvaloniaGlobals.GetService<IClipboard>());
            Assert.Same(baselineLifecycle, PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>());
        }, null);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(clipboard.IsAlive);
        Assert.False(lifecycle.IsAlive);
        GC.KeepAlive(context);
    }

    [Fact]
    public void ExpiredWindowFactoryContextRejectsConstructionAndAllowsFreshHost()
    {
        var (context, _, _) = CaptureExpiredContext();
        ExecutionContext.Run(context, _ =>
        {
            Assert.Throws<ObjectDisposedException>(() => new Form());
            using var next = ModernFormsTestHost.Create();
            using var form = new Form();
            var window = next.Show(form);
            Assert.Same(form, window.FormRoot);
            Assert.Same(next.Services.Clipboard, AvaloniaGlobals.GetRequiredService<IClipboard>());
        }, null);
    }

    [Fact]
    public void WindowFactoryRejectsConstructionFromForeignThread()
    {
        using var host = ModernFormsTestHost.Create();
        Exception? failure = null;
        var worker = new Thread(() => failure = Record.Exception(() => { using var form = new Form(); }));
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public async Task RetainedFakeInstancesRejectUseAfterHostDisposal()
    {
        var host = ModernFormsTestHost.Create();
        var services = host.Services;
        host.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => services.Clipboard.GetTextAsync());
        Assert.Throws<ObjectDisposedException>(() => services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background));
        Assert.Throws<ObjectDisposedException>(() => services.ThemeSettings.PreferredVariant);
    }

    [Fact]
    public void FakeMutationsRejectForeignThreads()
    {
        using var host = ModernFormsTestHost.Create();
        Exception? clipboardError = null;
        Exception? lifecycleError = null;
        Exception? settingsError = null;
        var worker = new Thread(() =>
        {
            clipboardError = Record.Exception(() => { _ = host.Services.Clipboard.SetTextAsync("wrong thread"); });
            lifecycleError = Record.Exception(() => host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background));
            settingsError = Record.Exception(() => host.Services.ThemeSettings.ReducedMotion = true);
        });
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(clipboardError);
        Assert.IsType<InvalidOperationException>(lifecycleError);
        Assert.IsType<InvalidOperationException>(settingsError);
    }

    [Fact]
    public async Task PlatformDispatcherUsesHostQueueAndCancelsBeforeStart()
    {
        using var host = ModernFormsTestHost.Create();
        var platform = PlatformServiceRegistry.GetRequiredService<IPlatformDispatcher>();
        using var cancellation = new CancellationTokenSource();
        Task<int>? result = null;
        Task<int>? cancelled = null;
        var invoked = false;
        var worker = new Thread(() =>
        {
            result = platform.InvokeAsync(() => { Assert.True(host.Dispatcher.CheckAccess()); return 42; });
            cancelled = platform.InvokeAsync(() => { invoked = true; return 1; }, cancellation.Token);
        });
        worker.Start();
        worker.Join();
        Assert.False(result!.IsCompleted);
        cancellation.Cancel();
        host.ProcessPendingWork();
        Assert.Equal(42, await result);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled!);
        Assert.False(invoked);
    }

    [Fact]
    public void AnimationSettingsDriveProductionPolicyWithoutNativePreferences()
    {
        using var host = ModernFormsTestHost.Create();
        var settings = host.Services.AnimationSettings;
        Assert.Same(settings, PlatformServiceRegistry.GetRequiredService<IPlatformAnimationSettings>());
        Assert.Equal("HeadlessTestHost", settings.Current.Source);
        Assert.True(settings.Current.AnimationsEnabled);
        Assert.False(settings.Current.ReducedMotion);
        Assert.Equal(1d, settings.Current.DurationScale);
        Assert.Null(settings.Current.LastPlatformUpdate);
        var changes = 0;
        settings.Changed += (_, args) => { changes++; Assert.Same(args.Current, settings.Current); };
        settings.SetPreferences(reducedMotion: true, durationScale: 0.5);
        settings.SetPreferences(reducedMotion: true, durationScale: 0.5);
        Assert.Equal(1, changes);
        Assert.Same(settings.Current, settings.Refresh());
        Assert.True(ModernFormsNext.Animations.AnimationScheduler.Default.Policy.ReducedMotion);
        settings.SetPreferences();
        Assert.False(ModernFormsNext.Animations.AnimationScheduler.Default.Policy.ReducedMotion);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetPreferences(durationScale: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetPreferences(durationScale: -1));
        Assert.Equal(1d, settings.Current.DurationScale);
    }

    [Fact]
    public void LifecyclePausesAndResumesProductionAnimationScheduler()
    {
        using var host = ModernFormsTestHost.Create();
        var handle = ModernFormsNext.Animations.AnimationScheduler.Default.Start(
            new object(), "lifecycle", _ => { },
            new ModernFormsNext.Animations.AnimationOptions { Duration = TimeSpan.FromSeconds(1) });
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        Assert.Equal(ModernFormsNext.Animations.AnimationState.Paused, handle.State);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.NoHost);
        Assert.Equal(ModernFormsNext.Animations.AnimationState.Paused, handle.State);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        Assert.Equal(ModernFormsNext.Animations.AnimationState.Running, handle.State);
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(ModernFormsNext.Animations.AnimationState.Completed, handle.State);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ExecutionContext, WeakReference, WeakReference) CaptureExpiredContext()
    {
        using var host = ModernFormsTestHost.Create();
        return (ExecutionContext.Capture()!, new WeakReference(host.Services.Clipboard), new WeakReference(host.Services.Lifecycle));
    }

    private sealed class ThrowingDataObject : IDataObject
    {
        public bool Contains(string dataFormat) => true;
        public IEnumerable<string> GetDataFormats() => [DataFormats.Text, "failure"];
        public object? Get(string dataFormat) => dataFormat == DataFormats.Text ? "partial" : throw new InvalidOperationException("getter");
    }

    private sealed class RepeatedFormatDataObject : IDataObject
    {
        public bool Contains(string dataFormat) => true;
        public IEnumerable<string> GetDataFormats() => [DataFormats.Text, DataFormats.Text];
        public object? Get(string dataFormat) => "duplicate";
    }

    private sealed class CaptionModel : INotifyPropertyChanged
    {
        private string caption = string.Empty;
        public string Caption
        {
            get => caption;
            set { caption = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Caption))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
