using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed partial class CommandRoutingTests
{
    [Fact]
    public void ButtonDefaultsToItsOwnSourceRegardlessOfFocusInEitherWindow()
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<Control>();
        ui.Form.CommandBindings.Add(Bind(command, (_, e) => {
            Assert.Same(ui.Focus, e.Source);
            calls.Add(e.Target);
        }));
        ui.Focus.Command = command;
        ui.Parent.Controls.Add(new Button()).Select();
        other.Focus.Select();
        ui.Focus.PerformClick();
        Assert.Equal([ui.Focus], calls);
    }

    [Fact]
    public void ExplicitButtonTargetUsesItsRouteAndPreservesSourceAndParameter()
    {
        using var ui = new WindowFixture();
        var target = ui.Form.Controls.Add(new Button());
        var command = new RoutedCommand();
        var parameter = new object();
        int calls = 0;
        target.CommandBindings.Add(Bind(command, (_, e) => {
            Assert.Same(target, e.Target);
            Assert.Same(ui.Focus, e.Source);
            Assert.Same(parameter, e.Parameter);
            calls++;
        }));
        ui.Focus.CommandTarget = target;
        ui.Focus.CommandParameter = parameter;
        ui.Focus.Command = command;
        Assert.True(ui.Focus.Enabled);
        ui.Focus.PerformClick();
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitWrongWindowTargetIsUnavailableForButtonAndKeyBinding(bool keyboard)
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        var command = new RoutedCommand();
        other.Form.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        ui.Form.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        if (keyboard)
        {
            ui.Form.InputBindings.Add(new KeyBinding(command, SaveGesture) { CommandTarget = other.Focus });
            Assert.False(ui.Key().Handled);
        }
        else
        {
            ui.Focus.CommandTarget = other.Focus;
            ui.Focus.Command = command;
            Assert.False(ui.Focus.Enabled);
            ui.Focus.PerformClick();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitDetachedOrDisposedTargetDoesNotFallBackToButton(bool disposed)
    {
        using var ui = new WindowFixture();
        using var target = new Button();
        if (disposed) target.Dispose();
        var command = new RoutedCommand();
        ui.Form.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        ui.Focus.CommandTarget = target;
        ui.Focus.Command = command;
        Assert.False(ui.Focus.Enabled);
        ui.Focus.PerformClick();
    }

    [Fact]
    public void KeyboardPrefersFocusOverBindingScope()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        int calls = 0;
        ui.Focus.CommandBindings.Add(Bind(command, (_, e) => {
            Assert.Same(ui.Focus, e.Target);
            Assert.Same(ui.Parent, e.Source);
            calls++;
        }));
        ui.Parent.InputBindings.Add(new KeyBinding(command, SaveGesture));
        Assert.True(ui.Key().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void KeyboardExplicitTargetOverridesFocus()
    {
        using var ui = new WindowFixture();
        var target = ui.Form.Controls.Add(new Button());
        var command = new RoutedCommand();
        int calls = 0;
        target.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(target, e.Target); calls++; }));
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        ui.Form.InputBindings.Add(new KeyBinding(command, SaveGesture) { CommandTarget = target });
        Assert.True(ui.Key().Handled);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowKeyboardFallsBackToItsRootWhenFocusIsMissingOrInAnotherWindow(bool wrongWindow)
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        ui.Form.adapter.SelectedControl = wrongWindow ? other.Focus : null;
        var command = new RoutedCommand();
        int calls = 0;
        ui.Form.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(ui.Form.adapter, e.Target); calls++; }));
        other.Focus.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        ui.Form.InputBindings.Add(new KeyBinding(command, SaveGesture));
        Assert.True(ui.Key().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SurfaceRootBindingSuppliesSourceTargetWithoutSelectedControl()
    {
        using var root = new Panel();
        using var surface = new SkiaControlSurface(root);
        var command = new RoutedCommand();
        int calls = 0;
        root.CommandBindings.Add(Bind(command, (_, e) => {
            Assert.Same(root, e.Target); Assert.Same(root, e.Source); calls++;
        }));
        root.InputBindings.Add(new KeyBinding(command, SaveGesture));
        surface.ProcessKeyDown(Keys.Control | Keys.S);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UnavailableRoutedBindingAllowsOrdinaryInputBindingFallback()
    {
        using var ui = new WindowFixture();
        int calls = 0;
        ui.Focus.InputBindings.Add(new KeyBinding(new RoutedCommand(), SaveGesture));
        ui.Form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++), SaveGesture));
        Assert.True(ui.Key().Handled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void RoutedKeyboardRepeatsExecuteAndReleaseIsConsumedAfterFocusChange()
    {
        using var ui = new WindowFixture();
        var next = ui.Parent.Controls.Add(new Button());
        var command = new RoutedCommand();
        int calls = 0;
        ui.Form.CommandBindings.Add(Bind(command, (_, _) => { calls++; next.Select(); }));
        next.KeyUp += (_, _) => Assert.Fail();
        ui.Form.InputBindings.Add(new KeyBinding(command, SaveGesture));
        Assert.True(ui.Key().Handled);
        Assert.True(ui.Key().Handled);
        Assert.True(ui.Key(down: false).Handled);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AccessibilityInvokeUsesButtonClickOrderingAndAvailabilityRecovery()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        bool allowed = true;
        var calls = new List<string>();
        ui.Form.CommandBindings.Add(new(command, (_, e) => { calls.Add("execute"); e.Handled = true; },
            (_, e) => { calls.Add("query"); e.CanExecute = allowed; }));
        ui.Focus.Command = command;
        ui.Focus.Click += (_, _) => calls.Add("click");
        calls.Clear();
        Assert.True(ui.Focus.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(["query", "click", "query", "execute"], calls);
        allowed = false;
        command.RaiseCanExecuteChanged();
        Assert.False(ui.Focus.Enabled);
        Assert.False(ui.Focus.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.True(ui.Focus.AccessibilityObject.State.HasFlag(AccessibleStates.Unavailable));
        allowed = true;
        command.RaiseCanExecuteChanged();
        Assert.True(ui.Focus.Enabled);
        Assert.True(ui.Focus.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
    }

    [Fact]
    public void CollectionEditsRequeryButtonWithoutASeparateCommandManager()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        ui.Focus.Command = command;
        Assert.False(ui.Focus.Enabled);
        var binding = Bind(command, (_, _) => { });
        ui.Form.CommandBindings.Add(binding);
        Assert.True(ui.Focus.Enabled);
        ui.Form.CommandBindings.Remove(binding);
        Assert.False(ui.Focus.Enabled);
        ui.Form.CommandBindings.Add(binding);
        Assert.True(ui.Focus.Enabled);
        ui.Form.CommandBindings.Clear();
        Assert.False(ui.Focus.Enabled);
    }

    [Fact]
    public void AttachingAndReparentingAncestorRefreshesRoutedSourcesInItsSubtree()
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        var command = new RoutedCommand();
        ui.Form.CommandBindings.Add(Bind(command, (_, _) => { }));
        using var panel = new Panel();
        var button = panel.Controls.Add(new Button { Command = command });
        Assert.False(button.Enabled);
        ui.Form.Controls.Add(panel);
        Assert.True(button.Enabled);
        other.Form.Controls.Add(panel);
        Assert.False(button.Enabled);
        ui.Form.Controls.Add(panel);
        Assert.True(button.Enabled);
    }

    [Fact]
    public void ClickCanReplaceRoutedTargetBeforeFreshGuardedExecution()
    {
        using var ui = new WindowFixture();
        var next = ui.Form.Controls.Add(new Button());
        var command = new RoutedCommand();
        int calls = 0;
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        next.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(next, e.Target); calls++; }));
        ui.Focus.Command = command;
        ui.Focus.Click += (_, _) => ui.Focus.CommandTarget = next;
        ui.Focus.PerformClick();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OrdinaryDelegateIgnoresCommandTargetWithoutExtraPredicateCalls()
    {
        using var ui = new WindowFixture();
        using var other = new WindowFixture();
        int queries = 0, executions = 0;
        var command = new DelegateCommand(() => executions++, () => { queries++; return true; });
        ui.Focus.Command = command;
        Assert.Equal(1, queries);
        ui.Focus.CommandTarget = other.Focus;
        Assert.Equal(1, queries);
        ui.Focus.PerformClick();
        Assert.Equal(3, queries);
        Assert.Equal(1, executions);
        ui.Form.InputBindings.Add(new KeyBinding(command, SaveGesture) { CommandTarget = other.Focus });
        ui.Key();
        Assert.Equal(4, queries);
        Assert.Equal(2, executions);
    }

    private static KeyGesture SaveGesture => new(Keys.S, KeyModifiers.Control);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingIgnoredTargetInsideOrdinaryPredicateDoesNotInvalidateTheCommand(bool keyboard)
    {
        using var ui = new WindowFixture();
        using var target = new Button();
        KeyBinding? binding = null;
        int calls = 0;
        var command = new DelegateCommand(() => calls++, () => {
            if (keyboard) binding!.CommandTarget = target;
            else ui.Focus.CommandTarget = target;
            return true;
        });
        if (keyboard)
        {
            binding = new KeyBinding(command, SaveGesture);
            ui.Form.InputBindings.Add(binding);
            Assert.True(ui.Key().Handled);
        }
        else
        {
            ui.Focus.Command = command;
            ui.Focus.CommandTarget = null;
            ui.Focus.PerformClick();
        }
        Assert.Equal(1, calls);
    }
}
