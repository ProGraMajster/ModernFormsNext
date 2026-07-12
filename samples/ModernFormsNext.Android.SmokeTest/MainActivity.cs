using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.Android.SmokeTest;

/// <summary>
/// Hosts manual Android lifecycle, dispatcher, manifest, and runtime-permission checks.
/// </summary>
[Activity(
    Label = "ModernFormsNext Android Smoke Test",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private TextView statusView = null!;

    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this)
        {
            DiagnosticSink = message => global::Android.Util.Log.Info("MFN.WindowKit", message)
        });
        AndroidWindowKit.ObserveHostActivity(this);

        var content = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        var padding = (int)(16 * Resources!.DisplayMetrics!.Density);
        content.SetPadding(padding, padding, padding, padding);

        statusView = new TextView(this)
        {
            TextSize = 15
        };
        content.AddView(statusView, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        AddButton(content, "Check Camera", () => CheckAsync(PlatformPermission.Camera));
        AddButton(content, "Request Camera", () => RequestAsync(PlatformPermission.Camera));
        AddButton(content, "Check Microphone (NotDeclared)", () => CheckAsync(PlatformPermission.Microphone));
        AddButton(content, "Request Microphone (NotDeclared)", () => RequestAsync(PlatformPermission.Microphone));
        AddButton(content, "Check Notifications", () => CheckAsync(PlatformPermission.Notifications));
        AddButton(content, "Request Notifications", () => RequestAsync(PlatformPermission.Notifications));
        AddButton(content, "Open App Settings", OpenSettingsAsync);

        var scroll = new ScrollView(this);
        scroll.AddView(content);
        SetContentView(scroll);
        _ = RefreshStatusAsync("Backend initialized.");
    }

    /// <inheritdoc/>
    protected override void OnResume()
    {
        base.OnResume();
        AndroidWindowKit.ObserveHostActivity(this);
        if (statusView is not null)
            _ = RefreshStatusAsync("Activity resumed.");
    }

    /// <inheritdoc/>
    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        var handled = AndroidWindowKit.HandleRequestPermissionsResult(
            requestCode,
            permissions,
            grantResults);
        if (!handled)
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    private void AddButton(LinearLayout parent, string caption, Func<Task> action)
    {
        var button = new Button(this)
        {
            Text = caption
        };
        button.Click += async (_, _) => await RunUiOperationAsync(action);
        parent.AddView(button, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));
    }

    private async Task RunUiOperationAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (System.OperationCanceledException)
        {
            await RefreshStatusAsync("Operation canceled.");
        }
        catch (Exception exception)
        {
            await RefreshStatusAsync($"ERROR: {exception.Message}");
        }
    }

    private async Task CheckAsync(PlatformPermission permission)
    {
        var result = await AndroidWindowKit.Current.Permissions.CheckAsync(permission);
        await RefreshStatusAsync(Format(result));
    }

    private async Task RequestAsync(PlatformPermission permission)
    {
        var result = await AndroidWindowKit.Current.Permissions.RequestAsync(permission);
        await RefreshStatusAsync(Format(result));
    }

    private async Task OpenSettingsAsync()
    {
        var opened = await AndroidWindowKit.Current.Permissions.OpenApplicationSettingsAsync();
        await RefreshStatusAsync(opened ? "Application settings opened." : "No active Activity.");
    }

    private async Task RefreshStatusAsync(string lastOperation)
    {
        var backend = AndroidWindowKit.Current;
        var camera = await backend.Permissions.CheckAsync(PlatformPermission.Camera);
        var microphone = await backend.Permissions.CheckAsync(PlatformPermission.Microphone);
        var notifications = await backend.Permissions.CheckAsync(PlatformPermission.Notifications);
        var activityName = backend.ActivityTracker.CurrentActivity?.GetType().Name ?? "<none>";

        statusView.Text =
            $"Backend initialized: {backend.IsInitialized}\n" +
            $"Backend registry: {WindowKitBackendRegistry.Current?.PlatformName ?? "<none>"}\n" +
            $"Activity: {activityName}\n" +
            $"Lifecycle: {backend.ActivityTracker.State}\n" +
            $"Android SDK: {backend.PlatformInfo.SdkVersion} ({backend.PlatformInfo.Release})\n\n" +
            $"Camera: {camera.Status}\n" +
            $"Microphone: {microphone.Status}\n" +
            $"Notifications: {notifications.Status}\n\n" +
            $"Last operation:\n{lastOperation}";
    }

    private static string Format(PlatformPermissionResult result)
        => $"{result.Permission}: {result.Status} ({result.RequestKind})" +
           (string.IsNullOrWhiteSpace(result.DiagnosticMessage)
               ? string.Empty
               : $"\n{result.DiagnosticMessage}");
}
