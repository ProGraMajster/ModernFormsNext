using System.ComponentModel;
using System.Windows.Input;
using ModernFormsNext.Accessibility;
using ModernFormsNext.DataBinding;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class BindingNavigatorHostTests
{
    [Fact]
    public void EmptySourceAllowsAddingWithoutInventingACurrentItem()
    {
        using var f = new Fixture(0);
        Assert.Equal("0", f.Navigator.PositionTextBox.Text);
        Assert.Equal("of 0", f.Navigator.CountLabel.Text);
        Assert.False(f.Navigator.MoveNextButton.Enabled);
        Assert.False(f.Navigator.DeleteButton.Enabled);
        Assert.False(f.Navigator.SaveButton.Enabled);
        Assert.True(f.Navigator.AddNewButton.Enabled);
        f.Navigator.AddNewButton.PerformClick();
        Assert.Single(f.Source);
        Assert.Equal("1", f.Navigator.PositionTextBox.Text);
        Assert.True(f.Navigator.DeleteButton.Enabled);
    }

    [Fact]
    public void NavigationAndExternalListChangesUseExistingCurrency()
    {
        using var f = new Fixture(3);
        var nav = f.Navigator;
        Assert.False(nav.MoveFirstButton.Enabled);
        Assert.False(nav.MovePreviousButton.Enabled);
        nav.MoveNextButton.PerformClick();
        Assert.Equal(1, f.Source.Position);
        Assert.Equal("2", nav.PositionTextBox.Text);
        nav.MoveLastButton.PerformClick();
        Assert.Equal(2, f.Source.Position);
        Assert.False(nav.MoveNextButton.Enabled);
        Assert.False(nav.MoveLastButton.Enabled);
        nav.MovePreviousButton.PerformClick();
        nav.MoveFirstButton.PerformClick();
        Assert.Equal(0, f.Source.Position);
        f.Rows.Add(new Record());
        Assert.Equal("of 4", nav.CountLabel.Text);
        f.Source.Position = 3;
        nav.DeleteButton.PerformClick();
        Assert.Equal(3, f.Source.Count);
        Assert.Equal("3", nav.PositionTextBox.Text);
        Assert.Equal("of 3", nav.CountLabel.Text);
    }

    [Fact]
    public void ReadOnlySourceKeepsNavigationAndDisablesDefaultEdits()
    {
        using var f = new Fixture(0);
        f.Source.DataSource = Array.AsReadOnly(new[] { new Record(), new Record() });
        Assert.True(f.Navigator.MoveNextButton.Enabled);
        Assert.False(f.Navigator.AddNewButton.Enabled);
        Assert.False(f.Navigator.DeleteButton.Enabled);
        Assert.False(f.Navigator.SaveButton.Enabled);
        Assert.False(f.Navigator.CancelButton.Enabled);
        f.Navigator.MoveLastButton.PerformClick();
        Assert.Equal(1, f.Source.Position);
    }

    [Fact]
    public void SaveAndCancelOnlyCommitOrRevertTheLocalEditableItem()
    {
        using var f = new Fixture(1);
        var row = f.Rows[0];
        row.BeginEdit();
        row.Name = "draft";
        f.Navigator.CancelButton.PerformClick();
        Assert.Equal("row 0", row.Name);
        row.BeginEdit();
        row.Name = "committed";
        f.Navigator.SaveButton.PerformClick();
        row.Name = "later draft";
        f.Navigator.CancelButton.PerformClick();
        Assert.Equal("committed", row.Name);
    }

    [Fact]
    public void PendingNewRowsCanBeCanceledWhenExistingRowsAreReadOnly()
    {
        using var f = new Fixture(0);
        f.Rows.AllowEdit = false;
        Assert.True(f.Navigator.AddNewButton.Enabled);
        f.Navigator.AddNewButton.PerformClick();
        Assert.True(f.Navigator.CancelButton.Enabled);
        Assert.True(f.Navigator.SaveButton.Enabled);
        f.Navigator.CancelButton.PerformClick();
        Assert.Empty(f.Rows);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("save")]
    [InlineData("cancel")]
    public void ActionFailuresPropagateWithoutRetryOrLosingPresentation(string operation)
    {
        using var f = new Fixture(1);
        var failure = new InvalidOperationException("expected edit failure");
        f.Source.Failure = failure;
        f.Source.FailOperation = operation;
        f.Rows[0].Failure = failure;
        f.Rows[0].FailOperation = operation;
        var button = operation switch
        {
            "add" => f.Navigator.AddNewButton, "delete" => f.Navigator.DeleteButton,
            "save" => f.Navigator.SaveButton, _ => f.Navigator.CancelButton
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(button.PerformClick));
        Assert.Single(f.Source);
        Assert.Equal("1", f.Navigator.PositionTextBox.Text);
        Assert.Equal("of 1", f.Navigator.CountLabel.Text);
        Assert.Equal(1, operation is "add" or "delete" ? f.Source.Failures : f.Rows[0].Failures);
        f.Source.FailOperation = null;
        f.Rows[0].FailOperation = null;
    }

    [Fact]
    public void CustomCommandsKeepTheirOwnParameterAvailabilityAndLocalIntent()
    {
        using var f = new Fixture(2);
        var token = new object();
        object? observed = null;
        bool available = true;
        var command = new DelegateCommand(value => observed = value, _ => available);
        f.Navigator.SaveButton.Command = command;
        f.Navigator.SaveButton.CommandParameter = token;
        f.Source.Position = 1;
        f.Navigator.SaveButton.PerformClick();
        Assert.Same(token, observed);
        available = false;
        command.RaiseCanExecuteChanged();
        Assert.False(f.Navigator.SaveButton.Enabled);
        f.Navigator.AddNewButton.Enabled = false;
        f.Navigator.DeleteButton.Visible = false;
        f.Rows.Add(new Record());
        Assert.False(f.Navigator.AddNewButton.Enabled);
        Assert.False(f.Navigator.DeleteButton.Visible);
        Assert.Same(token, f.Navigator.SaveButton.CommandParameter);
    }

    [Fact]
    public void ReplacementAndDisposalReleaseBorrowedSourceSubscriptions()
    {
        using var f = new Fixture(3);
        using var second = new BindingSource { DataSource = new BindingList<Record> { new() } };
        f.Navigator.BindingSource = second;
        f.Source.Position = 2;
        f.Rows.Add(new Record());
        Assert.Equal("1", f.Navigator.PositionTextBox.Text);
        Assert.Equal("of 1", f.Navigator.CountLabel.Text);
        second.Dispose();
        Assert.Null(f.Navigator.BindingSource);
        Assert.Equal("0", f.Navigator.PositionTextBox.Text);
        f.Navigator.BindingSource = f.Source;
        bool editorDisposed = false;
        f.Navigator.PositionTextBox.Disposed += (_, _) => editorDisposed = true;
        f.Navigator.Dispose();
        f.Source.Position = 0;
        Assert.Equal(4, f.Source.Count); // Navigator never owns/disposes the source.
        Assert.True(editorDisposed);
    }

    [Fact]
    public void ReentrantDisplayObserverCannotRestoreTheOldSourceSnapshot()
    {
        using var f = new Fixture(3);
        using var second = new BindingSource { DataSource = new BindingList<Record> { new() } };
        f.Navigator.PositionTextBox.TextChanged += (_, _) =>
        {
            if (f.Navigator.PositionTextBox.Text == "2") f.Navigator.BindingSource = second;
        };
        f.Source.Position = 1;
        Assert.Same(second, f.Navigator.BindingSource);
        Assert.Equal("1", f.Navigator.PositionTextBox.Text);
        Assert.Equal("of 1", f.Navigator.CountLabel.Text);
        Assert.False(f.Navigator.MoveNextButton.Enabled);
    }

    [Fact]
    public void DisposingFromAnAvailabilityObserverStopsTheOldRefresh()
    {
        using var f = new Fixture(2);
        f.Navigator.MoveNextButton.EnabledChanged += (_, _) => f.Navigator.Dispose();
        f.Source.Position = 1;
        Assert.Throws<ObjectDisposedException>(f.Navigator.RefreshItems);
        Assert.Equal(1, f.Source.Position);
    }

    [Fact]
    public void InitializationScopesDeferPresentationAndRefreshAtTheOutermostEnd()
    {
        using var f = new Fixture(1);
        f.Navigator.BeginInit();
        f.Navigator.BeginInit();
        f.Rows.Add(new Record());
        Assert.Equal("of 1", f.Navigator.CountLabel.Text);
        f.Navigator.EndInit();
        Assert.Equal("of 1", f.Navigator.CountLabel.Text);
        f.Navigator.EndInit();
        Assert.Equal("of 2", f.Navigator.CountLabel.Text);
        Assert.Throws<InvalidOperationException>(f.Navigator.EndInit);
    }

    [Theory]
    [InlineData("2", 1)]
    [InlineData("0", 0)]
    [InlineData("99", 2)]
    [InlineData("ordinary invalid text", 0)]
    public void PositionEditorUsesRealKeyboardAndClampsOrRestores(string text, int expected)
    {
        using var f = new Fixture(3);
        f.Navigator.PositionTextBox.Select();
        f.Navigator.PositionTextBox.Text = text;
        f.Window.Input.PressKey(Keys.Enter);
        Assert.Equal(expected, f.Source.Position);
        Assert.Equal((expected + 1).ToString(), f.Navigator.PositionTextBox.Text);
        f.Navigator.PositionTextBox.Text = "uncommitted";
        f.Window.Input.PressKey(Keys.Escape);
        Assert.Equal((expected + 1).ToString(), f.Navigator.PositionTextBox.Text);
    }

    [Fact]
    public void PublicKeyboardObserverHasFirstRefusalBeforePositionCommit()
    {
        using var f = new Fixture(3);
        f.Navigator.PositionTextBox.Select();
        f.Navigator.PositionTextBox.Text = "3";
        f.Navigator.PositionTextBox.KeyDown += (_, e) => e.Handled = true;
        f.Window.Input.PressKey(Keys.Enter);
        Assert.Equal(0, f.Source.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImplicitChildrenRetainCanonicalFocusSemanticIdentityAndRtl(bool rtl)
    {
        using var f = new Fixture(3);
        var nav = f.Navigator;
        Assert.Empty(nav.Controls); // Runtime chrome is not authored Designer content.
        var identity = nav.PositionTextBox.AccessibilityObject;
        nav.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        f.Window.Resize(1100, 100);
        Assert.True(rtl ? nav.MoveNextButton.Left < nav.MovePreviousButton.Left : nav.MoveNextButton.Left > nav.MovePreviousButton.Left);
        Assert.True(nav.MoveNextButton.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(1, f.Source.Position);
        nav.MoveFirstButton.Select();
        f.Window.Input.PressKey(Keys.Tab);
        Assert.True(nav.MovePreviousButton.Focused);
        f.Window.Input.PressKey(Keys.Tab);
        Assert.True(nav.PositionTextBox.Focused);
        Assert.Same(identity, nav.PositionTextBox.AccessibilityObject);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void ExistingFlowGeometrySurvivesDpiAndNarrowResize(double scale)
    {
        using var f = new Fixture(2);
        var before = f.Navigator.PositionTextBox.Bounds;
        f.Window.SetRenderScale(scale);
        Assert.Equal(before, f.Navigator.PositionTextBox.Bounds);
        f.Window.Resize(240, 100);
        Assert.True(f.Navigator.MoveLastButton.Left >= f.Navigator.MoveNextButton.Right);
        Assert.Equal(2, f.Source.Count);
    }

    [Fact]
    public void ThrowingBorrowedCommandDetachStillDisposesAllOwnedChildren()
    {
        using var f = new Fixture(2);
        var command = new ThrowingDetachCommand();
        f.Navigator.MoveFirstButton.Command = command;
        bool editorDisposed = false, lastButtonDisposed = false;
        f.Navigator.PositionTextBox.Disposed += (_, _) => editorDisposed = true;
        f.Navigator.CancelButton.Disposed += (_, _) => lastButtonDisposed = true;
        Assert.Same(command.Failure, Assert.Throws<InvalidOperationException>(f.Navigator.Dispose));
        Assert.True(editorDisposed);
        Assert.True(lastButtonDisposed);
        Assert.False(command.Disposed);
        Assert.Equal(2, f.Source.Count);
    }

    [Fact]
    public void FinalizerPathOnAnotherThreadDoesNotTouchUiOrManagedSubscriptions()
    {
        using var host = ModernFormsTestHost.Create();
        using var source = new BindingSource { DataSource = new BindingList<Record> { new(), new() } };
        var navigator = new FinalizerProbe { BindingSource = source };
        host.Show(navigator, 1100, 100);
        bool disposed = false;
        navigator.PositionTextBox.Disposed += (_, _) => disposed = true;
        Exception? backgroundFailure = null;
        var worker = new Thread(() => { try { navigator.FinalizerOnly(); } catch (Exception error) { backgroundFailure = error; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(backgroundFailure);
        Assert.False(disposed);
        source.Position = 1;
        Assert.Equal("2", navigator.PositionTextBox.Text);
        Assert.Same(source, navigator.BindingSource);
    }

    private sealed class FinalizerProbe : BindingNavigator
    {
        internal void FinalizerOnly() => base.Dispose(false);
    }

    private sealed class ThrowingDetachCommand : ICommand, IDisposable
    {
        internal Exception Failure { get; } = new InvalidOperationException("expected detach failure");
        internal bool Disposed;
        public event EventHandler? CanExecuteChanged { add { } remove => throw Failure; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void CancelUsesTheCanonicalValidationOptOutForAnInvalidDraft()
    {
        using var host = ModernFormsTestHost.Create();
        using var source = new BindingSource { DataSource = new BindingList<Record> { new(), new() } };
        var root = new Panel();
        var navigator = root.Controls.Add(new BindingNavigator(source));
        var editor = root.Controls.Add(new TextBox { Bounds = new(12, 130, 200, 30) });
        host.Show(root, 1100, 200);
        editor.Validating += (_, e) => e.Cancel = true;
        editor.Select();
        navigator.MoveNextButton.Select();
        Assert.True(editor.Focused);
        navigator.CancelButton.Select();
        Assert.True(navigator.CancelButton.Focused);
    }

    [Fact]
    public void SharedFixtureUsesTheSameProductionTreeBeforeNativeQualification()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        host.Show(root, 380, 440);
        var output = new List<string>();
        using var fixture = new ModernFormsNext.Validation.BindingNavigatorCertification(root.Controls, output.Add);
        fixture.Exercise();
        fixture.PreparePosition(true);
        fixture.CheckRetainedState();
        Assert.Equal(1, fixture.Position);
        Assert.Contains(output, line => line.StartsWith("BINDING_NAVIGATOR:PASS:", StringComparison.Ordinal));
        Assert.Contains("BINDING_NAVIGATOR:RECREATED:PASS", output);
    }

    [Fact]
    public void ReentrantSameSourceReturnDoesNotRaiseAnObsoleteAssignmentEvent()
    {
        using var f = new Fixture(3);
        using var first = new BindingSource { DataSource = new BindingList<Record> { new(), new() }, Position = 1 };
        using var second = new BindingSource { DataSource = new BindingList<Record> { new() } };
        bool replaced = false;
        var observed = new List<BindingSource?>();
        f.Navigator.BindingSourceChanged += (_, _) => observed.Add(f.Navigator.BindingSource);
        f.Navigator.PositionTextBox.TextChanged += (_, _) =>
        {
            if (replaced) return;
            replaced = true;
            f.Navigator.BindingSource = second;
            f.Navigator.BindingSource = first;
        };
        f.Navigator.BindingSource = first;
        Assert.Equal(new[] { second, first }, observed);
        Assert.Equal("2", f.Navigator.PositionTextBox.Text);
        Assert.Equal("of 2", f.Navigator.CountLabel.Text);
    }

    [Fact]
    public void ReentrantInitializationRetiresTheOlderRefreshUntilEndInit()
    {
        using var f = new Fixture(3);
        bool deferred = false;
        f.Navigator.PositionTextBox.TextChanged += (_, _) =>
        {
            if (deferred) return;
            deferred = true;
            f.Navigator.BeginInit();
            f.Rows.Add(new Record());
        };
        f.Source.Position = 1;
        Assert.Equal("of 3", f.Navigator.CountLabel.Text);
        f.Navigator.EndInit();
        Assert.Equal("of 4", f.Navigator.CountLabel.Text);
    }

    [Fact]
    public void CompositeSemanticTraversalDiscoversItsExistingPositionEditor()
    {
        using var f = new Fixture(2);
        var peer = f.Navigator.AccessibilityObject;
        var children = Enumerable.Range(0, peer.GetChildCount()).Select(peer.GetChild).ToArray();
        Assert.Contains(f.Navigator.PositionTextBox.AccessibilityObject, children);
        Assert.Contains(f.Navigator.MoveNextButton.AccessibilityObject, children);
        Assert.Equal(10, children.Length);
        Assert.Equal(children.Length, children.Select(child => child!.RuntimeId).Distinct().Count());
    }

    [Fact]
    public void CompositeExposesTheCanonicalToolbarControlType()
    {
        using var f = new Fixture(2);
        Assert.Equal(AccessibleControlType.ToolBar, f.Navigator.AccessibilityObject.ControlType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrecreatedBindingContextActivatesAnEditorWhenTheTreeIsShown(bool createBeforeAttach)
    {
        using var host = ModernFormsTestHost.Create();
        using var source = new BindingSource { DataSource = new BindingList<Record> { new() { Name = "First" }, new() { Name = "Second" } } };
        var root = new Panel { BindingContext = new BindingContext() };
        var nested = root.Controls.Add(new Panel { Dock = DockStyle.Fill });
        var editor = new TextBox();
        if (createBeforeAttach) editor.Visible = false;
        editor.DataBindings.Add(nameof(TextBox.Text), source, nameof(Record.Name), true, DataSourceUpdateMode.OnPropertyChanged);
        nested.Controls.Add(editor);
        editor.Visible = true;
        host.Show(root, 400, 120);
        Assert.Equal("First", editor.Text);
        source.Position = 1;
        Assert.Equal("Second", editor.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreatedEditorReparentingUsesTheInheritedContextUnlessLocallyOverridden(bool localContext)
    {
        using var host = ModernFormsTestHost.Create();
        var records = new BindingList<Record> { new() { Name = "First" }, new() { Name = "Second" } };
        var root = new Panel();
        var left = root.Controls.Add(new Panel { BindingContext = new BindingContext() });
        var right = root.Controls.Add(new Panel { BindingContext = new BindingContext() });
        right.BindingContext![records].Position = 1;
        var editor = new TextBox();
        if (localContext) editor.BindingContext = left.BindingContext;
        editor.DataBindings.Add(nameof(TextBox.Text), records, nameof(Record.Name), true, DataSourceUpdateMode.OnPropertyChanged);
        left.Controls.Add(editor);
        host.Show(root, 400, 120);
        Assert.Equal("First", editor.Text);
        right.Controls.Add(editor);
        Assert.Equal(localContext ? "First" : "Second", editor.Text);
        Assert.Same(localContext ? left.BindingContext : right.BindingContext, editor.BindingContext);
        right.BindingContext[records].Position = 0;
        Assert.Equal("First", editor.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyBoundListKeepsTheEditorRenderableAndCanBeRepopulated(bool replaceSource)
    {
        using var host = ModernFormsTestHost.Create();
        var records = new BindingList<Record> { new() { Name = "First" } };
        using var source = new BindingSource { DataSource = records };
        var root = new Panel { BindingContext = new BindingContext() };
        var navigator = root.Controls.Add(new BindingNavigator(source));
        var editor = root.Controls.Add(new TextBox { Bounds = new(0, 55, 200, 30) });
        editor.DataBindings.Add(nameof(TextBox.Text), source, nameof(Record.Name), true, DataSourceUpdateMode.OnPropertyChanged);
        using var window = host.Show(root, 800, 120);
        Assert.Equal("First", editor.Text);
        if (replaceSource) source.DataSource = new BindingList<Record>();
        else records.Clear();
        Assert.Equal(string.Empty, editor.Text);
        Assert.Equal("0", navigator.PositionTextBox.Text);
        using var emptyFrame = window.CaptureRenderedSnapshot();
        source.DataSource = new BindingList<Record> { new() { Name = "Restored" } };
        Assert.Equal("Restored", editor.Text);
        Assert.Equal("1", navigator.PositionTextBox.Text);
        using var restoredFrame = window.CaptureRenderedSnapshot();
    }

    [Fact]
    public void NullTextFromAPropertyDescriptorNormalizesToAnEmptyRenderableEditor()
    {
        using var host = ModernFormsTestHost.Create();
        var editor = new TextBox { Text = "Draft" };
        using var window = host.Show(editor, 200, 60);
        TypeDescriptor.GetProperties(editor)[nameof(TextBox.Text)]!.SetValue(editor, null);
        Assert.Equal(string.Empty, editor.Text);
        using var frame = window.CaptureRenderedSnapshot();
    }

    private sealed class Fixture : IDisposable
    {
        internal ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
        internal BindingList<Record> Rows { get; }
        internal FailingSource Source { get; }
        internal BindingNavigator Navigator { get; }
        internal TestWindowHost Window { get; }
        internal Fixture(int count)
        {
            Rows = new BindingList<Record>(Enumerable.Range(0, count).Select(index => new Record { Name = "row " + index }).ToList());
            Source = new FailingSource { DataSource = Rows };
            Navigator = new BindingNavigator(Source);
            Window = Host.Show(Navigator, 1100, 100);
        }
        public void Dispose()
        {
            Source.FailOperation = null;
            foreach (var row in Rows) row.FailOperation = null;
            Host.Dispose();
            Source.Dispose();
        }
    }

    private sealed class FailingSource : BindingSource
    {
        internal string? FailOperation;
        internal Exception? Failure;
        internal int Failures;
        public override object? AddNew() { Fail("add"); return base.AddNew(); }
        public override void RemoveAt(int index) { Fail("delete"); base.RemoveAt(index); }
        private void Fail(string operation) { if (FailOperation == operation) { Failures++; throw Failure!; } }
    }

    public sealed class Record : IEditableObject
    {
        private string before = "";
        public string Name { get; set; } = "";
        internal string? FailOperation;
        internal Exception? Failure;
        internal int Failures;
        public void BeginEdit() => before = Name;
        public void EndEdit() { Fail("save"); before = Name; }
        public void CancelEdit() { Fail("cancel"); Name = before; }
        private void Fail(string operation) { if (FailOperation == operation) { Failures++; throw Failure!; } }
    }
}
