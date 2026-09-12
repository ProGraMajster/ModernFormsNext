using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Testing;

/// <summary>Supplies scoped, controllable accessibility preferences through the ordinary platform settings service.</summary>
/// <remarks>
/// Initial values are unknown. All reads, changes and subscription operations require the host UI
/// thread. A retained disposed fake rejects access and clears handlers; no OS settings are changed.
/// Theme application remains an explicit consumer action.
/// </remarks>
public sealed class TestPlatformSettings : DefaultPlatformSettings, IPlatformAccessibilitySettings
{
    private readonly UiTestDispatcher dispatcher;
    private PlatformAccessibilityPreferences current = new();
    private EventHandler<PlatformAccessibilityPreferences>? changed;
    private EventHandler<PlatformColorValues>? colorsChanged;
    private bool disposed;

    internal TestPlatformSettings(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;

    /// <inheritdoc/>
    public PlatformAccessibilityPreferences GetAccessibilityPreferences() { VerifyAccess(); return current; }

    /// <inheritdoc/>
    public override PlatformColorValues GetColorValues() { VerifyAccess(); return current.ColorValues ?? base.GetColorValues(); }

    /// <inheritdoc/>
    public event EventHandler<PlatformAccessibilityPreferences>? AccessibilityPreferencesChanged
    {
        add { VerifyAccess(); changed += value; }
        remove { dispatcher.VerifyAccess(); changed -= value; }
    }

    /// <inheritdoc/>
    public override event EventHandler<PlatformColorValues>? ColorValuesChanged
    {
        add { VerifyAccess(); colorsChanged += value; }
        remove { dispatcher.VerifyAccess(); colorsChanged -= value; }
    }

    /// <summary>Publishes copied preferences after committing state; equal snapshots produce no notification.</summary>
    /// <param name="colorValues">Detected values, or null to simulate unavailable detection.</param>
    /// <param name="textScale">A positive finite multiplier, or null to simulate unavailable detection.</param>
    /// <remarks>All captured observers run even if an earlier observer throws; failures are aggregated.</remarks>
    public void SetPreferences(PlatformColorValues? colorValues = null, double? textScale = null)
    {
        VerifyAccess();
        var next = new PlatformAccessibilityPreferences(colorValues, textScale);
        if (next == current) return;
        var previous = current;
        current = next;
        var failures = new List<Exception>();
        if (changed is { } handlers)
            foreach (EventHandler<PlatformAccessibilityPreferences> handler in handlers.GetInvocationList())
            {
                if (disposed || current != next) break;
                try { handler(this, next); } catch (Exception error) { failures.Add(error); }
            }
        if (!disposed && current == next && previous.ColorValues != next.ColorValues && next.ColorValues is { } colors && colorsChanged is { } legacy)
            foreach (EventHandler<PlatformColorValues> handler in legacy.GetInvocationList())
            {
                if (disposed || current != next) break;
                try { handler(this, colors); } catch (Exception error) { failures.Add(error); }
            }
        if (failures.Count != 0) throw new AggregateException("Preference observers failed.", failures);
    }

    internal void Dispose()
    {
        dispatcher.VerifyAccess();
        disposed = true;
        changed = null;
        colorsChanged = null;
        current = new();
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispatcher.VerifyAccess();
    }
}
