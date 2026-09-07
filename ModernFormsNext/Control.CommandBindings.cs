using System.ComponentModel;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_commandBindingsProperty = PropertyStore.CreateKey();

    /// <summary>Gets runtime routed command handlers defined on this control.</summary>
    /// <remarks>
    /// Access on the UI thread. Routing visits target then ancestors, window and application.
    /// Collection edits requery affected commands; disposal releases registrations. This runtime
    /// collection is created lazily and is not serialized by the Designer.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBindingCollection CommandBindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed || FindWindow()?.InputBindingsClosed == true, this);
            if (!Properties.TryGetValue(s_commandBindingsProperty, out CommandBindingCollection? bindings))
                bindings = Properties.AddValue(s_commandBindingsProperty, new CommandBindingCollection(this));
            bindings!.VerifyAccess();
            return bindings;
        }
    }

    internal CommandBindingCollection? CommandBindingsInternal
        => Properties.TryGetValue(s_commandBindingsProperty, out CommandBindingCollection? bindings) ? bindings : null;

    // Only existing native/surface roots override this. It is not a second hierarchy or registry.
    internal virtual bool IsCommandRoutingRoot => false;
    internal virtual void RefreshRoutedCommandSource() { }

    internal void RefreshRoutedCommandSourcesForSubtree()
    {
        RefreshRoutedCommandSource();
        // Source requery can invoke application predicates. Snapshot children before visiting them.
        foreach (var child in Controls.GetAllControls(true).ToArray())
            if (!child.IsDisposed) child.RefreshRoutedCommandSourcesForSubtree();
    }

    internal void ReleaseCommandBindings()
    {
        CommandBindingsInternal?.Release();
        Properties.RemoveObject(s_commandBindingsProperty);
    }

    internal void ReleaseCommandBindingsForSubtree()
    {
        ReleaseCommandBindings();
        foreach (var child in Controls.GetAllControls(true)) child.ReleaseCommandBindingsForSubtree();
    }
}
