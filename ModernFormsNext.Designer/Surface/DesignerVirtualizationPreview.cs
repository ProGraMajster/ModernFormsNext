namespace ModernFormsNext.Designer.Surface;

/// <summary>Supplies a finite synthetic source to the built-in ListView preview.</summary>
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
}
