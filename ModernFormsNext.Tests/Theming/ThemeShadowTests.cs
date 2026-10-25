using System.Drawing;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ThemeShadowTests
{
    [Fact]
    public void JsonRoundTripPreservesShadowTokensAndCustomResources()
    {
        var theme = new ThemeDefinition("shadow-roundtrip", "Shadow roundtrip");
        var shadow = new Shadow(Color.FromArgb(96, 20, 40, 60), 5, -2, 3, 1);
        theme.Set(ThemeTokens.Shadows.Medium, shadow);
        theme.Resources["CardShadow"] = ThemeResourceValue.FromShadow(shadow);
        var serializer = new ThemeJsonSerializer();
        string json = serializer.Serialize(theme);
        var restored = serializer.Deserialize(json);
        Assert.Equal(shadow, restored.Shadows["Medium"]);
        Assert.Equal(ThemeResourceKind.Shadow, restored.Resources["CardShadow"].Kind);
        Assert.Equal(shadow, restored.Resources["CardShadow"].Value);
        Assert.Equal(json, serializer.Serialize(restored));
        Assert.Same(shadow, theme.Clone().Shadows["Medium"]);
    }

    [Fact]
    public void ResolutionCountsAndPublishesInheritedShadowsThroughCanonicalResources()
    {
        using var harness = new ThemeManagerTestHarness();
        var result = harness.Manager.Apply(BuiltInThemes.Light);
        Assert.True(result.Success);
        var snapshot = result.Snapshot!;
        Assert.Equal(3, snapshot.Counts.Shadows);
        Assert.Equal(3, snapshot.Shadows.Count);
        Assert.Equal(snapshot.Get(ThemeTokens.Shadows.Medium), harness.Resources[ThemeTokens.Shadows.Medium.ResourceKey]);
        using var control = new Panel();
        control.Resources["Card"] = snapshot.Get(ThemeTokens.Shadows.Medium);
        control.SetResourceReference(nameof(Control.Shadow), "Card");
        Assert.Equal(snapshot.Get(ThemeTokens.Shadows.Medium), control.Shadow);
    }

    [Fact]
    public void NullShadowRejectsApplyWithoutChangingExistingState()
    {
        using var harness = new ThemeManagerTestHarness();
        Assert.True(harness.Manager.Apply(BuiltInThemes.Light).Success);
        var before = harness.Manager.ActiveSnapshot;
        var invalid = new ThemeDefinition("invalid-shadow", "Invalid shadow");
        invalid.Shadows["Card"] = null!;
        var result = harness.Manager.Apply(invalid);
        Assert.False(result.Success);
        Assert.Same(before, harness.Manager.ActiveSnapshot);
        Assert.Contains(result.Diagnostics, item => item.Code == "THEME_SHADOW_NULL");
    }

    [Theory]
    [InlineData("blurRadius", "-1")]
    [InlineData("blurRadius", "129")]
    [InlineData("offsetX", "2049")]
    [InlineData("spread", "129")]
    [InlineData("unknown", "1")]
    public void ShadowJsonRejectsOutOfRangeOrUnknownMembers(string property, string number)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1, id = "invalid-shadow", name = "Invalid",
            shadows = new Dictionary<string, object>
            {
                ["Card"] = new Dictionary<string, object>
                {
                    ["color"] = "#FF000000",
                    [property] = double.Parse(number, System.Globalization.CultureInfo.InvariantCulture)
                }
            }
        });
        Assert.Throws<ThemeSerializationException>(() => new ThemeJsonSerializer().Deserialize(json));
    }

    [Fact]
    public void BodyTypographyMetricsReachLegacyStoreAndSerializedNumberResourcesReachControls()
    {
        using var harness = new ThemeManagerTestHarness();
        var theme = new ThemeDefinition("text-metrics", "Text metrics");
        theme.Typography["Body"] = new ThemeTypography("sans-serif", 14, lineHeight: 1.5f, letterSpacing: 1.25f);
        theme.Resources["Tracking"] = ThemeResourceValue.FromNumber(0.75);
        var restored = new ThemeJsonSerializer().Deserialize(new ThemeJsonSerializer().Serialize(theme));
        var result = harness.Manager.Apply(restored);
        Assert.True(result.Success);
        Assert.Equal(1.5d, harness.LegacyStore.GetSnapshot()[nameof(Theme.LineHeight)]);
        Assert.Equal(1.25d, harness.LegacyStore.GetSnapshot()[nameof(Theme.LetterSpacing)]);
        using var control = new Label();
        control.Resources["Tracking"] = restored.Resources["Tracking"].Value;
        control.SetResourceReference(nameof(Control.LetterSpacing), "Tracking");
        Assert.Equal(0.75d, control.LetterSpacing);
    }
}
