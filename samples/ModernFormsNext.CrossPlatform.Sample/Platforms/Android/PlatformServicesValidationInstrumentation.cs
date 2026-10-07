using Android.App;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Runs explicit SAF/system-service acceptance checks on an Android emulator or device.</summary>
/// <remarks>
/// Invoke with adb instrumentation on a disposable test profile. Only synthetic documents and
/// notifications are created. UI automation runs on the instrumentation worker, never the Looper.
/// The runner never sends shared content to a recipient. English system UI is required.
/// </remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.PlatformServicesValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class PlatformServicesValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private MainActivity activity = null!;
    private string stage = "startup";
    private int assertions;
    private bool messagesOnly;
    /// <summary>Creates the runner.</summary>
    public PlatformServicesValidationInstrumentation() { }
    /// <summary>Reattaches the runner to the native instrumentation peer.</summary>
    /// <param name="handle">JNI peer handle.</param>
    /// <param name="ownership">Ownership transfer.</param>
    public PlatformServicesValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { messagesOnly = arguments?.GetString("mode") == "messages"; base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var bundle = new Bundle();
        bool success = false;
        try
        {
            _ = UiAutomation;
            using var launch = new Intent(TargetContext!, typeof(MainActivity));
            launch.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(launch)!;
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity), "resumed");
            Check(Ui(() => ModernFormsNext.Application.OpenForms.Single().Visible), "Application.Run-Form-startup");
            var main = Ui(() => ModernFormsNext.Application.OpenForms.Single());
            if (messagesOnly)
            {
                MessageDialogs(main);
                MessageShutdown(main);
                bundle.PutString("stream", report + $"RESULT=PASS assertions={assertions}\n");
                Finish(Result.Ok, bundle);
                return;
            }
            var storage = AvaloniaGlobals.GetRequiredService<IStorageProvider>();
            var launcher = AvaloniaGlobals.GetRequiredService<IPlatformLauncherService>();
            var sharing = AvaloniaGlobals.GetRequiredService<IPlatformShareService>();
            var notifications = AvaloniaGlobals.GetRequiredService<IPlatformNotificationService>();
            Check(Ui(() => storage.CanOpen && storage.CanSave && storage.CanPickFolder), "SAF-capabilities");

            stage = "open-cancel";
            var open = new OpenFileDialog();
            var canceled = Ui(() => open.ShowDialog(main));
            WaitSystem("documentsui");
            ReturnFromNative();
            Check(Await(canceled) == DialogResult.Cancel, "open-user-cancel");
            Resume();

            stage = "save-read-write";
            string filename = "mfn-79-" + DateTime.UtcNow.Ticks + ".txt";
            var save = new SaveFileDialog { FileName = filename, DefaultExtension = "txt" };
            save.AddFilter("Text", "*.txt");
            var saving = Ui(() => save.ShowDialog(main));
            WaitSystem("documentsui");
            Click("SAVE");
            Check(Await(saving) == DialogResult.OK, "save-native-create");
            Resume();
            using var file = save.SelectedFiles.Single();
            Check(file.Path.Scheme == "content" && save.FileName == file.Path.AbsoluteUri, "content-URI-facade");
            using (var stream = Await(file.OpenWriteAsync()))
            using (var writer = new StreamWriter(stream)) writer.Write("Synthetic issue 79 data");
            using (var stream = Await(file.OpenReadAsync()))
            using (var reader = new StreamReader(stream)) Check(reader.ReadToEnd() == "Synthetic issue 79 data", "save-write-read");
            var metadata = Await(file.GetBasicPropertiesAsync());
            Check(metadata.Size == 23 && metadata.DateCreated is null, "provider-metadata-not-invented");
            string? bookmark = Await(file.SaveBookmarkAsync());
            Check(bookmark is not null, "persistable-bookmark");
            using (var reopened = Await(storage.OpenFileBookmarkAsync(bookmark!)))
                Check(reopened is not null && reopened.Path == file.Path, "bookmark-reopen");
            Check(Await(storage.TryGetFileFromPathAsync(new Uri("relative", UriKind.Relative))) is null, "invalid-uri-null");
            Check(Await(storage.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop)) is null, "desktop-unsupported");

            stage = "open-select";
            var picking = Ui(() => open.ShowDialog(main));
            WaitSystem("documentsui");
            Click(filename);
            Check(Await(picking) == DialogResult.OK && open.SelectedFiles.Count == 1, "open-native-select");
            foreach (var selected in open.SelectedFiles) selected.Dispose();
            Resume();

            stage = "folder-cancel-select";
            var folders = new FolderBrowserDialog();
            var folderCancel = Ui(() => folders.ShowDialog(main));
            WaitSystem("documentsui");
            ReturnFromNative();
            Check(Await(folderCancel) == DialogResult.Cancel, "folder-user-cancel");
            Resume();
            var folderPick = Ui(() => folders.ShowDialog(main));
            WaitSystem("documentsui");
            // Open the storage root from the standard drawer, then select a synthetic directory.
            stage = "folder-roots"; Click("Show roots");
            stage = "folder-device"; Click("sdk_gphone64_x86_64");
            stage = "folder-documents"; Click("Documents");
            stage = "folder-synthetic"; Click("MFN-Issue79");
            stage = "folder-use"; Click("USE THIS FOLDER");
            stage = "folder-allow"; Click("ALLOW");
            Check(Await(folderPick) == DialogResult.OK, "folder-native-select");
            Resume();
            using var folder = folders.SelectedFolder!;
            using var child = Await(folder.CreateFileAsync("child.txt"));
            Check(child is not null && child.Path.Scheme == "content", "tree-create-child");
            using (var stream = Await(child!.OpenWriteAsync())) stream.WriteByte(42);
            var enumerated = Enumerate(folder).GetAwaiter().GetResult();
            Check(enumerated > 0, "tree-enumerate");
            Await(child.DeleteAsync());
            var folderBookmark = Await(folder.SaveBookmarkAsync());
            using (var reopened = Await(storage.OpenFolderBookmarkAsync(folderBookmark!)))
                Check(reopened is not null, "tree-bookmark-reopen");

            stage = "caller-cancellation";
            using (var cancel = new CancellationTokenSource())
            {
                var pending = Ui(() => storage.OpenFilePickerAsync(new() { CancellationToken = cancel.Token }));
                WaitSystem("documentsui");
                cancel.Cancel();
                try { Await(pending); Check(false, "caller-cancel"); }
                catch (System.OperationCanceledException) { Check(true, "caller-cancel"); }
                Check(Ui(() => AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "canceled-native-slot-retained");
                ReturnFromNative();
                Resume();
                Wait(() => Ui(() => !AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "canceled-slot-cleared");
            }

            stage = "recreation";
            var old = activity;
            var recreation = Ui(() => storage.OpenFilePickerAsync(new()));
            WaitSystem("documentsui");
            Ui(() => { old.Recreate(); return true; });
            Wait(() => old.IsDestroyed, "old-Activity-destroyed");
            ReturnFromNative();
            try { Await(recreation); Check(false, "recreation-request-failure"); }
            catch (PlatformServiceException e) { Check(e.Status == PlatformServiceStatus.HostLost, "recreation-request-HostLost"); }
            Resume();
            Check(!ReferenceEquals(activity, old), "replacement-Activity");
            Check(!Ui(() => AndroidWindowKit.HandleActivityResult(old, 8192, Result.Ok, null)), "old-Activity-stale-result-rejected");

            stage = "launcher";
            Check(Ui(() => launcher.OpenUri(new Uri("mfn-no-handler-test:synthetic"))) == PlatformServiceStatus.NoHandler, "no-handler-URI");
            Check(Ui(() => launcher.OpenUri(new Uri("tel:123"))) == PlatformServiceStatus.Success, "URI-dialer-handoff");
            ReturnToApp();
            Check(Ui(() => launcher.OpenFile(new PrivateFile())) == PlatformServiceStatus.NotSupported, "raw-file-external-rejected");

            stage = "share";
            var shared = Ui(() => sharing.ShareAsync(new() { Title = "Synthetic chooser", Text = "Synthetic issue 79 share" }));
            WaitSystem("android");
            ReturnFromNative();
            Check(Await(shared) == PlatformServiceStatus.Success, "share-chooser-return-no-delivery-claim");
            Resume();

            stage = "share-content";
            var sharedFile = Ui(() => sharing.ShareAsync(new() { Title = "Synthetic attachment", Items = [file.Path], MimeType = "text/plain" }));
            WaitSystem("android");
            ReturnFromNative();
            Check(Await(sharedFile) == PlatformServiceStatus.Success, "content-share-chooser-read-grant");
            Resume();

            stage = "message";
            var message = Ui(() => new MessageBoxForm("Synthetic message", "Framework message"));
            var messageTask = Ui(() => message.ShowDialog(main));
            Check(Ui(() => message.Visible), "framework-message-dialog-visible");
            Ui(() => { message.Close(); return true; });
            Await(messageTask);
            Ui(() => { message.Dispose(); return true; });

            MessageDialogs(main);

            stage = "notification-permission";
            var permissions = PlatformServiceRegistry.GetRequiredService<IPermissionService>();
            if (OperatingSystem.IsAndroidVersionAtLeast(33) && Await(permissions.CheckAsync(PlatformPermission.Notifications)).Status != PlatformPermissionStatus.Granted)
            {
                Check(notifications.Show(new("issue79", "Synthetic", "Denied")) == PlatformServiceStatus.PermissionDenied, "show-does-not-request-permission");
                var permissionTask = Ui(() => permissions.RequestAsync(PlatformPermission.Notifications));
                WaitSystem("permissioncontroller");
                // Permission UI ignores taps during its entrance protection interval.
                UiAutomation!.WaitForIdle(1000, 5000);
                Click("Allow");
                Check(Await(permissionTask).Status == PlatformPermissionStatus.Granted, "explicit-API33-permission");
                Resume();
            }
            using var manager = (NotificationManager)TargetContext!.GetSystemService(Context.NotificationService)!;
            Check(notifications.Show(new("issue79", "Synthetic", "First")) == PlatformServiceStatus.Success, "notification-show");
            Wait(() => manager.GetActiveNotifications()?.Any(n => n.Tag == "issue79") == true, "native-notification-present");
            Check(notifications.Show(new("issue79", "Synthetic updated", "Second")) == PlatformServiceStatus.Success, "notification-update");
            Check(manager.GetActiveNotifications()!.Count(n => n.Tag == "issue79") == 1, "stable-ID-replaces");
            Home();
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is null), "background-no-Activity");
            Check(notifications.Show(new("issue79", "Synthetic background", "Third")) == PlatformServiceStatus.Success, "notification-without-Activity");
            Check(Ui(() => launcher.OpenUri(new Uri("https://example.com"))) == PlatformServiceStatus.Unavailable, "launch-no-Activity");
            ReturnToApp();
            Check(notifications.Dismiss("issue79") == PlatformServiceStatus.Success, "notification-dismiss");
            Wait(() => manager.GetActiveNotifications()?.Any(n => n.Tag == "issue79") != true, "native-notification-removed");

            stage = "shutdown";
            var shutdownRequest = Ui(() => SystemMessageBox.ShowAsync(main, "Shutdown", "Native shutdown"));
            WaitMessage("Native shutdown", "Shutdown");
            Ui(() => { ModernFormsNext.Application.Exit(); return true; });
            try { Await(shutdownRequest); Check(false, "shutdown-request-failure"); }
            catch (PlatformServiceException e) { Check(e.Status == PlatformServiceStatus.Shutdown, "shutdown-completes-pending"); }
            Check(Ui(() => !AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "shutdown-no-pending-Task");
            Check(notifications.Show(new("issue79", "", "")) == PlatformServiceStatus.Shutdown, "notification-shutdown");
            Back();
            success = true;
        }
        catch (Exception e) { report.AppendLine("FAIL stage=" + stage + " type=" + e.GetType().Name + " detail=" + e.Message); }
        bundle.PutString("stream", report + $"RESULT={(success ? "PASS" : "FAIL")} assertions={assertions}\n");
        Finish(success ? Result.Ok : Result.Canceled, bundle);
    }
    private void MessageDialogs(Form main)
    {
        stage = "native-message-labels";
        // Isolated resource contexts exercise Android qualifier fallback without changing the
        // device locale, Activity configuration or the process-wide managed culture.
        foreach (string language in new[] { "en", "pl", "de", "fr", "ja" })
        {
            using var configuration = new global::Android.Content.Res.Configuration(TargetContext!.Resources!.Configuration!);
            using var locale = Java.Util.Locale.ForLanguageTag(language)!;
            configuration.SetLocale(locale);
            using var localized = TargetContext.CreateConfigurationContext(configuration)!;
            string[] labels = [
                localized.GetString(ModernFormsNext.WindowKit.Backend.Android.Resource.String.mfn_message_yes),
                localized.GetString(ModernFormsNext.WindowKit.Backend.Android.Resource.String.mfn_message_no),
                localized.GetString(ModernFormsNext.WindowKit.Backend.Android.Resource.String.mfn_message_retry)
            ];
            string[] expected = language == "pl" ? ["Tak", "Nie", "Ponów próbę"] : ["Yes", "No", "Retry"];
            Check(labels.SequenceEqual(expected), "native-label-resources-" + language);
        }
        stage = "native-message";
        string ok = TargetContext!.GetString(global::Android.Resource.String.Ok);
        string cancel = TargetContext.GetString(global::Android.Resource.String.Cancel);
        string yes = TargetContext.GetString(ModernFormsNext.WindowKit.Backend.Android.Resource.String.mfn_message_yes);
        string no = TargetContext.GetString(ModernFormsNext.WindowKit.Backend.Android.Resource.String.mfn_message_no);
        string retry = TargetContext.GetString(ModernFormsNext.WindowKit.Backend.Android.Resource.String.mfn_message_retry);
        var first = Ui(() => SystemMessageBox.ShowAsync(main, "Synthetic native body", "Native title"));
        WaitMessage("Native title", "Synthetic native body");
        Check(Ui(() => AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Count) == 1, "native-no-framework-window");
        var overlap = Ui(() => SystemMessageBox.ShowAsync(main, "", "Competing"));
        ExpectService(overlap, PlatformServiceStatus.Busy, "message-overlap-busy");
        var picker = Ui(() => AvaloniaGlobals.GetRequiredService<IStorageProvider>().OpenFilePickerAsync(new()));
        ExpectService(picker, PlatformServiceStatus.Busy, "picker-during-message-busy");
        Check(Await(Ui(() => AvaloniaGlobals.GetRequiredService<IPlatformShareService>().ShareAsync(new() { Text = "synthetic" })))
            == PlatformServiceStatus.Busy, "share-during-message-busy");
        Back(); UiAutomation!.WaitForIdle(500, 5000);
        Check(!first.IsCompleted, "OK-Back-noncancelable");
        Click(ok);
        Check(Await(first) == DialogResult.OK, "native-OK");
        Wait(() => Ui(() => main.IsActive && !AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "native-focus-restored");
        Check(true, "native-focus-activation-restored");

        foreach (var choice in new[] { (yes, DialogResult.Yes), (no, DialogResult.No) })
        {
            var task = Ui(() => SystemMessageBox.ShowAsync(main, "Yes or no", "Native choice", MessageBoxButtons.YesNo, MessageBoxIcon.Question));
            WaitMessage("Native choice", "Yes or no"); Click(choice.Item1);
            Check(Await(task) == choice.Item2, "native-" + choice.Item2);
        }
        var retryTask = Ui(() => SystemMessageBox.ShowAsync(main, "Retry", "Native retry", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error));
        WaitMessage("Native retry", "Retry"); Click(retry);
        Check(Await(retryTask) == DialogResult.Retry, "native-Retry");
        var cancelTask = Ui(() => SystemMessageBox.ShowAsync(main, "Cancel", "Native cancel", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning));
        WaitMessage("Native cancel", "Cancel"); Click(cancel);
        Check(Await(cancelTask) == DialogResult.Cancel, "native-Cancel-button");
        var backTask = Ui(() => SystemMessageBox.ShowAsync(main, "Back", "Native back", MessageBoxButtons.OKCancel));
        WaitMessage("Native back", "Back"); Back();
        Check(Await(backTask) == DialogResult.Cancel, "native-Back-Cancel");
        var outside = Ui(() => SystemMessageBox.ShowAsync(main, "Outside tap", "Native outside", MessageBoxButtons.OKCancel));
        WaitMessage("Native outside", "Outside tap");
        UiAutomation!.WaitForIdle(500, 5000);
        using (var root = UiAutomation.RootInActiveWindow)
        using (var titleBounds = FindBounds(root!, "Native outside")!)
        using (var descriptor = UiAutomation.ExecuteShellCommand($"input touchscreen tap {titleBounds.CenterX()} {titleBounds.Top / 2}")!)
        using (var stream = new ParcelFileDescriptor.AutoCloseInputStream(descriptor))
            while (stream.Read() != -1) { }
        Check(Await(outside) == DialogResult.Cancel, "native-outside-Cancel");

        using (var token = new CancellationTokenSource())
        {
            token.Cancel();
            var pre = Ui(() => SystemMessageBox.ShowAsync(main, "", "Must not show", cancellationToken: token.Token));
            ExpectCanceled(pre, "native-pre-cancel");
            Check(!Ui(() => AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "native-pre-cancel-no-slot");
        }
        using (var token = new CancellationTokenSource())
        {
            var task = Ui(() => SystemMessageBox.ShowAsync(main, "Cancel token", "Native token", MessageBoxButtons.YesNo, cancellationToken: token.Token));
            WaitMessage("Native token", "Cancel token"); token.Cancel();
            ExpectCanceled(task, "native-token-dismiss");
            Wait(() => Ui(() => !AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "native-token-slot-cleared");
        }

        var unowned = Ui(() => SystemMessageBox.ShowAsync(null, "Current Activity", "Native unowned", icon: MessageBoxIcon.Information));
        WaitMessage("Native unowned", "Current Activity"); Click(ok);
        Check(Await(unowned) == DialogResult.OK, "native-no-owner");
        var background = Ui(() => SystemMessageBox.ShowAsync(main, "Background resume", "Native background", MessageBoxButtons.OKCancel));
        WaitMessage("Native background", "Background resume"); Home();
        Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is null), "message-background");
        Check(!background.IsCompleted, "native-background-keeps-request");
        ReturnToApp(); WaitMessage("Native background", "Background resume"); Click(ok);
        Check(Await(background) == DialogResult.OK, "native-resume-result");
        Home();
        Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is null), "message-no-activity");
        ExpectService(Ui(() => SystemMessageBox.ShowAsync(null, "", "Unavailable")), PlatformServiceStatus.Unavailable, "native-no-Activity");
        ReturnToApp();

        stage = "native-message-recreation";
        var old = activity;
        var recreation = Ui(() => SystemMessageBox.ShowAsync(main, "Retire without replay", "Native recreate", MessageBoxButtons.OKCancel));
        WaitMessage("Native recreate", "Retire without replay");
        Ui(() => { old.Recreate(); return true; });
        ExpectService(recreation, PlatformServiceStatus.HostLost, "native-recreation-HostLost");
        Resume();
        Wait(() => old.IsDestroyed && !ReferenceEquals(activity, old), "message-replacement");
        Check(!Ui(() => AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "native-recreation-no-pending");
        var second = Ui(() => SystemMessageBox.ShowAsync(main, "Replacement dialog", "Native replacement"));
        WaitMessage("Native replacement", "Replacement dialog");
        Check(!Ui(() => AndroidWindowKit.HandleActivityResult(old, 8192, Result.Ok, null)), "native-stale-host-callback");
        Check(!second.IsCompleted, "native-replacement-still-pending");
        Click(ok); Check(Await(second) == DialogResult.OK, "native-second-after-recreation");

        stage = "native-message-rotation";
        var orientation = Ui(() => activity.RequestedOrientation);
        try
        {
            var rotation = Ui(() => SystemMessageBox.ShowAsync(main, "Rotate safely", "Native rotation", MessageBoxButtons.OKCancel));
            WaitMessage("Native rotation", "Rotate safely");
            Ui(() => { activity.RequestedOrientation = global::Android.Content.PM.ScreenOrientation.Landscape; return true; });
            Wait(() => Ui(() => activity.Resources!.Configuration!.Orientation == global::Android.Content.Res.Orientation.Landscape), "landscape");
            // The sample handles configuration in-place. Other hosts recreate; explicit
            // recreation above verifies retirement without replay.
            if (rotation.IsCompleted) ExpectService(rotation, PlatformServiceStatus.HostLost, "native-rotation-retired");
            else { WaitMessage("Native rotation", "Rotate safely"); Click(cancel); Check(Await(rotation) == DialogResult.Cancel, "native-rotation-in-place"); }
        }
        finally { Ui(() => { activity.RequestedOrientation = orientation; return true; }); }
        Check(!Ui(() => AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "native-final-no-pending");
    }

    private void MessageShutdown(Form main)
    {
        stage = "native-message-shutdown";
        var task = Ui(() => SystemMessageBox.ShowAsync(main, "Shutdown", "Native shutdown"));
        WaitMessage("Native shutdown", "Shutdown");
        Ui(() => { ModernFormsNext.Application.Exit(); return true; });
        ExpectService(task, PlatformServiceStatus.Shutdown, "native-shutdown");
        Check(!Ui(() => AndroidWindowKit.Current.GetServiceDiagnostics().NativeUiPending), "native-shutdown-no-pending");
    }
    private void WaitMessage(string title, string message)
    {
        Wait(() => {
            using var root = UiAutomation!.RootInActiveWindow;
            using var titleBounds = root is null ? null : FindBounds(root, title);
            using var messageBounds = root is null ? null : FindBounds(root, message);
            return titleBounds is not null && messageBounds is not null;
        }, "native-message-title-body");
    }
    private void ExpectService(Task task, PlatformServiceStatus status, string category)
    {
        try { Await(task); Check(false, category); }
        catch (PlatformServiceException e) { Check(e.Status == status, category); }
    }
    private void ExpectCanceled(Task task, string category)
    {
        try { Await(task); Check(false, category); }
        catch (System.OperationCanceledException) { Check(task.IsCanceled, category); }
    }

    private static async Task<int> Enumerate(IStorageFolder folder)
    {
        int count = 0;
        await foreach (var item in folder.GetItemsAsync()) { count++; item.Dispose(); }
        return count;
    }
    private T Ui<T>(Func<T> action)
    {
        T value = default!; Exception? error = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception e) { error = e; } });
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return value;
    }
    private T Await<T>(Task<T> task) { Wait(() => task.IsCompleted, "request-completed"); return task.GetAwaiter().GetResult(); }
    private void Await(Task task) { Wait(() => task.IsCompleted, "request-completed"); task.GetAwaiter().GetResult(); }
    private void Resume()
    {
        Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity), "foreground-resume");
        activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
    }
    private void ReturnFromNative()
    {
        // DocumentsUI Back first navigates its current folder stack. Bound the traversal,
        // allowing each system transition to settle before expecting the app to resume.
        for (int i = 0; i < 8; i++)
        {
            if (Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity)) return;
            using (var root = UiAutomation!.RootInActiveWindow)
            {
                // After recreation the framework surface can return before the replacement
                // Activity publishes Resume. Another Back here would close the application.
                if (root?.PackageName == TargetContext!.PackageName) { Resume(); return; }
            }
            string before = NativeWindowState();
            Back();
            string? observed = null;
            var stable = Stopwatch.StartNew();
            // Input injection can finish before the foreign Activity handles Back. Wait for
            // an observed hierarchy transition before another Back; WaitForIdle alone can
            // return against the old window and enqueue a second Back into our application.
            Wait(() =>
            {
                if (Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity)) return true;
                string state = NativeWindowState();
                if (state.Length == 0 || state == before) { observed = null; stable.Restart(); return false; }
                if (state != observed) { observed = state; stable.Restart(); return false; }
                return stable.Elapsed >= TimeSpan.FromMilliseconds(800);
            }, "native-return-transition", TimeSpan.FromSeconds(5));
        }
        Resume();
    }
    private string NativeWindowState()
    {
        using var root = UiAutomation!.RootInActiveWindow;
        if (root is null) return string.Empty;
        var state = new StringBuilder().Append(root.WindowId).Append(':').Append(root.PackageName);
        Append(root);
        return state.ToString();
        void Append(AccessibilityNodeInfo node)
        {
            if (node.VisibleToUser) state.Append('|').Append(node.Text).Append(':').Append(node.ContentDescription);
            for (int i = 0; i < node.ChildCount; i++)
            {
                using var child = node.GetChild(i);
                if (child is not null) Append(child);
            }
        }
    }
    private void ReturnToApp()
    {
        using var intent = new Intent(TargetContext!, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        TargetContext!.StartActivity(intent); Resume();
    }
    private void WaitSystem(string packageFragment) => Wait(() =>
    {
        using var root = UiAutomation!.RootInActiveWindow;
        return root?.PackageName?.Contains(packageFragment, StringComparison.OrdinalIgnoreCase) == true;
    }, "native-UI-" + packageFragment);
    private void Back() => SendKeyDownUpSync(global::Android.Views.Keycode.Back);
    private void Home() => SendKeyDownUpSync(global::Android.Views.Keycode.Home);
    private void Click(string text)
    {
        (int X, int Y)? previous = null;
        var stable = Stopwatch.StartNew();
        Wait(() =>
        {
            using var root = UiAutomation!.RootInActiveWindow;
            using var bounds = root is null ? null : FindBounds(root, text);
            if (bounds is null) { previous = null; stable.Restart(); return false; }
            var point = (bounds.CenterX(), bounds.CenterY());
            if (previous != point) { previous = point; stable.Restart(); return false; }
            // Accessibility can publish the destination before the entrance animation finishes.
            // Require a stable target before injection; permission UI also guards early taps.
            if (stable.Elapsed < TimeSpan.FromMilliseconds(800)) return false;
            using var descriptor = UiAutomation.ExecuteShellCommand($"input touchscreen tap {point.Item1} {point.Item2}")!;
            using var stream = new ParcelFileDescriptor.AutoCloseInputStream(descriptor);
            while (stream.Read() != -1) { }
            return true;
        }, "native-control-stable-click");
    }
    private static global::Android.Graphics.Rect? FindBounds(AccessibilityNodeInfo node, string text)
    {
        // Topmost drawers are last; do not activate an identically named breadcrumb behind them.
        for (int i = node.ChildCount - 1; i >= 0; i--)
        {
            using var child = node.GetChild(i);
            if (child is not null && FindBounds(child, text) is { } found) return found;
        }
        if (!node.VisibleToUser || !node.Enabled ||
            (!string.Equals(node.Text, text, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(node.ContentDescription, text, StringComparison.OrdinalIgnoreCase))) return null;
        var bounds = new global::Android.Graphics.Rect();
        node.GetBoundsInScreen(bounds);
        if (bounds.Width() > 0 && bounds.Height() > 0) return bounds;
        bounds.Dispose();
        return null;
    }
    private void Wait(Func<bool> condition, string category, TimeSpan? timeout = null)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < (timeout ?? TimeSpan.FromSeconds(25)))
        {
            if (condition()) return;
            Thread.Sleep(50); // Instrumentation worker only; never blocks Android's UI thread.
        }
        throw new TimeoutException(category);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++; report.AppendLine("PASS " + category);
        global::Android.Util.Log.Info("MFN.Services", "PASS " + category);
    }
    private sealed class PrivateFile : IStorageFile
    {
        public string Name => "private";
        public Uri Path => new("file:///data/private");
        public bool CanBookmark => false;
        public Task<StorageItemProperties> GetBasicPropertiesAsync() => throw new NotSupportedException();
        public Task<string?> SaveBookmarkAsync() => throw new NotSupportedException();
        public Task<IStorageFolder?> GetParentAsync() => throw new NotSupportedException();
        public Task DeleteAsync() => throw new NotSupportedException();
        public Task<IStorageItem?> MoveAsync(IStorageFolder destination) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync() => throw new NotSupportedException();
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public void Dispose() { }
    }
}
