using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.History;

internal enum DesignerRootValueKind
{
    Namespace,
    ClassName,
    RootKind,
    FormName,
    Size
}

internal enum DesignerNodeValueKind
{
    TypeName,
    Name,
    Bounds,
    MemberVisibility
}

internal sealed class DesignerRootValueChange : IDesignerChange
{
    private readonly DesignDocument document;
    private readonly DesignerRootValueKind kind;
    private readonly object before;
    private object after;

    public DesignerRootValueChange(DesignDocument document, DesignerRootValueKind kind, object before, object after)
    {
        this.document = document;
        this.kind = kind;
        this.before = before;
        this.after = after;
    }

    public bool IsEmpty => Equals(before, after);

    public void Apply() => Set(after);

    public void Revert() => Set(before);

    public bool TryMerge(IDesignerChange subsequentChange)
    {
        if (subsequentChange is not DesignerRootValueChange next
            || !ReferenceEquals(document, next.document)
            || kind != next.kind)
        {
            return false;
        }

        after = next.after;
        return true;
    }

    private void Set(object value)
    {
        switch (kind)
        {
            case DesignerRootValueKind.Namespace:
                document.Namespace = (string)value;
                break;
            case DesignerRootValueKind.ClassName:
                document.ClassName = (string)value;
                break;
            case DesignerRootValueKind.RootKind:
                document.RootKind = (DesignRootKind)value;
                break;
            case DesignerRootValueKind.FormName:
                document.FormName = (string)value;
                break;
            case DesignerRootValueKind.Size:
                document.Size = (DesignSize)value;
                break;
            default:
                throw new InvalidOperationException($"Unsupported Designer root value kind '{kind}'.");
        }
    }
}

internal sealed class DesignerNodeValueChange : IDesignerChange
{
    private readonly DesignControlNode node;
    private readonly DesignerNodeValueKind kind;
    private readonly object before;
    private object after;

    public DesignerNodeValueChange(DesignControlNode node, DesignerNodeValueKind kind, object before, object after)
    {
        this.node = node;
        this.kind = kind;
        this.before = before;
        this.after = after;
    }

    public bool IsEmpty => Equals(before, after);

    public void Apply() => Set(after);

    public void Revert() => Set(before);

    public bool TryMerge(IDesignerChange subsequentChange)
    {
        if (subsequentChange is not DesignerNodeValueChange next
            || !ReferenceEquals(node, next.node)
            || kind != next.kind)
        {
            return false;
        }

        after = next.after;
        return true;
    }

    private void Set(object value)
    {
        switch (kind)
        {
            case DesignerNodeValueKind.TypeName:
                node.TypeName = (string)value;
                break;
            case DesignerNodeValueKind.Name:
                node.Name = (string)value;
                break;
            case DesignerNodeValueKind.Bounds:
                node.Bounds = (DesignBounds)value;
                break;
            case DesignerNodeValueKind.MemberVisibility:
                node.MemberVisibility = (DesignerMemberVisibility)value;
                break;
            default:
                throw new InvalidOperationException($"Unsupported Designer node value kind '{kind}'.");
        }
    }
}

internal sealed class DesignerPropertyDictionaryChange : IDesignerChange
{
    private readonly IDictionary<string, DesignPropertyValue> properties;
    private readonly string name;
    private readonly bool existedBefore;
    private readonly DesignPropertyValue? before;
    private bool existsAfter;
    private DesignPropertyValue? after;

    public DesignerPropertyDictionaryChange(
        IDictionary<string, DesignPropertyValue> properties,
        string name,
        bool existedBefore,
        DesignPropertyValue? before,
        bool existsAfter,
        DesignPropertyValue? after)
    {
        this.properties = properties;
        this.name = name;
        this.existedBefore = existedBefore;
        this.before = before;
        this.existsAfter = existsAfter;
        this.after = after;
    }

    public bool IsEmpty
        => existedBefore == existsAfter
            && (!existedBefore || DesignerPropertyValueComparer.Equals(before, after));

    public void Apply() => Set(existsAfter, after);

    public void Revert() => Set(existedBefore, before);

    public bool TryMerge(IDesignerChange subsequentChange)
    {
        if (subsequentChange is not DesignerPropertyDictionaryChange next
            || !ReferenceEquals(properties, next.properties)
            || !string.Equals(name, next.name, StringComparison.Ordinal))
        {
            return false;
        }

        existsAfter = next.existsAfter;
        after = next.after;
        return true;
    }

