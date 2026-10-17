using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

/// <summary>Validates built-in list settings through production setters before a grid transaction commits.</summary>
/// <remarks>
/// The caller has resolved the exact framework ListView or CollectionView type. No project constructor, source,
/// binding or template is invoked. A source-free temporary list keeps validation bounded and
/// avoids duplicating runtime size/cache/stack-wrap rules in the Designer.
/// </remarks>
internal static class DesignerVirtualizationPropertyValidator
{
    internal static bool TryValidate(DesignControlNode node, string path, out string? error, bool collectionView = false)
    {
        error = null;
        if (path.Split('.', 2)[0] is not ("ItemSize" or "CacheItemCount" or "ItemsLayout" or
            "ItemOrientation" or "VariableItemSize" or "VirtualizationEnabled")) return true;
        try
        {
            using ItemsControl view = collectionView ? new CollectionView() : new ListView();
            view.SuspendLayout();
            view.ItemSize = Read(node, "ItemSize", view.ItemSize);
            view.CacheItemCount = Read(node, "CacheItemCount", view.CacheItemCount);
            view.ItemOrientation = Read(node, "ItemOrientation", view.ItemOrientation);
            view.ItemsLayout = Read(node, "ItemsLayout", view.ItemsLayout);
            view.VariableItemSize = Read(node, "VariableItemSize", view.VariableItemSize);
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            error = failure.Message;
            return false;
        }
    }

    private static T Read<T>(DesignControlNode node, string name, T fallback)
        => node.Properties.TryGetValue(name, out var value)
            ? (T)DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(T))! : fallback;
}
