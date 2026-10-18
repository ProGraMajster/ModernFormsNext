using ModernFormsNext.WindowKit.Backend.Android.Windowing;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Input;
using System.Drawing;
using WSize = ModernFormsNext.WindowKit.Size;
using ModernFormsNext.Animations;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

[Collection(AndroidTextInputUiCollection.Name)]
public sealed class AndroidWindowingContractTests
{
    [Theory]
    [InlineData(ApplicationLifetimeMode.MainWindowClosed)]
    [InlineData(ApplicationLifetimeMode.LastWindowClosed)]
    [InlineData(ApplicationLifetimeMode.Explicit)]
    public void ExternalRunReturnsAndRetainsLifetimeUntilClosure(ApplicationLifetimeMode mode)
    {
        using var f = new Fixture();
        using var form = new Form();
        int loads = 0, shown = 0, exits = 0;
        form.Load += (_, _) => loads++;
        form.Shown += (_, _) => shown++;
        Application.OnExit += (_, _) => exits++;
        Application.Run(form, mode);
        Assert.True(Dispatcher.UIThread.HasExternalEventLoop);
        Assert.False(Dispatcher.UIThread.SupportsRunLoops);
        Assert.Equal((1, 1, 0), (loads, shown, exits));
        Assert.Same(form, Assert.Single(Application.OpenForms));
        Assert.Equal(PlatformApplicationPhase.Running, Application.Lifecycle.Snapshot.Phase);
        Assert.Throws<InvalidOperationException>(() => Application.Run(form));
        form.Close();
        Assert.Empty(Application.OpenForms);
        Assert.Equal(mode == ApplicationLifetimeMode.Explicit ? 0 : 1, exits);
        Application.Exit();
        Assert.Equal(1, exits);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
        bool delivered = false;
        Dispatcher.UIThread.Post(() => delivered = true);
        f.Loop.Drain();
        Assert.True(delivered); // Framework shutdown did not terminate the platform loop.
    }

    [Fact]
    public void RecreationPreservesFormFocusAndRejectsOldHostDetach()
    {
        using var f = new Fixture();
        using var form = new Form();
        var editor = form.Controls.Add(new TextBox { Text = "kept", Width = 120, Height = 40 });
        form.Show();
        editor.Select();
        var oldClient = form.TextInputClient!;
        var window = (AndroidWindowImpl)form.window;
        Assert.NotNull(oldClient);
        long old = f.Generation;
        f.Platform.Detach(f.Host, old);
        Assert.True(form.Visible);
        Assert.Same(form, Assert.Single(Application.OpenForms));
        Assert.Null(oldClient.GetState());
        Assert.Equal("kept", editor.Text);
        Assert.True(editor.Focused);
        var replacement = new Host();
        long current = f.Platform.Attach(replacement);
        f.Platform.Detach(f.Host, old);
        Assert.True(f.Platform.IsCurrent(replacement, current));
        Assert.True(window.Attached);
        Assert.Null(form.TextInputClient); // Layout alone cannot confirm native input focus.
        replacement.Activate(window);
        Assert.NotSame(oldClient, form.TextInputClient);
        Assert.Equal("kept", form.TextInputClient!.GetState()!.Text);
    }

    [Fact]
    public void HideShowPreservesFormAndSingleOpenFormsEntry()
    {
        using var f = new Fixture();
        using var form = new Form();
        int load = 0, shown = 0, closed = 0;
        form.Load += (_, _) => load++;
        form.Shown += (_, _) => shown++;
        form.Closed += (_, _) => closed++;
        form.Show(); form.Hide(); form.Show(); form.Show();
        Assert.Equal((1, 1, 0), (load, shown, closed));
        Assert.Single(Application.OpenForms);
        form.Close(); form.Close();
        Assert.Equal(1, closed);
        Assert.Empty(Application.OpenForms);
        Assert.Equal(0, f.Host.Finishes);
    }

    [Fact]
    public void RepeatedShowKeepsTheLivePresentationEpoch()
    {
        using var f = new Fixture();
        using var form = new Form();
        form.Show();
        var window = (AndroidWindowImpl)form.window;
        long epoch = window.PresentationEpoch;
        form.Show();
        Assert.Equal(epoch, window.PresentationEpoch);
        Assert.True(window.Attached);
    }

