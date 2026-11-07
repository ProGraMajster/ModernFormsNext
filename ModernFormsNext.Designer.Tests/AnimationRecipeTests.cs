using System.Drawing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Animations;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using ModernFormsNext.Testing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

// The deterministic host temporarily installs process-wide window and scheduler services.
[CollectionDefinition(nameof(AnimationRecipeHostCollection), DisableParallelization = true)]
public sealed class AnimationRecipeHostCollection { }

[Collection(nameof(AnimationRecipeHostCollection))]
public sealed class AnimationRecipeTests
{
    private const string Recipe = "Click\nOpacity 0.5 200 BlendEaseInOut 0.6\nRotation 30 100 Linear";
    private const string RecipeProperty = AnimationRecipeDesignValue.PropertyName;

    [Fact]
    public void DesignAndGeneratedLiteralRoundTripWithoutExecutingProjectCode()
    {
        var document = CreateDocument();
        document.RootKind = DesignRootKind.UserControl;
        document.Controls[0].Properties[RecipeProperty] = DesignPropertyValue.FromString(Recipe);
        document.Properties[RecipeProperty] = DesignPropertyValue.FromString("Click\nScale 1.15 100 EaseOut");

        var serializer = DesignDocumentSerializer.Default;
        string json = serializer.Serialize(document);
        var reopened = serializer.Deserialize(json);
        Assert.Equal(DesignDocumentSerializer.AnimationFormatVersion, reopened.Metadata.FormatVersion);
        Assert.Equal(DesignDocumentSerializer.CurrentFormatVersion, document.Metadata.FormatVersion);
        Assert.Equal(Recipe, reopened.Controls[0].Properties[RecipeProperty].Value);

        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Empty(generated.Validation.Warnings);
        Assert.Contains(".InteractionEffects.Add(new global::ModernFormsNext.Animations.AnimationRecipeEffect(", generated.Code);
        var parsed = new CSharpDesignerParser().Parse(generated.Code,
            new CSharpDesignerParseOptions { RootKind = DesignRootKind.UserControl });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        Assert.Equal(Recipe, parsed.Document!.Controls[0].Properties[RecipeProperty].Value);
        Assert.Equal(reopened.Properties[RecipeProperty].Value, parsed.Document.Properties[RecipeProperty].Value);

        // Compile the actual output. No assembly is loaded and no application constructor runs.
        string[] referencePaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var compilation = CSharpCompilation.Create("AnimationRecipeConsumer",
            [CSharpSyntaxTree.ParseText(generated.Code),
             CSharpSyntaxTree.ParseText("namespace Example; public partial class RecipeForm : ModernFormsNext.UserControl { }")],
            referencePaths.Append(typeof(Form).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var assemblyBytes = new MemoryStream();
        var emitted = compilation.Emit(assemblyBytes);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
    }

    [Theory]
    [InlineData("Click\nOpacity 0.5 120 ProjectCurve")]
    [InlineData("Click\nOpacity NaN 120 Linear")]
    [InlineData("Click\nOpacity 0.5 120 BlendEaseInOut 1.1")]
    [InlineData("Click\nScale 0.2 120 EaseOut")]
    [InlineData("Click\nRotation 25 1900 Linear\nRotation 0 101 Linear")]
    [InlineData("Click\nRotation 25 2147483647 Linear")]
    public void UnsupportedOrOverBudgetMetadataStaysInDocumentButNeverBecomesCode(string recipe)
    {
        var document = CreateDocument();
        document.Controls[0].Properties[RecipeProperty] = DesignPropertyValue.FromString(recipe);
        var serializer = DesignDocumentSerializer.Default;
        var reopened = serializer.Deserialize(serializer.Serialize(document));
        Assert.Equal(recipe, reopened.Controls[0].Properties[RecipeProperty].Value);

        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded);
        Assert.DoesNotContain("AnimationRecipeEffect", generated.Code);
        Assert.Contains(generated.Validation.Warnings, warning => warning.Contains("Animation Recipe", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => new AnimationRecipeEffect(recipe));
    }

    [Fact]
    public void WindowRecipeIsPreservedWithoutGeneratingAControlOnlyMember()
    {
        var document = CreateDocument();
        document.Properties[RecipeProperty] = DesignPropertyValue.FromString(Recipe);
        var serializer = DesignDocumentSerializer.Default;
        var reopened = serializer.Deserialize(serializer.Serialize(document));
        Assert.Equal(Recipe, reopened.Properties[RecipeProperty].Value);
        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded);
        Assert.DoesNotContain("AnimationRecipeEffect", generated.Code);
        Assert.Contains(generated.Validation.Warnings, warning => warning.Contains("A Form is a window", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyControlPropertyWithSimilarNameRemainsARegularAssignment()
    {
        var document = CreateDocument();
        document.Controls[0].TypeName = "Example.ProjectButton";
        document.Controls[0].Properties["AnimationRecipe"] = DesignPropertyValue.FromString("old value");
        var serializer = DesignDocumentSerializer.Default;
        var reopened = serializer.Deserialize(serializer.Serialize(document));
        Assert.Equal(DesignDocumentSerializer.CurrentFormatVersion, reopened.Metadata.FormatVersion);
        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded);
        Assert.Contains("this.button1.AnimationRecipe = \"old value\";", generated.Code);
    }

    [Fact]
    public void ShapeLengthAndStepCountAreAdmittedBeforeScheduling()
    {
        var document = CreateDocument();
        document.Properties[RecipeProperty] = DesignPropertyValue.FromString(new string(' ', 2049));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));

        document.Properties[RecipeProperty] = DesignPropertyValue.FromInt32(1);
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));

