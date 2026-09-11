using ModernFormsNext.CrossPlatform.Sample;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

// These cases deliberately start with Android key codes and use the sample's actual bridge.
// Character commits are separate semantic operations, never an A-key-to-"a" conversion.
[Collection(AndroidTextInputUiCollection.Name)]
public sealed class AndroidHardwareIntegrationTests
{
    [Theory]
    [InlineData(47, 0x1000, Keys.S, KeyModifiers.Control)]
    [InlineData(47, 0x1040, Keys.S, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(8, 0x4000, Keys.D1, KeyModifiers.Control)]
    [InlineData(131, 0, Keys.F1, KeyModifiers.None)]
    [InlineData(142, 0x40000, Keys.F12, KeyModifiers.Meta)]
    [InlineData(122, 0x10, Keys.Home, KeyModifiers.Alt)]
    [InlineData(93, 0x80, Keys.PageDown, KeyModifiers.Shift)]
    public void NativeIdentityAndModifiersExecuteTheActualSurfaceBindingExactlyOnce(
        int nativeKey, int nativeModifiers, Keys expectedKey, KeyModifiers expectedModifiers)
    {
        using var ui = new SurfaceFixture();
        var calls = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(expectedKey, expectedModifiers)));
        Assert.True(ui.Send(nativeKey, true, nativeModifiers));
        Assert.True(ui.Send(nativeKey, false, nativeModifiers));
        Assert.Equal(1, calls);
        Assert.Equal("seed", ui.Editor.Text);
    }

    [Fact]
    public void ControlShiftSaveIsDistinctAndNativeRepeatsExecuteOncePerDeliveredDown()
    {
        using var ui = new SurfaceFixture();
        var save = 0;
        var saveAs = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => save++),
            new KeyGesture(Keys.S, KeyModifiers.Control)));
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => saveAs++),
            new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Shift)));
        for (var repeat = 0; repeat < 3; repeat++)
            Assert.True(ui.Send(47, true, 0x1000, repeat: repeat));
        Assert.True(ui.Send(47, false, 0x1000));
        Assert.True(ui.Send(47, true, 0x1001));
        Assert.True(ui.Send(47, false, 0x1001));
        Assert.Equal((3, 1), (save, saveAs));
        Assert.Equal("seed", ui.Editor.Text);
    }

    [Fact]
    public void UnavailableBindingsFallThroughFocusedControlTwoAncestorsSurfaceAndApplication()
    {
        using var ui = new SurfaceFixture();
        bool[] available = [true, true, true, true, true];
        int[] queries = new int[5];
        var executed = new List<int>();
        KeyBinding Binding(int index) => new(new DelegateCommand(() => executed.Add(index),
            () => { queries[index]++; return available[index]; }), new KeyGesture(Keys.F1));
        ui.Editor.InputBindings.Add(Binding(0));
        ui.Inner.InputBindings.Add(Binding(1));
        ui.Outer.InputBindings.Add(Binding(2));
        ui.Root.InputBindings.Add(Binding(3));
        var applicationBinding = Binding(4);
        Application.InputBindings.Add(applicationBinding);
        try
        {
            for (var winner = 0; winner < 5; winner++)
            {
                Array.Clear(queries);
                Assert.True(ui.Send(131, true));
                Assert.True(ui.Send(131, false));
                Assert.Equal(winner, executed[^1]);
                for (var index = 0; index < 5; index++)
                    Assert.Equal(index <= winner ? 1 : 0, queries[index]);
                available[winner] = false;
            }
            Assert.False(ui.Send(131, true));
            Assert.False(ui.Send(131, false));
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, executed);
        }
        finally { Application.InputBindings.Remove(applicationBinding); }
    }

    [Fact]
    public void UnhandledLetterAllowsNativeFallbackButControlSuppressionIsPreserved()
    {
        using var ui = new SurfaceFixture();
        var down = 0;
        ui.Editor.KeyDown += (_, e) => { down++; if (e.Control) e.SuppressKeyPress = true; };
        Assert.False(ui.Send(47, true));
        Assert.False(ui.Send(47, false));
        Assert.True(ui.Send(47, true, 0x1000));
        Assert.Equal(2, down);
        Assert.Equal("seed", ui.Editor.Text);
    }

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(3, true, false)]
    [InlineData(3, false, true)]
    public void SoftwareOrVirtualInputNeverExecutesHardwareShortcut(int device, bool soft, bool connection)
    {
        using var ui = new SurfaceFixture();
        var calls = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(Keys.S, KeyModifiers.Control)));
        var input = AndroidInputKeyMapper.FromNative(47, true, 0x1000, device, soft, connection)!.Value;
        Assert.False(AndroidKeyboardInput.Process(ui.Surface, input));
        Assert.Equal(0, calls);
        Assert.Equal("seed", ui.Editor.Text);
    }

    [Fact]
    public void InputConnectionShiftSelectionRetainsModifiersWithoutResolvingBindings()
    {
        using var ui = new SurfaceFixture();
        var client = Assert.IsAssignableFrom<ITextInputClient>(ui.Surface.TextInputClient);
        Assert.True(client.SetSelection(4, 4));
        var calls = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(Keys.Left, KeyModifiers.Shift)));
        for (var index = 0; index < 2; index++)
        {
            var down = AndroidInputKeyMapper.FromNative(21, true, 0x40, 3, fromInputConnection: true)!.Value;
            AndroidKeyboardInput.Process(ui.Surface, down);
            AndroidKeyboardInput.Process(ui.Surface, down with { IsDown = false });
        }
        var state = client.GetState()!;
        Assert.Equal((4, 2), (state.SelectionStart, state.SelectionEnd));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InternationalInputClearsSameKeySuppressionAndCommitsTextOnce(bool deadKey)
    {
        using var ui = new SurfaceFixture();
        var calls = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(Keys.A, KeyModifiers.Control | KeyModifiers.Alt)));
        Assert.True(ui.Send(29, true, 0x1002));
        var nativeMeta = deadKey ? 0x1002 : 0x1020;
        var international = AndroidInputKeyMapper.FromNative(29, true, nativeMeta, 3,
            isDeadKey: deadKey)!.Value;
        Assert.False(AndroidKeyboardInput.Process(ui.Surface, international));
        Assert.False(AndroidKeyboardInput.Process(ui.Surface, international with { IsDown = false }));
        var client = Assert.IsAssignableFrom<ITextInputClient>(ui.Surface.TextInputClient);
        Assert.True(client.SetSelection(4, 4));
        Assert.True(client.CommitText("ą"));
        Assert.Equal("seedą", ui.Editor.Text);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void NavigationDuringCompositionUsesExistingEditorBehaviorWithoutExecutingACommand()
    {
        using var ui = new SurfaceFixture();
        var client = Assert.IsAssignableFrom<ITextInputClient>(ui.Surface.TextInputClient);
        Assert.True(client.SetSelection(4, 4));
        Assert.True(client.SetComposingText("に"));
        var calls = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++), new KeyGesture(Keys.Left)));
        ui.Send(21, true);
        ui.Send(21, false);
        Assert.Equal(0, calls);
        // A Left delivered past the native IME stage is ordinary editor navigation: it
        // accepts the visible preedit and moves left. This is not native candidate selection.
        Assert.Equal("seedに", ui.Editor.Text);
        Assert.Equal(4, client.GetState()!.SelectionEnd);
        Assert.True(client.CommitText("日本"));
        Assert.Equal("seed日本に", ui.Editor.Text);
    }

    [Fact]
    public void LostReleaseResetPreventsSuppressionOfTheNextUnboundKey()
    {
        using var ui = new SurfaceFixture();
        var available = true;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => { }, () => available),
            new KeyGesture(Keys.S, KeyModifiers.Control)));
        Assert.True(ui.Send(47, true, 0x1000));
        available = false;
        Assert.True(ui.Send(47, true, 0x1000, repeat: 1));
        ui.Surface.ResetKeyboardState();
        Assert.False(ui.Send(47, true));
        Assert.False(ui.Send(47, false));
    }

    [Fact]
    public void ConsumedReleaseCannotClickTheButtonFocusedByTheCommand()
    {
        using var ui = new SurfaceFixture();
        var button = ui.Root.Controls.Add(new Button());
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => button.Select()), new KeyGesture(Keys.Enter)));
        Assert.True(ui.Send(66, true));
        Assert.True(button.Selected);
        Assert.True(ui.Send(66, false));
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void CanceledNativeReleaseCannotActivateTheFocusedButton()
    {
        using var ui = new SurfaceFixture();
        var button = ui.Root.Controls.Add(new Button());
        button.Select();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        ui.Send(62, true);
        var canceled = AndroidInputKeyMapper.FromNative(62, false, 0, 3, isCanceled: true)!.Value;
        Assert.True(AndroidKeyboardInput.Process(ui.Surface, canceled));
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void ThrowingCommandKeepsItsReleaseConsumedWithoutRetry()
    {
        using var ui = new SurfaceFixture();
        var calls = 0;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(() =>
        {
            calls++;
            throw new InvalidOperationException("test command");
        }), new KeyGesture(Keys.F1)));
        Assert.Throws<InvalidOperationException>(() => ui.Send(131, true));
        Assert.True(ui.Send(131, false));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CommandCanDisposeItsBorrowingSurfaceWithoutStaleControlDelivery()
    {
        using var ui = new SurfaceFixture();
        var controls = 0;
        ui.Editor.KeyDown += (_, _) => controls++;
        ui.Root.InputBindings.Add(new KeyBinding(new DelegateCommand(ui.Surface.Dispose), new KeyGesture(Keys.F1)));
        Assert.True(ui.Send(131, true));
        Assert.Equal(0, controls);
        Assert.False(ui.Root.IsDisposed);
        Assert.Null(ui.Root.Parent);
    }

    private sealed class SurfaceFixture : IDisposable
    {
        internal readonly Panel Root = new();
        internal readonly Panel Outer;
        internal readonly Panel Inner;
        internal readonly TextBox Editor;
        internal readonly SkiaControlSurface Surface;

        internal SurfaceFixture()
        {
            Outer = Root.Controls.Add(new Panel { Dock = DockStyle.Fill });
            Inner = Outer.Controls.Add(new Panel { Dock = DockStyle.Fill });
            Editor = Inner.Controls.Add(new TextBox { Text = "seed", MultiLine = true });
            Surface = new SkiaControlSurface(Root);
            Surface.Resize(480, 240);
            Editor.Select();
        }

        internal bool Send(int key, bool down, int meta = 0, int repeat = 0)
        {
            var input = AndroidInputKeyMapper.FromNative(key, down, meta, 3, repeatCount: repeat);
            Assert.NotNull(input);
            return AndroidKeyboardInput.Process(Surface, input.Value);
        }

        public void Dispose()
        {
            Surface.Dispose();
            Root.Dispose();
        }
    }
}