    [Fact]
    public void BackAndReattachmentFollowModalShowOrderNotConstructionOrder()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var inner = new Form();
        using var outer = new Form();
        main.Show();
        var outerResult = outer.ShowDialog(main);
        var innerResult = inner.ShowDialog(outer);
        Assert.Same(inner.window, f.Platform.Windows.Last(w => w.Visible));
        f.Platform.Detach(f.Host, f.Generation);
        var replacement = new Host();
        f.Platform.Attach(replacement);
        Assert.Equal(new[] { main.window, outer.window, inner.window }, replacement.Presented);
        Assert.True(f.Platform.Back());
        Assert.True(innerResult.IsCompletedSuccessfully);
        Assert.False(outerResult.IsCompleted);
        Assert.True(outer.Visible);
    }

    [Fact]
    public void ReentrantShowFromModalClosureSupersedesAnOlderOwnerHide()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var modal = new Form();
        main.Show();
        _ = modal.ShowDialog(main);
        modal.Closed += (_, _) => main.Show();
        main.Hide();
        Assert.True(main.Visible);
        Assert.True(((AndroidWindowImpl)main.window).Attached);
        Assert.DoesNotContain(main.window, f.Host.Hidden);
    }

    [Fact]
    public void BackHonorsCancelAndClosesExactlyOnce()
    {
        using var f = new Fixture();
        using var form = new Form();
        int closing = 0, closed = 0;
        bool cancel = true;
        form.Closing += (_, e) => { closing++; e.Cancel = cancel; };
        form.Closed += (_, _) => closed++;
        Application.Run(form);
        Assert.True(f.Platform.Back());
        Assert.True(form.Visible);
        cancel = false;
        Assert.True(f.Platform.Back());
        Assert.Equal((2, 1), (closing, closed));
        Assert.Empty(Application.OpenForms);
        Assert.Equal(1, f.Host.Finishes);
    }

    [Fact]
    public async Task ModalAndPopupBackOrderUsesSharedLifecycle()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var dialog = new Form();
        main.Show();
        var result = dialog.ShowDialog(main);
        Assert.False(((AndroidWindowImpl)main.window).Enabled);
        using var popup = new PopupWindow(dialog) { Size = new System.Drawing.Size(100, 70) };
        popup.Show(10, 20);
        Assert.True(f.Platform.Back());
        Assert.False(popup.Visible);
        Assert.True(dialog.Visible);
        Assert.False(result.IsCompleted);
        dialog.DialogResult = DialogResult.OK;
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.OK, await result);
        Assert.True(((AndroidWindowImpl)main.window).Enabled);
        Assert.True(main.Visible);
        Assert.Single(Application.OpenForms);
    }

    [Fact]
    public void OwnerCloseForcesDescendantCleanupAndCompletesModalTask()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var dialog = new Form();
        main.Show();
        var task = dialog.ShowDialog(main);
        using var popup = new PopupWindow(dialog) { Size = new System.Drawing.Size(100, 70) };
        popup.Show(0, 0);
        main.Close();
        Assert.True(task.IsCompleted);
        Assert.False(dialog.Visible);
        Assert.False(popup.Visible);
        Assert.Empty(Application.OpenForms);
        Assert.Empty(f.Platform.Windows);
    }

    [Fact]
    public void UnsupportedSecondMainWindowRollsBackVisibilityWithoutDisablingOwner()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var other = new Form();
        main.Show();
        Assert.Throws<PlatformNotSupportedException>(other.Show);
        Assert.False(other.Visible);
        Assert.Single(Application.OpenForms);
        Assert.True(((AndroidWindowImpl)main.window).Enabled);
        Assert.Throws<PlatformNotSupportedException>(() => other.Location = new(5, 5));
        Assert.Equal(System.Drawing.Point.Empty, other.Location);
        Assert.Throws<PlatformNotSupportedException>(() => other.WindowState = FormWindowState.Maximized);
        Assert.Equal(FormWindowState.Normal, other.WindowState);
        Assert.Throws<PlatformNotSupportedException>(() => f.Platform.Attach(new Host { CanReplace = false }));
    }

    [Fact]
    public void ResizeAndInsetsUseConfirmedGeometryAndSingleSafeArea()
    {
        using var f = new Fixture();
        using var form = new Form();
        var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        form.Show();
        var window = (AndroidWindowImpl)form.window;
        Assert.False(form.TitleBar.Visible);
        form.Size = new(800, 900);
        Assert.Equal(new System.Drawing.Size(400, 300), form.Size); // Hint, not fabricated native state.
        window.ConfirmInsets(new WindowInsets(new Thickness(10, 20, 30, 40), new Thickness(0, 0, 0, 120)));
        Assert.Equal(new System.Drawing.Size(360, 240), form.ClientSize);
        Assert.Equal(new System.Drawing.Size(360, 240), fill.Size);
        Assert.Equal(120, form.Insets.Ime.Bottom); // IME remains informational.
        f.Host.Geometry(window, new(320, 240), 2);
        Assert.Equal(2, form.Scaling);
        Assert.Equal(new System.Drawing.Size(280, 180), form.ClientSize);
    }

    [Fact]
    public void ControlScreenBoundsIncludeNativeSafeAreaExactlyOnce()
    {
        using var f = new Fixture();
        using var form = new Form();
        var button = form.Controls.Add(new Button { Bounds = new(5, 7, 100, 40) });
        form.Show();
        var window = (AndroidWindowImpl)form.window;
        f.Host.Geometry(window, new(320, 240), 3);
        window.ConfirmInsets(new WindowInsets(new Thickness(10, 20, 0, 0), default));
        Assert.Equal(new System.Drawing.Point(45, 81), button.PointToScreen(System.Drawing.Point.Empty));
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        var input = (IWindowSurfaceInput)window.InputRoot!;
        input.Pointer(1, WindowSurfacePointerAction.Down, new(20, 32));
        input.Pointer(1, WindowSurfacePointerAction.Up, new(20, 32));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void CanonicalValidationCancelsNativeTouchAndRetiresOldTextSession()
    {
        using var f = new Fixture();
        using var form = new Form();
        var editor = form.Controls.Add(new TextBox { Text = "original", Bounds = new(5, 5, 140, 35) });
        var target = form.Controls.Add(new Button { Bounds = new(5, 60, 140, 40) });
        form.Show();
        editor.Select();
        bool cancel = true;
        int clicks = 0, validations = 0;
        editor.Validating += (_, e) => { validations++; e.Cancel = cancel; };
        target.Click += (_, _) => clicks++;
        var input = (IWindowSurfaceInput)((AndroidWindowImpl)form.window).InputRoot!;
        var client = form.TextInputClient!;
        void Tap()
        {
            input.Pointer(3, WindowSurfacePointerAction.Down, new(20, 75));
            input.Pointer(3, WindowSurfacePointerAction.Up, new(20, 75));
        }
        Tap();
        Assert.Equal((1, 0), (validations, clicks));
        Assert.True(editor.Focused);
        Assert.Same(client, form.TextInputClient);
        cancel = false;
        Tap();
        Assert.Equal((2, 1), (validations, clicks));
        Assert.True(target.Focused);
        Assert.Null(client.GetState());
    }

    [Fact]
    public void HardwareKeysIncludeFormBindingsAndPreview()
    {
        using var f = new Fixture();
        using var form = new Form();
        var editor = form.Controls.Add(new TextBox { Text = "seed" });
        form.Show(); editor.Select();
        int commands = 0, preview = 0, releases = 0;
        form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => commands++), new KeyGesture(Keys.F1)));
        form.KeyDown += (_, _) => preview++;
        form.KeyUp += (_, _) => releases++;
        var input = (IWindowSurfaceInput)((AndroidWindowImpl)form.window).InputRoot!;
        Assert.True(input.Key(Key.F1, WindowKit.Input.KeyModifiers.None, true, false, false, false));
        Assert.True(input.Key(Key.F1, WindowKit.Input.KeyModifiers.None, false, false, false, false));
        Assert.Equal((1, 1), (commands, preview));
        Assert.Equal(1, releases);
        Assert.Equal("seed", editor.Text);
    }

    [Fact]
    public void FormPreviewFocusChangeRoutesBindingToTheNewCanonicalOwner()
    {
        using var f = new Fixture();
        using var form = new Form();
        var first = form.Controls.Add(new TextBox());
        var second = form.Controls.Add(new TextBox { Top = 60 });
        int oldCalls = 0, newCalls = 0;
        first.InputBindings.Add(new KeyBinding(new DelegateCommand(() => oldCalls++), new KeyGesture(Keys.F1)));
        second.InputBindings.Add(new KeyBinding(new DelegateCommand(() => newCalls++), new KeyGesture(Keys.F1)));
        form.Show(); first.Select();
        form.KeyDown += (_, _) => second.Select();
        var input = (IWindowSurfaceInput)((AndroidWindowImpl)form.window).InputRoot!;
        input.Key(Key.F1, WindowKit.Input.KeyModifiers.None, true, false, false, false);
        Assert.Equal((0, 1), (oldCalls, newCalls));
        Assert.True(second.Focused);
    }

    [Fact]
    public void FailedShownObserverExitsAndCleansNativePresentation()
    {
        using var f = new Fixture();
        using var form = new Form();
        form.Shown += (_, _) => throw new InvalidOperationException("shown");
        Assert.Equal("shown", Assert.Throws<InvalidOperationException>(() => Application.Run(form)).Message);
        Assert.Empty(f.Platform.Windows);
        Assert.Empty(Application.OpenForms);
        Assert.Equal(1, f.Host.Finishes);
    }

    [Fact]
    public void HiddenOwnerKeepsPopupReusableAndClosesModalDescendants()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var popup = new PopupWindow(main);
        main.Show(); popup.Show(0, 0);
        main.Hide();
        Assert.False(popup.Visible);
        main.Show(); popup.Show(0, 0);
        Assert.True(popup.Visible);
        popup.Hide();
        using var modal = new Form();
        var result = modal.ShowDialog(main);
        main.Hide();
        Assert.True(result.IsCompletedSuccessfully);
        main.Show();
        Assert.True(((AndroidWindowImpl)main.window).Enabled);
    }

    [Fact]
    public void RejectedDesktopPropertiesKeepManagedValuesUnchanged()
    {
        using var f = new Fixture();
        using var form = new Form();
        Assert.Throws<PlatformNotSupportedException>(() => form.MinimumSize = new(20, 20));
        Assert.False(form.Resizeable);
        Assert.Throws<PlatformNotSupportedException>(() => form.Resizeable = true);
        Assert.Throws<PlatformNotSupportedException>(() => form.MaximumSize = new(200, 200));
        Assert.Equal(System.Drawing.Size.Empty, form.MinimumSize);
        Assert.Equal(System.Drawing.Size.Empty, form.MaximumSize);
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        Assert.Throws<PlatformNotSupportedException>(() => form.Image = bitmap);
        Assert.Null(form.Image);
    }

    [Fact]
    public void FormAccessibilityProjectsCanonicalOmittedContainerWithoutChangingParents()
    {
        using var f = new Fixture();
        using var form = new Form();
        var panel = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        var button = panel.Controls.Add(new Button { Text = "action" });
        form.Show();
        var host = (WindowKit.Platform.Accessibility.IPlatformAccessibilityHost)((AndroidWindowImpl)form.window).InputRoot!;
        var root = host.AccessibilityRoot!;
        Assert.Equal(1, root.GetChildCount());
        var child = root.GetChild(0)!;
        Assert.NotSame(root, child.Parent);
        Assert.Same(root, child.Parent!.Parent);
        Assert.Same(child, child.GetChild(0)!.Parent);
        using var session = new Accessibility.AndroidAccessibilitySession(host);
        session.Attach();
        Assert.Same(root, session.ProjectedParent(child));
        Assert.Single(session.Children(root));
        Assert.NotEqual(Accessibility.AndroidAccessibilitySession.InvalidId, session.Register(child.GetChild(0)!));
    }

    [Fact]
    public void ThrowingClosedObserverStillReleasesDescendantsAndCallbacks()
    {
        using var f = new Fixture();
        using var form = new Form();
        using var dialog = new Form();
        form.Show();
        var completion = dialog.ShowDialog(form);
        var window = (AndroidWindowImpl)form.window;
        form.Closed += (_, _) => throw new InvalidOperationException("observer");
        Assert.Throws<InvalidOperationException>(form.Close);
        Assert.True(completion.IsCompleted);
        Assert.Empty(Application.OpenForms);
        Assert.Empty(f.Platform.Windows);
        Assert.Null(window.InputRoot);
        Assert.Null(window.Closed);
        Assert.Equal(IntPtr.Zero, window.Handle.Handle);
    }

    [Fact]
    public void FailedReattachmentRollsBackOnlyTheFailedHost()
    {
        using var f = new Fixture();
        using var form = new Form();
        form.Show();
        f.Platform.Detach(f.Host, f.Generation);
        var failure = new Host { FailPresentation = true };
        Assert.Throws<InvalidOperationException>(() => f.Platform.Attach(failure));
        Assert.Null(f.Platform.Host);
        Assert.True(form.Visible);
        var replacement = new Host();
        long generation = f.Platform.Attach(replacement);
        Assert.True(f.Platform.IsCurrent(replacement, generation));
        Assert.True(((AndroidWindowImpl)form.window).Attached);
    }

    [Fact]
    public void RetiredPresentationAndTextAttachmentCannotEraseNewerOwnership()
    {
        using var f = new Fixture();
        using var form = new Form();
        form.Show();
        var window = (AndroidWindowImpl)form.window;
        long old = window.PresentationEpoch;
        form.Hide(); form.Show();
        window.RetirePresentation(old);
        Assert.True(window.Attached);
        Assert.True(window.Active);

        var proxy = new AndroidWindowTextInput(() => { });
        var first = new NativeText();
        var obsolete = new NativeText();
        var latest = new NativeText();
        proxy.Attach(first);
        first.OnClient = () => proxy.Attach(latest);
        proxy.Attach(obsolete);
        proxy.SetKeyboardVisible(true);
        Assert.Equal(0, obsolete.KeyboardRequests);
        Assert.Equal(1, latest.KeyboardRequests);
        latest.OnClient = () => proxy.Attach(null);
        proxy.Attach(obsolete);
        Assert.False(proxy.SetKeyboardVisible(true));
    }

    [Fact]
    public void SystemBackUsesCanonicalPageRouteThenRetainsLegacyRootClose()
    {
        using var f = new Fixture();
        using var form = new Form();
        var root = new ContentPage { Content = new TextBox { Text = "root" } };
        var navigation = new NavigationPage(root) { TransitionDuration = TimeSpan.Zero };
        form.Controls.Add(new PageHost { Page = navigation, Dock = DockStyle.Fill });
        form.Show();
        var next = new ContentPage { Content = new TextBox { Text = "next" } };
        Assert.True(Completed(navigation.PushAsync(next, false)));
        next.Content.Select();
        Assert.True(f.Platform.Back());
        Assert.True(form.Visible);
        Assert.Same(root, navigation.CurrentPage);
        Assert.Null(next.Parent);
        next.Dispose();
        Assert.True(f.Platform.Back());
        Assert.False(form.Visible);
    }

    [Fact]
    public void BusyNavigationConsumesBackInsteadOfClosingTheNativeWindow()
    {
        using var f = new Fixture();
        using var form = new Form();
        var root = new ContentPage { Content = new TextBox() };
        var navigation = new NavigationPage(root);
        form.Controls.Add(new PageHost { Page = navigation, Dock = DockStyle.Fill });
        form.Show();
        root.Content.Select();
        var next = new ContentPage { Content = new TextBox() };
        var pending = navigation.PushAsync(next);
        Assert.True(navigation.IsNavigating);
        Assert.True(f.Platform.Back());
        Assert.True(form.Visible);
        Assert.Same(next, navigation.CurrentPage);
        navigation.Dispose();
        f.Loop.Drain();
        Assert.True(pending.IsCompleted);
    }

    [Fact]
    public void BackPairsTheOriginalRouteAndConsumesReentrantDelivery()
    {
        using var f = new Fixture();
        using var form = new Form();
        int down = 0, up = 0, closing = 0;
        form.KeyDown += (_, e) =>
        {
            Assert.Equal(Keys.BrowserBack, e.KeyData);
            down++;
            Assert.True(f.Platform.Back());
            e.Handled = true;
        };
        form.KeyUp += (_, e) => { Assert.Equal(Keys.BrowserBack, e.KeyData); up++; };
        form.Closing += (_, _) => closing++;
        form.Show();
        Assert.True(f.Platform.Back());
        Assert.Equal((1, 1, 0), (down, up, closing));
        Assert.True(form.Visible);
    }

    [Fact]
    public void OldBackCannotReleaseOrCloseAReentrantReplacementPresentation()
    {
        using var f = new Fixture();
        using var form = new Form();
        bool replace = true;
        int up = 0;
        form.KeyDown += (_, _) => { if (replace) { replace = false; form.Hide(); form.Show(); } };
        form.KeyUp += (_, _) => up++;
        form.Show();
        Assert.True(f.Platform.Back());
        Assert.True(form.Visible);
        Assert.Equal(0, up);
        Assert.True(f.Platform.Back());
        Assert.Equal(1, up);
        Assert.False(form.Visible);
    }

    [Fact]
    public void PopupBackPrecedesNavigationAndModalRootFallbackPrecedesMain()
    {
        using var f = new Fixture();
        using var main = new Form();
        using var dialog = new Form();
        main.Show();
        var navigation = new NavigationPage(new ContentPage { Content = new TextBox() }) { TransitionDuration = TimeSpan.Zero };
        dialog.Controls.Add(new PageHost { Page = navigation, Dock = DockStyle.Fill });
        var modal = dialog.ShowDialog(main);
        using var next = new ContentPage { Content = new TextBox() };
        Assert.True(Completed(navigation.PushAsync(next, false)));
        next.Content.Select();
        using var popup = new PopupWindow(dialog) { Size = new System.Drawing.Size(100, 70) };
        popup.Show(0, 0);
        Assert.True(f.Platform.Back());
        Assert.False(popup.Visible);
        Assert.Same(next, navigation.CurrentPage);
        next.Content.Select();
        Assert.True(f.Platform.Back());
        Assert.False(modal.IsCompleted);
        Assert.Same(navigation.RootPage, navigation.CurrentPage);
        Assert.True(f.Platform.Back());
        Assert.True(modal.IsCompletedSuccessfully);
        Assert.True(main.Visible);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void BackReleaseStillRunsAndPreservesEveryInputCallbackFailure(bool throwDown, bool throwUp)
    {
        using var f = new Fixture();
        using var form = new Form();
        var downFailure = new InvalidOperationException("down");
        var upFailure = new ArgumentException("up");
        int down = 0, up = 0;
        form.KeyDown += (_, _) => { down++; if (throwDown) throw downFailure; };
        form.KeyUp += (_, _) => { up++; if (throwUp) throw upFailure; };
        form.Show();
        Exception? failure = Record.Exception(() => f.Platform.Back());
        Assert.Equal((1, 1), (down, up));
        Assert.True(form.Visible);
        if (throwDown && throwUp)
        {
            var aggregate = Assert.IsType<AggregateException>(failure);
            Assert.Equal(new Exception[] { downFailure, upFailure }, aggregate.InnerExceptions);
        }
        else Assert.Same(throwDown ? downFailure : (Exception)upFailure, failure);
        throwDown = throwUp = false;
        Assert.True(f.Platform.Back());
        Assert.False(form.Visible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackCannotCloseTheOwnerAfterItsCallbackPresentsANewModalOrPopup(bool popup)
    {
        using var f = new Fixture();
        using var main = new Form();
        using var dialog = new Form();
        main.Show();
        using var child = new PopupWindow(main) { Size = new System.Drawing.Size(100, 70) };
        bool open = true;
        int releases = 0;
        main.KeyDown += (_, _) =>
        {
            if (!open) return;
            open = false;
            if (popup) child.Show(0, 0); else _ = dialog.ShowDialog(main);
        };
        main.KeyUp += (_, _) => releases++;
        Assert.True(f.Platform.Back());
        Assert.True(main.Visible);
        Assert.True(popup ? child.Visible : dialog.Visible);
        Assert.Equal(1, releases);
        Assert.True(f.Platform.Back());
        Assert.False(popup ? child.Visible : dialog.Visible);
        Assert.True(main.Visible);
        Assert.True(f.Platform.Back());
        Assert.False(main.Visible);
    }

    private static T Completed<T>(Task<T> task)
    {
        Assert.True(task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    private sealed class NativeText : ITextInputMethod
    {
        internal Action? OnClient;
        internal int KeyboardRequests;
        public void SetClient(ITextInputClient? value) => OnClient?.Invoke();
        public bool SetKeyboardVisible(bool visible) { KeyboardRequests++; return true; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Stack<IDisposable> scopes = [];
        internal readonly ExternalLoop Loop = new();
        internal readonly AndroidWindowingPlatform Platform;
        internal readonly Host Host = new();
        internal readonly long Generation;
        internal Fixture()
        {
            scopes.Push(Application.PushRuntimeStateForTesting());
            scopes.Push(Dispatcher.PushUIThreadForTesting(Loop));
            var lifecycle = new PlatformApplicationLifecyclePublisher(Loop.Verify);
            scopes.Push(lifecycle);
            scopes.Push(PlatformServiceRegistry.PushServiceForTesting<IPlatformApplicationLifecycle>(lifecycle));
            // Each fake application lifetime needs its own production scheduler. Resolving
            // the process default would bind it to the first fixture and retain its Exited
            // state for later tests. No application runtime or duration policy is changed.
            var animationPlatform = new AnimationPlatform(Loop);
            var scheduler = new AnimationScheduler(animationPlatform, animationPlatform, animationPlatform,
                new AnimationPolicy(), lifecycle);
            scopes.Push(scheduler);
            scopes.Push(AnimationScheduler.PushDefaultForTesting(scheduler));
            Platform = new(Loop.Verify);
            scopes.Push(TestWindowFactoryScope.Push(Platform.CreateWindow));
            lifecycle.LifecycleChanged += (_, e) =>
            {
                if (e.Current.Phase == PlatformApplicationPhase.Exited) Platform.Shutdown();
            };
            Generation = Platform.Attach(Host);
        }
        public void Dispose()
        {
            try { Platform.Shutdown(); }
            finally { while (scopes.TryPop(out var scope)) scope.Dispose(); }
        }
    }
    private sealed class ExternalLoop : IExternallyOwnedDispatcherImpl
    {
        private readonly int thread = Environment.CurrentManagedThreadId;
        private bool pending;
        public bool CurrentThreadIsLoopThread => thread == Environment.CurrentManagedThreadId;
        public event Action? Signaled;
        public event Action? Timer;
        public long Now { get; private set; }
        public void Verify() { if (!CurrentThreadIsLoopThread) throw new InvalidOperationException("thread"); }
        public void Signal() => pending = true;
        public void UpdateTimer(long? dueTimeInMs) { }
        internal void Drain()
        {
            for (int i = 0; i < 100 && pending; i++) { pending = false; Signaled?.Invoke(); }
        }
        internal void Advance() { Now++; Timer?.Invoke(); Drain(); }
    }
    private sealed class AnimationPlatform(ExternalLoop loop) : IAnimationClock, IAnimationDispatcher, IAnimationTickSource
    {
        private Action? callback;
        public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(loop.Now);
        public bool CheckAccess() => loop.CurrentThreadIsLoopThread;
        public void Post(Action action) => Dispatcher.UIThread.Post(action);
        public bool IsRunning => callback is not null;
        public void Start(Action tickRequested) { Stop(); callback = tickRequested; loop.Timer += callback; }
        public void Stop() { if (callback is { } previous) loop.Timer -= previous; callback = null; }
        public void Dispose() => Stop();
    }
    private sealed class Host : IAndroidWindowHost
    {
        public bool CanReplace { get; init; } = false;
        internal int Finishes;
        internal readonly List<IWindowImpl> Presented = [];
        internal readonly List<IWindowImpl> Hidden = [];
        internal bool FailPresentation { get; init; }
        public void Present(AndroidWindowImpl w)
        {
            if (FailPresentation) throw new InvalidOperationException("presentation");
            Presented.Add(w);
            Geometry(w, new(400, 300), 1);
        }
        public void Hide(AndroidWindowImpl w) => Hidden.Add(w);
        public void Update(AndroidWindowImpl w) { }
        public void Activate(AndroidWindowImpl w) => w.ConfirmFocus(true);
        public void Invalidate(AndroidWindowImpl w) { }
        public void Clear() { }
        public void Finish() => Finishes++;
        internal void Geometry(AndroidWindowImpl w, WSize size, double density) =>
            w.ConfirmGeometry(size, density, default, new Screen(density, new PixelRect(0, 0, 800, 600),
                new PixelRect(0, 0, 800, 600), true), new PlatformHandle((IntPtr)42, "AndroidView"));
    }
}