    private void Set(bool exists, DesignPropertyValue? value)
    {
        if (exists)
            properties[name] = value ?? DesignPropertyValue.FromNull();
        else
            properties.Remove(name);
    }
}

internal sealed class DesignerEventDictionaryChange : IDesignerChange
{
    private readonly IDictionary<string, string?> events;
    private readonly string name;
    private readonly bool existedBefore;
    private readonly string? before;
    private bool existsAfter;
    private string? after;

    public DesignerEventDictionaryChange(
        IDictionary<string, string?> events,
        string name,
        bool existedBefore,
        string? before,
        bool existsAfter,
        string? after)
    {
        this.events = events;
        this.name = name;
        this.existedBefore = existedBefore;
        this.before = before;
        this.existsAfter = existsAfter;
        this.after = after;
    }

    public bool IsEmpty => existedBefore == existsAfter && (!existedBefore || string.Equals(before, after, StringComparison.Ordinal));

    public void Apply() => Set(existsAfter, after);

    public void Revert() => Set(existedBefore, before);

    public bool TryMerge(IDesignerChange subsequentChange)
    {
        if (subsequentChange is not DesignerEventDictionaryChange next
            || !ReferenceEquals(events, next.events)
            || !string.Equals(name, next.name, StringComparison.Ordinal))
        {
            return false;
        }

        existsAfter = next.existsAfter;
        after = next.after;
        return true;
    }

    private void Set(bool exists, string? value)
    {
        if (exists)
            events[name] = value;
        else
            events.Remove(name);
    }
}

internal sealed class DesignerTreeInsertChange : IDesignerChange
{
    private readonly DesignControlCollection collection;
    private readonly DesignControlNode node;
    private readonly int index;

    public DesignerTreeInsertChange(DesignControlCollection collection, DesignControlNode node, int index)
    {
        this.collection = collection;
        this.node = node;
        this.index = index;
    }

    public bool IsEmpty => false;

    public void Apply()
    {
        if (collection.Contains(node))
            throw new InvalidOperationException($"Designer node '{node.Name}' is already attached to the target collection.");

        collection.Insert(Math.Clamp(index, 0, collection.Count), node);
    }

    public void Revert()
        => RemoveExpected(collection, node);

    public bool TryMerge(IDesignerChange subsequentChange) => false;

    internal static void RemoveExpected(DesignControlCollection collection, DesignControlNode node)
    {
        var currentIndex = collection.IndexOf(node);
        if (currentIndex < 0)
            throw new InvalidOperationException($"Designer node '{node.Name}' is not attached to the expected collection.");

        collection.RemoveAt(currentIndex);
    }
}

internal sealed class DesignerTreeRemoveChange : IDesignerChange
{
    private readonly DesignControlCollection collection;
    private readonly DesignControlNode node;
    private readonly int index;

    public DesignerTreeRemoveChange(DesignControlCollection collection, DesignControlNode node, int index)
    {
        this.collection = collection;
        this.node = node;
        this.index = index;
    }

    public bool IsEmpty => false;

    public void Apply() => DesignerTreeInsertChange.RemoveExpected(collection, node);

    public void Revert()
    {
        if (collection.Contains(node))
            throw new InvalidOperationException($"Designer node '{node.Name}' is already attached while restoring a deletion.");

        collection.Insert(Math.Clamp(index, 0, collection.Count), node);
    }

    public bool TryMerge(IDesignerChange subsequentChange) => false;
}

internal sealed class DesignerTreeMoveChange : IDesignerChange
{
    private readonly DesignControlNode node;
    private readonly DesignControlCollection source;
    private readonly int sourceIndex;
    private readonly DesignControlCollection destination;
    private readonly int destinationIndex;

    public DesignerTreeMoveChange(
        DesignControlNode node,
        DesignControlCollection source,
        int sourceIndex,
        DesignControlCollection destination,
        int destinationIndex)
    {
        this.node = node;
        this.source = source;
        this.sourceIndex = sourceIndex;
        this.destination = destination;
        this.destinationIndex = destinationIndex;
    }

    public bool IsEmpty => ReferenceEquals(source, destination) && sourceIndex == destinationIndex;

