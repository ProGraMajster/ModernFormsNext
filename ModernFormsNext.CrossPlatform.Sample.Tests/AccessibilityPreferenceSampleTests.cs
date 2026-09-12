using System.Drawing;
using ModernFormsNext.Testing;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.CrossPlatform.Sample.Tests;

[CollectionDefinition("Sample accessibility preferences", DisableParallelization = true)]
public sealed class SampleAccessibilityPreferenceCollection { }

[Collection("Sample accessibility preferences")]
public sealed class AccessibilityPreferenceSampleTests
{
    [Fact]
    public void ExpiredProviderAndThrowingChildBothReportFailureWithoutRetainingGlobalCommand()
    {
        var host = ModernFormsTestHost.Create();
        var app = new App(new Platform());
        var bindings = Application.InputBindings;
        Find<CheckBox>(app, "FollowAccessibilityPreferences").Checked = true;
        var child = Find<Label>(app, "AccessibilityPreferenceStatus");
        EventHandler failure = (_, _) => throw new InvalidOperationException("child cleanup");
        child.Disposed += failure;
        try
        {
            // This deliberately retained, unhosted page outlives its service scope. Unsubscription
            // encounters the genuinely expired UI dispatcher; no mock-only failure seam is added.
            host.Dispose();
            var error = Assert.Throws<AggregateException>(app.Root.Dispose).Flatten();
            Assert.Contains(error.InnerExceptions, e => e is ObjectDisposedException);
            Assert.Contains(error.InnerExceptions, e => e is InvalidOperationException && e.Message == "child cleanup");
            Assert.Empty(bindings);
        }
        finally
        {
            child.Disposed -= failure;
            app.Root.Dispose();
            host.Dispose();
        }
    }

    [Fact]
    public void PreferencesAreExplicitThenApplyAuthoredTypographyOnceWithUsableRows()
    {
        using var host = ModernFormsTestHost.Create();
        host.Services.Settings.SetPreferences(HighContrast(), 2);
        var before = ThemeManager.Current.ActiveSnapshot;
        var app = new App(new Platform());
        host.Show(app.Root, 411, 840);
        var toggle = Find<CheckBox>(app, "FollowAccessibilityPreferences");
        var editor = Find<TextBox>(app, "ImeSingleLine");
        var status = Find<Label>(app, "AccessibilityPreferenceStatus");
        Assert.False(toggle.Checked);
        Assert.Same(before, ThemeManager.Current.ActiveSnapshot);
        toggle.Checked = true;
        host.ProcessPendingWork();
        Assert.Equal(2, ThemeManager.Current.ActiveSnapshot!.TextScale);
        Assert.Equal(28, ThemeManager.Current.ActiveSnapshot.Typography["Body"].Size);
        Assert.Equal(Color.Black, ThemeManager.Current.ActiveSnapshot.Get(ThemeTokens.Colors.Background));
        Assert.Equal(Color.White, ThemeManager.Current.ActiveSnapshot.Get(ThemeTokens.Colors.TextDisabled));
        Assert.Contains("contrast=High", status.Text);
        var measured = TextMeasurer.MeasureText(status.Text, status, new Size(status.Width - 16, int.MaxValue));
        Assert.True(status.Height >= measured.Height);
        Assert.True(editor.Height > editor.Font.SizeInPoints);
        host.Services.Settings.SetPreferences(HighContrast(), 1.5);
        host.ProcessPendingWork();
        host.Services.Settings.SetPreferences(HighContrast(), 2);
        host.ProcessPendingWork();
        Assert.Equal(28, ThemeManager.Current.ActiveSnapshot.Typography["Body"].Size);
    }

    [Fact]
    public void DisposalRevokesQueuedApplyBeforeAnyThemeMutation()
    {
        using var host = ModernFormsTestHost.Create();
        host.Services.Settings.SetPreferences(HighContrast(), 2);
        var before = ThemeManager.Current.ActiveSnapshot;
        var app = new App(new Platform());
        Find<CheckBox>(app, "FollowAccessibilityPreferences").Checked = true;
        app.Root.Dispose();
        host.ProcessPendingWork();
        Assert.Same(before, ThemeManager.Current.ActiveSnapshot);
        host.Services.Settings.SetPreferences(HighContrast(), 1.5);
        host.ProcessPendingWork();
        Assert.Same(before, ThemeManager.Current.ActiveSnapshot);
    }

