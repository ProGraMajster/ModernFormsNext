using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designing;
using ModernFormsNext.CodeGeneration.Utilities;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class PageDesignerTypeNameSafetyTests
{
    [Theory]
    [InlineData("\n", false)]
    [InlineData("\r", false)]
    [InlineData("\r\n", false)]
    [InlineData("\u0085", false)]
    [InlineData("\u2028", false)]
    [InlineData("\u2029", false)]
    [InlineData("\n", true)]
    [InlineData("\r", true)]
    [InlineData("\r\n", true)]
    [InlineData("\u0085", true)]
    [InlineData("\u2028", true)]
    [InlineData("\u2029", true)]
    public void InformationalMetadataCannotIntroducePreprocessorDirectives(string separator, bool hash)
    {
        var options = new CSharpDesignerGenerationOptions();
        var value = $"file{separator}#error metadata_escape";
        if (hash) options.DesignHash = value;
        else options.SourceFilePath = value;
        var generated = new CSharpDesignerGenerator().Generate(new DesignDocument { ClassName = "SafeForm" }, options);
        Assert.True(generated.Succeeded);
        // Parse only: metadata is inert display text, never compiled or executed by this test.
        var tree = CSharpSyntaxTree.ParseText(generated.Code);
        Assert.Empty(tree.GetDiagnostics());
        Assert.Empty(tree.GetRoot().DescendantNodes(descendIntoTrivia: true).OfType<DirectiveTriviaSyntax>());
        Assert.Equal(value, hash ? options.DesignHash : options.SourceFilePath);
    }

    [Fact]
    public void MalformedEnumAndCyclicStructuredValuesProduceDiagnosticsWithoutEmission()
    {
        var document = new DesignDocument { ClassName = "SafeForm" };
        document.Properties["Dock"] = new DesignPropertyValue(DesignPropertyValueKind.Enum, 42, "DockStyle");
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);

        var value = DesignPropertyValue.FromStructuredObject("System.Drawing.Size", new Dictionary<string, DesignPropertyValue>());
        ((IDictionary<string, DesignPropertyValue>)value.ObjectProperties!)["Width"] = value;
        document.Properties.Clear();
        document.Properties["Tag"] = value;
        generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATypeNameCannotIntroduceAnInvocationIntoGeneratedInitialization(bool roundTripJson)
    {
        var document = new DesignDocument
        {
            Namespace = "Safety",
            ClassName = "InjectionFixture",
            FormName = "InjectionFixture",
            RootKind = DesignRootKind.UserControl
        };
        var page = new DesignControlNode { Name = "page", TypeName = nameof(ContentPage) };
        page.Children.Add(new DesignControlNode
        {
            Name = "body",
            MemberVisibility = DesignerMemberVisibility.None,
            TypeName = "Button(); this.MarkInjectedCode(); var unused = new Button"
        });
        document.Controls.Add(page);
        if (roundTripJson)
            document = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));

        var generated = new CSharpDesignerGenerator().Generate(document);
        // Parse only. Never compile, instantiate or invoke this untrusted generated source.
        var tree = CSharpSyntaxTree.ParseText(generated.Code);
        int injectedCalls = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Count(call => call.ToString() == "this.MarkInjectedCode()");
        Assert.False(generated.Succeeded,
            $"Untrusted TypeName was accepted; injected invocation count={injectedCalls}. Generated source (NOT EXECUTED):{Environment.NewLine}{generated.Code}");
        Assert.Empty(generated.Code);
    }

    [Theory]
    [InlineData("Button(); this.MarkInjectedCode(); var unused = new Button")]
    [InlineData("Button\n#error injected")]
    [InlineData("Button /* hidden text */")]
    [InlineData("Button[]")]
    [InlineData("Button?")]
    [InlineData("Button<int; this.MarkInjectedCode()>")]
    [InlineData("Button<>")]
    public void MalformedTypesAreRejectedForBothFieldsAndLocals(string typeName)
    {
        foreach (var visibility in new[] { DesignerMemberVisibility.Private, DesignerMemberVisibility.None })
        {
            var document = new DesignDocument { ClassName = "SafeForm" };
            var node = new DesignControlNode { Name = "body", TypeName = typeName, MemberVisibility = visibility };
            document.Controls.Add(node);
            var generated = new CSharpDesignerGenerator().Generate(document);
            Assert.False(generated.Succeeded);
            Assert.Empty(generated.Code);
            Assert.Equal(typeName, node.TypeName);
        }
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("ModernFormsNext.Button")]
    [InlineData("global::ModernFormsNext.Button")]
    [InlineData("aliases::Widgets.Button")]
    [InlineData("Widgets.Container<System.String, int[]>.Nested<global::System.Int32?>")]
    [InlineData("Widgets.@class< System.String >")]
    public void SupportedNamedTypesRemainVerbatim(string typeName)
    {
        var document = new DesignDocument { ClassName = "SafeForm" };
        document.Controls.Add(new DesignControlNode { Name = "body", TypeName = typeName });
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.True(generated.Succeeded);
        Assert.Contains($"new {typeName}(", generated.Code);
        Assert.Empty(CSharpSyntaxTree.ParseText(generated.Code).GetDiagnostics());
    }

    [Theory]
    [InlineData("DockStyle", "Fill; this.MarkInjectedCode()")]
    [InlineData("DockStyle.Fill; this.MarkInjectedCode(); DockStyle", "Fill")]
    [InlineData("DockStyle", "Fill,,Left")]
    [InlineData("DockStyle", "Fill /* hidden text */")]
    [InlineData("DockStyle\n#error injected", "Fill")]
    public void EnumSourceTextFailsClosedIncludingNestedValues(string typeName, string memberName)
    {
        var value = DesignPropertyValue.FromEnum(typeName, memberName);
        Assert.Throws<FormatException>(() => CSharpLiteralWriter.WriteValue(value));
        var document = new DesignDocument { ClassName = "SafeForm" };
        document.Properties["Dock"] = value;
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);

        document.Properties.Clear();
        document.Properties["Geometry"] = DesignPropertyValue.FromStructuredObject(
            "ModernFormsNext.Drawing.LineGeometry",
            new Dictionary<string, DesignPropertyValue> { ["StartPoint"] = value });
        generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
    }

    [Theory]
    [InlineData("Fill, Left", "DockStyle.Fill | DockStyle.Left")]
    [InlineData("-1", "(DockStyle)-1")]
    [InlineData("18446744073709551615", "(DockStyle)18446744073709551615")]
    public void EnumFlagsAndUnnamedNumericValuesRemainSupported(string value, string expected)
        => Assert.Equal(expected, CSharpLiteralWriter.WriteValue(DesignPropertyValue.FromEnum("DockStyle", value)));

    [Fact]
    public void StructuredObjectTypeNamesCannotContributeSourceText()
    {
        var document = new DesignDocument { ClassName = "SafeForm" };
        document.Properties["Geometry"] = DesignPropertyValue.FromStructuredObject(
            "LineGeometry(); this.MarkInjectedCode(); new LineGeometry",
            new Dictionary<string, DesignPropertyValue>());
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
    }
}
