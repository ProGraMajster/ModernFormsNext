using ModernFormsNext.WindowKit;

namespace ModernFormsNext.Testing;

public sealed partial class TestWindowHost
{
    /// <summary>Publishes native client inset values through the production optional window feature.</summary>
    /// <param name="insets">Logical persistent and keyboard occlusion for this window.</param>
    /// <remarks>Call on the host UI thread. This tests shared propagation, not native inset measurement.</remarks>
    public void SetInsets(WindowInsets insets)
    {
        ThrowIfClosed();
        owner.Dispatcher.Run(() => backend.SetInsets(insets));
    }

    /// <summary>Publishes individual window activation or deactivation through the backend callback.</summary>
    /// <param name="active">Whether this window receives activation.</param>
    /// <remarks>
    /// Call on the host UI thread. This does not change process activity or other windows; publish
    /// application activity through Services.Lifecycle separately to test native notification ordering.
    /// </remarks>
    public void SetActive(bool active)
    {
        ThrowIfClosed();
        owner.Dispatcher.Run(() =>
        {
            if (active) backend.Activate();
            else backend.Deactivated?.Invoke();
        });
    }
}
