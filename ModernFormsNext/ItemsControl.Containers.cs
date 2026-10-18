using System.Drawing;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext;

public abstract partial class ItemsControl
{
    /// <summary>Creates a distinct unbound container owned by the host.</summary>
    /// <returns>A new, unparented and undisposed control. The default is a focusable Label.</returns>
    /// <remarks>UI thread only. Do not return source-owned controls, previously discarded controls or another container's instance.
    /// Creation occurs only for the viewport/cache; neither metadata discovery nor source counting invokes this hook.</remarks>
    protected virtual Control CreateItemContainer()
    {
        var row = new DefaultItemContainer(this) { TabStop = false, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft };
        row.SetControlBehavior(ControlBehaviors.Selectable, true);
        return row;
    }

    /// <summary>Prepares a cleared container for one borrowed logical occurrence.</summary>
    /// <param name="container">The owned container; never retain it as logical item identity.</param>
    /// <param name="item">The current borrowed value, possibly null.</param>
    /// <param name="index">The occurrence index, including distinct duplicate values.</param>
    /// <remarks>Called on the UI thread before attachment. Use existing DataBindings for property binding.
    /// ClearItemContainer must retire template-owned subscriptions/state before reuse. The host clears DataBindings before that cleanup hook.</remarks>
    protected virtual void PrepareItemContainer(Control container, object? item, int index)
        => container.Text = item?.ToString() ?? string.Empty;

    /// <summary>Retires template state for an old binding before recycling or disposal.</summary>
    /// <param name="container">The detached owned container.</param>
    /// <param name="item">The originally prepared borrowed value, even when the source has already mutated.</param>
    /// <param name="index">The originally prepared index; never use it to look up a mutated source.</param>
    /// <remarks>UI thread only. Unsubscribe template handlers and release template-owned resources; do not dispose source values.
    /// The host first clears canonical DataBindings on the old container and its owned descendants so cleanup assignments cannot write empty/recycled values back
    /// to the old source. Partial preparation and callback failures also invoke this cleanup.</remarks>
    protected virtual void ClearItemContainer(Control container, object? item, int index) => container.Text = string.Empty;

    /// <summary>Permanently releases one cleared item container owned by this host.</summary>
    /// <param name="container">The retired container, whose input handlers and bindings have been removed.</param>
    /// <remarks>
    /// UI thread only; invoked once per discarded container, including failed preparation. The default
    /// disposes it. Overrides must release template resources and call base even when cleanup fails,
    /// except when a callback moved the control into a different, newer parent tree.
    /// </remarks>
    protected virtual void DiscardItemContainer(Control container) => container.Dispose();

    /// <summary>Measures one realized stack cell's primary extent in logical pixels.</summary>
    /// <param name="container">The prepared container.</param>
    /// <param name="item">The borrowed source value.</param>
    /// <param name="index">The logical occurrence index.</param>
    /// <param name="estimatedSize">Positive logical cell estimate, including spacing.</param>
    /// <returns>A positive finite extent. The default queries canonical GetPreferredSize.</returns>
    /// <remarks>Only called for VariableItemSize stacks on the UI thread. No render-time layout or device-coordinate measurements.</remarks>
    protected virtual double MeasureItemContainer(Control container, object? item, int index, Size estimatedSize)
    {
        var preferred = container.GetPreferredSize(estimatedSize);
        return Math.Max(1, itemOrientation == Orientation.Vertical ? preferred.Height : preferred.Width);
    }

    /// <summary>Updates derived drawn-item metadata after the container has its logical bounds.</summary>
    /// <param name="container">The arranged owned container.</param>
    /// <param name="item">The borrowed source value.</param>
    /// <param name="index">The logical occurrence index.</param>
    /// <remarks>Called during canonical layout on the UI thread. Convert to device coordinates only when the renderer contract requires it.</remarks>
    protected virtual void OnItemContainerArranged(Control container, object? item, int index) { }

    /// <summary>Handles a pointer activation after committing the clicked logical selection.</summary>
    /// <param name="index">The clicked occurrence.</param>
    /// <param name="doubleClick">Whether this is a double-click notification.</param>
    /// <remarks>UI thread only. The base performs no additional action. An override may raise the control's existing item event.</remarks>
    protected virtual void OnItemInvoked(int index, bool doubleClick) { }

