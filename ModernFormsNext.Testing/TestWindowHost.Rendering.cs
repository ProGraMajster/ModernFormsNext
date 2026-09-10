namespace ModernFormsNext.Testing;

public sealed partial class TestWindowHost
{
    private bool capturingRenderedSnapshot;

    /// <summary>Captures one off-screen frame through the production window and control rendering pipeline.</summary>
    /// <param name="maximumPixels">
    /// Maximum framebuffer area in device pixels, from 1 through 16,777,216. The default permits
    /// 16,777,216 pixels. This bounds the capture framebuffer, not allocations made by application code.
    /// </param>
    /// <returns>A detached, caller-owned snapshot. Dispose it when its pixel data is no longer needed.</returns>
    /// <remarks>
    /// Call on the host's UI thread. The method drains pending dispatcher work and stabilizes layout,
    /// then invokes the backend's normal Paint callback. It does not advance the test clock, wait for
    /// asynchronous business work, or repaint repeatedly until application changes stop. Work queued
    /// during painting remains pending. The image includes the hosted Form's managed chrome when
    /// enabled; a control root stays inside its existing Form wrapper and is never reparented.
    /// Pixel dimensions truncate positive logical viewport dimensions multiplied by render scale,
    /// matching the production window. A snapshot survives later changes and window/host disposal.
    /// Rasterization and fonts depend on the current rendering environment; this API does not promise
    /// identical images across machines or implement automatic golden-image comparison.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The pixel budget is outside the supported range.</exception>
    /// <exception cref="InvalidOperationException">
    /// The caller is not the host UI thread, capture reenters the same window, layout does not
    /// stabilize, or scaled pixel dimensions are zero or exceed the budget.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The host or window has closed, including during layout or paint.</exception>
    /// <example>
    /// <code>
    /// using var host = ModernFormsTestHost.Create();
    /// var window = host.Show(new Button { Text = "Save" }, 160, 48);
    /// using var snapshot = window.CaptureRenderedSnapshot();
    /// File.WriteAllBytes("save-button.png", snapshot.EncodePng());
    /// </code>
    /// </example>
    public RenderedSnapshot CaptureRenderedSnapshot(int maximumPixels = HeadlessWindowImpl.MaximumSnapshotPixels)
    {
        ThrowIfClosed();
        return owner.Dispatcher.Run(() =>
        {
            if (capturingRenderedSnapshot)
                throw new InvalidOperationException("A rendered snapshot cannot recursively capture the same window.");
            backend.ValidateSnapshotSize(maximumPixels);
            capturingRenderedSnapshot = true;
            try
            {
                LayoutUntilStable();
                ThrowIfClosed();
                return backend.CaptureRenderedSnapshot(maximumPixels);
            }
            finally
            {
                capturingRenderedSnapshot = false;
            }
        });
    }
}
