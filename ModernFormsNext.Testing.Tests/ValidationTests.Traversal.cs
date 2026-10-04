using System.ComponentModel;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class ValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraversalPreservesPendingValuesOnDifferentControlsSharingSource(bool surface)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.B.DataBindings.Add(nameof(TextBox.Text), f.Source, nameof(Model.Second), true);
        f.A.Text = "first edit";
        f.B.Text = "second edit";
        Assert.True(surface ? f.Root.ValidateChildren() : f.Form.ValidateChildren());
        Assert.Equal("first edit", f.Model.Value);
        Assert.Equal("second edit", f.Model.Second);
        Assert.Equal("second edit", f.B.Text);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TraversalFailureReleasesPendingRefreshOnUnvisitedFields(bool surface, bool throws)
    {
        using var f = new Fixture(surface);
        f.B.DataBindings.Add(nameof(TextBox.Text), f.Source, nameof(Model.Second), true);
        f.B.Text = "pending";
        f.A.Validating += (_, e) => { if (throws) throw new InvalidOperationException("observer"); e.Cancel = true; };
        if (throws) Assert.Throws<InvalidOperationException>(() => f.Root.ValidateChildren());
        else Assert.False(f.Root.ValidateChildren());
        Assert.Equal("pending", f.B.Text);
        f.Model.Second = "external update";
        f.Source.ResetBindings(false);
        Assert.Equal("external update", f.B.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitValidationDoesNotRequireFocusOrVisibilityAndRejectsRecursion(bool surface)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.B.Select();
        f.A.Hide();
        f.A.Enabled = false;
        f.A.Text = "after";
        f.A.Validating += (_, _) => Assert.False(f.A.Validate());
        Assert.True(f.A.Validate());
        f.AssertOwner(f.B);
        Assert.Equal("after", f.Model.Value);
        using var detached = new TextBox();
        int events = 0;
        detached.Validated += (_, _) => events++;
        Assert.True(detached.Validate());
        Assert.Equal(1, events);
        detached.Dispose();
        Assert.False(detached.Validate());
        Assert.False(detached.ValidateChildren());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitCancellationAndExceptionKeepFocusAndAllowRetry(bool surface)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.B.Select();
        f.A.Text = "after";
        CancelEventHandler cancel = (_, e) => e.Cancel = true;
        CancelEventHandler fail = (_, _) => throw new InvalidOperationException("validation");
        f.A.Validating += cancel;
        Assert.False(f.A.Validate());
        f.A.Validating -= cancel;
        f.A.Validating += fail;
        Assert.Throws<InvalidOperationException>(() => f.A.Validate());
        f.A.Validating -= fail;
        Assert.Equal(0, f.Model.Writes);
        Assert.True(f.A.Validate());
        f.AssertOwner(f.B);
        Assert.Equal(1, f.Model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraversalUsesPublicDescendantsPreorderIncludingHiddenDisabled(bool surface)
    {
        using var f = new Fixture(surface);
        var nested = f.Root.Controls.Add(new Panel());
        nested.Controls.Add(f.B);
        f.A.Hide();
        f.C.Enabled = false;
        using var disposed = f.Root.Controls.Add(new TextBox());
        disposed.Dispose();
        var order = new List<string>();
        f.Root.Validating += (_, _) => order.Add("root");
        f.A.Validating += (_, _) => order.Add("A");
        f.B.Validating += (_, _) => order.Add("B");
        f.C.Validating += (_, _) => order.Add("C");
        nested.Validating += (_, _) => order.Add("nested");
        disposed.Validating += (_, _) => Assert.Fail("Disposed field must be skipped.");
        Assert.True(f.Root.ValidateChildren());
        Assert.Equal(new[] { "A", "C", "nested", "B" }, order);
        if (!surface)
        {
            order.Clear();
            Assert.True(f.Form.ValidateChildren());
            Assert.Equal(new[] { "root", "A", "C", "nested", "B" }, order);
        }
        using var empty = new Panel();
        Assert.True(empty.ValidateChildren());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TraversalStopsAtFirstFailureWithEarlierWritesRetained(bool surface, bool throws)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.A.Text = "after";
        f.B.Validating += (_, e) => { if (throws) throw new InvalidOperationException("B"); e.Cancel = true; };
        f.C.Validating += (_, _) => Assert.Fail("Traversal must stop at B.");
        if (throws) Assert.Throws<InvalidOperationException>(() => f.Root.ValidateChildren());
        else Assert.False(f.Root.ValidateChildren());
        Assert.Equal("after", f.Model.Value);
        Assert.Equal(1, f.Model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraversalSnapshotSkipsMovedEntriesAndDefersAdditions(bool surface)
    {
        using var f = new Fixture(surface);
        using var other = new Panel();
        int b = 0, c = 0, added = 0;
        f.A.Validating += (_, _) => {
            other.Controls.Add(f.B);
            f.Root.Controls.Add(f.B); // Moved away and back is still a stale snapshot entry.
            var extra = f.Root.Controls.Add(new TextBox());
            extra.Validating += (_, _) => added++;
        };
        f.B.Validating += (_, _) => b++;
        f.C.Validating += (_, _) => c++;
        Assert.True(f.Root.ValidateChildren());
        Assert.Equal(0, b);
        Assert.Equal(1, c);
        Assert.Equal(0, added);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitFocusMutationAbortsRemainingTraversal(bool surface)
    {
        using var f = new Fixture(surface);
        f.C.Select();
        f.A.Validating += (_, _) => { f.B.CausesValidation = false; f.B.Select(); };
        f.C.Validating += (_, _) => Assert.Fail("Stale traversal continued.");
        Assert.False(f.Root.ValidateChildren());
        f.AssertOwner(f.B);
    }

    [Fact]
    public void ExplicitValidationRequiresOwningThread()
    {
        using var control = new TextBox();
        Exception? error = null;
        var thread = new Thread(() => { try { control.Validate(); } catch (Exception ex) { error = ex; } });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(error);
    }
}
