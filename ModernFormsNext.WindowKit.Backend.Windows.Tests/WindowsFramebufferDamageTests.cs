using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsFramebufferDamageTests
{
    [Theory]
    [InlineData(7, 4, 29, 16)]
    [InlineData(3, 38, 44, 57)]
    [InlineData(-2, -3, 12, 9)]
    [InlineData(35, 49, 60, 80)]
    public void PartialPresentationCopiesCorrectTopDownRowsAndPreservesOutsidePixels(int left, int top, int right, int bottom)
    {
        if (!OperatingSystem.IsWindows()) return;
        const int width = 47, height = 61, background = 0x00112233;
        var header = new UnmanagedMethods.BITMAPINFOHEADER();
        header.Init(); header.biWidth = width; header.biHeight = -height;
        header.biPlanes = 1; header.biBitCount = 32;
        nint dc = CreateCompatibleDC(0);
        Assert.NotEqual(0, dc);
        nint bitmap = CreateDIBSection(dc, ref header, 0, out nint destination, 0, 0);
        Assert.NotEqual(0, bitmap);
        nint previous = SelectObject(dc, bitmap);
        var source = new int[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) source[y * width + x] = (y << 16) | (x << 8) | 0x77;
        var pin = GCHandle.Alloc(source, GCHandleType.Pinned);
        try {
            Marshal.Copy(Enumerable.Repeat(background, source.Length).ToArray(), 0, destination, source.Length);
            FramebufferManager.DrawDamageToDevice(dc, pin.AddrOfPinnedObject(), ref header,
                new UnmanagedMethods.RECT { left = left, top = top, right = right, bottom = bottom });
            GdiFlush();
            var actual = new int[source.Length];
            Marshal.Copy(destination, actual, 0, actual.Length);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) {
                    int expected = x >= left && x < right && y >= top && y < bottom ? source[y * width + x] : background;
                    Assert.Equal(expected, actual[y * width + x] & 0x00ffffff);
                }
        }
        finally { pin.Free(); SelectObject(dc, previous); DeleteObject(bitmap); DeleteDC(dc); }
    }

    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref UnmanagedMethods.BITMAPINFOHEADER header, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
}
