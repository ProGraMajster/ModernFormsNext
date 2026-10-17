using System.Collections;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext;

/// <summary>Displays a borrowed indexed collection using shared, bounded, recyclable control containers.</summary>
/// <remarks>
/// All source changes, templates, selection and layout operations require the owning UI thread.
/// The source and factory remain application-owned; the view owns returned containers. The default
/// template is a focusable text row. Selection is the single logical occurrence represented by
/// <see cref="ItemsControl.SelectedIndex"/>, independently of recycled controls and duplicate values.
/// Runtime templates use the existing factory contract; they are never executed by Designer metadata.
/// The inherited VirtualizationEnabled switch suspends realization when false; there is no eager fallback.
/// </remarks>
/// <example><code>
/// var view = new CollectionView { Dock = DockStyle.Fill, ItemSize = new(240, 36) };
/// view.ItemsSource = new System.Collections.ObjectModel.ObservableCollection&lt;string&gt; { "First", "Second" };
/// view.SelectedIndexChanged += (_, _) =&gt; Console.WriteLine(view.SelectedItem);
/// </code></example>
[DefaultProperty(nameof(ItemSize)), DefaultEvent(nameof(ItemInvoked))]
public class CollectionView : ItemsControl
{
    private IItemContainerFactory<Control>? itemTemplate;
    // A replacement factory must never clear/discard containers created by its predecessor.
    // This bounded association records ownership only; the generator remains the sole recycler.
    private sealed record TemplateBinding(IItemContainerFactory<Control>? Factory);
    private readonly ConditionalWeakTable<Control, TemplateBinding> containerTemplates = new();

    internal bool OwnsItemContainer(Control container) => containerTemplates.TryGetValue(container, out _);
    internal object? GetAccessibilityItem(int index) => GetItem(index);
    internal bool UsesDefaultItemTemplate => itemTemplate is null;

    /// <summary>Creates an empty collection view with shared virtualization enabled.</summary>
    public CollectionView() { }

    /// <summary>Gets or sets the borrowed synchronous indexed source, or null for an empty view.</summary>
    /// <remarks>
    /// UI thread only. Replacement clears selection, focus and old bindings. Indexed collection
    /// notifications preserve unaffected occurrences; values and duplicate references are not identities.
    /// Plain IList mutations require <see cref="RefreshItems"/>. Getters must not perform blocking I/O.
    /// The source and its values are never disposed by this control. Not serialized by Designer.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IList? ItemsSource { get => ItemsSourceCore; set => SetItemsSource(value); }

    /// <summary>Gets or sets the borrowed runtime factory for recyclable item controls, or null for text rows.</summary>
    /// <remarks>
    /// UI thread only. Changing the factory discards old containers through their original factory,
    /// resets measurements and requests layout while retaining logical selection. The factory must
    /// return distinct, unparented controls and retire its subscriptions/state in ClearContainer.
    /// PrepareContainer receives the current source index; ClearContainer receives the previous index
    /// after a possible source mutation, so retain old binding data instead of looking it up again.
    /// Canonical DataBindings are cleared before that callback. DiscardContainer releases factory
    /// resources; the host also disposes the control. The factory itself is never disposed.
    /// Templates/delegates are runtime-only and are not invoked or serialized by Designer.
    /// </remarks>
    /// <example><code>
    /// sealed class TextRows(CollectionView owner) : IItemContainerFactory&lt;Control&gt;
    /// {
    ///     public Control CreateContainer() =&gt; new Label();
    ///     public void PrepareContainer(Control row, int index)
    ///         =&gt; row.Text = owner.ItemsSource![index]?.ToString() ?? string.Empty;
    ///     public void ClearContainer(Control row, int previousIndex) =&gt; row.Text = string.Empty;
    ///     public void DiscardContainer(Control row) =&gt; row.Dispose();
    /// }
    /// // view.ItemTemplate = new TextRows(view);
    /// </code></example>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IItemContainerFactory<Control>? ItemTemplate
    {
        get { VerifyItemMutationAccess(); return itemTemplate; }
        set
        {
            VerifyItemMutationAccess();
            if (ReferenceEquals(itemTemplate, value)) return;
            itemTemplate = value;
            ResetItemContainers();
        }
    }

