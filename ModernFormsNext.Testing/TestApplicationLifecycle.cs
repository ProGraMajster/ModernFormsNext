using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Testing;

/// <summary>Controls the production application lifecycle provider in a headless host.</summary>
/// <remarks>
/// The same provider implements legacy foreground/background and optional rich lifecycle contracts.
/// Use inherited Publish, Activate, RequestSaveState and RestoreState to exercise the real shared
/// publisher; no native activity, intent, window-manager event or persistence operation is simulated.
/// Changes and callbacks run on the host UI thread. The initial snapshot is Running/Foreground with
/// one active host at generation one. The test host normally owns disposal.
/// </remarks>
/// <example>
/// <code>
/// using var host = ModernFormsTestHost.Create();
/// host.Services.Lifecycle.Activate(new PlatformApplicationActivation(
///     PlatformActivationKind.Uri, uri: new Uri("example://document/42")));
/// host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
/// </code>
/// </example>
public sealed class TestApplicationLifecycle : PlatformApplicationLifecyclePublisher
{
    internal TestApplicationLifecycle(UiTestDispatcher dispatcher)
        : base(dispatcher.VerifyAccess, new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Foreground,
            isActive: true, hostCount: 1, hostGeneration: 1))
    {
    }

    /// <summary>Publishes a state change through the production platform lifecycle event.</summary>
    /// <param name="value">An existing platform lifecycle state.</param>
    /// <remarks>
    /// Call on the host UI thread. This compatibility helper maps Foreground to an active host,
    /// Background to an inactive host and NoHost to zero hosts. Returning from zero hosts to
    /// Foreground advances host generation. Use Publish for explicit phase, active or host-count
    /// scenarios. State commits before legacy and rich callbacks; callback failures are reported
    /// after all mandatory subscribers and queued transitions run. A terminal application cannot restart.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined lifecycle state.</exception>
    public void SetState(PlatformApplicationLifecycleState value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        PlatformApplicationLifecycleSnapshot current = Snapshot;
        if (current.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
        {
            // Keep the same UI-affinity/terminal checks as explicit publication, including a
            // no-op request from a caller attempting to reactivate a terminated test session.
            Publish(current);
            return;
        }
        bool foreground = value == PlatformApplicationLifecycleState.Foreground;
        int hosts = value == PlatformApplicationLifecycleState.NoHost ? 0 :
            foreground ? Math.Max(1, current.HostCount) : current.HostCount;
        long generation = foreground && current.HostCount == 0
            ? checked(current.HostGeneration + 1) : current.HostGeneration;
        PlatformApplicationPhase phase = foreground && current.Phase == PlatformApplicationPhase.Suspended
            ? PlatformApplicationPhase.Running : current.Phase;
        Publish(new PlatformApplicationLifecycleSnapshot(phase, value, foreground, hosts, generation));
    }
}
