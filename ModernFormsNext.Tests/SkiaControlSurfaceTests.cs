using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class SkiaControlSurfaceTests
{
    [Fact]
    public void ResizeAndRenderUseBorrowedControlTree()
    {
        var root = new Control();
        using var adapter = new SkiaControlSurface(root);
        using var nativeSurface = SKSurface.Create(new SKImageInfo(320, 240));

        adapter.Resize(320, 240);
        adapter.Render(nativeSurface.Canvas);

        Assert.Equal(320, root.Width);
        Assert.Equal(240, root.Height);
        Assert.Equal(new System.Drawing.Size(320, 240), adapter.LogicalSize);
    }

    [Fact]
    public void CommittedUnicodeTextReachesSelectedTextBox()
    {
        var textBox = new TextBox { Width = 240, Height = 40 };
        var root = new Control { Width = 240, Height = 40 };
        root.Controls.Add(textBox);
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 40);
        Assert.True(textBox.Visible);
        Assert.True(textBox.Enabled);
        Assert.True(textBox.CanSelect);
        Assert.True(textBox.Bounds.Contains(4, 4));
        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        Assert.Same(textBox, adapter.SelectedControl);

        adapter.CommitText("Zażółć 👋");

        Assert.Equal("Zażółć 👋", textBox.Text);
    }

    [Fact]
    public void DeleteBackwardRemovesACompleteEmojiTextElement()
    {
        var textBox = new TextBox { Width = 240, Height = 40 };
        var root = new Control { Width = 240, Height = 40 };
        root.Controls.Add(textBox);
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 40);
        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        adapter.CommitText("A👋🏽");

        adapter.DeleteBackward();

        Assert.Equal("A", textBox.Text);
    }

    [Fact]
    public void CompositionIsReplacedAndCommittedWithoutDuplication()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.SetComposingText("za");
            adapter.SetComposingText("zaż");
            var composing = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal((0, 3), (composing.CompositionStart, composing.CompositionEnd));

            adapter.CommitText("zażółć 👋");

            Assert.Equal("zażółć 👋", textBox.Text);
            var committed = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal(-1, committed.CompositionStart);
            Assert.Equal(committed.Text.Length, committed.SelectionEnd);
        }
    }

    [Fact]
    public void RecordedGboardCompositionRecoverySequenceProducesQwertyOnce()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            var prefixes = new[] { "Q", "Qw", "Qwe", "Qwer", "Qwert", "Qwerty" };
            foreach (var prefix in prefixes)
            {
                adapter.SetComposingText(prefix, 1);

                var state = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
                Assert.Equal(prefix, state.Text);
                Assert.Equal((prefix.Length, prefix.Length), (state.SelectionStart, state.SelectionEnd));
                Assert.Equal((0, prefix.Length), (state.CompositionStart, state.CompositionEnd));

                // This is the sequence captured from Gboard: it finishes the span, then marks the
                // current word as composing again before sending the next complete prefix.
                adapter.FinishComposingText();
                adapter.SetComposingRegion(0, prefix.Length);
            }

            adapter.FinishComposingText();

            Assert.Equal("Qwerty", textBox.Text);
            var committed = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal((6, 6), (committed.SelectionStart, committed.SelectionEnd));
            Assert.Equal((-1, -1), (committed.CompositionStart, committed.CompositionEnd));
        }
    }

    [Fact]
    public void FinishComposingTextKeepsTextAndCaretUnchanged()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            var textChangedCount = 0;
            textBox.TextChanged += (_, _) => textChangedCount++;
            adapter.SetComposingText("test", 1);
            var before = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());

            adapter.FinishComposingText();

            var after = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal(before.Text, after.Text);
            Assert.Equal((before.SelectionStart, before.SelectionEnd), (after.SelectionStart, after.SelectionEnd));
            Assert.Equal((-1, -1), (after.CompositionStart, after.CompositionEnd));
            Assert.Equal(1, textChangedCount);
        }
    }

    [Fact]
    public void SelectionAndCompositionRemainIndependent()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.SetComposingText("abcd", 1);

            adapter.SetTextSelection(1, 1);

            var state = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal((1, 1), (state.SelectionStart, state.SelectionEnd));
            Assert.Equal((0, 4), (state.CompositionStart, state.CompositionEnd));
            Assert.Equal("abcd", textBox.Text);
        }
    }

    [Fact]
    public void CommitTextReplacesCompositionAndAppliesCursorPosition()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.SetComposingText("prefix", 1);

            adapter.CommitText("done", 0);

            var state = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal("done", textBox.Text);
            Assert.Equal((0, 0), (state.SelectionStart, state.SelectionEnd));
            Assert.Equal((-1, -1), (state.CompositionStart, state.CompositionEnd));
        }
    }

    [Fact]
    public void CompositionSupportsMultilinePolishAndEmojiText()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            textBox.MultiLine = true;
            adapter.SetComposingText("Zażółć\n👋🏽", 1);
            adapter.SetComposingText("Zażółć\n👋🏽 rakietę 🚀", 1);
            adapter.CommitText("Zażółć\n👋🏽 rakietę 🚀", 1);

            Assert.Equal("Zażółć\n👋🏽 rakietę 🚀", textBox.Text);
            var state = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal(state.Text.Length, state.SelectionEnd);
            Assert.Equal((-1, -1), (state.CompositionStart, state.CompositionEnd));
        }
    }

    [Fact]
    public void ComposingRegionIsClippedAndDoesNotMoveSelection()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.CommitText("abcdef", 1);
            adapter.SetTextSelection(2, 2);

            adapter.SetComposingRegion(99, -4);

            var state = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal((2, 2), (state.SelectionStart, state.SelectionEnd));
            Assert.Equal((0, 6), (state.CompositionStart, state.CompositionEnd));
            Assert.Equal("abcdef", textBox.Text);
        }
    }

    [Fact]
    public void DeleteSurroundingTextPreservesUnicodeTextElementBoundaries()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.CommitText("A👋🏽B");
            adapter.SetTextSelection("A👋🏽".Length, "A👋🏽".Length);

            adapter.DeleteSurroundingText(1, 1);

            Assert.Equal("A", textBox.Text);
        }
    }

    [Fact]
    public void DeleteSurroundingTextPreservesTheSelectedRange()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.CommitText("abcXYZdef");
            adapter.SetTextSelection(3, 6);

            adapter.DeleteSurroundingText(2, 2);

            Assert.Equal("aXYZf", textBox.Text);
            Assert.Equal(1, textBox.SelectionStart);
            Assert.Equal(4, textBox.SelectionEnd);
        }
    }

    [Fact]
    public void EnterAndArrowKeysUseTheFrameworkKeyboardPipeline()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            textBox.MultiLine = true;
            adapter.CommitText("AB");
            adapter.ProcessKeyDown(Keys.Left);
            adapter.ProcessKeyDown(Keys.Enter);

            Assert.Equal("A\nB", textBox.Text);
        }
    }

    [Fact]
    public void CancelClearsPointerCaptureWithoutClicking()
    {
        var button = new Button { Width = 120, Height = 40 };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var adapter = new SkiaControlSurface(button);
        adapter.Resize(120, 40);

        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(ControlSurfacePointerAction.Cancel, 10, 10);

        Assert.False(button.Capture);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void DownAndUpOnButtonRaiseExactlyOneClickInFrameworkOrder()
    {
        var button = new Button { Width = 120, Height = 40 };
        var events = new List<string>();
        button.MouseDown += (_, _) => events.Add("down");
        button.Click += (_, _) => events.Add("click");
        button.MouseUp += (_, _) => events.Add("up");
        using var adapter = new SkiaControlSurface(button);
        adapter.Resize(120, 40);

        adapter.ProcessPointer(17, ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(17, ControlSurfacePointerAction.Up, 10, 10);

        Assert.Equal(["down", "click", "up"], events);
        Assert.False(button.Capture);
        Assert.Equal(0, adapter.ActivePointerCount);
    }

    [Fact]
    public void MovementBelowThresholdRemainsATap()
    {
        var button = new Button { Width = 120, Height = 40 };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var adapter = new SkiaControlSurface(button) { PointerDragThreshold = 8 };
        adapter.Resize(120, 40);

        adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Move, 15, 14);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, 15, 14);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void NegativeDragThresholdIsRejectedAtAssignment()
    {
        using var adapter = new SkiaControlSurface(new Button());

        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.PointerDragThreshold = -1);
    }

    [Fact]
    public void MovementAboveThresholdCancelsClickButStillReleasesCapturedControl()
    {
        var button = new Button { Width = 120, Height = 40 };
        var clicks = 0;
        var mouseUps = 0;
        button.Click += (_, _) => clicks++;
        button.MouseUp += (_, _) => mouseUps++;
        using var adapter = new SkiaControlSurface(button) { PointerDragThreshold = 8 };
        adapter.Resize(120, 40);

        adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 10);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, 30, 10);

        Assert.Equal(0, clicks);
        Assert.Equal(1, mouseUps);
        Assert.False(button.Capture);
    }

    [Fact]
    public void PointerUpOutsideBoundsGoesToCaptureWithoutClicking()
    {
        var button = new Button { Width = 120, Height = 40 };
        System.Drawing.Point? releasedAt = null;
        var clicks = 0;
        button.MouseUp += (_, e) => releasedAt = e.Location;
        button.Click += (_, _) => clicks++;
        using var adapter = new SkiaControlSurface(button) { PointerDragThreshold = 200 };
        adapter.Resize(120, 40);

        adapter.ProcessPointer(9, ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(9, ControlSurfacePointerAction.Up, 150, 60);

        Assert.Equal(new System.Drawing.Point(150, 60), releasedAt);
        Assert.Equal(0, clicks);
        Assert.False(button.Capture);
    }

    [Fact]
    public void TwoPointersKeepIndependentTargetsAndCompletion()
    {
        var first = new Button { Left = 0, Top = 0, Width = 100, Height = 40 };
        var second = new Button { Left = 120, Top = 0, Width = 100, Height = 40 };
        var root = new Control();
        root.Controls.AddRange(first, second);
        var firstClicks = 0;
        var secondClicks = 0;
        first.Click += (_, _) => firstClicks++;
        second.Click += (_, _) => secondClicks++;
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 80);

        adapter.ProcessPointer(3, ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(7, ControlSurfacePointerAction.Down, 130, 10);
        adapter.ProcessPointer(7, ControlSurfacePointerAction.Up, 130, 10);
        Assert.Equal(1, adapter.ActivePointerCount);
        adapter.ProcessPointer(3, ControlSurfacePointerAction.Up, 10, 10);

        Assert.Equal(1, firstClicks);
        Assert.Equal(1, secondClicks);
        Assert.Equal(0, adapter.ActivePointerCount);
    }

    [Fact]
    public void RemovingCapturedControlCancelsAndForgetsItsPointer()
    {
        var button = new Button { Width = 120, Height = 40 };
        var root = new Control();
        root.Controls.Add(button);
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(160, 80);
        adapter.ProcessPointer(5, ControlSurfacePointerAction.Down, 10, 10);

        root.Controls.Remove(button);

        Assert.False(button.Capture);
        Assert.Equal(0, adapter.ActivePointerCount);
        adapter.ProcessPointer(5, ControlSurfacePointerAction.Up, 10, 10);
    }

    [Fact]
    public void NestedHitTestingDeliversCoordinatesLocalToDeepestControl()
    {
        var probe = new PointerProbe { Left = 7, Top = 9, Width = 60, Height = 30 };
        var panel = new Control { Left = 20, Top = 30, Width = 100, Height = 70 };
        panel.Controls.Add(probe);
        var root = new Control();
        root.Controls.Add(panel);
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(200, 150);

        adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, 32, 43);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, 32, 43);

        Assert.Equal(new System.Drawing.Point(5, 4), probe.LastDownLocation);
        Assert.Equal(1, probe.ClickCount);
    }

    [Fact]
    public void ContentDragScrollsRealScrollableControlAndSuppressesChildClick()
    {
        var (scroll, button, adapter) = CreateScrollableButton();
        using (adapter)
        {
            var originalTop = button.Top;
            var clicks = 0;
            button.Click += (_, _) => clicks++;

            adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 30);
            adapter.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, 5);
            Assert.True(scroll.Capture);
            adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, 20, 5);

            Assert.True(scroll.VerticalScrollProperties.Value > 0);
            Assert.Equal(scroll.VerticalScrollProperties.Value, scroll.TouchScrollPosition.Y);
            Assert.Equal(originalTop - scroll.VerticalScrollProperties.Value, button.Top);
            Assert.Equal(0, clicks);
            Assert.Equal(0, scroll.HorizontalScrollProperties.Value);
            Assert.False(scroll.Capture);
        }
    }

    [Fact]
    public void HorizontalContentDragUpdatesHorizontalScrollbarWithoutVerticalMovement()
    {
        var target = new Button { Left = 10, Top = 10, Width = 140, Height = 40 };
        var filler = new Control { Left = 620, Top = 10, Width = 40, Height = 40 };
        var scroll = new ScrollableControl { AutoScroll = true };
        scroll.Controls.AddRange(target, filler);
        using var adapter = new SkiaControlSurface(scroll) { PointerDragThreshold = 8 };
        adapter.Resize(200, 120);
        scroll.PerformLayout();

        adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, 100, 30);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Move, 50, 30);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, 50, 30);

        Assert.True(scroll.HorizontalScrollProperties.Value > 0);
        Assert.Equal(scroll.HorizontalScrollProperties.Value, scroll.TouchScrollPosition.X);
        Assert.Equal(0, scroll.VerticalScrollProperties.Value);
    }

    [Fact]
    public void ScrollDragClampsAndTapStillClicksOutsideScrollbar()
    {
        var (scroll, button, adapter) = CreateScrollableButton();
        using (adapter)
        {
            adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 30);
            adapter.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, -2000);
            adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, 20, -2000);
            Assert.Equal(scroll.VerticalScrollProperties.Maximum, scroll.VerticalScrollProperties.Value);

            scroll.VerticalScrollProperties.Value = 0;
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            adapter.ProcessPointer(2, ControlSurfacePointerAction.Down, 20, 30);
            adapter.ProcessPointer(2, ControlSurfacePointerAction.Up, 20, 30);

            Assert.Equal(1, clicks);
            Assert.Equal(0, scroll.HorizontalScrollProperties.Value);
        }
    }

    [Fact]
    public void PointerDiagnosticsAreDisabledByDefaultAndCompleteWhenEnabled()
    {
        var messages = new List<string>();
        var button = new Button { Name = "action", Width = 100, Height = 40 };
        using var adapter = new SkiaControlSurface(button, messages.Add);
        adapter.Resize(100, 40);

        adapter.ProcessPointer(42, ControlSurfacePointerAction.Down, 5, 6);
        adapter.ProcessPointer(42, ControlSurfacePointerAction.Up, 5, 6);

        Assert.Contains(messages, message => message.Contains("pointer=42", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("hit=Button#action", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("click=True", StringComparison.Ordinal));
    }

    [Fact]
    public void TapUsesClickSemanticsForCheckBoxRadioButtonAndSwitch()
    {
        var checkBox = new CheckBox { Width = 140, Height = 36, Text = "Check" };
        using (var adapter = new SkiaControlSurface(checkBox))
        {
            adapter.Resize(140, 36);
            Tap(adapter, 10, 10);
            Assert.True(checkBox.Checked);
        }

        var radioButton = new RadioButton { Width = 140, Height = 36, Text = "Radio" };
        using (var adapter = new SkiaControlSurface(radioButton))
        {
            adapter.Resize(140, 36);
            Tap(adapter, 10, 10);
            Assert.True(radioButton.Checked);
        }

        var toggle = new Switch { Width = 140, Height = 40 };
        using (var adapter = new SkiaControlSurface(toggle))
        {
            adapter.Resize(140, 40);
            Tap(adapter, 10, 10);
            Assert.True(toggle.IsToggled);
        }
    }

    [Fact]
    public void TapRoutesThroughListBoxAndListViewHitTesting()
    {
        var listBox = new ListBox { Width = 180, Height = 100, ItemHeight = 24 };
        listBox.Items.AddRange(["first", "second"]);
        using (var adapter = new SkiaControlSurface(listBox))
        {
            adapter.Resize(180, 100);
            var second = listBox.GetItemRectangle(1);
            Tap(adapter, second.Left + 5, second.Top + 5);
            Assert.Equal(1, listBox.SelectedIndex);
        }

        var listView = new ListView { Width = 180, Height = 100 };
        var item = listView.Items.Add("first");
        using (var adapter = new SkiaControlSurface(listView))
        using (var nativeSurface = SKSurface.Create(new SKImageInfo(180, 100)))
        {
            adapter.Resize(180, 100);
            adapter.Render(nativeSurface.Canvas);
            Tap(adapter, item.Bounds.Left + 5, item.Bounds.Top + 5);
            Assert.Same(item, listView.SelectedItem);
        }
    }

    [Fact]
    public void TapActivatesLinkAndDirectScrollbarInteraction()
    {
        var link = new LinkLabel { Width = 180, Height = 40, Text = "Documentation" };
        var linkClicks = 0;
        link.LinkClicked += (_, _) => linkClicks++;
        using (var adapter = new SkiaControlSurface(link))
        using (var nativeSurface = SKSurface.Create(new SKImageInfo(180, 40)))
        {
            adapter.Resize(180, 40);
            adapter.Render(nativeSurface.Canvas);
            Tap(adapter, 5, 10);
            Assert.Equal(1, linkClicks);
        }

        var scrollBar = new VerticalScrollBar
        {
            Width = 20,
            Height = 120,
            Minimum = 0,
            Maximum = 100,
            SmallChange = 5
        };
        using (var adapter = new SkiaControlSurface(scrollBar))
        {
            adapter.Resize(20, 120);
            Tap(adapter, 10, 115);
            Assert.Equal(5, scrollBar.Value);
            Assert.False(scrollBar.Capture);
        }
    }

    [Fact]
    public void TextBoxTapSelectsItAndCancelClearsCapture()
    {
        var textBox = new TextBox { Width = 180, Height = 40, Text = "test" };
        using var adapter = new SkiaControlSurface(textBox);
        adapter.Resize(180, 40);

        adapter.ProcessPointer(4, ControlSurfacePointerAction.Down, 12, 12);
        Assert.True(textBox.Capture);
        adapter.ProcessPointer(4, ControlSurfacePointerAction.Cancel, 12, 12);

        Assert.Same(textBox, adapter.SelectedControl);
        Assert.False(textBox.Capture);
        Assert.Equal(0, adapter.ActivePointerCount);
    }

    [Fact]
    public void OperationsAfterDisposeAreRejectedButRootRemainsOwnedByCaller()
    {
        var root = new Control();
        var adapter = new SkiaControlSurface(root);
        adapter.Dispose();

        Assert.Throws<ObjectDisposedException>(() => adapter.Resize(1, 1));
        root.Text = "Still alive";
        Assert.Equal("Still alive", root.Text);
    }

    private static TextBox CreateSelectedTextBox(out SkiaControlSurface adapter)
    {
        var textBox = new TextBox { Width = 240, Height = 40 };
        var root = new Control { Width = 240, Height = 40 };
        root.Controls.Add(textBox);
        adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 40);
        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        return textBox;
    }

    private static (ScrollableControl Scroll, Button Button, SkiaControlSurface Adapter) CreateScrollableButton()
    {
        var button = new Button { Left = 10, Top = 10, Width = 140, Height = 40 };
        var filler = new Control { Left = 10, Top = 520, Width = 100, Height = 40 };
        var scroll = new ScrollableControl { AutoScroll = true };
        scroll.Controls.AddRange(button, filler);
        var adapter = new SkiaControlSurface(scroll) { PointerDragThreshold = 8 };
        adapter.Resize(200, 160);
        scroll.PerformLayout();
        Assert.True(scroll.VerticalScrollProperties.Maximum > 0);
        return (scroll, button, adapter);
    }

    private static void Tap(SkiaControlSurface adapter, int x, int y)
    {
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Down, x, y);
        adapter.ProcessPointer(1, ControlSurfacePointerAction.Up, x, y);
    }

    private sealed class PointerProbe : Control
    {
        public System.Drawing.Point LastDownLocation { get; private set; }
        public int ClickCount { get; private set; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            LastDownLocation = e.Location;
            base.OnMouseDown(e);
        }

        protected override void OnClick(MouseEventArgs e)
        {
            ClickCount++;
            base.OnClick(e);
        }
    }
}
