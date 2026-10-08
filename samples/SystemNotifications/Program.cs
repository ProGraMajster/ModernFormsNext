using ModernFormsNext;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Notifications;

namespace SystemNotificationSample;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--smoke")) System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        var backend = args.Contains("--shell") ? WindowsNotificationBackendKind.Shell : args.Contains("--classic") ? WindowsNotificationBackendKind.Classic : WindowsNotificationBackendKind.AppSdk;
        var service = WindowsSystemNotificationService.Register(new()
        {
            Backend = backend,
            AppUserModelId = "ProGraMajster.ModernFormsNext.NotificationSample",
            // Classic COM/shortcut registration is intentionally left to an installer; see README.
            ClassicActivatorId = args.Contains("--installed-classic") ? new Guid("8C8F893B-7B43-48CC-90BF-68D02C319EF3") : null
        });
        var form = new NotificationForm(service, args.Contains("--smoke"), args.Contains("--leave-notification"), args.Contains("--history"), args.Contains("--leave-basic"), args.Contains("--restart-seed"), args.Contains("--restart-finish"));
        if (args.Contains("--show-basic")) form.Load += async (_, _) => await form.ShowBasicAsync();
        Application.Run(form);
    }
}

internal sealed class NotificationForm : Form
{
    private readonly WindowsSystemNotificationService service;
    private readonly Label status;
    private readonly TextBox imagePath;
    private SystemNotificationKey? key;
    private int progressSteps = 1;
    private bool closing;

