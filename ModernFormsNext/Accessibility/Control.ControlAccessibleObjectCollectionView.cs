using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext;

// This new partial follows the existing accessibility partials in compiler input order.
// Preserve the generated state-machine type names on their public iterator attributes.
public partial class Control
{
    public partial class ControlAccessibleObject
    {
        // Only queried occurrences have peers. Weak entries preserve an externally retained
        // peer across recycling without retaining every item value or every visited container.
        private Dictionary<int, WeakReference<CollectionItemAccessibleObject>>? collectionItemPeers;
        private Dictionary<int, CollectionItemAccessibleObject>? collectionPeerPins;
        private List<int>? collectionPinRetire;
        private int collectionPinStart = -1, collectionPinEnd, collectionPinSelection = -1, collectionPinFocus = -1;
        private long collectionPinVersion = -1;
        private int collectionPeerGc;

        private CollectionItemAccessibleObject GetCollectionItemObject(CollectionView owner, int index)
        {
            collectionItemPeers ??= new();
            int gc = GC.CollectionCount(0);
            if (collectionItemPeers.Count > 64 && collectionPeerGc != gc)
            {
                collectionPeerGc = gc;
                List<int>? dead = null;
                foreach (var entry in collectionItemPeers)
                    if (!entry.Value.TryGetTarget(out _)) (dead ??= []).Add(entry.Key);
                if (dead is not null) foreach (int key in dead) collectionItemPeers.Remove(key);
            }
            if (collectionItemPeers.TryGetValue(index, out var weak) && weak.TryGetTarget(out var existing))
                return PinCollectionItem(owner, index, existing);
            var peer = new CollectionItemAccessibleObject(this, owner, index);
            collectionItemPeers[index] = new(peer);
            return PinCollectionItem(owner, index, peer);
        }

        private CollectionItemAccessibleObject PinCollectionItem(CollectionView owner, int index, CollectionItemAccessibleObject peer)
        {
            collectionPeerPins ??= new();
            var range = owner.RealizedRange;
            int selected = owner.SelectedIndex, focused = owner.GetFocusedItemIndex();
            if (range.StartIndex != collectionPinStart || range.EndIndex != collectionPinEnd ||
                selected != collectionPinSelection || focused != collectionPinFocus || owner.ItemsGeneration != collectionPinVersion)
            {
                collectionPinStart = range.StartIndex; collectionPinEnd = range.EndIndex;
                collectionPinSelection = selected; collectionPinFocus = focused; collectionPinVersion = owner.ItemsGeneration;
                collectionPinRetire?.Clear();
                foreach (var entry in collectionPeerPins)
                    if (!range.Contains(entry.Key) && entry.Key != selected && entry.Key != focused)
                        (collectionPinRetire ??= []).Add(entry.Key);
                if (collectionPinRetire is not null)
                    foreach (int retired in collectionPinRetire) collectionPeerPins.Remove(retired);
            }
            // Native Windows providers intentionally hold weak node references. Pin only the
            // current viewport/cache and selected/focused occurrences so GC cannot replace their
            // canonical IDs while they are still active. Offscreen queried peers remain weak.
            if (range.Contains(index) || index == selected || index == focused) collectionPeerPins[index] = peer;
            return peer;
        }

        internal void ApplyCollectionItemChange(in ItemIndexChange change)
        {
            if (Owner is not CollectionView || collectionItemPeers is null) return;
            var mapped = new Dictionary<int, WeakReference<CollectionItemAccessibleObject>>(collectionItemPeers.Count);
            foreach (var entry in collectionItemPeers)
            {
                if (!entry.Value.TryGetTarget(out var peer)) continue;
                int next = change.MapIndex(entry.Key);
                peer.Index = next; // -1 permanently retires a removed/replaced/reset occurrence.
                if (next >= 0) mapped.Add(next, entry.Value);
            }
            collectionItemPeers = mapped;
            if (collectionPeerPins is not null)
            {
                var pins = new Dictionary<int, CollectionItemAccessibleObject>(collectionPeerPins.Count);
                foreach (var peer in collectionPeerPins.Values) if (peer.Index >= 0) pins.Add(peer.Index, peer);
                collectionPeerPins = pins;
                collectionPinStart = -1;
            }
        }

        private IEnumerable<AccessibleObject> GetCollectionLogicalChildren(CollectionView owner)
        {
            for (int index = 0; !owner.IsDisposed && index < owner.ItemCount; index++)
                yield return GetCollectionItemObject(owner, index);
        }

