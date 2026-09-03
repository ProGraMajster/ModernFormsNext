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
                ListBox listBox => GetListBoxItemObjects(listBox),
                ComboBox comboBox => GetComboBoxItemObjects(comboBox),
                ListView listView => listView.Items.Select(item => GetListViewItemObject(listView, item)),
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
                item => new ListBoxItemAccessibleObject(this, owner, item));

            return list_box_item_objects;
        }

        private IReadOnlyList<ComboBoxItemAccessibleObject> GetComboBoxItemObjects(ComboBox owner)
        {
            combo_box_item_objects = SynchronizeOccurrenceItems(
                combo_box_item_objects,
                owner.Items,
                item => new ComboBoxItemAccessibleObject(this, owner, item));

            return combo_box_item_objects;
        }

        private static List<TNode> SynchronizeOccurrenceItems<TNode>(
            List<TNode>? existing,
            IReadOnlyList<object> items,
            Func<object, TNode> create)
            where TNode : OccurrenceItemAccessibleObject
        {
            existing ??= [];
            var used = new bool[existing.Count];
            var synchronized = new List<TNode>(items.Count);

            foreach (object item in items)
            {
                TNode? matched = null;

                for (int i = 0; i < existing.Count; i++)
                {
                    if (!used[i] && existing[i].Represents(item))
                    {
                        used[i] = true;
                        matched = existing[i];
                        break;
                    }
                }

                matched ??= create(item);
                matched.Attach();
                synchronized.Add(matched);
            }

            for (int i = 0; i < existing.Count; i++)
            {
                if (!used[i])
                    existing[i].Detach();
            }

            return synchronized;
        }

        private ListViewItemAccessibleObject GetListViewItemObject(ListView owner, ListViewItem item)
            => list_view_item_objects.GetValue(item, value => new ListViewItemAccessibleObject(this, owner, value));

        private TreeViewItemAccessibleObject GetTreeViewItemObject(TreeView owner, TreeViewItem item)
            => tree_view_item_objects.GetValue(item, value => new TreeViewItemAccessibleObject(this, owner, value));

        private TabItemAccessibleObject GetTabItemObject(TabControl owner, TabPage page)
            => tab_item_objects.GetValue(page, value => new TabItemAccessibleObject(this, owner, value));

        private MenuItemAccessibleObject GetMenuItemObject(MenuBase owner, MenuItem item)
            => menu_item_objects.GetValue(item, value => new MenuItemAccessibleObject(this, owner, value));

        private int IndexOfListBoxItem(ListBoxItemAccessibleObject item, ListBox owner)
            => GetListBoxItemObjects(owner).IndexOfReference(item);

        private int IndexOfComboBoxItem(ComboBoxItemAccessibleObject item, ComboBox owner)
            => GetComboBoxItemObjects(owner).IndexOfReference(item);

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
            return new Rectangle(topLeft, localBounds.Size);
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
                => OwnerControl is { Enabled: true, Visible: true };

            public override AccessibilityView View
                => OwnerControl is { Visible: true } ? AccessibilityView.Control : AccessibilityView.Hidden;

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
            private bool attached = true;

            protected OccurrenceItemAccessibleObject(ControlAccessibleObject root, Control owner, object item)
                : base(root, owner)
            {
                item_reference = new WeakReference<object>(item);
            }

            protected object? Item
                => item_reference.TryGetTarget(out object? item) ? item : null;

            protected bool IsAttached => attached && Item is not null;

            public bool Represents(object item)
                => item_reference.TryGetTarget(out object? current) && ReferenceEquals(current, item);

            public void Attach() => attached = true;

            public void Detach() => attached = false;

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

            public ListBoxItemAccessibleObject(ControlAccessibleObject root, ListBox owner, object item)
                : base(root, owner, item)
            {
                owner_reference = new WeakReference<ListBox>(owner);
            }

            private ListBox? Owner
                => owner_reference.TryGetTarget(out ListBox? owner) && !owner.IsDisposed ? owner : null;

            private int Index => Owner is { } owner ? Root.IndexOfListBoxItem(this, owner) : -1;

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

                    if (owner.Items.SelectedIndexes.Contains(Index))
                        state |= AccessibleStates.Selected;

                    if (owner.Focused && owner.Items.FocusedIndex == Index)
                        state |= AccessibleStates.Focused;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
                => Index >= 0 ? AccessibleActions.Select | AccessibleActions.ScrollIntoView : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (parameter is not null || Owner is not { } owner || Index < 0 || !IsOwnerAvailable)
                    return false;

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

                if ((flags & AccessibleSelection.TakeFocus) != 0)
                    Owner?.Select();
            }
        }

        private sealed class ComboBoxItemAccessibleObject : OccurrenceItemAccessibleObject
        {
            private readonly WeakReference<ComboBox> owner_reference;

            public ComboBoxItemAccessibleObject(ControlAccessibleObject root, ComboBox owner, object item)
                : base(root, owner, item)
            {
                owner_reference = new WeakReference<ComboBox>(owner);
            }

            private ComboBox? Owner
                => owner_reference.TryGetTarget(out ComboBox? owner) && !owner.IsDisposed ? owner : null;

            private int Index => Owner is { } owner ? Root.IndexOfComboBoxItem(this, owner) : -1;

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
                => Index >= 0 ? AccessibleActions.Select : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (action != AccessibleActions.Select
                    || parameter is not null
                    || Owner is not { } owner
                    || Index < 0
                    || !IsOwnerAvailable)
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
                => IsAttached && Owner is { } owner && Item is { } item
                    ? ToScreenBounds(owner, item.Bounds)
                    : Rectangle.Empty;

            public override AccessibleStates State
            {
                get
                {
                    var state = GetCommonState(IsAttached, Bounds.IsEmpty);
                    if (Item?.Selected == true)
                        state |= AccessibleStates.Selected;

                    return state;
                }
            }

            public override AccessibleActions SupportedActions
                => IsAttached ? AccessibleActions.Select : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (action != AccessibleActions.Select
                    || parameter is not null
                    || !IsOwnerAvailable
                    || !IsAttached
                    || Owner is not { } owner
                    || Item is not { } item)
                {
                    return false;
                }

                owner.SelectedItem = item;
                return true;
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
                    if (!IsAttached)
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
                if (parameter is not null
                    || !IsOwnerAvailable
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
                => IsAttached ? AccessibleActions.Select : AccessibleActions.None;

            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (action != AccessibleActions.Select
                    || parameter is not null
                    || !IsOwnerAvailable
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
                => Item switch
                {
                    _ when !IsAttached => AccessibleActions.None,
                    MenuSeparatorItem => AccessibleActions.None,
                    { HasItems: true } => AccessibleActions.Expand | AccessibleActions.Collapse,
                    not null => AccessibleActions.Invoke,
                    _ => AccessibleActions.None
                };

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
                if (parameter is not null
                    || !IsOwnerAvailable
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
                        return true;
                    case AccessibleActions.Collapse when item.HasItems:
                        item.HideDropDown();
                        NotifyClients(AccessibleEvents.StateChange);
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}

internal static class AccessibleObjectListExtensions
{
    public static int IndexOfReference<T>(this IReadOnlyList<T> items, T item)
        where T : class
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item))
                return i;
        }

        return -1;
    }
}