    [Theory]
    [InlineData(2d)]
    [InlineData(2.25d)]
    public void EmptyEditorKeepsTheSameScaledLineMinimumAfterApplyAndResize(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        host.Services.Settings.SetPreferences(textScale: scale);
        var app = new App(new Platform());
        var editor = Find<TextBox>(app, "ImeSingleLine");
        editor.Text = string.Empty;
        var window = host.Show(app.Root, 411, 840);
        Find<CheckBox>(app, "FollowAccessibilityPreferences").Checked = true;
        host.ProcessPendingWork();

        int lineHeight = (int)Math.Ceiling(TextMeasurer.MeasureText("Ag", editor).Height / editor.ScaleFactor.Height) + 16;
        Assert.True(lineHeight > 38); // The authored row is too short at these actual font sizes.
        Assert.True(editor.Height >= lineHeight);
        int emptyHeight = editor.Height;
        editor.Text = "A populated editor";
        window.Resize(412, 840);
        Assert.Equal(emptyHeight, editor.Height);
        editor.Text = string.Empty;
        window.Resize(411, 840);
        Assert.Equal(emptyHeight, editor.Height);
        Assert.True(editor.Height >= lineHeight);
    }

    [Fact]
    public void BorrowedSurfaceRecreationKeepsOneConsumerAndUnknownRemainsVisible()
    {
        using var host = ModernFormsTestHost.Create();
        var app = new App(new Platform());
        using var root = app.Root;
        var toggle = Find<CheckBox>(app, "FollowAccessibilityPreferences");
        using (var first = new SkiaControlSurface(root))
        {
            first.Resize(411, 840);
            toggle.Checked = true;
            host.ProcessPendingWork();
            Assert.Contains("contrast=unknown", Find<Label>(app, "AccessibilityPreferenceStatus").Text);
            Assert.Contains("text scale=unknown", Find<Label>(app, "AccessibilityPreferenceStatus").Text);
        }
        using (var second = new SkiaControlSurface(root))
        {
            second.Resize(411, 840);
            host.Services.Settings.SetPreferences(HighContrast(), 1.5);
            host.ProcessPendingWork();
            Assert.Equal(1.5, ThemeManager.Current.ActiveSnapshot!.TextScale);
            Assert.True(toggle.Checked);
        }
        var applied = ThemeManager.Current.ActiveSnapshot;
        toggle.Checked = false;
        host.Services.Settings.SetPreferences(textScale: 2);
        host.ProcessPendingWork();
        Assert.Same(applied, ThemeManager.Current.ActiveSnapshot);
    }

    private static PlatformColorValues HighContrast() => new()
    {
        ContrastPreference = ColorContrastPreference.High, ThemeVariant = PlatformThemeVariant.Dark,
        BackgroundColor = Color.Black, ForegroundColor = Color.White,
        AccentColor1 = Color.Yellow, AccentColor2 = Color.Black
    };

    private static T Find<T>(App app, string name) where T : Control => Descendants(app.Root).OfType<T>().Single(control => control.Name == name);
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class Platform : ISamplePlatformServices
    {
        public string PlatformName => "Headless test";
        public string OperatingSystem => "Test OS";
        public string BackendName => "Test backend";
        public string HostState => "Attached";
        public string AnimationRuntimeStatus => "Test";
        public IPlatformDispatcher Dispatcher => PlatformServiceRegistry.GetRequiredService<IPlatformDispatcher>();
        public bool SupportsPermissionAction => false;
        public Task<PlatformPermissionStatus> CheckSamplePermissionAsync() => Task.FromResult(PlatformPermissionStatus.NotSupported);
        public Task<PlatformPermissionStatus> RequestSamplePermissionAsync() => Task.FromResult(PlatformPermissionStatus.NotSupported);
        public Task<bool> OpenApplicationSettingsAsync() => Task.FromResult(false);
    }
}
