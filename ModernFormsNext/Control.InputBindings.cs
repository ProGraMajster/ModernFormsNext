using System.ComponentModel;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_inputBindingsProperty = PropertyStore.CreateKey();

    /// <summary>Gets runtime keyboard bindings defined in this control's scope.</summary>
    /// <remarks>
    /// Access on the owning UI thread. Lookup starts at the focused control, then nearest ancestors,
    /// the window and Application. Bindings precede normal control KeyDown handling. First added
    /// available matches win; unavailable bindings allow fallback. The collection is created lazily.
    /// Disposal or window closure releases registrations; no rendering/layout is triggered by edits.
    /// </remarks>
    /// <example><code>
    /// editor.InputBindings.Add(new KeyBinding(save, new KeyGesture(Keys.S, KeyModifiers.Control)));
    /// </code></example>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public InputBindingCollection InputBindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed || FindWindow()?.InputBindingsClosed == true, this);
            if (!Properties.TryGetValue(s_inputBindingsProperty, out InputBindingCollection? bindings))
                bindings = Properties.AddValue(s_inputBindingsProperty, new InputBindingCollection(this));
            bindings!.VerifyAccess();
            return bindings;
        }
    }

    internal InputBindingCollection? InputBindingsInternal
        => Properties.TryGetValue(s_inputBindingsProperty, out InputBindingCollection? bindings) ? bindings : null;

    internal void ReleaseInputBindings()
    {
        InputBindingsInternal?.Release();
        // This key only stores an object. Do not inspect/remove unrelated integer entries;
        // other control cleanup paths likewise remove their specific object slots.
        Properties.RemoveObject(s_inputBindingsProperty);
    }

    internal void ReleaseInputBindingsForSubtree()
    {
        ReleaseInputBindings();
        foreach (var child in Controls.GetAllControls(true)) child.ReleaseInputBindingsForSubtree();
    }
}
