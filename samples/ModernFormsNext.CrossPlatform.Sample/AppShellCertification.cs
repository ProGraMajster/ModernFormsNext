using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Validation;

// Shared finite certification uses actual framework pages. Native hosts supply HWND/system Back,
// Activity lifecycle and text-method evidence; this fixture does not manufacture platform results.
internal sealed class AppShellCertification : IDisposable
{
    private readonly Form form;
    private readonly Action<string> output;
    private readonly Panel container;
    private readonly List<Page> created = [];
    private readonly AppShellLifecycleBinding binding;
    private readonly PlatformApplicationStateData? applicationState;
    private readonly ContentPage home;
    private NavigationPage? inner;
    private FlyoutPage? flyout;
    private ContentPage? nestedDetail;
    private TaskCompletionSource<Page>? slowSource;
    private Task<bool>? slowVisit;
    private CancellationToken slowToken;
    private ContentPage? late;
    private bool veto;
    private int factoryCalls;
    internal AppShell Shell { get; } = new();
    internal int LifecycleEvents { get; private set; }
    internal int Assertions { get; private set; }

    internal AppShellCertification(Form form, Action<string> output)
    {
        this.form = form; this.output = output;
        Shell.Routes.Register("home", _ => { factoryCalls++; return CreatePage("Shell home"); });
        Shell.Routes.Register("documents/{id}", context =>
        {
            factoryCalls++;
            return CreatePage("Document " + context.Parameters["id"] + " / " + context.Parameters.GetValueOrDefault("mode", "view"));
        });
        Shell.Routes.Register("workspace", _ => CreateWorkspace());
        Shell.Routes.Register("slow", (_, token) => { slowToken = token; return slowSource!.Task; });
        container = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        container.Controls.Add(new PageHost { Page = Shell, Dock = DockStyle.Fill });
        container.BringToFront();
        applicationState = Application.Lifecycle.SaveState(PlatformApplicationStateReason.Recreation);
        binding = Shell.AttachLifecycle(Application.Lifecycle, new Uri("mfn-cert://shell/"), "native", includeState: true);
        Check(Immediate(Shell.GoToAsync("/home", animated: false)), "initial-route");
        home = (ContentPage)Shell.CurrentPage!;
        Check(Shell.IsPresented && home.IsPresented, "actual-hosted-shell");
    }

    internal void ExerciseImmediate()
    {
        Editor(home).Select(); form.SetTextInputActive(true);
        var client = form.TextInputClient;
        Check(Editor(home).Focused && client is not null, "canonical-editor-focus");
        Check(client!.SetComposingText("shell-edit"), "composition-start");
        veto = true;
        Check(!Immediate(Shell.GoToAsync("/documents/42?mode=edit", animated: false)), "route-validation-veto");
        Check(ReferenceEquals(Shell.CurrentPage, home) && ReferenceEquals(form.TextInputClient, client) && client.GetState()?.HasComposition == true,
            "veto-retains-page-native-text");
        veto = false;
        Check(Immediate(Shell.GoToAsync("/documents/42?mode=edit", animated: false)), "route-admission");
        Check(Shell.CurrentRoute!.Parameters["id"] == "42" && Shell.CurrentRoute.Parameters["mode"] == "edit", "copied-route-parameters");
        Check(Editor((ContentPage)Shell.CurrentPage!).Focused && client.GetState() is null, "canonical-native-text-handoff");
        int calls = factoryCalls;
        Check(Immediate(Shell.GoToAsync("/documents/42?mode=edit", animated: false)) && calls == factoryCalls && Shell.NavigationStack.Count == 2,
            "duplicate-route-retains-page-and-history");
        Check(ReferenceEquals(Editor((ContentPage)Shell.CurrentPage!).AccessibilityObject,
            Shell.CurrentPage!.AccessibilityObject.GetChild(0)!.GetChild(0)), "actual-editor-semantic-identity");
    }

