using System.Collections.Immutable;
using System.Globalization;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation;

/// <summary>One bounded UI-thread capture. No peer references escape this operation.</summary>
internal sealed class SemanticTraversal(AutomationQueryOptions limits, CancellationToken token)
{
    internal const int MaxTextLength = 4096;
    private readonly Dictionary<long, Entry> byId = [];
    private readonly HashSet<AccessibleObject> seen = new(ReferenceEqualityComparer.Instance);
    private readonly List<AutomationIssue> issues = [];
    private int attempts;
    internal List<Entry> Entries { get; } = [];
    internal bool Truncated { get; private set; }
    internal AutomationErrorCode Error => issues.Count > 0 ? issues[0].Code
        : Truncated ? AutomationErrorCode.LimitExceeded : AutomationErrorCode.None;
    internal ImmutableArray<AutomationIssue> Issues => issues.ToImmutableArray();

    internal Entry? Find(string runtimeId)
        => long.TryParse(runtimeId, NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            && byId.TryGetValue(id, out var entry) ? entry : null;

    internal void Walk(AccessibleObject root)
    {
        var first = Visit(root, null, 0);
        if (first is null) return;
        var stack = new Stack<Frame>();
        Push(first, stack);
        while (stack.TryPeek(out var frame))
        {
            token.ThrowIfCancellationRequested();
            if (frame.Next >= frame.Count) { stack.Pop(); continue; }
            if (attempts >= limits.MaxNodes)
            {
                foreach (var pending in stack) pending.Entry.Truncated = true;
                Limit(frame.Entry, AutomationProperty.Children);
                break;
            }
            int index = frame.Next++;
            // Count every attempted edge, even null/throwing/repeated/hidden children.
            // GetChildCount can report Int32.MaxValue without causing proportional allocation.
            attempts++;
            AccessibleObject? child = Read(frame.Entry, AutomationProperty.Children, () => frame.Entry.Peer.GetChild(index), null);
            if (child is null)
            {
                Add(AutomationErrorCode.MalformedTree, frame.Entry, AutomationProperty.Children);
                continue;
            }
            var visited = Visit(child, frame.Entry, frame.Entry.Depth + 1);
            if (visited is not null) Push(visited, stack);
        }

        // A scoped surface root may have an external semantic parent. A parent pointing back
        // inside its own captured subtree, however, is a parent cycle; never follow that chain.
        if (first.ActualParent is { } parent && seen.Contains(parent))
            Add(AutomationErrorCode.CycleDetected, first, AutomationProperty.Parent);
    }

    private Entry? Visit(AccessibleObject peer, Entry? parent, int depth)
    {
        token.ThrowIfCancellationRequested();
        if (parent is null) attempts++;
        var entry = new Entry(peer, parent, depth);
        if (!seen.Add(peer) || byId.ContainsKey(peer.RuntimeId))
        {
            bool cycle = false;
            for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor.Peer, peer)) { cycle = true; break; }
            Add(cycle ? AutomationErrorCode.CycleDetected : AutomationErrorCode.DuplicateRuntimeId, entry, AutomationProperty.Node);
            return null;
        }
        if (peer.RuntimeId <= 0)
        {
            Add(AutomationErrorCode.MalformedTree, entry, AutomationProperty.Node);
            return null;
        }

        // Read privacy first, fail closed, and propagate it to custom descendants. Payload
        // getters are never called for a sensitive node or an unknown privacy classification.
        int before = issues.Count;
        bool sensitive = Read(entry, AutomationProperty.IsSensitive, () => peer.IsSensitive, true);
        // A custom ControlAccessibleObject must not be able to negate its known password owner
        // merely by overriding IsSensitive/State. Unknown custom owners still use the canonical markers.
        sensitive |= peer is Control.ControlAccessibleObject { Owner: TextBox { PasswordCharacter: not null } };
        entry.States = Read(entry, AutomationProperty.States, () => peer.State, AccessibleStates.Unavailable);
        entry.Redaction = parent?.Redaction ?? AutomationRedaction.None;
        if (issues.Count != before) entry.Redaction |= AutomationRedaction.PrivacyUnknown;
        if (sensitive || (entry.States & AccessibleStates.Protected) != 0) entry.Redaction |= AutomationRedaction.Sensitive;
        var view = Read(entry, AutomationProperty.View, () => peer.View, AccessibilityView.Hidden);
        if (view == AccessibilityView.Hidden || (entry.States & AccessibleStates.Invisible) != 0) return null;

