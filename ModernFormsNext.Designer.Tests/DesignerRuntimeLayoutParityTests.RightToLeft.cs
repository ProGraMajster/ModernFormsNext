using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData(FlowDirection.LeftToRight, false)]
    [InlineData(FlowDirection.LeftToRight, true)]
    [InlineData(FlowDirection.RightToLeft, false)]
    [InlineData(FlowDirection.RightToLeft, true)]
    [InlineData(FlowDirection.TopDown, false)]
    [InlineData(FlowDirection.TopDown, true)]
    [InlineData(FlowDirection.BottomUp, false)]
    [InlineData(FlowDirection.BottomUp, true)]
    public void RtlFlowUsesProductionGeometryThroughNestedMixedDirection(FlowDirection direction, bool rtl)
    {
        var document = FlowDocument(direction);
        document.Properties[nameof(Control.RightToLeft)] = EnumValue(rtl ? RightToLeft.Yes : RightToLeft.No);
        var flow = document.Controls[0];
        flow.Properties["Padding"] = PaddingValue(new Padding(7, 11, 13, 17));
        flow.Children[0].Properties["Margin"] = PaddingValue(new Padding(3, 5, 9, 7));
        flow.Children[0].Properties["Text"] = DesignPropertyValue.FromString("مرحبا");
        var nested = Node("nested", "FlowLayoutPanel", 0, 0, 120, 80);
        nested.Properties[nameof(Control.RightToLeft)] = EnumValue(RightToLeft.No);
        nested.Children.Add(Node("hebrew", "Label", 0, 0, 35, 20));
        nested.Children[0].Properties["Text"] = DesignPropertyValue.FromString("שלום");
        flow.Children.Add(nested);
        DesignerRuntimeLayoutParityHarness.AssertParity($"rtl-flow-{direction}-{rtl}", document, document);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RtlTablePreservesSpansAndLogicalCellIndexes(bool rtl)
    {
        var document = TableDocument();
        document.Properties[nameof(Control.RightToLeft)] = EnumValue(rtl ? RightToLeft.Yes : RightToLeft.No);
        var table = document.Controls[0];
        table.Properties["Padding"] = PaddingValue(new Padding(7, 11, 13, 17));
        table.Children.RemoveAt(3);
        table.Children[2].Properties["TableColumnSpan"] = DesignPropertyValue.FromInt32(2);
        table.Children[0].Properties["Margin"] = PaddingValue(new Padding(3, 5, 9, 7));
        DesignerRuntimeLayoutParityHarness.AssertParity($"rtl-table-span-{rtl}", document, document);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void RtlGeometryKeepsTheSameDeviceEdges(double dpi)
        => DesignerRuntimeLayoutParityHarness.AssertDpiParity(new ParityScenario("rtl-dpi", () =>
        {
            var document = FlowDocument();
            document.Properties[nameof(Control.RightToLeft)] = EnumValue(RightToLeft.Yes);
            return document;
        }), dpi);

    [Fact]
    public void InvalidAuthoredDirectionReportsUnavailableGeometryWithoutMutatingTheDocument()
    {
        var document = FlowDocument();
        document.Properties[nameof(Control.RightToLeft)] = DesignPropertyValue.FromEnum(typeof(RightToLeft).FullName!, "Sideways");
        var result = new DesignerLayoutEngine().Layout(document);
        Assert.NotEmpty(result.PreviewDiagnostics);
        Assert.Equal("Sideways", document.Properties[nameof(Control.RightToLeft)].Value);
    }

    [Fact]
    public void ExplicitBoundaryCanResolveItsOwnDirectionAfterInvalidAncestorData()
    {
        var document = FlowDocument();
        document.Properties[nameof(Control.RightToLeft)] = DesignPropertyValue.FromEnum(typeof(RightToLeft).FullName!, "Sideways");
        document.Controls[0].Properties[nameof(Control.RightToLeft)] = EnumValue(RightToLeft.No);
        var result = new DesignerLayoutEngine().Layout(document);
        Assert.Empty(result.PreviewDiagnostics);
        Assert.True(result.GetEffectiveBounds(document.Controls[0].Children[0]).X
            < result.GetEffectiveBounds(document.Controls[0].Children[1]).X);
    }

    [Theory]
    [InlineData(DesignRootKind.UserControl)]
    [InlineData(DesignRootKind.Form)]
    public void RtlAuthoredSelectionRoundTripsAndGeneratedRootCompiles(DesignRootKind kind)
    {
        var document = FlowDocument();
        document.RootKind = kind;
        document.Properties[nameof(Control.RightToLeft)] = EnumValue(RightToLeft.Yes);
        var serializer = DesignDocumentSerializer.Default;
        var reopened = serializer.Deserialize(serializer.Serialize(document));
        Assert.Equal("Yes", reopened.Properties[nameof(Control.RightToLeft)].Value);
        var source = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(source.Succeeded);
        var parsed = new CSharpDesignerParser().Parse(source.Code, new CSharpDesignerParseOptions {
            RootKind = kind, NamespaceOverride = reopened.Namespace, ClassNameOverride = reopened.ClassName
        });
        Assert.True(parsed.Success, string.Join("; ", parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        using var generated = CompileAndCreateGeneratedRoot(Assert.IsType<DesignDocument>(parsed.Document));
        DesignerRuntimeLayoutParityHarness.AssertParity($"rtl-generated-{kind}", reopened, generated.Instance);
    }
}
