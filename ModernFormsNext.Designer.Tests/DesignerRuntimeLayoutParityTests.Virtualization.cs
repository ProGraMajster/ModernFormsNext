using System.ComponentModel;
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
    public void VirtualizationPropertyGridPersistenceCodegenAndRuntimeUseSameSettings(
        ItemsLayout layout, Orientation orientation, bool variable)
    {
        var document = BasicDocument();
        var node = Node("list", nameof(ListView), 20, 25, 240, 120);
        document.Controls.Add(node);
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, nameof(ListView.VirtualizationEnabled), "True");
        Commit(grid, nameof(ListView.ItemsLayout), layout.ToString());
        Commit(grid, nameof(ListView.ItemOrientation), orientation.ToString());
        Commit(grid, nameof(ListView.ItemSize), "80, 24");
        Commit(grid, nameof(ListView.CacheItemCount), "4");
        Commit(grid, nameof(ListView.VariableItemSize), variable.ToString());

        Assert.DoesNotContain(grid.Properties, property => property.Name is
            nameof(ListView.ItemCount) or nameof(ListView.RealizedRange) or nameof(ListView.SelectedIndex));
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"virtualization-{Guid.NewGuid():N}.mfdesign");
        try
        {
            DesignDocumentSerializer.Default.Save(path, document);
            var reopened = DesignDocumentSerializer.Default.Load(path);
            var generated = new CSharpDesignerGenerator().Generate(reopened);
            Assert.True(generated.Succeeded);
            Assert.DoesNotContain("Sample ", generated.Code);
            Assert.DoesNotContain("PrepareItemContainer", generated.Code);
            var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
            {
                RootKind = reopened.RootKind, NamespaceOverride = reopened.Namespace, ClassNameOverride = reopened.ClassName
            });
            Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
            var parsedNode = Assert.Single(parsed.Document!.Controls);
            foreach (var name in new[] { "VirtualizationEnabled", "ItemsLayout", "ItemOrientation", "ItemSize", "CacheItemCount", "VariableItemSize" })
                Assert.Equal(DesignerPropertyValueEditor.ToDisplayString(
                    DesignerPropertyValueEditor.FromDesignPropertyValue(node.Properties[name], PropertyType(name))),
                    DesignerPropertyValueEditor.ToDisplayString(
                        DesignerPropertyValueEditor.FromDesignPropertyValue(parsedNode.Properties[name], PropertyType(name))));

            using var generatedRoot = CompileAndCreateGeneratedRoot(reopened);
            DesignerRuntimeLayoutParityHarness.AssertParity("virtualization-codegen", reopened, generatedRoot.Instance);
            var root = Assert.IsAssignableFrom<Control>(generatedRoot.Instance);
            var view = Assert.IsType<ListView>(Assert.Single(root.Controls));
            Assert.True(view.VirtualizationEnabled);
            Assert.Equal(layout, view.ItemsLayout);
            Assert.Equal(orientation, view.ItemOrientation);
            Assert.Equal(new Size(80, 24), view.ItemSize);
            Assert.Equal(4, view.CacheItemCount);
            Assert.Equal(variable, view.VariableItemSize);
            Assert.Empty(view.Items); // Preview data never becomes authored/runtime source data.
            view.SuspendLayout();
            for (int index = 0; index < 100_000; index++) view.Items.Add($"Entry {index}");
            view.ResumeLayout();
            view.ScrollToItem(90_000);
            Assert.NotNull(view.GetItemContainer(90_000));
            Assert.InRange(view.Controls.Count, 1, 40);
            Assert.Equal(100_000, view.ItemCount);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void BuiltInVirtualizationPreviewIsFiniteAndPaintsAtDesignerDpi(double scale)
    {
        using var view = new ListView
        {
            Size = new(200, 120), VirtualizationEnabled = true,
            ItemSize = new(1, 1), CacheItemCount = int.MaxValue
        };
        DesignerVirtualizationPreview.Populate(view);
        Assert.Equal(DesignerVirtualizationPreview.ItemLimit, view.ItemCount);
        Assert.Equal(2, view.CacheItemCount);
        Assert.InRange(view.Controls.Count, 1, DesignerVirtualizationPreview.ItemLimit);
        var rows = view.Controls.ToArray();
        int disposed = 0;
        foreach (var row in rows) row.Disposed += (_, _) => disposed++;
        var info = new SKImageInfo((int)(200 * scale), (int)(120 * scale));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, scale), view, view.Size,
            new Rectangle(0, 0, info.Width, info.Height), out var diagnostics, out var error), error);
        Assert.True(diagnostics.VisibleSampleCount > 0);
        view.Dispose();
        Assert.Equal(rows.Length, disposed);
    }

    [Fact]
    public void BuiltInListPreviewUsesProductionRendererWithoutChangingDocument()
    {
        var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("list", nameof(ListView), 20, 25, 240, 120);
        node.Properties["VirtualizationEnabled"] = DesignPropertyValue.FromBoolean(true);
        node.Properties["CacheItemCount"] = DesignPropertyValue.FromInt32(100_000);
        document.Controls.Add(node);
        session.LoadDocument(document);
        var info = new SKImageInfo(800, 600);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        new DesignerSurfaceRenderer().Render(new PaintEventArgs(info, canvas, 1), session, 800, 600);
        Assert.Contains(session.OutputLines, line => line.Contains("Runtime rendered list as ListView", StringComparison.Ordinal));
        Assert.Equal(100_000, node.Properties["CacheItemCount"].Value);
        Assert.DoesNotContain(node.Properties.Keys, key => key is "Items" or "ItemsSource");
    }

    private static Type PropertyType(string name) => typeof(ListView).GetProperty(name)!.PropertyType;

    [Theory]
    [InlineData("CacheItemCount", "-1")]
    [InlineData("ItemSize", "0, 24")]
    [InlineData("VariableItemSize", "True")]
    public void InvalidVirtualizationGridEditRollsBackWithoutCreatingUndoEntry(string propertyName, string value)
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("list", nameof(ListView), 0, 0, 240, 120);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        var property = Assert.Single(grid.Properties, candidate => candidate.Name == propertyName);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue(value));
        Assert.False(node.Properties.ContainsKey(propertyName));
        Assert.False(session.Transactions.CanUndo);
        Assert.Contains(session.OutputLines, line => line.Contains("was not changed", StringComparison.Ordinal));
    }

    [Fact]
    public void VirtualizationSettingsUseCanonicalUndoRedoAndRejectIncompatibleWrap()
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("list", nameof(ListView), 0, 0, 240, 120);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, "ItemsLayout", "Stack");
        Commit(grid, "VariableItemSize", "True");
        Assert.True(session.Transactions.Undo());
        Assert.False(node.Properties.ContainsKey("VariableItemSize"));
        Assert.True(session.Transactions.Redo());
        Assert.Equal(true, node.Properties["VariableItemSize"].Value);
        grid.Refresh();
        var property = Assert.Single(grid.Properties, candidate => candidate.Name == "ItemsLayout");
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue("Wrap"));
        Assert.Equal("Stack", node.Properties["ItemsLayout"].Value);
    }
}
