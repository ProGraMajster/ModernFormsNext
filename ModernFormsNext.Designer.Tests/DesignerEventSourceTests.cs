using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerEventSourceTests
{
    private const string Parameters = "object? sender, global::System.EventArgs e";
    private static DesignDocument Document => new() { Namespace = "Example", ClassName = "Sample", FormName = "Sample" };

    [Fact]
    public void PlansOneMethodInTheExactRootAndPreservesExistingText()
    {
        const string source = "// user comment\r\nnamespace Example;\r\npublic partial class Sample : ModernFormsNext.Form\r\n{\r\n    private int value = 42;\r\n}\r\n";
        var plan = Plan(source);
        Assert.True(plan.Succeeded, plan.Message);
        Assert.Equal(source, plan.UpdatedText.Remove(plan.Offset, plan.Insertion.Length));
        Assert.Contains("\r\n    private void Sample_Click(object? sender, global::System.EventArgs e)\r\n", plan.UpdatedText);
        var compilation = DesignerProjectUserControlDiscovery.CreateEventCompilation(
            CSharpSyntaxTree.ParseText(plan.UpdatedText, path: "Sample.cs"), null);
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var again = Plan(plan.UpdatedText);
        Assert.True(again.Succeeded, again.Message);
        Assert.Empty(again.Insertion);
        Assert.True(again.Offset > 0);
    }

    [Theory]
    [InlineData("namespace Other; public class Sample { }")]
    [InlineData("namespace Example; public class Other { }")]
    [InlineData("namespace Example; public class Outer { public class Sample { } }")]
    [InlineData("namespace Example; public class Sample<T> { }")]
    [InlineData("namespace Example; public partial class Sample { } public partial class Sample { }")]
    public void DoesNotGuessAnotherClassOrNamespace(string source)
        => Assert.False(Plan(source).Succeeded);

    [Theory]
    [InlineData("private int Sample_Click(object? sender, System.EventArgs e) => 1;")]
    [InlineData("private void Sample_Click(object? sender) { }")]
    [InlineData("private void Sample_Click(object? sender, string e) { }")]
    [InlineData("private static void Sample_Click(object? sender, System.EventArgs e) { }")]
    [InlineData("private void Sample_Click<T>(object? sender, System.EventArgs e) { }")]
    [InlineData("private int Sample_Click;")]
    public void IncompatibleExistingMembersAreRejectedWithoutAddingAnOverload(string member)
    {
        var plan = Plan("namespace Example; public class Sample { " + member + " }");
        Assert.False(plan.Succeeded);
        Assert.Empty(plan.Insertion);
        Assert.Contains("incompatible", plan.Message);
    }

    [Fact]
    public void AliasAndCompatibleOverloadUseTheCompilerConversionRules()
    {
        var plan = Plan("using Args = System.EventArgs; namespace Example; public class Sample { "
            + "private void Sample_Click(object? sender, Args e) { } private void Sample_Click(int n) { } }");
        Assert.True(plan.Succeeded, plan.Message);
        Assert.Empty(plan.Insertion);
    }

    [Fact]
    public void MissingHandlerDiagnosticRetainsTheBindingName()
    {
        var plan = Plan("namespace Example; public class Sample { private void Renamed(object? s, System.EventArgs e) { } }", create: false);
        Assert.False(plan.Succeeded);
        Assert.Contains("missing or was renamed", plan.Message);
        Assert.Empty(plan.Insertion);
    }

    [Theory]
    [InlineData("ref object sender")]
    [InlineData("object sender = null")]
    [InlineData("[System.Obsolete] object sender")]
    [InlineData("object sender,")]
    [InlineData("void* sender")]
    public void UnsupportedParameterShapesFailBeforeSourceMutation(string parameters)
        => Assert.False(DesignerEventSource.Plan(Document, "Sample.cs", "namespace Example; public class Sample { }",
            "Sample_Click", parameters, null, true).Succeeded);

    [Fact]
    public void KeywordParameterNamesRemainEscaped()
    {
        var plan = DesignerEventSource.Plan(Document, "Sample.cs", "namespace Example; public class Sample { }",
            "Sample_Click", "object @event, global::System.EventArgs args", null, true);
        Assert.True(plan.Succeeded, plan.Message);
        Assert.Contains("object @event", plan.Insertion);
    }

    [Fact]
    public void EscapedHandlerIdentifiersResolveTheirActualSymbolAndRemainAssignable()
    {
        const string source = "namespace Example; public class Sample { }";
        var created = DesignerEventSource.Plan(Document, "Sample.cs", source, "@class", Parameters, null, true);
        Assert.True(created.Succeeded, created.Message);
        Assert.Contains("void @class", created.Insertion);
        var existing = DesignerEventSource.Plan(Document, "Sample.cs", created.UpdatedText, "@class", Parameters, null, false);
        Assert.True(existing.Succeeded, existing.Message);
        Assert.Empty(existing.Insertion);
        Assert.Equal(new[] { "@class" }, DesignerEventSource.CompatibleMethods(Document, "Sample.cs", created.UpdatedText, Parameters, null));
    }

    [Fact]
    public void EscapedRootAndNamespaceTokensCompareByTheirCSharpIdentity()
    {
        var document = Document;
        document.ClassName = "@Sample";
        var result = DesignerEventSource.Plan(document, "Sample.cs", "namespace @Example; public class Sample { }",
            "OnClick", Parameters, null, true);
        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public void SourceAndNestingLimitsAreCheckedBeforeParsing()
    {
        Assert.False(Plan(new string(' ', DesignerEventSource.MaximumSourceCharacters + 1)).Succeeded);
        Assert.False(DesignerEventSource.TryParse(new string('{', DesignerEventSource.MaximumSyntaxDepth + 1), "Sample.cs", out _, out _));
    }

    [Fact]
    public void TrustedDelegateFormattingSupportsArraysAndNestedClosedGenerics()
    {
        var parameters = DesignerFileService.WriteEventHandlerParameters(typeof(Action<int[,], GenericOuter<int>.Inner<string>>));
        Assert.True(DesignerEventSource.TryReadParameters(parameters, out _));
        Assert.Contains("int[,]", parameters);
        Assert.Contains("GenericOuter<int>.Inner<string?>", parameters);
    }

    private sealed class GenericOuter<T>
    {
        internal sealed class Inner<U> { }
    }

    private static DesignerEventSourcePlan Plan(string source, bool create = true)
        => DesignerEventSource.Plan(Document, "Sample.cs", source, "Sample_Click", Parameters, null, create);
}
