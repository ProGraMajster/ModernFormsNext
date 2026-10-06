using ModernFormsNext.Diagnostics;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class RenderingConfigurationTests
{
    [Theory]
    [InlineData(RenderingBackend.Auto)]
    [InlineData(RenderingBackend.Software)]
    public void CopiedChoiceFreezesAtFirstWindowAndSurvivesClose(RenderingBackend requested)
    {
        using var host = ModernFormsTestHost.Create();
        Assert.Null(Application.ActiveRenderingBackend);
        var options = new RenderingOptions { Backend = requested };
        Application.ConfigureRendering(options);
        options.Backend = requested == RenderingBackend.Auto ? RenderingBackend.Software : RenderingBackend.Auto;
        Assert.Equal(requested, Application.RequestedRenderingBackend);
        using var profiler = PerformanceProfiler.Start();
        var window = host.Show(new Button(), 100, 40);
        using var snapshot = window.CaptureRenderedSnapshot();
        Assert.Equal(RenderingBackend.Software, Application.ActiveRenderingBackend);
        var info = profiler.Capture().LatestFrame!.Value.RenderInfo;
        Assert.Equal(requested, info.RequestedBackend);
        Assert.Equal(RenderingBackend.Software, info.ActiveBackend);
        Assert.Equal("Skia Raster", info.Renderer);
        Assert.Equal("Headless", info.Backend);
        Assert.Equal(PerformanceAcceleration.Software, info.Acceleration);
        Assert.Null(info.FallbackReason);
        Assert.Throws<InvalidOperationException>(() => Application.ConfigureRendering(options));
        window.Close();
        Assert.Throws<InvalidOperationException>(() => Application.ConfigureRendering(options));
    }

    [Fact]
    public void InvalidOptionsDoNotChangeTheRequestedChoice()
    {
        using var host = ModernFormsTestHost.Create();
        Assert.Throws<ArgumentNullException>(() => Application.ConfigureRendering(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderingOptions { Backend = (RenderingBackend)999 });
        Assert.Equal(RenderingBackend.Auto, Application.RequestedRenderingBackend);
        Assert.Null(Application.ActiveRenderingBackend);
    }

    [Fact]
    public void TestRuntimeRestoresBorrowedSelectionAndNextHostStartsWithAuto()
    {
        var previousRequested = Application.RequestedRenderingBackend;
        var previousActive = Application.ActiveRenderingBackend;
        using (var host = ModernFormsTestHost.Create()) {
            Application.ConfigureRendering(new RenderingOptions { Backend = RenderingBackend.Software });
            host.Show(new Button());
            Assert.Equal(RenderingBackend.Software, Application.ActiveRenderingBackend);
        }
        Assert.Equal(previousRequested, Application.RequestedRenderingBackend);
        Assert.Equal(previousActive, Application.ActiveRenderingBackend);
        using var next = ModernFormsTestHost.Create();
        Assert.Equal(RenderingBackend.Auto, Application.RequestedRenderingBackend);
        Assert.Null(Application.ActiveRenderingBackend);
    }
}
