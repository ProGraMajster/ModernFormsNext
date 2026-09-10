using System.Drawing;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TestInputTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void ClickUsesNestedPresentationCoordinatesAndNormalCommand(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 300, scale));
        var root = new Panel();
        var nested = root.Controls.Add(new Panel { Bounds = new Rectangle(37, 29, 220, 160) });
        int calls = 0;
        var button = nested.Controls.Add(new Button
        {
            Bounds = new Rectangle(21, 19, 100, 40),
            Command = new DelegateCommand(() => calls++)
        });
        host.Show(root);

        host.Input.Click(button);

        Assert.Equal(1, calls);
        Assert.Same(button, host.FocusedControl);
        Assert.False(button.Capture);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void ObscuredTargetUsesHitTestingInsteadOfInvokingRequestedControl()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var lower = root.Controls.Add(new Button { Bounds = new Rectangle(10, 10, 120, 40) });
        var upper = root.Controls.Add(new Button { Bounds = lower.Bounds });
        int lowerCalls = 0, upperCalls = 0;
        lower.Click += (_, _) => lowerCalls++;
        upper.Click += (_, _) => upperCalls++;
        host.Show(root);
        upper.BringToFront();

        host.Input.Click(lower);

        Assert.Equal(0, lowerCalls);
        Assert.Equal(1, upperCalls);
        Assert.Same(upper, host.FocusedControl);
    }

    [Fact]
    public void DisabledAndHiddenControlsDoNotReceiveDirectActivation()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button { Bounds = new Rectangle(10, 10, 100, 40), Enabled = false });
        int calls = 0;
        button.Click += (_, _) => calls++;
        host.Show(root);

        host.Input.Click(button);
        Assert.False(host.Input.Focus(button));
        button.Enabled = true;
        button.Visible = false;
        host.Input.Click(button);
        Assert.False(host.Input.Focus(button));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void TabUsesCanonicalOrderSkipsHiddenDisabledAndSupportsReverse()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { TabStop = false };
        var first = root.Controls.Add(new Button { Name = "first", TabIndex = 0 });
        root.Controls.Add(new Button { TabIndex = 1, Enabled = false });
        root.Controls.Add(new Button { TabIndex = 2, Visible = false });
        var last = root.Controls.Add(new Button { Name = "last", TabIndex = 3 });
        host.Show(root);
        Assert.True(host.Input.Focus(first));

        host.Input.Tab();
        Assert.Same(last, host.FocusedControl);
        host.Input.Tab(backwards: true);
        Assert.Same(first, host.FocusedControl);
    }

    [Fact]
    public void WindowPreviewCanSuppressTabAndControlSeesNormalKeyEvents()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var first = form.Controls.Add(new Button());
        form.Controls.Add(new Button());
        host.Show(form);
        host.Input.Focus(first);
        int events = 0;
        first.KeyDown += (_, _) => events++;
        form.KeyDown += (_, e) => { if (e.KeyCode == Keys.Tab) e.Handled = true; };

        host.Input.Tab();
        host.Input.PressKey(Keys.F2);

        Assert.Same(first, host.FocusedControl);
        Assert.Equal(1, events);
    }

    [Fact]
    public void ShortcutAndCommittedTextRemainSeparateThroughTheSharedResolver()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var text = root.Controls.Add(new TextBox());
        int calls = 0, releases = 0;
        root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++), new KeyGesture(Keys.S, KeyModifiers.Control)));
        text.KeyUp += (_, _) => releases++;
        host.Show(root);
        host.Input.Focus(text);

        Assert.True(host.Input.PressKey(Keys.Control | Keys.S));
        Assert.Equal(1, calls);
        Assert.Equal(0, releases);
        Assert.Empty(text.Text);
        host.Input.PressKey(Keys.A);
        Assert.Empty(text.Text);
        host.Input.TextInput("Zażółć 😀 中文", Keys.Control | Keys.Alt | Keys.AltGraph);

        Assert.Equal("Zażółć 😀 中文", text.Text);
        Assert.Equal(1, calls);
        Assert.DoesNotContain(host.Input.RecentEvents, entry => entry.Contains("Zażółć", StringComparison.Ordinal));
    }

    [Fact]
    public void DoubleClicksUseControlledTimeAndKeepBoundaryExclusive()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button { Bounds = new Rectangle(10, 10, 100, 40) });
        int doubleClicks = 0;
        button.DoubleClick += (_, _) => doubleClicks++;
        host.Show(root);
        host.Input.Click(button);
        host.Clock.Advance(TimeSpan.FromMilliseconds(500));
        host.Input.Click(button);
        Assert.Equal(0, doubleClicks);
        host.Clock.Advance(TimeSpan.FromMilliseconds(499));
        host.Input.Click(button);
        Assert.Equal(1, doubleClicks);
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        host.Input.DoubleClick(button);
        Assert.Equal(2, doubleClicks);
    }

    [Fact]
    public void CaptureLossCancelsPressedStateAndLaterClicksStillWork()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button { Bounds = new Rectangle(10, 10, 100, 40) });
        host.Show(root);
        host.Input.PointerDown(new Point(20, 20));
        Assert.True(button.Capture);
        host.Input.Move(new Point(5000, 5000));
        host.Input.LoseCapture();
        Assert.False(button.Capture);
        int calls = 0;
        button.Click += (_, _) => calls++;
        host.Input.Click(button);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void WheelBubblesFromChildToProductionScrollableParent()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { AutoScroll = true };
        root.Controls.Add(new Button { Bounds = new Rectangle(10, 10, 100, 40) });
        root.Controls.Add(new Button { Bounds = new Rectangle(10, 600, 100, 40) });
        host.Show(root, 250, 200);
        int before = root.VerticalScrollProperties.Value;

        host.Input.Wheel(new Point(20, 20), new Point(0, -1));

        Assert.True(root.VerticalScrollProperties.Value > before);
    }

    [Fact]
    public void WindowsKeepIndependentInputAndFocusOwners()
    {
        using var host = ModernFormsTestHost.Create();
        var first = new TextBox();
        var second = new TextBox();
        TestWindowHost a = host.Show(first);
        TestWindowHost b = host.Show(second);
        a.Input.Focus(first);
        b.Input.Focus(second);
        a.Input.TextInput("one");
        b.Input.TextInput("two");
        Assert.Equal("one", first.Text);
        Assert.Equal("two", second.Text);
        Assert.Same(first, a.FocusedControl);
        Assert.Same(second, b.FocusedControl);
        a.Close();
        b.Input.TextInput("!");
        Assert.Equal("two!", second.Text);
    }

    [Fact]
    public void InvalidInputAndDetachedControlsFailBeforeDispatch()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var child = root.Controls.Add(new Button());
        host.Show(root);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Input.KeyDown((Keys)0xffff));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Input.Move(Point.Empty, Keys.A));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Input.Click(Point.Empty, MouseButtons.XButton1));
        Assert.Throws<ArgumentNullException>(() => host.Input.TextInput(null!));
        host.Input.TextInput(string.Empty);
        root.Controls.Remove(child);
        Assert.Throws<ArgumentException>(() => host.Input.Focus(child));
        root.Controls.Add(child);
        Assert.True(host.Input.Focus(child));
        child.Dispose();
        Assert.Throws<ArgumentException>(() => host.Input.Click(child));
    }

    [Fact]
    public void InputEnforcesOwnerThreadAndCannotOutliveItsWindow()
    {
        using var host = ModernFormsTestHost.Create();
        var window = host.Show(new Panel());
        TestInput input = window.Input;
        Exception? failure = null;
        var thread = new Thread(() => { try { input.KeyDown(Keys.A); } catch (Exception exception) { failure = exception; } });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(failure);
        window.Close();
        Assert.Throws<ObjectDisposedException>(() => input.TextInput("late"));
    }

    [Fact]
    public void ClosingWindowFromPointerDownDoesNotDispatchSyntheticReleaseAfterDisposal()
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        TestWindowHost window = host.Show(button);
        button.MouseDown += (_, _) => window.Close();
        window.Input.Click(button);
        Assert.True(window.IsClosed);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public void InputHistoryIsBoundedDetachedAndDoesNotStoreTypedText()
    {
        using var host = ModernFormsTestHost.Create();
        var text = new TextBox();
        host.Show(text);
        host.Input.Focus(text);
        for (int i = 0; i < 80; i++) host.Input.TextInput("secret");
        IReadOnlyList<string> snapshot = host.Input.RecentEvents;
        Assert.Equal(64, snapshot.Count);
        Assert.All(snapshot, entry => Assert.Equal("TextInput", entry));
        host.Input.PressKey(Keys.F1);
        Assert.All(snapshot, entry => Assert.Equal("TextInput", entry));
    }

    [Fact]
    public void FailedShortcutReleasesConsumedKeyBeforeNextUnboundPress()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var button = root.Controls.Add(new Button());
        var expected = new InvalidOperationException("command failure");
        var binding = new KeyBinding(new DelegateCommand(() => throw expected), new KeyGesture(Keys.S, KeyModifiers.Control));
        root.InputBindings.Add(binding);
        host.Show(root);
        host.Input.Focus(button);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => host.Input.PressKey(Keys.Control | Keys.S)));
        root.InputBindings.Remove(binding);
        int deliveries = 0;
        button.KeyDown += (_, _) => deliveries++;
        Assert.False(host.Input.PressKey(Keys.S));
        Assert.Equal(1, deliveries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedConvenienceClickCancelsCaptureBeforeAnotherGesture(bool throwOnClick)
    {
        using var host = ModernFormsTestHost.Create();
        var button = new Button();
        host.Show(button);
        var expected = new InvalidOperationException("pointer callback failure");
        bool fail = true;
        if (throwOnClick) button.Click += (_, _) => { if (fail) throw expected; };
        else button.MouseDown += (_, _) => { if (fail) throw expected; };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => host.Input.Click(button)));
        Assert.False(button.Capture);
        fail = false;
        int calls = 0;
        button.Click += (_, _) => calls++;
        host.Input.Click(button);
        Assert.Equal(1, calls);
        Assert.False(button.Capture);
    }
}
