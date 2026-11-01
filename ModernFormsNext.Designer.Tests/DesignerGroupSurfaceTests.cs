using System.Drawing;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGroupSurfaceTests
{
    [Fact]
    public void AdditiveMarqueeCountsOverlappingMembershipOnlyOnceWithinTheSelectionBudget()
    {
        using var session = DesignerGroupTestModel.Create(out _, out _);
        session.Document.Controls.Clear();
        for (int index = 0; index <= DesignDocumentValidator.MaximumNodeCount / 2; index++)
            session.Document.Controls.AddNode("Button", $"button{index}", new(10, 20, 20, 20));
        session.Host.Selection.SelectNodes(session.Document.Controls);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        var controller = surface.MouseController;
        controller.HandleMouseDown(surface, At(surface, session, 1, 1, Keys.Control));
        controller.HandleMouseMove(surface, At(surface, session, 100, 100, Keys.Control));
        controller.HandleMouseUp(surface, At(surface, session, 100, 100, Keys.Control));
        Assert.Equal(session.Document.Controls.ToArray(), session.SelectedNodes);
        Assert.Same(session.Document.Controls[^1], session.SelectedNode);
        Assert.False(controller.HasActiveOperation);
        Assert.False(surface.Capture);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ReentrantGestureBeginCannotRetargetTheFrozenGroupOrOverwriteNewSelection()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var newer = session.Document.Controls.AddNode("Label", "newer", new(0, 250, 100, 20));
        session.Host.Selection.SelectNodes([first, second], first);
        bool replaced = false;
        session.OutputChanged += (_, _) =>
        {
            if (!replaced && session.OutputLines.Last().StartsWith("GESTURE_BEGIN", StringComparison.Ordinal))
            { replaced = true; session.SelectNode(newer); }
        };
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 70, 65));
        Assert.True(replaced);
        Assert.Same(newer, session.SelectedNode);
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.False(surface.Capture);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ASelectionFromRollbackObserverIsNewerThanTheReplacementThatCanceledTheGesture()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var replacement = session.Document.Controls.AddNode("Label", "replacement", new(0, 200, 100, 20));
        var latest = session.Document.Controls.AddNode("Label", "latest", new(0, 250, 100, 20));
        session.Host.Selection.SelectNodes([first, second], first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 60, 50));
        session.SelectNode(replacement);
        session.Host.Selection.SelectionChanged += (_, _) =>
        {
            if (ReferenceEquals(session.SelectedNode, first)) session.SelectNode(latest);
        };
        surface.MouseController.CancelOperation(surface, "TestReplacement");
        Assert.Same(latest, session.SelectedNode);
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementSelectionCancelsOldGestureWithoutOverwritingNewerMembership(bool cancelDirectly)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var newer = session.Document.Controls.AddNode("Label", "newer", new(0, 250, 100, 20));
        session.Host.Selection.SelectNodes([first, second], first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        var controller = surface.MouseController;
        controller.HandleMouseDown(surface, At(surface, session, 40, 35));
        controller.HandleMouseMove(surface, At(surface, session, 60, 50));
        session.SelectNode(newer);
        if (cancelDirectly) controller.CancelOperation(surface, "ReplacementThenCaptureLost");
        else
        {
            controller.HandleMouseMove(surface, At(surface, session, 80, 65));
            controller.HandleMouseUp(surface, At(surface, session, 80, 65));
        }
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.Same(newer, session.SelectedNode);
        Assert.Equal(new[] { newer }, session.SelectedNodes);
        Assert.False(controller.HasActiveOperation);
        Assert.False(surface.Capture);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ModifierClicksShareCanonicalMembershipAndPrimaryWithoutHistory()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        Click(surface, session, 40, 35);
        Click(surface, session, 140, 80, Keys.Control);
        Assert.Equal(new[] { first, second }, session.SelectedNodes);
        Assert.Same(second, session.SelectedNode);
        Click(surface, session, 40, 35, Keys.Control);
        Assert.Equal(new[] { second }, session.SelectedNodes);
        Click(surface, session, 40, 35, Keys.Shift);
        Assert.Equal(new[] { second, first }, session.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(surface.Capture);
    }

    [Theory]
    [InlineData(1000, 800)]
    [InlineData(440, 400)]
    public void MarqueeCommitsAfterMouseUpAndCancellationLeavesSelectionUnchanged(int width, int height)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = width, Height = height };
        var controller = surface.MouseController;
        controller.HandleMouseDown(surface, At(surface, session, 1, 1));
        controller.HandleMouseMove(surface, At(surface, session, 250, 140));
        Assert.True(controller.HasActiveOperation);
        Assert.NotNull(controller.MarqueeBounds);
        Assert.Equal(new[] { first }, session.SelectedNodes);
        Assert.False(session.Transactions.HasActiveTransaction);
        Assert.True(controller.CancelOperation(surface, "TestEscape"));
        Assert.Equal(new[] { first }, session.SelectedNodes);
        Assert.False(surface.Capture);
        controller.HandleMouseDown(surface, At(surface, session, 1, 1));
        controller.HandleMouseMove(surface, At(surface, session, 250, 140));
        controller.HandleMouseUp(surface, At(surface, session, 250, 140));
        Assert.Equal(new[] { first, second }, session.SelectedNodes);
        Assert.Same(second, session.SelectedNode);
        Assert.False(controller.HasActiveOperation);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void GroupDragUsesOneDeltaAndCaptureLossRollsBackEveryMember()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], second);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        var controller = surface.MouseController;
        controller.HandleMouseDown(surface, At(surface, session, 40, 35));
        Assert.Same(first, session.SelectedNode);
        Assert.Equal(2, session.SelectedNodes.Count);
        controller.HandleMouseMove(surface, At(surface, session, 60, 50));
        Assert.Equal(new DesignBounds(30, 35, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(100, 75, 120, 40), second.Bounds);
        surface.CancelPointerInteraction();
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.False(session.Transactions.CanUndo);
        controller.HandleMouseDown(surface, At(surface, session, 40, 35));
        controller.HandleMouseMove(surface, At(surface, session, 60, 50));
        controller.HandleMouseUp(surface, At(surface, session, 60, 50));
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.Equal(new[] { first, second }, session.SelectedNodes);
    }

    [Fact]
    public void MarqueeSelectsVisibleAuthoredChildrenAndSkipsInvisibleControls()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Properties["Visible"] = DesignPropertyValue.FromBoolean(false);
        var panel = session.Document.Controls.AddNode("Panel", "panel1", new(220, 10, 200, 160));
        var child = panel.Children.AddNode("Button", "child1", new(10, 10, 100, 30));
        var hit = new DesignerHitTestService(new DesignerCoordinateMapper()).HitTestMarquee(session, new(0, 0, 600, 400));
        Assert.DoesNotContain(first, hit);
        Assert.Contains(second, hit);
        Assert.Contains(panel, hit);
        Assert.Contains(child, hit);
        // Component boundaries are already covered by CustomUserControlPreviewTests; ordinary
        // panels must continue to expose their authored children to the canonical hit tester.
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void RenderedGroupBordersAndPrimaryMarkerFollowTheSameDpiBounds(double scale)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        const int width = 1000, height = 800;
        var info = new SKImageInfo((int)(width * scale), (int)(height * scale));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        new DesignerSurfaceRenderer().Render(new PaintEventArgs(info, canvas, scale), session, width, height);
        var mapper = new DesignerCoordinateMapper();
        var view = mapper.GetView(session, width, height);
        foreach (var node in session.SelectedNodes)
        {
            var bounds = DesignerDpiCoordinateConverter.LogicalToDevice(mapper.ToSurfaceBounds(node.Bounds, view), scale);
            Assert.Equal(DesignerColors.Accent, bitmap.GetPixel(bounds.Left, bounds.Top + (int)(10 * scale)));
        }
        var primary = DesignerDpiCoordinateConverter.LogicalToDevice(mapper.ToSurfaceBounds(first.Bounds, view), scale);
        Assert.Equal(DesignerColors.Accent, bitmap.GetPixel(primary.Left + 2, primary.Top + 2));
    }

    private static void Click(DesignerSurface surface, DesignerSession session, int x, int y, Keys modifiers = Keys.None)
    {
        surface.MouseController.HandleMouseDown(surface, At(surface, session, x, y, modifiers));
        surface.MouseController.HandleMouseUp(surface, At(surface, session, x, y, modifiers));
    }

    private static MouseEventArgs At(DesignerSurface surface, DesignerSession session, int x, int y, Keys modifiers = Keys.None)
    {
        var view = new DesignerCoordinateMapper().GetView(session, surface.Width, surface.Height);
        return new(MouseButtons.Left, 1, view.ClientX + (int)Math.Round(x * view.Scale),
            view.ClientY + (int)Math.Round(y * view.Scale), Point.Empty, keyData: modifiers);
    }
}
