using System;
using System.Collections;
using System.Collections.Generic;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents an observable, ordered collection of gradient stops.
/// </summary>
/// <remarks>
/// <para>
/// Collection order is preserved for editing and serialization. Rendering uses a stable sort by
/// <see cref="GradientStop.Offset"/>, so stops may be added in any order and multiple distinct
/// stops may share an offset. Mutate the collection on the UI thread when it is used by controls.
/// </para>
/// <para>
/// The collection subscribes to each contained stop. Adding, replacing, moving, removing,
/// clearing, or mutating a stop raises <see cref="Changed"/> exactly once for that operation.
/// </para>
/// </remarks>
public sealed class GradientStopCollection : IList<GradientStop>, IReadOnlyList<GradientStop>
{
    private readonly List<GradientStop> items = [];

    /// <summary>
    /// Occurs when collection membership, order, or a contained stop changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the number of gradient stops.
    /// </summary>
    public int Count => items.Count;

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or replaces the stop at the specified collection position.
    /// </summary>
    /// <param name="index">The zero-based collection position.</param>
    /// <exception cref="ArgumentNullException">Thrown when the assigned stop is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the same stop instance already exists at another position. Distinct stop
    /// instances with equal offsets are supported.
    /// </exception>
    public GradientStop this[int index]
    {
        get => items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            GradientStop previous = items[index];
            if (ReferenceEquals(previous, value))
                return;

            EnsureNewInstance(value);
            previous.Changed -= HandleStopChanged;
            items[index] = value;
            value.Changed += HandleStopChanged;
            OnChanged();
        }
    }

    /// <summary>
    /// Adds a stop to the end of the editable collection order.
    /// </summary>
    /// <param name="item">The non-null stop to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the same stop instance is already in this collection.
    /// </exception>
    public void Add(GradientStop item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureNewInstance(item);
        items.Add(item);
        item.Changed += HandleStopChanged;
        OnChanged();
    }

    /// <summary>
    /// Adds each stop from a sequence, preserving its collection order.
    /// </summary>
    /// <param name="stops">The non-null sequence of non-null, distinct stop instances.</param>
    /// <remarks>
    /// The method raises <see cref="Changed"/> once after all stops are added. Validation happens
    /// before mutation, so a duplicate or null entry leaves the collection unchanged.
    /// </remarks>
    public void AddRange(IEnumerable<GradientStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        GradientStop[] additions = stops is GradientStop[] array ? (GradientStop[])array.Clone() : [.. stops];
        var unique = new HashSet<GradientStop>(ReferenceEqualityComparer.Instance);

        foreach (GradientStop stop in additions)
        {
            ArgumentNullException.ThrowIfNull(stop);
            EnsureNewInstance(stop);
            if (!unique.Add(stop))
                throw new ArgumentException("The sequence contains the same gradient stop instance more than once.", nameof(stops));
        }

        if (additions.Length == 0)
            return;

        foreach (GradientStop stop in additions)
        {
            items.Add(stop);
            stop.Changed += HandleStopChanged;
        }

        OnChanged();
    }

    /// <summary>
    /// Removes all stops and their item subscriptions.
    /// </summary>
    public void Clear()
    {
        if (items.Count == 0)
            return;

        foreach (GradientStop stop in items)
            stop.Changed -= HandleStopChanged;

        items.Clear();
        OnChanged();
    }

    /// <summary>
    /// Determines whether the exact stop instance belongs to the collection.
    /// </summary>
    /// <param name="item">The stop instance to find.</param>
    /// <returns><see langword="true"/> when the same instance is present.</returns>
    public bool Contains(GradientStop item) => IndexOf(item) >= 0;

    /// <summary>
    /// Copies the editable collection order to an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination index.</param>
    public void CopyTo(GradientStop[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    /// <summary>
    /// Returns an enumerator over the editable collection order.
    /// </summary>
    public IEnumerator<GradientStop> GetEnumerator() => items.GetEnumerator();

    /// <summary>
    /// Returns the position of the exact stop instance.
    /// </summary>
    /// <param name="item">The stop instance to find.</param>
    /// <returns>The zero-based index, or -1 when absent.</returns>
    public int IndexOf(GradientStop item)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], item))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Inserts a stop at a specified editable collection position.
    /// </summary>
    /// <param name="index">The zero-based insertion position.</param>
    /// <param name="item">The non-null stop to insert.</param>
    public void Insert(int index, GradientStop item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureNewInstance(item);
        items.Insert(index, item);
        item.Changed += HandleStopChanged;
        OnChanged();
    }

    /// <summary>
    /// Moves a stop without changing its identity or offset.
    /// </summary>
    /// <param name="oldIndex">The current zero-based position.</param>
    /// <param name="newIndex">The destination zero-based position.</param>
    /// <remarks>
    /// Moving matters when two or more stops have the same offset because stable rendering order
    /// follows this editable order for equal offsets.
    /// </remarks>
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
            return;

        GradientStop item = items[oldIndex];
        items.RemoveAt(oldIndex);
        items.Insert(newIndex, item);
        OnChanged();
    }

    /// <summary>
    /// Removes the exact stop instance when present.
    /// </summary>
    /// <param name="item">The stop instance to remove.</param>
    /// <returns><see langword="true"/> when a stop was removed.</returns>
    public bool Remove(GradientStop item)
    {
        int index = IndexOf(item);
        if (index < 0)
            return false;

        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes and unsubscribes the stop at a specified position.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    public void RemoveAt(int index)
    {
        GradientStop item = items[index];
        items.RemoveAt(index);
        item.Changed -= HandleStopChanged;
        OnChanged();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureNewInstance(GradientStop item)
    {
        if (IndexOf(item) >= 0)
            throw new ArgumentException("The same gradient stop instance cannot be added more than once.", nameof(item));
    }

    private void HandleStopChanged(object? sender, EventArgs e) => OnChanged();

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
