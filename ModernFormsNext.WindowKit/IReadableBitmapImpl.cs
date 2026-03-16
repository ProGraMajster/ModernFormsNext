namespace ModernFormsNext.WindowKit.Platform;

public interface IReadableBitmapImpl
{
    PixelFormat? Format { get; }
    ILockedFramebuffer Lock();
}
