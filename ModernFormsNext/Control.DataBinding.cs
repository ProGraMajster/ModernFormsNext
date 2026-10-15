using ModernFormsNext.Layout;
using ModernFormsNext.DataBinding;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_bindingManagerProperty = PropertyStore.CreateKey();
    private static readonly int s_bindingsProperty = PropertyStore.CreateKey();

    // Recycled containers can prepare bindings while detached and before their first CreateControl.
    // Reassociate only existing bindings after attachment, through the canonical BindingContext path.
    internal void RefreshItemContainerBindings()
    {
        if (Properties.ContainsKey(s_bindingsProperty)) OnBindingContextChanged(EventArgs.Empty);
    }

    internal void ClearItemContainerBindings()
    {
        if (Properties.TryGetValue(s_bindingsProperty, out ControlBindingsCollection? bindings)) bindings?.Clear();
    }
}
