using System.Drawing;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerPreviewIsolationTests
{
    [Fact]
    public void RepeatedRenderReportsEachFaultOnceAndReopenedDesignPaintsFixedControls()
    {
        var document = DesignerSession.CreateDefaultDocument();
        document.Controls.Clear();
        var first = Node("badSlider", nameof(TrackBar), 8, 8);
        var second = Node("secondSlider", nameof(TrackBar), 8, 48);
        first.Properties[nameof(TrackBar.TickFrequency)] = DesignPropertyValue.FromInt32(0);
        second.Properties[nameof(TrackBar.TickFrequency)] = DesignPropertyValue.FromInt32(0);
        var runtimeOnly = Node("external", "Example.RuntimeWidget", 8, 88);
        var valid = Node("goodPanel", nameof(Panel), 8, 128);
        valid.Properties[nameof(Control.BackColor)] = DesignerPropertyValueEditor.ToDesignPropertyValue(new SKColor(213, 0, 70), typeof(SKColor));
        foreach (var node in new[] { first, second, runtimeOnly, valid })
            document.Controls.Add(node);

        using var session = new DesignerSession();
        using var renderer = new DesignerSurfaceRenderer();
        session.LoadDocument(document);
        var firstPixels = Render(renderer, session, valid.Bounds);
        Render(renderer, session, valid.Bounds);
        Assert.Equal(new SKColor(213, 0, 70), firstPixels);
        Assert.Single(session.OutputLines, m => m.Contains("Runtime property skipped for badSlider (TrackBar)")
            && m.Contains("TickFrequency") && m.Contains("TrackBar.set_TickFrequency"));
        Assert.Single(session.OutputLines, m => m.Contains("Runtime property skipped for secondSlider (TrackBar)")
            && m.Contains("TickFrequency") && m.Contains("TrackBar.set_TickFrequency"));
        Assert.Contains(session.OutputLines, m => m.Contains("preview fallback for external (Example.RuntimeWidget)"));

        var fixedDocument = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        Assert.NotNull(fixedDocument);
        fixedDocument.Controls[0].Properties[nameof(TrackBar.TickFrequency)] = DesignPropertyValue.FromInt32(2);
        fixedDocument.Controls[1].Properties[nameof(TrackBar.TickFrequency)] = DesignPropertyValue.FromInt32(3);
        session.LoadDocument(fixedDocument);
        int before = session.OutputLines.Count;
        Assert.Equal(firstPixels, Render(renderer, session, valid.Bounds));
        Assert.DoesNotContain(session.OutputLines.Skip(before), m => m.Contains("Runtime property skipped for"));
    }

    [Fact]
    public void FailingPagePropertyNamesItsChildAndValidPagesStillUseDesignMode()
    {
        var page = Node("page", nameof(ContentPage), 0, 0);
        var child = Node("slider", nameof(TrackBar), 0, 0);
        page.Children.Add(child);
        child.Properties[nameof(TrackBar.TickFrequency)] = DesignPropertyValue.FromInt32(0);

        Assert.Null(DesignerPagePreview.TryCreate(page, new Size(320, 200), out var error));
        Assert.Contains("Apply properties slider (TrackBar)", error);
        Assert.Contains("TrackBar.set_TickFrequency", error);

        child.Properties[nameof(TrackBar.TickFrequency)] = DesignPropertyValue.FromInt32(2);
        using var preview = DesignerPagePreview.TryCreate(page, new Size(320, 200), out error);
        Assert.NotNull(preview);
        Assert.Null(error);
        Assert.True(preview.Root.Site?.DesignMode);
        Assert.True(preview.Controls[child].Site?.DesignMode);
    }

    [Fact]
    public void AuthoredPropertiesCannotReplaceTheDesignModeSite()
    {
        var node = Node("preview", nameof(Label), 0, 0);
        node.Properties["Site"] = DesignPropertyValue.FromNull();
        node.Properties["Site.Name"] = DesignPropertyValue.FromString("replacement");
        node.Properties[nameof(Control.Text)] = DesignPropertyValue.FromString("Preview text");
        using var control = new Label();
        DesignerPreviewControl.Prepare(control);
        var site = control.Site;
        var errors = new List<string>();

        Assert.Equal(1, DesignerSurfaceRenderer.ApplyPreviewProperties(control, node, errors, includeLayout: true));
        Assert.Equal(2, errors.Count);
        Assert.Same(site, control.Site);
        Assert.True(control.Site?.DesignMode);
        Assert.Null(control.Site?.Name);
        Assert.Equal("Preview text", control.Text);
    }

    [Fact]
    public void PreviewCleanupAttemptsEveryOwnedControlAfterDisposalThrows()
    {
        var page = Node("page", nameof(ContentPage), 0, 0);
        var panel = Node("panel", nameof(Panel), 0, 0);
        var first = Node("first", nameof(Label), 0, 0);
        var failing = Node("failing", nameof(Label), 0, 24);
        panel.Children.Add(first);
        panel.Children.Add(failing);
        page.Children.Add(panel);
        var preview = DesignerPagePreview.TryCreate(page, new Size(320, 200), out var error);
        Assert.NotNull(preview);
        Assert.Null(error);
        Control firstControl = preview.Controls[first];
        Control failingControl = preview.Controls[failing];
        int firstDisposals = 0;
        int rootDisposals = 0;
        firstControl.Disposed += (_, _) => firstDisposals++;
        preview.Root.Disposed += (_, _) => rootDisposals++;
        failingControl.Disposed += (_, _) => throw new InvalidOperationException("Disposal fault");

        Assert.Contains("Dispose failing (Label): InvalidOperationException: Disposal fault", preview.TryDispose());
        Assert.Equal(1, firstDisposals);
        Assert.Equal(1, rootDisposals);
        Assert.Null(preview.TryDispose());
    }

    private static SKColor Render(DesignerSurfaceRenderer renderer, DesignerSession session, DesignBounds sample)
    {
        const int width = 800;
        const int height = 600;
        var info = new SKImageInfo(width, height);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(new PaintEventArgs(info, canvas, 1), session, width, height);
        var mapper = new DesignerCoordinateMapper();
        var bounds = mapper.ToSurfaceBounds(sample, mapper.GetView(session, width, height));
        canvas.Flush();
        return bitmap.GetPixel(bounds.Left + 6, bounds.Bottom - 6);
    }

    private static DesignControlNode Node(string name, string type, int x, int y)
        => new() { Name = name, TypeName = type, Bounds = new DesignBounds(x, y, 200, 32) };
}
