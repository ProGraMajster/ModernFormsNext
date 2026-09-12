using System;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal class Win32PlatformSettings : DefaultPlatformSettings, IPlatformAccessibilitySettings
{
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly Func<PlatformAccessibilityPreferences> read;
    private readonly Func<Action, IDisposable?> observe;
    private readonly Action<Action> post;
    private PlatformAccessibilityPreferences current = new();
    private EventHandler<PlatformAccessibilityPreferences>? changed;
    private IDisposable? subscription;
    private long generation;

    internal Win32PlatformSettings() : this(WindowsAccessibilityPreferenceReader.Read,
        WindowsTextScaleSubscription.Create, action => Dispatcher.UIThread.Post(action)) { }

    internal Win32PlatformSettings(Func<PlatformAccessibilityPreferences> read,
        Func<Action, IDisposable?> observe, Action<Action> post)
    {
        this.read = read;
        this.observe = observe;
        this.post = post;
        current = read();
    }

    public PlatformAccessibilityPreferences GetAccessibilityPreferences() { VerifyAccess(); Refresh(); return current; }

    public event EventHandler<PlatformAccessibilityPreferences>? AccessibilityPreferencesChanged
    {
        add
        {
            VerifyAccess();
            if (value is null) return;
            changed += value;
            if (subscription is not null) return;
            long version = ++generation;
            try
            {
                var created = observe(() => post(() =>
                {
                    if (version == generation && changed is not null) OnColorValuesChanged();
                }));
                if (version == generation && changed is not null) subscription = created;
                else created?.Dispose();
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidCastException or
                DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
            { System.Diagnostics.Debug.WriteLine($"Accessibility preference observation unavailable: {error.GetType().Name}"); }
            catch
            {
                changed -= value;
                if (changed is null) generation++;
                throw;
            }
        }
        remove
        {
            VerifyAccess();
            changed -= value;
            if (changed is not null) return;
            generation++;
            var old = subscription;
            subscription = null;
            old?.Dispose();
        }
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != ownerThread)
            throw new InvalidOperationException("Windows accessibility preferences require the platform UI thread.");
    }

    public override Size GetTapSize(PointerType type)
    {
        return type switch
        {
            PointerType.Touch => new(10, 10),
            _ => new(GetSystemMetrics(SystemMetric.SM_CXDRAG), GetSystemMetrics(SystemMetric.SM_CYDRAG)),
        };
    }

    public override Size GetDoubleTapSize(PointerType type)
    {
        return type switch
        {
            PointerType.Touch => new(16, 16),
            _ => new(GetSystemMetrics(SystemMetric.SM_CXDOUBLECLK), GetSystemMetrics(SystemMetric.SM_CYDOUBLECLK)),
        };
    }

    public override TimeSpan GetDoubleTapTime(PointerType type) => TimeSpan.FromMilliseconds(GetDoubleClickTime());
    
    // Preserve the legacy fallback while exposing unknown independently in the optional snapshot.
    public override PlatformColorValues GetColorValues() => current.ColorValues ?? base.GetColorValues();

    internal void OnColorValuesChanged()
    {
        // Called from WndProc and a posted WinRT callback. Native delivery cannot throw through
        // the OS; the state commits first and every still-current captured observer is attempted.
        try { Refresh(); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine($"Accessibility preference notification: {error.GetType().Name}"); }
    }

    private void Refresh()
    {
        VerifyAccess();
        var next = read();
        if (next == current) return;
        var previous = current;
        current = next;
        var failures = new List<Exception>();
        if (changed is { } handlers)
            foreach (EventHandler<PlatformAccessibilityPreferences> handler in handlers.GetInvocationList())
            {
                if (current != next) break;
                try { handler(this, next); } catch (Exception error) { failures.Add(error); }
            }
        if (current == next && previous.ColorValues != next.ColorValues && next.ColorValues is { } colors)
            try { base.OnColorValuesChanged(colors); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("Preference observers failed.", failures);
    }
}
