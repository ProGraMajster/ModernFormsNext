using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ThemeTextScaleTests
{
    [Fact]
    public void InheritedAuthoredTypographyScalesOnceWithoutMutatingDefinitionOrResources()
    {
        using var harness = new ThemeManagerTestHarness();
        var basis = new ThemeDefinition("scale.base", "Base");
        basis.Typography["Body"] = new ThemeTypography("Segoe UI", 12, FontStyle.Bold, 1.3f, 2);
        basis.Typography["Caption"] = new ThemeTypography("Segoe UI", 10);
        harness.Manager.Register(basis);
        var theme = new ThemeDefinition("scale.child", "Child") { BaseTheme = basis.Id };
        theme.Typography["Caption"] = new ThemeTypography("Segoe UI", 11);
        var explicitResource = new ThemeTypography("Segoe UI", 7);
        theme.Resources["ExplicitFont"] = ThemeResourceValue.FromTypography(explicitResource);
        var options = new ThemeApplyOptions { TextScale = 1.5 };

        for (int i = 0; i < 3; i++)
        {
            var result = harness.Manager.Apply(theme, options);
            Assert.True(result.Success);
            Assert.Equal(1.5, result.Snapshot!.TextScale);
            Assert.Equal(18, result.Snapshot.Typography["Body"].Size);
            Assert.Equal(16.5f, result.Snapshot.Typography["Caption"].Size);
            Assert.Equal(1.3f, result.Snapshot.Typography["Body"].LineHeight);
            Assert.Equal(2, result.Snapshot.Typography["Body"].LetterSpacing);
            Assert.Equal(7, ((ThemeTypography)result.Snapshot.Resources["ExplicitFont"].Value).Size);
        }
        Assert.Equal(12, basis.Typography["Body"].Size);
        Assert.Equal(11, theme.Typography["Caption"].Size);
        Assert.Equal(11, harness.Manager.ActiveTheme!.Typography["Caption"].Size);
        Assert.True(harness.Manager.Apply(theme).Success);
        Assert.Equal(1, harness.Manager.ActiveSnapshot!.TextScale);
        Assert.Equal(12, harness.Manager.ActiveSnapshot.Typography["Body"].Size);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidFactorsAreRejected(double scale)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ThemeApplyOptions { TextScale = scale });

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    [InlineData(1e10)]
    public void UnrepresentableProductsDoNotCommitAnyState(double scale)
    {
        using var harness = new ThemeManagerTestHarness();
        Assert.True(harness.Manager.Apply(BuiltInThemes.Dark, new ThemeApplyOptions { TextScale = 1.5 }).Success);
        var before = harness.Manager.ActiveSnapshot;
        var resources = harness.Resources.ToArray();
        var legacy = harness.LegacyStore.GetSnapshot();
        var result = harness.Manager.Apply(BuiltInThemes.Light, new ThemeApplyOptions { TextScale = scale });
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "THEME_TEXT_SCALE_RANGE");
        Assert.Same(before, harness.Manager.ActiveSnapshot);
        Assert.Equal(resources, harness.Resources.ToArray());
        Assert.Equal(legacy, harness.LegacyStore.GetSnapshot());
    }
}
