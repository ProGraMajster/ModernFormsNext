using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Tests;

internal sealed class ThemeManagerTestHarness : IDisposable
{
    public ThemeManagerTestHarness(
        ThemeVariant systemVariant = ThemeVariant.Light,
        bool reducedMotion = false,
        bool designMode = false,
        IThemeDispatcher? dispatcher = null,
        IPlatformApplicationLifecycle? lifecycle = null,
        ResourceDictionary? resources = null,
        IThemeLegacyStore? legacyStore = null)
    {
        SchedulerHarness = new AnimationSchedulerTestHarness(lifecycle: lifecycle);
        Dispatcher = dispatcher ?? new ImmediateThemeDispatcher();
        Environment = new TestThemeEnvironment(systemVariant, reducedMotion, designMode);
        Resources = resources ?? new ResourceDictionary();
        LegacyStore = new TestThemeLegacyStore();
        AppliedLegacyStore = legacyStore ?? LegacyStore;
        Manager = new ThemeManager(
            SchedulerHarness.Scheduler,
            Dispatcher,
            Environment,
            new ThemeSecurityLimits(),
            Resources,
            AppliedLegacyStore);
    }

    public AnimationSchedulerTestHarness SchedulerHarness { get; }
    public IThemeDispatcher Dispatcher { get; }
    public TestThemeEnvironment Environment { get; }
    public ResourceDictionary Resources { get; }
    public TestThemeLegacyStore LegacyStore { get; }
    public IThemeLegacyStore AppliedLegacyStore { get; }
    public ThemeManager Manager { get; }

    public void Dispose() => SchedulerHarness.Dispose();
}

internal sealed class TestThemeLegacyStore : IThemeLegacyStore
{
    private Dictionary<string, object> values = Theme.GetValueSnapshot();

    public int NotificationCount { get; private set; }
    public Exception? NextReplaceException { get; set; }

    public Dictionary<string, object> GetSnapshot() => new(values, StringComparer.Ordinal);

    public void Replace(IReadOnlyDictionary<string, object> replacement)
    {
        if (NextReplaceException is { } exception)
        {
            NextReplaceException = null;
            throw exception;
        }

        values = new Dictionary<string, object>(replacement, StringComparer.Ordinal);
    }

    public void NotifyChanged() => NotificationCount++;
}

internal sealed class ImmediateThemeDispatcher : IThemeDispatcher
{
    public int ThreadId { get; } = Environment.CurrentManagedThreadId;
    public int InvocationCount { get; private set; }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == ThreadId;

    public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvocationCount++;
        return Task.FromResult(function());
    }

    public void Post(Action action) => action();
}

internal sealed class QueuedThemeDispatcher : IThemeDispatcher
{
    private readonly Queue<Action> pending = new();
    private readonly TaskCompletionSource<bool> enqueued =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ThreadId { get; private set; }
    public int TotalInvocations { get; private set; }
    public int PendingCount
    {
        get
        {
            lock (pending)
                return pending.Count;
        }
    }

    public Task Enqueued => enqueued.Task;

    public bool CheckAccess() => ThreadId != 0 && ThreadId == Environment.CurrentManagedThreadId;

    public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken)
    {
        TotalInvocations++;
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (pending)
        {
            pending.Enqueue(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                    return;
                }
                try
                {
                    completion.TrySetResult(function());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
        }
        enqueued.TrySetResult(true);
        return completion.Task;
    }

    public void Post(Action action)
    {
        lock (pending)
            pending.Enqueue(action);
    }

    public void Drain()
    {
        ThreadId = Environment.CurrentManagedThreadId;
        while (true)
        {
            Action? action;
            lock (pending)
                action = pending.Count == 0 ? null : pending.Dequeue();
            if (action is null)
                return;
            action();
        }
    }
}

internal sealed class TestThemeEnvironment(
    ThemeVariant systemVariant,
    bool reducedMotion,
    bool designMode) : IThemeEnvironment
{
    public bool IsDesignMode { get; set; } = designMode;
    public ThemeVariant SystemVariant { get; set; } = systemVariant;
    public bool IsReducedMotionRequested { get; set; } = reducedMotion;

    public ThemeVariant GetSystemVariant(ThemeVariant fallback)
        => SystemVariant is ThemeVariant.Light or ThemeVariant.Dark ? SystemVariant : fallback;
}
