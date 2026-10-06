using System.Reflection;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(InputBindingCollectionTests.Name)]
public sealed class WindowRenderingDamageTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void ProductionPaintPreservesPixelsOutsideOutwardRoundedDamage(double scale)
    {
        using var backing = new Backing((int)(20 * scale), (int)(16 * scale));
        var native = DispatchProxy.Create<IWindowBaseImpl, WindowProxy>();
        var proxy = (WindowProxy)(object)native;
        proxy.Scale = scale;
        proxy.Surfaces = new object[] { backing };
        using var window = new ProbeWindow(native);
        var damage = new Rect(1.1, 1.1, 3.2, 2.7);
        backing.Bitmap.Erase(SKColors.Lime);
        proxy.Paint!(damage);
        int left = (int)Math.Floor(damage.Left * scale), top = (int)Math.Floor(damage.Top * scale);
        int right = (int)Math.Ceiling(damage.Right * scale), bottom = (int)Math.Ceiling(damage.Bottom * scale);
        for (int y = 0; y < backing.Bitmap.Height; y++)
            for (int x = 0; x < backing.Bitmap.Width; x++) {
                bool inside = x >= left && x < right && y >= top && y < bottom;
                Assert.Equal(inside, backing.Bitmap.GetPixel(x, y) != SKColors.Lime);
            }
        Assert.Equal(new[] { "background", "paint" }, window.Order);
        Assert.Equal(1, backing.Unlocks);
        window.Dispose();
        proxy.Paint!(new Rect(0, 0, 20, 16));
        Assert.Equal(1, backing.Unlocks);
        window.adapter.Dispose();
    }

    private sealed class ProbeWindow : WindowBase
    {
        internal readonly List<string> Order = [];
        internal ProbeWindow(IWindowBaseImpl impl) : base(impl) { shown = true; }
        protected override void OnPaintBackground(PaintEventArgs e) {
            Order.Add("background");
            using var paint = new SKPaint { Color = SKColors.Crimson };
            e.Canvas.DrawRect(0, 0, e.Info.Width, e.Info.Height, paint);
        }
        protected override void OnPaint(PaintEventArgs e) { Order.Add("paint"); }
    }
    private class WindowProxy : DispatchProxy
    {
        internal double Scale;
        internal object[] Surfaces = [];
        internal Action<Rect>? Paint;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name) {
                case "get_ClientSize": return new Size(20, 16);
                case "get_RenderScaling": case "get_DesktopScaling": return Scale;
                case "get_Surfaces": return Surfaces;
                case "set_Paint": Paint = (Action<Rect>?)args![0]; return null;
            }
            return method is not null && method.ReturnType != typeof(void) && method.ReturnType.IsValueType
                ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    private sealed class Backing(int width, int height) : IFramebufferPlatformSurface, IDisposable
    {
        internal SKBitmap Bitmap { get; } = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        internal int Unlocks;
        public ILockedFramebuffer Lock() => new LockedFramebuffer(Bitmap.GetPixels(),
            new PixelSize(width, height), Bitmap.RowBytes, new Vector(96, 96), PixelFormat.Bgra8888, () => Unlocks++);
        public void Dispose() => Bitmap.Dispose();
    }
}
