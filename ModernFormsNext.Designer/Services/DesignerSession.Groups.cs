using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

public sealed partial class DesignerSession
{
    internal void SelectNodeFromPointer(DesignControlNode node, bool toggle, bool extend)
    {
        EnsureNodeBelongsToDocument(node);
        var members = SelectedNodes.ToList();
        bool contained = members.Contains(node);
        if (toggle && contained)
        {
            members.Remove(node);
            Host.Selection.SelectNodes(members, ReferenceEquals(SelectedNode, node) ? null : SelectedNode);
        }
        else if (toggle || extend || contained)
        {
            if (!contained) members.Add(node);
            Host.Selection.SelectNodes(members, node);
        }
        else SelectNode(node);
    }

    internal bool ReparentSelectedNodesAtDocumentPoint(DesignPoint point)
    {
        if (!TryGetSelectedRoots(out var roots, out _)) return false;
        var layout = new DesignerLayoutEngine().Layout(Document, PreviewResourcePolicy);
        var excluded = new HashSet<DesignControlNode>(roots, ReferenceEqualityComparer.Instance);
        var parent = FindDeepestContainerAtPoint(Document.Controls, null, layout, roots[0], point,
            new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height), excluded);
        return ReparentSelectedNodes(parent);
    }

    private bool TryGetSelectedRoots(out List<DesignControlNode> roots, out string? error)
    {
        roots = [];
        var validation = new DesignDocumentValidator().ValidateStructure(Document);
        if (!validation.IsValid) { error = string.Join(Environment.NewLine, validation.Errors); return false; }
        var unresolved = new HashSet<DesignControlNode>(SelectedNodes, ReferenceEqualityComparer.Instance);
        if (unresolved.Count == 0)
        {
            error = "Select controls before applying a group operation.";
            return false;
        }
        var selected = new HashSet<DesignControlNode>(unresolved, ReferenceEqualityComparer.Instance);
        int selectedAncestorDepth = -1;
        // Walk the canonical outline once. Roots keep authored order; a selected ancestor owns
        // its selected descendants, so moves/copies/deletes never process the same subtree twice.
        foreach (var (node, depth) in EnumerateNodes())
        {
            unresolved.Remove(node);
            if (selectedAncestorDepth >= 0 && depth > selectedAncestorDepth) continue;
            selectedAncestorDepth = -1;
            if (!selected.Contains(node)) continue;
            roots.Add(node);
            selectedAncestorDepth = depth;
        }
        if (unresolved.Count > 0)
        {
            error = "Every selected control must belong to the active document.";
            return false;
        }
        if (roots.Any(DesignerSpecialContainers.IsSpecialGeneratedPart))
        {
            error = "Structural Designer nodes cannot be edited independently of their owner.";
            return false;
        }
        if (roots.Any(node => !ReferenceEquals(GetComponentBoundary(node), node)))
        {
            error = "Select the owning component instead of editing its embedded controls independently.";
            return false;
        }
        error = null;
        return true;
    }

    internal bool CanEditSelectedProperties(out string? error)
    {
        if (!TryGetSelectedRoots(out _, out error)) return false;
        // Tree operations normalize descendants under an owner. Property edits target every
        // raw member, so they must also reject covered structural/component-internal members.
        if (SelectedNodes.Any(node => DesignerSpecialContainers.IsSpecialGeneratedPart(node)
            || !ReferenceEquals(GetComponentBoundary(node), node)))
        {
            error = "Embedded and structural controls require editing through their owner.";
            return false;
        }
        return true;
    }

    internal bool TryGetManualSelectedRoots(out List<DesignControlNode> roots, out string? error)
    {
        if (!TryGetSelectedRoots(out roots, out error)) return false;
        foreach (var node in roots)
        {
            var parent = FindParent(node);
            if (DesignerLayoutProperties.IsDocked(node) || parent is not null && (PreservesSequentialChildOrder(parent)
                || DesignPageContainers.IsPage(parent.TypeName) || DesignPageContainers.IsType(parent.TypeName, "PageHost")))
            {
                error = "The selection contains a control whose position is managed by its container or Dock.";
                return false;
            }
        }
        error = null;
        return true;
    }

    /// <summary>Moves the selected control roots by one common parent-local logical-pixel offset.</summary>
    /// <param name="deltaX">Horizontal offset in logical pixels.</param>
    /// <param name="deltaY">Vertical offset in logical pixels.</param>
    /// <returns>Whether any bounds changed.</returns>
    /// <remarks>
    /// Call on the session's UI thread. Selected descendants move with their selected ancestor once.
    /// Docked controls and children of flow, table and tab layouts reject the entire operation.
    /// Coordinates are checked before mutation; one undo unit preserves membership and primary.
    /// The committed model event requests preview layout and repaint through existing observers.
    /// </remarks>
    /// <example><code>session.MoveSelectedNodes(deltaX: 8, deltaY: 0);</code></example>
    public bool MoveSelectedNodes(int deltaX, int deltaY)
    {
        ThrowIfDisposed();
        if (!TryGetManualSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        var plan = new List<(DesignControlNode Node, DesignBounds Bounds)>(roots.Count);
        foreach (var node in roots)
        {
            if (!TryTranslateBounds(node.Bounds, deltaX, deltaY, out var bounds))
            { Log("The group move exceeds the supported coordinate range."); return false; }
            plan.Add((node, bounds));
        }
        return ApplyGroupBounds(plan, "Move selected controls");
    }

    private bool ApplyGroupBounds(IReadOnlyList<(DesignControlNode Node, DesignBounds Bounds)> plan, string description)
    {
        if (plan.All(item => item.Node.Bounds == item.Bounds)) return false;
        using var transaction = Transactions.Begin(description);
        foreach (var (node, bounds) in plan) SetNodeBounds(node, bounds);
        transaction.Commit();
        return true;
    }

    private bool ReorderSelectedGroup(int delta)
    {
        if (!TryGetSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        var parents = roots.Select(FindParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance).ToArray();
        if (parents.Length != 1) { Log("Reordering requires selected siblings."); return false; }
        var collection = parents[0]?.Children ?? Document.Controls;
        var before = collection.ToArray();
        var after = before.ToArray();
        var selected = new HashSet<DesignControlNode>(roots, ReferenceEqualityComparer.Instance);
        for (int step = 1; step < after.Length; step++)
        {
            int index = delta < 0 ? step : after.Length - step - 1;
            int adjacent = index + Math.Sign(delta);
            if (selected.Contains(after[index]) && !selected.Contains(after[adjacent]))
                (after[index], after[adjacent]) = (after[adjacent], after[index]);
        }
        if (before.SequenceEqual(after)) return false;
        var pageSelection = CapturePageSelection(parents[0]);
        using var transaction = Transactions.Begin("Reorder selected controls");
        Transactions.ExecuteChange(new DesignerChildrenReplaceChange(collection, before, after));
        RestorePageSelection(parents[0], pageSelection);
        transaction.Commit();
        return true;
    }

    private bool MoveSelectedGroupOut()
    {
        if (!TryGetSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        var parents = roots.Select(FindParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance).ToArray();
        if (parents.Length != 1 || parents[0] is not { } parent)
        { Log("Move out requires selected siblings inside one container."); return false; }
        var destination = FindParent(parent);
        if (DesignerSpecialContainers.IsSpecialGeneratedPart(parent) && destination is not null)
            destination = FindParent(destination);
        return ReparentSelectedNodes(destination);
    }

    private bool MoveSelectedGroupToNextContainer()
    {
        ThrowIfDisposed();
        if (!TryGetSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        var containers = EnumerateNodes().Select(item => item.Node).Where(IsContainerNode)
            .Where(candidate => !roots.Any(node => ReferenceEquals(node, candidate) || ContainsDescendant(node, candidate))).ToArray();
        if (containers.Length == 0) return false;
        int current = Array.FindIndex(containers, candidate => ReferenceEquals(candidate, FindParent(roots[0])));
        // Match the singular command: try the next different container, rather than silently
        // bypassing a rejected destination to mutate an unrelated one later in the outline.
        for (int offset = 1; offset <= containers.Length; offset++)
        {
            var candidate = containers[(current + offset) % containers.Length];
            if (!roots.All(node => ReferenceEquals(FindParent(node), candidate))) return ReparentSelectedNodes(candidate);
        }
        return false;
    }

    /// <summary>Moves the selected subtrees together into a document container.</summary>
    /// <param name="parent">Destination container, or <see langword="null"/> for the design root.</param>
    /// <returns>Whether the complete group changed parent.</returns>
    /// <remarks>
    /// Call on the session's UI thread. Free-layout destinations preserve document-space positions.
    /// Managed containers own placement; table spans are retained and placed automatically by the
    /// canonical layout engine after bounded preview admission. Hierarchy, source and destination
    /// slots, cycles and coordinates are validated before mutation. Selection and one undo unit
    /// cover the complete operation. A TabControl accepts pages directly, otherwise its active
    /// page is used; a SplitContainer uses its existing first panel. No structural parts are created.
    /// </remarks>
    /// <example><code>session.ReparentSelectedNodes(destinationPanel);</code></example>
    public bool ReparentSelectedNodes(DesignControlNode? parent)
    {
        ThrowIfDisposed();
        if (!TryGetSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        if (parent is not null && (!TryFindNode(parent, out _, out _) || !IsContainerNode(parent))) return false;
        if (parent is not null && !ReferenceEquals(GetComponentBoundary(parent), parent))
        { Log("The destination is inside an atomic component; select an editable document container."); return false; }
        if (parent is not null && roots.Any(node => ReferenceEquals(node, parent) || ContainsDescendant(node, parent)))
        { Log("A group cannot be moved into itself or one of its descendants."); return false; }
        var actualParent = parent;
        if (parent is not null && DesignerSpecialContainers.IsTabControl(parent)
            && !roots.All(DesignerSpecialContainers.IsTabPage)) actualParent = DesignerSpecialContainers.GetSelectedTabPage(parent);
        else if (parent is not null && DesignerSpecialContainers.IsSplitContainer(parent)) actualParent = DesignerSpecialContainers.GetPanel1(parent);
        if (parent is not null && actualParent is null) { Log("The destination's structural container is unavailable."); return false; }
        if (actualParent is not null && roots.Any(node => ReferenceEquals(node, actualParent) || ContainsDescendant(node, actualParent)))
        { Log("A group cannot be moved into itself or one of its descendants."); return false; }
        var destination = actualParent?.Children ?? Document.Controls;
        if (roots.All(node => ReferenceEquals(FindParent(node), actualParent))) return false;
        var target = new DesignerPasteTarget(destination, actualParent, actualParent?.Name ?? Document.FormName, destination.Count);
        var moving = new HashSet<DesignControlNode>(roots, ReferenceEqualityComparer.Instance);
        var sourceParents = roots.Select(FindParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance).ToArray();
        foreach (var node in roots) if (!CanInsertNode(node, target, out error)) { Log(error!); return false; }
        foreach (var owner in sourceParents.Append(actualParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance))
        {
            if (owner is null) continue;
            var proposed = new DesignControlNode { TypeName = owner.TypeName, Properties = owner.Properties };
            foreach (var child in owner.Children) if (!moving.Contains(child)) proposed.Children.Add(child);
            if (ReferenceEquals(owner, actualParent)) foreach (var node in roots) proposed.Children.Add(node);
            if (DesignPageContainers.GetChildError(proposed) is { } slotError) { Log(slotError); return false; }
        }
        var layout = new DesignerLayoutEngine().Layout(Document, PreviewResourcePolicy);
        var origin = actualParent is null ? default : layout.GetEffectiveBounds(actualParent);
        var plan = new List<(DesignControlNode Node, DesignBounds Bounds)>(roots.Count);
        foreach (var node in roots)
        {
            if (!TryTranslateBounds(layout.GetEffectiveBounds(node), -(long)origin.X, -(long)origin.Y, out var bounds))
            { Log("The destination coordinates exceed the supported range."); return false; }
            plan.Add((node, bounds));
        }
        if (actualParent is not null && DesignerSpecialContainers.IsTableLayoutPanel(actualParent))
        {
            // Admission borrows copies of root metadata only; no setters touch live nodes yet.
            var proposed = new DesignControlNode { TypeName = actualParent.TypeName, Properties = actualParent.Properties };
            foreach (var child in destination) if (!moving.Contains(child)) proposed.Children.Add(child);
            foreach (var node in roots)
            {
                var data = new DesignControlNode { TypeName = node.TypeName, Name = node.Name, Bounds = node.Bounds,
                    Properties = new(node.Properties, StringComparer.Ordinal), Children = node.Children };
                DesignerSpecialContainers.SetInt(data, DesignerSpecialContainers.TableColumnPropertyName, -1);
                DesignerSpecialContainers.SetInt(data, DesignerSpecialContainers.TableRowPropertyName, -1);
                proposed.Children.Add(data);
            }
            if (!DesignerTablePreviewAdmission.TryValidate([proposed], PreviewResourcePolicy, out error)) { Log(error!); return false; }
        }
        int parentDepth = actualParent is null ? -1 : EnumerateNodes().First(item => ReferenceEquals(item.Node, actualParent)).Depth;
        var depths = new Stack<(DesignControlNode Node, int Depth)>();
        foreach (var node in roots) depths.Push((node, parentDepth + 1));
        while (depths.TryPop(out var item))
        {
            if (item.Depth > DesignDocumentValidator.MaximumTreeDepth) { Log("The move exceeds the Designer depth budget."); return false; }
            foreach (var child in item.Node.Children) depths.Push((child, item.Depth + 1));
        }
        var selections = sourceParents.Select(owner => (Owner: owner, Selected: CapturePageSelection(owner))).ToArray();
        using var transaction = Transactions.Begin("Reparent selected controls");
        foreach (var (node, bounds) in plan)
        {
            TryFindNodeWithParent(node, Document.Controls, null, out _, out var source, out int index);
            Transactions.ExecuteChange(new DesignerTreeMoveChange(node, source, index, destination, destination.Count));
            SetNodeBounds(node, bounds);
            if (actualParent is not null && DesignerSpecialContainers.IsTableLayoutPanel(actualParent))
            {
                SetPropertyValue(node, DesignerSpecialContainers.TableColumnPropertyName, DesignPropertyValue.FromInt32(-1));
                SetPropertyValue(node, DesignerSpecialContainers.TableRowPropertyName, DesignPropertyValue.FromInt32(-1));
            }
        }
        foreach (var selection in selections) RestorePageSelection(selection.Owner, selection.Selected);
        transaction.Commit();
        return true;
    }

    private bool DeleteSelectedGroup(string description = "Delete selected controls")
    {
        ThrowIfDisposed();
        if (!TryGetSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        var removed = new HashSet<DesignControlNode>(roots, ReferenceEqualityComparer.Instance);
        var parents = roots.Select(FindParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance).ToArray();
        foreach (var parent in parents)
        {
            if (parent is null) continue;
            // Reuse the canonical data-only slot validator before removing any live children.
            // Ordinary containers have no page-slot constraint; page/shell containers retain it.
            var proposed = new DesignControlNode { TypeName = parent.TypeName, Properties = parent.Properties };
            foreach (var child in parent.Children)
                if (!removed.Contains(child)) proposed.Children.Add(child);
            if (DesignPageContainers.GetChildError(proposed) is { } childError) { Log(childError); return false; }
        }

        using var transaction = Transactions.Begin(description);
        // Descending authored order keeps sibling indices valid. Execute existing delta changes
        // directly, so intermediate single-node selection changes cannot escape to observers.
        for (int index = roots.Count - 1; index >= 0; index--)
        {
            var node = roots[index];
            if (!TryFindNodeWithParent(node, Document.Controls, null, out var parent, out var collection, out var position))
                return false; // Disposing the outer scope rolls back earlier removals.
            Transactions.ExecuteChange(new DesignerTreeRemoveChange(collection, node, position));
            UpdatePageSelectionAfterRemoval(parent, position);
        }
        var commonParent = parents.Length == 1 ? parents[0] : null;
        Host.Selection.Select(commonParent);
        transaction.Commit();
        Log($"Deleted {roots.Count} selected controls.");
        return true;
    }
}
