using System.Drawing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerLayoutBudgetRegressionTests
{
    [Theory]
    [InlineData("ColumnCount")]
    [InlineData("RowCount")]
    public void LargeDimensionsRemainDataButDefaultPreviewIsDeferred(string property)
    {
        // User-requested separation supersedes the proposed data rejection. Large legal
        // dimensions remain serializable/generatable; only preview construction is deferred.
        var document = new DesignDocument { Namespace = "SafeTableData", ClassName = "ContentView",
            FormName = "ContentView", Size = new(320, 240) };
        var table = new DesignControlNode { Name = "table", TypeName = "TableLayoutPanel", Bounds = new(0, 0, 260, 100) };
        table.Properties[property] = DesignPropertyValue.FromInt32(257);
        table.Children.Add(new DesignControlNode { Name = "child", TypeName = "Button", Bounds = new(0, 0, 25, 20) });
        document.Controls.Add(table);
        var before = DesignDocumentSerializer.Default.Serialize(document);
        var result = new DesignDocumentValidator().ValidateStructure(document);
        Assert.True(result.IsValid);
        Assert.True(new CSharpDesignerGenerator().Generate(document).Succeeded);
        Assert.Null(DesignerPagePreview.TryCreate(table, new Size(260, 100), out string? error));
        Assert.Contains("configured resource policy", error);
        var layout = new DesignerLayoutEngine().Layout(document);
        Assert.Contains(table, layout.PreviewDiagnostics.Keys);
        Assert.Equal(0, layout.GetVisibleBounds(document, table.Children[0]).Width);
        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(document));
    }
}
