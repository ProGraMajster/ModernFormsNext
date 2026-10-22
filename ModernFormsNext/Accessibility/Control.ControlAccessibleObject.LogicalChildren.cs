using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class Control
{
    public partial class ControlAccessibleObject
    {
        private List<ListBoxItemAccessibleObject>? list_box_item_objects;
        private List<ComboBoxItemAccessibleObject>? combo_box_item_objects;
        private readonly ConditionalWeakTable<ListViewItem, ListViewItemAccessibleObject> list_view_item_objects = new();
        private readonly ConditionalWeakTable<TreeViewItem, TreeViewItemAccessibleObject> tree_view_item_objects = new();
        private readonly ConditionalWeakTable<TabPage, TabItemAccessibleObject> tab_item_objects = new();
        private readonly ConditionalWeakTable<MenuItem, MenuItemAccessibleObject> menu_item_objects = new();

        /// <inheritdoc/>
        public override AccessibleObject? GetSelected()
        {
            if (Owner is not { IsDisposed: false } owner)
                return null;

            return owner switch
            {
                CollectionView collection when GetType() == typeof(ControlAccessibleObject) && collection.SelectedIndex >= 0
                    => GetCollectionItemObject(collection, collection.SelectedIndex),
                ListBox listBox when listBox.SelectedIndex >= 0
                    => GetListBoxItemObjects(listBox).ElementAtOrDefault(listBox.SelectedIndex),
                ComboBox comboBox when comboBox.SelectedIndex >= 0
                    => GetComboBoxItemObjects(comboBox).ElementAtOrDefault(comboBox.SelectedIndex),
                ListView { SelectedItem: { } item } listView
                    => GetListViewItemObject(listView, item),
                TreeView treeView when treeView.SelectedItem.Parent is not null
                    && treeView.SelectedItem.TreeView == treeView
                    => GetTreeViewItemObject(treeView, treeView.SelectedItem),
                TabControl { SelectedTabPage: { } page } tabControl
                    => GetTabItemObject(tabControl, page),
                MenuBase { SelectedItem: { } item } menu
                    => GetMenuItemObject(menu, item),
                _ => base.GetSelected()
            };
        }

        private IEnumerable<AccessibleObject> GetLogicalAccessibilityChildren(Control owner)
        {
            return owner switch
            {
                CollectionView collection when GetType() == typeof(ControlAccessibleObject) => GetCollectionLogicalChildren(collection),
                LinkLabel label => GetLinkObjects(label),
                NumericUpDown numeric => GetNumericChildren(numeric),
                ListBox listBox => GetListBoxItemObjects(listBox),
                ComboBox comboBox => GetComboBoxItemObjects(comboBox),
                ListView listView => listView.Items.Select((item, index) => GetListViewItemObject(listView, item, index)),
                TreeView treeView => treeView.Items.Select(item => GetTreeViewItemObject(treeView, item)),
                TabControl tabControl => tabControl.TabPages.Select(page => GetTabItemObject(tabControl, page)),
                MenuBase menu => menu.Items.Select(item => GetMenuItemObject(menu, item)),
                _ => []
            };
        }

        private IReadOnlyList<ListBoxItemAccessibleObject> GetListBoxItemObjects(ListBox owner)
        {
            list_box_item_objects = SynchronizeOccurrenceItems(
                list_box_item_objects,
                owner.Items,
                owner.Items.GetAccessibilityIdentity,
                (item, identity) => new ListBoxItemAccessibleObject(this, owner, item, identity));

            return list_box_item_objects;
        }

        private IReadOnlyList<ComboBoxItemAccessibleObject> GetComboBoxItemObjects(ComboBox owner)
        {
            combo_box_item_objects = SynchronizeOccurrenceItems(
                combo_box_item_objects,
                owner.Items,
                owner.Items.GetAccessibilityIdentity,
                (item, identity) => new ComboBoxItemAccessibleObject(this, owner, item, identity));

            return combo_box_item_objects;
        }

        private static List<TNode> SynchronizeOccurrenceItems<TNode>(
            List<TNode>? existing,
            IReadOnlyList<object> items,
            Func<int, object> getIdentity,
            Func<object, object, TNode> create)
            where TNode : OccurrenceItemAccessibleObject
        {
            existing ??= [];
            var used = new bool[existing.Count];
            var synchronized = new List<TNode>(items.Count);

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                object item = items[itemIndex];
                object identity = getIdentity(itemIndex);
                TNode? matched = null;

                for (int i = 0; i < existing.Count; i++)
                {
                    if (!used[i] && existing[i].Represents(identity))
                    {
                        used[i] = true;
                        matched = existing[i];
                        break;
                    }
                }

                matched ??= create(item, identity);
                matched.Attach(itemIndex);
                synchronized.Add(matched);
            }

            for (int i = 0; i < existing.Count; i++)
            {
                if (!used[i])
                    existing[i].Detach();
            }

            return synchronized;
        }

        private ListViewItemAccessibleObject GetListViewItemObject(ListView owner, ListViewItem item, int knownIndex = -1)
        {
            var peer = list_view_item_objects.GetValue(item, value => new ListViewItemAccessibleObject(this, owner, value));
            if (knownIndex >= 0) peer.SetIndexHint(knownIndex);
            return peer;
        }

        internal bool TryGetVirtualizedChildren(out IReadOnlyList<AccessibleObject> children, out int? count)
        {
            if (TryGetCollectionProjection(out children, out count)) return true;
            children = Array.Empty<AccessibleObject>();
            count = null;
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not ListView { VirtualizationEnabled: true, IsDisposed: false } list)
                return false;
            count = list.Items.Count;
            if (!list.Visible || View == AccessibilityView.Hidden) return true;
            var range = list.RealizedRange;
            var peers = new List<AccessibleObject>(Math.Min(range.Count, list.Items.Count) + 2);
            for (int index = range.StartIndex; index < range.EndIndex && index < list.Items.Count; index++)
            {
                var peer = GetListViewItemObject(list, list.Items[index], index);
                if (peer.View != AccessibilityView.Hidden) peers.Add(peer);
            }
            // Keep at most two out-of-range logical references so native focus/selection queries
            // and scroll-to-item actions retain their existing identity during visual recycling.
            ReadOnlySpan<int> retainedIndices = [list.SelectedIndex, list.GetFocusedItemIndex()];
            foreach (int index in retainedIndices)
                if (index >= 0 && index < list.Items.Count && !range.Contains(index))
                {
                    var peer = GetListViewItemObject(list, list.Items[index], index);
                    if (peer.View != AccessibilityView.Hidden && !peers.Contains(peer)) peers.Add(peer);
                }
            foreach (Control visual in list.Controls)
            {
                var peer = visual.AccessibilityObject;
                if (peer.View == AccessibilityView.Hidden ||
                    peer.GetType() != typeof(ControlAccessibleObject) && (peer.State & AccessibleStates.Invisible) != 0) continue;
                peers.Add(peer);
                count = null;
            }
            children = peers;
            return true;
        }

        internal bool TryContainsVirtualizedChild(AccessibleObject child, out bool contains)
        {
            if (TryContainsCollectionChild(child, out contains)) return true;
            contains = false;
            // A derived accessibility peer can replace GetAccessibilityChildren entirely. Preserve
            // its projection contract; only the exact canonical peer can answer from item identity.
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not ListView { VirtualizationEnabled: true } list)
                return false;
            if (child is ListViewItemAccessibleObject item)
            {
                if (item.TryIndex(this, out int index) && index >= 0)
                    contains = list.RealizedRange.Contains(index) || list.SelectedIndex == index || list.GetFocusedItemIndex() == index;
                return true;
            }
            // Internal scrollbar infrastructure can have the host as Parent without belonging
            // to Controls. Confirm actual canonical enumeration, using only the bounded visual
            // collection; a Parent pointer alone must never admit a hidden/detached extra node.
            foreach (Control candidate in list.Controls)
                if (ReferenceEquals(candidate.AccessibilityObject, child))
                {
                    contains = child.View != AccessibilityView.Hidden &&
                        (child.GetType() == typeof(ControlAccessibleObject) || (child.State & AccessibleStates.Invisible) == 0);
                    break;
                }
            return true;
        }

        internal bool TryContainsCanonicalChild(AccessibleObject child, out bool contains)
        {
            if (TryContainsVirtualizedChild(child, out contains)) return true;
            if (GetType() != typeof(ControlAccessibleObject)) return false;
            if (Owner is not { IsDisposed: false } owner) return true;

            // Default visual membership is the actual control collection, including Form's
            // existing client-area projection. Do not repeatedly call GetChildCount/GetChild:
            // Android validates this ancestor chain for every recycled-row notification.
            var controls = owner is ControlAdapter adapter ? adapter.ParentForm.Controls : owner.Controls;
            for (int index = 0; index < controls.Count; index++)
                if (ReferenceEquals(controls[index].AccessibilityObject, child))
                {
                    contains = child.View != AccessibilityView.Hidden &&
                        (IsVisibilityDeterminedByView(child) || (child.State & AccessibleStates.Invisible) == 0);
                    return true;
                }

            // These controls also own logical children. Preserve their canonical enumeration
            // unless the indexed virtualized ListView path above has already answered it.
            return owner is not (LinkLabel or NumericUpDown or ListBox or ComboBox or ListView or TreeView or TabControl or MenuBase);
        }

        // Only framework-owned peers whose Invisible state follows View may opt in. Public
        // custom peers keep the independent State check, including custom hidden projections.
        internal virtual bool HasViewVisibility => GetType() == typeof(ControlAccessibleObject);

        internal static bool IsVisibilityDeterminedByView(AccessibleObject peer)
            => peer is ControlAccessibleObject { HasViewVisibility: true } or ListViewItemAccessibleObject or CollectionItemAccessibleObject;

        internal bool TryGetVirtualizedItemIndex(AccessibleObject child, out int index)
        {
            if (TryGetCollectionItemIndex(child, out index)) return true;
            index = -1;
            return GetType() == typeof(ControlAccessibleObject) && Owner is ListView { VirtualizationEnabled: true } &&
                child is ListViewItemAccessibleObject item && item.TryIndex(this, out index);
        }

        internal bool TryGetVirtualizedChild(int index, out AccessibleObject? child)
        {
            if (TryGetCollectionIndexedChild(index, out child)) return true;
            child = null;
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not ListView { VirtualizationEnabled: true, IsDisposed: false } list)
                return false;
            // A custom explicitly exposed visual child prevents flat logical indexing. Recycled
            // private focus containers stay hidden and do not change the logical occurrence order.
            for (int visual = 0; visual < list.Controls.Count; visual++)
                if (list.Controls[visual].AccessibilityObject.View != AccessibilityView.Hidden) return false;
            if (list.Visible && View != AccessibilityView.Hidden && (uint)index < (uint)list.Items.Count)
                child = GetListViewItemObject(list, list.Items[index], index);
            return true;
        }

        private TreeViewItemAccessibleObject GetTreeViewItemObject(TreeView owner, TreeViewItem item)
            => tree_view_item_objects.GetValue(item, value => new TreeViewItemAccessibleObject(this, owner, value));

        private TabItemAccessibleObject GetTabItemObject(TabControl owner, TabPage page)
            => tab_item_objects.GetValue(page, value => new TabItemAccessibleObject(this, owner, value));

        private MenuItemAccessibleObject GetMenuItemObject(MenuBase owner, MenuItem item)
            => menu_item_objects.GetValue(item, value => new MenuItemAccessibleObject(this, owner, value));

        private static AccessibleObject? NavigateLogicalObject(AccessibleObject current, AccessibleNavigation direction)
        {
            if (direction == AccessibleNavigation.FirstChild)
                return current.GetChildCount() > 0 ? current.GetChild(0) : null;

            if (direction == AccessibleNavigation.LastChild)
                return current.GetChildCount() > 0 ? current.GetChild(current.GetChildCount() - 1) : null;

            if (direction is not (AccessibleNavigation.Next or AccessibleNavigation.Previous)
                || current.Parent is not { } parent)
            {
                return null;
            }

            int offset = direction == AccessibleNavigation.Next ? 1 : -1;
            for (int i = 0; i < parent.GetChildCount(); i++)
            {
                if (!ReferenceEquals(parent.GetChild(i), current))
                    continue;

                int siblingIndex = i + offset;
                return siblingIndex >= 0 && siblingIndex < parent.GetChildCount()
                    ? parent.GetChild(siblingIndex)
                    : null;
            }

            return null;
        }

        private static Rectangle ToScreenBounds(Control owner, Rectangle localBounds)
        {
            if (localBounds.Width <= 0 || localBounds.Height <= 0)
                return Rectangle.Empty;

            Point topLeft = owner.PointToScreen(localBounds.Location);
            Point topRight = owner.PointToScreen(new Point(localBounds.Right, localBounds.Top));
            Point bottomLeft = owner.PointToScreen(new Point(localBounds.Left, localBounds.Bottom));
            Point bottomRight = owner.PointToScreen(new Point(localBounds.Right, localBounds.Bottom));
            int left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            int top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            int right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            int bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private abstract class LogicalItemAccessibleObject : AccessibleObject
        {
            private readonly WeakReference<Control> owner_reference;

            protected LogicalItemAccessibleObject(ControlAccessibleObject root, Control owner)
            {
                Root = root;
                owner_reference = new WeakReference<Control>(owner);
            }

            protected ControlAccessibleObject Root { get; }

            protected Control? OwnerControl
                => owner_reference.TryGetTarget(out Control? owner) && !owner.IsDisposed ? owner : null;

            protected bool IsOwnerAvailable
                => Root.View != AccessibilityView.Hidden
                    && OwnerControl is { Enabled: true, Visible: true, Disposing: false } owner
                    && owner.FindWindow()?.InputBindingsClosed != true;

            public override AccessibilityView View
                => Root.View != AccessibilityView.Hidden && OwnerControl is { Visible: true }
                    ? AccessibilityView.Control
                    : AccessibilityView.Hidden;

            public override AccessibleObject? Navigate(AccessibleNavigation navdir)
                => NavigateLogicalObject(this, navdir);

            protected AccessibleStates GetCommonState(bool isAttached, bool isOffscreen)
            {
                if (!isAttached || OwnerControl is not { } owner)
                    return AccessibleStates.Unavailable | AccessibleStates.Invisible | AccessibleStates.Offscreen;

                var state = AccessibleStates.Selectable;

                if (!owner.Enabled)
                    state |= AccessibleStates.Unavailable;

                if (!owner.Visible)
                    state |= AccessibleStates.Invisible;

                if (isOffscreen)
                    state |= AccessibleStates.Offscreen;

                return state;
            }
        }

        private abstract class OccurrenceItemAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly WeakReference<object> item_reference;
            private readonly object occurrence_identity;
            private bool attached = true;
            private int current_index;

            protected OccurrenceItemAccessibleObject(
                ControlAccessibleObject root,
                Control owner,
                object item,
                object occurrenceIdentity)
                : base(root, owner)
            {
                item_reference = new WeakReference<object>(item);
                occurrence_identity = occurrenceIdentity;
            }

            protected object? Item
                => item_reference.TryGetTarget(out object? item) ? item : null;

            protected bool IsAttached => attached && Item is not null;

            protected int CurrentIndex => IsAttached ? current_index : -1;

            public bool Represents(object identity)
                => ReferenceEquals(occurrence_identity, identity);

            public void Attach(int index)
            {
                attached = true;
                current_index = index;
            }

            public void Detach()
            {
                attached = false;
                current_index = -1;
            }

            public override string? Name
            {
                get => Item?.ToString();
                set { }
            }

            public override string? Value
            {
                get => Item?.ToString();
                set { }
            }

            public override AccessibilityView View
                => IsAttached ? base.View : AccessibilityView.Hidden;
        }

        private sealed class ListBoxItemAccessibleObject : OccurrenceItemAccessibleObject
        {
            private readonly WeakReference<ListBox> owner_reference;

            public ListBoxItemAccessibleObject(
                ControlAccessibleObject root,
                ListBox owner,
                object item,
                object occurrenceIdentity)
                : base(root, owner, item, occurrenceIdentity)
            {
                owner_reference = new WeakReference<ListBox>(owner);
            }

            private ListBox? Owner
                => owner_reference.TryGetTarget(out ListBox? owner) && !owner.IsDisposed ? owner : null;

            private int Index => Owner is not null ? CurrentIndex : -1;

            public override AccessibleObject? Parent => Index >= 0 ? Root : null;

            public override AccessibilityView View
                => Index >= 0 ? base.View : AccessibilityView.Hidden;

            public override AccessibleRole Role => AccessibleRole.ListItem;

            public override AccessibleControlType ControlType => AccessibleControlType.ListItem;

            public override Rectangle Bounds
            {
                get
                {
                    if (Owner is not { } owner || Index < 0 || !owner.Visible)
                        return Rectangle.Empty;

                    return ToScreenBounds(owner, owner.GetItemRectangle(Index));
                }
            }

            public override AccessibleStates State
            {
                get
                {
                    if (Owner is not { } owner || Index < 0)
                        return GetCommonState(isAttached: false, isOffscreen: true);

                    bool offscreen = Bounds.IsEmpty || Index < owner.FirstVisibleIndex;
                    var state = GetCommonState(IsAttached, offscreen);

                    if (owner.SelectionMode == SelectionMode.None)
                        state &= ~AccessibleStates.Selectable;

                    if (owner.Items.SelectedIndexes.Contains(Index))
                        state |= AccessibleStates.Selected;

                    if (owner.Focused && owner.Items.FocusedIndex == Index)
                        state |= AccessibleStates.Focused;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
            {
                get
                {
                    if (Index < 0 || !IsOwnerAvailable || Owner is not { } owner)
                        return AccessibleActions.None;

                    var actions = AccessibleActions.ScrollIntoView;
                    if (owner.SelectionMode != SelectionMode.None)
                        actions |= AccessibleActions.Select;

                    return actions;
                }
            }

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action)
                    || (SupportedActions & action) == 0
                    || parameter is not null
                    || Owner is not { } owner
                    || Index < 0)
                {
                    return false;
                }

                if (action == AccessibleActions.Select)
                {
                    owner.SelectedIndex = Index;
                    return true;
                }

                if (action == AccessibleActions.ScrollIntoView)
                {
                    owner.FirstVisibleIndex = Index;
                    return true;
                }

                return false;
            }

            public override void Select(AccessibleSelection flags)
            {
                if ((flags & AccessibleSelection.TakeSelection) != 0)
                    PerformAction(AccessibleActions.Select);

                if ((flags & AccessibleSelection.AddSelection) != 0)
                    Owner?.SetAccessibilitySelection(Index, selected: true);

                if ((flags & AccessibleSelection.RemoveSelection) != 0)
                    Owner?.SetAccessibilitySelection(Index, selected: false);

                if ((flags & AccessibleSelection.TakeFocus) != 0)
                    Owner?.Select();
            }
        }

        private sealed class ComboBoxItemAccessibleObject : OccurrenceItemAccessibleObject
        {
            private readonly WeakReference<ComboBox> owner_reference;

            public ComboBoxItemAccessibleObject(
                ControlAccessibleObject root,
                ComboBox owner,
                object item,
                object occurrenceIdentity)
                : base(root, owner, item, occurrenceIdentity)
            {
                owner_reference = new WeakReference<ComboBox>(owner);
            }

            private ComboBox? Owner
                => owner_reference.TryGetTarget(out ComboBox? owner) && !owner.IsDisposed ? owner : null;

            private int Index => Owner is not null ? CurrentIndex : -1;

            public override AccessibleObject? Parent => Index >= 0 ? Root : null;

            public override AccessibilityView View
                => Index >= 0 ? base.View : AccessibilityView.Hidden;

            public override AccessibleRole Role => AccessibleRole.ListItem;

            public override AccessibleControlType ControlType => AccessibleControlType.ListItem;

            public override AccessibleStates State
            {
                get
                {
                    if (Owner is not { } owner || Index < 0)
                        return GetCommonState(isAttached: false, isOffscreen: true);

                    var state = GetCommonState(IsAttached, isOffscreen: !owner.DroppedDown);
                    if (owner.SelectedIndex == Index)
                        state |= AccessibleStates.Selected;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
                => Index >= 0 && IsOwnerAvailable ? AccessibleActions.Select : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action)
                    || (SupportedActions & action) == 0
                    || action != AccessibleActions.Select
                    || parameter is not null
                    || Owner is not { } owner
                    || Index < 0)
                {
                    return false;
                }

                owner.SelectedIndex = Index;
                return true;
            }
        }

        private sealed class ListViewItemAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly WeakReference<ListView> owner_reference;
            private readonly WeakReference<ListViewItem> item_reference;
            private int lastKnownIndex = -1;

            internal void SetIndexHint(int index) => lastKnownIndex = index;

            internal bool TryIndex(ControlAccessibleObject root, out int index)
            {
                bool answered = TryMembership(root, out bool contains);
                index = contains ? lastKnownIndex : -1;
                return answered;
            }

            public ListViewItemAccessibleObject(ControlAccessibleObject root, ListView owner, ListViewItem item)
                : base(root, owner)
            {
                owner_reference = new WeakReference<ListView>(owner);
                item_reference = new WeakReference<ListViewItem>(item);
            }

            private ListView? Owner
                => owner_reference.TryGetTarget(out ListView? owner) && !owner.IsDisposed ? owner : null;

            private ListViewItem? Item
                => item_reference.TryGetTarget(out ListViewItem? item) ? item : null;

            private bool IsAttached => Owner is { } owner && Item is { Parent: { } parent } && ReferenceEquals(owner, parent);

            internal bool TryMembership(ControlAccessibleObject root, out bool contains)
            {
                contains = false;
                if (!ReferenceEquals(Root, root)) return false;
                if (!IsAttached || Root.View == AccessibilityView.Hidden || Owner is not { Visible: true } owner || Item is not { } item)
                    return true;
                // This is a checked hint on the same stable logical peer. Indexed mutations may
                // move it; validate by reference before using it, then recover once from the source.
                // Never materialize sibling peers merely to establish this one's membership.
                if ((uint)lastKnownIndex < (uint)owner.Items.Count && ReferenceEquals(owner.Items[lastKnownIndex], item))
                    contains = true;
                else
                {
                    lastKnownIndex = -1;
                    for (int index = 0; index < owner.Items.Count; index++)
                        if (ReferenceEquals(owner.Items[index], item)) { lastKnownIndex = index; contains = true; break; }
                }
                return true;
            }

            public override AccessibilityView View
                => IsAttached ? base.View : AccessibilityView.Hidden;

            public override AccessibleObject? Parent => IsAttached ? Root : null;

            public override string? Name
            {
                get => Item?.Text;
                set { }
            }

            public override AccessibleRole Role => AccessibleRole.ListItem;

            public override AccessibleControlType ControlType => AccessibleControlType.ListItem;

            public override Rectangle Bounds
                => IsAttached && Owner is { } owner && Item is { } item &&
                    (!owner.VirtualizationEnabled || owner.IsItemRealized(item))
                    ? ToScreenBounds(owner, item.Bounds)
                    : Rectangle.Empty;

            public override AccessibleStates State
            {
                get
                {
                    bool offscreen = Bounds.IsEmpty;
                    if (Owner is { VirtualizationEnabled: true } list && Item is { } currentItem)
                        offscreen |= !list.NativeHostingViewport.IntersectsWith(currentItem.Bounds);
                    var state = GetCommonState(IsAttached, offscreen);
                    if (Item?.Selected == true)
                        state |= AccessibleStates.Selected;
                    if (Owner is { VirtualizationEnabled: true } focusedOwner && focusedOwner.GetFocusedItemIndex() is int focused &&
                        focused >= 0 && focused < focusedOwner.Items.Count && ReferenceEquals(focusedOwner.Items[focused], Item))
                        state |= AccessibleStates.Focused;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
                => IsAttached && IsOwnerAvailable
                    ? AccessibleActions.Select | (Owner?.VirtualizationEnabled == true ? AccessibleActions.ScrollIntoView | AccessibleActions.Focus : AccessibleActions.None)
                    : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action)
                    || (SupportedActions & action) == 0
                    || parameter is not null
                    || !IsAttached
                    || Owner is not { } owner
                    || Item is not { } item)
                {
                    return false;
                }

                if (action == AccessibleActions.Select)
                {
                    owner.SelectedItem = item;
                    return IsAttached && !owner.IsDisposed && ReferenceEquals(owner.SelectedItem, item);
                }
                int index = owner.Items.IndexOf(item);
                if (index < 0) return false;
                var lifetime = new AccessibilityControlLifetime(owner);
                owner.ScrollToItem(index);
                if (!lifetime.IsCurrent || !IsAttached || owner.Items.IndexOf(item) != index) return false;
                if (action == AccessibleActions.Focus)
                {
                    owner.GetItemContainer(index)?.Select();
                    return lifetime.IsCurrent && owner.GetFocusedItemIndex() == index;
                }
                return action == AccessibleActions.ScrollIntoView && !Bounds.IsEmpty;
            }
        }

        private sealed class TreeViewItemAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly WeakReference<TreeView> owner_reference;
            private readonly WeakReference<TreeViewItem> item_reference;

            public TreeViewItemAccessibleObject(ControlAccessibleObject root, TreeView owner, TreeViewItem item)
                : base(root, owner)
            {
                owner_reference = new WeakReference<TreeView>(owner);
                item_reference = new WeakReference<TreeViewItem>(item);
            }

            private TreeView? Owner
                => owner_reference.TryGetTarget(out TreeView? owner) && !owner.IsDisposed ? owner : null;

            private TreeViewItem? Item
                => item_reference.TryGetTarget(out TreeViewItem? item) ? item : null;

            private bool IsAttached => Owner is { } owner && Item?.TreeView == owner;

            public override AccessibilityView View
                => IsAttached ? base.View : AccessibilityView.Hidden;

            public override AccessibleObject? Parent
            {
                get
                {
                    if (!IsAttached || Owner is not { } owner || Item is not { } item)
                        return null;

                    return owner.Items.Contains(item) || item.Parent is null
                        ? Root
                        : Root.GetTreeViewItemObject(owner, item.Parent);
                }
            }

            public override string? Name
            {
                get => Item?.Text;
                set { }
            }

            public override AccessibleRole Role => AccessibleRole.OutlineItem;

            public override AccessibleControlType ControlType => AccessibleControlType.TreeItem;

            public override AccessibleObject? HitTest(int x, int y)
            {
                if (!IsAttached || Owner is not { Visible: true })
                    return null;

                // A tree item's bounds describe its own row, not a clipping viewport.
                // Expanded descendants occupy following rows outside that rectangle. The
                // containing TreeView still performs the normal control-boundary clipping.
                if (Item?.Expanded == true)
                    for (int i = GetChildCount() - 1; i >= 0; i--)
                        if (GetChild(i)?.HitTest(x, y) is { } hit)
                            return hit;

                return base.HitTest(x, y);
            }

            public override Rectangle Bounds
                => IsAttached && Owner is { } owner && Item is { } item && owner.GetVisibleItems().Contains(item)
                    ? ToScreenBounds(owner, item.Bounds)
                    : Rectangle.Empty;

            public override AccessibleStates State
            {
                get
                {
                    var state = GetCommonState(IsAttached, Bounds.IsEmpty);

                    if (Owner is { } owner && Item is { } item && ReferenceEquals(owner.SelectedItem, item))
                        state |= AccessibleStates.Selected;

                    if (Item is { HasChildren: true } branch)
                        state |= branch.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
            {
                get
                {
                    if (!IsAttached || !IsOwnerAvailable)
                        return AccessibleActions.None;

                    var actions = AccessibleActions.Select | AccessibleActions.ScrollIntoView;
                    if (Item is { HasChildren: true })
                        actions |= AccessibleActions.Expand | AccessibleActions.Collapse;

                    return actions;
                }
            }

            public override int GetChildCount()
                => IsAttached && Item is { } item ? item.Items.Count : 0;

            public override AccessibleObject? GetChild(int index)
            {
                if (!IsAttached || Owner is not { } owner || Item is not { } item || index < 0 || index >= item.Items.Count)
                    return null;

                return Root.GetTreeViewItemObject(owner, item.Items[index]);
            }

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action)
                    || (SupportedActions & action) == 0
                    || parameter is not null
                    || !IsAttached
                    || Owner is not { } owner
                    || Item is not { } item)
                {
                    return false;
                }

                switch (action)
                {
                    case AccessibleActions.Select:
                        owner.SelectedItem = item;
                        return true;
                    case AccessibleActions.Expand when item.HasChildren:
                        item.Expand();
                        NotifyClients(AccessibleEvents.StateChange);
                        return true;
                    case AccessibleActions.Collapse when item.HasChildren:
                        item.Collapse();
                        NotifyClients(AccessibleEvents.StateChange);
                        return true;
                    case AccessibleActions.ScrollIntoView:
                        item.EnsureVisible();
                        return true;
                    default:
                        return false;
                }
            }
        }

        private sealed class TabItemAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly WeakReference<TabControl> owner_reference;
            private readonly WeakReference<TabPage> page_reference;

            public TabItemAccessibleObject(ControlAccessibleObject root, TabControl owner, TabPage page)
                : base(root, owner)
            {
                owner_reference = new WeakReference<TabControl>(owner);
                page_reference = new WeakReference<TabPage>(page);
            }

            private TabControl? Owner
                => owner_reference.TryGetTarget(out TabControl? owner) && !owner.IsDisposed ? owner : null;

            private TabPage? Page
                => page_reference.TryGetTarget(out TabPage? page) ? page : null;

            private bool IsAttached => Owner is { } owner && Page is { } page && owner.TabPages.Contains(page);

            public override AccessibilityView View
                => IsAttached ? base.View : AccessibilityView.Hidden;

            public override AccessibleObject? Parent => IsAttached ? Root : null;

            public override string? Name
            {
                get => Page?.Text;
                set { }
            }

            public override AccessibleRole Role => AccessibleRole.PageTab;

            public override AccessibleControlType ControlType => AccessibleControlType.TabItem;

            public override Rectangle Bounds
                => IsAttached && Owner is { } owner && Page is { } page
                    ? ToScreenBounds(owner, page.TabStripItem.Bounds)
                    : Rectangle.Empty;

            public override AccessibleStates State
            {
                get
                {
                    var state = GetCommonState(IsAttached, Bounds.IsEmpty);
                    if (Owner is { } owner && Page is { } page && ReferenceEquals(owner.SelectedTabPage, page))
                        state |= AccessibleStates.Selected;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
                => IsAttached && IsOwnerAvailable ? AccessibleActions.Select : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action)
                    || (SupportedActions & action) == 0
                    || action != AccessibleActions.Select
                    || parameter is not null
                    || !IsAttached
                    || Owner is not { } owner
                    || Page is not { } page)
                {
                    return false;
                }

                owner.SelectedTabPage = page;
                return true;
            }
        }

        private sealed class MenuItemAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly WeakReference<MenuBase> owner_reference;
            private readonly WeakReference<MenuItem> item_reference;

            public MenuItemAccessibleObject(ControlAccessibleObject root, MenuBase owner, MenuItem item)
                : base(root, owner)
            {
                owner_reference = new WeakReference<MenuBase>(owner);
                item_reference = new WeakReference<MenuItem>(item);
            }

            private MenuBase? Owner
                => owner_reference.TryGetTarget(out MenuBase? owner) && !owner.IsDisposed ? owner : null;

            private MenuItem? Item
                => item_reference.TryGetTarget(out MenuItem? item) ? item : null;

            private bool IsAttached => Owner is { } owner && Item?.GetTopMenu() == owner;

            public override AccessibilityView View
                => IsAttached ? base.View : AccessibilityView.Hidden;

            public override AccessibleObject? Parent
            {
                get
                {
                    if (!IsAttached || Owner is not { } owner || Item is not { } item)
                        return null;

                    return owner.Items.Contains(item) || item.Parent is null
                        ? Root
                        : Root.GetMenuItemObject(owner, item.Parent);
                }
            }

            public override string? Name
            {
                get => Item?.Text;
                set { }
            }

            public override AccessibleRole Role
                => Item is MenuSeparatorItem ? AccessibleRole.Separator : AccessibleRole.MenuItem;

            public override AccessibleControlType ControlType
                => Item is MenuSeparatorItem ? AccessibleControlType.Separator : AccessibleControlType.MenuItem;

            public override Rectangle Bounds
                => IsAttached && Item is { AccessibilityOwnerControl: { } owner } item
                    ? ToScreenBounds(owner, item.Bounds)
                    : Rectangle.Empty;

            public override AccessibleStates State
            {
                get
                {
                    var state = GetCommonState(IsAttached, Bounds.IsEmpty);
                    if (Item is not { } item)
                        return state;

                    if (!item.Enabled)
                        state |= AccessibleStates.Unavailable;
                    if (item.Checked)
                        state |= AccessibleStates.Checked;
                    if (item.Selected)
                        state |= AccessibleStates.Selected;
                    if (item.HasItems)
                        state |= item.IsDropDownOpened ? AccessibleStates.Expanded : AccessibleStates.Collapsed;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
            {
                get
                {
                    if (!IsAttached || !IsOwnerAvailable || Item is not { Enabled: true } item)
                        return AccessibleActions.None;

                    if (item is MenuSeparatorItem)
                        return AccessibleActions.None;

                    if (!item.HasItems)
                        return AccessibleActions.Invoke;

                    if (item.IsDropDownOpened)
                        return AccessibleActions.Collapse;

                    return item.AccessibilityOwnerControl?.FindForm() is not null
                        ? AccessibleActions.Expand
                        : AccessibleActions.None;
                }
            }

            public override int GetChildCount()
                => IsAttached && Item is { } item ? item.Items.Count : 0;

            public override AccessibleObject? GetChild(int index)
            {
                if (!IsAttached || Owner is not { } owner || Item is not { } item || index < 0 || index >= item.Items.Count)
                    return null;

                return Root.GetMenuItemObject(owner, item.Items[index]);
            }

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action)
                    || (SupportedActions & action) == 0
                    || parameter is not null
                    || !IsAttached
                    || Item is not { Enabled: true } item)
                {
                    return false;
                }

                switch (action)
                {
                    case AccessibleActions.Invoke when !item.HasItems:
                        item.OnClick(CreateAccessibilityClick());
                        return true;
                    case AccessibleActions.Expand when item.HasItems:
                        if (item.AccessibilityOwnerControl?.FindForm() is null)
                            return false;

                        item.ShowDropDown();
                        NotifyClients(AccessibleEvents.StateChange);
                        item.AccessibilityOwnerControl?.NotifyAccessibilityClients(AccessibleEvents.StateChange);
                        return true;
                    case AccessibleActions.Collapse when item.HasItems:
                        item.HideDropDown();
                        NotifyClients(AccessibleEvents.StateChange);
                        item.AccessibilityOwnerControl?.NotifyAccessibilityClients(AccessibleEvents.StateChange);
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
