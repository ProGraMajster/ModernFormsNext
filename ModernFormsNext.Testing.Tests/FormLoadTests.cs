using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class FormLoadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstDisplayLoadsPreparedControlsBeforeBackendShowAndShown(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form { ClientSize = new Size(320, 180) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        var backend = Backend(form);
        List<string> order = [];
        form.Load += (sender, args) => {
            Assert.Same(form, sender);
            Assert.Same(EventArgs.Empty, args);
            Assert.False(form.Visible);
            Assert.False(backend.IsShown);
            Assert.Equal(0, backend.ShowCallCount);
            Assert.DoesNotContain(form, Application.OpenForms);
            Assert.Equal(form.ClientSize, fill.Size);
            Assert.True(Backend(owner).IsEnabled);
            order.Add("Load");
        };
        backend.Activated += () => {
            Assert.True(backend.IsShown);
            Assert.Equal(1, backend.ShowCallCount);
            order.Add("backend");
        };
        form.Shown += (_, _) => {
            Assert.Contains(form, Application.OpenForms);
            order.Add("Shown");
        };

        Task<DialogResult>? result = null;
        if (modal) result = form.ShowDialog(owner);
        else form.Show();

        Assert.Equal(new[] { "Load", "backend", "Shown" }, order);
        if (modal) Assert.False(Backend(owner).IsEnabled);
        form.Close();
        if (modal) Assert.True(result!.IsCompletedSuccessfully);
    }

    [Fact]
    public void RepeatedShowAndHideShowDoNotReloadOrRepeatShown()
    {
        using var host = ModernFormsTestHost.Create();
        using var first = new Form();
        using var second = new Form();
        int firstLoads = 0, secondLoads = 0, shown = 0;
        first.Load += (_, _) => firstLoads++;
        second.Load += (_, _) => secondLoads++;
        first.Shown += (_, _) => shown++;

        first.Show();
        first.Show();
        first.Hide();
        first.Show();
        second.Show();

        Assert.Equal(1, firstLoads);
        Assert.Equal(1, secondLoads);
        Assert.Equal(1, shown);
        first.Close();
        Assert.Throws<ObjectDisposedException>(first.Show);
        Assert.Equal(1, firstLoads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadMutationsAreLaidOutBeforeCenteringAndShow(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form { Location = new Point(100, 80), Size = new Size(800, 600) };
        owner.Show();
        // Show may apply the owner's own startup position.
        owner.Location = new Point(100, 80);
        using var form = new Form { Size = new Size(200, 160), StartPosition = FormStartPosition.Manual };
        using var removed = form.Controls.Add(new Label());
        using var panel = new Panel { Dock = DockStyle.Fill };
        using var child = panel.Controls.Add(new Label { Dock = DockStyle.Fill });
        form.Load += (_, _) => {
            form.Controls.Remove(removed);
            form.Controls.Add(panel);
            form.Size = new Size(400, 300);
            form.StartPosition = modal ? FormStartPosition.CenterParent : FormStartPosition.Manual;
        };
        form.Shown += (_, _) => {
            Assert.Equal(new Size(400, 300), form.Size);
            Assert.Equal(form.ClientSize, panel.Size);
            Assert.Equal(panel.DisplayRectangle.Size, child.Size);
            Assert.DoesNotContain(removed, form.Controls.Cast<Control>());
            if (modal) Assert.Equal(new Point(300, 230), form.Location);
        };

        if (modal) _ = form.ShowDialog(owner);
        else form.Show();

        Assert.Equal(1, Backend(form).ShowCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedShowDoesNotDisplayPartiallyInitializedFormOrCancelOuterRequest(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        int loads = 0, shown = 0;
        form.Load += (_, _) => {
            loads++;
            form.Show();
            Assert.Equal(0, Backend(form).ShowCallCount);
            Assert.False(form.Visible);
        };
        form.Shown += (_, _) => shown++;

        if (modal) _ = form.ShowDialog(owner);
        else form.Show();

        Assert.Equal(1, loads);
        Assert.Equal(1, shown);
        Assert.Equal(1, Backend(form).ShowCallCount);
    }

    [Fact]
    public void LayoutReentrancyCannotShowBeforeLoad()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var panel = form.Controls.Add(new Panel());
        int loads = 0;
        panel.Layout += (_, _) => {
            if (loads == 0) {
                form.Show();
                Assert.Equal(0, Backend(form).ShowCallCount);
            }
        };
        form.Load += (_, _) => loads++;
        form.Show();
        Assert.Equal(1, loads);
        Assert.Equal(1, Backend(form).ShowCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HideInLoadCancelsAttemptAndAllowsLaterDisplayWithoutReload(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var editor = owner.Controls.Add(new TextBox());
        owner.Show();
        editor.Select();
        var client = owner.TextInputClient;
        using var form = new Form();
        int loads = 0, shown = 0;
        form.Load += (_, _) => { loads++; form.Hide(); form.Show(); };
        form.Shown += (_, _) => shown++;

        Task<DialogResult>? result = null;
        if (modal) result = form.ShowDialog(owner);
        else form.Show();

        Assert.False(form.Visible);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.Equal(0, shown);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        Assert.Same(client, owner.TextInputClient);
        if (modal) { Assert.True(result!.IsCompletedSuccessfully); Assert.Equal(DialogResult.None, await result); }

        if (modal) result = form.ShowDialog(owner);
        else form.Show();
        Assert.Equal(1, loads);
        Assert.Equal(1, shown);
        Assert.Equal(1, Backend(form).ShowCallCount);
        form.Close();
        if (modal) Assert.True(result!.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseInLoadIsTerminalWithoutDisplay(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        int closed = 0, shown = 0;
        form.Load += (_, _) => form.Close();
        form.Closed += (_, _) => closed++;
        form.Shown += (_, _) => shown++;

        Task<DialogResult>? result = null;
        if (modal) result = form.ShowDialog(owner);
        else form.Show();

        Assert.Equal(1, closed);
        Assert.Equal(0, shown);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.True(Backend(form).IsDisposed);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        if (modal) Assert.True(result!.IsCompletedSuccessfully);
        Assert.Throws<ObjectDisposedException>(form.Show);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledCloseInLoadPreservesThePendingDisplayAndModalOperation(bool setResult)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        bool cancel = true;
        form.Closing += (_, e) => e.Cancel = cancel;
        form.Load += (_, _) => {
            if (setResult) form.DialogResult = DialogResult.OK;
            else form.Close();
        };

        var result = form.ShowDialog(owner);

        Assert.True(form.Visible);
        Assert.Equal(1, Backend(form).ShowCallCount);
        Assert.False(result.IsCompleted);
        Assert.False(Backend(owner).IsEnabled);
        cancel = false;
        form.Close();
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(setResult ? DialogResult.OK : DialogResult.None, await result);
        Assert.True(Backend(owner).IsEnabled);
    }

    [Fact]
    public async Task DialogResultInLoadCompletesWithoutShowingOrRetiringOwnerInput()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var editor = owner.Controls.Add(new TextBox());
        owner.Show();
        editor.Select();
        var client = owner.TextInputClient;
        Assert.NotNull(client);
        using var form = new Form();
        form.Load += (_, _) => form.DialogResult = DialogResult.OK;

        var result = form.ShowDialog(owner);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.OK, await result);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.True(Backend(form).IsDisposed);
        Assert.True(Backend(owner).IsEnabled);
        Assert.Same(client, owner.TextInputClient);
    }

    [Fact]
    public async Task PreassignedResultDoesNotInitializeTheForm()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var form = new Form { DialogResult = DialogResult.Cancel };
        int loads = 0;
        form.Load += (_, _) => loads++;
        var result = form.ShowDialog(owner);
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.Cancel, await result);
        Assert.Equal(0, loads);
        Assert.Equal(0, Backend(form).ShowCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedLoadPropagatesOriginalAndPermanentlyRejectsDisplayWithoutDisposingChildren(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var editor = owner.Controls.Add(new TextBox());
        owner.Show();
        editor.Select();
        var client = owner.TextInputClient;
        using var form = new Form();
        using var child = form.Controls.Add(new Label());
        var expected = new InvalidOperationException("Load failure");
        int loads = 0, shown = 0, disposed = 0;
        child.Disposed += (_, _) => disposed++;
        form.Load += (_, _) => { loads++; throw expected; };
        form.Shown += (_, _) => shown++;

        var actual = Assert.Throws<InvalidOperationException>(() => {
            if (modal) _ = form.ShowDialog(owner);
            else form.Show();
        });

        Assert.Same(expected, actual);
        Assert.Equal(0, disposed);
        Assert.False(form.Visible);
        Assert.Equal(0, shown);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        Assert.Same(client, owner.TextInputClient);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(form.Show).InnerException);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => { _ = form.ShowDialog(owner); }).InnerException);
        Assert.Equal(1, loads);
        form.Close();
    }

    [Fact]
    public void ModalInputFailureAfterLoadRestoresOwnerWithoutRepeatingSuccessfulLoad()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var editor = owner.Controls.Add(new TextBox());
        owner.Show();
        editor.Select();
        Assert.True(owner.TextInputClient!.SetComposingText("pending"));
        var expected = new InvalidOperationException("composition observer");
        editor.TextCompositionChanged += ThrowOnComplete;
        using var form = new Form();
        int loads = 0;
        form.Load += (_, _) => loads++;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => { _ = form.ShowDialog(owner); }));
        Assert.True(Backend(owner).IsEnabled);
        Assert.NotNull(owner.TextInputClient);
        Assert.Equal(0, Backend(form).ShowCallCount);
        editor.TextCompositionChanged -= ThrowOnComplete;
        var result = form.ShowDialog(owner);
        Assert.Equal(1, loads);
        form.Close();
        Assert.True(result.IsCompletedSuccessfully);

        void ThrowOnComplete(object? sender, TextCompositionEventArgs args)
        {
            if (args.Stage == TextCompositionStage.Finished) throw expected;
        }
    }

    [Fact]
    public void ModalReentrancyDuringModelessLoadIsRejectedWithoutLeavingModalState()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        form.Load += (_, _) => Assert.Throws<InvalidOperationException>(() => { _ = form.ShowDialog(owner); });
        form.Show();
        Assert.Equal(1, Backend(form).ShowCallCount);
        Assert.True(Backend(owner).IsEnabled);
        form.Hide();
        var result = form.ShowDialog(owner);
        Assert.False(result.IsCompleted);
        form.Close();
        Assert.True(result.IsCompletedSuccessfully);
    }

    [Fact]
    public void ProtectedHookAndPublicEventRunThroughTheSameOnceOnlyBoundary()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new LoadOverrideForm();
        int events = 0;
        form.Load += (_, _) => events++;
        form.Show();
        form.Hide();
        form.Show();
        Assert.Equal(1, form.Calls);
        Assert.Equal(1, events);
    }


    [Fact]
    public void OwnerClosedDuringLoadCancelsModalDisplayAndCompletesTheAttempt()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        int loads = 0;
        form.Load += (_, _) => { loads++; owner.Close(); };

        var result = form.ShowDialog(owner);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.False(form.Visible);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.DoesNotContain(form, Application.OpenForms);
        // The dialog itself remains initialized and can later be shown without that owner.
        form.Show();
        Assert.Equal(1, loads);
        Assert.Equal(1, Backend(form).ShowCallCount);
    }

    [Fact]
    public void HideFromInitialLayoutCancelsBeforeLoadAndNextShowCanInitialize()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var panel = form.Controls.Add(new Panel());
        int loads = 0;
        bool hide = true;
        panel.Layout += (_, _) => { if (hide) form.Hide(); };
        form.Load += (_, _) => loads++;

        form.Show();

        Assert.Equal(0, loads);
        Assert.Equal(0, Backend(form).ShowCallCount);
        hide = false;
        form.Show();
        Assert.Equal(1, loads);
        Assert.Equal(1, Backend(form).ShowCallCount);
    }


    // Inspect the existing backend without adding test-only access to the production API.
    private static HeadlessWindowImpl Backend(Form form) => Assert.IsType<HeadlessWindowImpl>(
        typeof(WindowBase).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));

    private sealed class LoadOverrideForm : Form
    {
        internal int Calls;
        protected override void OnLoad(EventArgs e) { Calls++; base.OnLoad(e); }
    }
}
