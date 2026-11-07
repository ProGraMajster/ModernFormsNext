using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

// The generated projection supplies the common vocabulary. Compare its values to the
// reviewed source, then overlay only differences onto the authoritative detached copy.
internal static class DesignerReverseReconciliation
{
    internal static bool TryApply(DesignDocument current, DesignDocument baseline, DesignDocument incoming,
        out DesignDocument updated, List<string>? changes = null)
    {
        updated = null!;
        if (baseline.Namespace != incoming.Namespace || baseline.ClassName != incoming.ClassName ||
            baseline.RootKind != incoming.RootKind || current.BaseTypeName is not null || incoming.BaseTypeName is not null)
            return false;

        var draft = DesignInheritanceProjection.CloneHydrated(current);
        if (!Overlay(baseline.Properties, incoming.Properties, draft.Properties, SameValue, "Design", changes) ||
            !Overlay(baseline.Events, incoming.Events, draft.Events, StringComparer.Ordinal.Equals, "Design events", changes) ||
            !ApplyNodes(baseline.Controls, incoming.Controls, draft.Controls, changes))
            return false;

        if (baseline.FormName != incoming.FormName)
        {
            draft.FormName = incoming.FormName;
            Describe(changes, "Design.Name");
        }
        if (baseline.Size != incoming.Size)
        {
            draft.Size = incoming.Size;
            Describe(changes, "Design.Size");
        }
        if (!new DesignDocumentValidator().ValidateStructure(draft).IsValid) return false;
        updated = draft;
        return true;
    }

    private static bool ApplyNodes(IReadOnlyList<DesignControlNode> baseline,
        IReadOnlyList<DesignControlNode> incoming, IReadOnlyList<DesignControlNode> draft, List<string>? changes)
    {
        if (baseline.Count != incoming.Count || baseline.Count != draft.Count) return false;
        for (int i = 0; i < baseline.Count; i++)
        {
            var old = baseline[i];
            var proposed = incoming[i];
            var target = draft[i];
            if (old.Name != proposed.Name || old.Name != target.Name || old.TypeName != proposed.TypeName ||
                old.MemberVisibility != proposed.MemberVisibility || proposed.IsInherited ||
                !Overlay(old.Properties, proposed.Properties, target.Properties, SameValue, target.Name, changes) ||
                !Overlay(old.Events, proposed.Events, target.Events, StringComparer.Ordinal.Equals, target.Name + " events", changes) ||
                !ApplyNodes(old.Children, proposed.Children, target.Children, changes))
                return false;
            if (old.Bounds != proposed.Bounds)
            {
                target.Bounds = proposed.Bounds;
                Describe(changes, target.Name + ".Bounds");
            }
        }
        return true;
    }

    private static bool Overlay<T>(IReadOnlyDictionary<string, T> baseline, IReadOnlyDictionary<string, T> incoming,
        IDictionary<string, T> draft, Func<T, T, bool> equal, string owner, List<string>? changes)
    {
        // Absence is not permission to erase existing model data.
        if (baseline.Keys.Any(key => !incoming.ContainsKey(key))) return false;
        foreach (var (key, value) in incoming)
            if (!baseline.TryGetValue(key, out var old) || !equal(old, value))
            {
                draft[key] = value;
                if (changes is not null) Describe(changes, owner + "." + key);
            }
        return true;
    }

    private static void Describe(List<string>? changes, string name)
    {
        if (changes is not { Count: < 40 }) return;
        changes.Add(name.Length <= 180 ? name : name[..180]);
    }

    private static bool SameValue(DesignPropertyValue left, DesignPropertyValue right)
    {
        if (left.Kind != right.Kind || !Equals(left.Value, right.Value) || left.EnumTypeName != right.EnumTypeName ||
            left.ObjectTypeName != right.ObjectTypeName) return false;
        if (left.ObjectProperties is not { } children)
            return right.ObjectProperties is null;
        return right.ObjectProperties is { } other && children.Count == other.Count &&
            children.All(pair => other.TryGetValue(pair.Key, out var value) && SameValue(pair.Value, value));
    }
}
