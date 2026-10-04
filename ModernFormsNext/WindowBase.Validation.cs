namespace ModernFormsNext;

public abstract partial class WindowBase
{
    /// <summary>Validates the window's application controls without moving focus.</summary>
    /// <returns>False on the first failed/canceled validation or if the window is closed/disposed.</returns>
    /// <remarks>
    /// Call on the owning UI thread. Shares Control.ValidateChildren's depth-first snapshot and
    /// fail-fast binding pipeline. The public Controls collection includes Form client children,
    /// not implicit title bars or scrollbars. Hidden/disabled fields are included. Callback errors
    /// propagate and earlier source setter side effects are not rolled back.
    /// </remarks>
    /// <example><code>
    /// if (form.ValidateChildren()) SaveModel();
    /// </code></example>
    public bool ValidateChildren() => adapter.ValidateChildrenCore(Controls);
}
