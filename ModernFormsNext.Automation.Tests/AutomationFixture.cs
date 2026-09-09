using ModernFormsNext.Accessibility;
using ModernFormsNext.Testing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ModernFormsNext.Automation.Tests;

internal sealed class AutomationFixture : IDisposable
{
    internal ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
    internal Form Form { get; } = new() { Text = "Automation test" };
    internal AutomationSession Session { get; }
    internal AutomationRootRegistration Root { get; }

    internal AutomationFixture(AutomationQueryOptions? limits = null,
        AutomationCapability capabilities = AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions)
    {
        Host.Show(Form);
        Session = new(capabilities, limits);
        Root = Session.RegisterRoot(Form);
    }

    internal T Add<T>(T control) where T : Control => Form.Controls.Add(control);
    internal AutomationNodeHandle Handle(Control control) => new(Session.SessionId, control.AccessibilityObject.RuntimeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    internal AutomationNodeHandle Handle(AccessibleObject peer) => new(Session.SessionId, peer.RuntimeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    internal AutomationResult<AutomationNodeSnapshot> Inspect(Control control) => Session.InspectAsync(Root.RootId, Handle(control)).GetAwaiter().GetResult();
    internal AutomationActionResult Act(Control control, AccessibleActions action, AutomationActionValue? value = null)
        => Session.PerformActionAsync(Root.RootId, Handle(control), action, value).GetAwaiter().GetResult();
    internal AutomationResult<System.Collections.Immutable.ImmutableArray<AutomationNodeSnapshot>> Find(AutomationQuery query)
        => Session.FindAllAsync(Root.RootId, query).GetAwaiter().GetResult();
    public void Dispose() { Session.Dispose(); Host.Dispose(); }
}

internal static class CompletedTaskAssertions
{
    // UI-thread calls complete inline. Assert that contract before observing the result so
    // these synchronous, thread-affine TestHost tests can never block on a dispatcher job.
    internal static T Completed<T>(this Task<T> task)
    {
        Assert.True(task.IsCompletedSuccessfully);
        return task.GetAwaiter().GetResult();
    }

    internal static void Worker(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    internal static T Finish<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
}

// A deliberately raw custom child sequence lets the consumer test malformed canonical peers
// without changing #59's peer model or bypassing it with a separate testing tree.
internal sealed class SemanticControl : Control
{
    internal ScriptPeer Child { get; } = new();
    protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
    private sealed class Peer : ControlAccessibleObject
    {
        private readonly SemanticControl owner;
        internal Peer(SemanticControl owner) : base(owner) { this.owner = owner; owner.Child.ParentValue = this; }
        public override int GetChildCount() => 1;
        public override AccessibleObject? GetChild(int index) => index == 0 ? owner.Child : null;
    }
}

internal sealed class ScriptPeer : AccessibleObject
{
    internal AccessibleObject? ParentValue { get; set; }
    internal Func<AccessibleObject?>? ParentGetter { get; set; }
    internal Func<string?>? NameGetter { get; set; }
    internal Func<string?>? ValueGetter { get; set; }
    internal Func<bool>? SensitiveGetter { get; set; }
    internal Func<AccessibleStates>? StateGetter { get; set; }
    internal Func<int>? CountGetter { get; set; }
    internal Func<int, AccessibleObject?>? ChildGetter { get; set; }
    internal Func<AccessibleActions>? ActionsGetter { get; set; }
    internal Func<AccessibleActions, object?, bool>? Action { get; set; }
    internal Func<AccessibleRangeValue?>? RangeGetter { get; set; }
    internal List<AccessibleObject> Children { get; } = [];
    internal AccessibleStates States { get; set; }
    internal AccessibilityView Projection { get; set; } = AccessibilityView.Control;
    internal AccessibleActions Actions { get; set; } = AccessibleActions.Invoke;
    internal ScriptPeer Add(ScriptPeer child) { child.ParentValue = this; Children.Add(child); return child; }
    public override AccessibleObject? Parent => ParentGetter is null ? ParentValue : ParentGetter();
    public override string? Name { get => NameGetter is null ? base.Name : NameGetter(); set => base.Name = value; }
    public override string? Value { get => ValueGetter is null ? base.Value : ValueGetter(); set => base.Value = value; }
    public override bool IsSensitive => SensitiveGetter?.Invoke() ?? false;
    public override AccessibleStates State => StateGetter?.Invoke() ?? States;
    public override AccessibilityView View => Projection;
    public override AccessibleActions SupportedActions => ActionsGetter?.Invoke() ?? Actions;
    public override AccessibleRangeValue? RangeValue => RangeGetter?.Invoke();
    public override int GetChildCount() => CountGetter?.Invoke() ?? Children.Count;
    public override AccessibleObject? GetChild(int index) => ChildGetter is null ? Children.ElementAtOrDefault(index) : ChildGetter(index);
    public override bool PerformAction(AccessibleActions action, object? parameter = null) => Action?.Invoke(action, parameter) ?? true;
}
