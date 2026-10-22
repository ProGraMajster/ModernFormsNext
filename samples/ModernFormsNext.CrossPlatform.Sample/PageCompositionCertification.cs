using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;

namespace ModernFormsNext.Validation;

/// <summary>Finite composition assertions shared by independent native Windows and Android runners.</summary>
/// <remarks>UI thread only. The runners provide native key delivery, display ticks, Activity changes and waiting.</remarks>
internal sealed class PageCompositionCertification : IDisposable
{
    private readonly Form form;
    private readonly Action<string> output;
    private readonly Panel container;
    private readonly List<ContentPage> created = [];
    private readonly ContentPage first, second, menu;
    private bool veto;
    internal TabbedPage Tabs { get; } = new();
    internal FlyoutPage Flyout { get; } = new() { DisplayMode = FlyoutDisplayMode.Overlay, FlyoutWidth = 220 };
    internal int LifecycleEvents { get; private set; }
    internal int ValidationCount { get; private set; }

    internal PageCompositionCertification(Form form, Action<string> output)
    {
        this.form = form; this.output = output;
        first = CreatePage("Native documents"); second = CreatePage("Native settings");
        menu = CreatePage("Native navigation", editorWidth: 180);
        Tabs.Pages.Add(first); Tabs.Pages.Add(second);
        for (int index = 0; index < 9; index++) Tabs.Pages.Add(CreatePage("Retained tab " + index));
        Flyout.Flyout = menu; Flyout.Detail = Tabs;
        container = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        container.Controls.Add(new PageHost { Page = Flyout, Dock = DockStyle.Fill });
        container.BringToFront();
        Check(first.IsPresented && Tabs.IsPresented && Flyout.IsPresented && !menu.IsPresented, "actual-hosted-composition");
    }

    internal void ExerciseImmediate()
    {
        Editor(first).Select(); form.SetTextInputActive(true);
        var client = form.TextInputClient;
        Check(Editor(first).Focused && client is not null, "canonical-native-editor");
        Check(client!.SetComposingText("retained"), "native-composition-start");
        veto = true;
        Check(!Tabs.TrySelectPage(second) && ReferenceEquals(Tabs.SelectedPage, first) && Editor(first).Focused, "tab-validation-veto");
        Check(ReferenceEquals(client, form.TextInputClient) && client.GetState()?.HasComposition == true, "veto-retains-native-composition");
        veto = false;
        Check(Tabs.TrySelectPage(second) && Editor(second).Focused && client.GetState() is null, "tab-native-text-handoff");
        Check(Tabs.TrySelectPage(first) && Editor(first).Focused, "tab-remembered-editor");
        var originalParent = first.Parent;
        int lifecycle = LifecycleEvents;
        Tabs.Pages.Move(1, 0);
        Check(ReferenceEquals(Tabs.SelectedPage, first) && ReferenceEquals(first.Parent, originalParent) && lifecycle == LifecycleEvents, "tab-move-retains-identity-lifecycle");
        Tabs.Pages.Move(0, 1);
        Check(Tabs.Pages.Remove(second) && second.Parent is null && second.LifecycleState == PageLifecycleState.Detached, "tab-remove-transfers");
        Check(Tabs.Pages.Add(second), "tab-readmit-transferred-page");
        foreach (var direction in new[] { FlowDirection.LeftToRight, FlowDirection.RightToLeft })
        {
            Tabs.FlowDirection = direction;
            var last = Tabs.Pages[^1];
            var peer = HeaderPeers().Single(item => item.Name == last.Title);
            Check(peer.PerformAction(AccessibleActions.Select) && ReferenceEquals(Tabs.SelectedPage, last), "native-tab-semantic-select-" + direction);
            Check(!peer.Bounds.IsEmpty && (peer.State & AccessibleStates.Offscreen) == 0, "native-tab-reveal-" + direction);
        }
        Tabs.FlowDirection = FlowDirection.LeftToRight;
        Tabs.TrySelectPage(first);
        Check(HeaderPeers().Count() == Tabs.Pages.Count && HeaderPeers().Count(item => (item.State & AccessibleStates.Selected) != 0) == 1, "canonical-header-cardinality");
        Check(ReferenceEquals(Editor(first).AccessibilityObject, first.AccessibilityObject.GetChild(0)!.GetChild(0)), "actual-editor-peer-identity");
        Check(Editor(first).AccessibilityObject.Bounds.Location == Editor(first).PointToScreen(System.Drawing.Point.Empty), "native-editor-semantic-bounds");
        output("PAGE_COMPOSITION immediate=PASS");
    }

