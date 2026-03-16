using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Metadata;

#nullable enable

namespace ModernFormsNext.WindowKit.Platform
{
    [PrivateApi]
    public interface ICursorFactory
    {
        ICursorImpl GetCursor(StandardCursorType cursorType);
        ICursorImpl CreateCursor(IBitmapImpl cursor, PixelPoint hotSpot);
    }
}