    public void Apply()
    {
        DesignerTreeInsertChange.RemoveExpected(source, node);
        try
        {
            destination.Insert(Math.Clamp(destinationIndex, 0, destination.Count), node);
        }
        catch
        {
            source.Insert(Math.Clamp(sourceIndex, 0, source.Count), node);
            throw;
        }
    }

    public void Revert()
    {
        DesignerTreeInsertChange.RemoveExpected(destination, node);
        source.Insert(Math.Clamp(sourceIndex, 0, source.Count), node);
    }

    public bool TryMerge(IDesignerChange subsequentChange) => false;
}

internal sealed class DesignerChildrenReplaceChange : IDesignerChange
{
    private readonly DesignControlCollection collection;
    private readonly IReadOnlyList<DesignControlNode> before;
    private IReadOnlyList<DesignControlNode> after;

    public DesignerChildrenReplaceChange(
        DesignControlCollection collection,
        IReadOnlyList<DesignControlNode> before,
        IReadOnlyList<DesignControlNode> after)
    {
        this.collection = collection;
        this.before = before;
        this.after = after;
    }

    public bool IsEmpty => SequenceReferenceEquals(before, after);

    public void Apply() => Replace(after);

    public void Revert() => Replace(before);

    public bool TryMerge(IDesignerChange subsequentChange)
    {
        if (subsequentChange is not DesignerChildrenReplaceChange next
            || !ReferenceEquals(collection, next.collection))
        {
            return false;
        }

        after = next.after;
        return true;
    }

    internal static bool SequenceReferenceEquals(
        IReadOnlyList<DesignControlNode> first,
        IReadOnlyList<DesignControlNode> second)
    {
        if (first.Count != second.Count)
            return false;

        for (var index = 0; index < first.Count; index++)
        {
            if (!ReferenceEquals(first[index], second[index]))
                return false;
        }

        return true;
    }

    private void Replace(IReadOnlyList<DesignControlNode> nodes)
    {
        var previous = collection.ToArray();
        collection.Clear();
        try
        {
            foreach (var node in nodes)
                collection.Add(node);
        }
        catch
        {
            collection.Clear();
            foreach (var node in previous)
                collection.Add(node);
            throw;
        }
    }
}

internal sealed class DesignerDocumentReplaceChange : IDesignerChange
{
    private readonly DesignerHost host;
    private readonly DesignDocument before;
    private readonly DesignDocument after;

    public DesignerDocumentReplaceChange(DesignerHost host, DesignDocument before, DesignDocument after)
    {
        this.host = host;
        this.before = before;
        this.after = after;
    }

    public bool IsEmpty => ReferenceEquals(before, after);

    public void Apply() => Replace(after);

    public void Revert() => Replace(before);

    public bool TryMerge(IDesignerChange subsequentChange) => false;

    private void Replace(DesignDocument document)
    {
        var previousDocument = host.Document;
        var previousSelection = host.Selection.SelectedNode;

        try
        {
            host.LoadDocument(document);
        }
        catch
        {
            // LoadDocument updates the document and selection before notifying selection
            // observers. Restore both fields when such an observer fails so this individual
            // change remains atomic and the transaction scope has nothing partial to unwind.
            host.Document = previousDocument;
            try
            {
                host.Selection.Select(previousSelection);
            }
            catch
            {
                // Select updates its state before raising the event. A second observer failure
                // must not replace the original exception or prevent the model restoration.
            }

            throw;
        }
    }
}

internal static class DesignerPropertyValueComparer
{
    public static bool Equals(DesignPropertyValue? first, DesignPropertyValue? second)
    {
        if (ReferenceEquals(first, second))
            return true;
        if (first is null || second is null
            || first.Kind != second.Kind
            || !object.Equals(first.Value, second.Value)
            || !string.Equals(first.EnumTypeName, second.EnumTypeName, StringComparison.Ordinal)
            || !string.Equals(first.ObjectTypeName, second.ObjectTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        var firstProperties = first.ObjectProperties;
        var secondProperties = second.ObjectProperties;
        if (firstProperties is null || secondProperties is null)
            return firstProperties is null && secondProperties is null;
        if (firstProperties.Count != secondProperties.Count)
            return false;

        foreach (var property in firstProperties)
        {
            if (!secondProperties.TryGetValue(property.Key, out var secondValue)
                || !Equals(property.Value, secondValue))
            {
                return false;
            }
        }

        return true;
    }
}
