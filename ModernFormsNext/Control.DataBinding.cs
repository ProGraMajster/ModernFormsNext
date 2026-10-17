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
        if (Properties.ContainsKey(s_bindingsProperty) || Controls.Count > 0) OnBindingContextChanged(EventArgs.Empty);
    }

    internal void ClearItemContainerBindings()
    {
        // Composite templates own descendant bindings too. Capture that bounded old tree before
        // clearing can invoke application code; newly inserted/reparented children belong to the
        // newer operation and must not have their bindings removed by this retirement pass.
        long version = parentAssignmentVersion;
        (Control Child, long Version)[]? children = Controls.Count == 0 ? null : new (Control, long)[Controls.Count];
        if (children is not null)
            for (int index = 0; index < children.Length; index++)
                children[index] = (Controls[index], Controls[index].parentAssignmentVersion);
        List<Exception>? failures = null;
        try
        {
            if (Properties.TryGetValue(s_bindingsProperty, out ControlBindingsCollection? bindings)) bindings?.Clear();
        }
        catch (Exception error) { (failures ??= []).Add(error); }
        if (children is not null && !IsDisposed && version == parentAssignmentVersion)
            foreach (var child in children)
                if (ReferenceEquals(child.Child.Parent, this) && child.Child.parentAssignmentVersion == child.Version)
                {
                    try { child.Child.ClearItemContainerBindings(); }
                    catch (Exception error) { (failures ??= []).Add(error); }
                }
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Item template binding cleanup failed.", failures);
    }
}
