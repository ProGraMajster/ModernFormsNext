namespace ModernFormsNext.Designer.Surface;

/// <summary>Supplies finite synthetic sources to the built-in item-control previews.</summary>
/// <remarks>
/// This runs only after framework-only type resolution. It never reads project sources, bindings
/// or templates. Preview-only cache limits do not alter the authored document or generated code.
/// Geometry and recycling still use the production ItemsControl layout contracts.
/// </remarks>
internal static class DesignerVirtualizationPreview
{
    internal const int ItemLimit = 12;

    internal static void Populate(ListView view)
    {
        view.SuspendLayout();
        try
        {
            // A persisted cache may deliberately request the entire runtime source. The preview
            // has its own small source and never needs that budget, even for a one-pixel cell.
            view.CacheItemCount = Math.Min(view.CacheItemCount, 2);
            if (view.Items.Count == 0)
                for (int index = 0; index < ItemLimit; index++)
                    view.Items.Add($"Sample {index + 1}");
        }
        finally { view.ResumeLayout(); }
        view.PerformLayout();
    }

    internal static void Populate(CollectionView view)
    {
        view.SuspendLayout();
        try
        {
            view.CacheItemCount = Math.Min(view.CacheItemCount, 2);
            // This exact built-in control starts without a source/template. No project object,
            // indexer or factory is consulted to supply the representative production rows.
            if (view.ItemsSource is null)
                view.ItemsSource = Enumerable.Range(1, ItemLimit).Select(index => (object)$"Sample {index}").ToArray();
        }
        finally { view.ResumeLayout(); }
        view.PerformLayout();
    }

    internal static void Populate(CarouselView view)
    {
        view.SuspendLayout();
        try
        {
            view.CacheItemCount = Math.Min(view.CacheItemCount, 2);
            if (view.ItemsSource is null)
                view.ItemsSource = Enumerable.Range(1, ItemLimit).Select(index => (object)$"Page {index}").ToArray();
        }
        finally { view.ResumeLayout(); }
        view.PerformLayout();
    }
}
