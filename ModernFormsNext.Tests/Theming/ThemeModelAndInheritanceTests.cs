using System.Drawing;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ThemeModelAndInheritanceTests
{
    [Fact]
    public void BuiltInThemesResolveWithSemanticTokensAndMetrics()
    {
        using var harness = new ThemeManagerTestHarness();

        ThemeApplyResult result = harness.Manager.Apply(BuiltInThemes.Dark, Immediate());

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(static item => $"{item.Code}:{item.Message}")));
        Assert.Equal(ThemeVariant.Dark, result.Snapshot!.Variant);
        Assert.Equal(BuiltInThemes.BaseThemeId, Assert.Single(result.Snapshot.BaseChain));
        Assert.Equal(Color.FromArgb(255, 40, 40, 40), result.Snapshot.Get(ThemeTokens.Colors.Background));
        Assert.True(result.Snapshot.Spacing.ContainsKey("Medium"));
        Assert.Equal(new Padding(16), result.Snapshot.Get(
            new ThemeToken<Padding>(ThemeTokenCategory.Padding, "Card")));
        Assert.True(result.Snapshot.Counts.Total > 20);
    }

    [Fact]
    public void MultilevelInheritanceOverridesWithoutMutatingBases()
    {
        using var harness = new ThemeManagerTestHarness();
        var root = Theme("sample.root", Color.Red);
        root.Resources["ProductName"] = ThemeResourceValue.FromString("Root");
        var middle = new ThemeDefinition("sample.middle", "Middle") { BaseTheme = root.Id };
        middle.Colors[ThemeTokens.Colors.Background.Name] = Color.Green;
        var leaf = new ThemeDefinition("sample.leaf", "Leaf") { BaseTheme = middle.Id };
        leaf.Resources["ProductName"] = ThemeResourceValue.FromString("Leaf");
        harness.Manager.Register(root);
        harness.Manager.Register(middle);

        ThemeApplyResult result = harness.Manager.Apply(leaf, Immediate());

        Assert.True(result.Success);
        Assert.Equal([root.Id, middle.Id], result.Snapshot!.BaseChain);
        Assert.Equal(Color.Green, result.Snapshot.Get(ThemeTokens.Colors.Background));
        Assert.Equal("Leaf", result.Snapshot.Resources["ProductName"].Value);
        Assert.Equal(Color.Red, root.Colors[ThemeTokens.Colors.Background.Name]);
        Assert.Equal("Root", root.Resources["ProductName"].Value);
    }

    [Fact]
    public void MissingBaseIsRejectedWithoutChangingActiveTheme()
    {
        using var harness = new ThemeManagerTestHarness();
        Assert.True(harness.Manager.Apply(Theme("active.theme", Color.Red), Immediate()).Success);
        var invalid = Theme("invalid.theme", Color.Blue);
        invalid.BaseTheme = "missing.theme";

        ThemeApplyResult result = harness.Manager.Apply(invalid, Immediate());

        Assert.Equal(ThemeApplyStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_BASE_MISSING");
        Assert.Equal("active.theme", harness.Manager.ActiveSnapshot!.Id);
    }

    [Fact]
    public void InheritanceCycleIsRejected()
    {
        using var harness = new ThemeManagerTestHarness();
        var first = new ThemeDefinition("cycle.first", "First") { BaseTheme = "cycle.second" };
        var second = new ThemeDefinition("cycle.second", "Second") { BaseTheme = "cycle.first" };
        harness.Manager.Register(first);
        harness.Manager.Register(second);

        ThemeValidationResult result = harness.Manager.Validate(first);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_INHERITANCE_CYCLE");
    }

    [Fact]
    public void InheritanceDepthLimitIsEnforced()
    {
        using var scheduler = new AnimationSchedulerTestHarness();
        var manager = new ThemeManager(
            scheduler.Scheduler,
            new ImmediateThemeDispatcher(),
            new TestThemeEnvironment(ThemeVariant.Light, false, false),
            new ThemeSecurityLimits { MaximumInheritanceDepth = 2 });
        var root = new ThemeDefinition("depth.root", "Root");
        var middle = new ThemeDefinition("depth.middle", "Middle") { BaseTheme = root.Id };
        var leaf = new ThemeDefinition("depth.leaf", "Leaf") { BaseTheme = middle.Id };
        manager.Register(root);
        manager.Register(middle);

        ThemeValidationResult result = manager.Validate(leaf);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_INHERITANCE_DEPTH");
    }

    [Fact]
    public void IdenticalNamesInDifferentCategoriesRemainIndependent()
    {
        using var harness = new ThemeManagerTestHarness();
        var root = Theme("category.root", Color.Red);
        var leaf = new ThemeDefinition("category.leaf", "Leaf") { BaseTheme = root.Id };
        leaf.Brushes[ThemeTokens.Colors.Background.Name] = new SolidColorBrush(Color.Blue);
        harness.Manager.Register(root);

        ThemeApplyResult result = harness.Manager.Apply(leaf, Immediate());

        Assert.True(result.Success);
        Assert.Equal(Color.Red.ToArgb(), result.Snapshot!.Get(ThemeTokens.Colors.Background).ToArgb());
        var brushToken = new ThemeToken<ModernFormsNext.Drawing.Brush>(ThemeTokenCategory.Brush, ThemeTokens.Colors.Background.Name);
        Assert.Equal(Color.Blue.ToArgb(), Assert.IsType<SolidColorBrush>(result.Snapshot.Get(brushToken)).PaintColor.ToArgb());
    }

    [Fact]
    public void IncompatibleCustomResourceKindOverrideIsRejected()
    {
        using var harness = new ThemeManagerTestHarness();
        var root = Theme("resource.root", Color.Red);
        root.Resources["Value"] = ThemeResourceValue.FromString("text");
        var leaf = new ThemeDefinition("resource.leaf", "Leaf") { BaseTheme = root.Id };
        leaf.Resources["Value"] = ThemeResourceValue.FromNumber(1d);
        harness.Manager.Register(root);

        ThemeValidationResult result = harness.Manager.Validate(leaf);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_INCOMPATIBLE_RESOURCE_OVERRIDE");
    }

    [Fact]
    public void ResolvedSnapshotAndAppliedBrushesAreIsolated()
    {
        using var harness = new ThemeManagerTestHarness();
        var definition = Theme("brush.theme", Color.Red);
        var authored = new SolidColorBrush(Color.Blue);
        definition.Brushes["CardBrush"] = authored;

        ThemeApplyResult result = harness.Manager.Apply(definition, Immediate());
        authored.PaintColor = Color.Green;
        var first = result.Snapshot!.Get(new ThemeToken<ModernFormsNext.Drawing.Brush>(ThemeTokenCategory.Brush, "CardBrush"));
        var second = result.Snapshot.Get(new ThemeToken<ModernFormsNext.Drawing.Brush>(ThemeTokenCategory.Brush, "CardBrush"));
        first.Opacity = 0.1f;

        Assert.NotSame(authored, first);
        Assert.NotSame(first, second);
        Assert.Equal(Color.Blue.ToArgb(), Assert.IsType<SolidColorBrush>(second).PaintColor.ToArgb());
        var applied = Assert.IsType<SolidColorBrush>(harness.Resources[ThemeResourceKeys.Create(ThemeTokenCategory.Brush, "CardBrush")]);
        Assert.NotSame(first, applied);
        Assert.Equal(Color.Blue.ToArgb(), applied.PaintColor.ToArgb());
    }

    [Fact]
    public void SystemVariantUsesProviderAndFallback()
    {
        using var harness = new ThemeManagerTestHarness(systemVariant: ThemeVariant.Dark);
        var theme = Theme("system.theme", Color.Red);
        theme.Variant = ThemeVariant.System;

        ThemeApplyResult dark = harness.Manager.Apply(theme, Immediate());
        harness.Environment.SystemVariant = ThemeVariant.Custom;
        ThemeApplyResult fallback = harness.Manager.Apply(theme, new ThemeApplyOptions
        {
            SystemFallbackVariant = ThemeVariant.Light,
            Transition = new ThemeTransitionOptions { Enabled = false }
        });

        Assert.Equal(ThemeVariant.Dark, dark.Snapshot!.Variant);
        Assert.Equal(ThemeVariant.Light, fallback.Snapshot!.Variant);
    }

    [Fact]
    public void InvalidKeysNumbersAndGradientLimitsProduceDiagnostics()
    {
        using var harness = new ThemeManagerTestHarness();
        var theme = Theme("validation.theme", Color.Red);
        theme.Spacing["Bad Key"] = double.NaN;
        theme.Padding["BadPadding"] = new Padding(-1, 0, 0, 0);
        var gradient = new LinearGradientBrush();
        gradient.GradientStops.Add(new GradientStop(Color.Red, 0f));
        theme.Brushes["Gradient"] = gradient;

        ThemeValidationResult result = harness.Manager.Validate(theme);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_TOKEN_KEY_INVALID");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_NUMBER_INVALID");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_PADDING_INVALID");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_GRADIENT_STOPS_MIN");
    }

    [Fact]
    public void UnsupportedBrushSubclassProducesValidationDiagnosticWithoutCloningIt()
    {
        using var harness = new ThemeManagerTestHarness();
        var theme = Theme("validation.unsupported-brush", Color.Red);
        theme.Brushes["Unsupported"] = new UnsupportedBrush();

        ThemeValidationResult result = harness.Manager.Validate(theme);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "THEME_BRUSH_UNSUPPORTED");
    }

    private static ThemeDefinition Theme(string id, Color background)
    {
        var theme = new ThemeDefinition(id, id) { Variant = ThemeVariant.Custom };
        theme.Colors[ThemeTokens.Colors.Background.Name] = background;
        return theme;
    }

    private sealed class UnsupportedBrush : ModernFormsNext.Drawing.Brush
    {
    }

    private static ThemeApplyOptions Immediate()
        => new() { Transition = new ThemeTransitionOptions { Enabled = false } };
}