    internal NotificationForm(WindowsSystemNotificationService service, bool smoke, bool leaveNotification, bool history, bool leaveBasic, bool restartSeed, bool restartFinish)
    {
        this.service = service;
        Text = "ModernFormsNext — system notifications";
        Size = new System.Drawing.Size(820, 520);
        Controls.Add(new Label { Left = 20, Top = 20, Width = 760, Height = 55, Multiline = true, Text = $"Backend: {service.Capabilities.Backend}\n{service.Capabilities.Reason}" });
        Controls.Add(new Label { Left = 20, Top = 80, Width = 760, Height = 25, Text = "Thumbnail path (the file remains application-owned):" });
        imagePath = Controls.Add(new TextBox { Left = 20, Top = 110, Width = 760, Text = System.IO.Path.Combine(AppContext.BaseDirectory, "notification-image.png") });
        AddButton("Show download", 20, () => ShowDownload());
        AddButton("Update +10%", 215, () => Update());
        AddButton("Complete", 410, () => Complete());
        AddButton("Reply + choice", 605, () => Reply());
        AddButton("Read history", 20, async () => SetStatus(string.Join(", ", (await service.GetHistoryAsync()).Select(k => $"{k.Key?.Id ?? "native"}/{k.Key?.Group}"))), 210);
        AddButton("Dismiss current", 215, async () => { if (key is not null) SetStatus((await service.DismissAsync(key)).Status.ToString()); }, 210);
        AddButton("Clear sample", 410, async () => SetStatus((await service.ClearAsync()).Status.ToString()), 210);
        AddButton("Basic message", 605, () => Basic(), 210);
        status = Controls.Add(new Label { Left = 20, Top = 270, Width = 760, Height = 180, Multiline = true, Text = "Ready. Use the native banner or notification center to test activation.\nClose and click a retained notification to test cold launch." });
        service.Activated += (_, activation) =>
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Text = $"Notification activation: {activation.NotificationId}/{activation.ActionId ?? "body"}/{activation.UserInput.Count}";
            SetStatus($"Activated {activation.NotificationId}; action={activation.ActionId ?? "body"}; payload={activation.ActivationData}\n" +
                string.Join(", ", activation.UserInput.Select(p => $"{p.Key}={p.Value}")));
            Console.WriteLine($"ACTIVATION {activation.NotificationId} {activation.ActionId ?? "body"} INPUTS={activation.UserInput.Count}");
            // A downloader would resolve NotificationId in its own persisted queue before offering
            // Open/Open folder. Never execute a path supplied directly by the activation payload.
        };
        service.Changed += (_, change) => SetStatus($"Native event: {change.Reason}");
        Closing += async (_, e) =>
        {
            if (closing) return;
            e.Cancel = true;
            closing = true;
            try { await service.DisposeAsync(); }
            finally { Close(); }
        };
        if (restartSeed || restartFinish) Load += async (_, _) => await RestartSmoke(restartSeed);
        else if (smoke) Load += async (_, _) => await Smoke();
        else if (leaveNotification || history || leaveBasic) Load += async (_, _) =>
        {
            try
            {
                if (leaveNotification) await Reply();
                if (leaveBasic) await Basic();
                var entries = await service.GetHistoryAsync();
                Console.WriteLine("NATIVE_HISTORY " + string.Join(", ", entries.Select(k => $"{k.Key?.Id ?? "native"}/{k.Key?.Group}")));
                if (leaveNotification && !entries.Any(entry => entry.Key == new SystemNotificationKey("reply-42", "")))
                    throw new InvalidOperationException("The reply notification was not retained in native history.");
                if (leaveBasic && !entries.Any(entry => entry.Key == new SystemNotificationKey("basic-42", "")))
                    throw new InvalidOperationException("The basic notification was not retained in native history.");
            }
            catch (Exception e) { Console.WriteLine("NATIVE_HISTORY_FAIL " + e); Environment.ExitCode = 1; }
            finally { await service.DisposeAsync(); closing = true; Close(); }
        };
    }

    private SystemNotification Content() => new()
    {
        Id = "download-42", Group = "downloads", Title = "Downloading...", Message = "Example video",
        ActivationData = "download-42", Images = [new(imagePath.Text, SystemNotificationImageRole.Thumbnail, "Video thumbnail")],
        Actions = [new("Open", "open") { Id = "open" }, new("Open folder", "open-folder") { Id = "folder" }],
        Progress = new() { Value = .1, Title = "Example video", Status = "Downloading..."},
        PlatformOptions = new(new WindowsSystemNotificationOptions { Silent = true })
    };
    private async Task ShowDownload()
    {
        progressSteps = 1;
        var result = await service.ShowAsync(Content()); key = result.Key;
        SetStatus(Describe(result));
    }
    private async Task Update()
    {
        if (key is null) return;
        progressSteps++;
        var result = await service.UpdateAsync(key, new() { Value = Math.Min(progressSteps / 10d, 1), Status = "Downloading...", Title = "Example video"});
        SetStatus(Describe(result));
    }
    private async Task Complete()
    {
        var result = await service.ShowAsync(Content() with { Title = "Download complete", Progress = null });
        key = result.Key; SetStatus(Describe(result));
    }
    private async Task Reply()
    {
        var result = await service.ShowAsync(new()
        {
            Id = "reply-42", Title = "Reply from the notification", Message = "Type a message and choose a value.",
            Inputs = [new("reply") { Placeholder = "Your reply" }, new("choice") { Choices = [new("yes", "Yes"), new("no", "No")], DefaultValue = "yes" }],
            Actions = [new("Send", "send") { Id = "send", InputIds = ["reply", "choice"], PlatformOptions = new(new WindowsNotificationActionOptions { InputId = "reply" }) }]
        });
        SetStatus(Describe(result));
    }
    private async Task Basic()
    {
        var result = await service.ShowAsync(new()
        {
            Id = "basic-42", Title = "ModernFormsNext basic notification",
            Message = "A plain native message without images, buttons or inputs."
        });
        SetStatus(Describe(result));
    }
    internal Task ShowBasicAsync() => Basic();
    private void AddButton(string text, int left, Func<Task> action, int top = 160)
    {
        var button = Controls.Add(new Button { Text = text, Left = left, Top = top, Width = 175, Height = 36 });
        button.Click += async (_, _) =>
        {
            button.Enabled = false;
            try { await action(); }
            catch (Exception e) { SetStatus(e.Message); }
            finally { button.Enabled = true; }
        };
    }
    private void SetStatus(string value) { status.Text = value; Console.WriteLine(value); }
    private static string Describe(SystemNotificationResult result)
        => $"{result.Status}; {result.Key}; HRESULT={result.ErrorCode:X8}\n" + string.Join("\n", result.Warnings?.Select(w => $"{w.Code}: {w.Message}") ?? []);

    private async Task Smoke()
    {
        try
        {
            if (!service.Capabilities.IsSupported) throw new InvalidOperationException(service.Capabilities.Reason);
            var first = await service.ShowAsync(Content());
            Require(first.IsAccepted, "show", first);
            key = first.Key!;
            if (service.Capabilities.Backend == "Windows App SDK") await VerifyUntaggedHistory();
            else if (service.Capabilities.Backend.StartsWith("Classic", StringComparison.Ordinal) && service.Capabilities.Supports(SystemNotificationFeatures.History))
            {
                var reference = (WindowsSystemNotificationReference)(await service.GetHistoryAsync()).Single(entry => entry.Key == key).Reference;
                var untagged = await service.DismissHistoryAsync(reference with { Tag = "", Group = "" });
                Require(untagged.Status == SystemNotificationStatus.Unsupported, "classic untagged history limitation", untagged);
                Require((await service.GetHistoryAsync()).Any(entry => entry.Key == key), "unsupported history removal preserves content");
            }
            if (service.Capabilities.Supports(SystemNotificationFeatures.LiveUpdates))
            {
                Require((await service.GetHistoryAsync()).Any(entry => entry.Key == key), "native history after show");
                // Exercise rapid native submissions; service ordering must preserve the final value.
                for (int step = 1; step <= 20; step++)
                    Require((await service.UpdateAsync(key, new() { Value = step / 20d, Status = "Rapid updates" })).IsAccepted, "rapid progress");
                await VerifyNativeProgress(key, "1");
                // A later lower value is a new application state, not a stale native counter.
                var update = await service.UpdateAsync(key, new() { Value = .76, Status = "Downloading"});
                Require(update.IsAccepted, "native progress", update);
                await VerifyNativeProgress(key, "0.76");
                var indeterminate = await service.UpdateAsync(key, new() { Status = "Finalizing"});
                Require(indeterminate.IsAccepted, "native indeterminate", indeterminate);
                await VerifyNativeProgress(key, "indeterminate");
                var complete = await service.ShowAsync(Content() with { Progress = null, Title = "Download complete" });
                Require(complete.IsAccepted, "replace", complete);
                Require((await service.GetHistoryAsync()).Count(entry => entry.Key == key) == 1, "one native history identity");
            }
            var dismiss = await service.DismissAsync(key); Require(dismiss.IsAccepted, "dismiss", dismiss);
            if (service.Capabilities.Supports(SystemNotificationFeatures.History)) Require(!(await service.GetHistoryAsync()).Any(entry => entry.Key == key), "native history after remove");
            if (service.Capabilities.Supports(SystemNotificationFeatures.History | SystemNotificationFeatures.Grouping))
            {
                // Keep each run independent of history left by an interrupted earlier smoke.
                var sameTag = "smoke" + Guid.NewGuid().ToString("N")[..11];
                var ungrouped = await service.ShowAsync(new()
                {
                    Id = sameTag, Title = "Ungrouped",
                    Progress = service.Capabilities.Supports(SystemNotificationFeatures.Progress) ? new() { Value = .1, Status = "Working" } : null
                });
                var grouped = await service.ShowAsync(new() { Id = sameTag, Group = "other", Title = "Grouped" });
                Require(ungrouped.IsAccepted, "ungrouped setup", ungrouped);
                Require(grouped.IsAccepted, "grouped setup", grouped);
                var reserved = await service.ShowAsync(new() { Id = sameTag, Group = "mfn.default", Title = "Distinct logical group" });
                Require(reserved.IsAccepted, "logical reserved-name group remains distinct", reserved);
                if (service.Capabilities.Supports(SystemNotificationFeatures.LiveUpdates))
                    Require((await service.UpdateAsync(ungrouped.Key!, new() { Status = "Finishing"})).IsAccepted, "ungrouped indeterminate update");
                Require((await service.DismissAsync(ungrouped.Key!)).IsAccepted, "ungrouped dismissal");
                var history = await service.GetHistoryAsync();
                Require(!history.Any(entry => entry.Key == ungrouped.Key) && history.Any(entry => entry.Key == grouped.Key) && history.Any(entry => entry.Key == reserved.Key), "ungrouped dismissal preserves named group");
                Require((await service.DismissAsync(grouped.Key!)).IsAccepted, "cross-group cleanup");
                Require((await service.DismissAsync(reserved.Key!)).IsAccepted, "reserved-name group cleanup");
            }
            Console.WriteLine("NATIVE_SMOKE_PASS " + service.Capabilities.Backend);
        }
        catch (Exception e) { Console.WriteLine("NATIVE_SMOKE_FAIL " + e); Environment.ExitCode = 1; }
        finally { await service.DisposeAsync(); closing = true; Close(); }
    }
    // Direct SDK submission deliberately models notifications from another library. Only the
    // two IDs created here are cleaned up; unrelated entries under this sample's identity survive.
    private async Task VerifyUntaggedHistory()
    {
        var manager = Microsoft.Windows.AppNotifications.AppNotificationManager.Default;
        var before = (await service.GetHistoryAsync()).Select(entry => entry.Reference).ToHashSet();
        const string xml = "<toast><visual><binding template='ToastGeneric'><text>History reference smoke</text></binding></visual></toast>";
        var first = new Microsoft.Windows.AppNotifications.AppNotification(xml) { SuppressDisplay = true };
        var second = new Microsoft.Windows.AppNotifications.AppNotification(xml) { SuppressDisplay = true };
        try
        {
            manager.Show(first);
            manager.Show(second);
            Require(first.Id != 0 && second.Id != 0 && first.Id != second.Id, "independent native history IDs");
            var added = (await service.GetHistoryAsync()).Where(entry => !before.Contains(entry.Reference)).ToArray();
            Require(added.Length == 2 && added.All(entry => entry.Key is null), "untagged foreign history");
            Require((await service.DismissHistoryAsync(added[0].Reference)).IsAccepted, "untagged exact removal");
            var remaining = await manager.GetAllAsync();
            Require(remaining.Count(entry => entry.Id == first.Id || entry.Id == second.Id) == 1, "untagged removal preserves sibling");
            Require((await service.DismissHistoryAsync(added[1].Reference)).IsAccepted, "untagged sibling removal");
            Require(!(await manager.GetAllAsync()).Any(entry => entry.Id == first.Id || entry.Id == second.Id), "untagged cleanup");
        }
        finally
        {
            if (first.Id != 0) await manager.RemoveByIdAsync(first.Id);
            if (second.Id != 0) await manager.RemoveByIdAsync(second.Id);
        }
    }

    // The dedicated Windows sample inspects the actual OS data, not just Update's success code.
    private static async Task VerifyNativeProgress(SystemNotificationKey key, string expectedValue)
    {
        // OS persistence is asynchronous. Poll only this sample's entry with a bounded deadline;
        // a successful Update code alone is not evidence that the displayed data changed.
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            var native = Windows.UI.Notifications.ToastNotificationManager.History.GetHistory("ProGraMajster.ModernFormsNext.NotificationSample")
                .Single(entry => entry.Tag == WindowsToastContent.ToNativeTag(key.Id) && entry.Group == WindowsToastContent.ToNativeGroup(key.Group));
            if (native.Data is { } data && data.Values.TryGetValue("progressValue", out var value) && value == expectedValue) return;
            Require(elapsed.Elapsed < TimeSpan.FromSeconds(5), "retained native progress value");
            await Task.Delay(50);
        }
    }

    private async Task RestartSmoke(bool seed)
    {
        var persistedKey = new SystemNotificationKey("restart/download/" + new string('x', 80), "restart/conversation/" + new string('g', 80));
        try
        {
            Require(service.Capabilities.Supports(SystemNotificationFeatures.LiveUpdates), "restart progress capability");
            if (seed)
            {
                var result = await service.ShowAsync(new() { Id = persistedKey.Id, Group = persistedKey.Group, Title = "Restart progress probe", Progress = new() { Value = .1, Status = "Preparing" } });
                Require(result.IsAccepted, "restart seed", result);
                await VerifyNativeProgress(persistedKey, "0.1");
            }
            else
            {
                Require((await service.GetHistoryAsync()).Any(entry => entry.Key == persistedKey), "logical history after process restart");
                Require((await service.UpdateAsync(persistedKey, new() { Value = .6, Status = "Continued" })).IsAccepted, "restart update");
                await VerifyNativeProgress(persistedKey, "0.6");
                Require((await service.UpdateAsync(persistedKey, new() { Status = "Finishing" })).IsAccepted, "restart indeterminate");
                await VerifyNativeProgress(persistedKey, "indeterminate");
                Require((await service.DismissAsync(persistedKey)).IsAccepted, "restart removal");
                Require(!(await service.GetHistoryAsync()).Any(entry => entry.Key == persistedKey), "restart history after removal");
            }
            Console.WriteLine("NATIVE_RESTART_" + (seed ? "SEED" : "FINISH") + "_PASS");
        }
        catch (Exception e) { Console.WriteLine("NATIVE_RESTART_FAIL " + e); Environment.ExitCode = 1; }
        finally { await service.DisposeAsync(); closing = true; Close(); }
    }

    private static void Require(bool condition, string operation, SystemNotificationResult? result = null)
    { if (!condition) throw new InvalidOperationException(operation + ": " + result); }
}
