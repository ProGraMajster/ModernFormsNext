using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class ReverseSynchronizationFidelityTests
{
    [Fact]
    public void SupportedStaticFormsNormalizeAndRoundTrip()
    {
        const string code = """
            namespace Example;
            public partial class MainForm
            {
                private void InitializeComponent()
                {
                    this.Name = ("MainForm");
                    this.ClientSize = new((480), 320);
                    this.Text = ("Reviewed text");
                }
            }
            """;
        var parser = new CSharpDesignerParser();
        var result = parser.Parse(code);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var document = Assert.IsType<DesignDocument>(result.Document);
        Assert.Equal(new DesignSize(480, 320), document.Size);
        Assert.Equal("Reviewed text", document.Properties["Text"].GetString());
        var canonical = new CSharpDesignerGenerator().Generate(document);
        Assert.True(canonical.Succeeded);
        var reopened = parser.Parse(canonical.Code);
        Assert.True(reopened.Success);
        Assert.Equal(canonical.Code, new CSharpDesignerGenerator().Generate(reopened.Document!).Code);
    }

    [Theory]
    [InlineData("this.Text = string.Concat(\"edited\", \" text\");")]
    [InlineData("this.ClientSize = new(width: 480, height: 320);")]
    [InlineData("this.ClientSize = new Example.OwnSize(480, 320);")]
    public void UnsupportedValueIsPartialWithAnEndExclusiveSourceSpan(string statement)
    {
        string source = "partial class MainForm\n{\n    void InitializeComponent()\n    {\n        " + statement + "\n    }\n}";
        var result = new CSharpDesignerParser().Parse(source);
        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CSharpDesignerDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(5, diagnostic.Line);
        Assert.Equal(5, diagnostic.EndLine);
        Assert.True(diagnostic.EndColumn > diagnostic.Column);
        string line = source.Split('\n')[4];
        Assert.Equal(diagnostic.Syntax, line[(diagnostic.Column!.Value - 1)..(diagnostic.EndColumn!.Value - 1)]);
    }

    [Fact]
    public void MalformedAndOverBudgetSyntaxCannotProduceAnApplicableProjection()
    {
        var parser = new CSharpDesignerParser();
        var malformed = parser.Parse("partial class MainForm { void InitializeComponent() { this.Text = ; } }");
        Assert.False(malformed.Success);
        Assert.False(malformed.IsPartial);
        Assert.NotNull(malformed.Diagnostics[0].EndColumn);

        var deep = parser.Parse("partial class MainForm { void InitializeComponent() { this.Text = "
            + new string('(', 300) + "\"Text\"" + new string(')', 300) + "; } }");
        Assert.False(deep.Success);
        Assert.Contains(deep.Diagnostics, diagnostic => diagnostic.Message.Contains("syntax budget", StringComparison.Ordinal));
    }
}
