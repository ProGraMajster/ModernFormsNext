using System.Drawing;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class AccessibilityPreferenceTests
{
    [Fact]
    public void HostRestoresBorrowedThemeWithItsOriginalAppliedMultiplier()
    {
        var manager = ThemeManager.Current;
        var previous = manager.ActiveTheme ?? BuiltInThemes.Light;
        double previousScale = manager.ActiveSnapshot?.TextScale ?? 1d;
        try
        {
            // The application theme survives its dispatcher scope. Retire the setup runtime
            // before opening the test host: deterministic dispatchers cannot be nested.
            WithThemeRuntime(() => Assert.True(manager.Apply(BuiltInThemes.Dark, new ThemeApplyOptions { TextScale = 1.5 }).Success));
            var outerBody = manager.ActiveSnapshot!.Get(ThemeTokens.Typography.Body);
            using (var host = ModernFormsTestHost.Create())
                Assert.True(manager.Apply(BuiltInThemes.Light, new ThemeApplyOptions { TextScale = 2 }).Success);
            Assert.Equal(1.5, manager.ActiveSnapshot!.TextScale);
            Assert.Equal(BuiltInThemes.DarkThemeId, manager.ActiveSnapshot.Id);
            Assert.Equal(outerBody, manager.ActiveSnapshot.Get(ThemeTokens.Typography.Body));
            Assert.Equal(21, Theme.FontSize);
        }
        finally
        {
            WithThemeRuntime(() => Assert.True(manager.Apply(previous, new ThemeApplyOptions { TextScale = previousScale }).Success));
        }
    }

    private static void WithThemeRuntime(Action action)
    {
        var dispatcher = new UiTestDispatcher();
        try
        {
            var services = new TestPlatformServices(dispatcher);
            try
            {
                var clock = new TestClock(dispatcher);
                try { action(); }
                finally { clock.Dispose(); }
            }
            finally { services.Dispose(); }
        }
        finally { dispatcher.Dispose(); }
    }

    [Fact]
    public void ScopedSettingsCopyValuesNotifyAfterCommitAndRestoreOnFailure()
    {
        var previous = AvaloniaGlobals.GetService<IPlatformSettings>();
        TestPlatformSettings? retained = null;
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var host = ModernFormsTestHost.Create();
            retained = host.Services.Settings;
            Assert.Same(retained, AvaloniaGlobals.GetRequiredService<IPlatformSettings>());
            Assert.Null(retained.GetAccessibilityPreferences().TextScale);
            Assert.Null(retained.GetAccessibilityPreferences().ColorValues);
            int observed = 0;
            retained.AccessibilityPreferencesChanged += (_, e) =>
            {
                Assert.Same(e, retained.GetAccessibilityPreferences());
                observed++;
            };
            var colors = new PlatformColorValues { ContrastPreference = ColorContrastPreference.High, BackgroundColor = Color.Black, ForegroundColor = Color.White };
            retained.SetPreferences(colors, 1.5);
            retained.SetPreferences(colors, 1.5);
            Assert.Equal(1, observed);
            Assert.NotSame(colors, retained.GetAccessibilityPreferences().ColorValues);
            Assert.Equal(colors, retained.GetAccessibilityPreferences().ColorValues);
            Assert.False(new DefaultPlatformSettings() is IPlatformAccessibilitySettings);
            throw new InvalidOperationException("test body");
        }));
        Assert.Same(previous, AvaloniaGlobals.GetService<IPlatformSettings>());
        Assert.Throws<ObjectDisposedException>(() => retained!.GetAccessibilityPreferences());
        Assert.Throws<ObjectDisposedException>(() => retained!.SetPreferences(textScale: 2));
    }

    [Fact]
    public void PreferenceObserverFailureDoesNotSkipOtherConsumersOrChangeTheme()
    {
        using var host = ModernFormsTestHost.Create();
        var before = ThemeManager.Current.ActiveSnapshot;
        int observed = 0;
        host.Services.Settings.AccessibilityPreferencesChanged += (_, _) => throw new InvalidOperationException("observer");
        host.Services.Settings.AccessibilityPreferencesChanged += (_, _) => observed++;
        Assert.Throws<AggregateException>(() => host.Services.Settings.SetPreferences(textScale: 2));
        Assert.Equal(1, observed);
        Assert.Equal(2, host.Services.Settings.GetAccessibilityPreferences().TextScale);
        Assert.Same(before, ThemeManager.Current.ActiveSnapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrepaintThemeScalingChangesActualGeometryWithoutEditingComposition(bool rich)
    {
        using var host = ModernFormsTestHost.Create();
        TextBox editor = rich ? new RichTextBox() : new TextBox();
        editor.Text = "abc";
        editor.Width = 250;
        editor.Height = 80;
        var explicitFont = new ModernFormsNext.Font("Segoe UI", 9);
        var authored = new Label { Text = "authored", Font = explicitFont, Top = 100, Width = 200 };
        var root = new Panel();
        root.Controls.Add(editor);
        root.Controls.Add(authored);
        host.Show(root);
        host.Input.Focus(editor);
        var client = host.Input.TextInputClient!;
        Assert.True(client.SetSelection(1, 2));
        Assert.True(client.SetComposingText("日本"));
        var before = client.GetState()!;
        var textRange = editor.AccessibilityObject.TextProvider!.RangeFromOffsets(0, 1);
        var bounds = Assert.Single(textRange.GetBoundingRectangles());

        var result = ThemeManager.Current.Apply(BuiltInThemes.Light, new ThemeApplyOptions { TextScale = 2 });

        Assert.True(result.Success);
        var after = client.GetState()!;
        Assert.Equal(before.Text, after.Text);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.SelectionStart, after.SelectionStart);
        Assert.Equal(before.SelectionEnd, after.SelectionEnd);
        Assert.Equal(before.CompositionStart, after.CompositionStart);
        Assert.Equal(before.CompositionEnd, after.CompositionEnd);
        Assert.True(Assert.Single(textRange.GetBoundingRectangles()).Height > bounds.Height);
        Assert.Equal(explicitFont, authored.Font);
        Assert.True(client.CancelComposition());
        Assert.Equal("abc", editor.Text);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void SnapshotRejectsInvalidScale(double scale)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformAccessibilityPreferences(textScale: scale));
}
