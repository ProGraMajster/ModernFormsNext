using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidAppearanceModeTests
{
    [Theory]
    [InlineData(null, PlatformColorScheme.Unknown)]
    [InlineData(0x03, PlatformColorScheme.Unknown)]
    [InlineData(0x13, PlatformColorScheme.Light)]
    [InlineData(0x23, PlatformColorScheme.Dark)]
    [InlineData(0x33, PlatformColorScheme.Unknown)]
    public void OnlyDefinedNightFlagsAffectTheScheme(int? uiMode, PlatformColorScheme expected)
        => Assert.Equal(expected, AndroidAppearanceMode.GetColorScheme(uiMode));
}
