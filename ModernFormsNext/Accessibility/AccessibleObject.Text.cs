using System.ComponentModel;

namespace ModernFormsNext.Accessibility;

public partial class AccessibleObject
{
    /// <summary>Gets an optional provider for the object's existing text document and viewport.</summary>
    /// <remarks>
    /// Providers and ranges require the owning UI thread. This capability is independent of
    /// the current IME focus session and must be absent for sensitive content. Returning a
    /// provider neither selects the object nor exposes text through automation IPC snapshots.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual AccessibleTextProvider? TextProvider => null;
}
