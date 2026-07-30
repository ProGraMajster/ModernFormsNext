using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Android;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidAnimationScaleEvaluatorTests
{
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(0f, 0f)]
    public void ZeroAnimationScaleRequestsReducedMotion(float animatorScale, float transitionScale)
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            animatorScale,
            transitionScale,
            DateTimeOffset.UnixEpoch);

        Assert.True(snapshot.ReducedMotion);
        Assert.False(snapshot.AnimationsEnabled);
        Assert.False(snapshot.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Ready, snapshot.ProviderState);
    }

    [Fact]
    public void PositiveAnimationScalesKeepAnimationsEnabled()
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            0.5f,
            2f,
            DateTimeOffset.UnixEpoch);

        Assert.False(snapshot.ReducedMotion);
        Assert.True(snapshot.AnimationsEnabled);
        Assert.Equal(PlatformAnimationProviderState.Ready, snapshot.ProviderState);
    }

    [Theory]
    [InlineData(null, 1f)]
    [InlineData(1f, null)]
    [InlineData(-1f, 1f)]
    [InlineData(1f, -1f)]
    public void MissingOrInvalidScaleUsesSafeFallback(float? animatorScale, float? transitionScale)
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            animatorScale,
            transitionScale,
            DateTimeOffset.UnixEpoch);

        Assert.False(snapshot.ReducedMotion);
        Assert.True(snapshot.AnimationsEnabled);
        Assert.True(snapshot.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Fallback, snapshot.ProviderState);
    }

    [Fact]
    public void NonFiniteScaleUsesSafeFallback()
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            float.NaN,
            float.PositiveInfinity,
            DateTimeOffset.UnixEpoch);

        Assert.True(snapshot.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Fallback, snapshot.ProviderState);
    }

    [Fact]
    public void ContextOrPlatformErrorIsPreservedInDiagnostics()
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            null,
            null,
            DateTimeOffset.UnixEpoch,
            "Context unavailable");

        Assert.Equal("Context unavailable", snapshot.LastError);
        Assert.Equal(AndroidAnimationScaleEvaluator.SourceName, snapshot.Source);
    }
}
