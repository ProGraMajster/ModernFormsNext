using ModernFormsNext.Animations;

namespace ModernFormsNext.Validation;

/// <summary>Finite page assertions reused by separate real HWND and Android instrumentation runners.</summary>
/// <remarks>UI thread only. Native key delivery and waiting remain the responsibility of each runner.</remarks>
internal sealed class PageNavigationCertification : IDisposable
{
    private readonly Form form;
    private readonly Action<string> output;
    private readonly Panel container;
    private readonly List<ContentPage> created = [];
    private readonly ContentPage root;
    private ContentPage? detail;
    private bool veto;
    internal NavigationPage Navigation { get; }
    internal int LifecycleEvents { get; private set; }

    internal PageNavigationCertification(Form form, Action<string> output)
    {
        this.form = form;
        this.output = output;
        root = CreatePage("Native root");
        Navigation = new(root);
        container = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        container.Controls.Add(new PageHost { Page = Navigation, Dock = DockStyle.Fill });
        container.BringToFront();
        Check(root.LifecycleState == PageLifecycleState.Appeared && Navigation.IsPresented, "shown-host-lifecycle");
    }

    internal void ExerciseImmediate()
    {
        Editor(root).Select();
        form.SetTextInputActive(true);
        var client = form.TextInputClient;
        Check(Editor(root).Focused && client is not null, "canonical-native-editor");
        Check(client!.SetComposingText("retained"), "composition-start");
        veto = true;
        var next = CreatePage("Validated details");
        var rejected = Navigation.PushAsync(next, false);
        Check(rejected.IsCompletedSuccessfully && !rejected.Result, "validation-veto-task");
        Check(ReferenceEquals(Navigation.CurrentPage, root) && next.Parent is null && Editor(root).Focused,
            "validation-veto-history-focus-owner");
        Check(ReferenceEquals(client, form.TextInputClient) && client.GetState()?.HasComposition == true,
            "validation-veto-native-composition");
        veto = false;
        var accepted = Navigation.PushAsync(next, false);
        Check(accepted.IsCompletedSuccessfully && accepted.Result && Navigation.NavigationStack.Count == 2,
            "validation-accepted-history");
        Check(Editor(next).Focused && client.GetState() is null && form.TextInputClient is not null,
            "validation-native-text-handoff");
        var popped = Navigation.PopAsync(false);
        Check(popped.IsCompletedSuccessfully && ReferenceEquals(popped.Result, next) && next.Parent is null &&
            next.LifecycleState == PageLifecycleState.Detached, "pop-transfers-without-disposal");
        Check(Editor(root).Focused && root.IsPresented, "remembered-root-focus");
        output("PAGE_NAVIGATION immediate=PASS");
    }

    internal Task<bool> BeginAnimatedPush()
    {
        detail = CreatePage("Animated details");
        detail.Opacity = .7f;
        Editor(root).Select();
        return Navigation.PushAsync(detail);
    }

    internal void VerifyAnimatedPush(Task<bool> operation)
    {
        Check(operation.IsCompletedSuccessfully && operation.Result && ReferenceEquals(Navigation.CurrentPage, detail), "animated-push-completion");
        Check(detail!.LifecycleState == PageLifecycleState.Appeared && detail.Opacity == .7f && Editor(detail).Focused,
            "animated-state-authored-opacity-focus");
        var peer = Navigation.AccessibilityObject.GetChild(0)!.GetChild(0)!.GetChild(0)!;
        Check(ReferenceEquals(peer, detail.AccessibilityObject), "actual-current-semantic-page");
        Check(Editor(detail).AccessibilityObject.Bounds.Location == Editor(detail).PointToScreen(System.Drawing.Point.Empty),
            "native-semantic-editor-bounds");
    }

    internal void PrepareBack(bool cancel)
    {
        veto = cancel;
        Editor(detail!).Select();
    }

    internal void VerifyVetoedBack()
    {
        Check(form.Visible && ReferenceEquals(Navigation.CurrentPage, detail) && Editor(detail!).Focused,
            "native-Back-validation-veto");
    }

    internal Task BackCompletion => Navigation.BackCommand.ExecutionTask ?? throw new InvalidOperationException("Native Back did not reach the page command.");

    internal void VerifyBack()
    {
        Check(BackCompletion.IsCompletedSuccessfully && form.Visible && ReferenceEquals(Navigation.CurrentPage, root), "native-Back-pop-keeps-window");
        Check(detail!.Parent is null && detail.LifecycleState == PageLifecycleState.Detached && Editor(root).Focused,
            "native-Back-transfer-and-focus");
    }

    internal void VerifyRetainedPresentation()
    {
        Check(form.Visible && Navigation.NavigationStack.Count == 1 && root.IsPresented && Navigation.IsPresented,
            "native-lifetime-retains-page-history");
    }

    internal void ExerciseRepeatedOwnership()
    {
        for (int index = 0; index < 100; index++)
        {
            var next = CreatePage("Cycle " + index);
            var pushed = Navigation.PushAsync(next, false);
            Check(pushed.IsCompletedSuccessfully && pushed.Result, "cycle-push");
            var popped = Navigation.PopAsync(false);
            Check(popped.IsCompletedSuccessfully && ReferenceEquals(popped.Result, next) && next.Parent is null, "cycle-pop-transfer");
            next.Dispose();
        }
        Check(Navigation.NavigationStack.Count == 1 && root.IsPresented &&
            Navigation.AccessibilityObject.GetChild(0)!.GetChildCount() == 1, "bounded-current-semantic-presentation");
        output($"PAGE_NAVIGATION cycles=100 lifecycle={LifecycleEvents} activeAnimations={AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount}");
    }

    private ContentPage CreatePage(string title)
    {
        var body = new Panel { Padding = new Padding(12) };
        body.Controls.Add(new TextBox { Text = title, Bounds = new(12, 12, 280, 42), AccessibleName = title + " editor" });
        body.Controls.Add(new Label { Text = title + ": hosted page", Bounds = new(12, 70, 360, 34) });
        var page = new ContentPage { Title = title, Content = body, AccessibleName = title };
        Editor(page).Validating += (_, e) => e.Cancel = veto;
        page.Appearing += (_, _) => LifecycleEvents++;
        page.Appeared += (_, _) => LifecycleEvents++;
        page.Disappearing += (_, _) => LifecycleEvents++;
        page.Disappeared += (_, _) => LifecycleEvents++;
        created.Add(page);
        return page;
    }

    private static TextBox Editor(ContentPage page) => (TextBox)page.Content!.Controls[0];
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("PAGE_NAVIGATION " + category);
        // Repeated cases have a separate bounded aggregate, keeping native reports readable.
        if (!category.StartsWith("cycle-", StringComparison.Ordinal)) output("PASS PAGE_NAVIGATION " + category);
    }

    public void Dispose()
    {
        veto = false;
        form.Controls.Remove(container);
        container.Dispose();
        foreach (var page in created) if (page.Parent is null) page.Dispose();
        output("PAGE_NAVIGATION disposal=PASS");
    }
}
