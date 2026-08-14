using System.Collections;
using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Provides an observable, ordered collection of finite vector points.
/// </summary>
/// <remarks>
/// Every successful mutation raises <see cref="Changed"/> synchronously. A point is a value type;
/// change an existing point by assigning through the indexer. The collection is not thread-safe
/// and should be mutated on the UI thread while it is rendered.
/// </remarks>
public sealed class PointCollection : IList<PointF>, IReadOnlyList<PointF>
{
    private readonly List<PointF> points = [];

    /// <summary>Initializes an empty point collection.</summary>
    public PointCollection()
    {
    }

    /// <summary>Initializes a point collection from the supplied finite points.</summary>
    /// <param name="points">Points to copy in enumeration order.</param>
    public PointCollection(IEnumerable<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        foreach (PointF point in points)
        {
            Geometry.ValidatePoint(point, nameof(points));
            this.points.Add(point);
        }
    }

    /// <summary>Occurs after the collection contents or ordering changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public int Count => points.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public PointF this[int index]
    {
        get => points[index];
        set
        {
            Geometry.ValidatePoint(value, nameof(value));
            if (points[index] == value)
                return;

            points[index] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public void Add(PointF item)
    {
        Geometry.ValidatePoint(item, nameof(item));
        points.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds validated points in enumeration order and raises one change notification.</summary>
    /// <param name="items">Points to append.</param>
    public void AddRange(IEnumerable<PointF> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        PointF[] validated = items.ToArray();
        foreach (PointF point in validated)
            Geometry.ValidatePoint(point, nameof(items));
        if (validated.Length == 0)
            return;

        points.AddRange(validated);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (points.Count == 0)
            return;

        points.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public bool Contains(PointF item) => points.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(PointF[] array, int arrayIndex) => points.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<PointF> GetEnumerator() => points.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(PointF item) => points.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, PointF item)
    {
        Geometry.ValidatePoint(item, nameof(item));
        points.Insert(index, item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves one point to another collection position and raises one change notification.</summary>
    /// <param name="oldIndex">The current zero-based position.</param>
    /// <param name="newIndex">The destination zero-based position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the collection.
    /// </exception>
    public void Move(int oldIndex, int newIndex)
    {
        if ((uint)oldIndex >= (uint)points.Count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if ((uint)newIndex >= (uint)points.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex)
            return;

        PointF item = points[oldIndex];
        points.RemoveAt(oldIndex);
        points.Insert(newIndex, item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public bool Remove(PointF item)
    {
        if (!points.Remove(item))
            return false;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        points.RemoveAt(index);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
