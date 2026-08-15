using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Android;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidAnimationScaleEvaluatorTests
{
    [Theory]
    [InlineData(0f)]
    public void ZeroAnimationScaleRequestsReducedMotion(float animatorScale)
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            animatorScale,
            DateTimeOffset.UnixEpoch);

        Assert.True(snapshot.ReducedMotion);
        Assert.False(snapshot.AnimationsEnabled);
        Assert.False(snapshot.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Ready, snapshot.ProviderState);
        Assert.Equal(0d, snapshot.DurationScale);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void PositiveAnimatorScaleIsPreservedForSharedDurations(float scale)
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            scale,
            DateTimeOffset.UnixEpoch);

        Assert.False(snapshot.ReducedMotion);
        Assert.True(snapshot.AnimationsEnabled);
        Assert.Equal(scale, snapshot.DurationScale);
        Assert.Equal(PlatformAnimationProviderState.Ready, snapshot.ProviderState);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1f)]
    [InlineData(101f)]
    public void MissingOrInvalidScaleUsesSafeFallback(float? animatorScale)
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            animatorScale,
            DateTimeOffset.UnixEpoch);

        Assert.False(snapshot.ReducedMotion);
        Assert.True(snapshot.AnimationsEnabled);
        Assert.True(snapshot.FallbackUsed);
        Assert.Equal(1d, snapshot.DurationScale);
        Assert.Equal(PlatformAnimationProviderState.Fallback, snapshot.ProviderState);
    }

    [Fact]
    public void NonFiniteScaleUsesSafeFallback()
    {
        foreach (float scale in new[] { float.NaN, float.PositiveInfinity })
        {
            PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
                scale,
                DateTimeOffset.UnixEpoch);

            Assert.True(snapshot.FallbackUsed);
            Assert.Equal(PlatformAnimationProviderState.Fallback, snapshot.ProviderState);
        }
    }

    [Fact]
    public void ContextOrPlatformErrorIsPreservedInDiagnostics()
    {
        PlatformAnimationSettingsSnapshot snapshot = AndroidAnimationScaleEvaluator.CreateSnapshot(
            null,
            DateTimeOffset.UnixEpoch,
            "Context unavailable");

        Assert.Equal("Context unavailable", snapshot.LastError);
        Assert.Equal(AndroidAnimationScaleEvaluator.SourceName, snapshot.Source);
    }
}
