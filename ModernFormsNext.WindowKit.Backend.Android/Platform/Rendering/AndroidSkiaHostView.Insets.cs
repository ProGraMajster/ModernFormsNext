using ModernFormsNext.WindowKit;
using NativeWindowInsets = Android.Views.WindowInsets;
using SharedWindowInsets = ModernFormsNext.WindowKit.WindowInsets;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

public sealed partial class AndroidSkiaHostView
{
    /// <summary>Gets system/cutout and IME occlusion overlapping this surface, in logical pixels.</summary>
    /// <remarks>
    /// Read on the Android UI thread. Native decor fitting is accounted for before reporting
    /// insets, preventing double padding. API 30+ reports typed IME occlusion separately; API
    /// 23-29 reports system-bar/cutout edges and leaves IME insets zero.
    /// </remarks>
    public SharedWindowInsets CurrentInsets { get; private set; }

    /// <summary>Occurs on the Android UI thread after this surface's logical insets change.</summary>
    /// <remarks>Hosts forward this data to their existing shared control surface/layout.</remarks>
    public event EventHandler<WindowInsetsChangedEventArgs>? InsetsChanged;

    /// <inheritdoc/>
    public override NativeWindowInsets? OnApplyWindowInsets(NativeWindowInsets? insets)
    {
        NativeWindowInsets? result = base.OnApplyWindowInsets(insets);
        RefreshWindowInsets(insets);
        return result;
    }

    private void RefreshWindowInsets(NativeWindowInsets? suppliedInsets = null)
    {
        if (disposed || !IsAttachedToWindow || RootView is not { } root) return;
        NativeWindowInsets? native = suppliedInsets ?? RootWindowInsets;
        if (native is null) return;
        Thickness system;
        Thickness ime = default;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            using var bars = native.GetInsets(NativeWindowInsets.Type.SystemBars() | NativeWindowInsets.Type.DisplayCutout());
            using var keyboard = native.GetInsets(NativeWindowInsets.Type.Ime());
            system = new Thickness(bars.Left, bars.Top, bars.Right, bars.Bottom);
            ime = new Thickness(keyboard.Left, keyboard.Top, keyboard.Right, keyboard.Bottom);
        }
        else
        {
            // Legacy system-window insets may include the keyboard. Limit each edge to the
            // stable bar area; do not mislabel an untyped keyboard height as a safe area.
#pragma warning disable CA1422
            system = new Thickness(
                Math.Min(native.SystemWindowInsetLeft, native.StableInsetLeft),
                Math.Min(native.SystemWindowInsetTop, native.StableInsetTop),
                Math.Min(native.SystemWindowInsetRight, native.StableInsetRight),
                Math.Min(native.SystemWindowInsetBottom, native.StableInsetBottom));
#pragma warning restore CA1422
            if (OperatingSystem.IsAndroidVersionAtLeast(28) && native.DisplayCutout is { } cutout)
            {
                using (cutout)
                    system = new Thickness(Math.Max(system.Left, cutout.SafeInsetLeft),
                        Math.Max(system.Top, cutout.SafeInsetTop), Math.Max(system.Right, cutout.SafeInsetRight),
                        Math.Max(system.Bottom, cutout.SafeInsetBottom));
            }
        }

        int[] location = new int[2];
        int[] rootLocation = new int[2];
        GetLocationInWindow(location);
        root.GetLocationInWindow(rootLocation);
        var bounds = new Rect(location[0] - rootLocation[0], location[1] - rootLocation[1], Width, Height);
        var rootSize = new Size(root.Width, root.Height);
        var current = new SharedWindowInsets(AndroidInsetsMapper.ToSurface(system, bounds, rootSize, Density),
            AndroidInsetsMapper.ToSurface(ime, bounds, rootSize, Density));
        if (current == CurrentInsets) return;
        CurrentInsets = current;
        try { InsetsChanged?.Invoke(this, new WindowInsetsChangedEventArgs(current)); }
        catch (Exception exception)
        {
            AndroidLogger.Write($"Surface inset observer failed ({exception.GetType().Name}).", diagnosticSink);
        }
    }
}
