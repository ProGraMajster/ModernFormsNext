using System.Drawing;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using Xunit;
using Rect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext.Testing.Tests;

public sealed class TextInputSessionTests
{
    [Fact]
    public void SelectingAnEditorDuringWindowSetupDoesNotAcquireNativeTextOwnership()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        editor.Select();
        Assert.Null(form.TextInputClient);
        Assert.False(form.TextInputDiagnostics.IsActive);

        host.Show(form);

        Assert.NotNull(form.TextInputClient);
        Assert.True(form.TextInputDiagnostics.IsActive);
    }

    [Fact]
    public void CompositionHelpersUseTheRealEditorAndRetiredFocusCannotRetarget()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var first = root.Controls.Add(new TextBox());
        var second = root.Controls.Add(new TextBox());
        host.Show(root);
        Assert.True(host.Input.Focus(first));
        var old = Assert.IsAssignableFrom<ITextInputClient>(host.Input.TextInputClient);
        Assert.True(host.Input.SetComposingText("zaż"));
        Assert.True(old.GetState()!.HasComposition);

        Assert.True(host.Input.Focus(second));
        Assert.Null(old.GetState());
        Assert.False(old.CommitText("obsolete"));
        Assert.Equal("zaż", first.Text);
        Assert.Empty(second.Text);
        Assert.True(host.Input.CommitComposition("😀"));
        Assert.Equal("😀", second.Text);
        Assert.True(host.Input.Focus(first));
        Assert.False(host.Input.TextInputClient!.GetState()!.HasComposition);
        Assert.NotSame(old, host.Input.TextInputClient);
    }

    [Fact]
    public void RemovedAncestorRetiresBeforeCallbacksAndReattachmentGetsFreshSession()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var nested = root.Controls.Add(new Panel());
        var editor = nested.Controls.Add(new TextBox());
        host.Show(root);
        host.Input.Focus(editor);
        var old = host.Input.TextInputClient!;
        old.SetComposingText("日本");
        bool callbackRejected = false;
        nested.ParentChanged += (_, _) => {
            if (nested.Parent is null) callbackRejected = !old.CommitText("late");
        };

        root.Controls.Remove(nested);

        Assert.True(callbackRejected);
        Assert.Null(old.GetState());
        Assert.Equal("日本", editor.Text);
        Assert.Null(host.Input.TextInputClient);
        root.Controls.Add(nested);
        var current = host.Input.TextInputClient;
        Assert.NotNull(current);
        Assert.NotSame(old, current);
        Assert.False(current.GetState()!.HasComposition);
    }

    [Fact]
    public void ModalDialogRetiresOwnerAndRestoresFreshSessionAfterCanceledThenAcceptedClose()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(editor);
        var old = owner.Input.TextInputClient!;
        old.SetComposingText("visible");
        var dialogForm = new Form();
        dialogForm.Controls.Add(new TextBox());
        var dialog = host.ShowDialog(dialogForm, owner);
        bool cancel = true;
        dialogForm.Closing += (_, e) => e.Cancel = cancel;

        Assert.False(old.CommitText("late"));
        Assert.Null(form.TextInputClient);
        dialogForm.Close();
        Assert.Null(form.TextInputClient);
        cancel = false;
        dialogForm.Close();

        Assert.True(dialog.IsClosed);
        Assert.NotNull(owner.Input.TextInputClient);
        Assert.NotSame(old, owner.Input.TextInputClient);
        Assert.Equal("visible", editor.Text);
    }

    [Fact]
    public void PopupAndWindowActivationRetireSessionsWithoutChangingCanonicalSelection()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(editor);
        var old = owner.Input.TextInputClient!;
        old.SetComposingText("parent");
        var popup = new PopupWindow(form) { Size = new Size(200, 60) };
        var popupEditor = popup.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        popup.Show(0, 0);
        popupEditor.Select();
        var popupClient = popup.TextInputClient!;
        Assert.True(popupClient.CommitText("popup"));
        Assert.False(old.CommitText("late"));
        Assert.Same(editor, owner.FocusedControl);

        popup.Hide();

        Assert.Null(popupClient.GetState());
        Assert.Equal("popup", popupEditor.Text);
        var restored = owner.Input.TextInputClient!;
        Assert.NotSame(old, restored);
        owner.SetActive(false);
        Assert.Null(restored.GetState());
        owner.SetActive(true);
        Assert.NotSame(restored, owner.Input.TextInputClient);
    }

    [Fact]
    public void CompositionKeysBypassBindingsAndOrdinaryShortcutsResumeAfterFinish()
    {
        using var host = ModernFormsTestHost.Create();
        var editor = new TextBox();
        int calls = 0;
        editor.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++), new KeyGesture(Keys.Escape)));
        host.Show(editor);
        host.Input.Focus(editor);
        host.Input.SetComposingText("preedit");
        host.Input.PressKey(Keys.Escape);
        Assert.Equal(0, calls);
        host.Input.FinishComposition();
        host.Input.PressKey(Keys.Escape);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void CustomClientGeometryUsesExistingNestedPresentationAndHostDpi(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 300, scale));
        var form = new Form { UseSystemDecorations = true };
        var panel = form.Controls.Add(new Panel { Bounds = new Rectangle(30, 20, 240, 180), Padding = Padding.Empty });
        var editor = panel.Controls.Add(new ClientControl { Bounds = new Rectangle(15, 25, 180, 60) });
        var window = host.Show(form);
        window.Input.Focus(editor);
        var state = window.Input.TextInputClient!.GetState(0)!;
        var physical = editor.PointToScreen(new Point((int)(7 * scale), (int)(9 * scale)));
        var origin = form.PointToScreen(Point.Empty);
        Assert.InRange(Math.Abs(state.CaretRectangle.X - (physical.X - origin.X) / scale), 0, 1.5);
        Assert.InRange(Math.Abs(state.CaretRectangle.Y - (physical.Y - origin.Y) / scale), 0, 1.5);
        Assert.InRange(Math.Abs(state.CaretRectangle.Height - 18), 0, 0.1);
        Assert.NotNull(state.CaretBaseline);
        Assert.True(window.Input.CommitComposition("custom"));
        Assert.Equal("custom", editor.Client.LastCommit);
    }

    [Fact]
    public void SurfaceRetiresNativeClientBeforeFinishAndAllowsBorrowedRootReuse()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox());
        var method = new CapturingMethod();
        var surface = new SkiaControlSurface(root);
        surface.AttachTextInputMethod(method);
        editor.Select();
        var old = Assert.IsAssignableFrom<ITextInputClient>(method.Client);
        old.SetComposingText("retained");
        bool rejected = false;
        editor.TextCompositionChanged += (_, e) => {
            if (e.Stage == TextCompositionStage.Finished) rejected = !old.CommitText("late");
        };

        surface.Dispose();

        Assert.True(rejected);
        Assert.Null(method.Client);
        Assert.Null(old.GetState());
        Assert.Equal("retained", editor.Text);
        using var replacement = new SkiaControlSurface(root);
        Assert.NotNull(replacement.TextInputClient);
        Assert.NotSame(old, replacement.TextInputClient);
    }

    [Fact]
    public void DiagnosticsAreBoundedAndExcludeTextEvenInPasswordFields()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox { PasswordCharacter = '*' });
        host.Show(form);
        host.Input.Focus(editor);
        for (int i = 0; i < 80; i++) host.Input.SetComposingText("PRIVATE-SECRET");
        var diagnostics = form.TextInputDiagnostics;
        Assert.True(diagnostics.HasClient);
        Assert.Equal(TextInputScope.Password, diagnostics.Scope);
        Assert.InRange(diagnostics.RecentEvents.Count, 1, 64);
        Assert.DoesNotContain("PRIVATE-SECRET", System.Text.Json.JsonSerializer.Serialize(diagnostics));
        Assert.DoesNotContain("PRIVATE-SECRET", string.Join(" ", host.Input.RecentEvents));
    }

    [Fact]
    public void RetainedClientRejectsWrongThreadAndNativeReentrantEdit()
    {
        using var host = ModernFormsTestHost.Create();
        var editor = new TextBox();
        host.Show(editor);
        host.Input.Focus(editor);
        var client = host.Input.TextInputClient!;
        bool nestedAccepted = true;
        editor.KeyPress += (_, _) => nestedAccepted = client.CommitText("nested");
        Assert.True(client.CommitText("outer"));
        Assert.False(nestedAccepted);
        Assert.Equal("outer", editor.Text);
        Exception? failure = null;
        var thread = new Thread(() => { try { client.GetState(); } catch (Exception e) { failure = e; } });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void NativeStateNotificationWaitsForFinalCompositionAndCaret()
    {
        using var host = ModernFormsTestHost.Create();
        var editor = new TextBox { Text = "before" };
        host.Show(editor);
        host.Input.Focus(editor);
        var client = host.Input.TextInputClient!;
        client.SetSelection(0, editor.Text.Length);
        var observed = new List<TextInputState>();
        client.StateChanged += (_, _) => observed.Add(client.GetState()!);

        Assert.True(client.SetComposingText("日本", 1));

        Assert.NotEmpty(observed);
        Assert.All(observed, state => {
            Assert.Equal("日本", state.Text);
            Assert.Equal(2, state.SelectionStart);
            Assert.Equal(2, state.SelectionEnd);
            Assert.Equal(0, state.CompositionStart);
            Assert.Equal(2, state.CompositionEnd);
        });
    }

    [Fact]
    public void FinishFailureStillDisposesCustomControlAndClearsNativeBorrower()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new FailingClientControl());
        using var surface = new SkiaControlSurface(root);
        var native = new CapturingMethod();
        surface.AttachTextInputMethod(native);
        editor.Select();
        var old = native.Client!;
        bool disposed = false;
        editor.Disposed += (_, _) => disposed = true;
        editor.Client.ThrowOnFinish = true;

        Assert.Throws<InvalidOperationException>(editor.Dispose);

        Assert.True(disposed);
        Assert.Null(old.GetState());
        Assert.Null(native.Client);
        Assert.Null(surface.TextInputClient);
    }

    [Fact]
    public void PartialCustomSubscriptionFailureRemovesBothAccessorsAndCanRecover()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new FailingClientControl());
        using var surface = new SkiaControlSurface(root);
        editor.Client.ThrowOnSubscribe = true;
        Assert.Throws<InvalidOperationException>(editor.Select);
        Assert.Equal(0, editor.Client.StateSubscriberCount);
        Assert.Equal(0, editor.Client.CompositionSubscriberCount);
        editor.Client.ThrowOnSubscribe = false;
        Assert.NotNull(surface.TextInputClient);
        Assert.Equal(1, editor.Client.StateSubscriberCount);
        surface.Dispose();
        Assert.Equal(0, editor.Client.StateSubscriberCount);
        Assert.Equal(0, editor.Client.CompositionSubscriberCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDetachCallbackCannotOverwriteNewerMethodOrDisposedHost(bool dispose)
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox());
        using var surface = new SkiaControlSurface(root);
        var first = new CapturingMethod();
        var obsolete = new CapturingMethod();
        var replacement = new CapturingMethod();
        surface.AttachTextInputMethod(first);
        editor.Select();
        first.OnClientChanged = client => {
            if (client is not null) return;
            first.OnClientChanged = null;
            if (dispose) surface.Dispose();
            else surface.AttachTextInputMethod(replacement);
        };

        surface.AttachTextInputMethod(obsolete);

        Assert.Equal(0, obsolete.Attachments);
        if (dispose) Assert.Equal(0, replacement.Attachments);
        else {
            Assert.NotNull(replacement.Client);
            Assert.True(replacement.Client.CommitText("replacement"));
            Assert.Equal("replacement", editor.Text);
            surface.Dispose();
            Assert.Null(replacement.Client);
        }
    }

    [Fact]
    public void ReplacingNativeMethodRevokesItsRetainedClientBeforeDetachCallbacks()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox());
        using var surface = new SkiaControlSurface(root);
        var first = new CapturingMethod();
        var second = new CapturingMethod();
        surface.AttachTextInputMethod(first);
        editor.Select();
        var obsolete = first.Client!;
        obsolete.SetComposingText("visible");
        bool rejected = false;
        first.OnClientChanged = client => {
            if (client is null) rejected = !obsolete.CommitText("late");
        };

        surface.AttachTextInputMethod(second);

        Assert.True(rejected);
        Assert.Null(obsolete.GetState());
        Assert.NotSame(obsolete, second.Client);
        Assert.False(second.Client!.GetState()!.HasComposition);
        Assert.Equal("visible", editor.Text);
    }

    [Fact]
    public void FinishCanReattachTheSameNativeMethodWithoutAnOlderAttachClearingIt()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox());
        using var surface = new SkiaControlSurface(root);
        var first = new CapturingMethod();
        var obsolete = new CapturingMethod();
        surface.AttachTextInputMethod(first);
        editor.Select();
        var old = first.Client!;
        old.SetComposingText("visible");
        editor.TextCompositionChanged += (_, e) => {
            if (e.Stage == TextCompositionStage.Finished) surface.AttachTextInputMethod(first);
        };

        surface.AttachTextInputMethod(obsolete);

        Assert.Null(old.GetState());
        Assert.NotNull(first.Client);
        Assert.NotSame(old, first.Client);
        Assert.Equal(0, obsolete.Attachments);
        Assert.True(first.Client.CommitText("!"));
        Assert.Equal("visible!", editor.Text);
    }

    [Fact]
    public void NativeAttachmentFailureRevokesTheOfferAndAllowsExplicitRetry()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox());
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        var native = new CapturingMethod();
        ITextInputClient? rejected = null;
        native.OnClientChanged = value => {
            if (value is null) return;
            rejected = value;
            throw new InvalidOperationException("native attach failed");
        };

        Assert.Throws<InvalidOperationException>(() => surface.AttachTextInputMethod(native));

        Assert.NotNull(rejected);
        Assert.Null(rejected.GetState());
        Assert.Null(native.Client);
        native.OnClientChanged = null;
        surface.AttachTextInputMethod(native);
        Assert.NotNull(native.Client);
        Assert.NotSame(rejected, native.Client);
        Assert.True(native.Client.CommitText("retry"));
        Assert.Equal("retry", editor.Text);
    }

    [Fact]
    public void DisposePreservesBothEditorFinishAndNativeDetachFailures()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new FailingClientControl());
        using var surface = new SkiaControlSurface(root);
        var native = new CapturingMethod();
        surface.AttachTextInputMethod(native);
        editor.Select();
        var old = native.Client!;
        editor.Client.ThrowOnFinish = true;
        native.OnClientChanged = value => { if (value is null) throw new ArgumentException("detach failed"); };

        var failure = Assert.Throws<AggregateException>(surface.Dispose);

        Assert.Contains(failure.Flatten().InnerExceptions, error => error.Message == "finish failed");
        Assert.Contains(failure.Flatten().InnerExceptions, error => error.Message == "detach failed");
        Assert.Null(old.GetState());
        Assert.Null(native.Client);
        Assert.Equal(0, editor.Client.StateSubscriberCount);
        Assert.Equal(0, editor.Client.CompositionSubscriberCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomEventAccessorDisposalCannotPublishOrRetainSession(bool compositionAccessor)
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new FailingClientControl());
        using var surface = new SkiaControlSurface(root);
        var native = new CapturingMethod();
        surface.AttachTextInputMethod(native);
        if (compositionAccessor) editor.Client.OnCompositionSubscribe = surface.Dispose;
        else editor.Client.OnStateSubscribe = surface.Dispose;

        editor.Select();

        Assert.Null(native.Client);
        Assert.Equal(0, editor.Client.StateSubscriberCount);
        Assert.Equal(0, editor.Client.CompositionSubscriberCount);
    }

    [Fact]
    public void RecursiveNullClientQueryHasBoundedNotificationsAndCanRecover()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new QueryControl());
        using var surface = new SkiaControlSurface(root);
        editor.Query = () => { _ = surface.TextInputClient; return null; };

        Assert.Throws<InvalidOperationException>(editor.Select);

        Assert.InRange(editor.QueryCount, 1, 32);
        editor.Query = () => new RecordingClient();
        editor.Select();
        Assert.NotNull(surface.TextInputClient);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(2d)]
    public void ManagedWindowBorderIsIncludedInLiteralPaintCoordinates(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 300, scale));
        var form = new Form { UseSystemDecorations = false };
        form.TitleBar.Visible = false;
        form.Style.Border.Width = 4;
        var editor = form.Controls.Add(new ClientControl { Bounds = new Rectangle(20, 40, 100, 60) });
        var window = host.Show(form);
        window.Input.Focus(editor);

        var state = window.Input.TextInputClient!.GetState(0)!;

        // DrawBackBuffer places this untransformed child's local point at its scaled bounds,
        // then ControlAdapter adds four physical border pixels. Do not use PointToScreen here:
        // its input transform does not include this paint-only adapter border offset.
        Assert.Equal(27 + 4 / scale, state.CaretRectangle.X, 4);
        Assert.Equal(49 + 4 / scale, state.CaretRectangle.Y, 4);
        Assert.Equal(64 + 4 / scale, state.CaretBaseline!.Value, 4);
    }

    private sealed class CapturingMethod : ITextInputMethod
    {
        public ITextInputClient? Client { get; private set; }
        public int Attachments { get; private set; }
        public Action<ITextInputClient?>? OnClientChanged { get; set; }
        public void SetClient(ITextInputClient? client) { Attachments++; Client = client; OnClientChanged?.Invoke(client); }
        public bool SetKeyboardVisible(bool visible) => true;
    }

    private sealed class QueryControl : Control
    {
        public Func<ITextInputClient?>? Query { get; set; }
        public int QueryCount { get; private set; }
        protected override ITextInputClient? GetTextInputClient() { QueryCount++; return Query?.Invoke(); }
    }

    private sealed class ClientControl : Control
    {
        public RecordingClient Client { get; } = new();
        protected override ITextInputClient GetTextInputClient() => Client;
    }

    // A custom editor contract probe, not an alternative framework text document.
    private class RecordingClient : ITextInputClient
    {
        public string? LastCommit { get; private set; }
        public virtual event EventHandler? StateChanged { add { } remove { } }
        public virtual event EventHandler<TextCompositionEventArgs>? CompositionChanged { add { } remove { } }
        public TextInputState GetState(int maximumTextLength = 4096, int? textStart = null)
            => new("", 0, 0, 0, 0, -1, -1, 0, new Rect(7, 9, 1, 18), new(), 24);
        public bool CommitText(string text, int newCursorPosition = 1) { LastCommit = text; return true; }
        public bool SetComposingText(string text, int newCursorPosition = 1) => false;
        public bool SetComposingRegion(int start, int end) => false;
        public bool SetSelection(int start, int end) => false;
        public virtual bool FinishComposition() => true;
        public bool CancelComposition() => false;
        public bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false) => false;
        public bool PerformEditorAction(TextInputAction action) => false;
    }

    private sealed class FailingClientControl : Control
    {
        public FailingClient Client { get; } = new();
        protected override ITextInputClient GetTextInputClient() => Client;
    }

    private sealed class FailingClient : RecordingClient
    {
        private EventHandler? stateChanged;
        private EventHandler<TextCompositionEventArgs>? compositionChanged;
        public bool ThrowOnSubscribe { get; set; }
        public bool ThrowOnFinish { get; set; }
        public Action? OnStateSubscribe { get; set; }
        public Action? OnCompositionSubscribe { get; set; }
        public int StateSubscriberCount => stateChanged?.GetInvocationList().Length ?? 0;
        public int CompositionSubscriberCount => compositionChanged?.GetInvocationList().Length ?? 0;
        public override event EventHandler? StateChanged
        {
            add { stateChanged += value; OnStateSubscribe?.Invoke(); }
            remove => stateChanged -= value;
        }
        public override event EventHandler<TextCompositionEventArgs>? CompositionChanged
        {
            add { compositionChanged += value; OnCompositionSubscribe?.Invoke(); if (ThrowOnSubscribe) throw new InvalidOperationException("subscription failed"); }
            remove => compositionChanged -= value;
        }
        public override bool FinishComposition()
            => ThrowOnFinish ? throw new InvalidOperationException("finish failed") : true;
    }
}