    /// <summary>Gets the borrowed selected value, or null when there is no selection or the selected value is null.</summary>
    /// <remarks>UI thread only. Use SelectedIndex to distinguish a null value from no selection and to select duplicate occurrences.
    /// Returns null if a source indexer callback retires/replaces the selection while the value is being read.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get
        {
            VerifyItemMutationAccess();
            int index = SelectedIndex;
            if (index < 0) return null;
            long version = ItemsGeneration;
            object? item = GetItem(index);
            return !IsDisposed && version == ItemsGeneration && SelectedIndex == index ? item : null;
        }
    }

    /// <summary>Publishes a conservative reset after an unobservable source changes.</summary>
    /// <remarks>UI thread only. Retires old logical identities/selection and bindings; indexed observable notifications do not need this call.</remarks>
    public void RefreshItems() => RefreshItemsSource();

    /// <summary>Occurs after pointer, Enter/Space or accessibility activation commits the logical selection.</summary>
    /// <remarks>
    /// UI thread only. The arguments snapshot the invoked value/index, not a retained container.
    /// A selection or source-value callback that replaces the source, redirects selection, hides,
    /// disables, reparents or disposes the view prevents stale activation. Interactive controls
    /// inside a custom row keep their own actions.
    /// Keyboard navigation uses the inherited canonical selection/focus path.
    /// </remarks>
    public event EventHandler<CollectionViewItemEventArgs>? ItemInvoked;

    /// <inheritdoc/>
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        if (!IsDisposed) NotifyAccessibilityClients(AccessibleEvents.Selection);
    }

    /// <inheritdoc/>
    protected override void OnItemsChanged(ItemIndexChange change)
    {
        base.OnItemsChanged(change);
        if (!IsDisposed) NotifyAccessibilityClients(AccessibleEvents.Reorder);
    }

    /// <inheritdoc/>
    protected override void OnItemKeyDown(KeyEventArgs e)
    {
        base.OnItemKeyDown(e);
        if (!e.Handled && !IsDisposed && Enabled && Visible && !e.Control && !e.Alt &&
            e.KeyCode is Keys.Enter or Keys.Space && SelectedIndex >= 0)
        {
            e.Handled = true;
            InvokeLogicalItem(SelectedIndex);
        }
    }

    /// <inheritdoc/>
    protected override void OnItemInvoked(int index, bool doubleClick)
    {
        base.OnItemInvoked(index, doubleClick);
        long version = ItemsGeneration;
        var lifetime = new AccessibilityControlLifetime(this);
        object? item = GetItem(index);
        // The borrowed indexer is application code, just like a selection event handler.
        // It must not dispatch an activation against a hidden, moved or newer selection.
        if (lifetime.IsCurrent && !IsDisposed && Enabled && Visible &&
            version == ItemsGeneration && SelectedIndex == index)
            ItemInvoked?.Invoke(this, new(item, index, doubleClick));
    }

    /// <inheritdoc/>
    protected override Control CreateItemContainer()
    {
        var template = itemTemplate;
        var row = template is null ? base.CreateItemContainer() : template.CreateContainer()
            ?? throw new InvalidOperationException("ItemTemplate returned a null container.");
        if (row.IsDisposed || row.Parent is not null || containerTemplates.TryGetValue(row, out _))
            throw new InvalidOperationException("ItemTemplate must return a distinct, unparented, undisposed container.");
        // The owned row is the canonical keyboard target even when a factory returns a Label
        // or Panel that is normally not selectable. Interactive descendants retain their own focus.
        row.SetControlBehavior(ControlBehaviors.Selectable, true);
        containerTemplates.Add(row, new(template));
        return row;
    }

    /// <inheritdoc/>
    protected override void PrepareItemContainer(Control container, object? item, int index)
    {
        if (!containerTemplates.TryGetValue(container, out var binding))
            throw new InvalidOperationException("The item container no longer belongs to this template operation.");
        if (binding.Factory is { } template) template.PrepareContainer(container, index);
        else base.PrepareItemContainer(container, item, index);
    }

    /// <inheritdoc/>
    protected override void ClearItemContainer(Control container, object? item, int index)
    {
        if (containerTemplates.TryGetValue(container, out var binding) && binding.Factory is { } template)
            template.ClearContainer(container, index);
        else base.ClearItemContainer(container, item, index);
    }

    /// <inheritdoc/>
    protected override void DiscardItemContainer(Control container)
    {
        containerTemplates.TryGetValue(container, out var binding);
        containerTemplates.Remove(container);
        Exception? failure = null;
        try { binding?.Factory?.DiscardContainer(container); }
        catch (Exception error) { failure = error; }
        try
        {
            // A user cleanup callback may create a newer tree. Preserve that tree and surface
            // the ownership violation, just as the canonical ItemsControl discard path does.
            if (container.Parent is not null && !ReferenceEquals(container.Parent, this))
                throw new InvalidOperationException("The retired template container was moved to a different parent.");
            base.DiscardItemContainer(container);
        }
        catch (Exception error)
        {
            if (failure is not null) throw new AggregateException("Template and control disposal failed.", failure, error);
            throw;
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

/// <summary>Describes a CollectionView row activation after logical selection has been committed.</summary>
/// <param name="item">The borrowed source value at activation time, possibly null.</param>
/// <param name="index">The zero-based occurrence index at activation time.</param>
/// <param name="isDoubleClick">Whether the pointer activation is a double click.</param>
/// <remarks>Do not use this snapshot to index a subsequently mutated source. The view never owns/disposes Item.</remarks>
public sealed class CollectionViewItemEventArgs(object? item, int index, bool isDoubleClick) : EventArgs
{
    /// <summary>Gets the borrowed value captured before handlers run.</summary>
    public object? Item { get; } = item;
    /// <summary>Gets the zero-based occurrence index captured before handlers run.</summary>
    public int Index { get; } = index;
    /// <summary>Gets whether this was a double-click activation.</summary>
    public bool IsDoubleClick { get; } = isDoubleClick;
}
