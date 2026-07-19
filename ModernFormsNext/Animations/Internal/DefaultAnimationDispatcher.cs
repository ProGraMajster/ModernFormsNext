using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Animations;

internal sealed class DefaultAnimationDispatcher : IAnimationDispatcher
{
    public bool CheckAccess()
    {
        IPlatformDispatcher? platformDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        return platformDispatcher?.CheckAccess() ?? Dispatcher.UIThread.CheckAccess();
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        IPlatformDispatcher? platformDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        if (platformDispatcher is not null)
            platformDispatcher.Post(action);
        else
            Dispatcher.UIThread.Post(action);
    }
}
