using ModernFormsNext.WindowKit.Platform.Permissions;
using SkiaSharp;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Defines the one shared, scrollable ModernFormsNext control tree rendered on Windows and Android.
/// </summary>
/// <remarks>
/// Every visible widget is a framework control. Android renders this exact tree through
/// <see cref="SkiaControlSurface"/>; it does not replace the controls with native Android views.
/// The page intentionally exercises text input, Unicode, layout, scrolling, focus, lifecycle
/// diagnostics, dispatching, and an explicitly initiated permission flow.
/// </remarks>
public sealed class MainPage : Control
{
    private readonly App app;
    private readonly ScrollableControl scrollArea;
    private readonly Label headerLabel;
    private readonly Label platformLabel;
    private readonly Label operatingSystemLabel;
    private readonly Label backendLabel;
    private readonly Label dispatcherLabel;
    private readonly Label hostLabel;
    private readonly Label lifecycleLabel;
    private readonly Label surfaceLabel;
    private readonly Label densityLabel;
    private readonly Label renderLabel;
    private readonly Label inputLabel;
    private readonly Label focusLabel;
    private readonly Label clickLabel;
    private readonly Label unicodeLabel;
    private readonly Label longContentLabel;
    private readonly TextBox nameTextBox;
    private readonly TextBox multiLineTextBox;
    private readonly Label greetingLabel;
    private readonly CheckBox enabledCheckBox;
    private readonly CheckBox diagnosticsCheckBox;
    private readonly Button clickButton;
    private readonly Button dispatcherButton;
    private readonly Button lifecycleButton;
    private readonly FlowLayoutPanel permissionButtons;
    private readonly Button permissionCheckButton;
    private readonly Button permissionRequestButton;
    private readonly Button settingsButton;
    private readonly Label[] diagnosticLabels;
    private int displayedRenderBucket = -1;

    /// <summary>Creates the shared page for an application.</summary>
    /// <param name="app">The owning shared application.</param>
    public MainPage(App app)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        Dock = DockStyle.Fill;
        BackColor = new SKColor(246, 248, 252);

        scrollArea = new ScrollableControl
        {
            AutoScroll = true,
            BackColor = BackColor
        };
        headerLabel = CreateLabel("ModernFormsNext — one App, one Control tree");
        platformLabel = CreateLabel(string.Empty);
        operatingSystemLabel = CreateLabel(string.Empty);
        backendLabel = CreateLabel(string.Empty);
        dispatcherLabel = CreateLabel(string.Empty);
        hostLabel = CreateLabel(string.Empty);
        lifecycleLabel = CreateLabel(string.Empty);
        surfaceLabel = CreateLabel(string.Empty);
        densityLabel = CreateLabel(string.Empty);
        renderLabel = CreateLabel(string.Empty);
        inputLabel = CreateLabel(string.Empty);
        focusLabel = CreateLabel(string.Empty);
        clickLabel = CreateLabel(string.Empty);
        unicodeLabel = CreateLabel("Unicode: Zażółć gęślą jaźń · 你好 · مرحبًا · 👋🏽 🚀");
        longContentLabel = CreateLabel(
            "Scrollable content: resize the Windows window or rotate the Android device. " +
            "The same shared layout remains active while the native host updates its logical surface.");

        nameTextBox = new TextBox { Text = "ModernFormsNext" };
        multiLineTextBox = new TextBox
        {
            MultiLine = true,
            Text = "Multiline IME test:\r\nzażółć gęślą jaźń\r\nemoji 👋🏽 and composition: 你好"
        };
        greetingLabel = CreateLabel(string.Empty);
        enabledCheckBox = new CheckBox { Text = "Enable shared action", Checked = true };
        diagnosticsCheckBox = new CheckBox { Text = "Show host diagnostics", Checked = true };
        clickButton = new Button { Text = "Run shared action" };
        dispatcherButton = new Button { Text = "Post through UI dispatcher" };
        lifecycleButton = new Button { Text = "Refresh lifecycle snapshot" };
        permissionCheckButton = new Button { Text = "Check camera" };
        permissionRequestButton = new Button { Text = "Request camera" };
        settingsButton = new Button { Text = "Open app settings" };
        permissionButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        permissionButtons.Controls.AddRange([permissionCheckButton, permissionRequestButton, settingsButton]);

