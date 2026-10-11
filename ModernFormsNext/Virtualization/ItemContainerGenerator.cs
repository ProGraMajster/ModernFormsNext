using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace ModernFormsNext.Virtualization;

/// <summary>Realizes a bounded indexed range and recycles containers independently from any particular item control.</summary>
/// <typeparam name="TContainer">The reference-type container supplied by the borrowed factory.</typeparam>
/// <remarks>
/// Use all methods on the host's owning thread; this type is not thread-safe and does not own a dispatcher.
/// The factory must perform applicable framework UI-thread checks. Reentrant range requests are coalesced; indexed
/// collection changes retain order. A callback cannot recursively realize or overwrite a newer request. Disposal
/// requested inside a callback retires all owned containers after that callback returns. No finalizer performs UI work.
/// Any factory failure faults/disposes the generator, attempts cleanup of every owned container, and propagates errors.
/// Selection and focus are logical host state; use ItemIndexChange to map them, never container identity.
/// </remarks>
public sealed class ItemContainerGenerator<TContainer> : IDisposable where TContainer : class
{
    private sealed class Entry(TContainer container)
    {
        internal readonly TContainer Container = container;
        internal int Index;
        internal int PreparedIndex;
        internal bool Prepared;
    }

    private readonly IItemContainerFactory<TContainer> factory;
    private readonly Dictionary<int, Entry> realized = new();
    private readonly HashSet<Entry> owned = new();
    private readonly HashSet<TContainer> identities = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<Entry> recycled = new();
    private readonly List<Entry> scratch = new();
    private readonly List<Entry> retired = new();
    private readonly Queue<ItemIndexChange> changes = new();
    private RealizedRange requestedRange;
    private int itemCount, scheduledCount;
    private long generation, appliedGeneration = -1;
    private bool processing;

