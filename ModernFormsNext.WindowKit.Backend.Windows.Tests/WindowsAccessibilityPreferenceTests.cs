using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsAccessibilityPreferenceTests
{
    [Fact]
    public void NativeWindowsReaderAndOwnedWinRtSubscriptionUseActualApisWithoutChangingSettings()
    {
        if (!OperatingSystem.IsWindows()) return;
        var snapshot = WindowsAccessibilityPreferenceReader.Read();
        Assert.NotNull(snapshot.ColorValues);
        Assert.NotNull(snapshot.ColorValues.BackgroundColor);
        Assert.NotNull(snapshot.ColorValues.ForegroundColor);
        Assert.True(snapshot.TextScale is > 0 && double.IsFinite(snapshot.TextScale.Value));
        // Registration/removal is real; no preference is changed and no change event is fabricated.
        var subscription = WindowsTextScaleSubscription.Create(() => { });
        Assert.NotNull(subscription);
        try { Assert.True(subscription.Read() is > 0); }
        finally { subscription.Dispose(); subscription.Dispose(); }
    }

    [Fact]
    public void DetectionPreservesUnknownAndLegacyFallbackWithoutApplyingAnything()
    {
        var settings = new Win32PlatformSettings(() => new(), _ => null, action => action());
        Assert.Null(settings.GetAccessibilityPreferences().ColorValues);
        Assert.Null(settings.GetAccessibilityPreferences().TextScale);
        Assert.Equal(PlatformThemeVariant.Light, settings.GetColorValues().ThemeVariant);
        Assert.IsAssignableFrom<IPlatformSettings>(settings);
    }

    [Fact]
    public void NativeSubscriptionIsOwnedAndLatePostedCallbackCannotReachExpiredConsumer()
    {
        PlatformAccessibilityPreferences next = new(textScale: 1);
        Action? native = null;
        var queue = new Queue<Action>();
        var lease = new Lease();
        var settings = new Win32PlatformSettings(() => next, callback => { native = callback; return lease; }, queue.Enqueue);
        int observed = 0;
        EventHandler<PlatformAccessibilityPreferences> handler = (_, e) => { Assert.Equal(1.5, e.TextScale); observed++; };
        settings.AccessibilityPreferencesChanged += handler;
        next = new(textScale: 1.5);
        native!();
        Assert.Equal(0, observed);
        queue.Dequeue()();
        Assert.Equal(1, observed);
        native();
        settings.AccessibilityPreferencesChanged -= handler;
        queue.Dequeue()();
        Assert.Equal(1, observed);
        Assert.Equal(1, lease.Disposals);
    }

    [Fact]
    public void QueryCommitsBeforeObserversAndReportsAllFailures()
    {
        PlatformAccessibilityPreferences next = new(textScale: 1);
        var settings = new Win32PlatformSettings(() => next, _ => null, action => action());
        int second = 0;
        settings.AccessibilityPreferencesChanged += (_, e) => { Assert.Same(e, settings.GetAccessibilityPreferences()); throw new InvalidOperationException("observer"); };
        settings.AccessibilityPreferencesChanged += (_, _) => second++;
        next = new(textScale: 2);
        Assert.Throws<AggregateException>(() => settings.GetAccessibilityPreferences());
        Assert.Equal(1, second);
        Assert.Equal(2, settings.GetAccessibilityPreferences().TextScale);
    }

    private sealed class Lease : IDisposable
    {
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
}
