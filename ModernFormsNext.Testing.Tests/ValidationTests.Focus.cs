using System.ComponentModel;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class ValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EventsObservePrecommitOwnerThenExistingTextHandoff(bool surface)
    {
        using var f = new Fixture(surface);
        var binding = f.Bind();
        f.A.Select();
        var client = f.Client!;
        var order = new List<string>();
        f.A.Text = "after";
        void Before(string phase, string value)
        {
            order.Add(phase);
            f.AssertOwner(f.A);
            Assert.Same(client, f.Client);
            Assert.Equal(value, f.Model.Value);
        }
        f.A.Validating += (_, _) => Before("validating", "before");
        f.Model.OnWrite = () => Before("write", "after");
        binding.BindingComplete += (_, _) => Before("complete", "after");
        f.A.Validated += (_, _) => Before("validated", "after");
        f.A.LostFocus += (_, _) => { order.Add("lost"); f.AssertOwner(f.B); Assert.Null(client.GetState()); };
        f.B.GotFocus += (_, _) => { order.Add("got"); f.AssertOwner(f.B); };
        f.B.Select();
        f.B.Select();
        Assert.Equal(new[] { "validating", "write", "complete", "validated", "lost", "got" }, order);
        Assert.Equal(1, f.Model.Writes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CancellationPrecedesBindingsRegardlessOfSubscriptionOrder(bool surface, bool bindingFirst)
    {
        using var f = new Fixture(surface);
        if (bindingFirst) f.Bind();
        f.A.Validating += (_, e) => e.Cancel = true;
        if (!bindingFirst) f.Bind();
        f.A.Select();
        f.A.Text = "after";
        int forbidden = 0;
        f.A.Validated += (_, _) => forbidden++;
        f.A.LostFocus += (_, _) => forbidden++;
        f.B.GotFocus += (_, _) => forbidden++;
        var client = f.Client;
        f.B.Select();
        f.AssertOwner(f.A);
        Assert.Equal("before", f.Model.Value);
        Assert.Equal(0, f.Model.Writes);
        Assert.Equal(0, forbidden);
        Assert.Same(client, f.Client);
    }

    public static IEnumerable<object[]> InputCases()
    {
        foreach (bool surface in new[] { false, true })
            foreach (string path in new[] { "select", "tab", "pointer", "accessibility" })
                yield return new object[] { surface, path };
    }

    [Theory]
    [MemberData(nameof(InputCases))]
    public void InputCancellationKeepsCompositionAndDoesNotStartPointerGesture(bool surface, string path)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.A.Select();
        var client = f.Client!;
        client.SetComposingText("composing");
        var state = client.GetState()!;
        Assert.True(state.HasComposition);
        int pointer = 0;
        f.B.Click += (_, _) => pointer++;
        f.B.MouseDown += (_, _) => pointer++;
        f.A.Validating += (_, e) => e.Cancel = true;
        switch (path)
        {
            case "select": f.B.Select(); break;
            case "tab": f.Tab(); break;
            case "pointer": f.ClickB(); break;
            case "accessibility": Assert.False(f.B.AccessibilityObject.PerformAction(AccessibleActions.Focus)); break;
        }
        f.AssertOwner(f.A);
        Assert.Same(client, f.Client);
        Assert.True(client.GetState()!.HasComposition);
        Assert.Equal(state.Revision, client.GetState()!.Revision);
        Assert.False(f.B.Capture);
        Assert.Equal(0, pointer);
        Assert.Equal(0, f.Model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BypassSkipsDepartureButExplicitValidationStillWorks(bool surface)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.A.Select();
        f.A.Text = "after";
        int events = 0;
        f.A.Validating += (_, _) => events++;
        f.A.Validated += (_, _) => events++;
        Assert.True(f.B.CausesValidation);
        f.B.CausesValidation = false;
        f.A.CausesValidation = false;
        f.B.Select();
        f.AssertOwner(f.B);
        Assert.Equal(0, events);
        Assert.Equal(0, f.Model.Writes);
        var client = f.Client;
        Assert.True(f.A.Validate());
        Assert.Equal(2, events);
        Assert.Equal("after", f.Model.Value);
        f.AssertOwner(f.B);
        Assert.Same(client, f.Client);
    }

    public static IEnumerable<object[]> RedirectCases()
    {
        foreach (bool surface in new[] { false, true })
            foreach (bool validated in new[] { false, true })
                foreach (string action in new[] { "redirect", "same", "hide", "dispose", "ownerDispose", "reparent", "throw" })
                    yield return new object[] { surface, validated, action };
    }

    [Theory]
    [MemberData(nameof(RedirectCases))]
    public void CallbackReentrancyCannotCommitObsoleteDestination(bool surface, bool validated, string action)
    {
        using var f = new Fixture(surface);
        using var other = new Panel();
        f.Bind();
        f.A.Select();
        f.A.Text = "after";
        int gotB = 0, validating = 0;
        f.B.GotFocus += (_, _) => gotB++;
        f.A.Validating += (_, _) => validating++;
        void Change()
        {
            switch (action)
            {
                case "redirect": f.B.Select(); f.C.Select(); break;
                case "same": f.A.Select(); break;
                case "hide": f.B.Hide(); break;
                case "dispose": f.B.Dispose(); break;
                case "ownerDispose": f.A.Dispose(); break;
                case "reparent": other.Controls.Add(f.B); break;
                case "throw": throw new InvalidOperationException("observer");
            }
        }
        if (validated) f.A.Validated += (_, _) => Change();
        else f.A.Validating += (_, _) => Change();
        if (action == "throw") Assert.Throws<InvalidOperationException>(() => f.B.Select());
        else f.B.Select();
        f.AssertOwner(action == "redirect" ? f.C : action == "ownerDispose" ? null : f.A);
        Assert.Equal(1, validating);
        Assert.Equal(0, gotB);
        Assert.Equal(validated || action == "redirect" ? 1 : 0, f.Model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedirectThenCancelRejectsEntireDeparture(bool surface)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.A.Select();
        f.A.Text = "after";
        f.A.Validating += (_, e) => { f.C.Select(); e.Cancel = true; };
        f.B.Select();
        f.AssertOwner(f.A);
        Assert.Equal(0, f.Model.Writes);
    }

    public static IEnumerable<object[]> RetirementCases()
    {
        foreach (bool surface in new[] { false, true })
            foreach (string action in new[] { "hide", "disable", "remove", "dispose", "ancestor", "root" })
                yield return new object[] { surface, action };
    }

    [Theory]
    [MemberData(nameof(RetirementCases))]
    public void ForcedRetirementNeverValidates(bool surface, string action)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.A.Select();
        f.A.Text = "after";
        f.A.Validating += (_, _) => throw new InvalidOperationException("must not validate");
        switch (action)
        {
            case "hide": f.A.Hide(); break;
            case "disable": f.A.Enabled = false; break;
            case "remove": f.Root.Controls.Remove(f.A); break;
            case "dispose": f.A.Dispose(); break;
            case "ancestor": f.Root.Hide(); break;
            case "root": if (surface) f.Root.Dispose(); else f.Form.Close(); break;
        }
        if (surface || action != "root") f.AssertOwner(null);
        else { Assert.False(f.A.Selected); Assert.False(f.B.Selected); Assert.False(f.C.Selected); }
        Assert.Null(f.Client);
        Assert.Equal(0, f.Model.Writes);
        if (action == "remove") f.A.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidatingCanChangeCompositionBeforeBindingReadsIt(bool surface)
    {
        using var f = new Fixture(surface);
        f.Bind();
        f.A.Select();
        var old = f.Client!;
        f.A.Validating += (_, _) => { old.SetSelection(0, f.A.Text.Length); old.SetComposingText("accepted"); };
        f.B.Select();
        Assert.Equal("accepted", f.Model.Value);
        f.AssertOwner(f.B);
        Assert.Null(old.GetState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingPointerValidationCannotLeaveAReleaseClickAndNextGestureWorks(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        int clicks = 0;
        f.B.Click += (_, _) => clicks++;
        CancelEventHandler fail = (_, _) => throw new InvalidOperationException("validation");
        f.A.Validating += fail;
        Assert.Throws<InvalidOperationException>(() => f.ClickB());
        f.ReleaseB();
        Assert.Equal(0, clicks);
        Assert.False(f.B.Capture);
        f.AssertOwner(f.A);
        f.A.Validating -= fail;
        f.ClickB();
        Assert.Equal(1, clicks);
        f.AssertOwner(f.B);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OwnerReparentFromValidationRetiresSessionWithoutWriting(bool surface, bool validated)
    {
        using var f = new Fixture(surface);
        using var other = new Panel();
        f.Bind();
        f.A.Select();
        f.A.Text = "after";
        var client = f.Client!;
        if (validated) f.A.Validated += (_, _) => other.Controls.Add(f.A);
        else f.A.Validating += (_, _) => other.Controls.Add(f.A);
        f.B.Select();
        f.AssertOwner(null);
        Assert.Null(client.GetState());
        Assert.Equal(validated ? 1 : 0, f.Model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestinationMovedAwayAndBackCannotResumeOldRequest(bool surface)
    {
        using var f = new Fixture(surface);
        using var other = new Panel();
        f.A.Select();
        f.A.Validating += (_, _) => { other.Controls.Add(f.B); f.Root.Controls.Add(f.B); };
        f.B.Select();
        f.AssertOwner(f.A);
    }
}
