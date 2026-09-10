using System.Drawing;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class InputReentrancyTests
{
    [Fact]
    public void FocusCallbackCanCloseWindowWithoutResumingItsObsoleteSelection()
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        TestWindowHost window = host.Show(button);
        button.GotFocus += (_, _) => window.Close();

        Assert.False(window.Input.Focus(button));

        Assert.True(window.IsClosed);
        Assert.False(button.Selected);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public void ClickDoesNotDeliverMouseDownAfterGotFocusClosesWindow()
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        TestWindowHost window = host.Show(button);
        int mouseDowns = 0;
        button.GotFocus += (_, _) => window.Close();
        button.MouseDown += (_, _) => mouseDowns++;

        window.Input.Click(button);

        Assert.True(window.IsClosed);
        Assert.Equal(0, mouseDowns);
        Assert.False(button.Selected);
    }

    [Fact]
    public void FocusCallbackCanDetachAndLaterReattachTheControl()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button());
        TestWindowHost window = host.Show(root);
        EventHandler detach = (_, _) => root.Controls.Remove(button);
        button.GotFocus += detach;

        Assert.False(window.Input.Focus(button));
        Assert.False(button.Selected);
        Assert.Null(button.Parent);
        button.GotFocus -= detach;
        root.Controls.Add(button);

        Assert.True(window.Input.Focus(button));
        Assert.Same(button, window.FocusedControl);
    }

    [Fact]
    public void FocusCallbackCanDisposeControlWithoutSelectingItInTheLiveWindow()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button());
        TestWindowHost window = host.Show(root);
        button.GotFocus += (_, _) => button.Dispose();

        Assert.False(window.Input.Focus(button));

        Assert.False(button.Selected);
        Assert.NotSame(button, window.FocusedControl);
    }

    [Fact]
    public void ThrowingGotFocusLeavesSelectionRetryableAndPreservesTheFailure()
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        TestWindowHost window = host.Show(button);
        var expected = new InvalidOperationException("Focus failed.");
        EventHandler fail = (_, _) => throw expected;
        button.GotFocus += fail;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => window.Input.Focus(button)));
        Assert.False(button.Selected);
        button.GotFocus -= fail;

        Assert.True(window.Input.Focus(button));
        Assert.Same(button, window.FocusedControl);
    }

    [Fact]
    public void LostFocusCallbackCanCloseWindowDuringSelectionOfAnotherControl()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var first = root.Controls.Add(new Button());
        var second = root.Controls.Add(new Button());
        TestWindowHost window = host.Show(root);
        Assert.True(window.Input.Focus(first));
        first.LostFocus += (_, _) => window.Close();

        Assert.False(window.Input.Focus(second));

        Assert.True(window.IsClosed);
        Assert.False(second.Selected);
    }

    [Theory]
    [InlineData(Keys.Space)]
    [InlineData(Keys.Enter)]
    public void ActivationKeyDownDoesNotRestartPressedStateAfterCallbackClosesWindow(Keys key)
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        TestWindowHost window = host.Show(button);
        Assert.True(window.Input.Focus(button));
        button.KeyDown += (_, _) => window.Close();

        window.Input.KeyDown(key);

        Assert.True(window.IsClosed);
        Assert.NotEqual(VisualState.Pressed, button.VisualState);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("detach")]
    [InlineData("dispose")]
    public void LostFocusCallbackCannotLeaveAnUnavailableCanonicalFocusOwner(string change)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var first = root.Controls.Add(new Button());
        var second = root.Controls.Add(new Button());
        TestWindowHost window = host.Show(root);
        Assert.True(window.Input.Focus(first));
        first.LostFocus += (_, _) =>
        {
            if (change == "disable") second.Enabled = false;
            else if (change == "hide") second.Visible = false;
            else if (change == "detach") root.Controls.Remove(second);
            else second.Dispose();
        };

        Assert.False(window.Input.Focus(second));

        Assert.False(second.Selected);
        Assert.NotSame(second, window.FocusedControl);
    }

    [Fact]
    public void ActivationKeyUpDoesNotInvalidateWindowClosedByItsHandler()
    {
        using var host = ModernFormsTestHost.Create();
        var control = new ActivationControl();
        TestWindowHost window = host.Show(control);
        Assert.True(window.Input.Focus(control));
        window.Input.KeyDown(Keys.Space);
        Assert.Equal(VisualState.Pressed, control.VisualState);
        control.KeyUp += (_, _) => window.Close();

        window.Input.KeyUp(Keys.Space);

        Assert.True(window.IsClosed);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public void MouseUpCallbackCanCloseWindowWithoutInvalidatingItsDisposedBackend()
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        TestWindowHost window = host.Show(button, 120, 40);
        window.Input.PointerDown(new Point(20, 20));
        Assert.Equal(VisualState.Pressed, button.VisualState);
        button.MouseUp += (_, _) => window.Close();

        window.Input.PointerUp(new Point(20, 20));

        Assert.True(window.IsClosed);
        Assert.NotEqual(VisualState.Pressed, button.VisualState);
        Assert.Empty(host.Windows);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("detach")]
    public void KeyDownDoesNotRearmPressedStateWhenTargetBecomesUnavailable(string change)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button());
        TestWindowHost window = host.Show(root);
        Assert.True(window.Input.Focus(button));
        button.KeyDown += (_, _) =>
        {
            if (change == "disable") button.Enabled = false;
            else if (change == "hide") button.Visible = false;
            else root.Controls.Remove(button);
        };

        window.Input.KeyDown(Keys.Space);

        Assert.NotEqual(VisualState.Pressed, button.VisualState);
    }

    [Fact]
    public void KeyDownDoesNotRearmOldControlWhenHandlerMovesFocus()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var first = root.Controls.Add(new Button { Bounds = new Rectangle(0, 0, 100, 40) });
        var second = root.Controls.Add(new Button { Bounds = new Rectangle(0, 50, 100, 40) });
        TestWindowHost window = host.Show(root);
        Assert.True(window.Input.Focus(first));
        first.KeyDown += (_, _) => window.Input.Focus(second);

        window.Input.KeyDown(Keys.Space);

        Assert.Same(second, window.FocusedControl);
        Assert.NotEqual(VisualState.Pressed, first.VisualState);
    }

    [Fact]
    public void OrdinaryFocusChangePreservesExistingGotFocusThenLostFocusEventOrder()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var first = root.Controls.Add(new Button());
        var second = root.Controls.Add(new Button());
        TestWindowHost window = host.Show(root);
        Assert.True(window.Input.Focus(first));
        var events = new List<string>();
        second.GotFocus += (_, _) => events.Add("second.GotFocus");
        first.LostFocus += (_, _) => events.Add("first.LostFocus");

        Assert.True(window.Input.Focus(second));

        Assert.Equal(new[] { "second.GotFocus", "first.LostFocus" }, events);
        Assert.Same(second, window.FocusedControl);
    }

    private sealed class ActivationControl : Control
    {
        internal ActivationControl()
        {
            SetControlBehavior(ControlBehaviors.Selectable, true);
            SetControlBehavior(ControlBehaviors.Hoverable, true);
        }
    }
}
