using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class WindowVisibleChangedTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstShowPublishesCommittedVisibilityAfterLoadAndBeforeNativeShow(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        var backend = Backend(form);
        List<string> order = [];
        form.Load += (_, _) => order.Add("Load");
        form.VisibleChanged += (sender, args) => {
            Assert.Same(form, sender);
            Assert.Same(EventArgs.Empty, args);
            if (!form.Visible) return;
            Assert.Equal(0, backend.ShowCallCount);
            Assert.False(backend.IsShown);
            order.Add("Visible:true");
        };
        backend.Activated += () => order.Add("NativeShow");
        form.Shown += (_, _) => order.Add("Shown");

        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();

        Assert.Equal(new[] { "Load", "Visible:true", "NativeShow", "Shown" }, order);
        form.Close();
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Fact]
    public void OnlyActualTransitionsPublishAndInstancesRemainIndependent()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var other = new Form();
        List<bool> values = [];
        int loads = 0, shown = 0, otherEvents = 0;
        form.VisibleChanged += (_, _) => values.Add(form.Visible);
        form.Load += (_, _) => loads++;
        form.Shown += (_, _) => shown++;
        other.VisibleChanged += (_, _) => otherEvents++;

        form.Hide();
        form.Show();
        form.Show();
        form.Hide();
        form.Hide();
        form.Show();
        form.Close();
        form.Close();

        Assert.Equal(new[] { true, false, true, false }, values);
        Assert.Equal(1, loads);
        Assert.Equal(1, shown);
        Assert.Equal(0, otherEvents);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ProgrammaticAndBackendCloseNotifyBeforeClosedWithoutDuplicateAfterHide(bool native, bool hideFirst)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        List<string> order = [];
        form.VisibleChanged += (_, _) => order.Add("Visible:" + form.Visible);
        form.Closed += (_, _) => { Assert.False(form.Visible); order.Add("Closed"); };
        form.Show();
        if (hideFirst) form.Hide();

        if (native) Backend(form).Dispose();
        else form.Close();

        Assert.Equal(new[] { "Visible:True", "Visible:False", "Closed" }, order);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(form).IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanceledCloseAndNeverShownCloseDoNotInventTransitions(bool cancel)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        List<bool> values = [];
        form.VisibleChanged += (_, _) => values.Add(form.Visible);
        if (cancel) {
            form.Show();
            form.Closing += (_, e) => e.Cancel = true;
        }
        form.Close();
        Assert.Equal(cancel ? new[] { true } : Array.Empty<bool>(), values);
        Assert.Equal(cancel, form.Visible);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HideOrCloseFromTrueObserverCancelsPendingNativeShow(bool modal, bool close)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        List<bool> values = [];
        int shown = 0;
        form.VisibleChanged += (_, _) => {
            values.Add(form.Visible);
            if (form.Visible) {
                if (close) form.Close();
                else form.Hide();
            }
        };
        form.Shown += (_, _) => shown++;

        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();

        Assert.Equal(new[] { true, false }, values);
        Assert.False(form.Visible);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.Equal(0, shown);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowFromTrueObserverIsANoOpAndPreservesModalOperation(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        int events = 0;
        form.VisibleChanged += (_, _) => {
            if (!form.Visible) return;
            events++;
            form.Show();
            Assert.Equal(0, Backend(form).ShowCallCount);
        };

        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();

        Assert.Equal(1, events);
        Assert.Equal(1, Backend(form).ShowCallCount);
        if (modal) {
            Assert.False(completion!.IsCompleted);
            Assert.False(Backend(owner).IsEnabled);
        }
        form.Close();
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowFromFalseObserverSurvivesOlderHideEvenIfObserverThenThrows(bool throws)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        List<bool> values = [];
        var error = new InvalidOperationException("after newer show");
        bool observe = true;
        form.VisibleChanged += (_, _) => {
            if (!observe) return;
            values.Add(form.Visible);
            if (!form.Visible) {
                form.Show();
                if (throws) throw error;
            }
        };

        if (throws) Assert.Same(error, Assert.Throws<InvalidOperationException>(form.Hide));
        else form.Hide();

        Assert.Equal(new[] { false, true }, values);
        Assert.True(form.Visible);
        Assert.True(Backend(form).IsShown);
        Assert.Equal(2, Backend(form).ShowCallCount);
        observe = false;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowObserverFailureRollsBackAndAllowsRetryWithoutReload(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var editor = owner.Controls.Add(new TextBox());
        owner.Show();
        editor.Select();
        using var form = new Form();
        using var child = form.Controls.Add(new Label());
        int disposed = 0;
        child.Disposed += (_, _) => disposed++;
        int loads = 0;
        form.Load += (_, _) => loads++;
        List<bool> values = [];
        var error = new InvalidOperationException("visible observer");
        bool fail = true;
        form.VisibleChanged += (_, _) => {
            values.Add(form.Visible);
            if (form.Visible && fail) throw error;
        };

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => {
            if (modal) _ = form.ShowDialog(owner);
            else form.Show();
        }));

        Assert.Equal(new[] { true, false }, values);
        Assert.False(form.Visible);
        Assert.False(Backend(form).IsShown);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.Equal(0, disposed);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        Assert.NotNull(owner.TextInputClient);
        fail = false;
        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();
        Assert.Equal(1, loads);
        form.Close();
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Fact]
    public void ShowAndRollbackObserverFailuresAreAggregatedAfterReturningToHidden()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var showing = new InvalidOperationException("show");
        var hiding = new InvalidOperationException("hide");
        form.VisibleChanged += (_, _) => throw (form.Visible ? showing : hiding);

        var failure = Assert.Throws<AggregateException>(form.Show);

        Assert.Contains(showing, failure.Flatten().InnerExceptions);
        Assert.Contains(hiding, failure.Flatten().InnerExceptions);
        Assert.False(form.Visible);
        Assert.False(Backend(form).IsShown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HideObserverFailureDoesNotSkipNativeHideOrPopupOwnerRestoration(bool popupWindow)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var ownerEditor = owner.Controls.Add(new TextBox());
        var ownerHost = host.Show(owner);
        ownerEditor.Select();
        using WindowBase window = popupWindow ? new PopupWindow(owner) { Size = new Size(200, 80) } : new Form();
        using var editor = window.Controls.Add(new TextBox());
        if (window is PopupWindow popup) popup.Show(0, 0);
        else window.Show();
        editor.Select();
        var oldClient = window.TextInputClient!;
        oldClient.SetComposingText("committed");
        var error = new InvalidOperationException("hide observer");
        int laterObserver = 0;
        window.VisibleChanged += (_, _) => { if (!window.Visible) throw error; };
        window.VisibleChanged += (_, _) => laterObserver++;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(window.Hide));

        Assert.False(window.Visible);
        Assert.Null(oldClient.GetState());
        Assert.Null(window.TextInputClient);
        Assert.Equal(1, laterObserver);
        if (popupWindow) {
            Assert.False(Assert.Single(Backend(owner).LivePopups).State.IsShown);
            Assert.Null(ownerHost.ActivePopup);
            Assert.NotNull(owner.TextInputClient);
        }
        else Assert.False(Backend(window).IsShown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseObserverFailureStillCompletesBackendAndModalCleanup(bool native)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        using var editor = owner.Controls.Add(new TextBox());
        owner.Show();
        editor.Select();
        using var form = new Form();
        var completion = form.ShowDialog(owner);
        var error = new InvalidOperationException("close visibility observer");
        int notified = 0, closed = 0;
        form.VisibleChanged += (_, _) => { if (!form.Visible) throw error; };
        form.VisibleChanged += (_, _) => { Assert.False(form.Visible); notified++; };
        form.Closed += (_, _) => closed++;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => {
            if (native) Backend(form).Dispose();
            else form.DialogResult = DialogResult.OK;
        }));

        Assert.Equal(native ? DialogResult.None : DialogResult.OK, await completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, notified);
        Assert.Equal(1, closed);
        Assert.True(Backend(form).IsDisposed);
        Assert.False(form.Visible);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        Assert.NotNull(owner.TextInputClient);
    }

    [Fact]
    public void SupersededTrueTransitionDoesNotNotifyLaterObserversWithFalseState()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        List<bool> laterValues = [];
        form.VisibleChanged += (_, _) => { if (form.Visible) form.Hide(); };
        form.VisibleChanged += (_, _) => laterValues.Add(form.Visible);

        form.Show();

        Assert.Equal(new[] { false }, laterValues);
        Assert.Equal(0, Backend(form).ShowCallCount);
    }

    [Fact]
    public void LoadThatCancelsDisplayDoesNotCreateAVisibilityTransition()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        int events = 0;
        form.Load += (_, _) => form.Hide();
        form.VisibleChanged += (_, _) => events++;
        form.Show();
        Assert.Equal(0, events);
        form.Show();
        Assert.Equal(1, events);
    }

    [Fact]
    public void ProtectedHookUsesTheSameDeduplicatedBoundary()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new ObservingForm();
        int events = 0;
        form.VisibleChanged += (_, _) => events++;
        form.Show();
        form.Show();
        form.Hide();
        form.Hide();
        form.Close();
        Assert.Equal(new[] { true, false }, form.Values);
        Assert.Equal(2, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseFromFalseObserverDoesNotRunOlderHideOrNotifyAfterClosed(bool native)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        List<string> order = [];
        form.VisibleChanged += (_, _) => {
            order.Add("Visible:False");
            if (native) Backend(form).Dispose();
            else form.Close();
        };
        form.VisibleChanged += (_, _) => order.Add("Obsolete");
        form.Closed += (_, _) => order.Add("Closed");

        form.Hide();

        Assert.Equal(new[] { "Visible:False", "Closed" }, order);
        Assert.True(Backend(form).IsDisposed);
        Assert.False(form.Visible);
    }

    [Fact]
    public async Task HiddenEstablishedModalOperationCanStillCloseAfterObserverFailure()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        var completion = form.ShowDialog(owner);
        var error = new InvalidOperationException("hidden modal observer");
        form.VisibleChanged += (_, _) => { if (!form.Visible) throw error; };

        Assert.Same(error, Assert.Throws<InvalidOperationException>(form.Hide));
        Assert.False(Backend(form).IsShown);
        // Hide retains the already established modal operation, as before this event existed.
        Assert.False(completion.IsCompleted);
        form.DialogResult = DialogResult.Cancel;

        Assert.Equal(DialogResult.Cancel, await completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(Backend(owner).IsEnabled);
        Assert.True(Backend(form).IsDisposed);
    }

    private static HeadlessWindowImpl Backend(WindowBase window) => Assert.IsType<HeadlessWindowImpl>(
        typeof(WindowBase).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));

    private sealed class ObservingForm : Form
    {
        internal List<bool> Values { get; } = [];
        protected override void OnVisibleChanged(EventArgs e) { Values.Add(Visible); base.OnVisibleChanged(e); }
    }
}
