using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerSelectionServiceTests
{
    [Fact]
    public void SingularApiReplacesTheWholeSelectionEvenWhenPrimaryIsUnchanged()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second");
        int notifications = 0;
        selection.SelectionChanged += (_, _) => notifications++;
        selection.SelectNodes([first, second], first);
        selection.Select(first);
        Assert.Same(first, Assert.Single(selection.SelectedNodes));
        Assert.Same(first, selection.SelectedNode);
        selection.Select(first);
        Assert.Equal(2, notifications);
        selection.Clear(); selection.Clear();
        Assert.Empty(selection.SelectedNodes);
        Assert.Null(selection.SelectedNode);
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void OrderedIdentityMembershipHasAnExplicitPrimary()
    {
        var selection = new DesignerSelectionService();
        var first = Node("sameName"); var second = Node("sameName");
        selection.SelectNodes([first, second, first], first);
        Assert.Equal(new[] { first, second }, selection.SelectedNodes);
        Assert.Same(first, selection.SelectedNode);
    }

    [Fact]
    public void DefaultPrimaryIsTheLastDistinctInputMember()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second");
        selection.SelectNodes([first, second, first]);
        Assert.Same(second, selection.SelectedNode);
        selection.SelectNodes([]);
        Assert.Empty(selection.SelectedNodes);
        Assert.Null(selection.SelectedNode);
    }

    [Fact]
    public void EquivalentMembershipAndPrimaryDoNotNotify()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second");
        selection.SelectNodes([first, second], first);
        var snapshot = selection.SelectedNodes;
        int notifications = 0;
        selection.SelectionChanged += (_, _) => notifications++;
        selection.SelectNodes([first, first, second], first);
        Assert.Same(snapshot, selection.SelectedNodes);
        Assert.Equal(0, notifications);
        selection.SelectNodes([second, first], first);
        Assert.Equal(1, notifications);
        Assert.Equal(new[] { second, first }, selection.SelectedNodes);
    }

    [Fact]
    public void MembershipChangeNotifiesEvenWhenPrimaryStaysTheSame()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second"); var third = Node("third");
        selection.SelectNodes([first, second], first);
        int notifications = 0;
        selection.SelectionChanged += (_, _) =>
        {
            notifications++;
            Assert.Equal(new[] { third, first }, selection.SelectedNodes);
            Assert.Same(first, selection.SelectedNode);
        };
        selection.SelectNodes([third, first], first);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void RetainedMembershipIsImmutableAcrossLaterSelection()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second");
        var input = new List<DesignControlNode> { first };
        selection.SelectNodes(input);
        var snapshot = selection.SelectedNodes;
        input.Add(second);
        Assert.Same(first, Assert.Single(snapshot));
        Assert.Throws<NotSupportedException>(() => ((IList<DesignControlNode>)snapshot).Add(second));
        selection.Select(second);
        Assert.Same(first, Assert.Single(snapshot));
        Assert.Same(second, Assert.Single(selection.SelectedNodes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonmemberPrimaryCannotPartiallyChangeSelection(bool empty)
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var other = Node("first");
        selection.Select(first);
        var snapshot = selection.SelectedNodes;
        int notifications = 0;
        selection.SelectionChanged += (_, _) => notifications++;
        Assert.Throws<ArgumentException>(() => selection.SelectNodes(empty ? [] : [first], other));
        Assert.Same(snapshot, selection.SelectedNodes);
        Assert.Same(first, selection.SelectedNode);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void InvalidOrInterruptedInputLeavesThePriorSelectionIntact()
    {
        var selection = new DesignerSelectionService();
        var original = Node("original"); var next = Node("next");
        selection.Select(original);
        var snapshot = selection.SelectedNodes;
        int notifications = 0;
        selection.SelectionChanged += (_, _) => notifications++;
        Assert.Throws<ArgumentNullException>(() => selection.SelectNodes(null!));
        Assert.Throws<ArgumentException>(() => selection.SelectNodes([next, null!]));
        Assert.Throws<InvalidOperationException>(() => selection.SelectNodes(Interrupted(next)));
        Assert.Same(snapshot, selection.SelectedNodes);
        Assert.Same(original, selection.SelectedNode);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void RepeatedCandidatesStillRespectTheExistingDesignerInputBudget()
    {
        var selection = new DesignerSelectionService();
        var original = Node("original"); var next = Node("next");
        selection.Select(original);
        var snapshot = selection.SelectedNodes;
        // This finite benign input allocates one candidate, rather than constructing a large tree.
        Assert.Throws<ArgumentException>(() => selection.SelectNodes(
            Enumerable.Repeat(next, DesignDocumentValidator.MaximumNodeCount + 1)));
        Assert.Same(snapshot, selection.SelectedNodes);
        Assert.Same(original, selection.SelectedNode);
    }

    [Fact]
    public void ReentrantSelectionOwnsTheNewestCompleteState()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second"); var next = Node("next");
        int notifications = 0;
        selection.SelectionChanged += (_, _) =>
        {
            if (++notifications == 1) selection.Select(next);
        };
        selection.SelectNodes([first, second], first);
        Assert.Same(next, selection.SelectedNode);
        Assert.Same(next, Assert.Single(selection.SelectedNodes));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void ObserverFailureDoesNotExposeHalfCommittedMembership()
    {
        var selection = new DesignerSelectionService();
        var first = Node("first"); var second = Node("second");
        var expected = new InvalidOperationException("Benign observer failure");
        selection.SelectionChanged += (_, _) => throw expected;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => selection.SelectNodes([first, second], first)));
        Assert.Same(first, selection.SelectedNode);
        Assert.Equal(new[] { first, second }, selection.SelectedNodes);
    }

    private static DesignControlNode Node(string name) => new() { Name = name, TypeName = "Button" };

    private static IEnumerable<DesignControlNode> Interrupted(DesignControlNode node)
    {
        yield return node;
        throw new InvalidOperationException("Benign input enumeration failure");
    }
}
