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
    [InlineData(nameof(CarouselView))]
    [InlineData(nameof(RefreshView))]
    public void CollectionUtilitySettingsRoundTripThroughGridMfdesignCodeAndRuntime(string type)
    {
        var document = BasicDocument();
        var node = Node("utility", type, 20, 25, 280, 180);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        if (type == nameof(CarouselView))
        {
            Commit(grid, "PageOrientation", "Vertical");
            Commit(grid, "CacheItemCount", "4");
        }
        else
        {
            Commit(grid, "PullThreshold", "96");
            Commit(grid, "HeaderHeight", "40");
            Commit(grid, "CancelText", "Stop");
        }
        Assert.DoesNotContain(grid.Properties, p => p.Name is "ItemsSource" or "ItemTemplate" or
            "SelectedIndex" or "SelectedItem" or "PageSize" or "Command" or "CommandParameter" or
            "CommandTarget" or "IsRefreshing" or "CanCancelRefresh" or "CanRequestRefresh" or "PullProgress");
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"collection-utility-{Guid.NewGuid():N}.mfdesign");
        try
        {
            DesignDocumentSerializer.Default.Save(path, document);
            var reopened = DesignDocumentSerializer.Default.Load(path);
            var generated = new CSharpDesignerGenerator().Generate(reopened);
            Assert.True(generated.Succeeded);
            Assert.DoesNotContain("ItemsSource =", generated.Code);
            Assert.DoesNotContain("ItemTemplate =", generated.Code);
            Assert.DoesNotContain("Command =", generated.Code);
            var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
            {
                RootKind = reopened.RootKind, NamespaceOverride = reopened.Namespace, ClassNameOverride = reopened.ClassName
            });
            Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
            using var generatedRoot = CompileAndCreateGeneratedRoot(parsed.Document!);
            DesignerRuntimeLayoutParityHarness.AssertParity("collection-utility-codegen", reopened, generatedRoot.Instance);
            var root = Assert.IsAssignableFrom<Control>(generatedRoot.Instance);
            var utility = Assert.Single(root.Controls);
            Assert.Empty(utility.Controls); // Constructor-owned implicit UI must not become authored children.
            if (utility is CarouselView carousel)
            {
                Assert.Equal(Orientation.Vertical, carousel.PageOrientation);
                Assert.Equal(4, carousel.CacheItemCount);
                Assert.Null(carousel.ItemsSource);
                Assert.Null(carousel.ItemTemplate);
                carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
                carousel.SelectedIndex = 90_000;
                Assert.Equal(100_003, carousel.ItemCount);
                Assert.Equal(90_000, carousel.SelectedItem);
            }
            else
            {
                var refresh = Assert.IsType<RefreshView>(utility);
                Assert.Equal(96, refresh.PullThreshold);
                Assert.Equal(40, refresh.HeaderHeight);
                Assert.Equal("Stop", refresh.CancelText);
                Assert.Null(refresh.Command);
                Assert.False(refresh.IsRefreshing);
            }
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(nameof(CarouselView), "CacheItemCount", "-1")]
    [InlineData(nameof(CarouselView), "PageOrientation", "42")]
    [InlineData(nameof(RefreshView), "PullThreshold", "0")]
    [InlineData(nameof(RefreshView), "HeaderHeight", "-1")]
    public void InvalidUtilityMetricRollsBackTheExistingGridTransaction(string type, string propertyName, string value)
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("utility", type, 0, 0, 280, 180);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        var property = Assert.Single(grid.Properties, p => p.Name == propertyName);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue(value));
        Assert.False(node.Properties.ContainsKey(propertyName));
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2.625d)]
    public void CarouselPreviewIsFinitePaintsProductionPagesAndDisposesThem(double scale)
    {
        using var carousel = new CarouselView { Size = new(280, 180), CacheItemCount = int.MaxValue };
        DesignerVirtualizationPreview.Populate(carousel);
        Assert.Equal(12, carousel.ItemCount);
        Assert.Equal(2, carousel.CacheItemCount);
        Assert.Null(carousel.ItemTemplate);
        var range = carousel.GetVirtualizationSnapshot().RealizedRange;
        var rows = Enumerable.Range(range.StartIndex, range.Count).Select(carousel.GetItemContainer).OfType<Control>().ToArray();
        Assert.InRange(rows.Length, 1, 5);
        int disposed = 0;
        foreach (var row in rows) row.Disposed += (_, _) => disposed++;
        var info = new SKImageInfo((int)(280 * scale), (int)(180 * scale));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, scale), carousel, carousel.Size,
            new Rectangle(0, 0, info.Width, info.Height), out var diagnostics, out var error), error);
        Assert.True(diagnostics.VisibleSampleCount > 0);
        carousel.Dispose();
        Assert.Equal(rows.Length, disposed);
    }

    [Fact]
    public void UtilityToolboxAndUndoRedoUseCanonicalDesignerContracts()
    {
        var items = new DesignerToolboxService().GetItems();
        Assert.Contains(items, item => item.TypeName == nameof(CarouselView));
        Assert.Contains(items, item => item.TypeName == nameof(RefreshView));
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("refresh", nameof(RefreshView), 0, 0, 280, 180);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, "PullThreshold", "96");
        Assert.True(session.Transactions.Undo());
        Assert.False(node.Properties.ContainsKey("PullThreshold"));
        Assert.True(session.Transactions.Redo());
        Assert.Equal(96, node.Properties["PullThreshold"].Value);
    }
}