        diagnosticLabels =
        [
            platformLabel,
            operatingSystemLabel,
            backendLabel,
            dispatcherLabel,
            hostLabel,
            lifecycleLabel,
            surfaceLabel,
            densityLabel,
            renderLabel,
            inputLabel,
            focusLabel,
            clickLabel
        ];

        clickButton.Click += (_, _) =>
        {
            app.State.ClickCount++;
            app.State.LastInput = "Shared action button clicked";
            RefreshStatus();
        };
        nameTextBox.TextChanged += (_, _) => RefreshStatus();
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            clickButton.Enabled = enabledCheckBox.Checked;
            RefreshStatus();
        };
        diagnosticsCheckBox.CheckedChanged += (_, _) =>
        {
            foreach (var label in diagnosticLabels)
                label.Visible = diagnosticsCheckBox.Checked;
            ArrangeControls();
            RefreshStatus();
        };
        dispatcherButton.Click += (_, _) =>
        {
            app.PlatformServices.Dispatcher.Post(() =>
            {
                app.State.DispatcherCount++;
                app.State.LastInput = "Dispatcher callback completed";
                RefreshStatus();
            });
        };
        lifecycleButton.Click += (_, _) =>
        {
            app.State.LastInput = "Lifecycle snapshot refreshed";
            RefreshStatus();
        };
        permissionCheckButton.Click += async (_, _) => await RunPermissionActionAsync(requestPermission: false);
        permissionRequestButton.Click += async (_, _) => await RunPermissionActionAsync(requestPermission: true);
        settingsButton.Click += async (_, _) => await OpenSettingsAsync();

        TrackFocus(nameTextBox, "Single-line TextBox");
        TrackFocus(multiLineTextBox, "Multiline TextBox");
        TrackFocus(clickButton, "Shared action Button");

        scrollArea.Controls.AddRange([
            headerLabel,
            .. diagnosticLabels,
            unicodeLabel,
            nameTextBox,
            greetingLabel,
            multiLineTextBox,
            enabledCheckBox,
            diagnosticsCheckBox,
            clickButton,
            dispatcherButton,
            lifecycleButton,
            permissionButtons,
            longContentLabel
        ]);
        Controls.Add(scrollArea);

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
        hostLabel.Text = $"Backend host: {platform.HostState}";
        lifecycleLabel.Text = $"Activity/window lifecycle: {app.State.LifecycleStatus}";
        surfaceLabel.Text =
            $"Logical surface: {app.State.SurfaceWidth} × {app.State.SurfaceHeight}; " +
            $"attached: {app.State.SurfaceAttached}; pointers: {app.State.ActivePointerCount}";
        densityLabel.Text = $"Density: {app.State.Density:0.##}; scaled density: {app.State.ScaledDensity:0.##}";
        renderLabel.Text = $"Shared paints: {app.State.RenderCount}; native paints: {app.State.NativeRenderCount}";
        inputLabel.Text = $"Last input: {app.State.LastInput}";
        focusLabel.Text = $"Shared focus: {app.State.FocusedControl}";
        clickLabel.Text = $"Shared clicks: {app.State.ClickCount}";
        greetingLabel.Text = $"Label bound to TextBox: Hello, {nameTextBox.Text}!";
        clickButton.Enabled = enabledCheckBox.Checked;
        permissionCheckButton.Enabled = platform.SupportsPermissionAction;
        permissionRequestButton.Enabled = platform.SupportsPermissionAction;
        settingsButton.Enabled = platform.SupportsPermissionAction;
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        app.State.SurfaceWidth = Width;
        app.State.SurfaceHeight = Height;
        scrollArea.SetBounds(0, 0, Width, Height);
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
            renderLabel.Text = $"Shared paints: {app.State.RenderCount}; native paints: {app.State.NativeRenderCount}";
        }

        base.OnPaint(e);
    }

    private async Task RunPermissionActionAsync(bool requestPermission)
    {
        SetPermissionButtonsEnabled(false);
        try
        {
            var status = requestPermission
                ? await app.PlatformServices.RequestSamplePermissionAsync()
                : await app.PlatformServices.CheckSamplePermissionAsync();
            app.State.PermissionStatus = DescribePermissionStatus(status);
            app.State.LastInput = requestPermission ? "Camera permission requested" : "Camera permission checked";
        }
        catch (Exception exception)
        {
            app.State.PermissionStatus = $"Error: {exception.Message}";
        }
        finally
        {
            RefreshStatus();
            SetPermissionButtonsEnabled(app.PlatformServices.SupportsPermissionAction);
        }
    }

    private async Task OpenSettingsAsync()
    {
        SetPermissionButtonsEnabled(false);
        try
        {
            var opened = await app.PlatformServices.OpenApplicationSettingsAsync();
            app.State.LastInput = opened ? "Application settings opened" : "Application settings unavailable";
        }
        catch (Exception exception)
        {
            app.State.LastInput = $"Settings error: {exception.Message}";
        }
        finally
        {
            RefreshStatus();
            SetPermissionButtonsEnabled(app.PlatformServices.SupportsPermissionAction);
        }
    }

    private void SetPermissionButtonsEnabled(bool enabled)
    {
        permissionCheckButton.Enabled = enabled;
        permissionRequestButton.Enabled = enabled;
        settingsButton.Enabled = enabled;
        permissionCheckButton.Text = $"Check camera ({app.State.PermissionStatus})";
    }

    private void TrackFocus(Control control, string description)
    {
        control.GotFocus += (_, _) =>
        {
            app.State.FocusedControl = description;
            RefreshStatus();
        };
    }

    private void ArrangeControls()
    {
        const int margin = 24;
        const int gap = 8;
        var contentWidth = Math.Max(240, scrollArea.Width - (margin * 2) - 18);
        var y = margin;

        SetRow(headerLabel, margin, ref y, contentWidth, 40, gap + 4);
        if (diagnosticsCheckBox.Checked)
        {
            foreach (var label in diagnosticLabels)
                SetRow(label, margin, ref y, contentWidth, 24, 2);
            y += gap;
        }

        SetRow(unicodeLabel, margin, ref y, contentWidth, 32, gap);
        SetRow(nameTextBox, margin, ref y, contentWidth, 38, gap);
        SetRow(greetingLabel, margin, ref y, contentWidth, 30, gap);
        SetRow(multiLineTextBox, margin, ref y, contentWidth, 112, gap);
        SetRow(enabledCheckBox, margin, ref y, contentWidth, 30, gap);
        SetRow(diagnosticsCheckBox, margin, ref y, contentWidth, 30, gap);
        SetRow(clickButton, margin, ref y, contentWidth, 42, gap);
        SetRow(dispatcherButton, margin, ref y, contentWidth, 42, gap);
        SetRow(lifecycleButton, margin, ref y, contentWidth, 42, gap);

        var permissionButtonWidth = Math.Max(180, Math.Min(250, (contentWidth - 16) / 3));
        foreach (var button in permissionButtons.Controls.OfType<Button>())
            button.SetBounds(0, 0, permissionButtonWidth, 42);
        SetRow(permissionButtons, margin, ref y, contentWidth, contentWidth < 620 ? 146 : 50, gap);
        SetRow(longContentLabel, margin, ref y, contentWidth, 76, margin);

        scrollArea.PerformLayout();
    }

    private static string DescribePermissionStatus(PlatformPermissionStatus status) => status switch
    {
        PlatformPermissionStatus.Granted => "Granted",
        PlatformPermissionStatus.Denied => "Denied",
        PlatformPermissionStatus.PermanentlyDenied => "Permanently denied",
        PlatformPermissionStatus.Restricted => "Restricted",
        PlatformPermissionStatus.NotDeclared => "Not declared",
        PlatformPermissionStatus.NotSupported => "Not supported",
        _ => status.ToString()
    };

    private static Label CreateLabel(string text) => new() { Text = text };

    private static void SetRow(Control control, int x, ref int y, int width, int height, int gap)
    {
        control.SetBounds(x, y, width, height);
        y += height + gap;
    }
}
