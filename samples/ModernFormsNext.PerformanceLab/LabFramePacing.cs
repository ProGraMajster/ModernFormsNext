using System.Diagnostics;
using Microsoft.Win32;
using ModernFormsNext.Rendering;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.PerformanceLab;

// The experimental gate is attached only to this lab window. The default framework path does not create a timer.
internal sealed class LabFramePacing : IDisposable
{
    private readonly Form window;
    private readonly bool useMode;
    private readonly FrameRequestPacer pacer;
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private bool disposed;

    internal LabFramePacing(Form window, bool useMode)
    {
        this.window = window;
        this.useMode = useMode;
        pacer = window.AttachFrameRequestPacer(static () => Stopwatch.GetElapsedTime(0), SetWake);
        timer.Tick += OnWake;
        window.Shown += OnMonitorChanged;
        window.LocationChanged += OnMonitorChanged;
        if (useMode)
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    internal int? DisplayModeHz { get; private set; }
    internal FrameRequestCounts Counts => pacer.Counts;

    private void SetWake(TimeSpan? delay)
    {
        timer.Stop();
        if (delay is { } value)
        {
            // Dispatcher's native timer is whole milliseconds. The gate rechecks its exact deadline.
            timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, Math.Ceiling(value.TotalMilliseconds)));
            timer.Start();
        }
    }

    private void OnWake(object? sender, EventArgs args)
    {
        timer.Stop();
        pacer.Wake();
    }

    private void OnMonitorChanged(object? sender, EventArgs args) => ReadMode();

    private void OnDisplaySettingsChanged(object? sender, EventArgs args)
        => Dispatcher.UIThread.Post(ReadMode);

    private void ReadMode()
    {
        if (disposed || window.IsBackendClosed || window.InputBindingsClosed) return;
        DisplayModeHz = WindowsEnvironment.ReadRefreshForWindow(window.PlatformHandle.Handle);
        pacer.SetRefreshRate(useMode ? DisplayModeHz : null);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (useMode) SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        window.Shown -= OnMonitorChanged;
        window.LocationChanged -= OnMonitorChanged;
        window.DetachFrameRequestPacer(pacer);
        timer.Stop();
        timer.Tick -= OnWake;
    }
}
