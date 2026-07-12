using System.Runtime.CompilerServices;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Tests;

internal static class TestPlatformServices
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AvaloniaGlobals.AddService<ICursorFactory>(new TestCursorFactory());
    }

    private sealed class TestCursorFactory : ICursorFactory
    {
        public ICursorImpl CreateCursor(IBitmapImpl cursor, PixelPoint hotSpot)
            => new TestCursorImpl();

        public ICursorImpl GetCursor(StandardCursorType cursorType)
            => new TestCursorImpl();
    }

    private sealed class TestCursorImpl : ICursorImpl
    {
        public void Dispose()
        {
        }
    }
}
