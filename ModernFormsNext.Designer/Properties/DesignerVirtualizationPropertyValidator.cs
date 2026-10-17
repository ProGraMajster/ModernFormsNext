using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

/// <summary>Validates built-in collection/refresh settings through production setters before a grid transaction commits.</summary>
/// <remarks>
/// The caller has resolved an exact built-in framework type. No project constructor, source,
/// binding or template is invoked. A source-free temporary list keeps validation bounded and
/// avoids duplicating runtime size/cache/stack-wrap rules in the Designer.
/// </remarks>
internal static class DesignerVirtualizationPropertyValidator
{
    internal static bool TryValidate(DesignControlNode node, string path, Type controlType, out string? error)
    {
        error = null;
        string property = path.Split('.', 2)[0];
        try
        {
            if (controlType == typeof(CarouselView))
            {
                if (property is not ("CacheItemCount" or "PageOrientation")) return true;
                using var carousel = new CarouselView();
                carousel.SuspendLayout();
                carousel.CacheItemCount = Read(node, "CacheItemCount", carousel.CacheItemCount);
                carousel.PageOrientation = Read(node, "PageOrientation", carousel.PageOrientation);
                return true;
            }
            if (controlType == typeof(RefreshView))
            {
                if (property is not ("PullThreshold" or "HeaderHeight")) return true;
                using var refresh = new RefreshView();
                refresh.SuspendLayout();
                refresh.PullThreshold = Read(node, "PullThreshold", refresh.PullThreshold);
                refresh.HeaderHeight = Read(node, "HeaderHeight", refresh.HeaderHeight);
                return true;
            }
            if (property is not ("ItemSize" or "CacheItemCount" or "ItemsLayout" or
                "ItemOrientation" or "VariableItemSize" or "VirtualizationEnabled")) return true;
            using ItemsControl view = controlType == typeof(CollectionView) ? new CollectionView() : new ListView();
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
