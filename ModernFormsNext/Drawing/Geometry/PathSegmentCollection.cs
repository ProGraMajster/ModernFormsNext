using System.Collections;

namespace ModernFormsNext.Drawing;

/// <summary>Provides an observable ordered collection of <see cref="PathSegment"/> values.</summary>
public sealed class PathSegmentCollection : IList<PathSegment>, IReadOnlyList<PathSegment>
{
    private readonly List<PathSegment> items = [];
    private readonly Dictionary<PathSegment, int> subscriptions = new(ReferenceEqualityComparer.Instance);

    /// <summary>Occurs after items, ordering, or a contained segment changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public int Count => items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public PathSegment this[int index]
    {
        get => items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            PathSegment previous = items[index];
            if (ReferenceEquals(previous, value))
                return;

            Release(previous);
            items[index] = value;
            Acquire(value);
            OnChanged();
        }
    }

    /// <inheritdoc/>
    public void Add(PathSegment item)
    {
        ArgumentNullException.ThrowIfNull(item);
        items.Add(item);
        Acquire(item);
        OnChanged();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (items.Count == 0)
            return;

        foreach (PathSegment item in subscriptions.Keys.ToArray())
            item.Changed -= HandleItemChanged;
        subscriptions.Clear();
        items.Clear();
        OnChanged();
    }

    /// <inheritdoc/>
    public bool Contains(PathSegment item) => items.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(PathSegment[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<PathSegment> GetEnumerator() => items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(PathSegment item) => items.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, PathSegment item)
    {
        ArgumentNullException.ThrowIfNull(item);
        items.Insert(index, item);
        Acquire(item);
        OnChanged();
    }

    /// <summary>Moves a segment without changing subscriptions and raises one change notification.</summary>
    /// <param name="oldIndex">The current zero-based position.</param>
    /// <param name="newIndex">The destination zero-based position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the collection.
    /// </exception>
    public void Move(int oldIndex, int newIndex)
    {
        if ((uint)oldIndex >= (uint)items.Count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if ((uint)newIndex >= (uint)items.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex)
            return;

        PathSegment item = items[oldIndex];
        items.RemoveAt(oldIndex);
        items.Insert(newIndex, item);
        OnChanged();
    }

    /// <inheritdoc/>
    public bool Remove(PathSegment item)
    {
        int index = items.IndexOf(item);
        if (index < 0)
            return false;

        RemoveAt(index);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        PathSegment item = items[index];
        items.RemoveAt(index);
        Release(item);
        OnChanged();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Acquire(PathSegment item)
    {
        if (subscriptions.TryGetValue(item, out int count))
        {
            subscriptions[item] = count + 1;
            return;
        }

        subscriptions.Add(item, 1);
        item.Changed += HandleItemChanged;
    }

    private void Release(PathSegment item)
    {
        int count = subscriptions[item];
        if (count > 1)
        {
            subscriptions[item] = count - 1;
            return;
        }

        subscriptions.Remove(item);
        item.Changed -= HandleItemChanged;
    }

    private void HandleItemChanged(object? sender, EventArgs e) => OnChanged();

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
