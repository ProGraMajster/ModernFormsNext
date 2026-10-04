using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class CanonicalFocusTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothRootsCommitOneOwnerBeforeLostThenGotAndTextHandoff(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        var old = f.Client!;
        old.SetComposingText("provisional");
        var events = new List<string>();
        void Observe(string name)
        {
            events.Add(name);
            f.AssertOwner(f.B);
            Assert.Null(old.GetState());
            Assert.NotNull(f.Client);
            Assert.NotSame(old, f.Client);
        }
        f.A.LostFocus += (_, _) => Observe("A.Lost");
        f.B.GotFocus += (_, _) => Observe("B.Got");

        f.B.Select();
        f.B.Select();

        Assert.Equal(new[] { "A.Lost", "B.Got" }, events);
        Assert.Equal("provisional", f.A.Text);
        Assert.True(f.Client!.CommitText("new"));
        Assert.Equal("new", f.B.Text);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LatestReentrantRequestWinsWithoutObsoleteGotFocus(bool surface, bool redirectFromGot)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        int gotB = 0;
        f.B.GotFocus += (_, _) => gotB++;
        EventHandler redirect = (_, _) => f.C.Select();
        if (redirectFromGot) f.B.GotFocus += redirect;
        else f.A.LostFocus += redirect;

        f.B.Select();

        f.AssertOwner(f.C);
        Assert.Equal(redirectFromGot ? 1 : 0, gotB);
    }

    public static IEnumerable<object[]> RetirementCases()
    {
        foreach (bool surface in new[] { false, true })
            foreach (bool ancestor in new[] { false, true })
                foreach (string action in new[] { "hide", "disable", "remove", "dispose" })
                    yield return new object[] { surface, ancestor, action };
    }

    [Theory]
    [MemberData(nameof(RetirementCases))]
    public void RequiredRetirementClearsOwnerAndRevokesComposition(bool surface, bool ancestor, string action)
    {
        using var f = new Fixture(surface);
        var parent = f.Root.Controls.Add(new Panel());
        parent.Controls.Add(f.B);
        f.B.Select();
        var old = f.Client!;
        old.SetComposingText("kept");
        Control target = ancestor ? parent : f.B;
        int lost = 0;
        f.B.LostFocus += (_, _) => { lost++; f.B.Select(); };

        Retire(target, action);

        f.AssertOwner(null);
        Assert.Equal(1, lost);
        Assert.Null(old.GetState());
        Assert.False(old.CommitText("stale"));
        Assert.Null(f.Client);
        if (action != "dispose") Assert.Equal("kept", f.B.Text);
    }

    [Theory]
    [InlineData(false, "hide")]
    [InlineData(false, "dispose")]
    [InlineData(false, "remove")]
    [InlineData(true, "hide")]
    [InlineData(true, "dispose")]
    [InlineData(true, "remove")]
    public void GotFocusRetirementCannotBeOverwrittenByOuterSelect(bool surface, string action)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        f.B.GotFocus += (_, _) => Retire(f.B, action);
        f.B.Select();
        f.AssertOwner(null);
        Assert.Null(f.Client);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CrossRootReparentRequiresAnExplicitNewRequest(bool surface, bool duringGot)
    {
        using var host = ModernFormsTestHost.Create();
        using var f = new Fixture(surface, host);
        using var other = new Fixture(surface, host);
        other.C.Select();
        if (duringGot) f.B.GotFocus += Move;
        f.B.Select();
        if (!duringGot) Move(null, EventArgs.Empty);

        Assert.Null(f.Owner);
        Assert.False(f.B.Selected);
        other.AssertOwner(other.C);
        f.B.GotFocus -= Move;
        f.B.Select();
        Assert.Same(f.B, other.Owner);
        Assert.False(other.C.Selected);
        Assert.Null(f.Owner);

        void Move(object? sender, EventArgs e) => other.Root.Controls.Add(f.B);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabUsesSameOwnerAndSkipsUnavailableAndNonTabStops(bool surface)
    {
        using var f = new Fixture(surface);
        f.B.Enabled = false;
        f.C.TabStop = false;
        f.C.Select(); // Direct selection deliberately ignores TabStop.
        f.AssertOwner(f.C);
        f.Tab();
        f.AssertOwner(f.A);
        f.B.Enabled = true;
        f.Tab();
        f.AssertOwner(f.B);
        f.Tab(backwards: true);
        f.AssertOwner(f.A);
        f.A.Hide();
        f.Tab();
        f.AssertOwner(f.B);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabSkipsDisposedCandidateStillPresentInTheCollection(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        f.B.Dispose();
        f.Tab();
        f.AssertOwner(f.C);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovalDuringTabDoesNotResumeTheObsoleteSelection(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        f.B.GotFocus += (_, _) => f.Root.Controls.Remove(f.B);
        f.Tab();
        f.AssertOwner(null);
        Assert.Null(f.Client);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerAndSemanticFocusUseTheSameOwnerWithoutNoOpEvents(bool surface)
    {
        using var f = new Fixture(surface);
        int got = 0, lost = 0;
        f.A.GotFocus += (_, _) => got++;
        f.A.LostFocus += (_, _) => lost++;
        f.ClickA();
        f.ClickA();
        f.AssertOwner(f.A);
        Assert.Equal(1, got);
        Assert.Equal(0, lost);
        Assert.True(f.B.AccessibilityObject.PerformAction(AccessibleActions.Focus));
        f.AssertOwner(f.B);
        Assert.Equal(1, lost);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ThrowingObserverDoesNotRollbackCommittedOwner(bool surface, bool got)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        var expected = new InvalidOperationException("observer");
        EventHandler fail = (_, _) => throw expected;
        if (got) f.B.GotFocus += fail;
        else f.A.LostFocus += fail;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(f.B.Select));

        f.AssertOwner(f.B);
        Assert.NotNull(f.Client);
        f.B.GotFocus -= fail;
        f.A.LostFocus -= fail;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingImeAndLostObserverAreBothReportedAfterStateCommits(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        var old = f.Client!;
        old.SetComposingText("kept");
        var finish = new InvalidOperationException("finish");
        var lost = new ArgumentException("lost");
        f.A.TextCompositionChanged += (_, e) => { if (e.Stage == TextCompositionStage.Finished) throw finish; };
        EventHandler failLost = (_, _) => throw lost;
        f.A.LostFocus += failLost;

        var failure = Assert.Throws<AggregateException>(f.B.Select).Flatten();

        Assert.Contains(finish, failure.InnerExceptions);
        Assert.Contains(lost, failure.InnerExceptions);
        f.AssertOwner(f.B);
        Assert.Null(old.GetState());
        Assert.NotNull(f.Client); // The existing host can retry acquisition after retirement failure.
        f.A.LostFocus -= failLost;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImeFinishRedirectCannotBindTheObsoleteDestination(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        var old = f.Client!;
        old.SetComposingText("kept");
        f.A.TextCompositionChanged += (_, e) => { if (e.Stage == TextCompositionStage.Finished) f.C.Select(); };
        int gotB = 0;
        f.B.GotFocus += (_, _) => gotB++;
        f.B.Select();
        f.AssertOwner(f.C);
        Assert.Equal(0, gotB);
        Assert.Null(old.GetState());
        Assert.True(f.Client!.CommitText("C"));
        Assert.Equal("C", f.C.Text);
        Assert.Empty(f.B.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentRootsDoNotStealEachOthersLogicalOwner(bool surface)
    {
        using var host = ModernFormsTestHost.Create();
        using var first = new Fixture(surface, host);
        using var second = new Fixture(surface, host);
        first.A.Select();
        second.B.Select();
        first.AssertOwner(first.A);
        second.AssertOwner(second.B);
        first.C.Select();
        second.AssertOwner(second.B);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerRequestCannotOverrideRedirectFromFocusObserver(bool surface)
    {
        using var f = new Fixture(surface);
        f.B.Select();
        f.A.GotFocus += (_, _) => f.C.Select();
        f.ClickA();
        f.AssertOwner(f.C);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentNullCannotOverwriteReparentFromLostFocus(bool surface)
    {
        using var host = ModernFormsTestHost.Create();
        using var f = new Fixture(surface, host);
        using var other = new Fixture(surface, host);
        f.A.Select();
        f.A.LostFocus += (_, _) => other.Root.Controls.Add(f.A);

        f.A.Parent = null;

        Assert.Same(other.Root, f.A.Parent);
        Assert.Null(f.Owner);
        Assert.Null(other.Owner);
        Assert.False(f.A.Selected);
        f.A.Select();
        Assert.Same(f.A, other.Owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingPointerCleanupStillPublishesFocusLossAndCommitsNewOwner(bool surface)
    {
        using var f = new Fixture(surface);
        f.A.Select();
        f.A.Capture = true;
        int lost = 0;
        var failure = new InvalidOperationException("pointer cleanup");
        EventHandler fail = (_, _) => throw failure;
        f.A.MouseLeave += fail;
        f.A.LostFocus += (_, _) => lost++;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(f.B.Select));

        f.AssertOwner(f.B);
        Assert.Equal(1, lost);
        Assert.False(f.A.Capture);
        f.A.MouseLeave -= fail;
    }

    [Fact]
    public void WindowHideAndCloseClearSelectionAndShowDoesNotRestoreIt()
    {
        using var f = new Fixture(false);
        f.B.Select();
        f.Form.Hide();
        f.AssertOwner(null);
        f.B.Select();
        Assert.False(f.B.Selected);
        f.Form.Show();
        f.AssertOwner(null);
        f.B.Select();
        f.Form.Close();
        Assert.False(f.B.Selected);
        Assert.Null(f.Form.TextInputClient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowDisposalCannotReacquireFocusFromLostFocus(bool observerThrows)
    {
        using var f = new Fixture(false);
        f.B.Select();
        var old = f.Client!;
        int lost = 0;
        var failure = new InvalidOperationException("dispose observer");
        f.B.LostFocus += (_, _) =>
        {
            lost++;
            f.Form.Show();
            f.C.Select();
            if (observerThrows) throw failure;
        };

        if (observerThrows)
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(f.Form.Dispose));
        else
            f.Form.Dispose();

        Assert.Equal(1, lost);
        f.AssertOwner(null);
        Assert.Null(old.GetState());
        Assert.Null(f.Client);
        Assert.Throws<ObjectDisposedException>(() => f.Form.InputBindings);
    }

    private static void Retire(Control target, string action)
    {
        switch (action)
        {
            case "hide": target.Hide(); break;
            case "disable": target.Enabled = false; break;
            case "remove": target.Parent!.Controls.Remove(target); break;
            case "dispose": target.Dispose(); break;
            default: throw new ArgumentException(action);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ModernFormsTestHost host;
        private readonly bool ownsHost;
        private readonly SkiaControlSurface? surface;
        private readonly TestWindowHost? window;
        internal Panel Root { get; } = new() { TabStop = false };
        internal TextBox A { get; } = new() { Bounds = new Rectangle(10, 10, 90, 30), TabIndex = 0 };
        internal TextBox B { get; } = new() { Bounds = new Rectangle(110, 10, 90, 30), TabIndex = 1 };
        internal TextBox C { get; } = new() { Bounds = new Rectangle(210, 10, 90, 30), TabIndex = 2 };
        internal Form Form { get; } = null!;
        internal Control? Owner => surface is not null ? surface.SelectedControl : window!.FocusedControl;
        internal ITextInputClient? Client => surface is not null ? surface.TextInputClient : Form.TextInputClient;

        internal Fixture(bool useSurface, ModernFormsTestHost? sharedHost = null)
        {
            ownsHost = sharedHost is null;
            host = sharedHost ?? ModernFormsTestHost.Create(new TestViewport(400, 200));
            Root.Controls.AddRange(A, B, C);
            if (useSurface)
            {
                surface = new SkiaControlSurface(Root);
                surface.Resize(400, 200);
            }
            else
            {
                Form = new Form { UseSystemDecorations = true };
                Root.Dock = DockStyle.Fill;
                Form.Controls.Add(Root);
                window = host.Show(Form);
            }
        }

        internal void AssertOwner(Control? expected)
        {
            Assert.Same(expected, Owner);
            foreach (Control control in new Control[] { A, B, C })
            {
                Assert.Equal(ReferenceEquals(control, expected), control.Selected);
                Assert.Equal(control.Selected, control.Focused);
            }
        }

        internal void Tab(bool backwards = false)
        {
            if (surface is not null)
            {
                var key = Keys.Tab | (backwards ? Keys.Shift : Keys.None);
                surface.ProcessKeyDown(key);
                surface.ProcessKeyUp(key);
            }
            else window!.Input.Tab(backwards);
        }

        internal void ClickA()
        {
            if (surface is null) window!.Input.Click(A);
            else
            {
                surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 20);
                surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 20, 20);
            }
        }

        public void Dispose()
        {
            surface?.Dispose();
            if (surface is not null) Root.Dispose();
            if (ownsHost) host.Dispose();
        }
    }
}
