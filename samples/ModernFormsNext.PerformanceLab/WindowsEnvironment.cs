using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using SkiaSharp;

namespace ModernFormsNext.PerformanceLab;

internal sealed record LabEnvironment(
    string WindowsVersion, string Runtime, string ProcessArchitecture,
    string? ProcessorModel, int LogicalProcessors, ulong? PhysicalMemoryBytes,
    string? DisplayAdapter, int? ReportedRefreshHz, uint? WindowDpi, bool RemoteSession,
    string ManagedSkia, string NativeSkia, string? LabBuild, long StopwatchFrequency);

/// <summary>Reads Windows facts for the actual lab window after timed work has finished.</summary>
internal static class WindowsEnvironment
{
    internal static LabEnvironment Read(nint window)
    {
        var monitor = MonitorFromWindow(window, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        string? device = monitor != 0 && GetMonitorInfo(monitor, ref info) ? info.DeviceName : null;
        uint dpi = GetDpiForWindow(window);
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };

        return new(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(), ReadProcessorModel(),
            Environment.ProcessorCount, GlobalMemoryStatusEx(ref memory) ? memory.TotalPhysical : null,
            ReadAdapter(device), ReadRefresh(device), dpi == 0 ? null : dpi,
            GetSystemMetrics(0x1000) != 0, // SM_REMOTESESSION
            typeof(SKCanvas).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(SKCanvas).Assembly.GetName().Version?.ToString() ?? "unavailable",
            SkiaSharpVersion.Native.ToString(),
            typeof(WindowsEnvironment).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Stopwatch.Frequency);
    }

    private static string? ReadProcessorModel()
    {
        try
        {
            return (Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString", null) as string)?.Trim();
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? ReadAdapter(string? device)
    {
        if (string.IsNullOrEmpty(device))
            return null;
        for (uint index = 0; ; index++)
        {
            var details = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, index, ref details, 0))
                return null;
            if (string.Equals(details.DeviceName, device, StringComparison.OrdinalIgnoreCase))
                return details.Description;
        }
    }

    private static int? ReadRefresh(string? device)
    {
        if (string.IsNullOrEmpty(device))
            return null;
        nint context = CreateDC("DISPLAY", device, null, 0);
        if (context == 0)
            return null;
        try
        {
            // VREFRESH reports the Windows mode. 0 and 1 are driver defaults,
            // and none of these values measures physical scanout or compositor timing.
            int reported = GetDeviceCaps(context, 116);
            return reported > 1 ? reported : null;
        }
        finally
        {
            DeleteDC(context);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        internal int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        internal int Size;
        internal Rect Monitor, Work;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        internal int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? Description;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        internal uint Length, MemoryLoad;
        internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice details, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateDC(string driver, string device, string? port, nint mode);
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(nint context, int index);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint context);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
}
