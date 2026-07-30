using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Animations;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class InteractionEffectDesignerTests
{
    [Fact]
    public void RegistryOffersOnlyBuiltInSafeEffectTypes()
    {
        Assert.Equal(
            [
                InteractionEffectDesignerRegistry.RippleTypeName,
                InteractionEffectDesignerRegistry.PressScaleTypeName
            ],
            InteractionEffectDesignerRegistry.SupportedTypeNames);
    }

    [Fact]
    public void AddRemoveAndOrderProduceDeterministicCollectionValue()
    {
        var entries = new List<DesignerInteractionEffectEntry>
        {
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName),
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.PressScaleTypeName)
        };

        DesignPropertyValue value = InteractionEffectDesignerRegistry.WriteCollection(entries);
        Assert.True(InteractionEffectDesignerRegistry.TryReadCollection(value, out var restored, out var error), error);
        Assert.Equal(InteractionEffectDesignerRegistry.RippleTypeName, restored[0].TypeName);
        Assert.Equal(InteractionEffectDesignerRegistry.PressScaleTypeName, restored[1].TypeName);

        entries.RemoveAt(0);
        value = InteractionEffectDesignerRegistry.WriteCollection(entries);
        Assert.True(InteractionEffectDesignValue.TryRead(value, out var remaining, out error), error);
        Assert.Single(remaining);
        Assert.Equal(InteractionEffectDesignerRegistry.PressScaleTypeName, remaining[0].ObjectTypeName);
    }

    [Fact]
    public void PropertyMutationGeneratesStableRuntimeInitializer()
    {
        DesignerInteractionEffectEntry ripple =
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName);
        string edited = InteractionEffectDesignerRegistry.FormatEditorText(ripple)
            .Replace("DurationMilliseconds=450", "DurationMilliseconds=275", StringComparison.Ordinal)
            .Replace("OverflowPolicy=RemoveOldest", "OverflowPolicy=IgnoreNew", StringComparison.Ordinal)
            .Replace("ColorArgb=#5AFFFFFF", "ColorArgb=#7F102030", StringComparison.Ordinal);
        Assert.True(InteractionEffectDesignerRegistry.TryApplyEditorText(ripple, edited, out var error), error);
        DesignDocument document = CreateDocument(ripple);

        var generated = new CSharpDesignerGenerator().Generate(document);

        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("Duration = System.TimeSpan.FromMilliseconds(275)", generated.Code, StringComparison.Ordinal);
        Assert.Contains("Color = System.Drawing.Color.FromArgb(127, 16, 32, 48)", generated.Code, StringComparison.Ordinal);
        Assert.Contains("RippleOverflowPolicy.IgnoreNew", generated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(generated.Code).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void GeneratedCodeRoundTripPreservesOrderAndDoesNotDuplicateEffects()
    {
        DesignerInteractionEffectEntry ripple =
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName);
        DesignerInteractionEffectEntry press =
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.PressScaleTypeName);
        DesignDocument document = CreateDocument(ripple, press);
        var generator = new CSharpDesignerGenerator();

        string firstCode = generator.Generate(document).Code;
        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(firstCode);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        DesignDocument reopened = Assert.IsType<DesignDocument>(parsed.Document);
        DesignPropertyValue value = Assert.Single(reopened.Controls).Properties[InteractionEffectDesignValue.PropertyName];
        Assert.True(InteractionEffectDesignValue.TryRead(value, out var effects, out var error), error);
        Assert.Equal(2, effects.Count);
        Assert.EndsWith("RippleEffect", effects[0].ObjectTypeName, StringComparison.Ordinal);
        Assert.EndsWith("PressScaleEffect", effects[1].ObjectTypeName, StringComparison.Ordinal);

        string secondCode = generator.Generate(reopened).Code;
        Assert.Equal(firstCode, secondCode);
        Assert.Equal(2, CountOccurrences(secondCode, ".InteractionEffects.Add("));
    }

    [Fact]
    public void MfdesignSaveReloadAndCloneKeepOneOrderedCollection()
    {
        DesignDocument document = CreateDocument(
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.PressScaleTypeName),
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName));
        string json = DesignDocumentSerializer.Default.Serialize(document);

        DesignDocument reopened = DesignDocumentSerializer.Default.Deserialize(json);
        DesignDocument cloned = DesignDocumentSerializer.Default.Deserialize(
            DesignDocumentSerializer.Default.Serialize(reopened));
        DesignPropertyValue value = Assert.Single(cloned.Controls).Properties[InteractionEffectDesignValue.PropertyName];

        Assert.True(InteractionEffectDesignValue.TryRead(value, out var effects, out var error), error);
        Assert.Equal(2, effects.Count);
        Assert.EndsWith("PressScaleEffect", effects[0].ObjectTypeName, StringComparison.Ordinal);
        Assert.EndsWith("RippleEffect", effects[1].ObjectTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedEffectTypeIsRejectedWithoutMutation()
    {
        DesignPropertyValue unsupported = DesignPropertyValue.FromStructuredObject(
            "Example.UnsafeEffect",
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal));
        DesignPropertyValue value = InteractionEffectDesignValue.Create([unsupported]);

        bool success = InteractionEffectDesignerRegistry.TryReadCollection(value, out var entries, out var error);

        Assert.False(success);
        Assert.Empty(entries);
        Assert.Contains("not supported", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratorRejectsForeignTypeWithBuiltInShortName()
    {
        DesignDocument document = CreateDocument();
        DesignControlNode control = Assert.Single(document.Controls);
        DesignPropertyValue foreign = DesignPropertyValue.FromStructuredObject(
            "Example.RippleEffect",
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal));
        control.Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create([foreign]);

        var generated = new CSharpDesignerGenerator().Generate(document);

        Assert.DoesNotContain(".InteractionEffects.Add(", generated.Code, StringComparison.Ordinal);
        Assert.Contains(
            generated.Validation.Warnings,
            warning => warning.Contains("not registered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReverseParserRejectsForeignTypeAndUnsupportedPropertyWithoutMutation()
    {
        DesignDocument document = CreateDocument(
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName));
        string code = new CSharpDesignerGenerator().Generate(document).Code;
        string[] unsupportedVariants =
        [
            code.Replace(
                "ModernFormsNext.Animations.RippleEffect",
                "Example.RippleEffect",
                StringComparison.Ordinal),
            code.Replace("Enabled = true", "Unsupported = true", StringComparison.Ordinal)
        ];

        foreach (string unsupportedCode in unsupportedVariants)
        {
            CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(unsupportedCode);
            DesignControlNode control = Assert.Single(Assert.IsType<DesignDocument>(parsed.Document).Controls);

            Assert.Contains(
                parsed.Diagnostics,
                diagnostic => diagnostic.Message.Contains("Unsupported interaction effect initializer", StringComparison.Ordinal));
            Assert.False(control.Properties.ContainsKey(InteractionEffectDesignValue.PropertyName));
        }
    }

    [Fact]
    public void DesignerModelCreatesNoRuntimeEffectsOrSchedulerHandles()
    {
        int activeBefore = AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;

        DesignerInteractionEffectEntry entry =
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName);
        DesignPropertyValue value = InteractionEffectDesignerRegistry.WriteCollection([entry]);
        Assert.True(InteractionEffectDesignerRegistry.TryReadCollection(value, out var entries, out var error), error);

        Assert.All(entries, item => Assert.False((object)item is InteractionEffect));
        Assert.Equal(activeBefore, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void RuntimeCollectionMetadataUsesContentSerialization()
    {
        PropertyDescriptor property = TypeDescriptor.GetProperties(typeof(Control))[nameof(Control.InteractionEffects)]!;

        Assert.True(property.IsBrowsable);
        Assert.Equal(
            DesignerSerializationVisibility.Content,
            property.SerializationVisibility);
    }

    [Fact]
    public void EmptyRuntimeCollectionIsNotMarkedForSerialization()
    {
        using var button = new Button();
        PropertyDescriptor property = TypeDescriptor.GetProperties(button)[nameof(Control.InteractionEffects)]!;

        Assert.False(property.ShouldSerializeValue(button));

        button.InteractionEffects.Add(new RippleEffect());

        Assert.True(property.ShouldSerializeValue(button));
    }

    [Fact]
    public void PropertyGridExposesDesignerOnlyCollectionDialog()
    {
        int activeBefore = AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;
        DesignDocument document = CreateDocument(
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName));
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.Host.Selection.Select(Assert.Single(document.Controls));
        var state = new DesignerPropertyGridState(session);

        DesignerPropertyDescriptor descriptor = Assert.Single(
            state.Properties,
            property => property.Name == InteractionEffectDesignValue.PropertyName);

        Assert.True(descriptor.HasDialogEditor);
        Assert.NotNull(descriptor.DialogEditor);
        Assert.Equal("1 effect", descriptor.GetValue());
        Assert.Equal(activeBefore, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void NewButtonKeepsEmptyEffectsOutOfSaveCodeGenerationAndReverseSync()
    {
        DesignDocument document = CreateDocument();
        DesignControlNode button = Assert.Single(document.Controls);
        var generator = new CSharpDesignerGenerator();

        Assert.False(button.Properties.ContainsKey(InteractionEffectDesignValue.PropertyName));

        string json = DesignDocumentSerializer.Default.Serialize(document);
        Assert.DoesNotContain(InteractionEffectDesignValue.PropertyName, json, StringComparison.Ordinal);

        DesignDocument reopened = DesignDocumentSerializer.Default.Deserialize(json);
        Assert.False(Assert.Single(reopened.Controls).Properties.ContainsKey(InteractionEffectDesignValue.PropertyName));

        string code = generator.Generate(reopened).Code;
        Assert.DoesNotContain(".InteractionEffects.Add(", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".StyleTransitions.Add(", code, StringComparison.Ordinal);

        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        Assert.False(
            Assert.Single(Assert.IsType<DesignDocument>(parsed.Document).Controls)
                .Properties.ContainsKey(InteractionEffectDesignValue.PropertyName));
    }

    [Fact]
    public void NewButtonPropertyGridShowsEmptyCollectionWithoutStartingRuntimeWork()
    {
        int activeBefore = AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;
        DesignDocument document = CreateDocument();
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.Host.Selection.Select(Assert.Single(document.Controls));
        var state = new DesignerPropertyGridState(session);

        DesignerPropertyDescriptor descriptor = Assert.Single(
            state.Properties,
            property => property.Name == InteractionEffectDesignValue.PropertyName);

        Assert.True(descriptor.HasDialogEditor);
        Assert.Equal("0 effects", descriptor.GetValue());
        Assert.Equal(activeBefore, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void RemovingLastEffectRemovesGeneratedAddCall()
    {
        DesignDocument document = CreateDocument(
            InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName));
        DesignControlNode control = Assert.Single(document.Controls);
        control.Properties.Remove(InteractionEffectDesignValue.PropertyName);

        string code = new CSharpDesignerGenerator().Generate(document).Code;

        Assert.DoesNotContain(".InteractionEffects.Add(", code, StringComparison.Ordinal);
    }

    private static DesignDocument CreateDocument(params DesignerInteractionEffectEntry[] effects)
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(640, 480)
        };
        var control = new DesignControlNode
        {
            TypeName = "Button",
            Name = "button1",
            Bounds = new DesignBounds(10, 10, 120, 36),
            MemberVisibility = DesignerMemberVisibility.Private
        };
        if (effects.Length > 0)
        {
            control.Properties[InteractionEffectDesignValue.PropertyName] =
                InteractionEffectDesignerRegistry.WriteCollection(effects);
        }
        document.Controls.Add(control);
        return document;
    }

    private static int CountOccurrences(string text, string value)
        => text.Split(value, StringSplitOptions.None).Length - 1;
}