    internal Task<bool> BeginAnimatedOpen()
    {
        veto = false;
        Flyout.DisplayMode = FlyoutDisplayMode.Overlay;
        Tabs.TrySelectPage(first);
        Editor(first).Select();
        menu.Opacity = .7f;
        return Flyout.SetFlyoutOpenAsync(true);
    }
    internal void VerifyAnimatedOpen(Task<bool> request)
    {
        Check(request.IsCompletedSuccessfully && request.Result && Flyout.IsFlyoutOpen, "native-scheduler-open-completion");
        Check(menu.LifecycleState == PageLifecycleState.Appeared && menu.Opacity == .7f && Editor(menu).Focused, "native-flyout-opacity-lifecycle-focus");
        Check(!first.Enabled && menu.Enabled && first.IsPresented, "overlay-owned-wrapper-availability");
    }
    internal void PrepareBack(bool cancel) { veto = cancel; Editor(menu).Select(); }
    internal void VerifyVetoedBack(int previousValidations)
    {
        Check(ValidationCount > previousValidations && Flyout.IsFlyoutOpen && form.Visible && Editor(menu).Focused, "native-dismiss-validation-veto");
    }
    internal void VerifyClosed()
    {
        Check(!Flyout.IsFlyoutOpen && form.Visible && Editor(first).Focused && first.Enabled, "native-dismiss-restores-detail");
        Check(menu.LifecycleState == PageLifecycleState.Attached && Tabs.Pages.Count == 11, "dismiss-retains-tab-and-flyout-pages");
    }
    internal void VerifySupersededOpening(Task<bool> opening)
    {
        Check(opening.IsCompletedSuccessfully && !opening.Result && !Flyout.IsFlyoutOpen && form.Visible, "busy-Back-supersedes-opening");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0 && menu.Opacity == .7f, "busy-dismiss-retires-effect");
    }

    internal void ExerciseModesAndOwnership()
    {
        veto = false;
        Flyout.TrySetFlyoutOpen(false);
        Flyout.DisplayMode = FlyoutDisplayMode.Split;
        Check(menu.IsPresented && first.IsPresented && first.Enabled && !Flyout.IsFlyoutOpen, "split-presents-both-slots");
        Flyout.FlowDirection = FlowDirection.RightToLeft;
        Tabs.FlowDirection = FlowDirection.LeftToRight;
        var menuHost = menu.Parent!.Parent!;
        var detailHost = Tabs.Parent!.Parent!;
        Check(menuHost.Bounds.Left >= detailHost.Bounds.Left && menuHost.Bounds.Right <= Flyout.Width, "native-right-leading-edge");
        Flyout.DisplayMode = FlyoutDisplayMode.Auto;
        Flyout.SplitBreakpoint = Math.Max(1, Flyout.Width + 1);
        Check(Flyout.EffectiveDisplayMode == FlyoutDisplayMode.Overlay, "auto-below-logical-breakpoint");
        Flyout.SplitBreakpoint = Math.Max(1, Flyout.Width);
        Check(Flyout.EffectiveDisplayMode == FlyoutDisplayMode.Split, "auto-at-logical-breakpoint");
        Flyout.DisplayMode = FlyoutDisplayMode.Overlay;
        Flyout.FlowDirection = FlowDirection.LeftToRight;
        Flyout.SplitBreakpoint = 720;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        for (int index = 0; index < 100; index++)
        {
            var page = CreatePage("Cycle " + index);
            if (index == 0) output("PAGE_COMPOSITION cycle-first-created");
            Check(Tabs.Pages.Add(page) && Tabs.TrySelectPage(page), "cycle-tab-add-select");
            if (index == 0) output("PAGE_COMPOSITION cycle-first-selected");
            Check(Tabs.Pages.Remove(page) && page.Parent is null, "cycle-tab-remove-transfer");
            if (index == 0) output("PAGE_COMPOSITION cycle-first-transferred");
            page.Dispose();
            Check(Flyout.TrySetFlyoutOpen(true) && Flyout.TrySetFlyoutOpen(false), "cycle-flyout-open-close");
            if ((index + 1) % 10 == 0) output($"PAGE_COMPOSITION cycle-progress={index + 1}/100 elapsedMs={elapsed.ElapsedMilliseconds}");
        }
        Tabs.TrySelectPage(first); Editor(first).Select();
        Check(Tabs.Pages.Count == 11 && HeaderPeers().Count() == 11 && !Flyout.IsFlyoutOpen, "bounded-retained-composition");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "zero-active-effects-after-cycles");
        output($"PAGE_COMPOSITION cycles=100 lifecycle={LifecycleEvents} activeAnimations=0 tabs={Tabs.Pages.Count}");
    }
    internal void VerifyRetainedPresentation()
    {
        Check(form.Visible && Flyout.IsPresented && Tabs.IsPresented && first.IsPresented && !Flyout.IsFlyoutOpen && Tabs.Pages.Count == 11,
            "native-lifetime-retains-composition");
    }

    private ContentPage CreatePage(string title, int editorWidth = 300)
    {
        var body = new Panel { Padding = new Padding(12) };
        var editor = body.Controls.Add(new TextBox { Text = title, Bounds = new(12, 12, editorWidth, 40), AccessibleName = title + " editor" });
        body.Controls.Add(new Label { Text = title + ": retained page", Bounds = new(12, 72, editorWidth, 54), Multiline = true });
        var page = new ContentPage { Title = title, Content = body, AccessibleName = title };
        editor.Validating += (_, e) => { ValidationCount++; e.Cancel = veto; };
        page.Appearing += (_, _) => LifecycleEvents++;
        page.Appeared += (_, _) => LifecycleEvents++;
        page.Disappearing += (_, _) => LifecycleEvents++;
        page.Disappeared += (_, _) => LifecycleEvents++;
        created.Add(page);
        return page;
    }
    private IEnumerable<AccessibleObject> HeaderPeers()
    {
        var pending = new Stack<AccessibleObject>(); pending.Push(Tabs.AccessibilityObject);
        var seen = new HashSet<AccessibleObject>();
        while (pending.TryPop(out var peer))
        {
            if (!seen.Add(peer)) throw new InvalidOperationException("PAGE_COMPOSITION duplicate semantic peer");
            if (peer.ControlType == AccessibleControlType.TabItem) yield return peer;
            for (int index = 0; index < peer.GetChildCount(); index++) if (peer.GetChild(index) is { } child) pending.Push(child);
        }
    }
    private static TextBox Editor(ContentPage page) => (TextBox)page.Content!.Controls[0];
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("PAGE_COMPOSITION " + category);
        if (!category.StartsWith("cycle-", StringComparison.Ordinal)) output("PASS PAGE_COMPOSITION " + category);
    }
    public void Dispose()
    {
        veto = false;
        form.Controls.Remove(container); container.Dispose();
        foreach (var page in created) if (page.Parent is null) page.Dispose();
        output("PAGE_COMPOSITION disposal=PASS");
    }
}
