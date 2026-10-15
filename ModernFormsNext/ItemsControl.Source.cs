using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext;

public abstract partial class ItemsControl
{
    private readonly Queue<(NotifyCollectionChangedEventArgs Notification, int Count)> itemChanges = new();
    private bool handlingItemChanges;

    /// <summary>Replaces the borrowed indexed source, clearing logical selection/focus and all old bindings.</summary>
    /// <param name="source">A UI-thread IList, or null for an empty source. The host never disposes it or its values.</param>
    /// <remarks>
    /// Subscribes to INotifyCollectionChanged, or IBindingList when INCC is absent. Indexed notifications preserve
    /// occurrence identity; unknown/out-of-order indices conservatively reset it. Mutate on the owning UI thread.
    /// Runtime templates and sources are deliberately protected hooks rather than Designer-serialized delegates.
    /// A paged provider must expose cached synchronous values; getters must not block the dispatcher.
    /// </remarks>
    protected void SetItemsSource(IList? source)
    {
        VerifyItemsAccess();
        if (ReferenceEquals(source, itemsSource)) return;
        int count = source?.Count ?? 0;
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        UnsubscribeSource();
        itemsSource = source; sourceCount = count;
        itemsVersion++;
        itemChanges.Clear();
        bool selectionChanged = selectedIndex != -1;
        selectedIndex = focusedIndex = -1; restoreFocus = false;
        SubscribeSource();
        ResetMetrics((-1, 0), requestLayout: false);
        long version = itemsVersion;
        ReplaceGenerator();
        if (version != itemsVersion || IsDisposed || disposingItems) return;
        if (selectionChanged) OnSelectedIndexChanged(EventArgs.Empty);
        if (version == itemsVersion && !IsDisposed && !disposingItems) { RequestItemsLayout(); Invalidate(); }
    }

    /// <summary>Publishes a conservative reset after a source without notifications changes.</summary>
    /// <remarks>UI thread only. Clears logical selection/focus and bindings because occurrence history is unknown.
    /// Use after same-count replacements in a plain IList; unnotified count changes are also detected by layout.</remarks>
    protected void RefreshItemsSource()
    {
        VerifyItemsAccess();
        if (itemsSource is not null) HandleItemsChanged(itemsSource, new(NotifyCollectionChangedAction.Reset));
    }

    private void SubscribeSource()
    {
        if (itemsSource is INotifyCollectionChanged observable) observable.CollectionChanged += HandleItemsChanged;
        else if (itemsSource is IBindingList binding) binding.ListChanged += HandleBindingListChanged;
    }

    private void UnsubscribeSource()
    {
        if (itemsSource is INotifyCollectionChanged observable) observable.CollectionChanged -= HandleItemsChanged;
        else if (itemsSource is IBindingList binding) binding.ListChanged -= HandleBindingListChanged;
    }

    private void HandleBindingListChanged(object? sender, ListChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, itemsSource)) return;
        VerifyItemsAccess();
        int currentCount = itemsSource!.Count;
        if ((e.ListChangedType is ListChangedType.ItemAdded or ListChangedType.ItemChanged or ListChangedType.ItemMoved) &&
                (uint)e.NewIndex >= (uint)currentCount ||
            e.ListChangedType == ListChangedType.ItemMoved && (uint)e.OldIndex >= (uint)sourceCount ||
            e.ListChangedType == ListChangedType.ItemDeleted && (uint)e.NewIndex >= (uint)sourceCount ||
            e.ListChangedType == ListChangedType.ItemChanged && currentCount != sourceCount)
        {
            // Unknown/all-item or nested out-of-order notifications have no safe occurrence
            // index. Normalize before looking up a value or refreshing an old container.
            HandleItemsChanged(sender, new(NotifyCollectionChangedAction.Reset));
            return;
        }
        if (e.ListChangedType == ListChangedType.ItemChanged && e.PropertyDescriptor is not null)
        {
            // Canonical DataBindings already observe properties; the default textual adapter also needs
            // a bounded refresh. Property changes do not replace an occurrence or clear logical selection.
            SuspendLayout();
            try { generator?.RefreshItem(e.NewIndex); }
            finally { ResumeLayout(!disposingItems && generator?.IsDisposed != true); }
            if (!disposingItems && !IsDisposed) RequestItemsLayout();
            return;
        }
        NotifyCollectionChangedEventArgs notification = e.ListChangedType switch
        {
            ListChangedType.ItemAdded => new(NotifyCollectionChangedAction.Add, itemsSource![e.NewIndex], e.NewIndex),
            ListChangedType.ItemDeleted => new(NotifyCollectionChangedAction.Remove, (object?)null, e.NewIndex),
            ListChangedType.ItemMoved => new(NotifyCollectionChangedAction.Move, itemsSource![e.NewIndex], e.NewIndex, e.OldIndex),
            ListChangedType.ItemChanged => new(NotifyCollectionChangedAction.Replace, itemsSource![e.NewIndex], (object?)null, e.NewIndex),
            _ => new(NotifyCollectionChangedAction.Reset)
        };
        HandleItemsChanged(sender, notification);
    }

    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, itemsSource)) return;
        VerifyItemsAccess();
        itemsVersion++;
        itemChanges.Enqueue((e, itemsSource!.Count));
        if (handlingItemChanges) return;
        handlingItemChanges = true;
        SuspendLayout();
        try
        {
            for (int pass = 0; itemChanges.TryDequeue(out var pending); pass++)
            {
                if (pass == 64) throw new InvalidOperationException("Item source notifications did not stabilize after 64 callbacks.");
                if (IsDisposed || disposingItems) return;
                var anchor = CaptureAnchor();
                CaptureFocusedOccurrence();
                ItemIndexChange change;
                var notification = pending.Notification;
                try { change = ItemIndexChange.FromNotification(notification, sourceCount, pending.Count); }
                catch (ArgumentException)
                {
                    // Another observer can publish nested mutations before this handler receives the outer
                    // notification. The old index history is then unknowable; reset rather than invent identity.
                    notification = new(NotifyCollectionChangedAction.Reset);
                    change = ItemIndexChange.Reset(sourceCount, pending.Count);
                }
                int previousSelection = selectedIndex;
                selectedIndex = change.MapIndex(selectedIndex);
                focusedIndex = change.MapIndex(focusedIndex);
                anchor = (change.MapIndex(anchor.Index), anchor.Offset);
                if (measurementAnchor is not null) measurementAnchor = anchor.Index >= 0 ? anchor : null;
                metrics?.ApplyChange(change);
                sourceCount = pending.Count;
                var currentGenerator = generator;
                long version = itemsVersion;
                // Commit logical state before clearing/preparing visual bindings can call application code.
                if (virtualizationEnabled) currentGenerator?.NotifyItemsChanged(notification, sourceCount);
                if (version != itemsVersion || IsDisposed || disposingItems) continue;
                if (selectedIndex != previousSelection) OnSelectedIndexChanged(EventArgs.Empty);
                if (version != itemsVersion || IsDisposed || disposingItems) continue;
                RestoreAnchor(anchor);
                RequestItemsLayout();
                Invalidate();
            }
        }
        finally
        {
            handlingItemChanges = false;
            // Preserve the original template/generator failure. Its cleanup has already retired
            // owned controls; do not immediately enter the faulted generator from a deferred layout.
            ResumeLayout(!disposingItems && generator?.IsDisposed != true);
        }
    }
}
