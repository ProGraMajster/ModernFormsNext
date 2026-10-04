using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ControlFocusScopeTests
{
    [Theory]
    [InlineData("cancel")]
    [InlineData("redirect")]
    [InlineData("keep")]
    [InlineData("remove")]
    [InlineData("throw")]
    public void PreflightRunsBeforeCommitAndRevalidatesItsDecision(string action)
    {
        using var root = new Panel();
        var a = root.Controls.Add(new Button());
        var b = root.Controls.Add(new Button());
        var c = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        a.Select();
        var scope = root.GetFocusScope();
        var failure = new InvalidOperationException("preflight");
        scope.Preflight = (old, next) => {
            Assert.Same(a, old);
            Assert.Same(b, next);
            Assert.Same(a, scope.Owner);
            Assert.True(a.Selected);
            Assert.False(b.Selected);
            scope.Preflight = null;
            if (action == "redirect") c.Select();
            if (action == "keep") a.Select();
            if (action == "remove") root.Controls.Remove(b);
            if (action == "throw") throw failure;
            return action != "cancel";
        };

        if (action == "throw") Assert.Same(failure, Assert.Throws<InvalidOperationException>(b.Select));
        else b.Select();

        Assert.Same(action == "redirect" ? c : a, scope.Owner);
        Assert.False(b.Selected);
    }

    [Theory]
    [InlineData("hide")]
    [InlineData("disable")]
    [InlineData("remove")]
    [InlineData("dispose")]
    public void MandatoryRetirementBypassesVetoAndCannotReselectRetiringTree(string action)
    {
        using var root = new Panel();
        var child = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        child.Select();
        var scope = root.GetFocusScope();
        scope.Preflight = (_, _) => throw new InvalidOperationException("Must bypass validation.");
        child.LostFocus += (_, _) => child.Select();

        switch (action)
        {
            case "hide": root.Hide(); break;
            case "disable": root.Enabled = false; break;
            case "remove": surface.Root.Parent!.Controls.Remove(root); break;
            case "dispose": root.Dispose(); break;
        }

        Assert.Null(scope.Owner);
        Assert.False(child.Selected);
    }

    [Fact]
    public void ThreadAffinityAppliesBeforeTheFirstSelection()
    {
        using var root = new Panel();
        var child = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        Exception? error = null;
        var worker = new Thread(() => error = Record.Exception(child.Select));
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(error);
        Assert.Null(surface.SelectedControl);
        Assert.False(child.Selected);
    }

    [Fact]
    public void ReentrantCycleIsBoundedAndLeavesOneCommittedOwner()
    {
        using var root = new Panel();
        var a = root.Controls.Add(new Button());
        var b = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        EventHandler selectB = (_, _) => b.Select();
        EventHandler selectA = (_, _) => a.Select();
        a.GotFocus += selectB;
        b.GotFocus += selectA;
        Assert.Throws<InvalidOperationException>(a.Select);
        Assert.NotEqual(a.Selected, b.Selected);
        Assert.Same(a.Selected ? a : b, surface.SelectedControl);
        a.GotFocus -= selectB;
        b.GotFocus -= selectA;
    }

    [Fact]
    public void DetachedSelectionRetiresOnAdoptionWithoutStealingTheNewRootsOwner()
    {
        using var root = new Panel();
        var a = root.Controls.Add(new Button());
        using var detached = new VisibleButton();
        using var surface = new SkiaControlSurface(root);
        a.Select();
        detached.Select();
        Assert.True(detached.Selected);
        root.Controls.Add(detached);
        Assert.False(detached.Selected);
        Assert.Same(a, surface.SelectedControl);
    }

    [Fact]
    public void ExistingContainerInterfaceIsDiscoverableWithoutOwningNativeFocusRecovery()
    {
        using var container = new ExistingContainer();
        var child = container.Controls.Add(new Button());
        Assert.Same(container, child.GetContainerControl());
        Assert.Same(container, container.GetContainerControl());
        using var surface = new SkiaControlSurface(container);
        child.Select();
        child.Hide();
        Assert.Null(surface.SelectedControl);
        Assert.Equal(0, container.ActivationCalls);
    }

    private sealed class VisibleButton : Button
    {
        public override bool Visible { get => true; set { } }
    }

    private sealed class ExistingContainer : Panel, IContainerControl
    {
        public Control ActiveControl { get; set; } = null!;
        public int ActivationCalls { get; private set; }
        public bool ActivateControl(Control active) { ActivationCalls++; return false; }
    }
}
