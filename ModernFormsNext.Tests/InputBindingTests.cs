using System.Reflection;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InputBindingCollectionTests
{
    public const string Name = "Input bindings";
}

[Collection(InputBindingCollectionTests.Name)]
public sealed class InputBindingTests : IDisposable
{
    public InputBindingTests() => Application.ReleaseInputBindings();
    public void Dispose() => Application.ReleaseInputBindings();

    [Theory]
    [InlineData(Key.S, RawInputModifiers.Control)]
    [InlineData(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift)]
    [InlineData(Key.Z, RawInputModifiers.Control)]
    [InlineData(Key.F5, RawInputModifiers.None)]
    public void ExistingRawWindowPipelineExecutesMatchingKeyDown(Key key, RawInputModifiers modifiers)
    {
        using var ui = new WindowFixture();
        int calls = 0, queries = 0, controlEvents = 0;
        var gesture = key == Key.F5 ? new KeyGesture(Keys.F5) :
            new KeyGesture(key == Key.Z ? Keys.Z : Keys.S,
                KeyModifiers.Control | (modifiers.HasFlag(RawInputModifiers.Shift) ? KeyModifiers.Shift : 0));
        ui.Form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++, () => { queries++; return true; }), gesture));
        ui.Focus.KeyDown += (_, _) => controlEvents++;
        Assert.True(ui.Key(key, modifiers).Handled);
        Assert.Equal(1, queries);
        Assert.Equal(1, calls);
        Assert.Equal(0, controlEvents);
        Assert.True(ui.Key(key, modifiers, down: false).Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ControlShiftGestureIsDistinct()
    {
        using var ui = new WindowFixture();
        var calls = new List<string>();
        ui.Form.InputBindings.Add(Bind(() => calls.Add("save")));
        ui.Form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls.Add("save-as")),
            new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Shift)));
        ui.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        ui.Press();
        Assert.Equal(["save-as", "save"], calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NearestAvailableScopeWins(int winningScope)
    {
        using var ui = new WindowFixture();
        int winner = -1;
        InputBindingCollection[] scopes = [ui.Focus.InputBindings, ui.Parent.InputBindings, ui.Form.InputBindings, Application.InputBindings];
        for (int i = winningScope; i < scopes.Length; i++)
        {
            int value = i;
            scopes[i].Add(Bind(() => winner = value));
        }
        Assert.True(ui.Press().Handled);
        Assert.Equal(winningScope, winner);
    }

    [Fact]
    public void NearestAncestorPrecedesMoreDistantAncestor()
    {
        using var ui = new WindowFixture();
        var outer = ui.Form.Controls.Add(new Panel());
        ui.Form.Controls.Remove(ui.Parent);
        outer.Controls.Add(ui.Parent);
        var calls = new List<string>();
        outer.InputBindings.Add(Bind(() => calls.Add("outer")));
        ui.Parent.InputBindings.Add(Bind(() => calls.Add("inner")));
        ui.Press();
        Assert.Equal(["inner"], calls);
    }

    [Fact]
    public void FirstAddedAvailableDuplicateWinsAndRemovalRevealsNext()
    {
        using var ui = new WindowFixture();
        var calls = new List<string>();
        var first = Bind(() => calls.Add("first"));
        ui.Focus.InputBindings.Add(first);
        ui.Focus.InputBindings.Add(Bind(() => calls.Add("second")));
        ui.Press();
        ui.Focus.InputBindings.Remove(first);
        ui.Press();
        Assert.Equal(["first", "second"], calls);
    }

    [Fact]
    public void UnavailableDuplicatesFallThroughWithinScopeThenToWindow()
    {
        using var ui = new WindowFixture();
        var calls = new List<string>();
        bool secondAvailable = true;
        ui.Focus.InputBindings.Add(Bind(() => calls.Add("unavailable"), () => false));
        ui.Focus.InputBindings.Add(Bind(() => calls.Add("second"), () => secondAvailable));
        ui.Form.InputBindings.Add(Bind(() => calls.Add("window")));
        ui.Press();
        secondAvailable = false;
        ui.Press();
        Assert.Equal(["second", "window"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrUnavailableCommandDoesNotConsumeInput(bool missing)
    {
        using var ui = new WindowFixture();
        int controlEvents = 0;
        ui.Focus.InputBindings.Add(new KeyBinding(missing ? null : new DelegateCommand(() => Assert.Fail(), () => false), SaveGesture));
        ui.Focus.KeyDown += (_, _) => controlEvents++;
        Assert.False(ui.Press().Handled);
        Assert.Equal(1, controlEvents);
    }

    [Fact]
    public void InvalidDefaultGestureDoesNotConsumeInput()
    {
        using var ui = new WindowFixture();
        ui.Focus.InputBindings.Add(new KeyBinding(new DelegateCommand(() => Assert.Fail()), default));
        Assert.False(ui.Press().Handled);
    }

    [Fact]
    public void WindowPreviewRetainsFirstRefusal()
    {
        using var ui = new WindowFixture();
        ui.Form.InputBindings.Add(Bind(() => Assert.Fail()));
        ui.Focus.KeyDown += (_, _) => Assert.Fail();
        ui.Form.KeyDown += (_, e) => e.Handled = true;
        Assert.True(ui.Press().Handled);
    }

    [Fact]
    public void ExistingControlHandledStateReachesTheRawBackend()
    {
        using var ui = new WindowFixture();
        ui.Focus.KeyDown += (_, e) => e.SuppressKeyPress = true;
        Assert.True(ui.Press().Handled);
    }

    [Fact]
    public void ParametersAndReplacementUseTheCurrentBinding()
    {
        using var ui = new WindowFixture();
        var values = new List<object?>();
        var binding = new KeyBinding(new DelegateCommand(values.Add, value => value is string), SaveGesture);
        ui.Focus.InputBindings.Add(binding);
        Assert.False(ui.Press().Handled);
        binding.CommandParameter = "first";
        ui.Press();
        binding.Command = new DelegateCommand(p => values.Add($"replacement:{p}"));
        binding.CommandParameter = "second";
        ui.Press();
        binding.Command = null;
        Assert.False(ui.Press().Handled);
        Assert.Equal(new object?[] { "first", "replacement:second" }, values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutionMayRemoveItselfOrAddAnotherBindingWithoutDoubleExecution(bool remove)
    {
        using var ui = new WindowFixture();
        int calls = 0;
        KeyBinding? first = null;
        first = Bind(() =>
        {
            calls++;
            if (remove) ui.Focus.InputBindings.Remove(first!);
            else ui.Focus.InputBindings.Add(Bind(() => calls += 100));
        });
        ui.Focus.InputBindings.Add(first);
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void FocusChangeDoesNotRestartLookupAndReleaseCannotClickNewButton()
    {
        using var ui = new WindowFixture();
        var second = ui.Parent.Controls.Add(new Button { Command = new DelegateCommand(() => Assert.Fail()) });
        int calls = 0;
        ui.Focus.InputBindings.Add(new KeyBinding(new DelegateCommand(() => { calls++; second.Select(); }), new KeyGesture(Keys.Enter)));
        second.InputBindings.Add(new KeyBinding(new DelegateCommand(() => Assert.Fail()), new KeyGesture(Keys.Enter)));
        Assert.True(ui.Key(Key.Return).Handled);
        Assert.True(second.Selected);
        Assert.True(ui.Key(Key.Return, down: false).Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void RepeatsExecutePerKeyDownAndKeyUpNeverExecutesBinding()
    {
        using var ui = new WindowFixture();
        int calls = 0;
        ui.Form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++), new KeyGesture(Keys.F5)));
        Assert.False(ui.Key(Key.F5, down: false).Handled);
        for (int i = 0; i < 3; i++) Assert.True(ui.Key(Key.F5).Handled);
        Assert.True(ui.Key(Key.F5, down: false).Handled);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void ConsumedPressKeepsReleaseSuppressedIfAvailabilityChanges()
    {
        using var ui = new WindowFixture();
        bool available = true;
        int calls = 0;
        ui.Form.InputBindings.Add(Bind(() => calls++, () => available));
        Assert.True(ui.Key(Key.S, RawInputModifiers.Control).Handled);
        available = false;
        Assert.True(ui.Key(Key.S, RawInputModifiers.Control).Handled);
        Assert.True(ui.Key(Key.S, down: false).Handled);
        Assert.False(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DeactivationDropsSuppressionForMissingKeyUp()
    {
        using var ui = new WindowFixture();
        ui.Form.InputBindings.Add(Bind(() => { }));
        ui.Key(Key.S, RawInputModifiers.Control);
        ui.Proxy.Deactivated!();
        ui.Form.InputBindings.Clear();
        Assert.False(ui.Press().Handled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PredicateMutationRejectsObsoleteSnapshot(int mutation)
    {
        using var ui = new WindowFixture();
        var binding = new KeyBinding(null, SaveGesture);
        binding.Command = new DelegateCommand(() => Assert.Fail(), () =>
        {
            switch (mutation)
            {
                case 0: binding.Command = new DelegateCommand(() => Assert.Fail()); break;
                case 1: binding.CommandParameter = new object(); break;
                case 2: ui.Focus.InputBindings.Remove(binding); break;
                case 3: ui.Focus.InputBindings.Add(Bind(() => Assert.Fail())); break;
            }
            return true;
        });
        ui.Focus.InputBindings.Add(binding);
        Assert.False(ui.Press().Handled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExceptionsRemainOriginalAndLaterActivationCanRecover(bool predicate)
    {
        using var ui = new WindowFixture();
        var error = new InvalidOperationException("command failure");
        bool fail = true;
        int calls = 0;
        ui.Form.InputBindings.Add(Bind(() => { if (fail && !predicate) throw error; calls++; }, () => fail && predicate ? throw error : true));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => ui.Key(Key.S, RawInputModifiers.Control)));
        Assert.Equal(!predicate, ui.Key(Key.S, down: false).Handled);
        fail = false;
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("a", Key.A, RawInputModifiers.None)]
    [InlineData("A", Key.A, RawInputModifiers.Shift)]
    [InlineData("ą", Key.A, RawInputModifiers.Control | RawInputModifiers.Alt | RawInputModifiers.AltGraph)]
    [InlineData("é", Key.OemQuotes, RawInputModifiers.None)]
    public void NormalShiftAltGraphAndCommittedDeadKeyTextStayOnTextPath(string text, Key key, RawInputModifiers modifiers)
    {
        using var ui = new WindowFixture();
        var textBox = ui.Parent.Controls.Add(new TextBox());
        textBox.Select();
        ui.Form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => Assert.Fail()), new KeyGesture(Keys.A, KeyModifiers.Control | KeyModifiers.Alt)));
        Assert.False(ui.Key(key, modifiers).Handled);
        ui.Text(text, modifiers);
        Assert.Equal(text, textBox.Text);
    }

    [Fact]
    public void ExistingTextEditingControlShortcutIsPreservedWithoutMatchingBinding()
    {
        using var ui = new WindowFixture();
        var text = ui.Parent.Controls.Add(new TextBox { Text = "document" });
        text.Select();
        ui.Form.InputBindings.Add(Bind(() => Assert.Fail()));
        Assert.True(ui.Key(Key.A, RawInputModifiers.Control).Handled);
        Assert.Equal(text.Text.Length, Math.Abs(text.SelectionEnd - text.SelectionStart));
    }

    [Fact]
    public void WindowScopeWorksWithoutSelectedControl()
    {
        using var ui = new WindowFixture();
        ui.Form.adapter.SelectedControl = null;
        int calls = 0;
        ui.Form.InputBindings.Add(Bind(() => calls++));
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DisposedControlReleasesBindingsAndCannotExecuteThem()
    {
        using var ui = new WindowFixture();
        var collection = ui.Focus.InputBindings;
        var binding = Bind(() => Assert.Fail());
        collection.Add(binding);
        ui.Focus.Dispose();
        Assert.Empty(collection);
        Assert.Throws<ObjectDisposedException>(() => collection.Add(binding));
        Assert.False(ui.Press().Handled);
        ui.Parent.InputBindings.Add(binding); // The disposed source no longer owns the registration.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualWindowCloseOrDisposeReleasesDescendantAndWindowRegistrations(bool dispose)
    {
        using var ui = new WindowFixture();
        var local = ui.Focus.InputBindings;
        var window = ui.Form.InputBindings;
        local.Add(Bind(() => Assert.Fail()));
        window.Add(Bind(() => Assert.Fail()));
        if (dispose) ui.Form.Dispose(); else ui.Form.Close();
        Assert.Empty(local);
        Assert.Empty(window);
        Assert.Throws<ObjectDisposedException>(() => ui.Form.InputBindings);
        Assert.False(ui.Press().Handled);
    }

    [Fact]
    public void CancelledClosePreservesBindings()
    {
        using var ui = new WindowFixture();
        int calls = 0;
        ui.Form.Closing += (_, e) => e.Cancel = true;
        ui.Form.InputBindings.Add(Bind(() => calls++));
        ui.Form.Close();
        Assert.Single(ui.Form.InputBindings);
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ApplicationCleanupReleasesRegistrationsAndDiagnosticSubscribers()
    {
        var global = Application.InputBindings;
        var binding = Bind(() => { });
        global.Add(binding);
        global.Diagnostic += (_, _) => Assert.Fail();
        Application.ReleaseInputBindings();
        Assert.Empty(global);
        Assert.Throws<ObjectDisposedException>(() => global.Add(binding));
        using var control = new Control();
        control.InputBindings.Add(binding);
    }

    [Fact]
    public void OneRegistrationHasOneOwnerAndCanMoveAfterRemoval()
    {
        using var first = new Control();
        using var second = new Control();
        var binding = Bind(() => { });
        first.InputBindings.Add(binding);
        Assert.Throws<ArgumentException>(() => first.InputBindings.Add(binding));
        Assert.Throws<ArgumentException>(() => second.InputBindings.Add(binding));
        first.InputBindings.Remove(binding);
        second.InputBindings.Add(binding);
        Assert.Single(second.InputBindings);
        second.InputBindings.Clear();
        first.InputBindings.Add(binding);
    }

    [Fact]
    public void BackgroundRegistrationAndMutationAreRejected()
    {
        using var control = new Control();
        var collection = control.InputBindings;
        var binding = Bind(() => { });
        collection.Add(binding);
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                Assert.Throws<InvalidOperationException>(() => collection.Clear());
                Assert.Throws<InvalidOperationException>(() => binding.CommandParameter = "worker");
            }
            catch (Exception error) { failure = error; }
        });
        worker.Start(); worker.Join();
        Assert.Null(failure);
        Assert.Single(collection);
        Assert.Null(binding.CommandParameter);
    }

    [Fact]
    public void DiagnosticsDescribeInvalidDuplicatesUnavailableAndExecution()
    {
        using var ui = new WindowFixture();
        var observations = new List<InputBindingDiagnosticKind>();
        ui.Form.InputBindings.Diagnostic += (_, e) => observations.Add(e.Kind);
        ui.Form.InputBindings.Add(new KeyBinding(null, default));
        ui.Form.InputBindings.Add(Bind(() => Assert.Fail(), () => false));
        ui.Form.InputBindings.Add(Bind(() => { }));
        ui.Press();
        Assert.Equal([InputBindingDiagnosticKind.InvalidBinding, InputBindingDiagnosticKind.DuplicateGesture,
            InputBindingDiagnosticKind.CommandUnavailable, InputBindingDiagnosticKind.Executed], observations);
    }

    [Fact]
    public void ButtonSemanticInvokeAndKeyboardShareTheSameDomainAction()
    {
        using var ui = new WindowFixture();
        var values = new List<object?>();
        var command = new DelegateCommand(values.Add);
        ui.Focus.Command = command;
        ui.Focus.CommandParameter = "document";
        ui.Focus.InputBindings.Add(new KeyBinding(command, SaveGesture) { CommandParameter = "document" });
        int clicks = 0;
        ui.Focus.Click += (_, _) => clicks++;
        ui.Focus.PerformClick();
        Assert.True(ui.Focus.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        ui.Press();
        Assert.Equal(new object?[] { "document", "document", "document" }, values);
        Assert.Equal(2, clicks); // Keyboard invokes the action without synthesizing Button.Click.
    }

    [Fact]
    public void SurfaceHardwareBindingsDoNotInterceptImeEditingKeysOrTextCommits()
    {
        using var root = new Panel();
        var text = root.Controls.Add(new TextBox { Text = "ab", MultiLine = true });
        using var surface = new SkiaControlSurface(root);
        text.Select();
        surface.SetTextSelection(2, 2);
        int shortcuts = 0;
        root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => shortcuts++), new KeyGesture(Keys.Left)));
        surface.ProcessKeyDown(Keys.Left, isTextInput: true);
        surface.ProcessKeyUp(Keys.Left, isTextInput: true);
        Assert.Equal(1, surface.GetTextInputState()!.Value.SelectionStart);
        Assert.Equal(0, shortcuts);
        surface.ProcessKeyDown(Keys.Left);
        surface.ProcessKeyUp(Keys.Left);
        Assert.Equal(1, shortcuts);
        Assert.Equal(1, surface.GetTextInputState()!.Value.SelectionStart);
        surface.CommitText("ą");
        Assert.Equal("aąb", text.Text);
    }

    [Fact]
    public void StandaloneSurfaceUsesApplicationFallbackWithNoFocus()
    {
        using var root = new Panel();
        using var surface = new SkiaControlSurface(root);
        int calls = 0;
        Application.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++), new KeyGesture(Keys.F5)));
        surface.ProcessKeyDown(Keys.F5);
        surface.ProcessKeyUp(Keys.F5);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InactiveControlScopeAllowsWindowFallback(bool hidden)
    {
        using var ui = new WindowFixture();
        ui.Focus.InputBindings.Add(Bind(() => Assert.Fail()));
        int calls = 0;
        ui.Form.InputBindings.Add(Bind(() => calls++));
        if (hidden) ui.Focus.Visible = false; else ui.Focus.Enabled = false;
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void IndexReplacementReleasesOldBindingAndFailedReplacementPreservesOwnership()
    {
        using var ui = new WindowFixture();
        var old = Bind(() => Assert.Fail());
        var other = Bind(() => Assert.Fail());
        int calls = 0;
        var replacement = Bind(() => calls++);
        ui.Focus.InputBindings.Add(old);
        ui.Parent.InputBindings.Add(other);
        Assert.Throws<ArgumentException>(() => ui.Focus.InputBindings[0] = other);
        Assert.Same(old, ui.Focus.InputBindings[0]);
        ui.Focus.InputBindings[0] = replacement;
        ui.Form.InputBindings.Add(old);
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void GestureReplacementAndClearTakeEffectOnTheNextPress()
    {
        using var ui = new WindowFixture();
        int calls = 0;
        var binding = Bind(() => calls++);
        ui.Form.InputBindings.Add(binding);
        binding.Gesture = new KeyGesture(Keys.F5);
        Assert.False(ui.Press().Handled);
        Assert.True(ui.Press(Key.F5, RawInputModifiers.None).Handled);
        ui.Form.InputBindings.Clear();
        Assert.False(ui.Press(Key.F5, RawInputModifiers.None).Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ArbitraryICommandGetsOneFreshQueryAndNoEventSubscription()
    {
        using var ui = new WindowFixture();
        var command = new DirectCommand();
        var parameter = new object();
        var binding = new KeyBinding(command, SaveGesture) { CommandParameter = parameter };
        ui.Form.InputBindings.Add(binding);
        Assert.True(ui.Press().Handled);
        Assert.Equal(1, command.Queries);
        Assert.Equal(1, command.Executions);
        Assert.Same(parameter, command.Parameter);
        binding.Command = null;
        binding.CommandParameter = null;
        Assert.False(ui.Press().Handled);
        Assert.Equal(1, command.Executions);
    }

    private sealed class DirectCommand : System.Windows.Input.ICommand
    {
        internal int Queries, Executions;
        internal object? Parameter;
        public event EventHandler? CanExecuteChanged { add => Assert.Fail(); remove => Assert.Fail(); }
        public bool CanExecute(object? parameter) { Queries++; return true; }
        public void Execute(object? parameter) { Executions++; Parameter = parameter; }
    }

    private static KeyGesture SaveGesture => new(Keys.S, KeyModifiers.Control);
    private static KeyBinding Bind(Action execute, Func<bool>? canExecute = null) => new(new DelegateCommand(execute, canExecute), SaveGesture);

    // Reuse the existing IWindowImpl input callback seam, as other window tests do with proxies.
    // This is not a TestHost keyboard simulator and does not claim native OS input evidence.
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

        internal RawKeyEventArgs Key(Key key, RawInputModifiers modifiers = RawInputModifiers.None, bool down = true)
        {
            var e = new RawKeyEventArgs(keyboard, 0, Form.adapter, down ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp, key, modifiers);
            Proxy.Input!(e);
            return e;
        }

        internal RawKeyEventArgs Press(Key key = WindowKit.Input.Key.S, RawInputModifiers modifiers = RawInputModifiers.Control)
        {
            var e = Key(key, modifiers);
            Key(key, modifiers, down: false);
            return e;
        }

        internal void Text(string text, RawInputModifiers modifiers)
            => Proxy.Input!(new RawTextInputEventArgs(keyboard, 0, Form.adapter, text, modifiers));

        public void Dispose() { Form.Dispose(); Form.adapter.Dispose(); }
    }

    private class WindowProxy : DispatchProxy
    {
        internal Action<RawInputEventArgs>? Input;
        internal Action? Closed;
        internal Action? Deactivated;
        private Size clientSize = new(800, 600);

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case "set_Input": Input = (Action<RawInputEventArgs>?)args![0]; return null;
                case "set_Closed": Closed = (Action?)args![0]; return null;
                case "set_Deactivated": Deactivated = (Action?)args![0]; return null;
                case "get_ClientSize": return clientSize;
                case "get_RenderScaling": case "get_DesktopScaling": return 1d;
                case "get_Position": return PixelPoint.Origin;
                case "get_Handle": return new PlatformHandle(IntPtr.Zero, "TEST");
                case "Resize": clientSize = (Size)args![0]!; return null;
                case "Dispose": Closed?.Invoke(); return null;
            }
            return method is not null && method.ReturnType != typeof(void) && method.ReturnType.IsValueType
                ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
