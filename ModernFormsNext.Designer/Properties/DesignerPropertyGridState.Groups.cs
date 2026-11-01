using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed partial class DesignerPropertyGridState
{
    private IReadOnlyList<DesignerPropertyDescriptor> BuildSharedPropertyDescriptors()
    {
        if (!playgroundState.CanEditSelectedProperties(out _)) return [];
        var sets = new List<IReadOnlyList<DesignerPropertyDescriptor>>();
        foreach (var node in playgroundState.SelectedNodes)
        {
            var descriptors = new List<DesignerPropertyDescriptor>();
            AddNodeDescriptors(descriptors, node);
            AddMetadataDescriptors(descriptors, node);
            sets.Add(descriptors.Where(property => property.IsVisible).ToArray());
        }
        return IntersectDescriptors(sets);
    }

    private static IReadOnlyList<DesignerPropertyDescriptor> IntersectDescriptors(
        IReadOnlyList<IReadOnlyList<DesignerPropertyDescriptor>> sets)
    {
        var maps = sets.Select(set => set.GroupBy(property => property.Identity)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)).ToArray();
        var shared = new List<DesignerPropertyDescriptor>();
        foreach (var first in sets[0])
        {
            var members = new List<DesignerPropertyDescriptor> { first };
            foreach (var map in maps.Skip(1))
            {
                if (!map.TryGetValue(first.Identity, out var member) || member.ValueType != first.ValueType) break;
                members.Add(member);
            }
            if (members.Count != sets.Count) continue;
            var type = Nullable.GetUnderlyingType(first.ValueType) ?? first.ValueType;
            bool readOnly = first.Name == "Name" || members.Any(member => member.IsReadOnly || member.HasDialogEditor)
                || !(type == typeof(string) || type.IsValueType);
            // Keep per-node descriptors and their existing safe converters. No primary selection
            // swapping, project property invocation or mutable complex object sharing is required.
            var property = new DesignerPropertyDescriptor
            {
                Name = first.Name, Path = first.Path, DisplayName = first.DisplayName, Category = first.Category,
                Description = first.Description + (readOnly ? " This value requires single-control editing." : " Edits every selected control atomically."),
                ValueType = first.ValueType, IsReadOnly = readOnly, IsVisible = true, IsAdvanced = first.IsAdvanced,
                Depth = first.Depth, ShouldSerialize = first.ShouldSerialize,
                StandardValues = first.StandardValues, NumericMinimum = first.NumericMinimum, NumericMaximum = first.NumericMaximum,
                NumericIncrement = first.NumericIncrement, NumericDecimalPlaces = first.NumericDecimalPlaces,
                UseBooleanCheckBox = first.UseBooleanCheckBox,
                GetValue = first.GetValue,
                HasMixedValue = () => members.Skip(1).Any(member => !Equals(first.GetValue(), member.GetValue())),
                CommitText = text =>
                {
                    foreach (var member in members)
                        if (!member.TryCommit(text, out var error)) return (false, error);
                    return (true, null);
                }
            };
            if (members.All(member => member.HasChildren))
                property.Children.AddRange(IntersectDescriptors(members.Select(member => (IReadOnlyList<DesignerPropertyDescriptor>)member.Children).ToArray()));
            shared.Add(property);
        }
        return shared;
    }

    private bool ValidateCommittedProperty(DesignControlNode? node, string identity, out string? error)
    {
        error = null;
        var type = node is null ? playgroundState.Document.RootKind == DesignRootKind.UserControl ? typeof(UserControl) : null
            : playgroundState.ResolveControlType(node);
        if (type is null) return true;
        if (!DesignerTypographyPropertyValidator.TryValidate(node?.Properties ?? playgroundState.Document.Properties, type, out error)) return false;
        if (node is null) return true;
        if ((type == typeof(ListView) || type == typeof(CollectionView) || type == typeof(CarouselView) || type == typeof(RefreshView))
            && !DesignerVirtualizationPropertyValidator.TryValidate(node, identity, type, out error)) return false;
        if ((type == typeof(SearchBar) || type == typeof(TimePicker))
            && !DesignerSearchTimePropertyValidator.TryValidate(node, type, out error)) return false;
        return !DesignerPagePropertyValidator.IsKnown(type) || DesignerPagePropertyValidator.TryValidate(node, type, out error);
    }
}
