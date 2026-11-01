using ModernFormsNext.Designer.Clipboard;
using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

public sealed partial class DesignerSession
{
    private bool TryEncodeSelectedGroup(out string? content, out string? error)
    {
        content = null;
        if (!TryGetSelectedRoots(out var roots, out error)) return false;
        IReadOnlyList<DesignBounds>? bounds = null;
        if (roots.Select(FindParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance).Take(2).Count() > 1)
        {
            // Local coordinates from unrelated parents are incomparable. Borrow the canonical
            // preview layout once and copy document coordinates without changing authored nodes.
            var layout = new DesignerLayoutEngine().Layout(Document, PreviewResourcePolicy);
            bounds = roots.Select(layout.GetEffectiveBounds).ToArray();
        }
        return DesignerClipboardSerializer.TrySerializeSelection(roots, out content, out error, bounds);
    }

    private bool CopySelectedGroup()
    {
        if (!TryEncodeSelectedGroup(out var content, out var error)) { Log(error!); return false; }
        clipboard.SetContent(content!);
        Log("Copied selected controls.");
        return true;
    }

    private bool CutSelectedGroup()
    {
        if (!TryEncodeSelectedGroup(out var content, out var error)) { Log(error!); return false; }
        // As with a single cut, publication precedes mutation. Observer failure cannot lose data.
        var selected = DesignerSelectionSnapshot.Capture(Host.Selection);
        clipboard.SetContent(content!);
        if (!selected.Matches(Host.Selection))
        { Log("Selection changed during clipboard publication; the controls were copied without removing them."); return false; }
        return DeleteSelectedGroup("Cut selected controls");
    }

    private bool PasteGroup(IReadOnlyList<DesignControlNode> nodes)
    {
        if (!TryResolvePasteTarget(nodes[0], out var target, out var error)
            || !TryPrepareGroupInsertion(nodes, target, out error)) { Log(error!); return false; }
        using var transaction = Transactions.Begin("Paste selected controls");
        for (int index = 0; index < nodes.Count; index++)
            Transactions.ExecuteChange(new DesignerTreeInsertChange(target.Collection, nodes[index], target.Index + index));
        Host.Selection.SelectNodes(nodes);
        transaction.Commit();
        Log($"Pasted {nodes.Count} controls into {target.DisplayName}.");
        return true;
    }

