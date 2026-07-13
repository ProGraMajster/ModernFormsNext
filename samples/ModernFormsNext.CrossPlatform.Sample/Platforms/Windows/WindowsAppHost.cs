using System.Drawing;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Provides the native Windows window for the shared cross-platform application.
/// </summary>
/// <remarks>
/// The host adds the shared <see cref="App.Root"/> directly to a real ModernFormsNext form. It
/// contains no page construction or duplicated application behavior.
/// </remarks>
public sealed class WindowsAppHost : Form
{
    /// <summary>Creates the Windows host for a shared application instance.</summary>
    /// <param name="app">The shared application.</param>
    public WindowsAppHost(App app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Text = "ModernFormsNext Cross-Platform Sample — Windows";
        ClientSize = new Size(760, 720);
        MinimumSize = new Size(520, 620);
        app.Root.Dock = DockStyle.Fill;
        Controls.Add(app.Root);
        app.UpdateSurfaceDiagnostics(1, 1, surfaceAttached: true, activePointers: 0, nativeRenderCount: 0);
        app.NotifyLifecycle("Windows window created");
    }
}