        private IEnumerable<AccessibleObject> GetCollectionVisualChildren(CollectionView owner)
        {
            foreach (Control row in owner.Controls)
            {
                if (owner.OwnsItemContainer(row)) continue;
                var peer = row.AccessibilityObject;
                if (peer.View != AccessibilityView.Hidden &&
                    (IsVisibilityDeterminedByView(peer) || (peer.State & AccessibleStates.Invisible) == 0)) yield return peer;
            }
        }

        private int GetCollectionChildCount(CollectionView owner)
            => owner.IsDisposed || owner.AccessibilityObject.View == AccessibilityView.Hidden ? 0
                : checked(owner.ItemCount + GetCollectionVisualChildren(owner).Count());

        private AccessibleObject? GetCollectionChild(CollectionView owner, int index)
            => owner.IsDisposed || View == AccessibilityView.Hidden ? null
                : index < owner.ItemCount ? GetCollectionItemObject(owner, index)
                : GetCollectionVisualChildren(owner).ElementAtOrDefault(index - owner.ItemCount);

        private bool TryGetCollectionProjection(out IReadOnlyList<AccessibleObject> children, out int? count)
        {
            children = Array.Empty<AccessibleObject>(); count = null;
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not CollectionView { IsDisposed: false } owner) return false;
            count = owner.ItemCount;
            if (!owner.Visible || View == AccessibilityView.Hidden) return true;
            var range = owner.RealizedRange;
            List<AccessibleObject> peers = new(Math.Min(range.Count, owner.ItemCount) + 2);
            for (int index = range.StartIndex; index < range.EndIndex && index < owner.ItemCount; index++)
                peers.Add(GetCollectionItemObject(owner, index));
            ReadOnlySpan<int> retained = [owner.SelectedIndex, owner.GetFocusedItemIndex()];
            foreach (int index in retained)
                if ((uint)index < (uint)owner.ItemCount && !range.Contains(index))
                {
                    var peer = GetCollectionItemObject(owner, index);
                    if (!peers.Contains(peer)) peers.Add(peer);
                }
            foreach (var child in GetCollectionVisualChildren(owner)) { peers.Add(child); count = null; }
            children = peers;
            return true;
        }

