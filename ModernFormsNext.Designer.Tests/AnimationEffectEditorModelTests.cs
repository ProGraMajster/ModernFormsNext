using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class AnimationEffectEditorModelTests
{
    [Fact]
    public void CancelLeavesInteractionEffectPropertiesUntouched()
    {
        var properties = StoredEffects(InteractionEffectDesignerRegistry.RippleTypeName);
        DesignPropertyValue original = properties[InteractionEffectDesignValue.PropertyName];
        var model = CreateEffectModel(properties);

        model.Add(InteractionEffectDesignerRegistry.PressScaleTypeName);

        Assert.Same(original, properties[InteractionEffectDesignValue.PropertyName]);
        Assert.True(InteractionEffectDesignValue.TryRead(original, out var effects, out string? error), error);
        Assert.Single(effects);
    }

    [Fact]
    public void ApplyWritesTemporaryInteractionEffectChangesWithoutClosingModel()
    {
        var properties = new Dictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        var model = CreateEffectModel(properties);

        model.Add(InteractionEffectDesignerRegistry.RippleTypeName);
        model.Apply(properties);
        model.Add(InteractionEffectDesignerRegistry.PressScaleTypeName);

        Assert.True(InteractionEffectDesignValue.TryRead(
            properties[InteractionEffectDesignValue.PropertyName],
            out var applied,
            out string? error), error);
        Assert.Single(applied);
        Assert.Equal(2, model.Entries.Count);
    }

    [Fact]
    public void InteractionEffectReorderPreservesRuntimeOrder()
    {
        var properties = StoredEffects(
            InteractionEffectDesignerRegistry.RippleTypeName,
            InteractionEffectDesignerRegistry.PressScaleTypeName);
        var model = CreateEffectModel(properties);

        Assert.True(model.Move(1, -1));
        model.Apply(properties);

        Assert.True(InteractionEffectDesignValue.TryRead(
            properties[InteractionEffectDesignValue.PropertyName],
            out var reordered,
            out string? error), error);
        Assert.Equal(InteractionEffectDesignerRegistry.PressScaleTypeName, reordered[0].ObjectTypeName);
        Assert.Equal(InteractionEffectDesignerRegistry.RippleTypeName, reordered[1].ObjectTypeName);
    }

    [Fact]
    public void StructuredEffectDescriptorCommitsTypedValueAndElidesDefault()
    {
        DesignerInteractionEffectEntry entry = InteractionEffectDesignerRegistry.Create(
            InteractionEffectDesignerRegistry.RippleTypeName);
        DesignerPropertyDescriptor duration = Assert.Single(
            DesignerInteractionEffectPropertyDescriptors.Create(entry),
            item => item.Name == "DurationMilliseconds");

        Assert.NotNull(duration.NumericMinimum);
        Assert.True(duration.TryCommit("125.5", out string? error), error);
        Assert.Equal(125.5d, entry.Properties["DurationMilliseconds"].Value);
        Assert.True(duration.TryCommit("450", out error), error);
        Assert.DoesNotContain("DurationMilliseconds", entry.Properties.Keys);
    }

    [Fact]
    public void UnknownInteractionEffectRemainsPreservedAcrossApply()
    {
        DesignPropertyValue unknown = DesignPropertyValue.FromStructuredObject(
            "Example.MissingEffect",
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Token"] = DesignPropertyValue.FromString("preserve-me")
            });
        var properties = new Dictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            [InteractionEffectDesignValue.PropertyName] = InteractionEffectDesignValue.Create([unknown])
        };
        var model = CreateEffectModel(properties);

        model.Add(InteractionEffectDesignerRegistry.RippleTypeName);
        model.Apply(properties);

        Assert.True(InteractionEffectDesignValue.TryRead(
            properties[InteractionEffectDesignValue.PropertyName],
            out var effects,
            out string? error), error);
        Assert.Equal("Example.MissingEffect", effects[0].ObjectTypeName);
        Assert.Equal("preserve-me", effects[0].ObjectProperties!["Token"].Value);
    }

    [Fact]
    public void LayoutTransitionStructuredValuesRoundTripAndResetToDefault()
    {
        var model = new DesignerLayoutTransitionEditorModel(
            LayoutTransitionDesignValue.Create(true, 210d, "EaseOut"));

        Assert.True(model.TrySet(false, 125.5d, "CubicInOut", out string? error), error);
        Assert.True(model.TryCreateValue(out DesignPropertyValue? value, out error), error);
        Assert.True(LayoutTransitionDesignValue.TryRead(
            value,
            out bool enabled,
            out double duration,
            out string easing,
            out error), error);
        Assert.False(enabled);
        Assert.Equal(125.5d, duration);
        Assert.Equal("CubicInOut", easing);

        model.Reset();
        Assert.True(model.TryCreateValue(out value, out error), error);
        Assert.Null(value);
        Assert.True(model.Enabled);
        Assert.Equal(250d, model.DurationMilliseconds);
        Assert.Equal("EaseOut", model.Easing);
    }

    [Fact]
    public void VisualStateTransitionModelAddsEditsAndRemovesEntries()
    {
        var model = new DesignerVisualStateTransitionEditorModel(stored: null);

        Assert.True(model.TryAddDefault(out int first, out string? error), error);
        Assert.True(model.TryUpdate(first, "Hover", "Pressed", 80d, "EaseOut", out error), error);
        Assert.True(model.TryAdd("Pressed", "Hover", 120d, "CubicOut", out error), error);
        Assert.True(model.RemoveAt(first));
        Assert.True(model.TryCreateValue(out DesignPropertyValue value, out error), error);
        Assert.True(VisualStateTransitionDesignValue.TryRead(value, out var entries, out error), error);
        DesignVisualStateTransition remaining = Assert.Single(entries);
        Assert.Equal("Pressed", remaining.From);
        Assert.Equal("Hover", remaining.To);
    }

    [Fact]
    public void InvalidTransitionStateAndEasingReturnErrorsWithoutMutation()
    {
        var layout = new DesignerLayoutTransitionEditorModel(stored: null);
        var visual = new DesignerVisualStateTransitionEditorModel(stored: null);

        Assert.False(layout.TrySet(true, 100d, "ApplicationDelegate", out string? layoutError));
        Assert.Contains("not supported", layoutError, StringComparison.OrdinalIgnoreCase);
        Assert.False(visual.TryAdd("Unknown", "Hover", 100d, "Linear", out string? stateError));
        Assert.Contains("supported visual states", stateError, StringComparison.OrdinalIgnoreCase);
        Assert.False(visual.TryAdd("Normal", "Hover", 100d, "ApplicationDelegate", out string? easingError));
        Assert.Contains("not supported", easingError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(visual.Entries);
    }

    [Fact]
    public void VisualStateTransitionRejectsDuplicateAndSameStatePairs()
    {
        var model = new DesignerVisualStateTransitionEditorModel(stored: null);

        Assert.False(model.TryAdd("Hover", "Hover", 100d, "Linear", out string? sameStateError));
        Assert.Contains("different", sameStateError, StringComparison.OrdinalIgnoreCase);
        Assert.True(model.TryAdd("Normal", "Hover", 100d, "Linear", out string? error), error);
        Assert.False(model.TryAdd("Normal", "Hover", 200d, "EaseOut", out string? duplicateError));
        Assert.Contains("already configured", duplicateError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DialogsConstructWithStructuredControlsInsteadOfRawMultilineEditors()
    {
        var session = new DesignerSession();
        var properties = new Dictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        using var effects = new DesignerInteractionEffectCollectionDialog(session, properties);
        using var layout = new DesignerTransitionDialog(session, properties, isLayout: true);
        using var visual = new DesignerTransitionDialog(session, properties, isLayout: false);

        Assert.Contains(effects.Controls.Cast<Control>(), control => control is DesignerPropertyGrid);
        Assert.DoesNotContain(effects.Controls.OfType<TextBox>(), editor => editor.MultiLine);
        Assert.Contains(layout.Controls.OfType<CheckBox>(), checkBox => checkBox.Text.Contains("Animate", StringComparison.Ordinal));
        Assert.Single(layout.Controls.OfType<NumericUpDown>());
        DataGridView grid = Assert.Single(visual.Controls.OfType<DataGridView>());
        Assert.Equal(4, grid.Columns.Count);
        Assert.Single(visual.Controls.OfType<NumericUpDown>());
    }

    private static DesignerInteractionEffectCollectionEditorModel CreateEffectModel(
        IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        properties.TryGetValue(InteractionEffectDesignValue.PropertyName, out DesignPropertyValue? stored);
        return new DesignerInteractionEffectCollectionEditorModel(
            stored,
            BuiltInAnimationDefinitionCatalog.Definitions);
    }

    private static Dictionary<string, DesignPropertyValue> StoredEffects(params string[] typeNames)
    {
        DesignPropertyValue[] effects = typeNames
            .Select(typeName => DesignPropertyValue.FromStructuredObject(
                typeName,
                new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)))
            .ToArray();
        return new Dictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            [InteractionEffectDesignValue.PropertyName] = InteractionEffectDesignValue.Create(effects)
        };
    }
}
