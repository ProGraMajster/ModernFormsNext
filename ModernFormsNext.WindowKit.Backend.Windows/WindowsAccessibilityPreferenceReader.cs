using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Windows;

// UISettings is acquired only for a read or an owned event subscription. No HWND, registry
// approximation, Activity reference or application theme is created by preference detection.
internal static class WindowsAccessibilityPreferenceReader
{
    internal static PlatformAccessibilityPreferences Read()
    {
        if (!OperatingSystem.IsWindows()) return new();
        PlatformColorValues? colors = null;
        double? scale = null;
        try
        {
            var contrast = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
            if (SystemParametersInfo(0x42, contrast.Size, ref contrast, 0))
            {
                Color background = ColorFromRef(GetSysColor(5)); // COLOR_WINDOW
                Color foreground = ColorFromRef(GetSysColor(8)); // COLOR_WINDOWTEXT
                colors = new PlatformColorValues
                {
                    ContrastPreference = (contrast.Flags & 1) != 0 ? ColorContrastPreference.High : ColorContrastPreference.NoPreference,
                    ThemeVariant = background.GetBrightness() < .5 ? PlatformThemeVariant.Dark : PlatformThemeVariant.Light,
                    BackgroundColor = background, ForegroundColor = foreground,
                    AccentColor1 = ColorFromRef(GetSysColor(13)), // COLOR_HIGHLIGHT
                    AccentColor2 = ColorFromRef(GetSysColor(14)) // COLOR_HIGHLIGHTTEXT
                };
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { }
        try
        {
            using var lease = WindowsTextScaleSubscription.Create(null);
            scale = lease?.Read();
        }
        catch (Exception error) when (error is COMException or InvalidCastException or DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException) { }
        return new(colors, scale);
    }

    private static Color ColorFromRef(uint value) => Color.FromArgb(255, (int)(value & 255), (int)((value >> 8) & 255), (int)((value >> 16) & 255));
    [StructLayout(LayoutKind.Sequential)] private struct HighContrast { internal uint Size, Flags; internal nint Scheme; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrast data, uint flags);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
}

// ABI order and GUIDs are from the Windows SDK windows.ui.viewmanagement.h. IUISettings2
// derives from IInspectable (three slots after IUnknown); its typed event handler derives
// directly from IUnknown. Source generation owns the RCW/CCW vtables, not handwritten output.
[GeneratedComInterface, Guid("bad82401-2721-44f9-bb91-2bb228be442f")]
internal partial interface IWindowsUiSettings2
{
    [PreserveSig] int GetIids(out uint count, out nint values);
    [PreserveSig] int GetRuntimeClassName(out nint name);
    [PreserveSig] int GetTrustLevel(out int trust);
    [PreserveSig] int GetTextScaleFactor(out double value);
    [PreserveSig] int AddTextScaleFactorChanged(IWindowsTextScaleChanged handler, out long token);
    [PreserveSig] int RemoveTextScaleFactorChanged(long token);
}

[GeneratedComInterface, Guid("2dbdba9d-20da-519d-9078-09f835bc5bc7")]
internal partial interface IWindowsTextScaleChanged
{
    [PreserveSig] int Invoke(nint sender, nint arguments);
}

[GeneratedComClass]
internal sealed partial class WindowsTextScaleSubscription : IWindowsTextScaleChanged, IDisposable
{
    private IWindowsUiSettings2? settings;
    private Action? changed;
    private long token;
    private bool subscribed;
    private bool initialized;

    internal static unsafe WindowsTextScaleSubscription? Create(Action? changed)
    {
        var result = new WindowsTextScaleSubscription { changed = changed };
        nint name = 0, instance = 0;
        try
        {
            int hr = RoInitialize(0); // STA; an existing different apartment remains owned by its caller.
            result.initialized = hr >= 0;
            if (hr < 0 && hr != unchecked((int)0x80010106)) return null; // RPC_E_CHANGED_MODE
            const string className = "Windows.UI.ViewManagement.UISettings";
            Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out name));
            Marshal.ThrowExceptionForHR(RoActivateInstance(name, out instance));
            result.settings = UniqueComInterfaceMarshaller<IWindowsUiSettings2>.ConvertToManaged((void*)instance);
            if (result.settings is null) throw new COMException("UISettings was unavailable.");
            if (changed is not null)
            {
                Marshal.ThrowExceptionForHR(result.settings.AddTextScaleFactorChanged(result, out result.token));
                result.subscribed = true;
            }
            return result;
        }
        catch { result.Dispose(); throw; }
        finally
        {
            if (instance != 0) Marshal.Release(instance);
            if (name != 0) WindowsDeleteString(name);
        }
    }

    internal double? Read()
        => settings is { } value && value.GetTextScaleFactor(out double scale) >= 0 && double.IsFinite(scale) && scale > 0 ? scale : null;

    public int Invoke(nint sender, nint arguments)
    {
        // WinRT can invoke on another thread. The provider's callback posts to its existing UI
        // dispatcher; exceptions never cross the unmanaged callback boundary.
        try { Volatile.Read(ref changed)?.Invoke(); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine($"Accessibility preference callback: {error.GetType().Name}"); }
        return 0;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref changed, null);
        var owned = settings;
        settings = null;
        try { if (subscribed && owned is not null) owned.RemoveTextScaleFactorChanged(token); }
        finally
        {
            subscribed = false;
            // Generated COM interfaces are implemented dynamically by the sealed ComObject
            // wrapper; cast through object because the C# static interface pattern is disjoint.
            try { if ((object?)owned is ComObject wrapper) wrapper.FinalRelease(); }
            finally { if (initialized) { initialized = false; RoUninitialize(); } }
        }
    }

    [DllImport("combase.dll")] private static extern int RoInitialize(uint mode);
    [DllImport("combase.dll")] private static extern void RoUninitialize();
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out nint result);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll")] private static extern int RoActivateInstance(nint name, out nint instance);
}
