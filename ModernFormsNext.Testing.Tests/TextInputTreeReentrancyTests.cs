using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TextInputTreeReentrancyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinishCallbackReparentsTheEditorWithoutTheOuterRemovalOverwritingIt(bool returnToSource)
    {
        using var fixture = new Fixture();
        fixture.Editor.TextCompositionChanged += (_, args) => {
            if (args.Stage != TextCompositionStage.Finished) return;
            fixture.Destination.Controls.Add(fixture.Editor);
            if (returnToSource) fixture.Source.Controls.Add(fixture.Editor);
        };

        fixture.Source.Controls.Remove(fixture.Editor);

        fixture.AssertOwnedBy(returnToSource ? fixture.Source : fixture.Destination);
        Assert.Null(fixture.OldClient.GetState());
        Assert.False(fixture.OldClient.CommitText("obsolete"));
        Assert.Equal("provisional", fixture.Editor.Text);
        var replacement = fixture.Host.Input.TextInputClient;
        Assert.NotNull(replacement);
        Assert.NotSame(fixture.OldClient, replacement);
        Assert.False(replacement.GetState()!.HasComposition);
    }

    [Fact]
    public void FinishCallbackCanReaddDirectlyToTheSourceCollection()
    {
        using var fixture = new Fixture();
        fixture.Editor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Finished)
                fixture.Source.Controls.Add(fixture.Editor);
        };

        fixture.Source.Controls.Remove(fixture.Editor);

        fixture.AssertOwnedBy(fixture.Source);
        Assert.Null(fixture.OldClient.GetState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OuterAddDoesNotDuplicateOrOverwriteTheAssignmentMadeDuringRemoval(bool useThirdParent)
    {
        using var fixture = new Fixture();
        var target = useThirdParent ? fixture.Root.Controls.Add(new Panel()) : fixture.Destination;
        fixture.Editor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Finished)
                target.Controls.Add(fixture.Editor);
        };

        fixture.Destination.Controls.Add(fixture.Editor);

        fixture.AssertOwnedBy(target);
        Assert.Equal(1, target.Controls.Count(control => ReferenceEquals(control, fixture.Editor)));
        Assert.Null(fixture.OldClient.GetState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingFinishLeavesTheRemovalOrNewAssignmentConsistent(bool reparentFirst)
    {
        using var fixture = new Fixture();
        var expected = new InvalidOperationException("Finish observer failed.");
        fixture.Editor.TextCompositionChanged += (_, args) => {
            if (args.Stage != TextCompositionStage.Finished) return;
            if (reparentFirst) fixture.Destination.Controls.Add(fixture.Editor);
            throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => {
            fixture.Source.Controls.Remove(fixture.Editor);
        }));

        fixture.AssertOwnedBy(reparentFirst ? fixture.Destination : null);
        Assert.Null(fixture.OldClient.GetState());
        Assert.Equal("provisional", fixture.Editor.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingFinishStillRunsLostFocusAndReleasesCaptureAndVisualFocus(bool failLostFocus)
    {
        using var fixture = new Fixture();
        var next = fixture.Root.Controls.Add(new Button());
        var finishFailure = new InvalidOperationException("Finish observer failed.");
        var lostFocusFailure = new ArgumentException("LostFocus observer failed.");
        int lostFocusCount = 0;
        EventHandler<TextCompositionEventArgs> finish = (_, args) => {
            if (args.Stage == TextCompositionStage.Finished) throw finishFailure;
        };
        EventHandler lostFocus = (_, _) => {
            lostFocusCount++;
            if (failLostFocus) throw lostFocusFailure;
        };
        fixture.Editor.TextCompositionChanged += finish;
        fixture.Editor.LostFocus += lostFocus;
        fixture.Editor.Capture = true;

        var failure = Record.Exception(() => { fixture.Host.Input.Focus(next); });

        if (failLostFocus) {
            var aggregate = Assert.IsType<AggregateException>(failure);
            Assert.Contains(finishFailure, aggregate.Flatten().InnerExceptions);
            Assert.Contains(lostFocusFailure, aggregate.Flatten().InnerExceptions);
        } else Assert.Same(finishFailure, failure);
        Assert.Equal(1, lostFocusCount);
        Assert.False(fixture.Editor.Selected);
        Assert.False(fixture.Editor.Capture);
        Assert.NotEqual(VisualState.Focused, fixture.Editor.VisualState);
        Assert.False(next.Selected);
        Assert.NotEqual(VisualState.Focused, next.VisualState);
        Assert.Null(fixture.OldClient.GetState());

        fixture.Editor.TextCompositionChanged -= finish;
        fixture.Editor.LostFocus -= lostFocus;
        Assert.True(fixture.Host.Input.Focus(next));
        Assert.Same(next, fixture.Window.FocusedControl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableAncestorAcceptsCompositionAndCannotRestoreTheRetiredCheckpoint(bool hide)
    {
        using var fixture = new Fixture();
        if (hide) fixture.Source.Visible = false;
        else fixture.Source.Enabled = false;

        Assert.Null(fixture.OldClient.GetState());
        Assert.False(fixture.OldClient.CancelComposition());
        Assert.Null(fixture.Host.Input.TextInputClient);
        Assert.Equal("provisional", fixture.Editor.Text);
        if (hide) fixture.Source.Visible = true;
        else fixture.Source.Enabled = true;

        Assert.True(fixture.Host.Input.Focus(fixture.Editor));
        var current = fixture.Host.Input.TextInputClient!;
        Assert.NotSame(fixture.OldClient, current);
        Assert.False(current.GetState()!.HasComposition);
        Assert.True(current.CancelComposition());
        Assert.Equal("provisional", fixture.Editor.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetirementInsideClientStateChangedFinishesAfterObserversUnwind(bool hide)
    {
        using var fixture = new Fixture();
        var editorClient = ((ClientProbeTextBox)fixture.Editor).BorrowTextInputClient();
        bool retired = false;
        editorClient.StateChanged += (_, _) => {
            if (retired || editorClient.GetState()?.HasComposition != true) return;
            retired = true;
            if (hide) fixture.Source.Visible = false;
            else fixture.Source.Enabled = false;
        };

        editorClient.SetSelection(0, 0);

        Assert.True(retired);
        Assert.Null(fixture.OldClient.GetState());
        if (hide) fixture.Source.Visible = true;
        else fixture.Source.Enabled = true;
        fixture.Host.Input.Focus(fixture.Editor);
        var current = fixture.Host.Input.TextInputClient!;
        Assert.False(current.GetState()!.HasComposition);
        Assert.True(current.CancelComposition());
        Assert.Equal("provisional", fixture.Editor.Text);
    }

    private sealed class ClientProbeTextBox : TextBox
    {
        // Exercise the documented protected client seam without reflection or a second editor.
        internal ITextInputClient BorrowTextInputClient() => base.GetTextInputClient()!;
    }

    private sealed class Fixture : IDisposable
    {
        internal ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
        internal Panel Root { get; } = new();
        internal Panel Source { get; }
        internal Panel Destination { get; }
        internal TextBox Editor { get; }
        internal TestWindowHost Window { get; }
        internal ITextInputClient OldClient { get; }

        internal Fixture()
        {
            Source = Root.Controls.Add(new Panel());
            Destination = Root.Controls.Add(new Panel());
            Editor = Source.Controls.Add(new ClientProbeTextBox());
            Window = Host.Show(Root);
            Assert.True(Host.Input.Focus(Editor));
            OldClient = Host.Input.TextInputClient!;
            Assert.True(OldClient.SetComposingText("provisional"));
        }

        internal void AssertOwnedBy(Control? owner)
        {
            Assert.Same(owner, Editor.Parent);
            foreach (var container in Root.Controls)
                Assert.Equal(ReferenceEquals(container, owner) ? 1 : 0,
                    container.Controls.Count(control => ReferenceEquals(control, Editor)));
        }

        public void Dispose()
        {
            // A deliberately removed control is owned by the caller, not by the host window.
            if (Editor.Parent is null) Editor.Dispose();
            Host.Dispose();
        }
    }
}