    private sealed class DefaultItemContainer : Label
    {
        private readonly ItemsControl owner;
        internal DefaultItemContainer(ItemsControl owner)
        {
            this.owner = owner;
            Style.ParentStyle = owner.Style;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (owner.itemBindings.TryGetValue(this, out var binding) && binding.Index == owner.selectedIndex)
                e.Canvas.FillRectangle(ClientRectangle, Theme.ControlHighlightLowColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Focused && ShowFocusCues) e.Canvas.DrawFocusRectangle(ClientRectangle, 1);
        }
    }

    private ItemContainerGenerator<Control> CreateGenerator()
    {
        var factory = new ContainerFactory(this);
        var result = new ItemContainerGenerator<Control>(factory);
        factory.Generator = result;
        return result;
    }

    private sealed class ContainerFactory(ItemsControl owner) : IItemContainerFactory<Control>
    {
        internal ItemContainerGenerator<Control>? Generator;

        public Control CreateContainer()
        {
            var row = owner.CreateItemContainer() ?? throw new InvalidOperationException("CreateItemContainer returned null.");
            if (row.IsDisposed || row.Parent is not null)
                throw new InvalidOperationException("CreateItemContainer must return a new unparented control.");
            row.TabStop = false;
            row.Click += owner.HandleContainerClick;
            row.DoubleClick += owner.HandleContainerDoubleClick;
            row.KeyDown += owner.HandleContainerKeyDown;
            row.GotFocus += owner.HandleContainerGotFocus;
            row.LostFocus += owner.HandleContainerLostFocus;
            return row;
        }

        public void PrepareContainer(Control container, int index)
        {
            if (!ReferenceEquals(Generator, owner.generator) || owner.disposingItems || owner.IsDisposed) return;
            long version = owner.itemsVersion;
            var item = owner.GetItem(index);
            // An indexed provider is application code too: it can publish cached-page changes
            // or replace the source while returning a value. Do not invoke an old template,
            // attach its control or activate bindings against the newer source generation.
            if (!CanCompletePreparation(version, container, index)) return;
            owner.itemBindings[container] = new(item, index);
            owner.PrepareItemContainer(container, item, index);
            if (!CanCompletePreparation(version, container, index)) return;
            if (container.IsDisposed || container.Parent is not null && !ReferenceEquals(container.Parent, owner))
                throw new InvalidOperationException("A template disposed or reparented its owned container.");
            owner.Controls.Add(container);
            if (version == owner.itemsVersion && ReferenceEquals(Generator, owner.generator) &&
                ReferenceEquals(container.Parent, owner) && !container.IsDisposed && !owner.disposingItems)
                container.RefreshItemContainerBindings();
        }

        private bool CanCompletePreparation(long version, Control container, int index)
        {
            var active = Generator;
            if (active is null || active.IsDisposed || !ReferenceEquals(active, owner.generator) ||
                owner.disposingItems || owner.IsDisposed) return false;
            if (version == owner.itemsVersion) return true;
            // A metric/cache change can retire this host pass without replacing the generator
            // or its source mapping. Its generic entry was already marked prepared, but this
            // factory deliberately did not attach the old binding. Refresh that same occurrence
            // through the existing coalesced generator path before a later layout can arrange it.
            if ((uint)index < (uint)owner.sourceCount && active.TryGetContainer(index, out var current) &&
                ReferenceEquals(current, container)) active.RefreshItem(index);
            return false;
        }

