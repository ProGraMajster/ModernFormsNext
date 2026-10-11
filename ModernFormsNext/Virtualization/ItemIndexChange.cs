using System.Collections.Specialized;

namespace ModernFormsNext.Virtualization;

/// <summary>Maps logical occurrences through one indexed collection notification without retaining item payloads.</summary>
/// <remarks>
/// Selection/focus consumers can apply the same mapping as the container generator. Equal or duplicate source values
/// are not conflated. Removed/replaced occurrences and resets map to -1; a host may restore by its own stable source key.
/// Notifications with unspecified indices conservatively reset the mapping. This immutable value has no thread affinity.
/// </remarks>
public readonly struct ItemIndexChange
{
    private ItemIndexChange(NotifyCollectionChangedAction action, int oldCount, int newCount,
        int oldIndex, int newIndex, int removed, int added)
    {
        Action = action; OldCount = oldCount; NewCount = newCount;
        OldIndex = oldIndex; NewIndex = newIndex; RemovedCount = removed; AddedCount = added;
    }
    /// <summary>Gets the normalized notification action.</summary>
    public NotifyCollectionChangedAction Action { get; }
    /// <summary>Gets the source count before the change.</summary>
    public int OldCount { get; }
    /// <summary>Gets the source count after the change.</summary>
    public int NewCount { get; }
    /// <summary>Gets the old affected index, or -1 for a reset/add.</summary>
    public int OldIndex { get; }
    /// <summary>Gets the new affected index, or -1 for a reset/remove.</summary>
    public int NewIndex { get; }
    /// <summary>Gets the old block length; moves retain these occurrences.</summary>
    public int RemovedCount { get; }
    /// <summary>Gets the new block length; moves retain these occurrences.</summary>
    public int AddedCount { get; }

    /// <summary>Creates a reset mapping that retires every old occurrence, without retaining source objects.</summary>
    /// <param name="oldCount">The nonnegative previous source count.</param>
    /// <param name="newCount">The nonnegative replacement source count.</param>
    public static ItemIndexChange Reset(int oldCount, int newCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(oldCount);
        ArgumentOutOfRangeException.ThrowIfNegative(newCount);
        return new(NotifyCollectionChangedAction.Reset, oldCount, newCount, -1, -1, oldCount, newCount);
    }

    /// <summary>Validates and snapshots indexed notification metadata against the old/new counts.</summary>
    /// <param name="notification">The notification after the source has changed. Payload objects are not retained.</param>
    /// <param name="oldCount">The logical count immediately before this notification.</param>
    /// <param name="newCount">The logical count immediately after this notification.</param>
    /// <returns>The occurrence mapping. Unspecified starting indices become an explicit reset.</returns>
    /// <exception cref="ArgumentException">Indexed block/count metadata is inconsistent.</exception>
    public static ItemIndexChange FromNotification(NotifyCollectionChangedEventArgs notification, int oldCount, int newCount)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentOutOfRangeException.ThrowIfNegative(oldCount);
        ArgumentOutOfRangeException.ThrowIfNegative(newCount);
        var action = notification.Action;
        if (!Enum.IsDefined(action)) throw new ArgumentException("Unknown collection action.", nameof(notification));
        if (action == NotifyCollectionChangedAction.Reset) return Reset(oldCount, newCount);
        int oldIndex = notification.OldStartingIndex, newIndex = notification.NewStartingIndex;
        int removed = notification.OldItems?.Count ?? 0, added = notification.NewItems?.Count ?? 0;
        bool needsOld = action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Move;
        bool needsNew = action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Move;
        if (needsOld && oldIndex < 0 || needsNew && newIndex < 0) return Reset(oldCount, newCount);
        bool valid = (!needsOld || (long)oldIndex + removed <= oldCount)
            && (!needsNew || (long)newIndex + added <= newCount);
        valid &= action switch
        {
            NotifyCollectionChangedAction.Add => (long)oldCount + added == newCount,
            NotifyCollectionChangedAction.Remove => (long)oldCount - removed == newCount,
            NotifyCollectionChangedAction.Replace => oldIndex == newIndex && (long)oldCount - removed + added == newCount,
            NotifyCollectionChangedAction.Move => oldCount == newCount && removed == added,
            _ => false
        };
        if (!valid) throw new ArgumentException("Collection block indices/counts are inconsistent.", nameof(notification));
        return new(action, oldCount, newCount, oldIndex, newIndex, removed, added);
    }

    /// <summary>Maps an old occurrence index to its new index; -1 remains no selection.</summary>
    /// <param name="index">An old index from zero through OldCount-1, or -1.</param>
    /// <returns>The new occurrence index or -1 if that occurrence was removed, replaced, or reset.</returns>
    public int MapIndex(int index)
    {
        if (index == -1) return -1;
        if ((uint)index >= (uint)OldCount) throw new ArgumentOutOfRangeException(nameof(index));
        switch (Action)
        {
            case NotifyCollectionChangedAction.Add: return index >= NewIndex ? index + AddedCount : index;
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (index >= OldIndex && index < OldIndex + RemovedCount) return -1;
                return index >= OldIndex + RemovedCount ? index + AddedCount - RemovedCount : index;
            case NotifyCollectionChangedAction.Move:
                if (index >= OldIndex && index < OldIndex + RemovedCount) return NewIndex + index - OldIndex;
                if (OldIndex < NewIndex && index >= OldIndex + RemovedCount && index < NewIndex + RemovedCount) return index - RemovedCount;
                if (NewIndex < OldIndex && index >= NewIndex && index < OldIndex) return index + RemovedCount;
                return index;
            default: return -1;
        }
    }
}
