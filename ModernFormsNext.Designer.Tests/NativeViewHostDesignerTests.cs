using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class NativeViewHostDesignerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void RuntimePreviewPaintsPlaceholderWithoutCreatingNativeRuntime(double scale)
    {
        using var host = new CustomNativeHost { Bounds = new(0, 0, 200, 80), Text = "Safe feature placeholder", PeerFactory = new ExplodingFactory() };
        var info = new SKImageInfo((int)(200 * scale), (int)(80 * scale));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, scale), host,
            new Size(200, 80), new Rectangle(0, 0, info.Width, info.Height), out var diagnostics, out var error), error);
        Assert.True(diagnostics.VisibleSampleCount > 0);
        Assert.Equal(NativeViewHostState.Detached, host.HostingDiagnostics.State);
        Assert.False(host.HostingCapabilities.Supported);
    }

    [Fact]
    public void HandlesAndSessionsCannotEnterDesignerSerialization()
    {
        Assert.Contains(new DesignerToolboxService().GetItems(), item => item.TypeName == nameof(NativeViewHost));
        var properties = TypeDescriptor.GetProperties(typeof(NativeViewHost));
        foreach (var name in new[] { "PeerFactory", "HostingDiagnostics", "HostingCapabilities" })
        {
            var property = properties[name]!;
            Assert.False(property.IsBrowsable);
            Assert.Equal(DesignerSerializationVisibility.Hidden,
                ((DesignerSerializationVisibilityAttribute)property.Attributes[typeof(DesignerSerializationVisibilityAttribute)]!).Visibility);
        }
        Assert.NotNull(properties["Text"]);
        Assert.DoesNotContain(properties.Cast<PropertyDescriptor>(), p => p.Name is "NativeView" or "Session" or "NativeHandle");
    }

    private sealed class ExplodingFactory : INativeViewFactory
    {
        public INativeViewPeer CreatePeer(INativeViewSite site) => throw new InvalidOperationException("Designer invoked native runtime.");
    }
    private sealed class CustomNativeHost : NativeViewHost { }
}