        public void ClearContainer(Control container, int index)
        {
            bool hasBinding = owner.itemBindings.Remove(container, out var binding);
            List<Exception>? failures = null;
            if (owner.IsFocusWithin(container) && !owner.disposingItems && !owner.IsDisposed)
            {
                owner.restoreFocus = owner.focusedIndex >= 0;
                try { owner.GetFocusScope().Request(owner, forced: true); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }
            if (ReferenceEquals(container.Parent, owner))
            {
                try { owner.Controls.Remove(container); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }
            // Focus/ParentChanged/binding callbacks may move the retired row to a newer tree.
            // Do not clear that tree's bindings or run old template assignments against it.
            // DiscardContainer still reports the ownership violation and preserves its parent.
            if (container.Parent is null || ReferenceEquals(container.Parent, owner))
            {
                try { container.ClearItemContainerBindings(); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
                if (hasBinding && (container.Parent is null || ReferenceEquals(container.Parent, owner)))
                {
                    try { owner.ClearItemContainer(container, binding.Item, index); }
                    catch (Exception failure) { (failures ??= []).Add(failure); }
                }
            }
            if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures is not null) throw new AggregateException("Item container cleanup failed.", failures);
        }

        public void DiscardContainer(Control container)
        {
            container.Click -= owner.HandleContainerClick;
            container.DoubleClick -= owner.HandleContainerDoubleClick;
            container.KeyDown -= owner.HandleContainerKeyDown;
            container.GotFocus -= owner.HandleContainerGotFocus;
            container.LostFocus -= owner.HandleContainerLostFocus;
            if (container.Parent is not null && !ReferenceEquals(container.Parent, owner))
                throw new InvalidOperationException("The owned item container was moved to another parent; that newer tree was preserved.");
            owner.DiscardItemContainer(container);
        }
    }

    private void HandleContainerClick(object? sender, MouseEventArgs e) => InvokeContainer(sender, doubleClick: false);
    private void HandleContainerDoubleClick(object? sender, MouseEventArgs e) => InvokeContainer(sender, doubleClick: true);

    private void InvokeContainer(object? sender, bool doubleClick)
    {
        if (sender is not Control row || !itemBindings.TryGetValue(row, out var binding) || disposingItems || IsDisposed) return;
        InvokeLogicalItem(binding.Index, doubleClick);
    }

    internal bool InvokeLogicalItem(int index, bool doubleClick = false)
    {
        VerifyItemsAccess();
        if (!Enabled || !Visible || (uint)index >= (uint)sourceCount) return false;
        var lifetime = new Accessibility.AccessibilityControlLifetime(this);
        long version = itemsVersion;
        SelectedIndex = index;
        // Selection handlers can hide, disable or move this host without changing the source.
        // Reuse the canonical ancestry/lifetime guard before dispatching the old activation.
        if (!lifetime.IsCurrent || !Enabled || !Visible || version != itemsVersion ||
            disposingItems || IsDisposed || selectedIndex != index) return false;
        OnItemInvoked(index, doubleClick);
        return lifetime.IsCurrent && Enabled && Visible && version == itemsVersion &&
            !disposingItems && !IsDisposed && selectedIndex == index;
    }

    private void HandleContainerGotFocus(object? sender, EventArgs e)
    {
        if (sender is Control row && itemBindings.TryGetValue(row, out var binding))
        { focusedIndex = binding.Index; restoreFocus = false; Invalidate(); }
    }

    private void HandleContainerLostFocus(object? sender, EventArgs e)
    {
        if (!disposingItems && !IsDisposed) Invalidate();
    }

    private void HandleContainerKeyDown(object? sender, KeyEventArgs e) => OnItemKeyDown(e);

    /// <summary>Processes an unhandled item key from this host or one of its owned row containers.</summary>
    /// <param name="e">The current canonical keyboard event; set Handled when consuming it.</param>
    /// <remarks>UI thread only. Call base to retain shared directional/page navigation and focus guards.
    /// Interactive descendants retain their own keyboard processing; this does not install another input router.</remarks>
    protected virtual void OnItemKeyDown(KeyEventArgs e) => NavigateItems(e);

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && !disposingItems && !IsDisposed) OnItemKeyDown(e);
    }

