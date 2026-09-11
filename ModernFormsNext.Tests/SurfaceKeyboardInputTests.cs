using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Tests;

// Control input uses process-wide application bindings and UI services.
[Collection(InputBindingCollectionTests.Name)]
public sealed class SurfaceKeyboardInputTests : IDisposable
{
    public SurfaceKeyboardInputTests() => Application.ReleaseInputBindings();
    public void Dispose() => Application.ReleaseInputBindings();

    [Theory]
    [InlineData(Key.S, Keys.S)]
    [InlineData(Key.Divide, Keys.Divide)]
    [InlineData(Key.Multiply, Keys.Multiply)]
    [InlineData(Key.Subtract, Keys.Subtract)]
    [InlineData(Key.Add, Keys.Add)]
    [InlineData(Key.Decimal, Keys.Decimal)]
    [InlineData(Key.Separator, Keys.Separator)]
    [InlineData(Key.OemQuotes, Keys.OemQuotes)]
    [InlineData(Key.OemPlus, Keys.Oemplus)]
    [InlineData(Key.OemComma, Keys.Oemcomma)]
    [InlineData(Key.LWin, Keys.LWin)]
    [InlineData(Key.RWin, Keys.RWin)]
    [InlineData((Key)int.MaxValue, Keys.None)]
    public void PlatformFactoryUsesCanonicalIdentityAndExplicitModifiers(Key key, Keys expected)
    {
        const KeyModifiers modifiers = KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt |
            KeyModifiers.Meta | KeyModifiers.AltGraph;
        var e = KeyEventArgs.FromPlatformKey(key, modifiers);
        Assert.Equal(expected, e.KeyCode);
        Assert.Equal(expected | Keys.Control | Keys.Shift | Keys.Alt | Keys.Meta | Keys.AltGraph, e.KeyData);
        Assert.False(e.Handled);
        Assert.False(e.SuppressKeyPress);
        Assert.True(e.AltGraph);
        Assert.False(KeyEventArgs.FromPlatformKey(key, KeyModifiers.Control | KeyModifiers.Alt).AltGraph);
    }

