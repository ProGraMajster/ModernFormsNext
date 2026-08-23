using System.Collections.Concurrent;
using ModernFormsNext.Designer.Services;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerPersistenceInfrastructureTests
{
    [Fact]
    public async Task SystemSchedulerRunsOneShotCallbackOnce()
    {
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        using var handle = SystemDesignerOneShotScheduler.Instance.Schedule(
            TimeSpan.FromMilliseconds(10),
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                fired.TrySetResult();
            });

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, Volatile.Read(ref invocationCount));
        Assert.False(handle.Cancel());
    }

    [Fact]
    public void SystemSchedulerCancellationIsIdempotentAndReleasesPendingCallback()
    {
        var callbackInvoked = false;
        using var handle = SystemDesignerOneShotScheduler.Instance.Schedule(
            TimeSpan.FromMinutes(1),
            () => callbackInvoked = true);

        Assert.True(handle.Cancel());
        Assert.False(handle.Cancel());
        Assert.False(callbackInvoked);
    }

    [Fact]
    public void SystemSchedulerRejectsNegativeDelay()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            SystemDesignerOneShotScheduler.Instance.Schedule(TimeSpan.FromTicks(-1), static () => { }));

    [Fact]
    public void ManualSchedulerUsesUtcClockAndHonorsCancellationDeterministically()
    {
        var start = new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);
        var scheduler = new ManualDesignerOneShotScheduler(start);
        var calls = new List<string>();
        using var cancelled = scheduler.Schedule(TimeSpan.FromSeconds(2), () => calls.Add("cancelled"));
        using var first = scheduler.Schedule(TimeSpan.FromSeconds(1), () => calls.Add("first"));
        using var second = scheduler.Schedule(TimeSpan.FromSeconds(2), () => calls.Add("second"));

        Assert.True(cancelled.Cancel());
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        Assert.Equal(["first"], calls);
        Assert.Equal(start.AddSeconds(1), scheduler.UtcNow);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        Assert.Equal(["first", "second"], calls);
        Assert.Equal(start.AddSeconds(2), scheduler.UtcNow);
    }

    [Fact]
    public async Task FileSystemSourceEmitsRawEventsOnlyForExactDesignerTargets()
    {
        using var temporary = new TemporaryDirectory();
        var designPath = IOPath.Combine(temporary.Path, "MainForm.mfdesign");
        var generatedPath = IOPath.Combine(temporary.Path, "MainForm.Designer.cs");
        var movedPath = IOPath.Combine(temporary.Path, "Moved.mfdesign");
        var unrelatedPath = IOPath.Combine(temporary.Path, "notes.txt");
        var observed = new ConcurrentQueue<DesignerFileChangeEventArgs>();
        using var source = FileSystemDesignerFileChangeSourceFactory.Instance.Create(designPath);
        source.Changed += (_, e) => observed.Enqueue(e);

        Assert.Equal(IOPath.GetFullPath(designPath), source.DesignDocumentPath);
        Assert.Equal(IOPath.GetFullPath(generatedPath), source.GeneratedCodePath);

        File.WriteAllText(unrelatedPath, "not a Designer target");

        await ActAndWaitAsync(
            source,
            e => IsPath(e.Path, designPath)
                && e.Kind is DesignerFileChangeKind.Created or DesignerFileChangeKind.Changed,
            () => File.WriteAllText(designPath, "{}"));

        await ActAndWaitAsync(
            source,
            e => IsPath(e.Path, generatedPath)
                && e.Kind is DesignerFileChangeKind.Created or DesignerFileChangeKind.Changed,
            () => File.WriteAllText(generatedPath, "// generated"));

        await ActAndWaitAsync(
            source,
            e => e.Kind == DesignerFileChangeKind.Renamed
                && e.OldPath is not null
                && IsPath(e.OldPath, designPath),
            () => File.Move(designPath, movedPath));

        await ActAndWaitAsync(
            source,
            e => e.Kind == DesignerFileChangeKind.Renamed
                && IsPath(e.Path, designPath),
            () => File.Move(movedPath, designPath));

        await ActAndWaitAsync(
            source,
            e => e.Kind == DesignerFileChangeKind.Deleted && IsPath(e.Path, designPath),
            () => File.Delete(designPath));

        Assert.DoesNotContain(observed, e => IsPath(e.Path, unrelatedPath)
            || e.OldPath is not null && IsPath(e.OldPath, unrelatedPath));
    }

    [Fact]
    public void FileSystemSourceValidatesTargetAndDisposesIdempotently()
    {
        using var temporary = new TemporaryDirectory();
        var invalidPath = IOPath.Combine(temporary.Path, "MainForm.cs");
        Assert.Throws<ArgumentException>(() =>
            FileSystemDesignerFileChangeSourceFactory.Instance.Create(invalidPath));

        var source = FileSystemDesignerFileChangeSourceFactory.Instance.Create(
            IOPath.Combine(temporary.Path, "MainForm.mfdesign"));
        source.Dispose();
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            source.Changed += static (_, _) => { });
    }

    [Fact]
    public void FileSystemWatcherErrorRequestsStableRecheckOfBothExactTargets()
    {
        using var temporary = new TemporaryDirectory();
        var designPath = IOPath.Combine(temporary.Path, "MainForm.mfdesign");
        using var source = new FileSystemDesignerFileChangeSource(designPath);
        var observed = new List<DesignerFileChangeEventArgs>();
        source.Changed += (_, e) => observed.Add(e);

        source.PublishFullRecheck();

        Assert.Collection(
            observed,
            change =>
            {
                Assert.Equal(DesignerFileChangeKind.Changed, change.Kind);
                Assert.True(IsPath(change.Path, designPath));
            },
            change =>
            {
                Assert.Equal(DesignerFileChangeKind.Changed, change.Kind);
                Assert.True(IsPath(change.Path, IOPath.Combine(temporary.Path, "MainForm.Designer.cs")));
            });
    }

    private static async Task<DesignerFileChangeEventArgs> ActAndWaitAsync(
        IDesignerFileChangeSource source,
        Func<DesignerFileChangeEventArgs, bool> predicate,
        Action action)
    {
        var completion = new TaskCompletionSource<DesignerFileChangeEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<DesignerFileChangeEventArgs>? handler = null;
        handler = (_, e) =>
        {
            if (predicate(e))
                completion.TrySetResult(e);
        };

        source.Changed += handler;
        try
        {
            action();
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            source.Changed -= handler;
        }
    }

    private static bool IsPath(string actual, string expected)
        => string.Equals(
            IOPath.GetFullPath(actual),
            IOPath.GetFullPath(expected),
            StringComparison.OrdinalIgnoreCase);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = IOPath.Combine(
                IOPath.GetTempPath(),
                "ModernFormsNext.Designer.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

/// <summary>
/// Deterministic one-shot scheduler shared by Designer persistence tests.
/// </summary>
internal sealed class ManualDesignerOneShotScheduler : IDesignerOneShotScheduler
{
    private readonly List<Entry> entries = [];
    private long nextSequence;

    public ManualDesignerOneShotScheduler(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public IDesignerScheduledHandle Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay));

        var entry = new Entry(UtcNow + delay, nextSequence++, callback);
        entries.Add(entry);
        return entry;
    }

    public void AdvanceBy(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        var target = UtcNow + duration;
        while (true)
        {
            var next = entries
                .Where(entry => entry.IsPending && entry.DueUtc <= target)
                .OrderBy(entry => entry.DueUtc)
                .ThenBy(entry => entry.Sequence)
                .FirstOrDefault();
            if (next is null)
                break;

            entries.Remove(next);
            UtcNow = next.DueUtc;
            next.Fire();
        }

        entries.RemoveAll(entry => !entry.IsPending);
        UtcNow = target;
    }

    private sealed class Entry(
        DateTimeOffset dueUtc,
        long sequence,
        Action callback) : IDesignerScheduledHandle
    {
        private Action? callback = callback;

        public DateTimeOffset DueUtc { get; } = dueUtc;

        public long Sequence { get; } = sequence;

        public bool IsPending => callback is not null;

        public bool Cancel()
        {
            if (callback is null)
                return false;

            callback = null;
            return true;
        }

        public void Dispose()
            => Cancel();

        public void Fire()
        {
            var action = callback;
            callback = null;
            action?.Invoke();
        }
    }
}