    private bool TryPrepareGroupInsertion(IReadOnlyList<DesignControlNode> nodes, DesignerPasteTarget target,
        out string? error, HashSet<string>? usedNames = null)
    {
        if (target.Parent is { } owner && !ReferenceEquals(GetComponentBoundary(owner), owner))
        { error = "The paste destination is inside an atomic component."; return false; }
        foreach (var node in nodes)
        {
            if (!CanInsertNode(node, target, out error)
                || !DesignerControlReferenceGuard.CanReferenceTree(Document, node, CurrentProjectPath, out error)) return false;
        }
        if (target.Parent is { } parent)
        {
            var proposed = new DesignControlNode { TypeName = parent.TypeName, Properties = parent.Properties };
            foreach (var existing in target.Collection) proposed.Children.Add(existing);
            foreach (var node in nodes) proposed.Children.Add(node);
            if (DesignPageContainers.GetChildError(proposed) is { } childError) { error = childError; return false; }
        }
        usedNames ??= GetUsedControlNames();
        bool nudge = (target.Parent is null || !PreservesSequentialChildOrder(target.Parent))
            && nodes.All(node => DesignerSpecialContainers.GetEnum(node, "Dock", DockStyle.None) == DockStyle.None);
        foreach (var node in nodes)
        {
            RemapClipboardNames(node, null, usedNames);
            if (nudge)
            {
                if (!TryTranslateBounds(node.Bounds, 16, 16, out var bounds))
                { error = "The group offset exceeds the supported coordinate range."; return false; }
                node.Bounds = bounds;
            }
            if (target.Parent is not null && DesignerSpecialContainers.IsTableLayoutPanel(target.Parent))
            {
                // Keep copied spans; let the production table placer reserve them. Automatic
                // placement avoids a second Designer grid algorithm and duplicate first-free cells.
                DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnPropertyName, -1);
                DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowPropertyName, -1);
            }
        }
        if (target.Parent is { } table && DesignerSpecialContainers.IsTableLayoutPanel(table))
        {
            var proposed = new DesignControlNode { TypeName = table.TypeName, Properties = table.Properties };
            foreach (var child in table.Children) proposed.Children.Add(child);
            foreach (var node in nodes) proposed.Children.Add(node);
            if (!DesignerTablePreviewAdmission.TryValidate([proposed], PreviewResourcePolicy, out error)) return false;
        }
        return ValidateClipboardAdmission(nodes, target.Parent, out error);
    }

    private bool ValidateClipboardAdmission(IReadOnlyList<DesignControlNode> additions, DesignControlNode? parent,
        out string? error)
    {
        // Borrow the existing graph and additions for aggregate budgets, without runtime controls
        // or a serialized model snapshot. Ownership and page-slot checks are performed separately.
        var proposed = new DesignDocument
        {
            Namespace = Document.Namespace, ClassName = Document.ClassName, FormName = Document.FormName,
            RootKind = Document.RootKind, Size = Document.Size, Metadata = Document.Metadata,
            Properties = Document.Properties, Events = Document.Events
        };
        foreach (var node in Document.Controls) proposed.Controls.Add(node);
        foreach (var node in additions) proposed.Controls.Add(node);
        var validation = new DesignDocumentValidator().ValidateStructure(proposed);
        if (!validation.IsValid) { error = string.Join(Environment.NewLine, validation.Errors); return false; }
        int baseDepth = parent is null ? 0 : EnumerateNodes().First(item => ReferenceEquals(item.Node, parent)).Depth + 1;
        var pending = new Stack<(DesignControlNode Node, int Depth)>();
        foreach (var node in additions) pending.Push((node, baseDepth));
        while (pending.TryPop(out var item))
        {
            if (item.Depth > DesignDocumentValidator.MaximumTreeDepth)
            { error = "Pasting here exceeds the Designer control-depth budget."; return false; }
            foreach (var child in item.Node.Children) pending.Push((child, item.Depth + 1));
        }
        error = null;
        return true;
    }

    private bool TryPrepareGroupDuplicates(out List<(DesignControlNode Node, DesignerPasteTarget Target)> plan,
        out string? error)
    {
        plan = [];
        if (!TryGetSelectedRoots(out var roots, out error)
            || !DesignerClipboardSerializer.TrySerializeSelection(roots, out var content, out error)
            || !DesignerClipboardSerializer.TryDeserializeSelection(content, out var clones, out error)) return false;
        var usedNames = GetUsedControlNames();
        var groups = roots.Select((node, index) => (Source: node, Copy: clones![index]))
            .GroupBy(pair => FindParent(pair.Source));
        foreach (var group in groups)
        {
            var collection = group.Key?.Children ?? Document.Controls;
            int index = group.Max(pair => collection.IndexOf(pair.Source)) + 1;
            var target = new DesignerPasteTarget(collection, group.Key, group.Key?.Name ?? Document.FormName, index);
            var copies = group.Select(pair => pair.Copy).ToArray();
            if (!TryPrepareGroupInsertion(copies, target, out error, usedNames)) return false;
            foreach (var copy in copies) plan.Add((copy, target));
        }
        // Separate destination batches still share one document admission budget.
        return ValidateClipboardAdmission(plan.Select(item => item.Node).ToArray(), null, out error);
    }

    private bool DuplicateSelectedGroup()
    {
        if (!TryPrepareGroupDuplicates(out var plan, out var error)) { Log(error!); return false; }
        using var transaction = Transactions.Begin("Duplicate selected controls");
        var offsets = new Dictionary<DesignControlCollection, int>();
        foreach (var (node, target) in plan)
        {
            offsets.TryGetValue(target.Collection, out int offset);
            Transactions.ExecuteChange(new DesignerTreeInsertChange(target.Collection, node, target.Index + offset));
            offsets[target.Collection] = offset + 1;
        }
        Host.Selection.SelectNodes(plan.Select(item => item.Node));
        transaction.Commit();
        Log($"Duplicated {plan.Count} controls.");
        return true;
    }

    private static bool TryTranslateBounds(DesignBounds source, long deltaX, long deltaY, out DesignBounds result)
    {
        long x = source.X + deltaX, y = source.Y + deltaY;
        if (source.Width < 0 || source.Height < 0 || x < int.MinValue || y < int.MinValue
            || x + source.Width > int.MaxValue || y + source.Height > int.MaxValue)
        { result = default; return false; }
        result = new DesignBounds((int)x, (int)y, source.Width, source.Height);
        return true;
    }
}
