namespace ModernFormsNext;

public abstract partial class WindowBase
{
    internal DataBinding.InputBindingResolver SurfaceInputBindingResolver => inputBindingResolver;
    internal void PreviewSurfaceKeyDown(KeyEventArgs e) => OnKeyDown(e);
    internal void PreviewSurfaceKeyUp(KeyEventArgs e) => OnKeyUp(e);
}
