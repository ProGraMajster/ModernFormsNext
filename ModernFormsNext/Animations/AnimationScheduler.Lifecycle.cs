using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Animations;

public sealed partial class AnimationScheduler
{
    private IPlatformApplicationLifecycle? platformLifecycle;

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
        lock (sync)
        {
            if (platformLifecycle is not null || isShutdown)
                return;

            lifecycle.StateChanged += HandlePlatformLifecycleChanged;
            platformLifecycle = lifecycle;
        }

        ApplyPlatformLifecycleState(lifecycle.State);
    }

    partial void UnbindPlatformLifecycle()
    {
        IPlatformApplicationLifecycle? lifecycle;
        lock (sync)
        {
            lifecycle = platformLifecycle;
            platformLifecycle = null;
        }

        if (lifecycle is not null)
            lifecycle.StateChanged -= HandlePlatformLifecycleChanged;
    }

    private void HandlePlatformLifecycleChanged(
        object? sender,
        PlatformApplicationLifecycleChangedEventArgs e)
        => ApplyPlatformLifecycleState(e.CurrentState);

    private void ApplyPlatformLifecycleState(PlatformApplicationLifecycleState state)
    {
        if (state == PlatformApplicationLifecycleState.Foreground)
            Resume();
        else if (state is PlatformApplicationLifecycleState.Background or PlatformApplicationLifecycleState.NoHost)
            Pause();
    }
}
