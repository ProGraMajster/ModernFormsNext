using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsFramebufferDamageTests
{
    public static IEnumerable<object[]> DamageCases()
    {
        foreach (double scale in new[] { 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5 })
            foreach (bool deviceBitmap in new[] { false, true })
                foreach (var rect in new[] {
                    (7, 4, 29, 16), (3, 238, 244, 257), (200, 150, 291, 223),
                    (0, 12, 119, 271), (0, 12, 317, 271), // Bottom-edge source (0,0) regression.
                    (-2, -3, 12, 9), (235, 249, 360, 380),
                    (0, 0, 317, 271), (400, 300, 420, 320), (10, 10, 10, 20) })
                    yield return [scale, deviceBitmap, rect.Item1, rect.Item2, rect.Item3, rect.Item4];
    }

    [Theory]
    [MemberData(nameof(DamageCases))]
    public void PartialPresentationPreservesSourceRowsAndOutsidePixels(
        double scale, bool deviceBitmap, int left, int top, int right, int bottom)
    {
        if (!OperatingSystem.IsWindows()) return;
        int width = (int)Math.Ceiling(317 * scale), height = (int)Math.Ceiling(271 * scale);
        left = (int)Math.Floor(left * scale); top = (int)Math.Floor(top * scale);
        right = (int)Math.Ceiling(right * scale); bottom = (int)Math.Ceiling(bottom * scale);
        const int background = 0x00112233;
        var header = new UnmanagedMethods.BITMAPINFOHEADER();
        header.Init(); header.biWidth = width; header.biHeight = -height;
        header.biPlanes = 1; header.biBitCount = 32;
        nint screen = GetDC(0), dc = 0, readDc = 0, bitmap = 0, readBitmap = 0, previous = 0, readPrevious = 0;
        var source = new int[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                source[y * width + x] = ((y % 251) << 16) | ((x % 253) << 8) | ((x + y) % 241);
        var pin = GCHandle.Alloc(source, GCHandleType.Pinned);
        try {
            Assert.NotEqual(0, screen);
            dc = CreateCompatibleDC(screen); readDc = CreateCompatibleDC(screen);
            Assert.NotEqual(0, dc); Assert.NotEqual(0, readDc);
            readBitmap = CreateDIBSection(readDc, ref header, 0, out nint destination, 0, 0);
            Assert.NotEqual(0, readBitmap); Assert.NotEqual(0, destination);
            readPrevious = SelectObject(readDc, readBitmap);
            // HWND presentation can take a different GDI path than a DIB destination.
            // Exercise both, reading a device bitmap back into a DIB only AFTER transfer.
            if (deviceBitmap) {
                bitmap = CreateCompatibleBitmap(screen, width, height);
                Assert.NotEqual(0, bitmap);
                previous = SelectObject(dc, bitmap);
            }
            Marshal.Copy(Enumerable.Repeat(background, source.Length).ToArray(), 0, destination, source.Length);
            if (deviceBitmap) Assert.True(BitBlt(dc, 0, 0, width, height, readDc, 0, 0, 0x00CC0020));
            FramebufferManager.DrawDamageToDevice(deviceBitmap ? dc : readDc, pin.AddrOfPinnedObject(), ref header,
                new UnmanagedMethods.RECT { left = left, top = top, right = right, bottom = bottom });
            Assert.Equal(width, header.biWidth);
            Assert.Equal(-height, header.biHeight); // The borrowed framebuffer descriptor must remain unchanged.
            if (deviceBitmap) Assert.True(BitBlt(readDc, 0, 0, width, height, dc, 0, 0, 0x00CC0020));
            GdiFlush();
            var actual = new int[source.Length];
            Marshal.Copy(destination, actual, 0, actual.Length);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) {
                    int expected = x >= left && x < right && y >= top && y < bottom ? source[y * width + x] : background;
                    if (expected != (actual[y * width + x] & 0x00ffffff))
                        Assert.Fail($"Wrong pixel at {x},{y}: expected {expected:x6}, actual {actual[y * width + x]:x8}.");
                }
        }
        finally {
            pin.Free();
            if (previous != 0) SelectObject(dc, previous);
            if (readPrevious != 0) SelectObject(readDc, readPrevious);
            if (bitmap != 0) DeleteObject(bitmap);
            if (readBitmap != 0) DeleteObject(readBitmap);
            if (dc != 0) DeleteDC(dc);
            if (readDc != 0) DeleteDC(readDc);
            if (screen != 0) ReleaseDC(0, screen);
        }
    }

    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref UnmanagedMethods.BITMAPINFOHEADER header, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
}