    /// <summary>Creates a generator with a bounded pool of cleared containers.</summary>
    /// <param name="factory">The borrowed factory. The generator does not dispose it or the logical source.</param>
    /// <param name="recyclingCapacity">The nonnegative maximum number of additional cleared containers to retain.</param>
    public ItemContainerGenerator(IItemContainerFactory<TContainer> factory, int recyclingCapacity = 64)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfNegative(recyclingCapacity);
        this.factory = factory;
        RecyclingCapacity = recyclingCapacity;
    }

    /// <summary>Gets the immutable maximum pool size, excluding realized containers.</summary>
    public int RecyclingCapacity { get; }
    /// <summary>Gets whether disposal was requested or a factory callback faulted the generator.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Gets the last completely realized range. During callbacks, TryGetContainer reflects the current committed mapping.</summary>
    public RealizedRange RealizedRange { get; private set; }
    /// <summary>Gets the current mapped container count, including in-progress bindings during callbacks.</summary>
    public int RealizedContainerCount => realized.Count;
    /// <summary>Gets the current retained pool count.</summary>
    public int RecycledContainerCount => recycled.Count;
    /// <summary>Gets the number of distinct non-null containers successfully acquired from CreateContainer.</summary>
    public long CreatedContainerCount { get; private set; }
    /// <summary>Gets the number of acquisitions from the cleared pool, including subsequently superseded bindings.</summary>
    public long ReusedContainerCount { get; private set; }
    /// <summary>Gets the number of DiscardContainer invocations. A thrown disposal is still an invocation, not a successful cleanup claim.</summary>
    public long DiscardedContainerCount { get; private set; }

    /// <summary>Gets the currently mapped container without realizing an item or changing focus.</summary>
    /// <param name="index">The logical occurrence index.</param>
    /// <param name="container">The mapped container, or null. A callback may still be preparing its binding.</param>
    public bool TryGetContainer(int index, [NotNullWhen(true)] out TContainer? container)
    {
        if (realized.TryGetValue(index, out var entry)) { container = entry.Container; return true; }
        container = null;
        return false;
    }

    /// <summary>Requests realization of exactly the supplied viewport/cache range.</summary>
    /// <param name="range">A validated range whose exclusive end does not exceed itemCount.</param>
    /// <param name="itemCount">The current nonnegative source count. Unnotified count changes conservatively reset bindings.</param>
    /// <remarks>
    /// Retired bindings are cleared before new bindings are prepared, allowing jump scrolling to reuse containers.
    /// A stable repeated request does no factory work or allocations. Only viewport/pool metadata is retained.
    /// </remarks>
    public void Realize(RealizedRange range, int itemCount)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        if (range.EndIndex > itemCount) throw new ArgumentOutOfRangeException(nameof(range));
        if (scheduledCount == itemCount && requestedRange == range && (processing || appliedGeneration == generation)) return;
        if (scheduledCount != itemCount)
        {
            changes.Enqueue(ItemIndexChange.Reset(scheduledCount, itemCount));
            scheduledCount = itemCount;
        }
        requestedRange = range;
        generation++;
        ProcessPending();
    }

    /// <summary>Maps realized occurrences after an indexed source notification and returns the shared logical-state mapping.</summary>
    /// <param name="notification">The source's change notification, after mutation.</param>
    /// <param name="newItemCount">The current source count immediately after this notification.</param>
    /// <returns>The mapping for selection/focus/viewport-anchor consumers. Payload values are not retained.</returns>
    /// <remarks>Notify before requesting the newly calculated range. Reentrant notifications are queued in order.</remarks>
    public ItemIndexChange NotifyItemsChanged(NotifyCollectionChangedEventArgs notification, int newItemCount)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var change = ItemIndexChange.FromNotification(notification, scheduledCount, newItemCount);
        changes.Enqueue(change);
        scheduledCount = newItemCount;
        requestedRange = requestedRange.Clamp(newItemCount);
        generation++;
        ProcessPending();
        return change;
    }

    /// <summary>Retires every binding and discards every owned container; does not dispose the factory/source.</summary>
    /// <remarks>
    /// Call on the owning thread. During a callback, cleanup is deferred until it returns. Otherwise cleanup is synchronous.
    /// All discard callbacks are attempted even when cleanup fails; failures propagate. Repeated disposal is inert.
    /// </remarks>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        generation++;
        if (!processing) Cleanup();
    }

    private void ProcessPending()
    {
        if (processing) return;
        processing = true;
        Exception? failure = null;
        try
        {
            // Match the framework's bounded stabilization policy without recursion/nested loops.
            for (int pass = 0; !IsDisposed; pass++)
            {
                if (pass == 64) throw new InvalidOperationException("Virtualization callbacks did not stabilize after 64 changes.");
                if (changes.TryDequeue(out var change)) { ApplyChange(change); continue; }
                long version = generation;
                ApplyRange(requestedRange, version);
                if (IsDisposed) break;
                if (version != generation) continue;
                RealizedRange = requestedRange;
                appliedGeneration = version;
                break;
            }
        }
        catch (Exception error) { IsDisposed = true; failure = error; }
        finally
        {
            processing = false;
            if (IsDisposed)
            {
                try { Cleanup(); }
                catch (Exception cleanupError) { failure = failure is null ? cleanupError : new AggregateException(failure, cleanupError); }
            }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void ApplyChange(ItemIndexChange change)
    {
        if (change.OldCount != itemCount) throw new InvalidOperationException("Collection notifications are out of sequence.");
        scratch.Clear();
        retired.Clear();
        foreach (var entry in realized.Values) scratch.Add(entry);
        // Publish the complete new mapping before clear callbacks can inspect/request newer state.
        realized.Clear();
        foreach (var entry in scratch)
        {
            int mapped = change.MapIndex(entry.Index);
            if (mapped < 0) retired.Add(entry);
            else { entry.Index = mapped; realized.Add(mapped, entry); }
        }
        itemCount = change.NewCount;
        RealizedRange = RealizedRange.Empty;
        foreach (var entry in retired)
        {
            if (IsDisposed) break;
            Recycle(entry);
        }
        scratch.Clear();
        retired.Clear();
    }

    private void ApplyRange(RealizedRange range, long version)
    {
        retired.Clear();
        foreach (var entry in realized.Values)
            if (!range.Contains(entry.Index)) retired.Add(entry);
        foreach (var entry in retired) realized.Remove(entry.Index);
        foreach (var entry in retired)
        {
            if (IsDisposed) return;
            Recycle(entry);
        }
        retired.Clear();
        if (IsDisposed || generation != version) return;
        for (int index = range.StartIndex; index < range.EndIndex; index++)
        {
            if (!realized.TryGetValue(index, out var entry))
            {
                entry = Acquire();
                entry.Index = index;
                realized.Add(index, entry);
                if (IsDisposed || generation != version) return;
            }
            if (entry.Prepared && entry.PreparedIndex == index) continue;
            ClearBinding(entry);
            if (IsDisposed || generation != version) return;
            // Record ownership/binding before application code. Failure/disposal can retire even
            // a partially prepared binding without resurrecting it after the callback returns.
            entry.Prepared = true;
            entry.PreparedIndex = index;
            factory.PrepareContainer(entry.Container, index);
            if (IsDisposed || generation != version) return;
        }
    }

    private Entry Acquire()
    {
        if (recycled.TryPop(out var existing)) { ReusedContainerCount++; return existing; }
        var container = factory.CreateContainer() ?? throw new InvalidOperationException("A container factory returned null.");
        if (!identities.Add(container)) throw new InvalidOperationException("A container factory returned an already-owned instance.");
        var entry = new Entry(container);
        owned.Add(entry);
        CreatedContainerCount++;
        return entry;
    }

    private void ClearBinding(Entry entry)
    {
        if (!entry.Prepared) return;
        entry.Prepared = false;
        factory.ClearContainer(entry.Container, entry.PreparedIndex);
    }

    private void Recycle(Entry entry)
    {
        ClearBinding(entry);
        if (IsDisposed) return; // Cleanup still owns this detached entry.
        if (recycled.Count < RecyclingCapacity) recycled.Push(entry);
        else Discard(entry);
    }

    private void Discard(Entry entry)
    {
        owned.Remove(entry);
        identities.Remove(entry.Container);
        DiscardedContainerCount++;
        factory.DiscardContainer(entry.Container);
    }

    private void Cleanup()
    {
        // Explicit/fault disposal may allocate a bounded snapshot. Ordinary scrolling does not.
        var entries = owned.ToArray();
        owned.Clear(); identities.Clear(); realized.Clear(); recycled.Clear(); changes.Clear(); scratch.Clear(); retired.Clear();
        requestedRange = RealizedRange = RealizedRange.Empty;
        List<Exception>? errors = null;
        foreach (var entry in entries)
        {
            try { ClearBinding(entry); } catch (Exception error) { (errors ??= []).Add(error); }
            try { DiscardedContainerCount++; factory.DiscardContainer(entry.Container); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors is not null) throw new AggregateException("Virtualized container cleanup failed.", errors);
    }
}