    internal void PrepareNestedBack()
    {
        Check(Immediate(Shell.GoToAsync("/workspace", animated: false)), "workspace-route");
        nestedDetail = CreatePage("Nested detail");
        Check(Immediate(inner!.PushAsync(nestedDetail, animated: false)), "inner-push");
        Editor(nestedDetail).Select();
    }
    internal void VerifyInnerBack()
    {
        Check(inner!.NavigationStack.Count == 1 && Shell.CurrentRoute!.Route == "/workspace" && Shell.NavigationStack.Count == 3,
            "native-Back-nearest-inner-history");
        Check(nestedDetail!.Parent is null && nestedDetail.LifecycleState != PageLifecycleState.Disposed, "inner-pop-transfers-page");
        nestedDetail.Dispose();
    }
    internal void OpenFlyout()
    {
        Check(flyout!.TrySetFlyoutOpen(true), "open-workspace-flyout");
        Editor((ContentPage)flyout.Flyout!).Select();
    }
    internal void VerifyFlyoutBack()
    {
        Check(!flyout!.IsFlyoutOpen && Shell.CurrentRoute!.Route == "/workspace" && Shell.NavigationStack.Count == 3,
            "native-Back-dismisses-flyout-before-shell");
        Editor((ContentPage)inner!.CurrentPage!).Select();
    }
    internal void VerifyOuterBack()
    {
        Check(Shell.CurrentRoute!.Route == "/documents/42?mode=edit" && Shell.NavigationStack.Count == 2 && flyout!.Parent is null,
            "native-Back-pops-shell-and-transfers-workspace");
    }
    internal void BeginSlow()
    {
        ReturnToRoot();
        slowSource = new(); late = CreatePage("Late borrowed result");
        slowVisit = Shell.GoToAsync("/slow", animated: false);
        Check(!slowVisit.IsCompleted && Shell.PendingRequestCount == 1, "pending-route-factory");
        Editor((ContentPage)Shell.CurrentPage!).Select();
    }
    internal void VerifyPendingBack()
    {
        Check(slowToken.IsCancellationRequested && Shell.CurrentRoute!.Route == "/home" && form.Visible,
            "native-Back-cancels-uncommitted-factory");
    }
    internal Task<bool> CompleteSlow() { slowSource!.SetResult(late!); return slowVisit!; }
    internal void VerifyLate()
    {
        Check(slowVisit!.IsCompletedSuccessfully && !slowVisit.Result && late!.Parent is null && late.LifecycleState != PageLifecycleState.Disposed,
            "late-factory-does-not-reclaim-or-dispose-page");
        Check(Shell.PendingRequestCount == 0, "pending-slots-released");
    }
    internal Task<bool> DeliverLinks()
    {
        Check(Immediate(Shell.GoToAsync("/documents/42?mode=edit", animated: false)), "prepare-activation-history");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Protocol, uri: new Uri("mfn-cert://shell/documents/99?mode=linked")));
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("mfn-cert://shell/documents/100?mode=linked")));
        return binding.LastOperation!;
    }
    internal void VerifyLinks(Task<bool> delivery)
    {
        Check(delivery.IsCompletedSuccessfully && delivery.Result && Shell.CurrentRoute!.Route == "/documents/100?mode=linked",
            "canonical-lifecycle-latest-activation");
    }
    internal void ExerciseStateAndCycles()
    {
        var handedOff = Application.Lifecycle.SaveState(PlatformApplicationStateReason.Recreation);
        // The Android sample already supplies application-owned state. The binding must preserve
        // that earlier assignment; an explicit shell capture can still test fresh-shell restoration.
        if (applicationState is not null)
            Check(handedOff is not null && handedOff.Version == applicationState.Version && handedOff.Values.Count == applicationState.Values.Count &&
                applicationState.Values.All(pair => handedOff.Values.TryGetValue(pair.Key, out var value) && value == pair.Value), "canonical-application-state-override-preserved");
        else Check(handedOff?.Values.GetValueOrDefault("mfn.shell.native.count") == "3", "canonical-bounded-state-handoff");
        var saved = Shell.CaptureState("native");
        Check(saved.Version == 1 && saved.Values["mfn.shell.native.count"] == "3", "explicit-bounded-shell-state");
        Check(!Immediate(Shell.RestoreStateAsync(saved, "native")), "retained-user-history-preserved");
        using (var fresh = new AppShell())
        {
            fresh.Routes.Register("home", _ => CreatePage("Restored home"));
            fresh.Routes.Register("documents/{id}", context => CreatePage("Restored " + context.Parameters["id"]));
            Check(Immediate(fresh.RestoreStateAsync(saved, "native")) && fresh.NavigationStack.Count == 3 && fresh.CurrentRoute!.Route == Shell.CurrentRoute!.Route,
                "fresh-shell-route-only-restoration");
        }
        for (int index = 0; index < 100; index++)
        {
            Check(Immediate(Shell.GoToAsync("/documents/" + index + "?mode=cycle", animated: false)), "cycle-route-push");
            var removed = Shell.GoBackAsync(animated: false);
            Check(removed.IsCompletedSuccessfully && removed.Result is { Parent: null }, "cycle-route-transfer");
            removed.Result!.Dispose();
        }
        Check(Shell.NavigationStack.Count == 3 && Shell.PendingRequestCount == 0 && AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0,
            "bounded-history-and-zero-effects-after-100-cycles");
        Editor((ContentPage)Shell.CurrentPage!).Select();
        output($"APP_SHELL cycles=100 assertions={Assertions} lifecycle={LifecycleEvents} activeAnimations=0");
    }
    internal void VerifyRetainedPresentation()
    {
        Check(form.Visible && Shell.IsPresented && Shell.CurrentPage!.IsPresented && Shell.CurrentRoute!.Route == "/documents/100?mode=linked" &&
            Shell.NavigationStack.Count == 3, "native-lifecycle-retains-shell-history");
    }
    internal void ReturnToRoot()
    {
        while (Shell.NavigationStack.Count > 1)
        {
            var removed = Shell.GoBackAsync(animated: false);
            Check(removed.IsCompletedSuccessfully && removed.Result is not null, "root-preparation-pop");
            removed.Result!.Dispose();
        }
        Editor(home).Select();
    }
    private FlyoutPage CreateWorkspace()
    {
        inner = new NavigationPage(CreatePage("Inner root")) { TransitionDuration = TimeSpan.Zero };
        created.Add(inner);
        var tabs = new TabbedPage(); tabs.Pages.Add(inner); tabs.Pages.Add(CreatePage("Settings")); created.Add(tabs);
        flyout = new FlyoutPage { Detail = tabs, Flyout = CreatePage("Workspace menu"),
            DisplayMode = FlyoutDisplayMode.Overlay, TransitionDuration = TimeSpan.Zero };
        created.Add(flyout); return flyout;
    }
    private ContentPage CreatePage(string title)
    {
        var body = new Panel { Padding = new Padding(12) };
        var editor = body.Controls.Add(new TextBox { Text = title, AccessibleName = title + " editor", Bounds = new(12, 12, 280, 40) });
        body.Controls.Add(new Label { Text = title + "\nAppShell uses the existing route, focus and page hosts.", Multiline = true, Bounds = new(12, 72, 440, 80) });
        editor.Validating += (_, args) => args.Cancel = veto;
        var page = new ContentPage { Title = title, Content = body };
        page.Appearing += (_, _) => LifecycleEvents++;
        page.Appeared += (_, _) => LifecycleEvents++;
        page.Disappearing += (_, _) => LifecycleEvents++;
        page.Disappeared += (_, _) => LifecycleEvents++;
        created.Add(page); return page;
    }
    private static TextBox Editor(ContentPage page) => (TextBox)page.Content!.Controls[0];
    private static bool Immediate(Task<bool> task) => task.IsCompletedSuccessfully ? task.Result :
        throw new InvalidOperationException("APP_SHELL immediate operation did not succeed/settle: " + task.Exception);
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("APP_SHELL " + category);
        Assertions++;
        if (!category.StartsWith("cycle-", StringComparison.Ordinal)) output("PASS APP_SHELL " + category);
    }
    public void Dispose()
    {
        veto = false; binding.Dispose();
        form.Controls.Remove(container); container.Dispose();
        foreach (var page in created) if (page.Parent is null) page.Dispose();
        created.Clear(); output("APP_SHELL disposal=PASS");
    }
}
