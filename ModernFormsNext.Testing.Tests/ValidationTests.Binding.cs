using System.ComponentModel;
using ModernFormsNext.DataBinding;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class ValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingBindingModeChangePreservesImmediatePropertyUpdates(bool surface)
    {
        using var f = new Fixture(surface);
        var first = f.Bind();
        var second = f.A.DataBindings.Add(nameof(Control.Enabled), f.Source, nameof(Model.Flag), true);
        f.B.Select();
        f.A.Text = "after";
        first.BindingComplete += (_, _) => {
            second.DataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged;
            f.A.Enabled = false;
        };
        Assert.True(f.A.Validate());
        Assert.False(f.Model.Flag);
        Assert.Equal("after", f.Model.Value);
        f.AssertOwner(f.B);
    }

    public static IEnumerable<object[]> FailureCases()
    {
        foreach (bool surface in new[] { false, true })
            foreach (bool formatting in new[] { false, true })
                foreach (bool explicitCall in new[] { false, true })
                    foreach (string failure in new[] { "convert", "parse", "setter" })
                        yield return new object[] { surface, formatting, explicitCall, failure };
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public void BindingFailureRejectsValidationAndPreservesSession(bool surface, bool formatting, bool explicitCall, string failure)
    {
        using var f = new Fixture(surface);
        var binding = f.Bind(formatting, member: failure == "convert" ? nameof(Model.Number) : nameof(Model.Value));
        f.A.Select();
        f.A.Text = "invalid";
        if (failure == "parse") binding.Parse += (_, _) => throw new FormatException("parse");
        if (failure == "setter") f.Model.OnWrite = () => throw new InvalidOperationException("setter");
        int validated = 0, completed = 0;
        BindingCompleteState? completionState = null;
        f.A.Validated += (_, _) => validated++;
        binding.BindingComplete += (_, e) => { completed++; completionState = e.BindingCompleteState; e.Cancel = false; };
        var client = f.Client;
        void Attempt() { if (explicitCall) Assert.False(f.A.Validate()); else f.B.Select(); }
        if (!formatting && failure == "setter") Assert.ThrowsAny<Exception>(Attempt);
        else Attempt();
        f.AssertOwner(f.A);
        Assert.Same(client, f.Client);
        Assert.Equal(0, validated);
        Assert.Equal(formatting ? 1 : 0, completed);
        if (formatting) Assert.Equal(BindingCompleteState.Exception, completionState);
        Assert.Equal(7, f.Model.Number);
        Assert.Equal(failure == "setter" ? 1 : 0, f.Model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingCompleteCancellationStopsFocusAfterSetterSideEffect(bool surface)
    {
        using var f = new Fixture(surface);
        var binding = f.Bind();
        f.A.Select();
        f.A.Text = "after";
        binding.BindingComplete += (_, e) => e.Cancel = true;
        f.A.Validated += (_, _) => Assert.Fail("Canceled completion cannot validate.");
        f.B.Select();
        f.AssertOwner(f.A);
        Assert.Equal("after", f.Model.Value);
        Assert.Equal(1, f.Model.Writes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SourceDataErrorCannotBeAcceptedByClearingCompletionCancel(bool surface, bool formatting)
    {
        using var f = new Fixture(surface);
        var model = new ErrorModel();
        var binding = f.A.DataBindings.Add(nameof(TextBox.Text), model, nameof(ErrorModel.Value), formatting);
        f.A.Select();
        f.A.Text = "invalid";
        model.Reject = true;
        BindingCompleteState? completionState = null;
        binding.BindingComplete += (_, e) => { completionState = e.BindingCompleteState; e.Cancel = false; };
        Assert.False(f.A.Validate());
        if (formatting) Assert.Equal(BindingCompleteState.DataError, completionState);
        f.AssertOwner(f.A);
    }

    [Theory]
    [InlineData(false, DataSourceUpdateMode.OnPropertyChanged)]
    [InlineData(false, DataSourceUpdateMode.Never)]
    [InlineData(true, DataSourceUpdateMode.OnPropertyChanged)]
    [InlineData(true, DataSourceUpdateMode.Never)]
    public void OtherModesAndExplicitReadWriteKeepTheirContract(bool surface, DataSourceUpdateMode mode)
    {
        using var f = new Fixture(surface);
        var binding = f.Bind(mode: mode);
        f.A.Select();
        f.A.Text = "after";
        int expected = mode == DataSourceUpdateMode.OnPropertyChanged ? 1 : 0;
        Assert.Equal(expected, f.Model.Writes);
        f.B.Select();
        Assert.True(f.A.Validate());
        Assert.Equal(expected, f.Model.Writes);
        binding.WriteValue();
        Assert.Equal(expected + 1, f.Model.Writes);
        f.Model.Value = "source";
        binding.ReadValue();
        Assert.Equal("source", f.A.Text);
        Assert.Equal(expected + 2, f.Model.Writes);
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(false, "first")]
    [InlineData(false, "second")]
    [InlineData(true, "none")]
    [InlineData(true, "first")]
    [InlineData(true, "second")]
    public void MultipleBindingsAreOrderedFailFastWithNoRollback(bool surface, string failure)
    {
        using var f = new Fixture(surface);
        var first = f.Bind();
        var second = f.A.DataBindings.Add(nameof(TextBox.Placeholder), f.Source, nameof(Model.Second), true);
        var third = f.A.DataBindings.Add(nameof(Control.Tag), f.Source, nameof(Model.Number), true);
        f.A.Select();
        f.A.Text = "after";
        f.A.Placeholder = "second after";
        f.A.Tag = 9;
        var order = new List<string>();
        first.Parse += (_, _) => { order.Add("first"); if (failure == "first") throw new FormatException(); };
        second.Parse += (_, _) => { order.Add("second"); if (failure == "second") throw new FormatException(); };
        third.Parse += (_, _) => order.Add("third");
        f.B.Select();
        Assert.Equal(failure == "none" ? new[] { "first", "second", "third" } : failure == "first" ? new[] { "first" } : new[] { "first", "second" }, order);
        f.AssertOwner(failure == "none" ? f.B : f.A);
        Assert.Equal(failure == "first" ? 0 : 1, f.Model.Writes);
        Assert.Equal(failure == "none" ? "second after" : "second", f.Model.Second);
        Assert.Equal(failure == "none" ? 9 : 7, f.Model.Number);
    }

    [Theory]
    [InlineData(false, "add")]
    [InlineData(false, "remove")]
    [InlineData(false, "mode")]
    [InlineData(false, "source")]
    [InlineData(true, "add")]
    [InlineData(true, "remove")]
    [InlineData(true, "mode")]
    [InlineData(true, "source")]
    public void ObserverBindingMutationsParticipateInPostObserverSnapshot(bool surface, string mutation)
    {
        using var f = new Fixture(surface);
        Binding? binding = mutation == "add" ? null : f.Bind();
        var replacement = new Model();
        f.A.Select();
        f.A.Text = "after";
        f.A.Validating += (_, _) => {
            switch (mutation)
            {
                case "add": f.Bind(); f.A.Text = "added"; break;
                case "remove": f.A.DataBindings.Remove(binding!); break;
                case "mode": binding!.DataSourceUpdateMode = DataSourceUpdateMode.Never; break;
                case "source": f.Source.DataSource = new BindingList<Model> { replacement }; f.A.Text = "replacement"; break;
            }
        };
        f.B.Select();
        f.AssertOwner(f.B);
        Assert.Equal(mutation == "add" ? 1 : 0, f.Model.Writes);
        if (mutation == "add") Assert.Equal("added", f.Model.Value);
        if (mutation == "source") { Assert.Equal("replacement", replacement.Value); Assert.Equal(1, replacement.Writes); }
    }

    [Theory]
    [InlineData(false, "remove")]
    [InlineData(false, "source")]
    [InlineData(false, "focus")]
    [InlineData(true, "remove")]
    [InlineData(true, "source")]
    [InlineData(true, "focus")]
    public void ParseCallbackCannotWriteThroughStaleBindingOrRequest(bool surface, string mutation)
    {
        using var f = new Fixture(surface);
        var binding = f.Bind();
        var replacement = new Model();
        f.A.Select();
        f.A.Text = "after";
        binding.Parse += (_, _) => {
            if (mutation == "remove") f.A.DataBindings.Remove(binding);
            else if (mutation == "source") f.Source.DataSource = new BindingList<Model> { replacement };
            else { f.C.CausesValidation = false; f.C.Select(); }
        };
        f.B.Select();
        f.AssertOwner(mutation == "focus" ? f.C : f.A);
        Assert.Equal(0, f.Model.Writes);
        Assert.Equal(0, replacement.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingPhaseUsesSnapshotAndSkipsRemovedBindings(bool surface)
    {
        using var f = new Fixture(surface);
        var first = f.Bind();
        var second = f.A.DataBindings.Add(nameof(TextBox.Placeholder), f.Source, nameof(Model.Second), true);
        f.A.Select();
        f.A.Text = "after";
        f.A.Placeholder = "not written";
        first.BindingComplete += (_, _) => {
            f.A.DataBindings.Remove(second);
            f.A.DataBindings.Add(nameof(Control.Tag), f.Source, nameof(Model.Number), true);
            f.A.Tag = 42;
        };
        f.B.Select();
        f.AssertOwner(f.B);
        Assert.Equal(1, f.Model.Writes);
        Assert.Equal("second", f.Model.Second);
        Assert.Equal(7, f.Model.Number);
    }

    [Fact]
    public void GenericComponentKeepsItsDiscoveredValidatingPathExactlyOnce()
    {
        using var component = new GenericEditor();
        var model = new Model();
        component.DataBindings.Add(nameof(GenericEditor.Value), model, nameof(Model.Value), true);
        component.Value = "generic";
        Assert.False(component.RaiseValidation().Cancel);
        Assert.Equal("generic", model.Value);
        Assert.Equal(1, model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrencyManagerWritesCurrentItemAndPreservesOtherRows(bool surface)
    {
        using var f = new Fixture(surface);
        var other = new Model { Value = "row two" };
        f.Source.Add(other);
        f.Bind();
        f.Source.Position = 1;
        f.A.Select();
        f.A.Text = "changed second";
        f.B.Select();
        Assert.Equal("before", f.Model.Value);
        Assert.Equal("changed second", other.Value);
        Assert.Equal(0, f.Model.Writes);
        Assert.Equal(2, other.Writes);
    }

    private sealed class GenericEditor : BindableComponent
    {
        public string Value { get; set; } = "";
        public event CancelEventHandler? Validating;
        internal CancelEventArgs RaiseValidation() { var e = new CancelEventArgs(); Validating?.Invoke(this, e); return e; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FieldlessValueTypeListBindingUsesExistingCurrencySetter(bool surface)
    {
        using var f = new Fixture(surface);
        using var source = new BindingSource { DataSource = new BindingList<int> { 7, 9 } };
        f.A.DataBindings.Add(nameof(TextBox.Text), source, "", true);
        f.A.Select();
        f.A.Text = "42";
        Assert.True(f.A.Validate());
        Assert.Equal(42, source[0]);
        Assert.Equal(9, source[1]);
        f.AssertOwner(f.A);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullSubstitutionAndControlUpdateNeverKeepExistingConversion(bool surface)
    {
        using var f = new Fixture(surface);
        var binding = f.Bind();
        binding.NullValue = "<empty>";
        binding.DataSourceNullValue = null;
        binding.ControlUpdateMode = ControlUpdateMode.Never;
        f.A.Select();
        f.A.Text = "<empty>";
        Assert.True(f.A.Validate());
        Assert.Null(f.Model.Value);
        Assert.Equal("<empty>", f.A.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BeginEditInvalidationPreventsStaleSourceSetter(bool surface)
    {
        using var f = new Fixture(surface);
        var model = new EditableModel();
        f.A.DataBindings.Add(nameof(TextBox.Text), model, nameof(EditableModel.Value), true);
        f.A.Select();
        f.A.Text = "after";
        model.Begin = () => f.A.Dispose();
        f.B.Select();
        f.AssertOwner(null);
        Assert.Equal("before", model.Value);
    }

    private sealed class EditableModel : IEditableObject
    {
        public string Value { get; set; } = "before";
        internal Action? Begin { get; set; }
        public void BeginEdit() => Begin?.Invoke();
        public void EndEdit() { }
        public void CancelEdit() { }
    }

    private sealed class ErrorModel : IDataErrorInfo
    {
        public string Value { get; set; } = "before";
        public bool Reject { get; set; }
        public string Error => Reject ? "rejected" : "";
        public string this[string name] => Error;
    }
}
