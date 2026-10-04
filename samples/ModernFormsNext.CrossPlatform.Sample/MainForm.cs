namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>The shared Form used by the Windows and Android application/windowing hosts.</summary>
/// <remarks>The backend owns native presentation; the application owns this Form and its controls.</remarks>
public class MainForm : Form
{
    /// <summary>Creates a Form around the application's existing shared page.</summary>
    /// <param name="app">The process-owned application state and page.</param>
    public MainForm(App app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Text = "ModernFormsNext Cross-Platform Sample";
        app.Root.Dock = DockStyle.Fill;
        Controls.Add(app.Root);
        app.Root.TextInputClientProvider = () => TextInputClient;
        InsetsChanged += (_, e) => { app.Root.UpdateKeyboardOcclusion(e.Insets); app.Root.ScrollCurrentCaretIntoView(); };
        ClientSizeChanged += (_, _) => { app.Root.ScrollCurrentCaretIntoView(); app.RefreshPlatformStatus(); };
        Activated += (_, _) => app.RefreshPlatformStatus();
        Closed += (_, _) => app.Root.TextInputClientProvider = null;
    }
}
