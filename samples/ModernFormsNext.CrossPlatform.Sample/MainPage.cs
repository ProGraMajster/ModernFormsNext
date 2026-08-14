using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Platform.Permissions;
using SkiaSharp;
using System.Drawing;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Defines the one shared, scrollable ModernFormsNext control tree rendered on Windows and Android.
/// </summary>
/// <remarks>
/// Every visible widget is a framework control. Android renders this exact tree through
/// <see cref="SkiaControlSurface"/>; it does not replace the controls with native Android views.
/// The page intentionally exercises text input, Unicode, layout, scrolling, focus, lifecycle
/// diagnostics, dispatching, shared animations and effects, theme transitions, reduced motion,
/// and an explicitly initiated permission flow.
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
    private readonly Label actionLabel;
    private readonly Label serviceLabel;
    private readonly Label serviceResultLabel;
    private readonly Label focusLabel;
    private readonly Label clickLabel;
    private readonly Label animationRuntimeLabel;
    private readonly Label animationStatusLabel;
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
    private readonly FlowLayoutPanel animationButtons;
    private readonly Panel animationStage;
    private readonly Panel animationCard;
    private readonly Button effectButton;
    private readonly Button startAnimationsButton;
    private readonly Button stopAnimationsButton;
    private readonly Button themeButton;
    private readonly Button reducedMotionButton;
    private readonly Button permissionCheckButton;
    private readonly Button permissionRequestButton;
    private readonly Button settingsButton;
    private readonly Label[] diagnosticLabels;
    private int displayedRenderBucket = -1;
    private AnimationRun? activeDemoRun;
    private bool alternateAnimationTarget;
    private bool darkTheme;

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
        actionLabel = CreateLabel(string.Empty);
        serviceLabel = CreateLabel(string.Empty);
        serviceResultLabel = CreateLabel(string.Empty);
        focusLabel = CreateLabel(string.Empty);
        clickLabel = CreateLabel(string.Empty);
        animationRuntimeLabel = CreateLabel(string.Empty);
        animationStatusLabel = CreateLabel("Animation smoke controls are ready.");
        unicodeLabel = CreateLabel("Unicode: Zażółć gęślą jaźń · 你好 · مرحبًا · 👋🏽 🚀");
        longContentLabel = CreateLabel(
            "Manual smoke: use two fingers on the effect button; start and rapidly retarget; " +
            "rotate/resize; background and resume; change Android animator scale; then verify " +
            "active work and the frame callback return to idle without losing TextBox focus.");

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

        effectButton = new Button
        {
            Text = "Ripple + press scale + visual state",
            Ripple = new RippleEffect
            {
                Color = Color.FromArgb(105, 255, 255, 255),
                Duration = TimeSpan.FromMilliseconds(500),
                StartFromPointer = true,
                MaxConcurrentRipples = 4
            },
            PressEffect = new PressScaleEffect
            {
                PressedScale = 0.94f,
                PressDuration = TimeSpan.FromMilliseconds(90),
                ReleaseDuration = TimeSpan.FromMilliseconds(140)
            }
        };
        effectButton.Style.BackgroundColor = SKColors.SlateBlue;
        effectButton.Style.ForegroundColor = SKColors.White;
        effectButton.StyleHover.BackgroundColor = SKColors.MediumPurple;
        effectButton.StyleHover.ScaleX = 1.03f;
        effectButton.StyleHover.ScaleY = 1.03f;
        effectButton.StylePressed.BackgroundColor = SKColors.DarkSlateBlue;
        AddStateTransition(effectButton, VisualState.Normal, VisualState.Hover);
        AddStateTransition(effectButton, VisualState.Normal, VisualState.Pressed);
        AddStateTransition(effectButton, VisualState.Hover, VisualState.Normal);
        AddStateTransition(effectButton, VisualState.Hover, VisualState.Pressed);
        AddStateTransition(effectButton, VisualState.Pressed, VisualState.Normal);
        AddStateTransition(effectButton, VisualState.Pressed, VisualState.Hover);

        animationStage = new Panel { BackColor = new SKColor(236, 240, 247) };
        animationCard = new Panel
        {
            Bounds = new Rectangle(12, 18, 118, 72),
            BackColor = SKColors.CornflowerBlue,
            LayoutTransition = new LayoutTransition
            {
                Duration = TimeSpan.FromMilliseconds(520),
                Easing = Easings.EaseOut
            }
        };
        animationStage.SetResourceReference(nameof(Control.BackgroundBrush), "SurfaceGlow");
        animationCard.SetResourceReference(nameof(Control.BackgroundBrush), "PrimaryGradient");
        animationCard.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Layout + visual animation",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter,
            ForeColor = SKColors.White
        });
        animationStage.Controls.Add(animationCard);

        startAnimationsButton = new Button { Text = "Start / retarget" };
        stopAnimationsButton = new Button { Text = "Stop / reset" };
        themeButton = new Button { Text = "Theme transition" };
        reducedMotionButton = new Button { Text = "Reduced motion" };
        animationButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        animationButtons.Controls.AddRange([
            startAnimationsButton,
            stopAnimationsButton,
            themeButton,
            reducedMotionButton
        ]);

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
            actionLabel,
            serviceLabel,
            serviceResultLabel,
            focusLabel,
            clickLabel
        ];

        clickButton.Click += (_, _) =>
        {
            app.State.ClickCount++;
            app.State.LastAction = "Run shared action: click received";
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
            app.State.LastAction = "Dispatcher button: click received";
            app.State.LastServiceInvocation = "IPlatformDispatcher.Post invoked";
            app.State.LastServiceResult = "Pending";
            RefreshStatus();
            try
            {
                app.PlatformServices.Dispatcher.Post(() =>
                {
                    app.State.DispatcherCount++;
                    app.State.LastInput = "Dispatcher callback completed";
                    app.State.LastServiceResult =
                        $"Completed; UI access: {app.PlatformServices.Dispatcher.CheckAccess()}";
                    RefreshStatus();
                });
            }
            catch (Exception exception)
            {
                app.State.LastServiceResult = $"Failed: {exception.Message}";
                RefreshStatus();
            }
        };
        lifecycleButton.Click += (_, _) =>
        {
            app.State.LastInput = "Lifecycle snapshot refreshed";
            RefreshStatus();
        };
        permissionCheckButton.Click += async (_, _) => await RunPermissionActionAsync(requestPermission: false);
        permissionRequestButton.Click += async (_, _) => await RunPermissionActionAsync(requestPermission: true);
        settingsButton.Click += async (_, _) => await OpenSettingsAsync();
        effectButton.Click += (_, _) =>
        {
            app.State.LastInput = "Ripple/press/state button clicked";
            RefreshStatus();
        };
        startAnimationsButton.Click += (_, _) => StartAnimationSmoke();
        stopAnimationsButton.Click += (_, _) => StopAnimationSmoke();
        themeButton.Click += (_, _) => ToggleAnimatedTheme();
        reducedMotionButton.Click += (_, _) => ToggleReducedMotion();

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
            animationRuntimeLabel,
            effectButton,
            animationButtons,
            animationStage,
            animationStatusLabel,
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
        actionLabel.Text = $"Action: {app.State.LastAction}";
        serviceLabel.Text = $"Service invocation: {app.State.LastServiceInvocation}";
        serviceResultLabel.Text = $"Service result: {app.State.LastServiceResult}";
        focusLabel.Text = $"Shared focus: {app.State.FocusedControl}";
        clickLabel.Text = $"Shared clicks: {app.State.ClickCount}";
        AnimationSchedulerDiagnostics animationDiagnostics = AnimationScheduler.Default.GetDiagnostics();
        AnimationPlatformDiagnostics platformAnimation = AnimationScheduler.Default.GetPlatformDiagnostics();
        animationRuntimeLabel.Text =
            $"Scheduler active: {animationDiagnostics.ActiveAnimationCount}; " +
            $"state: {(animationDiagnostics.IsPaused ? "paused" : animationDiagnostics.IsTickSourceRunning ? "active" : "idle")}; " +
            $"platform scale: {platformAnimation.PlatformDurationScale:0.##}. " +
            app.PlatformServices.AnimationRuntimeStatus;
        greetingLabel.Text = $"Label bound to TextBox: Hello, {nameTextBox.Text}!";
        clickButton.Enabled = enabledCheckBox.Checked;
        permissionCheckButton.Enabled = platform.SupportsPermissionAction;
        permissionRequestButton.Enabled = platform.SupportsPermissionAction;
        settingsButton.Enabled = platform.SupportsPermissionAction;
        reducedMotionButton.Text = AnimationScheduler.Default.Policy.ReducedMotion
            ? "Reduced motion: on"
            : "Reduced motion: off";
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
        app.State.LastAction = requestPermission
            ? "Request camera: click received"
            : "Check camera: click received";
        app.State.LastServiceInvocation = requestPermission
            ? "IPermissionService.RequestAsync invoked"
            : "IPermissionService.CheckAsync invoked";
        app.State.LastServiceResult = "Pending";
        SetPermissionButtonsEnabled(false);
        RefreshStatus();
        try
        {
            var status = requestPermission
                ? await app.PlatformServices.RequestSamplePermissionAsync()
                : await app.PlatformServices.CheckSamplePermissionAsync();
            app.State.PermissionStatus = DescribePermissionStatus(status);
            app.State.LastInput = requestPermission ? "Camera permission requested" : "Camera permission checked";
            app.State.LastServiceResult = $"Completed: {app.State.PermissionStatus}";
        }
        catch (Exception exception)
        {
            app.State.PermissionStatus = $"Error: {exception.Message}";
            app.State.LastServiceResult = $"Failed: {exception.Message}";
        }
        finally
        {
            RefreshStatus();
            SetPermissionButtonsEnabled(app.PlatformServices.SupportsPermissionAction);
        }
    }

    private async Task OpenSettingsAsync()
    {
        app.State.LastAction = "Open app settings: click received";
        app.State.LastServiceInvocation = "IPermissionService.OpenApplicationSettingsAsync invoked";
        app.State.LastServiceResult = "Pending";
        SetPermissionButtonsEnabled(false);
        RefreshStatus();
        try
        {
            var opened = await app.PlatformServices.OpenApplicationSettingsAsync();
            app.State.LastInput = opened ? "Application settings opened" : "Application settings unavailable";
            app.State.LastServiceResult = opened ? "Completed: settings opened" : "Completed: unavailable";
        }
        catch (Exception exception)
        {
            app.State.LastInput = $"Settings error: {exception.Message}";
            app.State.LastServiceResult = $"Failed: {exception.Message}";
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
        SetRow(animationRuntimeLabel, margin, ref y, contentWidth, 54, gap);
        SetRow(effectButton, margin, ref y, contentWidth, 48, gap);
        var animationButtonWidth = Math.Max(150, Math.Min(220, (contentWidth - 12) / 2));
        foreach (var button in animationButtons.Controls.OfType<Button>())
            button.SetBounds(0, 0, animationButtonWidth, 38);
        SetRow(animationButtons, margin, ref y, contentWidth, contentWidth < 650 ? 92 : 46, gap);
        SetRow(animationStage, margin, ref y, contentWidth, 132, gap);
        SetRow(animationStatusLabel, margin, ref y, contentWidth, 54, gap);
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

    private void StartAnimationSmoke()
    {
        activeDemoRun?.Cancel();
        alternateAnimationTarget = !alternateAnimationTarget;
        animationCard.Bounds = alternateAnimationTarget
            ? new Rectangle(Math.Max(12, animationStage.Width - 190), 34, 166, 82)
            : new Rectangle(12, 18, 118, 72);
        activeDemoRun = Animation.Parallel(
                animationCard.RotateTo(alternateAnimationTarget ? 8f : -8f, SmokeAnimationOptions(620)),
                animationCard.ScaleTo(alternateAnimationTarget ? 1.08f : 0.96f, SmokeAnimationOptions(620)),
                animationCard.FadeTo(alternateAnimationTarget ? 0.72f : 1f, SmokeAnimationOptions(620)))
            .Start(AnimationScheduler.Default);
        animationStatusLabel.Text =
            "Started simultaneous LayoutTransition, transform, opacity, and state-capable effects. " +
            "Rotate or resize while it runs, then retarget rapidly.";
        RefreshStatus();
    }

    private void StopAnimationSmoke()
    {
        activeDemoRun?.Cancel();
        activeDemoRun = null;
        if (animationCard.LayoutTransition is { } transition)
        {
            transition.Enabled = false;
            transition.Enabled = true;
        }
        animationCard.Rotation = 0f;
        animationCard.ScaleX = 1f;
        animationCard.ScaleY = 1f;
        animationCard.Opacity = 1f;
        animationCard.Bounds = new Rectangle(12, 18, 118, 72);
        animationStatusLabel.Text = "Animations canceled and presentation state reset.";
        RefreshStatus();
    }

    private void ToggleAnimatedTheme()
    {
        darkTheme = !darkTheme;
        ThemeApplyResult result = ThemeManager.Current.Apply(
            darkTheme ? BuiltInThemes.Dark : BuiltInThemes.Light,
            new ThemeApplyOptions
            {
                Transition = new ThemeTransitionOptions
                {
                    Enabled = true,
                    Duration = TimeSpan.FromMilliseconds(650),
                    Easing = ThemeEasing.EaseInOut,
                    RespectReducedMotion = true
                }
            });
        animationStatusLabel.Text = result.Success
            ? $"Theme '{result.Snapshot!.Name}' committed; transition: {result.Transition?.State.ToString() ?? "immediate"}."
            : "Theme transition was rejected; inspect diagnostics.";
        RefreshStatus();
    }

    private void ToggleReducedMotion()
    {
        AnimationPolicy policy = AnimationScheduler.Default.Policy;
        policy.ReducedMotion = !policy.ApplicationReducedMotion;
        animationStatusLabel.Text = policy.ReducedMotion
            ? "Reduced motion enabled: active work must snap to exact targets and return idle."
            : "Application reduced motion disabled; Android system scale remains authoritative.";
        RefreshStatus();
    }

    private static void AddStateTransition(Button button, VisualState from, VisualState to)
        => button.StyleTransitions.Add(
            from,
            to,
            new VisualStateTransition
            {
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = Easings.CubicOut
            });

    private static AnimationOptions SmokeAnimationOptions(int milliseconds)
        => new()
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            Easing = Easings.CubicOut
        };

    private static Label CreateLabel(string text) => new() { Text = text };

    private static void SetRow(Control control, int x, ref int y, int width, int height, int gap)
    {
        control.SetBounds(x, y, width, height);
        y += height + gap;
    }
}
