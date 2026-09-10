using System.Runtime.ExceptionServices;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

// Native host state commits before callbacks. Cancellation and resource release are mandatory
// even when application observers fail; this shared helper keeps that path testable without Java.
internal static class AndroidSurfaceCleanup
{
    internal static void Complete(params Action[] actions)
    {
        List<Exception>? failures = null;
        foreach (Action action in actions)
        {
            try { action(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        ThrowFailures(failures);
    }

    internal static void CancelPointers(object sender, EventHandler<AndroidPointerEvent>? handlers,
        IReadOnlyList<int> pointerIds, int? primaryPointerId)
    {
        if (handlers is null || pointerIds.Count == 0) return;
        Delegate[] subscribers = handlers.GetInvocationList();
        List<Exception>? failures = null;
        foreach (int pointerId in pointerIds)
        {
            var args = new AndroidPointerEvent(pointerId, AndroidPointerAction.Cancel, 0, 0, pointerId == primaryPointerId);
            foreach (EventHandler<AndroidPointerEvent> handler in subscribers)
            {
                try { handler(sender, args); }
                catch (Exception exception) { (failures ??= []).Add(exception); }
            }
        }
        ThrowFailures(failures);
    }

    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 }) throw new AggregateException("Android surface cleanup callbacks failed.", failures);
    }
}