        before = issues.Count;
        entry.ActualParent = Read(entry, AutomationProperty.Parent, () => peer.Parent, null);
        if (issues.Count != before) return null;
        if (parent is not null && !ValidateParent(entry, parent)) return null;
        byId.Add(peer.RuntimeId, entry);
        Entries.Add(entry);
        parent?.Children.Add(entry);
        return entry;
    }

    private bool ValidateParent(Entry entry, Entry expected)
    {
        // Canonical Form children intentionally omit FormClientArea although their Parent points
        // through that implementation peer. Validate the bounded canonical parent chain without
        // projecting these omitted peers or enumerating a second control/visual tree.
        var parents = new HashSet<AccessibleObject>(ReferenceEqualityComparer.Instance) { entry.Peer };
        var current = entry.ActualParent;
        int depth = 0;
        while (current is not null)
        {
            if (!parents.Add(current)) { Add(AutomationErrorCode.CycleDetected, entry, AutomationProperty.Parent); return false; }
            if (ReferenceEquals(current, expected.Peer)) return true;
            if (++depth > limits.MaxDepth || attempts >= limits.MaxNodes) { Limit(entry, AutomationProperty.Parent); return false; }
            attempts++;
            token.ThrowIfCancellationRequested();
            var next = current;
            current = Read(entry, AutomationProperty.Parent, () => next.Parent, null);
        }
        Add(AutomationErrorCode.MalformedTree, entry, AutomationProperty.Parent);
        return false;
    }

    private void Push(Entry entry, Stack<Frame> stack)
    {
        int count = Read(entry, AutomationProperty.Children, entry.Peer.GetChildCount, 0);
        if (count < 0) { Add(AutomationErrorCode.MalformedTree, entry, AutomationProperty.Children); return; }
        if (count == 0) return;
        if (entry.Depth >= limits.MaxDepth) { Limit(entry, AutomationProperty.Children); return; }
        stack.Push(new(entry, count));
    }

    internal AutomationNodeSnapshot Capture(Entry entry, string sessionId, string rootId,
        AutomationCoordinateSpace coordinates, string captureId)
    {
        token.ThrowIfCancellationRequested();
        var peer = entry.Peer;
        RefreshPrivacy(entry);
        string? automationId = null, name = null, value = null;
        AccessibleRangeValue? range = null;
        if (entry.Redaction == AutomationRedaction.None)
        {
            automationId = Text(entry, AutomationProperty.AutomationId, () => peer.AutomationId);
            name = Text(entry, AutomationProperty.Name, () => peer.Name);
            value = Text(entry, AutomationProperty.Value, () => peer.Value);
            range = Read(entry, AutomationProperty.RangeValue, () => peer.RangeValue, null);
        }
        var role = Read(entry, AutomationProperty.Role, () => peer.Role, AccessibleRole.Default);
        var type = Read(entry, AutomationProperty.ControlType, () => peer.ControlType, AccessibleControlType.Custom);
        var actions = Read(entry, AutomationProperty.SupportedActions, () => peer.SupportedActions, AccessibleActions.None);
        var bounds = Read(entry, AutomationProperty.Bounds, () => peer.Bounds, System.Drawing.Rectangle.Empty);
        // A getter can reenter application code. Discard captured payload if privacy became
        // sensitive/unknown during capture instead of returning a value classified earlier.
        RefreshPrivacy(entry);
        if (entry.Redaction != AutomationRedaction.None) { automationId = null; name = null; value = null; range = null; }
        return new(new(sessionId, entry.Id), rootId, automationId, name, role, type, entry.States,
            actions, value, range, new(bounds.X, bounds.Y, bounds.Width, bounds.Height, coordinates),
            entry.Parent?.Id, entry.Children.Select(child => child.Id).ToImmutableArray(), entry.Redaction, captureId, entry.Truncated);
    }

    private void RefreshPrivacy(Entry entry)
    {
        int before = issues.Count;
        bool sensitive = Read(entry, AutomationProperty.IsSensitive, () => entry.Peer.IsSensitive, true);
        var state = Read(entry, AutomationProperty.States, () => entry.Peer.State, AccessibleStates.Unavailable);
        entry.Redaction |= entry.Parent?.Redaction ?? AutomationRedaction.None;
        if (issues.Count != before) entry.Redaction |= AutomationRedaction.PrivacyUnknown;
        if (sensitive || (state & AccessibleStates.Protected) != 0
            || entry.Peer is Control.ControlAccessibleObject { Owner: TextBox { PasswordCharacter: not null } })
            entry.Redaction |= AutomationRedaction.Sensitive;
    }

    private string? Text(Entry entry, AutomationProperty property, Func<string?> getter)
    {
        string? value = Read(entry, property, getter, null);
        if (value is { Length: > MaxTextLength }) { Limit(entry, property); return null; }
        return value;
    }

    internal T Read<T>(Entry entry, AutomationProperty property, Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch (Exception)
        {
            Add(AutomationErrorCode.GetterFault, entry, property);
            return fallback;
        }
    }

    internal void Limit(Entry? entry = null, AutomationProperty property = AutomationProperty.Node)
    {
        Truncated = true;
        if (entry is not null) entry.Truncated = true;
        Add(AutomationErrorCode.LimitExceeded, entry, property);
    }

    private void Add(AutomationErrorCode code, Entry? entry, AutomationProperty property)
    {
        // Diagnostics must be bounded too, including multiple faults per hostile peer.
        if (issues.Count < limits.MaxNodes) issues.Add(new(code, entry?.Id, property));
        else Truncated = true;
    }

    internal sealed class Entry(AccessibleObject peer, Entry? parent, int depth)
    {
        internal AccessibleObject Peer { get; } = peer;
        internal string Id { get; } = peer.RuntimeId.ToString(CultureInfo.InvariantCulture);
        internal Entry? Parent { get; } = parent;
        internal AccessibleObject? ActualParent { get; set; }
        internal int Depth { get; } = depth;
        internal AccessibleStates States { get; set; }
        internal AutomationRedaction Redaction { get; set; }
        internal bool Truncated { get; set; }
        internal List<Entry> Children { get; } = [];
    }

    private sealed class Frame(Entry entry, int count)
    {
        internal Entry Entry { get; } = entry;
        internal int Count { get; } = count;
        internal int Next { get; set; }
    }
}
