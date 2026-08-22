using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.History;

/// <summary>
/// Captures only the model fields owned by a known Designer mutation target.
/// </summary>
/// <remarks>
/// This is not a serialized document snapshot. Ordinary Property Grid edits capture one root or
/// node plus, only for collection editors, its immediate child references. Root resize is the
/// deliberate complex-operation exception: it captures direct state for every existing node
/// because the production layout engine persists Anchor-derived bounds throughout the tree.
/// </remarks>
internal sealed class DesignerModelMutationSnapshot
{
    private readonly RootSnapshot? root;
    private readonly IReadOnlyList<NodeSnapshot> nodes;

    private DesignerModelMutationSnapshot(RootSnapshot? root, IReadOnlyList<NodeSnapshot> nodes)
    {
        this.root = root;
        this.nodes = nodes;
    }

    public static DesignerModelMutationSnapshot CaptureSelected(
        DesignerSession session,
        bool includeImmediateChildren = false,
        bool includeDescendantState = false)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (includeDescendantState)
            return CaptureDocumentLayout(session.Document);

        return session.SelectedNode is { } node
            ? new DesignerModelMutationSnapshot(
                root: null,
                [NodeSnapshot.Capture(node, includeImmediateChildren)])
            : new DesignerModelMutationSnapshot(
                RootSnapshot.Capture(session.Document, includeImmediateChildren),
                []);
    }

    public static DesignerModelMutationSnapshot CaptureNode(DesignControlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new DesignerModelMutationSnapshot(root: null, [NodeSnapshot.Capture(node, includeChildren: false)]);
    }

    public static DesignerModelMutationSnapshot CaptureDocumentLayout(DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var snapshots = new List<NodeSnapshot>();
        CaptureNodes(document.Controls, snapshots);
        return new DesignerModelMutationSnapshot(
            RootSnapshot.Capture(document, includeControls: true),
            snapshots);
    }

    public void RecordChanges(DesignerTransactionManager transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        root?.RecordChanges(transactions);
        foreach (var node in nodes)
            node.RecordChanges(transactions);
    }

    private static void CaptureNodes(DesignControlCollection controls, ICollection<NodeSnapshot> snapshots)
    {
        foreach (var node in controls)
        {
            snapshots.Add(NodeSnapshot.Capture(node, includeChildren: true));
            CaptureNodes(node.Children, snapshots);
        }
    }

    private sealed class RootSnapshot
    {
        private readonly DesignDocument document;
        private readonly string namespaceName;
        private readonly string className;
        private readonly DesignRootKind rootKind;
        private readonly string formName;
        private readonly DesignSize size;
        private readonly Dictionary<string, DesignPropertyValue> properties;
        private readonly Dictionary<string, string?> events;
        private readonly IReadOnlyList<DesignControlNode>? controls;

        private RootSnapshot(DesignDocument document, bool includeControls)
        {
            this.document = document;
            namespaceName = document.Namespace;
            className = document.ClassName;
            rootKind = document.RootKind;
            formName = document.FormName;
            size = document.Size;
            properties = new Dictionary<string, DesignPropertyValue>(document.Properties, StringComparer.Ordinal);
            events = new Dictionary<string, string?>(document.Events, StringComparer.Ordinal);
            controls = includeControls ? document.Controls.ToArray() : null;
        }

        public static RootSnapshot Capture(DesignDocument document, bool includeControls)
            => new(document, includeControls);

        public void RecordChanges(DesignerTransactionManager transactions)
        {
            transactions.RecordAppliedChange(new DesignerRootValueChange(document, DesignerRootValueKind.Namespace, namespaceName, document.Namespace));
            transactions.RecordAppliedChange(new DesignerRootValueChange(document, DesignerRootValueKind.ClassName, className, document.ClassName));
            transactions.RecordAppliedChange(new DesignerRootValueChange(document, DesignerRootValueKind.RootKind, rootKind, document.RootKind));
            transactions.RecordAppliedChange(new DesignerRootValueChange(document, DesignerRootValueKind.FormName, formName, document.FormName));
            transactions.RecordAppliedChange(new DesignerRootValueChange(document, DesignerRootValueKind.Size, size, document.Size));
            RecordPropertyChanges(transactions, document.Properties, properties);
            RecordEventChanges(transactions, document.Events, events);

            if (controls is not null)
            {
                transactions.RecordAppliedChange(new DesignerChildrenReplaceChange(
                    document.Controls,
                    controls,
                    document.Controls.ToArray()));
            }
        }
    }

    private sealed class NodeSnapshot
    {
        private readonly DesignControlNode node;
        private readonly string typeName;
        private readonly string name;
        private readonly DesignBounds bounds;
        private readonly DesignerMemberVisibility memberVisibility;
        private readonly Dictionary<string, DesignPropertyValue> properties;
        private readonly Dictionary<string, string?> events;
        private readonly IReadOnlyList<DesignControlNode>? children;

        private NodeSnapshot(DesignControlNode node, bool includeChildren)
        {
            this.node = node;
            typeName = node.TypeName;
            name = node.Name;
            bounds = node.Bounds;
            memberVisibility = node.MemberVisibility;
            properties = new Dictionary<string, DesignPropertyValue>(node.Properties, StringComparer.Ordinal);
            events = new Dictionary<string, string?>(node.Events, StringComparer.Ordinal);
            children = includeChildren ? node.Children.ToArray() : null;
        }

        public static NodeSnapshot Capture(DesignControlNode node, bool includeChildren)
            => new(node, includeChildren);

        public void RecordChanges(DesignerTransactionManager transactions)
        {
            transactions.RecordAppliedChange(new DesignerNodeValueChange(node, DesignerNodeValueKind.TypeName, typeName, node.TypeName));
            transactions.RecordAppliedChange(new DesignerNodeValueChange(node, DesignerNodeValueKind.Name, name, node.Name));
            transactions.RecordAppliedChange(new DesignerNodeValueChange(node, DesignerNodeValueKind.Bounds, bounds, node.Bounds));
            transactions.RecordAppliedChange(new DesignerNodeValueChange(node, DesignerNodeValueKind.MemberVisibility, memberVisibility, node.MemberVisibility));
            RecordPropertyChanges(transactions, node.Properties, properties);
            RecordEventChanges(transactions, node.Events, events);

            if (children is not null)
            {
                transactions.RecordAppliedChange(new DesignerChildrenReplaceChange(
                    node.Children,
                    children,
                    node.Children.ToArray()));
            }
        }
    }

    private static void RecordPropertyChanges(
        DesignerTransactionManager transactions,
        IDictionary<string, DesignPropertyValue> current,
        IReadOnlyDictionary<string, DesignPropertyValue> before)
    {
        foreach (var name in before.Keys.Concat(current.Keys).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            var existedBefore = before.TryGetValue(name, out var beforeValue);
            var existsAfter = current.TryGetValue(name, out var afterValue);
            transactions.RecordAppliedChange(new DesignerPropertyDictionaryChange(
                current,
                name,
                existedBefore,
                beforeValue,
                existsAfter,
                afterValue));
        }
    }

    private static void RecordEventChanges(
        DesignerTransactionManager transactions,
        IDictionary<string, string?> current,
        IReadOnlyDictionary<string, string?> before)
    {
        foreach (var name in before.Keys.Concat(current.Keys).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            var existedBefore = before.TryGetValue(name, out var beforeValue);
            var existsAfter = current.TryGetValue(name, out var afterValue);
            transactions.RecordAppliedChange(new DesignerEventDictionaryChange(
                current,
                name,
                existedBefore,
                beforeValue,
                existsAfter,
                afterValue));
        }
    }
}
