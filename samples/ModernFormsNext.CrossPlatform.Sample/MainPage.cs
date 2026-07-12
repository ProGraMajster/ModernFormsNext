using ModernFormsNext.WindowKit.Platform.Permissions;
using SkiaSharp;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Defines the one shared ModernFormsNext control tree rendered on Windows and Android.
/// </summary>
/// <remarks>
/// All visible widgets are framework controls. Android renders this same tree through
/// <see cref="SkiaControlSurface"/>; it does not replace these controls with native Android views.
/// </remarks>
public sealed class MainPage : Control
{
    private readonly App app;
    private readonly Label headerLabel;
    private readonly Label platformLabel;
    private readonly Label operatingSystemLabel;
    private readonly Label backendLabel;
    private readonly Label dispatcherLabel;
    private readonly Label hostLabel;
    private readonly Label surfaceLabel;
    private readonly Label renderLabel;
    private readonly Label clickLabel;
    private readonly TextBox nameTextBox;
    private readonly Label greetingLabel;
    private readonly CheckBox enabledCheckBox;
    private readonly Button clickButton;
    private readonly Button dispatcherButton;
    private readonly Button permissionButton;
    private int displayedRenderBucket = -1;

    /// <summary>Creates the shared page for an application.</summary>
    /// <param name="app">The owning shared application.</param>
    public MainPage(App app)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        Dock = DockStyle.Fill;
        BackColor = new SKColor(246, 248, 252);

        headerLabel = CreateLabel("ModernFormsNext — one App, one Control tree");
        platformLabel = CreateLabel(string.Empty);
        operatingSystemLabel = CreateLabel(string.Empty);
        backendLabel = CreateLabel(string.Empty);
        dispatcherLabel = CreateLabel(string.Empty);
        hostLabel = CreateLabel(string.Empty);
        surfaceLabel = CreateLabel(string.Empty);
        renderLabel = CreateLabel(string.Empty);
        clickLabel = CreateLabel(string.Empty);

        nameTextBox = new TextBox { Text = "ModernFormsNext" };
        greetingLabel = CreateLabel(string.Empty);
        enabledCheckBox = new CheckBox { Text = "Enable shared action", Checked = true };
        clickButton = new Button { Text = "Run shared action" };
        dispatcherButton = new Button { Text = "Post through UI dispatcher" };
        permissionButton = new Button { Text = "Check/request camera permission" };

        clickButton.Click += (_, _) =>
        {
            app.State.ClickCount++;
            RefreshStatus();
        };
        nameTextBox.TextChanged += (_, _) => RefreshStatus();
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            clickButton.Enabled = enabledCheckBox.Checked;
            RefreshStatus();
        };
        dispatcherButton.Click += (_, _) =>
        {
            app.PlatformServices.Dispatcher.Post(() =>
            {
                app.State.DispatcherCount++;
                RefreshStatus();
            });
        };
        permissionButton.Click += async (_, _) => await RequestPermissionAsync();

        Controls.AddRange([
            headerLabel,
            platformLabel,
            operatingSystemLabel,
            backendLabel,
            dispatcherLabel,
            hostLabel,
            surfaceLabel,
            renderLabel,
            clickLabel,
            nameTextBox,
            greetingLabel,
            enabledCheckBox,
            clickButton,
            dispatcherButton,
            permissionButton
        ]);

        RefreshStatus();
    }

    /// <summary>Updates labels from shared state and the injected platform implementation.</summary>
    public void RefreshStatus()
    {
        var platform = app.PlatformServices;
        platformLabel.Text = $"Platform: {platform.PlatformName}";
        operatingSystemLabel.Text = $"OS: {platform.OperatingSystem}";
        backendLabel.Text = $"Backend: {platform.BackendName}";
        dispatcherLabel.Text = $"Dispatcher callbacks: {app.State.DispatcherCount}; UI access: {platform.Dispatcher.CheckAccess()}";
        hostLabel.Text = $"Host lifecycle: {platform.HostState}";
        surfaceLabel.Text = $"Logical surface: {app.State.SurfaceWidth} × {app.State.SurfaceHeight}";
        renderLabel.Text = $"Render passes: {app.State.RenderCount}";
        clickLabel.Text = $"Shared clicks: {app.State.ClickCount}";
        greetingLabel.Text = $"Label bound to TextBox: Hello, {nameTextBox.Text}!";
        clickButton.Enabled = enabledCheckBox.Checked;
        permissionButton.Enabled = platform.SupportsPermissionAction;
        permissionButton.Text = platform.SupportsPermissionAction
            ? $"Camera permission: {app.State.PermissionStatus}"
            : "Camera permission: Android only";
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        app.State.SurfaceWidth = Width;
        app.State.SurfaceHeight = Height;
        ArrangeControls();
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        app.State.RenderCount++;

        // Updating every render would invalidate while painting and create a render loop. Refresh
        // at the first pass and then every tenth pass; user interactions refresh it immediately.
        var bucket = app.State.RenderCount == 1 ? 0 : (int)(app.State.RenderCount / 10);
        if (bucket != displayedRenderBucket)
        {
            displayedRenderBucket = bucket;
            renderLabel.Text = $"Render passes: {app.State.RenderCount}";
        }

        base.OnPaint(e);
    }

    private async Task RequestPermissionAsync()
    {
        permissionButton.Enabled = false;
        try
        {
            var status = await app.PlatformServices.RequestSamplePermissionAsync();
            app.State.PermissionStatus = status switch
            {
                PlatformPermissionStatus.Granted => "Granted",
                PlatformPermissionStatus.Denied => "Denied",
                PlatformPermissionStatus.Restricted => "Restricted",
                PlatformPermissionStatus.NotDeclared => "Not declared",
                _ => status.ToString()
            };
        }
        catch (Exception exception)
        {
            app.State.PermissionStatus = $"Error: {exception.Message}";
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void ArrangeControls()
    {
        const int margin = 24;
        const int gap = 8;
        const int rowHeight = 30;
        var contentWidth = Math.Max(160, Width - (margin * 2));
        var y = margin;

        SetRow(headerLabel, margin, ref y, contentWidth, 38, gap + 4);
        foreach (var label in new[]
        {
            platformLabel, operatingSystemLabel, backendLabel, dispatcherLabel, hostLabel,
            surfaceLabel, renderLabel, clickLabel
        })
        {
            SetRow(label, margin, ref y, contentWidth, 24, 2);
        }

        y += gap;
        SetRow(nameTextBox, margin, ref y, contentWidth, 38, gap);
        SetRow(greetingLabel, margin, ref y, contentWidth, rowHeight, gap);
        SetRow(enabledCheckBox, margin, ref y, contentWidth, rowHeight, gap);
        SetRow(clickButton, margin, ref y, contentWidth, 42, gap);
        SetRow(dispatcherButton, margin, ref y, contentWidth, 42, gap);
        SetRow(permissionButton, margin, ref y, contentWidth, 42, gap);
    }

    private static Label CreateLabel(string text) => new() { Text = text };

    private static void SetRow(Control control, int x, ref int y, int width, int height, int gap)
    {
        control.SetBounds(x, y, width, height);
        y += height + gap;
    }
}
