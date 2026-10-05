using System.Drawing;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Provides the native Windows window for the shared cross-platform application.
/// </summary>
/// <remarks>
/// The host specializes the shared <see cref="MainForm"/> with desktop size and diagnostics.
/// Page construction and Form input/lifecycle wiring remain shared with Android.
/// </remarks>
public sealed class WindowsAppHost : MainForm
{
    /// <summary>Creates the Windows host for a shared application instance.</summary>
    /// <param name="app">The shared application.</param>
    public WindowsAppHost(App app) : base(app)
    {
        Text = "ModernFormsNext Cross-Platform Sample — Windows";
        ClientSize = new Size(760, 720);
        MinimumSize = new Size(520, 620);
        app.UpdateSurfaceDiagnostics(1, 1, surfaceAttached: true, activePointers: 0, nativeRenderCount: 0);
        app.NotifyLifecycle("Windows window created");
    }
}
