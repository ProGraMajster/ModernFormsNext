using System.Collections;

namespace ModernFormsNext.Drawing;

/// <summary>Provides the observable ordered children of a <see cref="GeometryGroup"/>.</summary>
/// <remarks>
/// Children are borrowed and shareable, not disposed by the collection. Repeated children contribute
/// repeated contours, but a child mutation raises one notification per owning collection. Changes
/// must occur on the UI thread while rendered; cycles are rejected before mutation.
/// </remarks>
public sealed class GeometryCollection : IList<Geometry>, IReadOnlyList<Geometry>
{
    private readonly Geometry owner;
    private readonly List<Geometry> items = [];
    private readonly Dictionary<Geometry, (int Count, WeakGeometrySubscription<GeometryCollection> Listener)> subscriptions
        = new(ReferenceEqualityComparer.Instance);

    internal GeometryCollection(Geometry owner) => this.owner = owner;

    /// <summary>Occurs after children, ordering or a contained geometry changes.</summary>
    public event EventHandler? Changed;
    /// <inheritdoc/>
    public int Count => items.Count;
    /// <inheritdoc/>
    public bool IsReadOnly => false;
    /// <inheritdoc/>
    public Geometry this[int index]
    {
        get => items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var previous = items[index];
            if (ReferenceEquals(previous, value)) return;
            GeometryGraph.RequireAcyclic(owner, value);
            Release(previous);
            items[index] = value;
            Acquire(value);
            OnChanged();
        }
    }
    /// <inheritdoc/>
    public void Add(Geometry item) => Insert(items.Count, item);
    /// <inheritdoc/>
    public void Insert(int index, Geometry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        GeometryGraph.RequireAcyclic(owner, item);
        items.Insert(index, item);
        Acquire(item);
        OnChanged();
    }
    /// <inheritdoc/>
    public bool Remove(Geometry item)
    {
        int index = items.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }
    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        var item = items[index];
        items.RemoveAt(index);
        Release(item);
        OnChanged();
    }
    /// <inheritdoc/>
    public void Clear()
    {
        if (items.Count == 0) return;
        foreach (var entry in subscriptions.Values) entry.Listener.Dispose();
        subscriptions.Clear();
        items.Clear();
        OnChanged();
    }
    /// <summary>Moves a child and raises one notification without changing subscriptions.</summary>
    /// <param name="oldIndex">Its current zero-based index.</param>
    /// <param name="newIndex">Its destination zero-based index.</param>
    public void Move(int oldIndex, int newIndex)
    {
        if ((uint)oldIndex >= (uint)items.Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if ((uint)newIndex >= (uint)items.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex) return;
        var item = items[oldIndex];
        items.RemoveAt(oldIndex);
        items.Insert(newIndex, item);
        OnChanged();
    }
    /// <inheritdoc/>
    public bool Contains(Geometry item) => items.Contains(item);
    /// <inheritdoc/>
    public int IndexOf(Geometry item) => items.IndexOf(item);
    /// <inheritdoc/>
    public void CopyTo(Geometry[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
    /// <inheritdoc/>
    public IEnumerator<Geometry> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Acquire(Geometry item)
    {
        if (subscriptions.TryGetValue(item, out var entry)) subscriptions[item] = (entry.Count + 1, entry.Listener);
        else subscriptions.Add(item, (1, new(this, item, static collection => collection.OnChanged())));
    }
    private void Release(Geometry item)
    {
        var entry = subscriptions[item];
        if (entry.Count > 1) subscriptions[item] = (entry.Count - 1, entry.Listener);
        else { subscriptions.Remove(item); entry.Listener.Dispose(); }
    }
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
