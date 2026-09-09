using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Testing;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Lifecycle")]
public sealed class LifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StopAndDisposeInvalidateAllHandlesAndAreIdempotent(bool dispose)
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button()); var handle = f.Handle(b);
        if (dispose) f.Session.Dispose(); else f.Session.Stop();
        f.Session.Stop(); f.Session.Dispose();
        Assert.True(f.Session.IsStopped); Assert.False(f.Root.IsRegistered);
        Assert.Equal(AutomationErrorCode.SessionEnded, f.Session.GetRootsAsync().Completed().Error);
        Assert.Equal(AutomationErrorCode.SessionEnded, f.Session.InspectAsync(f.Root.RootId, handle).Completed().Error);
        Assert.Equal(AutomationErrorCode.SessionEnded, f.Session.FindOneAsync(f.Root.RootId, new()).Completed().Error);
        Assert.Equal(AutomationErrorCode.SessionEnded, f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke).Completed().Error);
        Assert.Throws<ObjectDisposedException>(() => f.Session.RegisterRoot(new Form()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingOneWindowUnregistersItAndPreservesAnother(bool dispose)
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button()); var handle = f.Handle(b);
        var other = new Form(); f.Host.Show(other); using var otherRoot = f.Session.RegisterRoot(other);
        if (dispose) f.Form.Dispose(); else f.Form.Close();
        Assert.False(f.Root.IsRegistered); Assert.True(otherRoot.IsRegistered); Assert.False(f.Session.IsStopped);
        Assert.Equal(otherRoot.RootId, Assert.Single(f.Session.GetRootsAsync().Completed().Value).RootId);
        Assert.Equal(AutomationErrorCode.StaleNode, f.Session.InspectAsync(f.Root.RootId, handle).Completed().Error);
        Assert.Throws<ObjectDisposedException>(() => f.Session.RegisterRoot(f.Form));
    }

    [Fact]
    public void CancelledClosePreservesRoot()
    {
        using var f = new AutomationFixture();
        f.Form.Closing += (_, e) => e.Cancel = true;
        f.Form.Close();
        Assert.True(f.Root.IsRegistered);
        Assert.Single(f.Session.GetRootsAsync().Completed().Value);
    }

    [Fact]
    public void ExplicitUnregisterAndReregisterHaveDistinctScopeIdentity()
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button()); var handle = f.Handle(b);
        f.Root.Dispose(); f.Root.Dispose();
        using var newRoot = f.Session.RegisterRoot(f.Form);
        Assert.NotEqual(newRoot.RootId, f.Root.RootId);
        Assert.Equal(AutomationErrorCode.StaleNode, f.Session.InspectAsync(f.Root.RootId, handle).Completed().Error);
        Assert.Equal(AutomationErrorCode.None, f.Session.InspectAsync(newRoot.RootId, handle).Completed().Error);
    }

    [Fact]
    public void NoRootEnumerationHappensWithoutExplicitRegistration()
    {
        using var host = ModernFormsTestHost.Create(); var form = new Form(); host.Show(form);
        using var session = new AutomationSession();
        Assert.Empty(session.GetRootsAsync().Completed().Value);
        using var root = session.RegisterRoot(form);
        Assert.Throws<InvalidOperationException>(() => session.RegisterRoot(form));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SurfaceDisposalOrControlDisposalInvalidatesRegistration(bool disposeControl)
    {
        using var host = ModernFormsTestHost.Create(); using var control = new Panel(); using var surface = new SkiaControlSurface(control);
        surface.Resize(300, 200);
        var text = control.Controls.Add(new TextBox { Name = "field" });
        using var session = new AutomationSession(); using var root = session.RegisterRoot(surface);
        var result = session.FindOneAsync(root.RootId, new() { AutomationId = "field" }).Completed();
        Assert.Equal(AutomationErrorCode.None, result.Error);
        Assert.Equal(AutomationCoordinateSpace.Surface, result.Value!.Bounds.CoordinateSpace);
        Assert.Equal(AutomationActionStatus.Accepted, session.PerformActionAsync(root.RootId, result.Value.Handle,
            AccessibleActions.SetValue, AutomationActionValue.FromText("surface text")).Completed().Status);
        Assert.Equal("surface text", text.Text);
        if (disposeControl) control.Dispose(); else surface.Dispose();
        Assert.False(root.IsRegistered); Assert.Empty(session.GetRootsAsync().Completed().Value);
        Assert.Equal(AutomationErrorCode.StaleNode, session.InspectAsync(root.RootId, result.Value.Handle).Completed().Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegistrationsAndSnapshotsNeverRetainBorrowedControls(bool stop)
    {
        using var host = ModernFormsTestHost.Create(); using var session = new AutomationSession();
        var weak = RegisterAndReleaseSurface(session);
        if (stop) session.Stop();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.Control.IsAlive); Assert.False(weak.Surface.IsAlive);
        Assert.False(weak.Registration.IsRegistered);
        GC.KeepAlive(weak.Snapshot); GC.KeepAlive(weak.Registration);
    }

    [Fact]
    public void CapabilitiesAreIntersectedAndEnforcedIndependently()
    {
        using var f = new AutomationFixture(capabilities: AutomationCapability.Inspect | AutomationCapability.Query);
        var b = f.Add(new Button());
        Assert.Equal(AutomationErrorCode.CapabilityDenied, f.Act(b, AccessibleActions.Invoke).Error);
        using var onlyActions = new AutomationSession(AutomationCapability.Actions);
        using var actionRoot = onlyActions.RegisterRoot(f.Form);
        Assert.Equal(AutomationErrorCode.CapabilityDenied, onlyActions.GetRootsAsync().Completed().Error);
        Assert.Equal(AutomationErrorCode.CapabilityDenied, onlyActions.FindAllAsync(actionRoot.RootId, new()).Completed().Error);
        Assert.Equal(AutomationActionStatus.Accepted, onlyActions.PerformActionAsync(actionRoot.RootId,
            new(onlyActions.SessionId, b.AccessibilityObject.RuntimeId.ToString()), AccessibleActions.Invoke).Completed().Status);
        using var limited = new AutomationSession(); using var limitedRoot = limited.RegisterRoot(f.Form, AutomationCapability.Inspect);
        Assert.Equal(AutomationCapability.Inspect, limitedRoot.Capabilities);
        Assert.Equal(AutomationErrorCode.CapabilityDenied, limited.FindAllAsync(limitedRoot.RootId, new()).Completed().Error);
        Assert.Equal(AutomationErrorCode.CapabilityDenied, limited.PerformActionAsync(limitedRoot.RootId,
            new(limited.SessionId, b.AccessibilityObject.RuntimeId.ToString()), AccessibleActions.Invoke).Completed().Error);
    }

    [Fact]
    public void InvalidSessionBudgetsAndCapabilitiesAreRejected()
    {
        using var host = ModernFormsTestHost.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationSession((AutomationCapability)32));
        foreach (var options in new[] { new AutomationQueryOptions { MaxNodes = 0 }, new() { MaxResults = 0 }, new() { MaxDepth = -1 }, new() { MaxDepth = 257 }, new() { MaxNodes = int.MaxValue } })
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationSession(limits: options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndDuringSnapshotGetterDoesNotReturnLiveSuccess(bool stopSession)
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        c.Child.NameGetter = () => { if (stopSession) f.Session.Stop(); else f.Root.Dispose(); return "after end"; };
        var result = f.Find(new());
        Assert.Equal(stopSession ? AutomationErrorCode.SessionEnded : AutomationErrorCode.StaleNode, result.Error);
        Assert.False(result.Value.IsDefault);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Control, WeakReference Surface, AutomationRootRegistration Registration, AutomationNodeSnapshot Snapshot) RegisterAndReleaseSurface(AutomationSession session)
    {
        var control = new Panel(); var surface = new SkiaControlSurface(control); surface.Resize(300, 200);
        var registration = session.RegisterRoot(surface);
        var root = session.GetRootsAsync().Completed().Value[0];
        var snapshot = session.InspectAsync(root.RootId, root.Handle).Completed().Value!;
        return (new(control), new(surface), registration, snapshot);
    }
}
