using System.Reflection;
using System.Windows.Input;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(InputBindingCollectionTests.Name)]
public sealed partial class CommandRoutingTests : IDisposable
{
    public CommandRoutingTests() { Application.ReleaseCommandBindings(); Application.ReleaseInputBindings(); }
    public void Dispose() { Application.ReleaseCommandBindings(); Application.ReleaseInputBindings(); }

    [Fact]
    public void CommandHasReferenceIdentityAndNoTargetlessAmbientContext()
    {
        var command = new RoutedCommand("Save");
        ICommand contract = Assert.IsAssignableFrom<ICommand>(command);
        Assert.Equal("Save", command.Name);
        Assert.NotEqual(command, new RoutedCommand("Save"));
        Application.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        Assert.False(contract.CanExecute(null));
        contract.Execute(null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DirectTargetFindsNearestAvailableControlParentWindowOrApplication(int firstScope)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var parameter = new object();
        int winner = -1;
        var scopes = Scopes(ui);
        for (int i = firstScope; i < scopes.Length; i++)
        {
            int scope = i;
            scopes[i].Add(Bind(command, (_, e) => {
                Assert.Same(command, e.Command);
                Assert.Same(parameter, e.Parameter);
                Assert.Same(ui.Focus, e.Target);
                Assert.Null(e.Source);
                winner = scope;
            }));
        }
        Assert.True(command.CanExecute(parameter, ui.Focus));
        Assert.Equal(-1, winner);
        command.Execute(parameter, ui.Focus);
        Assert.Equal(firstScope, winner);
    }

    [Fact]
    public void NearestAncestorWinsBeforeOuterAncestor()
    {
        using var ui = new WindowFixture();
        var outer = ui.Form.Controls.Add(new Panel());
        outer.Controls.Add(ui.Parent);
        var command = new RoutedCommand();
        int calls = 0;
        ui.Parent.CommandBindings.Add(Bind(command, (_, _) => calls++));
        outer.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        command.Execute(null, ui.Focus);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoMatchingOrNoAvailabilityHandlerIsUnavailable(bool register)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand("Save");
        ui.Focus.CommandBindings.Add(Bind(new RoutedCommand("Save"), (_, _) => Assert.Fail()));
        if (register) ui.Focus.CommandBindings.Add(new CommandBinding(command, (_, _) => Assert.Fail()));
        Assert.False(command.CanExecute(null, ui.Focus));
        command.Execute(null, ui.Focus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FalseQueryFallsThroughUnlessExplicitlyHandled(bool veto)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        int calls = 0;
        ui.Focus.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, e) => e.Handled = veto));
        ui.Parent.CommandBindings.Add(Bind(command, (_, _) => calls++));
        Assert.Equal(!veto, command.CanExecute(null, ui.Focus));
        command.Execute(null, ui.Focus);
        Assert.Equal(veto ? 0 : 1, calls);
    }

    [Fact]
    public void DuplicateBindingsContinueInInsertionOrderUntilExecutionHandled()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<int>();
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => calls.Add(1), handled: false));
        ui.Focus.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, _) => { }));
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => calls.Add(2)));
        ui.Parent.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        command.Execute(null, ui.Focus);
        Assert.Equal([1, 2], calls);
    }

    [Fact]
    public void UnhandledExecutionBubblesAndQueriesEachLaterBindingBeforeExecutingIt()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<string>();
        ui.Focus.CommandBindings.Add(new(command, (_, _) => calls.Add("local execute"),
            (_, e) => { calls.Add("local query"); e.CanExecute = true; e.Handled = true; }));
        ui.Form.CommandBindings.Add(new(command, (_, e) => { calls.Add("window execute"); e.Handled = true; },
            (_, e) => { calls.Add("window query"); e.CanExecute = true; }));
        command.Execute(null, ui.Focus);
        Assert.Equal(["local query", "local execute", "window query", "window execute"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachedOrDisposedTargetsAreRejected(bool dispose)
    {
        using var target = new Button();
        var command = new RoutedCommand();
        target.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        if (dispose) target.Dispose();
        Assert.False(command.CanExecute(null, target));
        command.Execute(null, target);
    }

    [Fact]
    public void ExistingStandaloneSurfaceRootsPermitRoutingAndDetachRejectsIt()
    {
        using var root = new Panel();
        var target = root.Controls.Add(new Button());
        var command = new RoutedCommand();
        int calls = 0;
        root.CommandBindings.Add(Bind(command, (_, _) => calls++));
        using (var surface = new SkiaControlSurface(root)) command.Execute(null, target);
        command.Execute(null, target);
        Assert.Equal(1, calls);
        Assert.False(command.CanExecute(null, target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedRouteSurvivesReparentOrRemovalInExecution(bool remove)
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<string>();
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => {
            calls.Add("target");
            if (remove) ui.Focus.Parent = null; else other.Parent.Controls.Add(ui.Focus);
        }, handled: false));
        ui.Parent.CommandBindings.Add(Bind(command, (_, _) => calls.Add("original parent")));
        other.Parent.CommandBindings.Add(Bind(command, (_, _) => calls.Add("new parent")));
        command.Execute(null, ui.Focus);
        Assert.Equal(["target", "original parent"], calls);
        ui.Focus.CommandBindings.Clear();
        command.Execute(null, ui.Focus);
        Assert.Equal(remove ? new[] { "target", "original parent" } : ["target", "original parent", "new parent"], calls);
        if (remove) ui.Focus.Dispose();
    }

    [Fact]
    public void QueryAndExecutionShareSnapshotEvenWhenQueryChangesParents()
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<string>();
        ui.Focus.CommandBindings.Add(new(command, (_, _) => calls.Add("target"), (_, e) => {
            other.Parent.Controls.Add(ui.Focus); e.CanExecute = true;
        }));
        ui.Parent.CommandBindings.Add(Bind(command, (_, _) => calls.Add("original")));
        other.Parent.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        command.Execute(null, ui.Focus);
        Assert.Equal(["target", "original"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingEditsAffectNextInvocationNotCapturedRegistrations(bool add)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<string>();
        var later = Bind(command, (_, _) => calls.Add("parent"));
        if (!add) ui.Parent.CommandBindings.Add(later);
        bool first = true;
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => {
            calls.Add("target");
            if (!first) return;
            first = false;
            if (add) ui.Parent.CommandBindings.Add(later); else ui.Parent.CommandBindings.Remove(later);
        }, handled: false));
        command.Execute(null, ui.Focus);
        Assert.Equal(add ? new[] { "target" } : ["target", "parent"], calls);
        calls.Clear();
        command.Execute(null, ui.Focus);
        Assert.Equal(add ? new[] { "target", "parent" } : ["target"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingWindowDuringQueryOrExecutionStopsTheCapturedRoute(bool duringQuery)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        int executed = 0;
        ui.Focus.CommandBindings.Add(new(command, (_, _) => { executed++; ui.Form.Close(); }, (_, e) => {
            e.CanExecute = true;
            if (duringQuery) ui.Form.Close();
        }));
        Application.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        command.Execute(null, ui.Focus);
        Assert.Equal(duringQuery ? 0 : 1, executed);
        Assert.False(command.CanExecute(null, ui.Focus));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HandlerExceptionsPropagateUnchangedAndDoNotCorruptNextInvocation(bool query)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var failure = new InvalidOperationException("sensitive exception content");
        bool fail = true;
        int calls = 0;
        ui.Focus.CommandBindings.Add(new(command, (_, e) => {
            if (fail && !query) throw failure;
            calls++; e.Handled = true;
        }, (_, e) => { if (fail && query) throw failure; e.CanExecute = true; }));
        command.Diagnostic += (_, e) => { if (e.Kind == CommandRoutingDiagnosticKind.Failed) throw new Exception("observer"); };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => command.Execute(null, ui.Focus)));
        fail = false;
        command.Execute(null, ui.Focus);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedAndRecursiveCommandsUseIndependentInvocationState(bool sameCommand)
    {
        using var ui = new WindowFixture();
        var outer = new RoutedCommand("outer");
        var inner = sameCommand ? outer : new RoutedCommand("inner");
        var calls = new List<int>();
        ui.Focus.CommandBindings.Add(Bind(outer, (_, e) => {
            int depth = (int)e.Parameter!;
            calls.Add(depth);
            if (depth == 0) inner.Execute(1, ui.Focus);
            calls.Add(depth + 10);
        }));
        if (!sameCommand) ui.Focus.CommandBindings.Add(Bind(inner, (_, _) => { calls.Add(1); calls.Add(11); }));
        outer.Execute(0, ui.Focus);
        Assert.Equal([0, 1, 11, 10], calls);
    }

    private static CommandBinding Bind(RoutedCommand command, EventHandler<ExecutedCommandEventArgs> execute, bool handled = true)
        => new(command, (s, e) => { execute(s, e); e.Handled = handled; }, (_, e) => e.CanExecute = true);

    private static CommandBindingCollection[] Scopes(WindowFixture ui)
        => [ui.Focus.CommandBindings, ui.Parent.CommandBindings, ui.Form.CommandBindings, Application.CommandBindings];

    // Existing backend callback seam: no production input simulator or separate command tree.
    private sealed class WindowFixture : IDisposable
    {
        private readonly KeyboardDevice keyboard = new();
        internal WindowProxy Proxy { get; }
        internal Form Form { get; }
        internal Panel Parent { get; }
        internal Button Focus { get; }

        internal WindowFixture()
        {
            var platform = DispatchProxy.Create<IWindowImpl, WindowProxy>();
            Proxy = (WindowProxy)(object)platform;
            Form = new Form(platform);
            Parent = Form.Controls.Add(new Panel { Width = 500, Height = 300 });
            Focus = Parent.Controls.Add(new Button { Text = "Target" });
            Focus.Select();
        }

        internal RawKeyEventArgs Key(bool down = true)
        {
            var e = new RawKeyEventArgs(keyboard, 0, Form.adapter,
                down ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp, WindowKit.Input.Key.S, RawInputModifiers.Control);
            Proxy.Input!(e);
            return e;
        }

        public void Dispose() { Form.Dispose(); Form.adapter.Dispose(); }
    }

    private class WindowProxy : DispatchProxy
    {
        internal Action<RawInputEventArgs>? Input;
        private Action? closed;
        private Size size = new(800, 600);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case "set_Input": Input = (Action<RawInputEventArgs>?)args![0]; return null;
                case "set_Closed": closed = (Action?)args![0]; return null;
                case "get_ClientSize": return size;
                case "get_RenderScaling": case "get_DesktopScaling": return 1d;
                case "get_Position": return PixelPoint.Origin;
                case "get_Handle": return new PlatformHandle(IntPtr.Zero, "TEST");
                case "Resize": size = (Size)args![0]!; return null;
                case "Dispose": closed?.Invoke(); return null;
            }
            return method is not null && method.ReturnType != typeof(void) && method.ReturnType.IsValueType
                ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
