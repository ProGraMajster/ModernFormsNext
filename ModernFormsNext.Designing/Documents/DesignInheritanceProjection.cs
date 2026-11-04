using System.Text.Json;

namespace ModernFormsNext.Designing;

// Inherited nodes participate in the existing design tree while open. Persistence
// and generation share this projection, which retains only locally owned changes.
internal static class DesignInheritanceProjection
{
    internal static bool IsStructuralProperty(string name)
        => name is "Name" or "MemberVisibility" or "Children" or "Controls" or "TabPages" or "Pages"
            or "Columns" or "Items" or "Nodes" or "InteractionEffects" or "RowStyles" or "ColumnStyles"
            or "TableColumn" or "TableRow" or "TableColumnSpan" or "TableRowSpan";

    internal static SortedDictionary<string, DesignPropertyValue> CloneProperties(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => new(properties.ToDictionary(pair => pair.Key, pair => CloneValue(pair.Value), StringComparer.Ordinal), StringComparer.Ordinal);

    private static DesignPropertyValue CloneValue(DesignPropertyValue value)
        => new(value.Kind, value.Value, value.EnumTypeName, value.ObjectTypeName,
            value.ObjectProperties is null ? null : CloneProperties(value.ObjectProperties));

    internal static bool PreservesSequence(DesignControlNode node)
        => DesignPageContainers.IsType(node.TypeName, "FlowLayoutPanel")
            || DesignPageContainers.IsType(node.TypeName, "TableLayoutPanel")
            || DesignPageContainers.IsType(node.TypeName, "TabControl")
            || DesignPageContainers.IsType(node.TypeName, "TabbedPage");

    internal static DesignDocument CloneHydrated(DesignDocument document)
        => new()
        {
            Metadata = document.Metadata, Namespace = document.Namespace, ClassName = document.ClassName,
            RootKind = document.RootKind, BaseTypeName = document.BaseTypeName, InheritedSize = document.InheritedSize,
            FormName = document.FormName, Size = document.Size,
            Properties = CloneProperties(document.Properties),
            Events = new(document.Events, StringComparer.Ordinal),
            Controls = [.. document.Controls.Select(CloneHydratedNode)],
            InheritanceState = document.InheritanceState, InheritanceError = document.InheritanceError
        };

    private static DesignControlNode CloneHydratedNode(DesignControlNode node)
        => new()
        {
            Name = node.Name, TypeName = node.TypeName, Bounds = node.Bounds, MemberVisibility = node.MemberVisibility,
            InheritedFrom = node.InheritedFrom, InheritedBounds = node.InheritedBounds, InheritanceState = node.InheritanceState,
            Properties = CloneProperties(node.Properties), Events = new(node.Events, StringComparer.Ordinal),
            Children = [.. node.Children.Select(CloneHydratedNode)]
        };

    internal static DesignDocument Authored(DesignDocument document)
    {
        if (document.BaseTypeName is null && document.InheritanceState is null)
            return document;

        var state = document.InheritanceState;
        if (state is not null && (document.BaseTypeName != state.BaseTypeName || document.Namespace != state.Namespace
            || document.ClassName != state.ClassName || document.RootKind != state.RootKind))
            throw new InvalidOperationException("Reopen the declaring source before changing an inherited design's type identity.");
        if (state is not null && !document.Controls.Where(node => node.IsInherited)
            .Select(node => node.Name).SequenceEqual(state.ChildNames))
            throw new InvalidOperationException("The inherited root structure must be edited in its declaring design.");
        var authored = new DesignDocument
        {
            Metadata = document.Metadata,
            Namespace = document.Namespace,
            ClassName = document.ClassName,
            RootKind = document.RootKind,
            BaseTypeName = document.BaseTypeName,
            FormName = document.FormName,
            Size = document.Size,
            InheritedSize = state is null ? document.InheritedSize
                : document.Size == state.Size ? null : document.Size,
            Properties = Difference(document.Properties, state?.Properties),
            Events = Difference(document.Events, state?.Events)
        };
        RequireOwnershipOrder(document.Controls, parent: null);
        foreach (var node in document.Controls)
            if (AuthoredNode(node, parent: null) is { } child)
                authored.Controls.Add(child);
        return authored;
    }

    internal static void RequireOwnershipOrder(IEnumerable<DesignControlNode> nodes, DesignControlNode? parent)
    {
        bool sequential = parent is not null && PreservesSequence(parent);
        bool boundarySeen = false;
        foreach (var node in nodes)
        {
            bool firstGroup = sequential ? node.IsInherited : !node.IsInherited;
            if (!firstGroup) boundarySeen = true;
            else if (boundarySeen)
                throw new InvalidOperationException("Local controls must preserve the inherited container's append and Z-order rules.");
            RequireOwnershipOrder(node.Children, node);
        }
    }

    private static DesignControlNode? AuthoredNode(DesignControlNode node, string? parent)
    {
        var state = node.InheritanceState;
        if (state is not null && (node.Name != state.Name || node.TypeName != state.TypeName
            || node.InheritedFrom != state.DeclaringType || node.MemberVisibility != state.Visibility
            || parent != state.ParentName
            || !node.Children.Where(child => child.IsInherited).Select(child => child.Name).SequenceEqual(state.ChildNames)))
            throw new InvalidOperationException("The inherited control structure must be edited in its declaring design.");

        var projected = new DesignControlNode
        {
            Name = node.Name,
            TypeName = node.TypeName,
            MemberVisibility = node.MemberVisibility,
            Bounds = node.Bounds,
            InheritedFrom = node.InheritedFrom,
            InheritedBounds = state is null ? node.InheritedBounds : node.Bounds == state.Bounds ? null : node.Bounds,
            Properties = Difference(node.Properties, state?.Properties),
            Events = Difference(node.Events, state?.Events)
        };
        foreach (var child in node.Children)
            if (AuthoredNode(child, node.Name) is { } authoredChild)
                projected.Children.Add(authoredChild);

        if (!node.IsInherited)
            return projected;
        if (state is null)
            return projected; // Unresolved authored patches remain lossless.
        bool changed = projected.InheritedBounds is not null || projected.Properties.Count != 0
            || projected.Events.Count != 0 || projected.Children.Count != 0;
        if (projected.Properties.Keys.Any(IsStructuralProperty))
            throw new InvalidOperationException($"Inherited collections and identity on '{node.Name}' belong to the declaring design.");
        if (changed && !state.CanOverride)
            throw new InvalidOperationException($"Inherited control '{node.Name}' is locked by its declaring metadata.");
        return changed ? projected : null;
    }

    private static SortedDictionary<string, T> Difference<T>(
        IReadOnlyDictionary<string, T> current, IReadOnlyDictionary<string, T>? baseline)
    {
        var result = new SortedDictionary<string, T>(StringComparer.Ordinal);
        if (baseline is not null && baseline.Keys.Any(key => !current.ContainsKey(key)))
            throw new InvalidOperationException("Inherited values must be reset through their declaring design.");
        foreach (var item in current)
            if (baseline is null || !baseline.TryGetValue(item.Key, out var original) || !Equal(item.Value, original))
                result.Add(item.Key, item.Value);
        return result;
    }

    private static bool Equal<T>(T left, T right)
        => left is DesignPropertyValue value && right is DesignPropertyValue other
            ? JsonSerializer.Serialize(value, DesignDocumentSerializer.Default.Options)
                == JsonSerializer.Serialize(other, DesignDocumentSerializer.Default.Options)
            : EqualityComparer<T>.Default.Equals(left, right);
}

internal sealed record DesignRootInheritanceState(
    string BaseTypeName,
    string Namespace,
    string ClassName,
    DesignRootKind RootKind,
    DesignSize Size,
    IReadOnlyDictionary<string, DesignPropertyValue> Properties,
    IReadOnlyDictionary<string, string?> Events,
    IReadOnlyList<string> ChildNames);

internal sealed record DesignNodeInheritanceState(
    string DeclaringType,
    string Name,
    string TypeName,
    string? ParentName,
    DesignBounds Bounds,
    DesignerMemberVisibility Visibility,
    bool CanOverride,
    IReadOnlyDictionary<string, DesignPropertyValue> Properties,
    IReadOnlyDictionary<string, string?> Events,
    IReadOnlyList<string> ChildNames);
