namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private ResourceDictionary? resources;

    /// <summary>
    /// Gets the resources scoped to this window.
    /// </summary>
    /// <remarks>
    /// Window resources override <see cref="Application.Resources"/> for controls hosted by this
    /// window, but are themselves overridden by resources on a control or one of its ancestors.
    /// Update resources used by live controls on the owning UI/dispatcher thread.
    /// </remarks>
    public ResourceDictionary Resources => resources ??= new ResourceDictionary();

    internal ResourceDictionary? ResourcesInternal => resources;
}
