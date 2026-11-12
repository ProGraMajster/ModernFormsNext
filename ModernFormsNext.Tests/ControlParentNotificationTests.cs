using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ControlParentNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachNotifiesOnceAndSameParentAssignmentIsSilent(bool useProperty)
    {
        using var owner = new Panel();
        using var child = new Button();
        var parents = Observe(child, owner);

        Attach(child, owner, useProperty);
        Assert.Equal(new Control?[] { owner }, parents);

        parents.Clear();
        child.Parent = owner;
        Assert.Empty(parents);
        AssertOwner(child, owner, owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReparentPublishesEachCommittedDetachAndAttach(bool useProperty)
    {
        using var first = new Panel();
        using var second = new Panel();
        using var child = new Button();
        first.Controls.Add(child);
        var parents = Observe(child, first, second);

        Attach(child, second, useProperty);

        Assert.Equal(new Control?[] { null, second }, parents);
        AssertOwner(child, second, first, second);
        parents.Clear();
        if (useProperty) child.Parent = null;
        else second.Controls.Remove(child);
        child.Parent = null;
        Assert.Equal(new Control?[] { null }, parents);
        AssertOwner(child, null, first, second);
    }

    [Fact]
    public void ConstructorOwnedImplicitChildRetainsItsIndependentNotificationPath()
    {
        using var owner = new Panel();
        using var child = new Button();
        var parents = Observe(child, owner);

        owner.Controls.AddImplicitControl(child);
        child.SetParentInternal(owner);

        Assert.Equal(new Control?[] { owner }, parents);
        Assert.DoesNotContain(child, owner.Controls);
        Assert.Contains(child, owner.Controls.GetAllControls());
        child.Parent = null;
        Assert.Equal(new Control?[] { owner, null }, parents);
        AssertOwner(child, null, owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachCallbackCanRedirectWithoutTheOuterSetterStealingTheChild(bool reparent)
    {
        using var first = new Panel();
        using var requested = new Panel();
        using var redirected = new Panel();
        using var child = new Button();
        first.Controls.Add(child);
        var parents = Observe(child, first, requested, redirected);
        child.ParentChanged += (_, _) => {
            if (child.Parent is null) child.Parent = redirected;
        };

        child.Parent = reparent ? requested : null;

        Assert.Equal(new Control?[] { null, redirected }, parents);
        AssertOwner(child, redirected, first, requested, redirected);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("remove")]
    [InlineData("dispose")]
    public void AttachCallbackOwnsTheFinalTreeWithoutAnObsoleteExtraNotification(string action)
    {
        using var owner = new Panel();
        using var redirected = new Panel();
        using var child = new Button();
        var parents = Observe(child, owner, redirected);
        child.ParentChanged += (_, _) => {
            if (child.Parent != owner) return;
            switch (action) {
                case "redirect": child.Parent = redirected; break;
                case "remove": owner.Controls.Remove(child); break;
                case "dispose": child.Dispose(); break;
            }
        };

        child.Parent = owner;

        Control? expected = action == "redirect" ? redirected : action == "remove" ? null : owner;
        // Disposal retains the existing parent/collection contract; it is not a detach.
        Control?[] expectedEvents = action == "redirect" ? [owner, null, redirected]
            : action == "remove" ? [owner, null] : [owner];
        Assert.Equal(expectedEvents, parents);
        Assert.Equal(action == "dispose", child.IsDisposed);
        AssertOwner(child, expected, owner, redirected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingObserverDoesNotRollBackACommittedAttachOrDetach(bool detach)
    {
        using var owner = new Panel();
        using var child = new Button();
        if (detach) owner.Controls.Add(child);
        var parents = Observe(child, owner);
        var failure = new InvalidOperationException("Parent observer failed.");
        child.ParentChanged += (_, _) => throw failure;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => child.Parent = detach ? null : owner));

        Assert.Equal(new Control?[] { detach ? null : owner }, parents);
        AssertOwner(child, detach ? null : owner, owner);
    }

    [Fact]
    public void RedirectThenThrowPreservesTheNewerCanonicalAssignment()
    {
        using var owner = new Panel();
        using var redirected = new Panel();
        using var child = new Button();
        var parents = Observe(child, owner, redirected);
        var failure = new InvalidOperationException("Redirecting observer failed.");
        child.ParentChanged += (_, _) => {
            if (child.Parent != owner) return;
            child.Parent = redirected;
            throw failure;
        };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => child.Parent = owner));

        Assert.Equal(new Control?[] { owner, null, redirected }, parents);
        AssertOwner(child, redirected, owner, redirected);
    }

    private static void Attach(Control child, Control owner, bool useProperty)
    {
        if (useProperty) child.Parent = owner;
        else owner.Controls.Add(child);
    }

    private static List<Control?> Observe(Control child, params Control[] owners)
    {
        var parents = new List<Control?>();
        child.ParentChanged += (_, _) => {
            AssertOwner(child, child.Parent, owners);
            parents.Add(child.Parent);
        };
        return parents;
    }

    private static void AssertOwner(Control child, Control? expected, params Control[] owners)
    {
        Assert.Same(expected, child.Parent);
        foreach (var owner in owners)
            Assert.Equal(owner == expected ? 1 : 0,
                owner.Controls.GetAllControls().Count(item => ReferenceEquals(item, child)));
    }
}
