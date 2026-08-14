using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Animations;

public sealed partial class AnimationScheduler
{
    private IPlatformApplicationLifecycle? platformLifecycle;
    private long platformLifecycleVersion;

    partial void BindPlatformLifecycleCore()
    {
        IPlatformApplicationLifecycle? lifecycle;
        lock (sync)
        {
            if (platformLifecycle is not null || isShutdown)
                return;

            lifecycle = PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>();
            if (lifecycle is null)
                return;
        }

        BindPlatformLifecycle(lifecycle);
    }

    private void BindPlatformLifecycle(IPlatformApplicationLifecycle lifecycle)
    {
        long version;
        lock (sync)
        {
            if (platformLifecycle is not null || isShutdown)
                return;

            lifecycle.StateChanged += HandlePlatformLifecycleChanged;
            platformLifecycle = lifecycle;
            version = ++platformLifecycleVersion;
        }

        ApplyPlatformLifecycleState(lifecycle.State, version);
    }

    partial void UnbindPlatformLifecycle()
    {
        IPlatformApplicationLifecycle? lifecycle;
        lock (sync)
        {
            lifecycle = platformLifecycle;
            platformLifecycle = null;
            platformLifecycleVersion++;
        }

        if (lifecycle is not null)
            lifecycle.StateChanged -= HandlePlatformLifecycleChanged;
    }

    private void HandlePlatformLifecycleChanged(
        object? sender,
        PlatformApplicationLifecycleChangedEventArgs e)
    {
        long version;
        lock (sync)
        {
            if (isShutdown || !ReferenceEquals(sender, platformLifecycle))
                return;
            version = ++platformLifecycleVersion;
        }

        ApplyPlatformLifecycleState(e.CurrentState, version);
    }

    private void ApplyPlatformLifecycleState(
        PlatformApplicationLifecycleState state,
        long version)
    {
        if (state == PlatformApplicationLifecycleState.Foreground)
        {
            RefreshPlatformPolicyAfterForeground();

            lock (sync)
            {
                // A background event can arrive while the foreground settings refresh is in
                // progress. Never let that stale foreground continuation restart frame demand.
                if (!isShutdown && version == platformLifecycleVersion)
                    Resume();
            }
        }
        else if (state is PlatformApplicationLifecycleState.Background or PlatformApplicationLifecycleState.NoHost)
        {
            lock (sync)
            {
                if (!isShutdown && version == platformLifecycleVersion)
                    Pause();
            }
        }
    }
}
