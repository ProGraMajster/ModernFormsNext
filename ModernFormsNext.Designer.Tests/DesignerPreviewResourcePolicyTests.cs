using System.Drawing;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerPreviewResourcePolicyTests
{
    private static readonly DesignerPreviewResourcePolicy OrdinaryLimitsDisabled = new()
    { MaximumTableStrips = 0, MaximumTableCells = 0 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrustedHostCanRaiseOrDisableOrdinaryLimits(bool disable)
    {
        var (_, table) = Table();
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(257);
        var policy = disable ? OrdinaryLimitsDisabled : new DesignerPreviewResourcePolicy { MaximumTableStrips = 512 };
        using var preview = DesignerPagePreview.TryCreate(table, new Size(260, 100), out string? error, resourcePolicy: policy);
        Assert.Null(error);
        Assert.NotNull(preview);
        Assert.Equal(257, Assert.IsType<TableLayoutPanel>(preview.Root).ColumnCount);
    }

    [Theory]
    [InlineData("TableLayoutPanel")]
    [InlineData("ModernFormsNext.TableLayoutPanel")]
    [InlineData("global::ModernFormsNext.TableLayoutPanel")]
    [InlineData(" TableLayoutPanel ")]
    [InlineData(" ModernFormsNext.TableLayoutPanel ")]
    public void AllResolvedTableNamesUseTheSamePolicyAndLazyRendering(string typeName)
    {
        var (document, table) = Table();
        table.TypeName = typeName;
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(257);
        AssertDeferred(document, table, DesignerPreviewResourcePolicy.Default, "configured resource policy");
        using var session = new DesignerSession();
        session.LoadDocument(document);
        var info = new SKImageInfo(800, 600, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        new DesignerSurfaceRenderer().Render(new PaintEventArgs(info, canvas, 1), session, 800, 600);
        Assert.Contains(session.OutputLines, line => line.Contains("configured resource policy", StringComparison.Ordinal));
        using var raised = DesignerPagePreview.TryCreate(table, new Size(260, 100), out string? error,
            resourcePolicy: OrdinaryLimitsDisabled);
        Assert.NotNull(raised);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("ColumnCount")]
    [InlineData("RowCount")]
    public void HardSafetyCeilingRemainsWhenOrdinaryLimitsAreDisabled(string property)
    {
        var (document, table) = Table();
        table.Properties[property] = DesignPropertyValue.FromInt32(DesignerPreviewResourcePolicy.HardMaximumTableStrips + 1);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "hard resource safety budget");
    }

    [Theory]
    [InlineData("TableColumn", 256)]
    [InlineData("TableRow", 256)]
    [InlineData("TableColumnSpan", 257)]
    [InlineData("TableRowSpan", 257)]
    public void PlacementsAndSpansCannotBypassOrdinaryExtentBudget(string property, int value)
    {
        var (document, table) = Table();
        table.Children[0].Properties[property] = DesignPropertyValue.FromInt32(value);
        AssertDeferred(document, table, DesignerPreviewResourcePolicy.Default, "configured resource policy");
    }

    [Theory]
    [InlineData("AddRows", "TableRowSpan")]
    [InlineData("AddColumns", "TableColumnSpan")]
    public void CollisionAndAutomaticGrowthAreBoundedBeforeLayout(string growStyle, string spanProperty)
    {
        var (document, table) = Table();
        table.Properties["GrowStyle"] = DesignPropertyValue.FromEnum(typeof(TableLayoutPanelGrowStyle).FullName!, growStyle);
        table.Children.Add(new DesignControlNode { Name = "second", TypeName = "Button", Bounds = new(0, 0, 25, 20) });
        // Each span fits alone. Their combined collision/growth envelope exceeds this small policy.
        foreach (var child in table.Children) child.Properties[spanProperty] = DesignPropertyValue.FromInt32(3);
        AssertDeferred(document, table, new DesignerPreviewResourcePolicy { MaximumTableStrips = 5 }, "configured resource policy");
    }

    [Fact]
    public void AddColumnsAreaEstimateIsAlsoIncludedInTheBudget()
    {
        var (document, table) = Table();
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(1);
        table.Properties["GrowStyle"] = DesignPropertyValue.FromEnum(typeof(TableLayoutPanelGrowStyle).FullName!, "AddColumns");
        table.Children[0].Properties["TableRowSpan"] = DesignPropertyValue.FromInt32(4);
        AssertDeferred(document, table, new DesignerPreviewResourcePolicy { MaximumTableCells = 15 }, "configured resource policy");
    }

    [Theory]
    [InlineData("ColumnCount", -1)]
    [InlineData("RowCount", -1)]
    [InlineData("TableRow", -2)]
    [InlineData("TableColumn", -2)]
    [InlineData("TableRowSpan", 0)]
    [InlineData("TableColumnSpan", 0)]
    public void InvalidCountPlacementAndSpanRangesAreRejectedBeforeSetters(string property, int value)
    {
        var (document, table) = Table();
        (property.StartsWith("Table", StringComparison.Ordinal) ? table.Children[0] : table)
            .Properties[property] = DesignPropertyValue.FromInt32(value);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "requires an Int32");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongNumericKindsAndUnvalidatedSettingsPathsDoNotReachSetters(bool settingsPath)
    {
        var (document, table) = Table();
        table.Properties[settingsPath ? "LayoutSettings.ColumnCount" : "ColumnCount"] = settingsPath
            ? DesignPropertyValue.FromInt32(3) : DesignPropertyValue.FromString("3");
        AssertDeferred(document, table, OrdinaryLimitsDisabled, settingsPath ? "canonical" : "requires an Int32");
    }

    [Fact]
    public void SpanEndpointOverflowIsDiagnosedUsingDataOnly()
    {
        var (document, table) = Table();
        table.Children[0].Properties["TableColumn"] = DesignPropertyValue.FromInt32(int.MaxValue);
        table.Children[0].Properties["TableColumnSpan"] = DesignPropertyValue.FromInt32(2);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "endpoints must be representable");
    }

    [Theory]
    [InlineData("ModernFormsNext.TableLayoutPanelGrowStyle", "Sideways")]
    [InlineData("ModernFormsNext.TableLayoutPanelGrowStyle", "1")]
    [InlineData("Project.TableLayoutPanelGrowStyle", "AddRows")]
    public void GrowStyleRemainsAClosedDataSelection(string type, string value)
    {
        var (document, table) = Table();
        table.Properties["GrowStyle"] = DesignPropertyValue.FromEnum(type, value);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "declared GrowStyle");
    }

    [Fact]
    public void FixedTableSpanOverflowIsDiagnosedBeforePlacement()
    {
        var (document, table) = Table();
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(2);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(2);
        table.Properties["GrowStyle"] = DesignPropertyValue.FromEnum(typeof(TableLayoutPanelGrowStyle).FullName!, "FixedSize");
        table.Children[0].Properties["TableColumn"] = DesignPropertyValue.FromInt32(1);
        table.Children[0].Properties["TableRow"] = DesignPropertyValue.FromInt32(0);
        table.Children[0].Properties["TableColumnSpan"] = DesignPropertyValue.FromInt32(2);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "fixed table placement");
    }

    [Fact]
    public void SpanAreaArithmeticCannotWrapOrDisableHardBudgets()
    {
        var (document, table) = Table();
        // Only the pure data preflight runs; no strip or reservation grid is constructed.
        table.Children[0].Properties["TableColumnSpan"] = DesignPropertyValue.FromInt32(int.MaxValue);
        table.Children[0].Properties["TableRowSpan"] = DesignPropertyValue.FromInt32(int.MaxValue);
        var second = new DesignControlNode { Name = "second", TypeName = "Button", Bounds = new(0, 0, 25, 20) };
        second.Properties["TableColumnSpan"] = DesignPropertyValue.FromInt32(int.MaxValue);
        second.Properties["TableRowSpan"] = DesignPropertyValue.FromInt32(int.MaxValue);
        table.Children.Add(second);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "arithmetic safety budget");
    }

    [Fact]
    public void NestedAndSiblingTablesShareTheProjectionBudget()
    {
        var (document, table) = Table();
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(4);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(4);
        var (_, sibling) = Table();
        sibling.Name = "sibling";
        sibling.Children[0].Name = "otherChild";
        sibling.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(4);
        sibling.Properties["RowCount"] = DesignPropertyValue.FromInt32(4);
        document.Controls.Add(sibling);
        var policy = new DesignerPreviewResourcePolicy { MaximumTableCells = 31 };
        var layout = new DesignerLayoutEngine().Layout(document, policy);
        Assert.Equal(2, layout.PreviewDiagnostics.Count);
        var (_, outer) = Table();
        outer.Name = "outer";
        outer.Children.Clear();
        outer.Children.Add(table);
        outer.Children.Add(sibling);
        using var preview = DesignerPagePreview.TryCreate(outer, new Size(260, 100), out string? error, resourcePolicy: policy);
        Assert.Null(preview);
        Assert.Contains("configured resource policy", error);
    }

    [Fact]
    public void HardCellAndWorkBudgetsRemainIndependentOfStripBudget()
    {
        var (document, table) = Table();
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(513);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(513);
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "hard resource safety budget");
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(256);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(256);
        table.Properties["GrowStyle"] = DesignPropertyValue.FromEnum(typeof(TableLayoutPanelGrowStyle).FullName!, "AddColumns");
        // More than one child makes the conservative repeated-placement work exceed the ceiling.
        table.Children.Add(new DesignControlNode { Name = "second", TypeName = "Button", Bounds = new(0, 0, 25, 20) });
        AssertDeferred(document, table, OrdinaryLimitsDisabled, "hard resource safety budget");
    }

    [Fact]
    public void OrdinaryBoundaryAndRuntimeCountContractArePreserved()
    {
        var (_, table) = Table();
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(256);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(256);
        using var preview = DesignerPagePreview.TryCreate(table, new Size(260, 100), out string? error);
        Assert.NotNull(preview);
        Assert.Null(error);
        // Exercise only the count setters under layout suspension; no large runtime layout runs.
        using var runtime = new TableLayoutPanel();
        runtime.SuspendLayout();
        runtime.ColumnCount = DesignerPreviewResourcePolicy.HardMaximumTableStrips + 1;
        runtime.RowCount = DesignerPreviewResourcePolicy.HardMaximumTableStrips + 1;
        Assert.Equal(4097, runtime.ColumnCount);
        Assert.Equal(4097, runtime.RowCount);
        runtime.ResumeLayout(false);
    }

    [Fact]
    public void SessionPolicyChangesPresentationWithoutEditingTheDocument()
    {
        using var session = new DesignerSession();
        string before = DesignDocumentSerializer.Default.Serialize(session.Document);
        int settings = 0;
        session.SettingsChanged += (_, _) => settings++;
        session.PreviewResourcePolicy = OrdinaryLimitsDisabled;
        Assert.Same(OrdinaryLimitsDisabled, session.PreviewResourcePolicy);
        Assert.Equal(1, settings);
        Assert.False(session.IsDirty);
        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesignerPreviewResourcePolicy { MaximumTableStrips = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesignerPreviewResourcePolicy { MaximumTableCells = -1 });
    }

    [Fact]
    public void LazyPlaceholderPaintAndHitTestingRetainTheSameAdmissionDecision()
    {
        var (document, table) = Table();
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(257);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        var info = new SKImageInfo(800, 600, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        var renderer = new DesignerSurfaceRenderer();
        renderer.Render(new PaintEventArgs(info, canvas, 1), session, 800, 600);
        Assert.Contains(session.OutputLines, line => line.Contains("configured resource policy", StringComparison.Ordinal));
        var layout = new DesignerLayoutEngine().Layout(session.Document, session.PreviewResourcePolicy);
        Assert.False(layout.IsPointInsideVisibleParentChain(session.Document, table.Children[0], new DesignPoint(5, 5)));
        session.PreviewResourcePolicy = OrdinaryLimitsDisabled;
        layout = new DesignerLayoutEngine().Layout(session.Document, session.PreviewResourcePolicy);
        Assert.Empty(layout.PreviewDiagnostics);
        Assert.True(layout.GetVisibleBounds(session.Document, table.Children[0]).Width > 0);
        renderer.Render(new PaintEventArgs(info, canvas, 1), session, 800, 600);
    }

    [Fact]
    public void SettingsDialogAppliesTrustedProjectLimitsToTheExistingSession()
    {
        using var session = new DesignerSession();
        var options = new ModernFormsDesignerOptions { Language = DesignerLanguage.English };
        using var dialog = new DesignerSettingsDialog(options, session);
        var strips = Assert.Single(dialog.Controls.Cast<Control>().OfType<NumericUpDown>(), control => control.Name == "TablePreviewStrips");
        var cells = Assert.Single(dialog.Controls.Cast<Control>().OfType<NumericUpDown>(), control => control.Name == "TablePreviewCells");
        strips.Value = 512;
        cells.Value = 0;
        Assert.Single(dialog.Controls.Cast<Control>().OfType<Button>(), control => control.Text == "OK").PerformClick();
        Assert.Same(options.PreviewResourcePolicy, session.PreviewResourcePolicy);
        Assert.Equal(512, session.PreviewResourcePolicy.MaximumTableStrips);
        Assert.Equal(0, session.PreviewResourcePolicy.MaximumTableCells);
        Assert.False(session.IsDirty);
    }

    private static void AssertDeferred(DesignDocument document, DesignControlNode table,
        DesignerPreviewResourcePolicy policy, string diagnostic)
    {
        string before = DesignDocumentSerializer.Default.Serialize(document);
        using var preview = DesignerPagePreview.TryCreate(table, new Size(260, 100), out string? error, resourcePolicy: policy);
        Assert.Null(preview);
        Assert.Contains(diagnostic, error);
        var layout = new DesignerLayoutEngine().Layout(document, policy);
        Assert.Contains(diagnostic, layout.PreviewDiagnostics[table]);
        Assert.Equal(0, layout.GetVisibleBounds(document, table.Children[0]).Width);
        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(document));
    }

    private static (DesignDocument Document, DesignControlNode Table) Table()
    {
        var document = new DesignDocument { Namespace = "SafeTableData", ClassName = "ContentView",
            FormName = "ContentView", Size = new(320, 240) };
        var table = new DesignControlNode { Name = "table", TypeName = "TableLayoutPanel", Bounds = new(0, 0, 260, 100) };
        table.Children.Add(new DesignControlNode { Name = "child", TypeName = "Button", Bounds = new(0, 0, 25, 20) });
        document.Controls.Add(table);
        return (document, table);
    }
}