    private void NavigateItems(KeyEventArgs e)
    {
        if (e.Handled || !virtualizationEnabled || !Enabled || !Visible || sourceCount == 0 || e.Alt || e.Control) return;
        int current = selectedIndex >= 0 ? selectedIndex : focusedIndex;
        int page = (int)Math.Max(1L, RealizedRange.Count - (long)cacheItemCount * 2);
        var viewport = DisplayRectangle;
        int across = itemsLayout == ItemsLayout.Wrap ? Math.Max(1,
            itemOrientation == Orientation.Vertical ? viewport.Width / itemSize.Width : viewport.Height / itemSize.Height) : 1;
        int primaryStep = itemsLayout == ItemsLayout.Wrap ? across : 1;
        int? requested = e.KeyCode switch
        {
            Keys.Down when itemOrientation == Orientation.Vertical => (int)Math.Min(sourceCount - 1L, (long)current + primaryStep),
            Keys.Up when itemOrientation == Orientation.Vertical => Math.Max(0, current - primaryStep),
            Keys.Right when itemOrientation == Orientation.Horizontal => (int)Math.Min(sourceCount - 1L, (long)current + primaryStep),
            Keys.Left when itemOrientation == Orientation.Horizontal => Math.Max(0, current - primaryStep),
            Keys.Right when itemsLayout == ItemsLayout.Wrap => current + 1,
            Keys.Left when itemsLayout == ItemsLayout.Wrap => current - 1,
            Keys.Down when itemsLayout == ItemsLayout.Wrap => current + 1,
            Keys.Up when itemsLayout == ItemsLayout.Wrap => current - 1,
            Keys.Home => 0,
            Keys.End => sourceCount - 1,
            Keys.PageDown => (int)Math.Min(sourceCount - 1L, (long)Math.Max(0, current) + page),
            Keys.PageUp => Math.Max(0, current - page),
            _ => null
        };
        if (requested is not { } target) return;
        e.Handled = true;
        target = Math.Clamp(target, 0, sourceCount - 1);
        long version = itemsVersion;
        var focusScope = GetFocusScope();
        long focusVersion = focusScope.Version;
        SelectedIndex = target;
        if (version != itemsVersion || disposingItems || IsDisposed || !Enabled || !Visible ||
            selectedIndex != target || focusScope.Version != focusVersion) return;
        ScrollToItem(target);
        // Scrolling may retire the old focused row to this host. An application callback
        // that instead focuses another control owns that newer canonical focus transaction.
        if (version == itemsVersion && !disposingItems && !IsDisposed && Enabled && Visible && selectedIndex == target &&
            ReferenceEquals(FindExistingFocusScope(), focusScope) &&
            (focusScope.Version == focusVersion || ReferenceEquals(focusScope.Owner, this)))
        {
            focusedIndex = target;
            GetItemContainer(target)?.Select();
        }
    }

    private bool IsFocusWithin(Control row)
    {
        for (Control? current = FindExistingFocusScope()?.Owner; current is not null; current = current.Parent)
            if (ReferenceEquals(current, row)) return true;
        return false;
    }

    private void CaptureFocusedOccurrence()
    {
        for (Control? current = FindExistingFocusScope()?.Owner; current is not null && !ReferenceEquals(current, this); current = current.Parent)
            if (itemBindings.TryGetValue(current, out var binding)) { focusedIndex = binding.Index; return; }
    }

    internal int GetFocusedItemIndex()
    {
        for (Control? current = FindExistingFocusScope()?.Owner; current is not null && !ReferenceEquals(current, this); current = current.Parent)
            if (itemBindings.TryGetValue(current, out var binding)) return binding.Index;
        return -1;
    }

    internal bool TryGetCurrentItemIndex(Control container, out int index)
    {
        index = -1;
        if (disposingItems || IsDisposed || !ReferenceEquals(container.Parent, this) ||
            !itemBindings.TryGetValue(container, out var binding) ||
            generator?.TryGetContainer(binding.Index, out var current) != true || !ReferenceEquals(current, container)) return false;
        index = binding.Index;
        return true;
    }

    internal bool IsItemRealized(object item)
    {
        if (disposingItems || IsDisposed || generator?.IsDisposed != false) return false;
        // Logical ListView peers must not expose old legacy bounds after opting in. Match
        // the bounded current bindings rather than searching the entire indexed source.
        foreach (var binding in itemBindings)
            if (ReferenceEquals(binding.Value.Item, item) && !binding.Key.IsDisposed && ReferenceEquals(binding.Key.Parent, this) &&
                generator.TryGetContainer(binding.Value.Index, out var mapped) && ReferenceEquals(mapped, binding.Key)) return true;
        return false;
    }

    private void RestoreItemFocus()
    {
        if (!restoreFocus || focusedIndex < 0 || !ReferenceEquals(FindExistingFocusScope()?.Owner, this)) return;
        if (generator?.TryGetContainer(focusedIndex, out var row) == true)
        {
            restoreFocus = false;
            row.Select();
        }
    }
}
