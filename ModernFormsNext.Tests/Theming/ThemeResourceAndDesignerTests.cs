using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ThemeResourceAndDesignerTests
{
    [Fact]
    public void ResourcePrecedenceIsControlThenApplicationThenTheme()
    {
        string tokenName = "Precedence" + Guid.NewGuid().ToString("N");
        string key = ThemeResourceKeys.Create(ThemeTokenCategory.Color, tokenName);
        Dictionary<object, object?> originalThemeResources = Application.ThemeResourcesInternal.GetSnapshot();
        using var harness = new ThemeManagerTestHarness(resources: Application.ThemeResourcesInternal);
        using var control = new ColorProbeControl();
        var theme = new ThemeDefinition("resources.precedence", "Precedence");
        theme.Colors[tokenName] = Color.Red;

        try
        {
            Assert.True(harness.Manager.Apply(theme, Immediate()).Success);
            control.SetResourceReference(nameof(ColorProbeControl.Value), key);
            Assert.Equal(Color.Red.ToArgb(), control.Value.ToArgb());

            Application.Resources[key] = Color.Blue;
            Assert.Equal(Color.Blue.ToArgb(), control.Value.ToArgb());

            control.Resources[key] = Color.Green;
            Assert.Equal(Color.Green.ToArgb(), control.Value.ToArgb());

            Assert.True(control.Resources.Remove(key));
            Assert.Equal(Color.Blue.ToArgb(), control.Value.ToArgb());
            Assert.True(Application.Resources.Remove(key));
            Assert.Equal(Color.Red.ToArgb(), control.Value.ToArgb());
        }
        finally
        {
            Application.Resources.Remove(key);
            RestoreThemeResources(originalThemeResources);
        }
    }

    [Fact]
    public void SwitchingThemeRefreshesDynamicPropertyOnceWithoutRecreatingControl()
    {
        string tokenName = "Refresh" + Guid.NewGuid().ToString("N");
        string key = ThemeResourceKeys.Create(ThemeTokenCategory.Color, tokenName);
        Dictionary<object, object?> originalThemeResources = Application.ThemeResourcesInternal.GetSnapshot();
        using var harness = new ThemeManagerTestHarness(resources: Application.ThemeResourcesInternal);
        using var control = new ColorProbeControl();
        var weakIdentity = new WeakReference<Control>(control);

        try
        {
            Assert.True(harness.Manager.Apply(ColorTheme("resources.first", tokenName, Color.Red), Immediate()).Success);
            control.SetResourceReference(nameof(ColorProbeControl.Value), key);
            control.Reset();

            Assert.True(harness.Manager.Apply(ColorTheme("resources.second", tokenName, Color.Blue), Immediate()).Success);

            Assert.Equal(Color.Blue.ToArgb(), control.Value.ToArgb());
            Assert.Equal(1, control.SetterCalls);
            Assert.True(weakIdentity.TryGetTarget(out Control? identity));
            Assert.Same(control, identity);
        }
        finally
        {
            RestoreThemeResources(originalThemeResources);
        }
    }

    [Fact]
    public void ApplyingThemeDoesNotOverwriteApplicationResources()
    {
        string unrelatedKey = "ThemeResourceTests.Unrelated." + Guid.NewGuid().ToString("N");
        Application.Resources[unrelatedKey] = "application-value";
        using var harness = new ThemeManagerTestHarness();

        try
        {
            Assert.True(harness.Manager.Apply(ColorTheme("resources.isolated", "Custom", Color.Red), Immediate()).Success);

            Assert.Equal("application-value", Application.Resources[unrelatedKey]);
            Assert.False(harness.Resources.ContainsKey(unrelatedKey));
        }
        finally
        {
            Application.Resources.Remove(unrelatedKey);
        }
    }

    [Fact]
    public void AppliedBrushMutationStillInvalidatesDynamicConsumer()
    {
        string tokenName = "Brush" + Guid.NewGuid().ToString("N");
        string key = ThemeResourceKeys.Create(ThemeTokenCategory.Brush, tokenName);
        Dictionary<object, object?> originalThemeResources = Application.ThemeResourcesInternal.GetSnapshot();
        using var harness = new ThemeManagerTestHarness(resources: Application.ThemeResourcesInternal);
        using var control = new InvalidationProbeControl();
        using var surface = new SkiaControlSurface(control);
        var first = BrushTheme("brush.first", tokenName, Color.Red);
        var second = BrushTheme("brush.second", tokenName, Color.Blue);

        try
        {
            harness.Manager.Apply(first, Immediate());
            control.SetResourceReference(nameof(Control.BackgroundBrush), key);
            control.Reset();

            harness.Manager.Apply(second, Immediate());
            var applied = Assert.IsType<SolidColorBrush>(control.BackgroundBrush);
            Assert.NotSame(second.Brushes[tokenName], applied);
            control.Reset();
            applied.PaintColor = Color.Green;

            Assert.Equal(1, control.Invalidations);
        }
        finally
        {
            RestoreThemeResources(originalThemeResources);
        }
    }

    [Fact]
    public void ComplexAuthoringCollectionsAreHiddenFromDesignerPropertyGrid()
    {
        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(ThemeDefinition));
        string[] complex =
        [
            nameof(ThemeDefinition.Metadata), nameof(ThemeDefinition.Tags), nameof(ThemeDefinition.Colors),
            nameof(ThemeDefinition.Brushes), nameof(ThemeDefinition.Typography), nameof(ThemeDefinition.Spacing),
            nameof(ThemeDefinition.Padding), nameof(ThemeDefinition.Sizing), nameof(ThemeDefinition.Corners), nameof(ThemeDefinition.BorderThickness),
            nameof(ThemeDefinition.Animations), nameof(ThemeDefinition.Resources)
        ];

        foreach (string propertyName in complex)
        {
            PropertyDescriptor property = Assert.IsAssignableFrom<PropertyDescriptor>(properties[propertyName]);
            Assert.False(property.IsBrowsable);
            var visibility = Assert.IsType<DesignerSerializationVisibilityAttribute>(
                property.Attributes[typeof(DesignerSerializationVisibilityAttribute)]);
            Assert.Equal(DesignerSerializationVisibility.Hidden, visibility.Visibility);
        }
    }

    [Fact]
    public void DesignerEnvironmentUsesExplicitSystemFallbackAndNeverStartsScheduler()
    {
        using var harness = new ThemeManagerTestHarness(
            systemVariant: ThemeVariant.Dark,
            reducedMotion: false,
            designMode: true);
        var theme = ColorTheme("designer.system", "Custom", Color.Red);
        theme.Variant = ThemeVariant.System;

        ThemeApplyResult result = harness.Manager.Apply(theme, Animated());

        Assert.True(result.Success);
        Assert.Equal(ThemeVariant.Dark, result.Snapshot!.Variant);
        Assert.Null(result.Transition);
        Assert.Equal(0, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.SchedulerHarness.TickSource.IsRunning);
    }

    private static ThemeDefinition ColorTheme(string id, string tokenName, Color color)
    {
        var theme = new ThemeDefinition(id, id);
        theme.Colors[tokenName] = color;
        return theme;
    }

    private static ThemeDefinition BrushTheme(string id, string tokenName, Color color)
    {
        var theme = new ThemeDefinition(id, id);
        theme.Brushes[tokenName] = new SolidColorBrush(color);
        return theme;
    }

    private static ThemeApplyOptions Immediate()
        => new() { Transition = new ThemeTransitionOptions { Enabled = false } };

    private static ThemeApplyOptions Animated()
        => new()
        {
            Transition = new ThemeTransitionOptions
            {
                Enabled = true,
                Duration = TimeSpan.FromMilliseconds(100)
            }
        };

    private static void RestoreThemeResources(Dictionary<object, object?> original)
    {
        ResourceDictionaryChange[] changes = Application.ThemeResourcesInternal.ReplaceSnapshot(original);
        Application.ThemeResourcesInternal.PublishChanges(changes);
    }

    private sealed class ColorProbeControl : Control
    {
        private Color value;

        public Color Value
        {
            get => value;
            set
            {
                this.value = value;
                SetterCalls++;
            }
        }

        public int SetterCalls { get; private set; }

        public void Reset() => SetterCalls = 0;
    }

    private sealed class InvalidationProbeControl : Control
    {
        public int Invalidations { get; private set; }

        public void Reset() => Invalidations = 0;

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            Invalidations++;
            base.OnInvalidated(e);
        }
    }
}
