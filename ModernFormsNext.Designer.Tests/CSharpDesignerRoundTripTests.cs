using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class CSharpDesignerRoundTripTests
{
    [Fact]
    public void GeneratorUsesClientSizeAndNeverEmitsDecoratedWindowSize()
    {
        var document = CreateDocument();
        document.Properties["ClientSize"] = DesignPropertyValue.FromInt32(123);

        var result = new CSharpDesignerGenerator().Generate(document);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Validation.Errors));
        Assert.Contains("this.ClientSize = new System.Drawing.Size(900, 600);", result.Code);
        Assert.DoesNotContain("this.Size =", result.Code);
    }

    [Theory]
    [InlineData("Size")]
    [InlineData("ClientSize")]
    public void ParserAcceptsCurrentAndLegacyFormSizeAssignments(string propertyName)
    {
        var source = $$"""
            using ModernFormsNext;

            namespace Example;

            public partial class MainForm
            {
                private void InitializeComponent()
                {
                    this.Name = "MainForm";
                    this.Text = "MainForm";
                    this.{{propertyName}} = new System.Drawing.Size(900, 600);
                }
            }
            """;

        var result = new CSharpDesignerParser().Parse(source);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(new DesignSize(900, 600), Assert.IsType<DesignDocument>(result.Document).Size);
    }

    [Fact]
    public void RepeatedMfdesignAndCodeRoundTripsKeepLogicalGeometry()
    {
        var serializer = DesignDocumentSerializer.Default;
        var service = new CSharpDesignerRoundTripService();
        var document = CreateDocument();
        var expectedBounds = document.Controls[0].Bounds;

        for (var iteration = 0; iteration < 5; iteration++)
        {
            document = serializer.Deserialize(serializer.Serialize(document));
            var generated = service.Generate(document);
            Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
            Assert.Contains("this.ClientSize = new System.Drawing.Size(900, 600);", generated.Code);
            Assert.DoesNotContain("this.Size =", generated.Code);

            var parsed = service.ParseDesignerCode(generated.Code);
            Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
            document = Assert.IsType<DesignDocument>(parsed.Document);

            Assert.Equal(new DesignSize(900, 600), document.Size);
            Assert.Equal(expectedBounds, Assert.Single(document.Controls).Bounds);
        }
    }

    private static DesignDocument CreateDocument()
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(900, 600)
        };

        document.Controls.Add(new DesignControlNode
        {
            TypeName = "Button",
            Name = "button1",
            Bounds = new DesignBounds(40, 50, 120, 36),
            MemberVisibility = DesignerMemberVisibility.Private
        });

        return document;
    }
}
