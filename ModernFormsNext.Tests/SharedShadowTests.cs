using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class SharedShadowTests
{
    [Fact]
    public void ReusedSurfaceShowsTextDisabledOpacityAndShadowChangesTogether()
    {
        using var root = new Panel { BackColor = SKColors.White };
        using var card = new Panel { Bounds = new Rectangle(20, 20, 180, 100),
            Shadow = new Shadow(Color.Black, 2, offsetY: 2) };
        using var label = new Label { Bounds = new Rectangle(8, 8, 160, 60), Text = "Before", LineHeight = 1 };
        card.Controls.Add(label);
        root.Controls.Add(card);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(240, 160);
        using var image = new SKBitmap(240, 160);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        byte[] before = image.Bytes;
        label.Text = "After";
        label.Enabled = false;
        label.LineHeight = 1.8;
        label.LetterSpacing = 1.25;
        card.Shadow = new Shadow(Color.Black, 8, 3, 6, 1);
        card.Opacity = 0.8f;
        surface.Render(canvas);
        Assert.False(before.SequenceEqual(image.Bytes));
        Assert.Equal("After", label.Text);
        Assert.False(label.Enabled);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void ShadowDistancesAndFractionalTrackingUseTheOwningWindowDpi(double scale)
    {
        var native = DispatchProxy.Create<IWindowBaseImpl, AppearanceWindowProxy>();
        ((AppearanceWindowProxy)(object)native).Scale = scale;
        using var window = new AppearanceWindow(native);
        using var child = new Panel { Bounds = new Rectangle(10, 10, 40, 20), LetterSpacing = 0.5,
            Shadow = new Shadow(Color.Red, offsetX: 8) };
        window.Controls.Add(child);
        using var bitmap = new SKBitmap(180, 100);
        bitmap.Erase(SKColors.White);
        using var canvas = new SKCanvas(bitmap);
        child.DrawShadow(canvas, 0, 0);
        Assert.Equal(0.5 * scale, TextMeasurer.GetDeviceLetterSpacing(child));
        Assert.Equal(SKColors.Red, bitmap.GetPixel((int)(44 * scale), (int)(10 * scale)));
        Assert.Equal(SKColors.White, bitmap.GetPixel((int)(20 * scale), (int)(10 * scale)));
        window.adapter.Dispose();
    }

    [Fact]
    public void NestedParentClipsShadowWithoutExpandingItsLayout()
    {
        using var root = new Panel { BackColor = SKColors.White };
        using var parent = new Panel { Bounds = new Rectangle(10, 10, 50, 40), BackColor = SKColors.White };
        using var child = new Panel { Bounds = new Rectangle(20, 10, 20, 20), BackColor = SKColors.Blue,
            Shadow = new Shadow(Color.Red, offsetX: 20) };
        root.Controls.Add(parent);
        parent.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(100, 80);
        using var image = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        Assert.Equal(SKColors.Red, image.GetPixel(55, 30));
        Assert.Equal(SKColors.White, image.GetPixel(65, 30));
        Assert.Equal(new Rectangle(10, 10, 50, 40), parent.Bounds);
    }

    [Fact]
    public void EffectiveOpacityAppliesToTheComposedShadow()
    {
        using var root = new Panel { BackColor = SKColors.White };
        using var child = new Panel { Bounds = new Rectangle(20, 20, 40, 20), BackColor = SKColors.Blue,
            Shadow = new Shadow(Color.Red, offsetX: 8), Opacity = 0.5f };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(90, 60);
        using var image = new SKBitmap(90, 60);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        var pixel = image.GetPixel(64, 30);
        Assert.Equal(255, pixel.Red);
        Assert.InRange(pixel.Green, (byte)126, (byte)128);
        Assert.InRange(pixel.Blue, (byte)126, (byte)128);
    }

    [Fact]
    public void DisposalFromAnExtensibleStyleReadRetiresTheShadowBeforeNativeCacheUse()
    {
        using var child = new DisposingStylePanel { Width = 40, Height = 20, Shadow = new Shadow(Color.Red, 3) };
        using var image = new SKBitmap(100, 80);
        image.Erase(SKColors.White);
        using var canvas = new SKCanvas(image);
        child.DisposeOnStyleRead = true;
        child.DrawShadow(canvas, 20, 20);
        Assert.True(child.IsDisposed);
        Assert.Equal(SKColors.White, image.GetPixel(65, 30));
    }

    [Fact]
    public void OuterShadowDoesNotFillTransparentControlInterior()
    {
        using var root = new Panel { BackColor = SKColors.White };
        using var child = new Panel { Bounds = new Rectangle(20, 20, 40, 20), BackColor = SKColors.Transparent,
            Shadow = new Shadow(Color.Red, offsetX: 8) };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(90, 60);
        using var image = new SKBitmap(90, 60);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        Assert.Equal(SKColors.White, image.GetPixel(40, 30));
        Assert.Equal(SKColors.Red, image.GetPixel(64, 30));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteDistancesAreRejected(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, blurRadius: value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, offsetX: value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, offsetY: value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, spread: value));
    }

    [Fact]
    public void DistanceLimitsAndImmutableEqualityAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, blurRadius: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, blurRadius: 129));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, offsetX: 2049));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, offsetY: -2049));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, spread: 129));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shadow(Color.Black, spread: -129));
        var a = new Shadow(Color.FromArgb(120, 10, 20, 30), 5, -2, 3, 1);
        var b = new Shadow(Color.FromArgb(120, 10, 20, 30), 5, -2, 3, 1);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new Shadow(Color.Red, 5, -2, 3, 1));
    }

    [Fact]
    public void OuterPixelsAreComposedClippedAndErasedWhenShadowIsRemoved()
    {
        using var root = new Panel { BackColor = SKColors.White };
        using var child = new Panel { Bounds = new Rectangle(20, 20, 40, 20), BackColor = SKColors.Blue,
            Shadow = new Shadow(Color.Red, offsetX: 8) };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(70, 60);
        using var image = new SKBitmap(70, 60);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        Assert.Equal(SKColors.Red, image.GetPixel(64, 30));
        Assert.Equal(SKColors.Blue, image.GetPixel(30, 30));
        Assert.Equal(SKColors.White, image.GetPixel(69, 30));
        Assert.Equal(new Rectangle(20, 20, 40, 20), child.Bounds);
        Assert.False(child.Bounds.Contains(64, 30));
        child.Shadow = null;
        surface.Render(canvas);
        Assert.Equal(SKColors.White, image.GetPixel(64, 30));
        Assert.Equal(SKColors.Blue, image.GetPixel(30, 30));
    }

    [Fact]
    public void ShadowMovesWithControlAndOldFootprintIsErased()
    {
        using var root = new Panel { BackColor = SKColors.White };
        using var child = new Panel { Bounds = new Rectangle(10, 10, 20, 20), BackColor = SKColors.Blue,
            Shadow = new Shadow(Color.Red, offsetX: 5) };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(100, 60);
        using var image = new SKBitmap(100, 60);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        Assert.Equal(SKColors.Red, image.GetPixel(32, 20));
        child.Left = 50;
        surface.Render(canvas);
        Assert.Equal(SKColors.White, image.GetPixel(32, 20));
        Assert.Equal(SKColors.Red, image.GetPixel(72, 20));
    }

    [Fact]
    public void DynamicResourceReplacementErasesPreviousShadowFootprint()
    {
        using var parent = new Panel { BackColor = SKColors.White };
        using var child = new Panel { Bounds = new Rectangle(20, 20, 30, 20), BackColor = SKColors.Blue };
        parent.Controls.Add(child);
        child.Resources["Elevation"] = new Shadow(Color.Red, offsetX: 5);
        child.SetResourceReference(nameof(Control.Shadow), "Elevation");
        using var surface = new SkiaControlSurface(parent);
        surface.Resize(90, 60);
        using var image = new SKBitmap(90, 60);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        Assert.Equal(SKColors.Red, image.GetPixel(52, 30));
        child.Resources["Elevation"] = new Shadow(Color.Green, offsetX: -5);
        surface.Render(canvas);
        Assert.Equal(SKColors.White, image.GetPixel(52, 30));
        Assert.Equal(SKColors.Green, image.GetPixel(17, 30));
        Assert.True(child.ClearResourceReference(nameof(Control.Shadow)));
        surface.Render(canvas);
        Assert.Equal(SKColors.White, image.GetPixel(17, 30));
    }

    [Fact]
    public void WarmShadowDrawingDoesNotAllocateAndExplicitDisposalReleasesCache()
    {
        using var child = new Panel { Width = 30, Height = 20, Shadow = new Shadow(Color.Black, 2, 2, 2) };
        using var image = new SKBitmap(80, 60);
        using var canvas = new SKCanvas(image);
        for (int i = 0; i < 100; i++) child.DrawShadow(canvas, 10, 10);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) child.DrawShadow(canvas, 10, 10);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        var field = typeof(Control).GetField("shadowPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var filterField = typeof(Control).GetField("shadowMaskFilter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cutoutField = typeof(Control).GetField("shadowCutout", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var paint = Assert.IsType<SKPaint>(field.GetValue(child));
        var filter = Assert.IsType<SKMaskFilter>(filterField.GetValue(child));
        var cutout = Assert.IsType<SKPath>(cutoutField.GetValue(child));
        child.Dispose();
        Assert.Null(field.GetValue(child));
        Assert.Null(filterField.GetValue(child));
        Assert.Null(cutoutField.GetValue(child));
        Assert.Equal(IntPtr.Zero, paint.Handle);
        Assert.Equal(IntPtr.Zero, filter.Handle);
        Assert.Equal(IntPtr.Zero, cutout.Handle);
    }

    private sealed class DisposingStylePanel : Panel
    {
        internal bool DisposeOnStyleRead;
        public override ControlStyle CurrentStyle
        {
            get
            {
                if (DisposeOnStyleRead) { DisposeOnStyleRead = false; Dispose(); }
                return base.CurrentStyle;
            }
        }
    }

    private sealed class AppearanceWindow(IWindowBaseImpl implementation) : WindowBase(implementation);

    public class AppearanceWindowProxy : DispatchProxy
    {
        public double Scale = 1;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name is "get_RenderScaling" or "get_DesktopScaling") return Scale;
            Type result = targetMethod?.ReturnType ?? typeof(void);
            return result == typeof(void) || !result.IsValueType ? null : Activator.CreateInstance(result);
        }
    }
}
