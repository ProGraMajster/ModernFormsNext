using System.Drawing;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TextInputWindowLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReactivationCommitsNativeStateAndNotifiesObserversAfterTextClientFailure(bool observerThrows)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new QueryTextBox());
        var window = host.Show(form);
        window.Input.Focus(editor);
        var old = form.TextInputClient!;
        window.SetActive(false);
        Assert.Equal(0, Application.Lifecycle.GetDiagnostics().ActiveWindowCount);
        var textFailure = new InvalidOperationException("Text client activation failed.");
        var observerFailure = new InvalidOperationException("Activation observer failed.");
        editor.Query = () => {
            Assert.True(form.IsActive);
            Assert.Equal(1, Application.Lifecycle.GetDiagnostics().ActiveWindowCount);
            throw textFailure;
        };
        form.Activated += (_, _) => { if (observerThrows) throw observerFailure; };
        int activated = 0;
        form.Activated += (_, _) => activated++;

        if (observerThrows) {
            var failure = Assert.Throws<AggregateException>(() => window.SetActive(true));
            Assert.Equal(new Exception[] { textFailure, observerFailure }, failure.InnerExceptions);
        }
        else Assert.Same(textFailure, Assert.Throws<InvalidOperationException>(() => window.SetActive(true)));

        Assert.True(form.IsActive);
        Assert.Equal(1, activated);
        Assert.Equal(1, Application.Lifecycle.GetDiagnostics().ActiveWindowCount);
        Assert.Null(old.GetState());
        editor.Query = null;
        Assert.NotNull(form.TextInputClient);
        Assert.NotSame(old, form.TextInputClient);
        window.SetActive(true);
        Assert.Equal(1, activated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextClientActivationThatHidesOrClosesWindowCannotPublishAnObsoleteActivatedEvent(bool close)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new QueryTextBox());
        var window = host.Show(form);
        window.Input.Focus(editor);
        window.SetActive(false);
        editor.Query = () => {
            editor.Query = null;
            if (close) form.Close();
            else form.Hide();
        };
        int activated = 0;
        form.Activated += (_, _) => activated++;

        window.SetActive(true);

        Assert.False(form.IsActive);
        Assert.False(form.Visible);
        Assert.Equal(0, activated);
        Assert.Equal(0, Application.Lifecycle.GetDiagnostics().ActiveWindowCount);
        Assert.Null(form.TextInputClient);
    }

    [Fact]
    public void NestedDeactivationAndReactivationPublishOnlyTheCurrentActivation()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new QueryTextBox());
        var window = host.Show(form);
        window.Input.Focus(editor);
        window.SetActive(false);
        editor.Query = () => {
            editor.Query = null;
            window.SetActive(false);
            window.SetActive(true);
        };
        int activated = 0;
        form.Activated += (_, _) => activated++;

        window.SetActive(true);

        Assert.True(form.IsActive);
        Assert.Equal(1, activated);
        Assert.Equal(1, Application.Lifecycle.GetDiagnostics().ActiveWindowCount);
        Assert.NotNull(form.TextInputClient);
    }

    [Theory]
    [InlineData("plain", false)]
    [InlineData("rich", false)]
    [InlineData("markdown", false)]
    [InlineData("plain", true)]
    [InlineData("rich", true)]
    [InlineData("markdown", true)]
    public void ClosingCompositionKeepsDocumentNotificationsWithoutPaintingDestroyedBackend(string kind, bool observerThrows)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        Control content = kind switch {
            "rich" => new RichTextBox(),
            "markdown" => new MarkdownEditor { ShowToolbar = false },
            _ => new TextBox()
        };
        content.Dock = DockStyle.Fill;
        form.Controls.Add(content);
        TextBox editor = content as TextBox ?? Assert.Single(content.Controls.OfType<TextBox>());
        var window = host.Show(form, 500, 300);
        Assert.True(window.Input.Focus(editor));
        var old = form.TextInputClient!;
        Assert.True(old.SetComposingText("accepted"));
        int finished = 0;
        int invalidated = 0;
        int closed = 0;
        var expected = new InvalidOperationException("Terminal composition observer failed.");
        editor.Invalidated += (_, _) => invalidated++;
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage != TextCompositionStage.Finished) return;
            finished++;
            Assert.True(window.Backend.IsDisposed);
            Assert.Equal(-1, args.Start);
            Assert.Equal(-1, args.End);
            Assert.Equal("accepted", editor.Text);
            Assert.Null(old.GetState());
            if (observerThrows) throw expected;
        };
        form.Closed += (_, _) => closed++;

        if (observerThrows) Assert.Same(expected, Assert.Throws<InvalidOperationException>(form.Close));
        else form.Close();

        Assert.Equal(1, finished);
        Assert.Equal(1, closed);
        Assert.True(invalidated > 0);
        Assert.True(window.IsClosed);
        Assert.False(old.CommitText("late"));
        Assert.Null(form.TextInputClient);
        Assert.Equal("accepted", editor.Text);
        if (content is MarkdownEditor markdown) Assert.Equal("accepted", markdown.Markdown);
        int nativeInvalidations = window.Backend.TotalInvalidationCount;
        form.Invalidate();
        editor.Invalidate();
        Assert.Equal(nativeInvalidations, window.Backend.TotalInvalidationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HideOrDeactivationFinishesBeforeVisibilityChangesAndCleansUpAfterObserverFailure(bool hide)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        var window = host.Show(form);
        window.Input.Focus(editor);
        editor.InputBindings.Add(new KeyBinding(new DelegateCommand(() => { }),
            new KeyGesture(Keys.S, KeyModifiers.Control)));
        window.Input.KeyDown(Keys.S | Keys.Control);
        var old = window.Input.TextInputClient!;
        old.SetComposingText("visible");
        var expected = new InvalidOperationException("Finish observer failed.");
        int finished = 0;
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage != TextCompositionStage.Finished) return;
            finished++;
            Assert.True(editor.Visible);
            Assert.Null(old.GetState());
            throw expected;
        };
        int deactivated = 0;
        form.Deactivated += (_, _) => deactivated++;

        Exception actual = Assert.Throws<InvalidOperationException>(() => {
            if (hide) form.Hide();
            else window.SetActive(false);
        });

        Assert.Same(expected, actual);
        Assert.Equal(1, finished);
        Assert.False(form.IsActive);
        Assert.Null(old.GetState());
        Assert.Null(form.TextInputClient);
        Assert.Equal(!hide, form.Visible);
        Assert.Equal(!hide, window.Backend.IsShown);
        Assert.Equal(hide ? 0 : 1, deactivated);
        if (hide) form.Show();
        else window.SetActive(true);
        Assert.NotSame(old, form.TextInputClient);
        Assert.False(form.TextInputClient!.GetState()!.HasComposition);
        Assert.Equal("visible", editor.Text);
        int released = 0;
        editor.KeyUp += (_, _) => released++;
        window.Input.KeyUp(Keys.S | Keys.Control);
        Assert.Equal(1, released);
    }

    [Fact]
    public void PopupHideReleasesNativeVisibilityAndRestoresOwnerAfterFinishFailure()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var ownerEditor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(ownerEditor);
        var popup = new PopupWindow(form) { Size = new Size(200, 60) };
        var editor = popup.Controls.Add(new TextBox());
        popup.Show(0, 0);
        editor.Select();
        var old = popup.TextInputClient!;
        old.SetComposingText("popup");
        var backend = Assert.Single(owner.Backend.LivePopups).State;
        var expected = new InvalidOperationException("Popup finish failed.");
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Finished) throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(popup.Hide));

        Assert.False(backend.IsShown);
        Assert.False(popup.Visible);
        Assert.Null(old.GetState());
        Assert.Null(popup.TextInputClient);
        Assert.Null(owner.ActivePopup);
        Assert.NotNull(form.TextInputClient);
        owner.Input.TextInput("owner");
        Assert.Equal("owner", ownerEditor.Text);
        popup.Show(0, 0);
        var current = popup.TextInputClient!;
        Assert.NotNull(current);
        Assert.NotSame(old, current);
        Assert.False(current.GetState()!.HasComposition);
        owner.Input.TextInput("!");
        Assert.Equal("popup!", editor.Text);
        Assert.Equal("owner", ownerEditor.Text);
    }

    [Fact]
    public void ParentDeactivationHidesPopupDespiteFinishAndEarlierDeactivationObserverFailures()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var ownerEditor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(ownerEditor);
        var first = new InvalidOperationException("Deactivated observer failed.");
        form.Deactivated += (_, _) => throw first;
        var popup = new PopupWindow(form) { Size = new Size(200, 60) };
        var editor = popup.Controls.Add(new TextBox());
        popup.Show(0, 0);
        editor.Select();
        var old = popup.TextInputClient!;
        old.SetComposingText("popup");
        var second = new InvalidOperationException("Popup finish failed.");
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Finished) throw second;
        };
        int notified = 0;
        form.Deactivated += (_, _) => notified++;

        var failure = Assert.Throws<AggregateException>(() => owner.SetActive(false));

        Assert.Contains(first, failure.InnerExceptions);
        Assert.Contains(second, failure.InnerExceptions);
        Assert.Equal(1, notified);
        Assert.False(Assert.Single(owner.Backend.LivePopups).State.IsShown);
        Assert.False(popup.Visible);
        Assert.Null(old.GetState());
        Assert.Null(form.TextInputClient);
        Assert.Null(owner.ActivePopup);
    }

    [Fact]
    public void OlderPopupHideAndRepeatedParentActivationPreserveTheNewPopupSession()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var ownerEditor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(ownerEditor);
        var older = new PopupWindow(form) { Size = new Size(200, 60) };
        var oldEditor = older.Controls.Add(new TextBox());
        older.Show(0, 0);
        oldEditor.Select();
        var retired = older.TextInputClient!;
        retired.SetComposingText("accepted");
        var newer = new PopupWindow(form) { Size = new Size(200, 60) };
        var editor = newer.Controls.Add(new TextBox());
        newer.Show(0, 0);
        editor.Select();
        Assert.Null(retired.GetState());
        Assert.False(retired.CommitText("late"));
        Assert.Null(older.TextInputClient);
        Assert.Equal("accepted", oldEditor.Text);
        var current = newer.TextInputClient!;
        current.SetComposingText("new");

        older.Hide();
        owner.SetActive(true);

        Assert.Same(newer, owner.ActivePopup!.Window);
        Assert.Null(form.TextInputClient);
        Assert.Same(current, newer.TextInputClient);
        Assert.True(current.GetState()!.HasComposition);
        Assert.True(current.CommitText("newer"));
        owner.Input.TextInput("!");
        Assert.Equal("newer!", editor.Text);
        Assert.Empty(ownerEditor.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopupOpenedByFinishObserverKeepsItsNewSessionWhenOlderHideUnwinds(bool reuse)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var ownerEditor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(ownerEditor);
        var older = new PopupWindow(form) { Size = new Size(200, 60) };
        var oldEditor = older.Controls.Add(new TextBox());
        older.Show(0, 0);
        oldEditor.Select();
        var old = older.TextInputClient!;
        old.SetComposingText("accepted");
        var newer = reuse ? older : new PopupWindow(form) { Size = new Size(200, 60) };
        var editor = reuse ? oldEditor : newer.Controls.Add(new TextBox());
        var expected = new InvalidOperationException("Finish after reentrant show.");
        oldEditor.TextCompositionChanged += (_, args) => {
            if (args.Stage != TextCompositionStage.Finished) return;
            newer.Show(0, 0);
            editor.Select();
            throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(older.Hide));

        Assert.True(newer.Visible);
        Assert.True(owner.Backend.LivePopups.Single(p => ReferenceEquals(p.HostedPopup, newer)).State.IsShown);
        Assert.Same(newer, owner.ActivePopup!.Window);
        Assert.Null(form.TextInputClient);
        Assert.Null(old.GetState());
        Assert.NotNull(newer.TextInputClient);
        Assert.NotSame(old, newer.TextInputClient);
        Assert.False(newer.TextInputClient!.GetState()!.HasComposition);
        owner.Input.TextInput("!");
        Assert.Equal(reuse ? "accepted!" : "!", editor.Text);
        Assert.Empty(ownerEditor.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedOrClosedPopupDuringOwnerFinishDoesNotReachNativeShow(bool close)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        var owner = host.Show(form);
        owner.Input.Focus(editor);
        var old = form.TextInputClient!;
        old.SetComposingText("owner");
        var popup = new PopupWindow(form) { Size = new Size(200, 60) };
        popup.Controls.Add(new TextBox());
        var backend = Assert.Single(owner.Backend.LivePopups).State;
        var expected = new InvalidOperationException("Owner finish failed.");
        editor.TextCompositionChanged += (_, args) => {
            if (args.Stage != TextCompositionStage.Finished) return;
            if (close) popup.Close();
            else throw expected;
        };

        if (close) popup.Show(0, 0);
        else Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => popup.Show(0, 0)));

        Assert.False(backend.IsShown);
        Assert.Equal(close, backend.IsDisposed);
        Assert.False(popup.Visible);
        Assert.Null(owner.ActivePopup);
        Assert.Null(old.GetState());
        Assert.NotNull(form.TextInputClient);
        Assert.False(form.TextInputClient!.GetState()!.HasComposition);
        Assert.Equal("owner", editor.Text);
    }

    private sealed class QueryTextBox : TextBox
    {
        public Action? Query { get; set; }

        protected override ITextInputClient GetTextInputClient()
        {
            Query?.Invoke();
            return base.GetTextInputClient();
        }
    }
}
