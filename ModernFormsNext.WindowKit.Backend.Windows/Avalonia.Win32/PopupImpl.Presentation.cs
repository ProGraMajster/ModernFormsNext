using System;
using ModernFormsNext.WindowKit.Platform;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal partial class PopupImpl
{
    private bool passivePresentation;
    private uint? styleBeforePresentation;

    void IPopupPresentation.SetPassive() => passivePresentation = true;

    bool IPopupPresentation.TrySetOpacity(float opacity)
    {
        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity));
        var handle = Handle.Handle;
        if (handle == IntPtr.Zero) return false;
        uint previous = GetWindowLong(handle, (int)WindowLongParam.GWL_EXSTYLE);
        styleBeforePresentation ??= previous;
        uint required = (uint)WindowStyles.WS_EX_LAYERED;
        if (passivePresentation) required |= (uint)WindowStyles.WS_EX_TRANSPARENT;
        // Uniform layered-window alpha redirects the existing WM_PAINT surface; it does not
        // replace Skia/GDI with a per-pixel renderer. Passive layered windows also pass pointer
        // messages to underlying windows, including a different desktop application's window.
        // https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows
        if ((previous & required) != required)
            SetWindowLong(handle, (int)WindowLongParam.GWL_EXSTYLE, previous | required);
        if ((GetWindowLong(handle, (int)WindowLongParam.GWL_EXSTYLE) & required) == required &&
            SetLayeredWindowAttributes(handle, 0, (byte)MathF.Round(opacity * 255), LayeredWindowFlags.LWA_ALPHA))
            return true;

        // Native refusal is a supported fallback, never a permanently invisible tooltip.
        SetLayeredWindowAttributes(handle, 0, 255, LayeredWindowFlags.LWA_ALPHA);
        // Restore the original opaque style, not an intermediate layered style whose alpha
        // could still be zero when the API also refuses the recovery call above.
        SetWindowLong(handle, (int)WindowLongParam.GWL_EXSTYLE, styleBeforePresentation.Value);
        return false;
    }
}