        private bool TryContainsCollectionChild(AccessibleObject child, out bool contains)
        {
            contains = false;
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not CollectionView owner) return false;
            if (child is CollectionItemAccessibleObject item && ReferenceEquals(item.Parent, this))
            {
                int index = item.Index;
                contains = item.IsAttached && (owner.RealizedRange.Contains(index) || owner.SelectedIndex == index || owner.GetFocusedItemIndex() == index);
            }
            else
            {
                // Avoid a captured predicate: ancestor probes also call this method for
                // controls other than CollectionView, where no allocation is needed.
                foreach (var peer in GetCollectionVisualChildren(owner))
                    if (ReferenceEquals(peer, child)) { contains = true; break; }
            }
            return true;
        }

        private bool TryGetCollectionItemIndex(AccessibleObject child, out int index)
        {
            index = -1;
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not CollectionView ||
                child is not CollectionItemAccessibleObject item || !ReferenceEquals(item.Parent, this)) return false;
            index = item.IsAttached ? item.Index : -1;
            return true;
        }

        private bool TryGetCollectionIndexedChild(int index, out AccessibleObject? child)
        {
            child = null;
            if (GetType() != typeof(ControlAccessibleObject) || Owner is not CollectionView { IsDisposed: false } owner ||
                GetCollectionVisualChildren(owner).Any()) return false;
            if ((uint)index < (uint)owner.ItemCount && owner.Visible && View != AccessibilityView.Hidden)
                child = GetCollectionItemObject(owner, index);
            return true;
        }

        private sealed class CollectionItemAccessibleObject(ControlAccessibleObject root, CollectionView owner, int index)
            : LogicalItemAccessibleObject(root, owner)
        {
            internal int Index = index;
            private CollectionView? Owner => OwnerControl as CollectionView;
            internal bool IsAttached => Owner is { } current && (uint)Index < (uint)current.ItemCount;
            public override AccessibleObject? Parent => IsAttached ? Root : null;
            public override AccessibilityView View => IsAttached ? base.View : AccessibilityView.Hidden;
            public override AccessibleRole Role => AccessibleRole.ListItem;
            public override AccessibleControlType ControlType => AccessibleControlType.ListItem;
            private Control? Row => IsAttached && Owner is { } current && current.GetItemContainer(Index) is { } row &&
                current.TryGetCurrentItemIndex(row, out int bound) && bound == Index ? row : null;

            public override string? Name
            {
                get
                {
                    if (!IsAttached || Owner is not { } current) return null;
                    long version = current.ItemsGeneration;
                    string? name = Row?.AccessibilityObject.Name;
                    // A custom template may intentionally redact a model's text (password
                    // editors are one example). Never expose its raw ToString offscreen just
                    // because no visual row currently supplies an explicit accessible label.
                    if (string.IsNullOrEmpty(name)) name = current.UsesDefaultItemTemplate
                        ? current.GetAccessibilityItem(Index)?.ToString() : $"Item {Index + 1}";
                    return !current.IsDisposed && version == current.ItemsGeneration && IsAttached ? name : null;
                }
                set { }
            }
            public override Rectangle Bounds => Row is { } row ? ToScreenBounds(row, row.ClientRectangle) : Rectangle.Empty;
            public override bool IsSensitive => Row is TextBox { IsAccessibilitySensitive: true } || Row?.AccessibilityObject.IsSensitive == true;
            public override AccessibleStates State
            {
                get
                {
                    var bounds = Bounds;
                    bool offscreen = bounds.IsEmpty || Owner is not { } current ||
                        !ToScreenBounds(current, current.NativeHostingViewport).IntersectsWith(bounds);
                    var state = GetCommonState(IsAttached, offscreen);
                    if (Owner is { } selected && selected.SelectedIndex == Index) state |= AccessibleStates.Selected;
                    if (Owner is { } focused && focused.GetFocusedItemIndex() == Index) state |= AccessibleStates.Focused;
                    return state;
                }
            }
            private AccessibleObject? Child
            {
                get
                {
                    var peer = Row?.AccessibilityObject;
                    return peer is not null && peer.View != AccessibilityView.Hidden &&
                        (IsVisibilityDeterminedByView(peer) || (peer.State & AccessibleStates.Invisible) == 0) && ReferenceEquals(peer.Parent, this) ? peer : null;
                }
            }
            public override int GetChildCount() => Child is null ? 0 : 1;
            public override AccessibleObject? GetChild(int childIndex) => childIndex == 0 ? Child : null;
            public override AccessibleObject? GetFocused() => Row?.AccessibilityObject.GetFocused();
            public override AccessibleObject? HitTest(int x, int y)
                => Bounds.Contains(x, y) ? Child?.HitTest(x, y) ?? this : null;
            public override AccessibleObject? Navigate(AccessibleNavigation direction)
            {
                if (!IsAttached || Owner is not { } current) return null;
                if (direction is AccessibleNavigation.Next or AccessibleNavigation.Previous)
                {
                    int next = Index + (direction == AccessibleNavigation.Next ? 1 : -1);
                    return (uint)next < (uint)current.ItemCount ? Root.GetCollectionItemObject(current, next) : null;
                }
                return base.Navigate(direction);
            }
            public override AccessibleActions SupportedActions => !IsAttached || !IsOwnerAvailable ? AccessibleActions.None
                : AccessibleActions.Select | AccessibleActions.Invoke | (Owner?.VirtualizationEnabled == true
                    ? AccessibleActions.Focus | AccessibleActions.ScrollIntoView : AccessibleActions.None);
            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (!IsSingleAction(action) || parameter is not null || (SupportedActions & action) == 0 || Owner is not { } current) return false;
                int expected = Index;
                long version = current.ItemsGeneration;
                var lifetime = new AccessibilityControlLifetime(current);
                if (action == AccessibleActions.Select)
                {
                    current.SelectedIndex = expected;
                    return lifetime.IsCurrent && current.ItemsGeneration == version && Index == expected &&
                        IsAttached && IsOwnerAvailable && current.SelectedIndex == expected;
                }
                if (action == AccessibleActions.Invoke) return current.InvokeLogicalItem(expected) && lifetime.IsCurrent;
                if (!current.RevealAccessibilityItem(expected)) return false;
                if (!lifetime.IsCurrent || current.ItemsGeneration != version || Index != expected || !IsAttached || !IsOwnerAvailable) return false;
                if (action == AccessibleActions.Focus)
                {
                    Row?.Select();
                    return lifetime.IsCurrent && current.ItemsGeneration == version && Index == expected &&
                        IsAttached && IsOwnerAvailable && current.GetFocusedItemIndex() == expected;
                }
                return !Bounds.IsEmpty;
            }
            public override void Select(AccessibleSelection flags)
            {
                if ((flags & (AccessibleSelection.TakeSelection | AccessibleSelection.AddSelection)) != 0) PerformAction(AccessibleActions.Select);
                if ((flags & AccessibleSelection.RemoveSelection) != 0 && IsAttached && Owner is { } current && current.SelectedIndex == Index) current.SelectedIndex = -1;
                if ((flags & AccessibleSelection.TakeFocus) != 0) PerformAction(AccessibleActions.Focus);
            }
        }
    }
}