    [Fact]
    public void PlatformFactoryRejectsUnknownModifierBits()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyEventArgs.FromPlatformKey(Key.A, (KeyModifiers)(1 << 20)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticEnterReturnsHandledAndInsertsExactlyOneLineBreak(bool shift)
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { MultiLine = true });
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        var e = new KeyEventArgs(Keys.Enter | (shift ? Keys.Shift : Keys.None));
        Assert.True(surface.TryProcessKeyDown(e));
        Assert.True(e.Handled);
        Assert.Equal("\n", editor.Text);
        surface.TryProcessKeyUp(new KeyEventArgs(e.KeyData));
        Assert.Equal("\n", editor.Text);
    }

    [Fact]
    public void PublicKeyPressHandlerCanConsumeSyntheticEnterWithoutEditing()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { MultiLine = true, Text = "keep" });
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        int calls = 0;
        editor.KeyPress += (_, e) => { calls++; e.Handled = true; };
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Enter)));
        Assert.Equal("keep", editor.Text);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TabUsesNestedTraversalOnceAndShiftTabReturnsToPreviousControl()
    {
        using var root = new Panel();
        var first = root.Controls.Add(new Button { TabIndex = 0 });
        var nested = root.Controls.Add(new Panel { TabIndex = 1 });
        nested.Controls.Add(new Button { TabIndex = 0, Enabled = false });
        nested.Controls.Add(new Button { TabIndex = 1, Visible = false });
        var second = nested.Controls.Add(new Button { TabIndex = 2 });
        var third = root.Controls.Add(new Button { TabIndex = 2 });
        using var surface = new SkiaControlSurface(root);
        first.Select();
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Tab)));
        Assert.Same(second, surface.SelectedControl);
        Assert.True(surface.TryProcessKeyUp(new KeyEventArgs(Keys.Tab)));
        Assert.Same(second, surface.SelectedControl);
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Tab | Keys.Shift)));
        Assert.Same(first, surface.SelectedControl);
        Assert.False(third.Selected);
    }

    [Fact]
    public void RichTextBoxAcceptsTabWithoutTraversalOrDuplicateInsertion()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new RichTextBox { AcceptsTab = true });
        var next = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Tab)));
        Assert.Equal("\t", editor.Text);
        Assert.Same(editor, surface.SelectedControl);
        Assert.False(next.Selected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextBoxPreservesPublicKeyDownHandlingAndDoesNotEdit(bool suppress)
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { Text = "keep" });
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        surface.SetTextSelection(2, 2);
        editor.KeyDown += (_, e) => { if (suppress) e.SuppressKeyPress = true; else e.Handled = true; };
        var e = new KeyEventArgs(suppress ? Keys.Back : Keys.A | Keys.Control);
        Assert.True(surface.TryProcessKeyDown(e));
        Assert.True(e.Handled);
        Assert.Equal("keep", editor.Text);
        var state = Assert.IsType<ControlSurfaceTextInputState>(surface.GetTextInputState());
        Assert.Equal((2, 2), (state.SelectionStart, state.SelectionEnd));
    }

    [Fact]
    public void DerivedEditingMethodMaySuppressWhileReturningFalse()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new SuppressingEditor());
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        var e = new KeyEventArgs(Keys.Enter);
        Assert.True(surface.TryProcessKeyDown(e));
        Assert.True(e.SuppressKeyPress);
        Assert.Empty(editor.Text);
    }

    [Theory]
    [InlineData("focus")]
    [InlineData("hide")]
    [InlineData("remove")]
    [InlineData("dispose")]
    [InlineData("surface")]
    public void CallbackRetiringRouteCannotEditOrActivateItsReplacement(string action)
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { MultiLine = true, Text = "keep" });
        var next = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        int clicks = 0;
        next.Click += (_, _) => clicks++;
        editor.Select();
        editor.KeyDown += (_, _) =>
        {
            switch (action)
            {
                case "focus": next.Select(); break;
                case "hide": editor.Visible = false; break;
                case "remove": root.Controls.Remove(editor); break;
                case "dispose": editor.Dispose(); break;
                case "surface": surface.Dispose(); break;
            }
        };
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Enter)));
        Assert.Equal("keep", editor.Text);
        if (action != "surface")
        {
            next.Select();
            Assert.True(surface.TryProcessKeyUp(new KeyEventArgs(Keys.Enter)));
            Assert.Equal(0, clicks);
            surface.TryProcessKeyDown(new KeyEventArgs(Keys.Enter));
            surface.TryProcessKeyUp(new KeyEventArgs(Keys.Enter));
            Assert.Equal(1, clicks);
        }
        Assert.False(root.IsDisposed);
        editor.Dispose();
    }

    [Fact]
    public void ReparentedAncestorRetiresTheRouteEvenWithTheSameImmediateEditorParent()
    {
        using var root = new Panel();
        var first = root.Controls.Add(new Panel());
        var second = root.Controls.Add(new Panel());
        var inner = first.Controls.Add(new Panel());
        var editor = inner.Controls.Add(new TextBox { MultiLine = true });
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        editor.KeyDown += (_, _) =>
        {
            first.Controls.Remove(inner);
            second.Controls.Add(inner);
            editor.Select();
        };
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Enter)));
        Assert.Same(inner, editor.Parent);
        Assert.Empty(editor.Text);
    }

    [Fact]
    public void DeadKeyBypassesEditingAndClearsOnlyItsPriorConsumedLatch()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { Text = "keep" });
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        surface.SetTextSelection(2, 2);
        int calls = 0, keyEvents = 0;
        editor.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(Keys.A, KeyModifiers.Control | KeyModifiers.Alt)));
        editor.KeyDown += (_, _) => keyEvents++;
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.A | Keys.Control | Keys.Alt)));
        Assert.Equal(1, calls);
        var dead = new KeyEventArgs(Keys.A | Keys.Control | Keys.Alt);
        Assert.False(surface.TryProcessKeyDown(dead, isDeadKey: true));
        Assert.False(dead.AltGraph);
        Assert.False(dead.Handled);
        Assert.Equal(0, keyEvents);
        var state = Assert.IsType<ControlSurfaceTextInputState>(surface.GetTextInputState());
        Assert.Equal((2, 2), (state.SelectionStart, state.SelectionEnd));
        Assert.Equal("keep", editor.Text);
        Assert.False(surface.TryProcessKeyUp(new KeyEventArgs(dead.KeyData)));
        surface.CommitText("ą");
        Assert.Equal("keąep", editor.Text);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ResetReleasesMissingShortcutUpWithoutRemovingBindingsOrChangingSelection()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { Text = "keep" });
        using var surface = new SkiaControlSurface(root);
        editor.Select();
        surface.SetTextSelection(1, 3);
        int calls = 0;
        editor.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(Keys.S, KeyModifiers.Control)));
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Control | Keys.S)));
        surface.ResetKeyboardState();
        Assert.False(surface.TryProcessKeyDown(new KeyEventArgs(Keys.S)));
        Assert.False(surface.TryProcessKeyUp(new KeyEventArgs(Keys.S)));
        Assert.Equal("keep", editor.Text);
        var state = Assert.IsType<ControlSurfaceTextInputState>(surface.GetTextInputState());
        Assert.Equal((1, 3), (state.SelectionStart, state.SelectionEnd));
        Assert.Same(editor, surface.SelectedControl);
        Assert.Single(editor.InputBindings);
        Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Control | Keys.S)));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ThrowingCommandMarksCallerEventAndItsReleaseStillCannotActivate()
    {
        using var root = new Panel();
        var button = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        button.Select();
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        var failure = new InvalidOperationException("expected command failure");
        button.InputBindings.Add(new KeyBinding(new DelegateCommand(() => throw failure), new KeyGesture(Keys.Enter)));
        var e = new KeyEventArgs(Keys.Enter);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => surface.TryProcessKeyDown(e)));
        Assert.True(e.Handled);
        Assert.True(surface.TryProcessKeyUp(new KeyEventArgs(Keys.Enter)));
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void CancellationInvokesRemainingEffectsAndPermitsReentrantResetWithoutRecursion()
    {
        using var root = new Panel();
        var button = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        button.Select();
        var failure = new InvalidOperationException("expected effect failure");
        int first = 0, second = 0;
        button.InteractionEffects.Add(new CancellationEffect(() =>
        {
            if (++first != 1) return;
            surface.ResetKeyboardState();
            Assert.True(surface.TryProcessKeyDown(new KeyEventArgs(Keys.Enter)));
            throw failure;
        }));
        button.InteractionEffects.Add(new CancellationEffect(() => second++));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(surface.ResetKeyboardState));
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        surface.ResetKeyboardState();
        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Theory]
    [InlineData(Keys.Space)]
    [InlineData(Keys.Enter)]
    public void CanceledReleaseCannotClickOrStartRippleAndFreshPressStillWorks(Keys key)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel();
        var button = root.Controls.Add(new Button
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Ripple = new RippleEffect(),
            PressEffect = new PressScaleEffect
            {
                PressedScale = 0.8f, PressDuration = TimeSpan.Zero, ReleaseDuration = TimeSpan.Zero
            }
        });
        using var surface = new SkiaControlSurface(root);
        button.Select();
        var ripple = Assert.IsType<RippleEffect>(button.Ripple);
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        surface.TryProcessKeyDown(new KeyEventArgs(key));
        Assert.Equal(VisualState.Pressed, button.VisualState);
        Assert.Equal(0.8f, button.InteractionScale, 3);
        Assert.True(surface.TryProcessKeyUp(new KeyEventArgs(key), isCanceled: true));
        Assert.Equal(0, clicks);
        Assert.Equal(0, ripple.ActiveRippleCount);
        Assert.NotEqual(VisualState.Pressed, button.VisualState);
        Assert.Equal(1f, button.InteractionScale, 3);
        surface.TryProcessKeyDown(new KeyEventArgs(key));
        Assert.True(surface.TryProcessKeyUp(new KeyEventArgs(key)));
        Assert.Equal(1, clicks);
        Assert.Equal(1, ripple.ActiveRippleCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyboardCleanupPreservesConcurrentPointerCaptureAndPressEffect(bool reset)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel();
        var button = root.Controls.Add(new Button
        {
            Width = 100, Height = 40,
            AnimationSchedulerOverride = harness.Scheduler,
            PressEffect = new PressScaleEffect
            {
                PressedScale = 0.8f, PressDuration = TimeSpan.Zero, ReleaseDuration = TimeSpan.Zero
            }
        });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(160, 80);
        surface.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        Assert.Same(button, surface.SelectedControl);
        Assert.True(button.Capture);
        surface.TryProcessKeyDown(new KeyEventArgs(Keys.Space));
        if (reset) surface.ResetKeyboardState();
        else surface.TryProcessKeyUp(new KeyEventArgs(Keys.Space), isCanceled: true);
        Assert.True(button.Capture);
        Assert.Equal(VisualState.Pressed, button.VisualState);
        Assert.Equal(0.8f, button.InteractionScale, 3);
        surface.ProcessPointer(ControlSurfacePointerAction.Up, 4, 4);
        Assert.False(button.Capture);
        Assert.NotEqual(VisualState.Pressed, button.VisualState);
        Assert.Equal(1f, button.InteractionScale, 3);
    }

    [Fact]
    public void DisposedSurfaceRejectsKeyboardOperationsAndKeepsBorrowedTreeReusable()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox());
        var surface = new SkiaControlSurface(root);
        editor.Select();
        surface.Dispose();
        Assert.Throws<ObjectDisposedException>(() => surface.TryProcessKeyDown(new KeyEventArgs(Keys.A)));
        Assert.Throws<ObjectDisposedException>(() => surface.TryProcessKeyUp(new KeyEventArgs(Keys.A)));
        Assert.Throws<ObjectDisposedException>(surface.ResetKeyboardState);
        Assert.False(root.IsDisposed);
        using var replacement = new SkiaControlSurface(root);
        editor.Select();
        replacement.CommitText("reused");
        Assert.Equal("reused", editor.Text);
    }

    private sealed class SuppressingEditor : TextBox
    {
        protected override bool ProcessTextBoxKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            return false;
        }
    }

    private sealed class CancellationEffect(Action cancel) : InteractionEffect
    {
        protected override void OnKeyboardCanceled() => cancel();
    }
}
