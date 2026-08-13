using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class AnimationEffectDefinitionDesignerTests
{
    [Fact]
    public void BuiltInEffectEasingAndTimeSpanRoundTripThroughGeneratedCode()
    {
        DesignerInteractionEffectEntry entry = InteractionEffectDesignerRegistry.Create(
            InteractionEffectDesignerRegistry.PressScaleTypeName);
        Assert.True(SetEffectProperty(entry, "PressDurationMilliseconds", DesignPropertyValue.FromDouble(95d), out string? error), error);
        Assert.True(SetEffectProperty(entry, "Easing", DesignPropertyValue.FromString("EaseInOut"), out error), error);
        DesignDocument document = CreateDocument();
        Assert.Single(document.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignerRegistry.WriteCollection([entry]);

        CSharpDesignerGenerationResult generated = new CSharpDesignerGenerator().Generate(document);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("PressDuration = System.TimeSpan.FromMilliseconds(95)", generated.Code, StringComparison.Ordinal);
        Assert.Contains("Easing = ModernFormsNext.Animations.Easings.EaseInOut", generated.Code, StringComparison.Ordinal);

        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        DesignPropertyValue stored = Assert.Single(parsed.Document!.Controls).Properties[InteractionEffectDesignValue.PropertyName];
        Assert.True(InteractionEffectDesignValue.TryRead(stored, out var effects, out error), error);
        Assert.Equal(95d, effects[0].ObjectProperties!["PressDurationMilliseconds"].Value);
        Assert.Equal("EaseInOut", effects[0].ObjectProperties!["Easing"].Value);
    }

    [Fact]
    public void DefaultEffectValuesStayOutOfMfdesignAndGeneratedInitializer()
    {
        DesignerInteractionEffectEntry entry = InteractionEffectDesignerRegistry.Create(
            InteractionEffectDesignerRegistry.RippleTypeName);
        Assert.Empty(entry.Properties);
        DesignDocument document = CreateDocument();
        Assert.Single(document.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignerRegistry.WriteCollection([entry]);

        string json = DesignDocumentSerializer.Default.Serialize(document);
        string code = new CSharpDesignerGenerator().Generate(document).Code;

        Assert.DoesNotContain("DurationMilliseconds", json, StringComparison.Ordinal);
        Assert.Contains("new ModernFormsNext.Animations.RippleEffect {}", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Duration =", code, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownEasingProducesControlledGenerationWarning()
    {
        DesignDocument document = CreateDocument();
        DesignPropertyValue effect = DesignPropertyValue.FromStructuredObject(
            BuiltInAnimationDefinitionCatalog.RippleEffectTypeName,
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Easing"] = DesignPropertyValue.FromString("ApplicationDelegate")
            });
        Assert.Single(document.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create([effect]);

        CSharpDesignerGenerationResult generated = new CSharpDesignerGenerator().Generate(document);

        Assert.DoesNotContain(".InteractionEffects.Add(", generated.Code, StringComparison.Ordinal);
        Assert.Contains(generated.Validation.Warnings, warning =>
            warning.Contains("invalid designer value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OutOfRangeStoredEffectMetricsProduceControlledGenerationWarnings()
    {
        DesignAnimationDefinitionDescriptor descriptor = CustomEffectDescriptor();
        DesignDocument document = CreateDocument();
        Assert.Single(document.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create(
            [
                DesignPropertyValue.FromStructuredObject(
                    descriptor.TypeName,
                    new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                    {
                        ["Opacity"] = DesignPropertyValue.FromDouble(2d)
                    }),
                DesignPropertyValue.FromStructuredObject(
                    descriptor.TypeName,
                    new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                    {
                        ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(-1d)
                    }),
                DesignPropertyValue.FromStructuredObject(
                    BuiltInAnimationDefinitionCatalog.PressScaleEffectTypeName,
                    new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                    {
                        ["PressedScale"] = DesignPropertyValue.FromDouble(double.Epsilon)
                    })
            ]);

        CSharpDesignerGenerationResult generated = new CSharpDesignerGenerator().Generate(
            document,
            new CSharpDesignerGenerationOptions { AnimationDefinitions = [descriptor] });

        Assert.DoesNotContain(".InteractionEffects.Add(", generated.Code, StringComparison.Ordinal);
        Assert.Contains(generated.Validation.Warnings, warning => warning.Contains("Opacity", StringComparison.Ordinal));
        Assert.Contains(generated.Validation.Warnings, warning => warning.Contains("DurationMilliseconds", StringComparison.Ordinal));
        Assert.Contains(generated.Validation.Warnings, warning => warning.Contains("PressedScale", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsafeCustomDescriptorCannotInjectGeneratedCode()
    {
        var descriptor = new DesignAnimationDefinitionDescriptor(
            "Example.GlowEffect; System.Console.WriteLine(1)",
            "Unsafe",
            DesignAnimationDefinitionKind.InteractionEffect,
            []);
        DesignDocument document = CreateDocument();
        Assert.Single(document.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create(
            [
                DesignPropertyValue.FromStructuredObject(
                    descriptor.TypeName,
                    new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal))
            ]);

        CSharpDesignerGenerationResult generated = new CSharpDesignerGenerator().Generate(
            document,
            new CSharpDesignerGenerationOptions { AnimationDefinitions = [descriptor] });

        Assert.DoesNotContain("System.Console.WriteLine", generated.Code, StringComparison.Ordinal);
        Assert.Contains(generated.Validation.Warnings, warning => warning.Contains("safe C# syntax", StringComparison.Ordinal));
    }

    [Fact]
    public void LayoutAndVisualStateTransitionsRoundTripWithoutDelegateSerialization()
    {
        DesignDocument document = CreateDocument();
        DesignControlNode button = Assert.Single(document.Controls);
        button.Properties[LayoutTransitionDesignValue.PropertyName] =
            LayoutTransitionDesignValue.Create(enabled: true, durationMilliseconds: 210d, easing: "EaseOut");
        button.Properties[VisualStateTransitionDesignValue.PropertyName] =
            VisualStateTransitionDesignValue.Create(
            [
                new("Normal", "Hover", 140d, "CubicOut"),
                new("Hover", "Pressed", 80d, "Linear")
            ]);

        string code = new CSharpDesignerGenerator().Generate(document).Code;
        AssertGeneratedCodeCompiles(code);
        Assert.Contains("LayoutTransition = new ModernFormsNext.Animations.LayoutTransition", code, StringComparison.Ordinal);
        Assert.Equal(2, Count(code, ".StyleTransitions.Add("));

        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        DesignControlNode reopened = Assert.Single(parsed.Document!.Controls);
        Assert.True(LayoutTransitionDesignValue.TryRead(
            reopened.Properties[LayoutTransitionDesignValue.PropertyName],
            out bool enabled,
            out double duration,
            out string easing,
            out string? error), error);
        Assert.True(enabled);
        Assert.Equal(210d, duration);
        Assert.Equal("EaseOut", easing);
        Assert.True(VisualStateTransitionDesignValue.TryRead(
            reopened.Properties[VisualStateTransitionDesignValue.PropertyName],
            out var transitions,
            out error), error);
        Assert.Equal(["Normal", "Hover"], transitions.Select(item => item.From));
        Assert.Equal(["Hover", "Pressed"], transitions.Select(item => item.To));
    }

    [Fact]
    public void ReverseParserDoesNotCanonicalizeSimilarlyNamedForeignAnimationApis()
    {
        DesignDocument document = CreateDocument();
        DesignControlNode button = Assert.Single(document.Controls);
        button.Properties[LayoutTransitionDesignValue.PropertyName] =
            LayoutTransitionDesignValue.Create(enabled: true, durationMilliseconds: 210d, easing: "EaseOut");
        button.Properties[VisualStateTransitionDesignValue.PropertyName] =
            VisualStateTransitionDesignValue.Create([new("Normal", "Hover", 140d, "CubicOut")]);
        button.Properties[InteractionEffectDesignValue.PropertyName] = InteractionEffectDesignValue.Create(
        [
            DesignPropertyValue.FromStructuredObject(
                BuiltInAnimationDefinitionCatalog.RippleEffectTypeName,
                new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                {
                    ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(123d),
                    ["RadiusMode"] = DesignPropertyValue.FromEnum(
                        "ModernFormsNext.Animations.RippleRadiusMode",
                        "Fixed")
                })
        ]);
        string generated = new CSharpDesignerGenerator().Generate(document).Code;
        string source = generated
            .Replace(
                "new ModernFormsNext.Animations.LayoutTransition",
                "new Example.CustomLayoutTransition",
                StringComparison.Ordinal)
            .Replace(
                "ModernFormsNext.Animations.VisualState.Normal",
                "Example.CustomVisualState.Normal",
                StringComparison.Ordinal)
            .Replace(
                "System.TimeSpan.FromMilliseconds(123)",
                "Example.CustomTimeSpan.FromMilliseconds(123)",
                StringComparison.Ordinal);

        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(source);

        Assert.True(parsed.Success);
        DesignControlNode parsedButton = Assert.Single(parsed.Document!.Controls);
        Assert.DoesNotContain(LayoutTransitionDesignValue.PropertyName, parsedButton.Properties.Keys);
        Assert.DoesNotContain(VisualStateTransitionDesignValue.PropertyName, parsedButton.Properties.Keys);
        Assert.DoesNotContain(InteractionEffectDesignValue.PropertyName, parsedButton.Properties.Keys);
        Assert.Contains(parsed.Diagnostics, diagnostic =>
            diagnostic.Severity == CSharpDesignerDiagnosticSeverity.Warning
            && diagnostic.Message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase));

        CSharpDesignerParseResult foreignEnum = new CSharpDesignerParser().Parse(
            generated.Replace(
                "ModernFormsNext.Animations.RippleRadiusMode.Fixed",
                "Example.CustomRadiusMode.Fixed",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            InteractionEffectDesignValue.PropertyName,
            Assert.Single(foreignEnum.Document!.Controls).Properties.Keys);

        CSharpDesignerParseResult foreignEasing = new CSharpDesignerParser().Parse(
            generated.Replace(
                "ModernFormsNext.Animations.Easings.EaseOut",
                "Example.CustomEasings.EaseOut",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            LayoutTransitionDesignValue.PropertyName,
            Assert.Single(foreignEasing.Document!.Controls).Properties.Keys);

        CSharpDesignerParseResult foreignTransition = new CSharpDesignerParser().Parse(
            generated.Replace(
                "new ModernFormsNext.Animations.VisualStateTransition",
                "new Example.CustomVisualStateTransition",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            VisualStateTransitionDesignValue.PropertyName,
            Assert.Single(foreignTransition.Document!.Controls).Properties.Keys);
    }

    [Fact]
    public void RootAnimationPropertiesUseSameRuntimeAndRoundTripContracts()
    {
        DesignDocument document = CreateDocument();
        document.Properties[LayoutTransitionDesignValue.PropertyName] =
            LayoutTransitionDesignValue.Create(false, 0d, "Linear");
        document.Properties[VisualStateTransitionDesignValue.PropertyName] =
            VisualStateTransitionDesignValue.Create([new("Normal", "Focused", 100d, "EaseIn")]);
        document.Properties[InteractionEffectDesignValue.PropertyName] = InteractionEffectDesignValue.Create(
        [
            DesignPropertyValue.FromStructuredObject(
                BuiltInAnimationDefinitionCatalog.PressScaleEffectTypeName,
                new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal))
        ]);

        string code = new CSharpDesignerGenerator().Generate(document).Code;
        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(code);

        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        Assert.Contains(LayoutTransitionDesignValue.PropertyName, parsed.Document!.Properties.Keys);
        Assert.Contains(VisualStateTransitionDesignValue.PropertyName, parsed.Document.Properties.Keys);
        Assert.Contains(InteractionEffectDesignValue.PropertyName, parsed.Document.Properties.Keys);
    }

    [Fact]
    public void CustomEffectDescriptorGeneratesAndReverseParsesSupportedLiteralSubset()
    {
        DesignAnimationDefinitionDescriptor descriptor = CustomEffectDescriptor();
        DesignDocument document = CreateDocument();
        Assert.Single(document.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create(
            [
                DesignPropertyValue.FromStructuredObject(
                    descriptor.TypeName,
                    new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                    {
                        ["Opacity"] = DesignPropertyValue.FromDouble(0.65d),
                        ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(125d),
                        ["Easing"] = DesignPropertyValue.FromString("BounceOut")
                    })
            ]);
        var generationOptions = new CSharpDesignerGenerationOptions { AnimationDefinitions = [descriptor] };
        var parseOptions = new CSharpDesignerParseOptions { AnimationDefinitions = [descriptor] };

        string code = new CSharpDesignerGenerator().Generate(document, generationOptions).Code;
        Assert.Contains("new Example.GlowEffect", code, StringComparison.Ordinal);
        Assert.Contains("Opacity = 0.65f", code, StringComparison.Ordinal);
        Assert.DoesNotContain(CSharpSyntaxTree.ParseText(code).GetDiagnostics(), diagnostic =>
            diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(code, parseOptions);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        DesignPropertyValue stored = Assert.Single(parsed.Document!.Controls).Properties[InteractionEffectDesignValue.PropertyName];
        Assert.True(InteractionEffectDesignValue.TryRead(stored, out var effects, out string? error), error);
        Assert.Equal("Example.GlowEffect", Assert.Single(effects).ObjectTypeName);
    }

    [Fact]
    public void SourceDiscoveryRequiresOptInAndNeverExecutesConstructors()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mfn-animation-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Effects.cs"), """
                using EffectRoot = ModernFormsNext.Animations.InteractionEffect;
                using DefinitionMarker = ModernFormsNext.Animations.DesignableAnimationDefinitionAttribute;
                using PropertyMarker = ModernFormsNext.Animations.DesignableAnimationPropertyAttribute;
                using PropertyKind = ModernFormsNext.Animations.DesignableAnimationPropertyKind;
                namespace Example.Nested;
                public abstract class GlowEffectBase : EffectRoot
                {
                    [PropertyMarker(PropertyKind.Number, DefaultValue = "0.4", Minimum = 0, Maximum = 1)]
                    public System.Single Opacity { get; set; }
                }
                [DefinitionMarker("Glow")]
                public sealed class GlowEffect : GlowEffectBase
                {
                    public GlowEffect() => throw new System.Exception("must not run");
                }
                public sealed class UnmarkedEffect : EffectRoot { }
                public class FakeInteractionEffect { }
                [DefinitionMarker("False positive")]
                public sealed class NotAnEffect : FakeInteractionEffect { }
                [DefinitionMarker("Generic")]
                public sealed class GenericEffect<T> : EffectRoot { }
                """);

            DesignAnimationDefinitionDescriptor descriptor = Assert.Single(
                DesignerProjectAnimationDefinitionDiscovery.Discover(directory));

            Assert.Equal("Example.Nested.GlowEffect", descriptor.TypeName);
            DesignAnimationPropertyDescriptor property = Assert.Single(descriptor.Properties);
            Assert.Equal(0.4d, property.DefaultValue.Value);
            Assert.Equal("System.Single", property.RuntimeTypeName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AnimationDefinitionMetadataIsDiscoveredButNotOfferedAsInteractionEffect()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mfn-animation-definition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Pulse.cs"), """
                using ModernFormsNext.Animations;
                namespace Example;
                [DesignableAnimationDefinition("Pulse")]
                public sealed class PulseDefinition : AnimationDefinition { }
                """);
            DesignAnimationDefinitionDescriptor descriptor = Assert.Single(
                DesignerProjectAnimationDefinitionDiscovery.Discover(directory));

            Assert.Equal(DesignAnimationDefinitionKind.AnimationDefinition, descriptor.Kind);
            Assert.Throws<NotSupportedException>(() =>
                InteractionEffectDesignerRegistry.Create(descriptor.TypeName, [descriptor]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PropertyGridSeparatesEffectsLayoutAndVisualStateTransitions()
    {
        DesignDocument document = CreateDocument();
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.Host.Selection.Select(Assert.Single(document.Controls));
        var state = new DesignerPropertyGridState(session);

        Assert.Contains(state.Properties, item => item.Name == InteractionEffectDesignValue.PropertyName && item.Category == "Behavior");
        Assert.Contains(state.Properties, item => item.Name == LayoutTransitionDesignValue.PropertyName && item.Category == "Behavior");
        Assert.Contains(state.Properties, item => item.Name == VisualStateTransitionDesignValue.PropertyName && item.Category == "Appearance");
        Assert.DoesNotContain(state.Properties, item => item.Name.Contains("Scheduler", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateEffectsAndDeepCopyPreserveIndependentOrderedDefinitions()
    {
        DesignDocument original = CreateDocument();
        DesignPropertyValue first = DesignPropertyValue.FromStructuredObject(
            BuiltInAnimationDefinitionCatalog.RippleEffectTypeName,
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(200d)
            });
        Assert.Single(original.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create([first, first]);
        DesignDocument copy = DesignDocumentSerializer.Default.Deserialize(
            DesignDocumentSerializer.Default.Serialize(original));
        Assert.Single(copy.Controls).Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignValue.Create([first]);

        Assert.True(InteractionEffectDesignValue.TryRead(
            Assert.Single(original.Controls).Properties[InteractionEffectDesignValue.PropertyName],
            out var originalEffects,
            out string? error), error);
        Assert.True(InteractionEffectDesignValue.TryRead(
            Assert.Single(copy.Controls).Properties[InteractionEffectDesignValue.PropertyName],
            out var copiedEffects,
            out error), error);
        Assert.Equal(2, originalEffects.Count);
        Assert.Single(copiedEffects);
    }

    [Fact]
    public void ChangedOrMalformedCustomDescriptorDoesNotDestroyStoredDefinition()
    {
        DesignAnimationDefinitionDescriptor descriptor = CustomEffectDescriptor();
        DesignPropertyValue stored = InteractionEffectDesignValue.Create(
        [
            DesignPropertyValue.FromStructuredObject(
                descriptor.TypeName,
                new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                {
                    ["RemovedProperty"] = DesignPropertyValue.FromDouble(1d)
                })
        ]);

        Assert.True(InteractionEffectDesignerRegistry.TryReadCollection(
            stored,
            out var entries,
            out string? error,
            [descriptor]), error);
        DesignerInteractionEffectEntry entry = Assert.Single(entries);
        Assert.False(entry.IsSupported);
        Assert.Contains("RemovedProperty", entry.ToDesignValue().ObjectProperties!.Keys);

        var malformedProperties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Count"] = DesignPropertyValue.FromInt32(0),
            ["FutureMetadata"] = DesignPropertyValue.FromString("preserve")
        };
        DesignPropertyValue malformed = DesignPropertyValue.FromStructuredObject(
            InteractionEffectDesignValue.CollectionTypeName,
            malformedProperties);
        Assert.False(InteractionEffectDesignerRegistry.TryReadCollection(
            malformed,
            out _,
            out error,
            [descriptor]));
        Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FutureMetadata", malformed.ObjectProperties!.Keys);
    }

    [Fact]
    public void LegacyMfdesignWithoutAnimationPropertiesRemainsStable()
    {
        const string json = """
            {
              "metadata": { "formatVersion": 1, "generator": "legacy" },
              "namespace": "Example",
              "className": "LegacyForm",
              "formName": "LegacyForm",
              "size": { "width": 640, "height": 480 },
              "properties": {},
              "events": {},
              "controls": []
            }
            """;

        DesignDocument document = DesignDocumentSerializer.Default.Deserialize(json);
        string generated = new CSharpDesignerGenerator().Generate(document).Code;

        Assert.Empty(document.Properties);
        Assert.DoesNotContain("InteractionEffects", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("LayoutTransition", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("StyleTransitions", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void TransitionResetRemovesOptionalSerializedValues()
    {
        var layout = new DesignerLayoutTransitionEditorModel(
            LayoutTransitionDesignValue.Create(true, 200d, "EaseOut"));
        layout.Reset();
        Assert.True(layout.TryCreateValue(out DesignPropertyValue? resetLayout, out string? error), error);
        Assert.Null(resetLayout);

        var visual = new DesignerVisualStateTransitionEditorModel(
            VisualStateTransitionDesignValue.Create([new("Normal", "Hover", 100d, "Linear")]));
        visual.Reset();
        Assert.True(visual.TryCreateValue(out DesignPropertyValue resetVisual, out error), error);
        Assert.True(VisualStateTransitionDesignValue.TryRead(resetVisual, out var transitions, out error), error);
        Assert.Empty(transitions);
    }

    private static DesignAnimationDefinitionDescriptor CustomEffectDescriptor()
        => new(
            "Example.GlowEffect",
            "Glow",
            DesignAnimationDefinitionKind.InteractionEffect,
            [
                new("Opacity", "Opacity", DesignAnimationPropertyKind.Number, DesignPropertyValue.FromDouble(0.4d))
                { Minimum = 0d, Maximum = 1d, RuntimeTypeName = "System.Single" },
                new("DurationMilliseconds", "Duration", DesignAnimationPropertyKind.TimeSpan, DesignPropertyValue.FromDouble(100d))
                { Minimum = 0d, RuntimeTypeName = "System.TimeSpan" },
                new("Easing", "Easing", DesignAnimationPropertyKind.Easing, DesignPropertyValue.FromString("Linear"))
            ]);

    private static bool SetEffectProperty(
        DesignerInteractionEffectEntry entry,
        string name,
        DesignPropertyValue value,
        out string? error)
        => InteractionEffectDesignerRegistry.TrySetProperty(
            entry,
            entry.Descriptor!.Properties.Single(property => property.Name == name),
            value,
            out error);

    private static DesignDocument CreateDocument()
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(640, 480)
        };
        document.Controls.Add(new DesignControlNode
        {
            TypeName = "Button",
            Name = "button1",
            Bounds = new DesignBounds(10, 10, 120, 36),
            MemberVisibility = DesignerMemberVisibility.Private
        });
        return document;
    }

    private static int Count(string text, string value)
        => text.Split(value, StringSplitOptions.None).Length - 1;

    private static void AssertGeneratedCodeCompiles(string generatedCode)
    {
        string baseClass = """
            namespace Example;
            public partial class MainForm : ModernFormsNext.Form { }
            """;
        string[] trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];
        IEnumerable<MetadataReference> references = trustedAssemblies
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(assembly => assembly.Location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratedDesignerValidation",
            [CSharpSyntaxTree.ParseText(generatedCode), CSharpSyntaxTree.ParseText(baseClass)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
