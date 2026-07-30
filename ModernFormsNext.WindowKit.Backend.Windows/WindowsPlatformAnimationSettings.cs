using System.ComponentModel;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Windows;

internal readonly record struct WindowsAnimationPreferenceReadResult(
    bool Succeeded,
    bool AnimationsEnabled,
    string? Error);

internal interface IWindowsAnimationPreferenceReader
{
    WindowsAnimationPreferenceReadResult Read();
}

internal sealed class WindowsAnimationPreferenceReader : IWindowsAnimationPreferenceReader
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    public WindowsAnimationPreferenceReadResult Read()
    {
        if (!OperatingSystem.IsWindows())
            return new(false, true, "Windows client-area animation settings are unavailable on this platform.");

        try
        {
            if (SystemParametersInfo(SpiGetClientAreaAnimation, 0, out bool enabled, 0))
                return new(true, enabled, null);

            return new(false, true, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return new(false, true, exception.Message);
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        [MarshalAs(UnmanagedType.Bool)] out bool value,
        uint updateFlags);
}

/// <summary>
/// Reads and publishes the Windows client-area animation accessibility preference.
/// </summary>
/// <remarks>
/// The provider creates no native handles. The existing backend message window calls
/// <see cref="NotifySystemSettingsChanged"/> for <c>WM_SETTINGCHANGE</c>.
/// </remarks>
internal sealed class WindowsPlatformAnimationSettings : IPlatformAnimationSettings
{
    internal const string SourceName = "Windows SPI_GETCLIENTAREAANIMATION";

    private readonly object sync = new();
    private readonly IWindowsAnimationPreferenceReader reader;
    private readonly Func<DateTimeOffset> utcNow;
    private EventHandler<PlatformAnimationSettingsChangedEventArgs>? changed;
    private PlatformAnimationSettingsSnapshot current;

    public WindowsPlatformAnimationSettings()
        : this(new WindowsAnimationPreferenceReader(), static () => DateTimeOffset.UtcNow)
    {
    }

    internal WindowsPlatformAnimationSettings(
        IWindowsAnimationPreferenceReader reader,
        Func<DateTimeOffset> utcNow)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        current = CreateFallback(lastError: null, lastUpdate: null);
        Refresh();
    }

    public PlatformAnimationSettingsSnapshot Current
    {
        get
        {
            lock (sync)
                return current;
        }
    }

    public event EventHandler<PlatformAnimationSettingsChangedEventArgs>? Changed
    {
        add
        {
            lock (sync)
                changed += value;
        }
        remove
        {
            lock (sync)
                changed -= value;
        }
    }

    internal int SubscriberCount
    {
        get
        {
            lock (sync)
                return changed?.GetInvocationList().Length ?? 0;
        }
    }

    internal bool IsLockHeldByCurrentThread => Monitor.IsEntered(sync);

    public PlatformAnimationSettingsSnapshot Refresh()
    {
        WindowsAnimationPreferenceReadResult result;
        try
        {
            result = reader.Read();
        }
        catch (Exception exception)
        {
            result = new WindowsAnimationPreferenceReadResult(false, true, exception.Message);
        }

        DateTimeOffset update = utcNow();
        PlatformAnimationSettingsSnapshot next = result.Succeeded
            ? new PlatformAnimationSettingsSnapshot(
                SourceName,
                reducedMotion: !result.AnimationsEnabled,
                animationsEnabled: result.AnimationsEnabled,
                update,
                fallbackUsed: false,
                PlatformAnimationProviderState.Ready,
                lastError: null)
            : CreateFallback(result.Error, update);

        EventHandler<PlatformAnimationSettingsChangedEventArgs>? handlers;
        PlatformAnimationSettingsSnapshot previous;
        bool meaningfulChange;
        lock (sync)
        {
            previous = current;
            current = next;
            meaningfulChange = !IsMeaningfullyEquivalent(previous, next);
            handlers = meaningfulChange ? changed : null;
        }

        handlers?.Invoke(this, new PlatformAnimationSettingsChangedEventArgs(previous, next));
        return next;
    }

    internal void NotifySystemSettingsChanged()
        => Refresh();

    private static PlatformAnimationSettingsSnapshot CreateFallback(
        string? lastError,
        DateTimeOffset? lastUpdate)
        => new(
            SourceName,
            reducedMotion: false,
            animationsEnabled: true,
            lastUpdate,
            fallbackUsed: true,
            PlatformAnimationProviderState.Fallback,
            lastError);

    private static bool IsMeaningfullyEquivalent(
        PlatformAnimationSettingsSnapshot left,
        PlatformAnimationSettingsSnapshot right)
        => left.ReducedMotion == right.ReducedMotion
        && left.AnimationsEnabled == right.AnimationsEnabled
        && left.FallbackUsed == right.FallbackUsed
        && left.ProviderState == right.ProviderState
        && string.Equals(left.LastError, right.LastError, StringComparison.Ordinal);
}
