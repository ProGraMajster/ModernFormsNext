using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ControlDisposalTests
{
    [Theory]
    [InlineData("never opened")]
    [InlineData("open")]
    [InlineData("hidden")]
    [InlineData("closed")]
    [InlineData("removed")]
    public void ComboFinalizerPathDoesNotTouchPopupTreeOrTextInput(string state)
    {
        using var host = ModernFormsTestHost.Create();
        using var combo = new ProbeComboBox();
        var window = host.Show(combo);
        var list = PopupList(combo);
        TestPopupHost? popup = null;
        PopupWindow? canonicalPopup = null;
        TextBox? editor = null;
        if (state != "never opened")
        {
            combo.DroppedDown = true;
            popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
            canonicalPopup = popup.Window;
            editor = list.Controls.Add(new TextBox());
            Assert.True(popup.Input.Focus(editor));
            Assert.NotNull(popup.Window.TextInputClient);
            Assert.True(popup.Window.TextInputClient!.SetComposingText("composition"));
            if (state == "hidden") combo.DroppedDown = false;
            if (state == "closed") popup.Close();
            if (state == "removed") combo.Parent!.Controls.Remove(combo);
        }
        var client = canonicalPopup?.TextInputClient;
        bool listWasDisposed = IsDisposed(list);
        int listDisposed = 0, closed = 0, compositions = 0;
        list.Disposed += (_, _) => listDisposed++;
        if (canonicalPopup is not null) canonicalPopup.Closed += (_, _) => closed++;
        if (editor is not null) editor.TextCompositionChanged += (_, _) => compositions++;

        Exception? failure = OnOtherThread(combo.FinalizeForTest);

        Assert.Null(failure);
        Assert.False(IsDisposed(combo));
        Assert.Equal(listWasDisposed, IsDisposed(list));
        Assert.Equal(0, listDisposed);
        Assert.Equal(0, closed);
        Assert.Equal(0, compositions);
        if (client is not null) Assert.NotNull(client.GetState());
        combo.Dispose();
        Assert.True(IsDisposed(list));
    }

    [Theory]
    [InlineData("never opened")]
    [InlineData("open")]
    [InlineData("hidden")]
    [InlineData("closed")]
    [InlineData("removed")]
    [InlineData("owner closed")]
    public void ExplicitComboDisposalReleasesOwnedResourcesExactlyOnce(string state)
    {
        using var host = ModernFormsTestHost.Create();
        var combo = new ComboBox();
        var window = host.Show(combo);
        var list = PopupList(combo);
        TestPopupHost? popup = null;
        ModernFormsNext.WindowKit.Input.ITextInputClient? client = null;
        if (state != "never opened")
        {
            combo.DroppedDown = true;
            popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
            var editor = list.Controls.Add(new TextBox());
            Assert.True(popup.Input.Focus(editor));
            client = popup.Window.TextInputClient;
            Assert.NotNull(client);
            Assert.True(client.SetComposingText("composition"));
        }
        int listDisposed = 0, comboDisposed = 0, closed = 0;
        list.Disposed += (_, _) => listDisposed++;
        combo.Disposed += (_, _) => comboDisposed++;
        if (popup is not null)
        {
            popup.Window.Closed += (_, _) => closed++;
        }
        if (state == "hidden") combo.DroppedDown = false;
        if (state == "closed") popup!.Close();
        if (state == "removed") combo.Parent!.Controls.Remove(combo);
        if (state == "owner closed") window.Close();
        var popupRoot = list.Parent;

        combo.Dispose();
        combo.Dispose();

        Assert.True(IsDisposed(combo));
        Assert.True(IsDisposed(list));
        Assert.Equal(1, comboDisposed);
        Assert.Equal(1, listDisposed);
        Assert.False(combo.DroppedDown);
        if (popupRoot is not null) Assert.True(IsDisposed(popupRoot));
        if (popup is not null)
        {
            Assert.True(popup.IsClosed);
            Assert.Equal(1, closed);
            Assert.Null(client!.GetState());
            Assert.False(client.CommitText("late"));
        }
        // A caller may retain Items. Its callbacks must no longer call the disposed ComboBox.
        int selectionChanges = 0;
        combo.SelectedIndexChanged += (_, _) => selectionChanges++;
        list.Items.Add("retained");
        list.SelectedIndex = 0;
        Assert.Equal(0, selectionChanges);
    }

    [Fact]
    public void ControlFinalizerPathDoesNotReleaseBindingsOrWalkChildren()
    {
        using var host = ModernFormsTestHost.Create();
        using var root = new ProbeControl();
        var child = root.Controls.Add(new ProbeControl());
        var window = host.Show(root);
        var bindings = root.InputBindings;
        bindings.Add(new KeyBinding(new DelegateCommand(() => { }), new KeyGesture(Keys.F2)));
        int disposed = 0;
        child.Disposed += (_, _) => disposed++;

        Assert.Null(OnOtherThread(root.FinalizeForTest));

        Assert.Single(bindings);
        Assert.False(IsDisposed(root));
        Assert.Equal(0, child.FinalizerCalls);
        Assert.Equal(0, disposed);
        root.Dispose();
        root.Dispose();
        Assert.Empty(bindings);
        Assert.True(IsDisposed(child));
        Assert.Equal(1, disposed);
    }

    [Fact]
    public void ButtonFinalizerPathDoesNotInvokeCommandEventAccessor()
    {
        using var host = ModernFormsTestHost.Create();
        var command = new ThreadBoundCommand();
        using var button = new ProbeButton { Command = command };
        host.Show(button);

        Assert.Null(OnOtherThread(button.FinalizeForTest));
        Assert.Equal(0, command.Removals);
        button.Dispose();
        Assert.Equal(1, command.Removals);
    }

    [Fact]
    public void ComboDisposalContinuesAfterThrowingAndReentrantPopupCloseObserver()
    {
        using var host = ModernFormsTestHost.Create();
        var combo = new ComboBox();
        var window = host.Show(combo);
        combo.DroppedDown = true;
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        var list = PopupList(combo);
        int disposed = 0;
        list.Disposed += (_, _) => disposed++;
        popup.Window.Closed += (_, _) => { combo.Dispose(); throw new CloseFailure(); };

        Assert.Throws<CloseFailure>(combo.Dispose);

        Assert.True(IsDisposed(combo));
        Assert.True(IsDisposed(list));
        Assert.Equal(1, disposed);
        Assert.True(popup.IsClosed);
        combo.Dispose();
        Assert.Equal(1, disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedItemsDoNotKeepDisposedComboAlive(bool openPopup)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        host.Show(form);
        (WeakReference reference, ListBoxItemCollection items) = DisposeAndRetainItems(form, openPopup);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.IsAlive);
        GC.KeepAlive(items);
        GC.KeepAlive(form);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, ListBoxItemCollection) DisposeAndRetainItems(Form form, bool openPopup)
    {
        var combo = form.Controls.Add(new ComboBox());
        combo.Items.Add("retained");
        if (openPopup) combo.DroppedDown = true;
        form.Controls.Remove(combo);
        combo.Dispose();
        return (new WeakReference(combo), combo.Items);
    }

    [Fact]
    public void PopupCannotBeReopenedByDisposalCallbacksOrAfterDisposal()
    {
        using var host = ModernFormsTestHost.Create();
        var combo = new ComboBox();
        var window = host.Show(combo);
        combo.DroppedDown = true;
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        popup.Window.Closed += (_, _) =>
            Assert.Throws<ObjectDisposedException>(() => combo.DroppedDown = true);

        combo.Dispose();

        Assert.Throws<ObjectDisposedException>(() => combo.DroppedDown = true);
        Assert.Null(window.ActivePopup);
    }

    private static bool IsDisposed(Control control) => (bool)typeof(Control)
        .GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;

    private static ListBox PopupList(ComboBox combo) => (ListBox)typeof(ComboBox)
        .GetField("popup_listbox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;

    private static Exception? OnOtherThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        return failure;
    }

    private sealed class ProbeComboBox : ComboBox
    {
        internal void FinalizeForTest() => Dispose(false);
    }

    private sealed class ProbeControl : Control
    {
        internal int FinalizerCalls { get; private set; }
        internal void FinalizeForTest() => Dispose(false);
        protected override void Dispose(bool disposing)
        {
            if (!disposing) FinalizerCalls++;
            base.Dispose(disposing);
        }
    }

    private sealed class ProbeButton : Button
    {
        internal void FinalizeForTest() => Dispose(false);
    }

    private sealed class CloseFailure : Exception;

    private sealed class ThreadBoundCommand : ICommand
    {
        private readonly int thread = Environment.CurrentManagedThreadId;
        internal int Removals { get; private set; }
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove
            {
                if (thread != Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("Command subscription requires its owning UI thread.");
                Removals++;
            }
        }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
