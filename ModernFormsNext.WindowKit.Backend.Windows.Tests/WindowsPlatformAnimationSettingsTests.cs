using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Windows;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsPlatformAnimationSettingsTests
{
    [Fact]
    public void ConstructorReadsNativePreferenceAtStartup()
    {
        var reader = new TestReader(new WindowsAnimationPreferenceReadResult(true, false, null));

        var provider = CreateProvider(reader);

        Assert.Equal(1, reader.ReadCount);
        Assert.True(provider.Current.ReducedMotion);
        Assert.False(provider.Current.AnimationsEnabled);
        Assert.Equal(PlatformAnimationProviderState.Ready, provider.Current.ProviderState);
        Assert.False(provider.Current.FallbackUsed);
    }

    [Fact]
    public void SystemSettingsChangePublishesMeaningfulLiveUpdate()
    {
        var reader = new TestReader(
            new(true, true, null),
            new(true, false, null));
        var provider = CreateProvider(reader);
        PlatformAnimationSettingsChangedEventArgs? observed = null;
        provider.Changed += (_, args) => observed = args;

        provider.NotifySystemSettingsChanged();

        Assert.NotNull(observed);
        Assert.True(observed.Current.ReducedMotion);
        Assert.False(observed.Current.AnimationsEnabled);
        Assert.Equal(2, reader.ReadCount);
    }

    [Fact]
    public void EquivalentRefreshDoesNotPublishDuplicateChange()
    {
        var reader = new TestReader(
            new(true, true, null),
            new(true, true, null));
        var provider = CreateProvider(reader);
        int changes = 0;
        provider.Changed += (_, _) => changes++;

        provider.Refresh();

        Assert.Equal(0, changes);
        Assert.Equal(2, reader.ReadCount);
    }

    [Fact]
    public void CallbackRunsOutsideProviderLock()
    {
        var reader = new TestReader(
            new(true, true, null),
            new(true, false, null));
        var provider = CreateProvider(reader);
        bool? lockHeld = null;
        provider.Changed += (_, _) => lockHeld = provider.IsLockHeldByCurrentThread;

        provider.Refresh();

        Assert.False(lockHeld);
    }

    [Fact]
    public void FailedNativeReadUsesSafeEnabledFallback()
    {
        var reader = new TestReader(new WindowsAnimationPreferenceReadResult(false, true, "SPI failed"));

        var provider = CreateProvider(reader);

        Assert.False(provider.Current.ReducedMotion);
        Assert.True(provider.Current.AnimationsEnabled);
        Assert.True(provider.Current.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Fallback, provider.Current.ProviderState);
        Assert.Equal("SPI failed", provider.Current.LastError);
    }

    [Fact]
    public void ReaderExceptionCannotCrashProvider()
    {
        var provider = CreateProvider(new ThrowingReader());

        Assert.Equal(PlatformAnimationProviderState.Fallback, provider.Current.ProviderState);
        Assert.Contains("Native read failed", provider.Current.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void EventAccessorsTrackSubscriptionsWithoutDuplication()
    {
        var provider = CreateProvider(new TestReader(
            new WindowsAnimationPreferenceReadResult(true, true, null)));
        EventHandler<PlatformAnimationSettingsChangedEventArgs> handler = (_, _) => { };

        provider.Changed += handler;
        Assert.Equal(1, provider.SubscriberCount);
        provider.Changed -= handler;

        Assert.Equal(0, provider.SubscriberCount);
    }

    private static WindowsPlatformAnimationSettings CreateProvider(IWindowsAnimationPreferenceReader reader)
        => new(reader, static () => DateTimeOffset.UnixEpoch);

    private sealed class TestReader(params WindowsAnimationPreferenceReadResult[] results)
        : IWindowsAnimationPreferenceReader
    {
        private readonly Queue<WindowsAnimationPreferenceReadResult> results = new(results);

        public int ReadCount { get; private set; }

        public WindowsAnimationPreferenceReadResult Read()
        {
            ReadCount++;
            return results.Count > 1 ? results.Dequeue() : results.Peek();
        }
    }

    private sealed class ThrowingReader : IWindowsAnimationPreferenceReader
    {
        public WindowsAnimationPreferenceReadResult Read()
            => throw new InvalidOperationException("Native read failed");
    }
}
