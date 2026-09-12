using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidAccessibilityPreferenceTests
{
    [Theory]
    [InlineData(-1f, ColorContrastPreference.NoPreference)]
    [InlineData(0f, ColorContrastPreference.NoPreference)]
    [InlineData(.5f, ColorContrastPreference.High)]
    [InlineData(1f, ColorContrastPreference.High)]
    public void ActualSignalsNormalizeIndependentlyFromDisplayDensity(float contrast, ColorContrastPreference expected)
    {
        var snapshot = AndroidAccessibilityPreferences.Create(true, 1.3f, contrast, true);
        Assert.Equal((double)1.3f, snapshot.TextScale);
        Assert.Equal(expected, snapshot.ColorValues!.ContrastPreference);
        Assert.Equal(PlatformThemeVariant.Dark, snapshot.ColorValues.ThemeVariant);
        Assert.Null(snapshot.ColorValues.BackgroundColor);
        Assert.Null(snapshot.ColorValues.ForegroundColor);
    }

    [Fact]
    public void LegacyApiNoHostAndInvalidSignalsRemainUnknown()
    {
        Assert.Null(AndroidAccessibilityPreferences.Create(true, 1.5f, null, false).ColorValues);
        Assert.Equal(1.5, AndroidAccessibilityPreferences.Create(true, 1.5f, null, false).TextScale);
        Assert.Equal(new PlatformAccessibilityPreferences(), AndroidAccessibilityPreferences.Create(false, 2, 1, true));
        var invalid = AndroidAccessibilityPreferences.Create(true, float.NaN, 2, false);
        Assert.Null(invalid.TextScale);
        Assert.Null(invalid.ColorValues);
    }
}
