using System.Drawing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData(ItemsLayout.Stack, Orientation.Vertical, true)]
    [InlineData(ItemsLayout.Stack, Orientation.Horizontal, false)]
    [InlineData(ItemsLayout.Wrap, Orientation.Vertical, false)]
    [InlineData(ItemsLayout.Wrap, Orientation.Horizontal, false)]
    public void CollectionViewSettingsRoundTripThroughPropertyGridMfdesignCodegenAndCompiledRuntime(
        ItemsLayout layout, Orientation direction, bool variable)
    {
        var document = BasicDocument();
        var node = Node("collection", nameof(CollectionView), 20, 25, 240, 120);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, "ItemsLayout", layout.ToString());
        Commit(grid, "ItemOrientation", direction.ToString());
        Commit(grid, "ItemSize", "80, 24");
        Commit(grid, "CacheItemCount", "4");
        Commit(grid, "VariableItemSize", variable.ToString());
        Assert.DoesNotContain(grid.Properties, property => property.Name is "ItemsSource" or "ItemTemplate" or "SelectedItem" or "SelectedIndex");
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"collection-{Guid.NewGuid():N}.mfdesign");
        try
        {
            DesignDocumentSerializer.Default.Save(path, document);
            var reopened = DesignDocumentSerializer.Default.Load(path);
            var generated = new CSharpDesignerGenerator().Generate(reopened);
            Assert.True(generated.Succeeded);
            Assert.DoesNotContain("Sample ", generated.Code);
            Assert.DoesNotContain("ItemsSource =", generated.Code);
            Assert.DoesNotContain("ItemTemplate =", generated.Code);
            var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
            {
                RootKind = reopened.RootKind, NamespaceOverride = reopened.Namespace, ClassNameOverride = reopened.ClassName
            });
            Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
            using var generatedRoot = CompileAndCreateGeneratedRoot(parsed.Document!);
            DesignerRuntimeLayoutParityHarness.AssertParity("collection-view-codegen", reopened, generatedRoot.Instance);
            var control = Assert.IsAssignableFrom<Control>(generatedRoot.Instance);
            var view = Assert.IsType<CollectionView>(Assert.Single(control.Controls));
            Assert.Null(view.ItemsSource);
            Assert.Null(view.ItemTemplate);
            Assert.Equal(layout, view.ItemsLayout);
            Assert.Equal(direction, view.ItemOrientation);
            Assert.Equal(new Size(80, 24), view.ItemSize);
            Assert.Equal(4, view.CacheItemCount);
            Assert.Equal(variable, view.VariableItemSize);
            view.ItemsSource = Enumerable.Range(0, 100_003).Select(index => (object)$"Entry {index}").ToArray();
            view.ScrollToItem(90_000);
            Assert.NotNull(view.GetItemContainer(90_000));
            Assert.InRange(view.Controls.Count, 1, 40);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2.625d)]
    public void CollectionViewDesignerPreviewIsBoundedUsesProductionRowsAndDisposesThem(double scale)
    {
        using var view = new CollectionView { Size = new(240, 120), CacheItemCount = int.MaxValue, ItemSize = new(1, 1) };
        DesignerVirtualizationPreview.Populate(view);
        Assert.Equal(12, view.ItemCount);
        Assert.Null(view.ItemTemplate);
        Assert.Equal(2, view.CacheItemCount);
        Assert.InRange(view.Controls.Count, 1, 12);
        var rows = view.Controls.ToArray();
        int disposed = 0;
        foreach (var row in rows) row.Disposed += (_, _) => disposed++;
        var info = new SKImageInfo((int)(240 * scale), (int)(120 * scale));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, scale), view, view.Size,
            new Rectangle(0, 0, info.Width, info.Height), out var diagnostics, out var error), error);
        Assert.True(diagnostics.VisibleSampleCount > 0);
        view.Dispose();
        Assert.Equal(rows.Length, disposed);
    }

    [Theory]
    [InlineData("CacheItemCount", "-1")]
    [InlineData("ItemSize", "0, 24")]
    [InlineData("VariableItemSize", "True")]
    public void CollectionViewInvalidGridEditRollsBackCanonicalTransaction(string propertyName, string value)
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("collection", nameof(CollectionView), 0, 0, 240, 120);
        if (propertyName == "VariableItemSize")
            node.Properties["ItemsLayout"] = DesignPropertyValue.FromEnum(typeof(ItemsLayout).FullName!, "Wrap");
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        var property = Assert.Single(grid.Properties, candidate => candidate.Name == propertyName);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue(value));
        Assert.False(node.Properties.ContainsKey(propertyName));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void CollectionViewScalarUndoRedoAndToolboxUseExistingDesignerContracts()
    {
        Assert.Contains(new DesignerToolboxService().GetItems(), item => item.TypeName == nameof(CollectionView));
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("collection", nameof(CollectionView), 0, 0, 240, 120);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, "CacheItemCount", "5");
        Assert.True(session.Transactions.Undo());
        Assert.False(node.Properties.ContainsKey("CacheItemCount"));
        Assert.True(session.Transactions.Redo());
        Assert.Equal(5, node.Properties["CacheItemCount"].Value);
    }
}