        string recipe = "Click\n" + string.Concat(Enumerable.Repeat("Scale 1 10 Linear\n", 9));
        Assert.False(AnimationRecipeDesignValue.TryValidate(recipe, out _));
        Assert.Throws<ArgumentException>(() => new AnimationRecipeEffect(recipe));
    }

    [Fact]
    public void ReverseImportRejectsNonLiteralRecipeWithoutReplacingData()
    {
        var document = CreateDocument();
        document.Controls[0].Properties[RecipeProperty] = DesignPropertyValue.FromString(Recipe);
        string code = new CSharpDesignerGenerator().Generate(document).Code;
        const string prefix = "new global::ModernFormsNext.Animations.AnimationRecipeEffect(";
        int start = code.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        int end = code.IndexOf("));", start, StringComparison.Ordinal);
        string changed = code[..start] + "ReadRecipe()" + code[end..];

        var parsed = new CSharpDesignerParser().Parse(changed);

        Assert.False(parsed.Success);
        Assert.Contains(parsed.Diagnostics, item => item.Message.Contains("string literal", StringComparison.Ordinal));
    }

    [Fact]
    public void PreviewAndClickUseTheSameSchedulerCurveAndStopReleasesOnlyPreview()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        var runtimeButton = owner.Controls.Add(new Button { Bounds = new Rectangle(20, 20, 100, 40) });
        using var runtimeEffect = new AnimationRecipeEffect(Recipe);
        runtimeButton.InteractionEffects.Add(runtimeEffect);
        var window = host.Show(owner);
        using var dialog = new DesignerAnimationRecipeDialog(Recipe);
        host.ShowDialog(dialog, window);
        var sample = Find<Button>(dialog, "Sample");

        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Find<Button>(dialog, "Play").PerformClick();
        runtimeButton.PerformClick();
        host.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(0.9125f, sample.Opacity, precision: 5);
        Assert.Equal(sample.Opacity, runtimeButton.Opacity);

        Find<Button>(dialog, "Restart").PerformClick();
        host.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(0.9125f, sample.Opacity, precision: 5);
        Assert.NotEqual(sample.Opacity, runtimeButton.Opacity);

        Find<Button>(dialog, "Stop").PerformClick();
        Assert.Equal(1f, sample.Opacity);
        Assert.Equal(1, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        host.Clock.Advance(TimeSpan.FromMilliseconds(120));
        host.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0.5f, runtimeButton.Opacity);
        Assert.Equal(30f, runtimeButton.Rotation);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(0f, sample.Rotation);
    }

    [Fact]
    public void ReducedMotionCompletesPreviewAndUnavailableRecipeSchedulesNothing()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        var window = host.Show(owner);
        AnimationScheduler.Default.Policy.ReducedMotion = true;
        using var dialog = new DesignerAnimationRecipeDialog("Click\nOpacity 0.5 120 ProjectCurve");
        host.ShowDialog(dialog, window);
        var sample = Find<Button>(dialog, "Sample");
        Find<Button>(dialog, "Play").PerformClick();
        Assert.Contains("Preview unavailable", Find<Label>(dialog, "Status").Text);
        Assert.Equal(1f, sample.Opacity);

        Find<TextBox>(dialog, "Text").Text = Recipe;
        Find<Button>(dialog, "Play").PerformClick();
        host.Clock.Advance(TimeSpan.Zero);

        Assert.Equal(0.5f, sample.Opacity);
        Assert.Equal(30f, sample.Rotation);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void PreviewFaultShowsPlaceholderAndReleasesSchedulerWork()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        var window = host.Show(owner);
        AnimationScheduler.Default.Policy.DurationScale = double.MaxValue;
        using var dialog = new DesignerAnimationRecipeDialog(Recipe);
        host.ShowDialog(dialog, window);

        Find<Button>(dialog, "Play").PerformClick();
        host.Dispatcher.Drain();

        Assert.Contains("Preview unavailable", Find<Label>(dialog, "Status").Text);
        Assert.Equal(1f, Find<Button>(dialog, "Sample").Opacity);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void PreviewDeadlineCancelsWorkEvenWithScaledDurationAndNoTicks()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        var window = host.Show(owner);
        AnimationScheduler.Default.Policy.DurationScale = 12d;
        using var dialog = new DesignerAnimationRecipeDialog("Click\nOpacity 0.5 1000 EaseOut");
        host.ShowDialog(dialog, window);

        Find<Button>(dialog, "Play").PerformClick();
        Assert.Equal(1, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        var status = Find<Label>(dialog, "Status");
        Assert.True(SpinWait.SpinUntil(() =>
        {
            host.Dispatcher.Drain();
            return status.Text.Contains("time limit", StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(5)));
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(1f, Find<Button>(dialog, "Sample").Opacity);
    }

    [Fact]
    public void PropertyGridPreviewAndCancelDoNotCreateHistoryAndCommitCanUndoRedo()
    {
        using var host = ModernFormsTestHost.Create();
        using var session = new DesignerSession();
        session.LoadDocument(CreateDocument());
        session.Host.Selection.Select(session.Document.Controls[0]);
        using var owner = new Form();
        var grid = owner.Controls.Add(new DesignerPropertyGrid(session));
        host.Show(owner);
        var row = new DesignerPropertyGridRow(new DesignerPropertyGridState(session).Properties.Single(item => item.Name == RecipeProperty));

        grid.OpenDialogEditor(row);
        var first = Assert.Single(Application.OpenForms.OfType<DesignerAnimationRecipeDialog>());
        Find<Button>(first, "Play").PerformClick();
        host.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Find<Button>(first, "Cancel").PerformClick();
        host.Dispatcher.Drain();
        Assert.False(session.Transactions.CanUndo);
        Assert.Empty(session.Document.Controls[0].Properties);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);

        grid.OpenDialogEditor(row);
        var second = Assert.Single(Application.OpenForms.OfType<DesignerAnimationRecipeDialog>());
        var editor = Find<TextBox>(second, "Text");
        editor.Text = Recipe;
        string savedText = editor.Text;
        Find<Button>(second, "OK").PerformClick();
        host.Dispatcher.Drain();
        Assert.Equal(savedText, session.Document.Controls[0].Properties[RecipeProperty].Value);
        Assert.True(session.Transactions.Undo());
        Assert.False(session.Document.Controls[0].Properties.ContainsKey(RecipeProperty));
        Assert.True(session.Transactions.Redo());
        Assert.Equal(savedText, session.Document.Controls[0].Properties[RecipeProperty].Value);
    }

    private static T Find<T>(Form dialog, string suffix) where T : Control
        => Assert.IsType<T>(Assert.Single(dialog.Controls.Find("AnimationRecipe" + suffix, searchAllChildren: true)));

    private static DesignDocument CreateDocument()
        => new()
        {
            Namespace = "Example",
            ClassName = "RecipeForm",
            FormName = "RecipeForm",
            Size = new(360, 240),
            Controls =
            [
                new DesignControlNode
                {
                    TypeName = "ModernFormsNext.Button",
                    Name = "button1",
                    Bounds = new(20, 20, 100, 40)
                }
            ]
        };
}
