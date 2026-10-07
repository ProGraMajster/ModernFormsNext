using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.CrossPlatform.Sample;

public sealed partial class MainPage
{
    private Label platformServicesTitle = null!;
    private Label platformServicesStatus = null!;
    private FlowLayoutPanel platformServicesButtons = null!;
    private void InitializePlatformServices()
    {
        platformServicesTitle = CreateLabel("Platform services");
        var shareSupported = AvaloniaGlobals.GetService<IPlatformShareService>()?.IsSupported == true;
        var notificationStatus = AvaloniaGlobals.GetService<IPlatformNotificationService>()?.Status ?? PlatformServiceStatus.NotSupported;
        platformServicesStatus = CreateLabel($"Sharing: {shareSupported}; notifications: {notificationStatus}.");
        platformServicesButtons = new FlowLayoutPanel { WrapContents = true };
        void Add(string text, Func<Task<string>> action)
        {
            var button = platformServicesButtons.Controls.Add(new Button { Text = text });
            button.Click += async (_, _) =>
            {
                button.Enabled = false;
                try { platformServicesStatus.Text = await action(); }
                catch (OperationCanceledException) { platformServicesStatus.Text = "Caller canceled."; }
                catch (PlatformServiceException e) { platformServicesStatus.Text = e.Status.ToString(); }
                catch (Exception e) { platformServicesStatus.Text = "Service error: " + e.GetType().Name; }
                finally { if (button.Parent is not null) button.Enabled = true; }
            };
        }
        Form Owner() => FindForm() ?? throw new InvalidOperationException("No owning Form.");
        Add("Open file", async () =>
        {
            var dialog = new OpenFileDialog { AllowMultiple = true };
            dialog.AddFilter("Text", "*.txt");
            var result = await dialog.ShowDialog(Owner());
            long bytes = 0;
            foreach (var file in dialog.SelectedFiles)
            {
                using (file)
                using (var stream = await file.OpenReadAsync())
                {
                    var buffer = new byte[1024];
                    bytes += await stream.ReadAsync(buffer);
                }
            }
            return $"{result}: {dialog.SelectedFiles.Count} file(s), read {bytes} sample bytes.";
        });
        Add("Save file", async () =>
        {
            var dialog = new SaveFileDialog { FileName = "mfn-services.txt", DefaultExtension = "txt" };
            dialog.AddFilter("Text", "*.txt");
            if (await dialog.ShowDialog(Owner()) != DialogResult.OK) return "User canceled.";
            using var file = dialog.SelectedFiles[0];
            using (var stream = await file.OpenWriteAsync())
            using (var writer = new StreamWriter(stream)) await writer.WriteAsync("ModernFormsNext synthetic document");
            using var read = await file.OpenReadAsync();
            using var reader = new StreamReader(read);
            return "Saved and read back: " + ((await reader.ReadToEndAsync()).Length > 0);
        });
        Add("Pick folder", async () =>
        {
            var dialog = new FolderBrowserDialog();
            var result = await dialog.ShowDialog(Owner());
            dialog.SelectedFolder?.Dispose();
            return "Folder: " + result;
        });
        Add("Open URI", () => Task.FromResult(
            AvaloniaGlobals.GetRequiredService<IPlatformLauncherService>().OpenUri(new Uri("https://example.com")).ToString()));
        Add("Share text", async () => (await AvaloniaGlobals.GetRequiredService<IPlatformShareService>().ShareAsync(
            new PlatformShareRequest { Title = "Share example", Text = "ModernFormsNext sample" })).ToString());
        Add("Notification permission", async () =>
        {
            var service = WindowKit.Backend.PlatformServiceRegistry.GetService<IPermissionService>();
            return service is null ? "Permission service unavailable." :
                (await service.RequestAsync(PlatformPermission.Notifications)).Status.ToString();
        });
        Add("Notification / update", () => Task.FromResult(AvaloniaGlobals.GetRequiredService<IPlatformNotificationService>()
            .Show(new PlatformNotification("sample-services", "ModernFormsNext", "Local sample notification " + DateTime.Now.ToShortTimeString())).ToString()));
        Add("Dismiss notification", () => Task.FromResult(AvaloniaGlobals.GetRequiredService<IPlatformNotificationService>().Dismiss("sample-services").ToString()));
        Add("Native message", async () => "Native result: " + await SystemMessageBox.ShowAsync(Owner(),
            "Continue with this sample action?", "System message", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question));
        Add("Framework message", async () =>
        {
            using var message = new MessageBoxForm("Platform services", "Framework message dialog on the existing Form host.");
            await message.ShowDialog(Owner());
            return "Message closed.";
        });
    }
}
