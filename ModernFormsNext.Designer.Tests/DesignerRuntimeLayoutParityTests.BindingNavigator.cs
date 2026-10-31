using ModernFormsNext.DataBinding;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Fact]
    public void NavigatorScalarPropertyGridUsesExistingUndoRedoAndPersistence()
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("navigator", typeof(BindingNavigator).FullName!, 0, 0, 760, 42);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Assert.DoesNotContain(grid.Properties, property => property.Name == nameof(BindingNavigator.BindingSource));
        Assert.DoesNotContain(grid.Properties, property => property.Name == nameof(BindingNavigator.PositionTextBox));
        Commit(grid, nameof(BindingNavigator.CountItemFormat), "Records: {0}");
        Assert.Equal("Records: {0}", node.Properties[nameof(BindingNavigator.CountItemFormat)].Value);
        Assert.True(session.Transactions.Undo());
        Assert.False(node.Properties.ContainsKey(nameof(BindingNavigator.CountItemFormat)));
        Assert.True(session.Transactions.Redo());
        var reopened = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        Assert.Equal("Records: {0}", reopened.Controls[0].Properties[nameof(BindingNavigator.CountItemFormat)].Value);
    }

    [Theory]
    [InlineData(DesignRootKind.Form)]
    [InlineData(DesignRootKind.UserControl)]
    public void GeneratedBindingNavigatorUsesOnlyFrameworkConstructionAndScalarData(DesignRootKind kind)
    {
        var document = new DesignDocument
        {
            Namespace = "NavigatorGenerated", ClassName = "ContentView", FormName = "ContentView",
            RootKind = kind, Size = new(820, 180)
        };
        var node = new DesignControlNode
        {
            Name = "navigator", TypeName = typeof(BindingNavigator).FullName!, Bounds = new(0, 0, 760, 42)
        };
        node.Properties[nameof(BindingNavigator.CountItemFormat)] = DesignPropertyValue.FromString("Records: {0}");
        document.Controls.Add(node);
        using var generated = CompileAndCreateGeneratedRoot(document);
        Assert.NotNull(generated.Instance);
        DesignerRuntimeLayoutParityHarness.AssertParity($"navigator-generated-{kind}", document, generated.Instance);
    }
}
