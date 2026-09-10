using System.Drawing;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ModalHostTests
{
    [Fact]
    public async Task DialogUsesProductionInputBlocksOwnerAndRestoresItsSelectedControl()
    {
        using var host = ModernFormsTestHost.Create();
        var parent = new Form { UseSystemDecorations = true };
        var parentText = parent.Controls.Add(new TextBox { Bounds = new Rectangle(10, 10, 160, 36) });
        var parentButton = parent.Controls.Add(new Button { Bounds = new Rectangle(10, 60, 100, 40) });
        var clicks = 0;
        parentButton.Click += (_, _) => clicks++;
        var owner = host.Show(parent);
        Assert.True(owner.Input.Focus(parentText));
        var form = new Form { UseSystemDecorations = true };
        var text = form.Controls.Add(new TextBox { Bounds = new Rectangle(10, 10, 160, 36) });
        var accept = form.Controls.Add(new Button { Bounds = new Rectangle(10, 60, 100, 40) });
        accept.Click += (_, _) => form.DialogResult = DialogResult.OK;

        var dialog = host.ShowDialog(form, owner, new TestViewport(320, 200, 1.25));

        Assert.NotNull(dialog.DialogCompletion);
        Assert.False(dialog.DialogCompletion.IsCompleted);
        Assert.Null(owner.DialogCompletion);
        Assert.False(owner.Input.Focus(parentButton));
        owner.Input.Click(parentButton);
        owner.Input.TextInput("blocked");
        Assert.Equal(0, clicks);
        Assert.Empty(parentText.Text);
        Assert.True(dialog.Input.Focus(text));
        dialog.Input.TextInput("modal text");
        Assert.Equal("modal text", text.Text);
        dialog.Input.Click(accept);

        Assert.True(dialog.IsClosed);
        Assert.True(dialog.DialogCompletion.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.OK, await dialog.DialogCompletion);
        Assert.Same(parentText, owner.FocusedControl);
        owner.Input.TextInput("resumed");
        Assert.Equal("resumed", parentText.Text);
        owner.Input.Click(parentButton);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task ProductionClosingCancellationKeepsOwnerBlockedAndResultPending()
    {
        using var host = ModernFormsTestHost.Create();
        var parent = new Form();
        var button = parent.Controls.Add(new Button());
        var owner = host.Show(parent);
        var form = new Form();
        var dialog = host.ShowDialog(form, owner);
        var cancel = true;
        form.Closing += (_, args) => args.Cancel = cancel;

        form.DialogResult = DialogResult.Cancel;

        Assert.False(dialog.IsClosed);
        Assert.False(dialog.DialogCompletion!.IsCompleted);
        Assert.False(owner.Input.Focus(button));
        cancel = false;
        form.Close();
        Assert.True(dialog.DialogCompletion.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.Cancel, await dialog.DialogCompletion);
        Assert.True(owner.Input.Focus(button));
    }

    [Fact]
    public async Task ForcedDialogCleanupCompletesCanceledDialogAndReenablesOwner()
    {
        using var host = ModernFormsTestHost.Create();
        var parent = new Form();
        var button = parent.Controls.Add(new Button());
        var owner = host.Show(parent);
        var form = new Form();
        var dialog = host.ShowDialog(form, owner);
        form.Closing += (_, args) => args.Cancel = true;

        dialog.Close();

        Assert.True(dialog.IsClosed);
        Assert.True(dialog.DialogCompletion!.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.None, await dialog.DialogCompletion);
        Assert.True(owner.Input.Focus(button));
    }

    [Fact]
    public async Task NestedDialogsCloseFromLeafToOwnerThroughCanonicalCompletion()
    {
        using var host = ModernFormsTestHost.Create();
        var owner = host.Show(new Form());
        var first = host.ShowDialog(new Form(), owner);
        var second = host.ShowDialog(new Form(), first);
        var order = new List<string>();
        owner.FormRoot!.Closing += (_, _) => order.Add("owner");
        first.FormRoot!.Closing += (_, _) => order.Add("first");
        second.FormRoot!.Closing += (_, _) => order.Add("second");

        owner.Close();

        Assert.Equal(new[] { "second", "first", "owner" }, order);
        Assert.Empty(host.Windows);
        Assert.True(first.DialogCompletion!.IsCompletedSuccessfully);
        Assert.True(second.DialogCompletion!.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.None, await first.DialogCompletion);
        Assert.Equal(DialogResult.None, await second.DialogCompletion);
    }

    [Fact]
    public async Task NestedDialogFailureStillReleasesEveryWindowAndOriginalTasks()
    {
        var host = ModernFormsTestHost.Create();
        var owner = host.Show(new Form());
        var first = host.ShowDialog(new Form(), owner);
        var second = host.ShowDialog(new Form(), first);
        second.FormRoot!.Closing += (_, _) => throw new DialogClosingFailure();
        second.FormRoot.Closed += (_, _) => throw new DialogClosedFailure();

        var failure = Assert.Throws<AggregateException>(host.Dispose);

        Assert.Contains(failure.Flatten().InnerExceptions, item => item is DialogClosingFailure);
        Assert.Contains(failure.Flatten().InnerExceptions, item => item is DialogClosedFailure);
        Assert.True(owner.IsClosed);
        Assert.True(first.IsClosed);
        Assert.True(second.IsClosed);
        Assert.True(first.DialogCompletion!.IsCompletedSuccessfully);
        Assert.True(second.DialogCompletion!.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.None, await first.DialogCompletion);
        Assert.Equal(DialogResult.None, await second.DialogCompletion);
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public async Task PresetResultCompletesWithoutShowingOrDisablingOwner()
    {
        using var host = ModernFormsTestHost.Create();
        var parent = new Form();
        var button = parent.Controls.Add(new Button());
        var owner = host.Show(parent);
        var form = new Form { DialogResult = DialogResult.Yes };
        var shown = 0;
        form.Shown += (_, _) => shown++;

        var dialog = host.ShowDialog(form, owner);

        Assert.Equal(0, shown);
        Assert.False(form.Visible);
        Assert.True(dialog.DialogCompletion!.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.Yes, await dialog.DialogCompletion);
        Assert.True(owner.Input.Focus(button));
    }

    [Fact]
    public async Task ClosingInsideShownReturnsOriginalCompletedTaskAndClosedWindow()
    {
        using var host = ModernFormsTestHost.Create();
        var owner = host.Show(new Form());
        var form = new Form();
        form.Shown += (_, _) => form.DialogResult = DialogResult.OK;

        var dialog = host.ShowDialog(form, owner);

        Assert.True(dialog.IsClosed);
        Assert.True(dialog.DialogCompletion!.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.OK, await dialog.DialogCompletion);
        Assert.Single(host.Windows);
    }

    [Fact]
    public async Task HostDisposalInsideDialogShownDoesNotResumeLayoutOnDisposedServices()
    {
        var host = ModernFormsTestHost.Create();
        var owner = host.Show(new Form());
        var form = new Form();
        form.Shown += (_, _) => host.Dispose();

        var dialog = host.ShowDialog(form, owner);

        Assert.True(owner.IsClosed);
        Assert.True(dialog.IsClosed);
        Assert.True(dialog.DialogCompletion!.IsCompletedSuccessfully);
        Assert.Equal(DialogResult.None, await dialog.DialogCompletion);
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public void ShownFailureReenablesOwnerAndReleasesFailedDialog()
    {
        using var host = ModernFormsTestHost.Create();
        var parent = new Form();
        var button = parent.Controls.Add(new Button());
        var owner = host.Show(parent);
        var form = new Form();
        form.Shown += (_, _) => throw new DialogShownFailure();

        Assert.Throws<DialogShownFailure>(() => host.ShowDialog(form, owner));

        Assert.Single(host.Windows);
        Assert.True(owner.Input.Focus(button));
        Assert.DoesNotContain(form, Application.OpenForms);
    }

    [Fact]
    public void InvalidModalOwnershipIsRejectedBeforeDialogMutation()
    {
        TestWindowHost formerWindow;
        using (var former = ModernFormsTestHost.Create())
            formerWindow = former.Show(new Form());
        using var host = ModernFormsTestHost.Create();
        var owner = host.Show(new Form());
        var dialog = new Form();
        var originalSize = dialog.ClientSize;

        Assert.Throws<ArgumentException>(() => host.ShowDialog(dialog, formerWindow, new TestViewport(100, 100)));
        Assert.Throws<ArgumentException>(() => host.ShowDialog(owner.FormRoot!, owner));
        Assert.Equal(originalSize, dialog.ClientSize);
        Assert.False(dialog.Visible);
        Assert.Single(host.Windows);

        var active = host.ShowDialog(dialog, owner);
        var next = new Form();
        var nextSize = next.ClientSize;
        Assert.Throws<InvalidOperationException>(() => host.ShowDialog(next, owner, new TestViewport(100, 100)));
        Assert.Throws<ArgumentException>(() => host.ShowDialog(dialog, active));
        Assert.Equal(nextSize, next.ClientSize);
        Assert.False(next.Visible);
        Assert.Equal(2, host.Windows.Count);
    }

    [Fact]
    public async Task DialogCanUseHostedControlWrapperAsOwner()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button());
        var owner = host.Show(root);
        var form = new Form();
        var dialog = host.ShowDialog(form, owner);

        Assert.False(owner.Input.Focus(button));
        form.DialogResult = DialogResult.Abort;

        Assert.Equal(DialogResult.Abort, await dialog.DialogCompletion!);
        Assert.True(owner.Input.Focus(button));
    }

    private sealed class DialogClosingFailure : Exception;
    private sealed class DialogClosedFailure : Exception;
    private sealed class DialogShownFailure : Exception;
}
