using System.Drawing;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;
using System.Runtime.CompilerServices;
using ModernFormsNext.CodeGeneration.CSharp;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Fact]
    public void OptionalGalleryDocumentMatchesGeneratedSourceAndRetainsEditorState()
    {
        var directory = GalleryPageExampleDirectory();
        var document = DesignDocumentSerializer.Default.Deserialize(File.ReadAllText(IOPath.Combine(directory, "DesignedPagesView.mfdesign")));
        var generated = new CSharpDesignerGenerator().Generate(document,
            new CSharpDesignerGenerationOptions { EmitDesignerMetadataHeader = false });
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Equal(File.ReadAllText(IOPath.Combine(directory, "DesignedPagesView.Designer.cs")).ReplaceLineEndings("\n"), generated.Code.ReplaceLineEndings("\n"));
        using var runtime = CompileAndCreateGeneratedRoot(document);
        DesignerRuntimeLayoutParityHarness.AssertPageGeometry("optional-gallery-page-document", document, runtime.Instance);
        var host = Assert.IsType<PageHost>(Assert.Single(Assert.IsAssignableFrom<Control>(runtime.Instance).Controls));
        var tabs = Assert.IsType<TabbedPage>(host.Page);
        var first = Assert.IsType<ContentPage>(tabs.Pages[0]);
        var body = Assert.IsType<Panel>(first.Content);
        var editor = Assert.Single(body.Controls.OfType<TextBox>());
        editor.Text = "Retained after selection";
        tabs.SelectedIndex = 1;
        Assert.IsType<NavigationPage>(tabs.SelectedPage);
        tabs.SelectedIndex = 0;
        Assert.Same(first, tabs.SelectedPage);
        Assert.Equal("Retained after selection", editor.Text);
    }

    private static string GalleryPageExampleDirectory([CallerFilePath] string source = "")
        => IOPath.GetFullPath(IOPath.Combine(IOPath.GetDirectoryName(source)!, "..", "samples", "ControlGallery", "Examples"));

    [Fact]
    public void PagePreviewGeometryUsesCompiledProductionSlotsAndImplicitFrames()
    {
        var document = PagePreviewDocument();
        Assert.Empty(new DesignerLayoutEngine().Layout(document).PreviewDiagnostics);
        using var runtime = CompileAndCreateGeneratedRoot(document);
        DesignerRuntimeLayoutParityHarness.AssertPageGeometry("nested-pages-and-implicit-frames", document, runtime.Instance);
        Assert.Empty(new DesignerLayoutEngine().Layout(document).PreviewDiagnostics);
    }

    [Fact]
    public void InactivePagesAreExcludedFromCanonicalHitTesting()
    {
        var document = PagePreviewDocument();
        using var session = new DesignerSession();
        session.LoadDocument(document);
        var tabs = document.Controls[0].Children[0];
        var inactive = tabs.Children[0];
        var active = tabs.Children[1];
        var layout = new DesignerLayoutEngine().Layout(document);
        Assert.Equal(0, layout.GetVisibleBounds(document, inactive).Width);
        Assert.True(layout.GetVisibleBounds(document, active).Width > 0);
        Assert.False(layout.IsPointInsideVisibleParentChain(document, inactive, new DesignPoint(40, 80)));
        var hit = new DesignerHitTestService(new DesignerCoordinateMapper()).HitTestControl(session, new DesignPoint(40, 100));
        Assert.NotSame(inactive, hit.Node);
        Assert.NotSame(inactive.Children[0], hit.Node);
    }

    [Fact]
    public void SurfaceReparentingUsesTheVisiblePageInsteadOfAnInactiveOverlappingPage()
    {
        var document = PagePreviewDocument();
        var floating = Node("floating", nameof(Button), 560, 410, 30, 25);
        document.Controls.Add(floating);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        var tabs = document.Controls[0].Children[0];
        var menu = tabs.Children[1].Children[0];
        Assert.True(session.ReparentNodeAtDocumentPoint(floating, new DesignPoint(40, 100)));
        Assert.Same(menu, session.FindParent(floating));
        session.Transactions.Undo();
        Assert.Contains(floating, document.Controls);
        Assert.Empty(menu.Children);
    }

    [Theory]
    [InlineData(1d, 1d)]
    [InlineData(1.25d, 0.8d)]
    [InlineData(2d, 0.75d)]
    public void BoundedProductionPagePaintKeepsLifecycleInertAndDisposesRetainedPages(double dpi, double zoom)
    {
        var document = PagePreviewDocument();
        var host = document.Controls[0];
        var preview = DesignerPagePreview.TryCreate(host, new Size(540, 400), out var error);
        Assert.True(preview is not null, error);
        var pages = preview.Controls.Values.OfType<Page>().ToArray();
        int appeared = 0, retired = 0;
        foreach (var page in pages)
        {
            page.Appearing += (_, _) => appeared++;
            page.Disposed += (_, _) => retired++;
        }
        using (preview)
        {
            preview.Root.PerformLayout();
            int width = (int)(540 * dpi * zoom), height = (int)(400 * dpi * zoom);
            var info = new SKImageInfo(width + 20, height + 20);
            using var bitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, dpi), preview.Root,
                new Size(540, 400), new Rectangle(10, 10, width, height), out var diagnostic, out error), error);
            Assert.True(diagnostic.VisibleSampleCount > 0);
            Assert.Equal(0, bitmap.GetPixel(width + 15, height + 15).Alpha);
            Assert.All(pages, page => Assert.False(page.IsPresented));
            Assert.Equal(0, appeared);
        }
        Assert.Equal(pages.Length, retired);
    }

    [Fact]
    public void PreviewRejectsUnknownControlsAndOverBudgetTreesBeforeConstruction()
    {
        var page = Node("content", nameof(ContentPage), 0, 0, 300, 200);
        page.Children.Add(Node("untrusted", "Application.CustomControl", 0, 0, 100, 30));
        Assert.Null(DesignerPagePreview.TryCreate(page, new Size(300, 200), out var error));
        Assert.Contains("known framework", error);
        var document = BasicDocument();
        document.Controls.Add(page);
        var failedLayout = new DesignerLayoutEngine().Layout(document);
        Assert.Contains(page, failedLayout.PreviewDiagnostics.Keys);
        Assert.Equal(0, failedLayout.GetVisibleBounds(document, page.Children[0]).Width);
        foreach (var typeName in new[] { "Button\0", "Button, ModernFormsNext", "ModernFormsNext.Button[]" })
        {
            page.Children[0].TypeName = typeName;
            Assert.Null(DesignerPagePreview.TryCreate(page, new Size(300, 200), out error));
            Assert.Contains("known framework", error);
        }
        page.Children.Clear();
        var panel = Node("body", nameof(Panel), 0, 0, 300, 200);
        page.Children.Add(panel);
        for (int index = 0; index < DesignerPagePreview.MaximumNodes; index++)
            panel.Children.Add(Node($"label{index}", nameof(Label), 0, 0, 100, 20));
        Assert.Null(DesignerPagePreview.TryCreate(page, new Size(300, 200), out error));
        Assert.Contains("256", error);
        panel.Children.Clear();
        var current = panel;
        for (int index = 0; index < DesignerPagePreview.MaximumDepth; index++)
        {
            var child = Node($"panel{index}", nameof(Panel), 0, 0, 100, 100);
            current.Children.Add(child);
            current = child;
        }
        Assert.Null(DesignerPagePreview.TryCreate(page, new Size(300, 200), out error));
        Assert.Contains("depth 16", error);
    }

    private static DesignDocument PagePreviewDocument()
    {
        var document = BasicDocument();
        document.Size = new DesignSize(600, 450);
        var host = Node("host", nameof(PageHost), 10, 15, 540, 400);
        host.Properties["Padding"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Padding(3, 5, 7, 11), typeof(Padding));
        var tabs = Node("tabs", nameof(TabbedPage), 0, 0, 300, 200);
        tabs.Properties["SelectedIndex"] = DesignPropertyValue.FromInt32(1);
        var first = Node("inactive", nameof(ContentPage), 0, 0, 300, 200);
        first.Children.Add(Node("inactiveLabel", nameof(Label), 0, 0, 80, 20));
        var flyout = Node("flyout", nameof(FlyoutPage), 0, 0, 300, 200);
        flyout.Properties["DisplayMode"] = DesignPropertyValue.FromEnum(typeof(FlyoutDisplayMode).FullName!, "Split");
        flyout.Properties["FlyoutWidth"] = DesignPropertyValue.FromInt32(120);
        var menu = Node("menu", nameof(ContentPage), 0, 0, 100, 200);
        menu.Properties[DesignNodeRoleNames.RolePropertyName] = DesignPropertyValue.FromString(DesignNodeRoleNames.FlyoutPageFlyout);
        var navigation = Node("navigation", nameof(NavigationPage), 0, 0, 300, 200);
        navigation.Properties[DesignNodeRoleNames.RolePropertyName] = DesignPropertyValue.FromString(DesignNodeRoleNames.FlyoutPageDetail);
        var detail = Node("detail", nameof(ContentPage), 0, 0, 300, 200);
        detail.Properties["Padding"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Padding(7, 11, 13, 17), typeof(Padding));
        var body = Node("body", nameof(Label), 0, 0, 80, 20);
        body.Properties["Text"] = DesignPropertyValue.FromString("Production page preview");
        body.Properties["Margin"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Padding(2, 3, 5, 7), typeof(Padding));
        detail.Children.Add(body);
        navigation.Children.Add(detail);
        flyout.Children.Add(menu);
        flyout.Children.Add(navigation);
        tabs.Children.Add(first);
        tabs.Children.Add(flyout);
        host.Children.Add(tabs);
        document.Controls.Add(host);
        return document;
    }
}
