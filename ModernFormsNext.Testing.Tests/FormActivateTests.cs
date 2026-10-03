using System.Reflection;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class FormActivateTests
{
    [Fact]
    public void RequestForwardsWithoutInventingConfirmationOrOtherLifecycleEvents()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        var backend = Backend(form);
        backend.Deactivated!();
        var confirm = backend.Activated!;
        int requests = 0, activated = 0, other = 0;
        // Intercept the headless backend's synchronous confirmation. The public method must
        // only reach the backend; delivery can be delayed or refused by a real platform.
        backend.Activated = () => requests++;
        form.Activated += (_, _) => { Assert.True(form.IsActive); activated++; };
        form.Load += (_, _) => other++;
        form.Shown += (_, _) => other++;
        form.VisibleChanged += (_, _) => other++;
        form.WindowStateChanged += (_, _) => other++;
        form.DpiChanged += (_, _) => other++;
        form.Resize += (_, _) => other++;
        form.SizeChanged += (_, _) => other++;
        form.LocationChanged += (_, _) => other++;
        form.ClientSizeChanged += (_, _) => other++;

        form.Activate();
        form.Activate();
        Assert.Equal(2, requests);
        Assert.False(form.IsActive);
        Assert.Equal(0, activated);
        Assert.True(form.Visible);
        Assert.Equal(1, backend.ShowCallCount);

        confirm();
        confirm(); // Repeated platform confirmation is already deduplicated by WindowBase.
        form.Activate();
        Assert.True(form.IsActive);
        Assert.Equal(1, activated);
        Assert.Equal(2, requests);
        Assert.Equal(0, other);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenFormIsRejectedWithoutShowing(bool previouslyShown)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        if (previouslyShown) { form.Show(); form.Hide(); }
        var backend = Backend(form);
        int calls = backend.ShowCallCount, events = 0;
        form.Activated += (_, _) => events++;
        form.VisibleChanged += (_, _) => events++;
        Assert.Throws<InvalidOperationException>(form.Activate);
        Assert.False(form.Visible);
        Assert.False(form.IsActive);
        Assert.False(backend.IsShown);
        Assert.Equal(calls, backend.ShowCallCount);
        Assert.Equal(0, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedFormCannotReachBackendOrRejoinOpenForms(bool previouslyShown)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        if (previouslyShown) form.Show();
        var backend = Backend(form);
        var staleConfirmation = backend.Activated!;
        form.Close();
        int requests = 0, events = 0;
        backend.Activated = () => requests++;
        form.Activated += (_, _) => events++;
        Assert.Throws<ObjectDisposedException>(form.Activate);
        staleConfirmation();
        Assert.Equal(0, requests);
        Assert.Equal(0, events);
        Assert.False(form.IsActive);
        Assert.False(form.Visible);
        Assert.True(backend.IsDisposed);
        Assert.DoesNotContain(form, Application.OpenForms);
    }

    [Fact]
    public void DisposedFormIsRejectedUsingExistingLifetimeGuard()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        form.Show();
        Backend(form).Deactivated!();
        form.Dispose();
        try {
            Assert.Throws<ObjectDisposedException>(form.Activate);
            Assert.False(form.IsActive);
        }
        finally { form.Close(); } // Dispose and native Close retain their existing distinct roles.
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("hide")]
    [InlineData("close")]
    public void ReentrantObserverUsesExistingTransitionGuards(string operation)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        var backend = Backend(form);
        backend.Deactivated!();
        var confirm = backend.Activated!;
        int requests = 0, events = 0, later = 0;
        backend.Activated = () => { requests++; confirm(); };
        form.Activated += (_, _) => {
            Assert.True(form.IsActive);
            events++;
            if (operation == "activate") form.Activate();
            else if (operation == "hide") form.Hide();
            else form.Close();
        };
        form.Activated += (_, _) => later++;
        form.Activate();
        Assert.Equal(1, requests);
        Assert.Equal(1, events);
        Assert.Equal(operation == "activate" ? 1 : 0, later);
        Assert.Equal(operation == "activate", form.IsActive);
        Assert.Equal(operation == "activate", form.Visible);
        Assert.Equal(1, backend.ShowCallCount);
    }

    [Fact]
    public void MinimizedRequestDoesNotRestoreOrFabricateState()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { WindowState = FormWindowState.Minimized };
        form.Show();
        var backend = Backend(form);
        backend.Deactivated!();
        int requests = 0, events = 0;
        backend.Activated = () => requests++;
        form.WindowStateChanged += (_, _) => events++;
        form.Activated += (_, _) => events++;
        form.Activate();
        Assert.Equal(1, requests);
        Assert.Equal(FormWindowState.Minimized, form.WindowState);
        Assert.False(form.IsActive);
        Assert.Equal(0, events);
    }

    [Fact]
    public void BackendFailurePropagatesWithoutFakeSuccessOrRollback()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        var backend = Backend(form);
        backend.Deactivated!();
        var failure = new InvalidOperationException("platform request failed");
        backend.Activated = () => throw failure;
        int events = 0;
        form.Activated += (_, _) => events++;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(form.Activate));
        Assert.False(form.IsActive);
        Assert.True(form.Visible);
        Assert.True(backend.IsShown);
        Assert.Equal(0, events);
    }

    [Fact]
    public void MultipleFormsChangeActivityOnlyThroughTheirBackendConfirmations()
    {
        using var host = ModernFormsTestHost.Create();
        using var first = new Form();
        using var second = new Form();
        first.Show(); second.Show();
        var one = Backend(first); var two = Backend(second);
        one.Deactivated!();
        int activated = 0, deactivated = 0;
        first.Activated += (_, _) => activated++;
        second.Deactivated += (_, _) => { Assert.False(second.IsActive); deactivated++; };
        var confirm = one.Activated!;
        // Headless does not emulate global native focus arbitration. Publish the OS ordering.
        one.Activated = () => { two.Deactivated!(); confirm(); };
        first.Activate();
        first.Activate();
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
        Assert.Equal(1, activated);
        Assert.Equal(1, deactivated);
    }

    [Fact]
    public void ModalActivationDoesNotEnableOwnerOrCompleteDialog()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var dialog = new Form();
        owner.Show();
        var task = dialog.ShowDialog(owner);
        var parent = Backend(owner); var child = Backend(dialog);
        parent.Deactivated!(); child.Deactivated!();
        int ownerEvents = 0, dialogEvents = 0;
        owner.Activated += (_, _) => ownerEvents++;
        dialog.Activated += (_, _) => dialogEvents++;
        owner.Activate();
        Assert.False(owner.IsActive);
        Assert.Equal(0, ownerEvents);
        dialog.Activate(); dialog.Activate();
        Assert.True(dialog.IsActive);
        Assert.Equal(1, dialogEvents);
        Assert.False(parent.IsEnabled);
        Assert.False(task.IsCompleted);
        Assert.Equal(DialogResult.None, dialog.DialogResult);
        dialog.Close();
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(parent.IsEnabled);
    }

    [Fact]
    public void CanceledCloseKeepsVisibleFormEligibleForActivation()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        Backend(form).Deactivated!();
        form.Closing += (_, e) => e.Cancel = true;
        form.Close();
        form.Activate();
        Assert.True(form.IsActive);
        Assert.True(form.Visible);
    }

    [Fact]
    public void ActivateDuringLoadCannotImplicitlyShowTheForm()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Load += (_, _) => {
            Assert.Throws<InvalidOperationException>(form.Activate);
            Assert.Equal(0, Backend(form).ShowCallCount);
        };
        form.Show();
        Assert.Equal(1, Backend(form).ShowCallCount);
    }

    private static HeadlessWindowImpl Backend(Form form) => Assert.IsType<HeadlessWindowImpl>(
        typeof(WindowBase).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
}
