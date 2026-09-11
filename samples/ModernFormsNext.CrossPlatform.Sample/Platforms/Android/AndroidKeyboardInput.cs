using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.CrossPlatform.Sample;

// This adapter only joins the backend's key identity/source metadata to the shared surface.
// It performs no text translation or command lookup. The managed Android integration tests
// compile this same file so their event path cannot drift into a second sample-only mapper.
internal static class AndroidKeyboardInput
{
    internal static bool Process(SkiaControlSurface surface, AndroidInputKeyEvent input)
    {
        var args = KeyEventArgs.FromPlatformKey(input.PlatformKey, input.Modifiers);
        return input.IsDown
            ? surface.TryProcessKeyDown(args, isTextInput: !input.IsHardwareKey, isDeadKey: input.IsDeadKey)
            : surface.TryProcessKeyUp(args, isTextInput: !input.IsHardwareKey, isCanceled: input.IsCanceled);
    }
}
