using System.Drawing;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerSnappingSurfaceTests
{
    [Fact]
    public void TheSurfaceCanOwnKeyboardFocusForGestureCancellation()
    {
        using var session = DesignerGroupTestModel.Create(out _, out _);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        // Detached controls are intentionally not visible. Supply the production windowless
        // surface root so this checks focus admission in a real eligible control tree.
        using var input = new SkiaControlSurface(surface);
        Assert.True(surface.CanSelect);
        surface.Select();
        Assert.True(surface.Selected);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AReparentCallbackCannotContinueWithTheOldGestureCoordinates(bool group, bool resize)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var destination = session.Document.Controls.AddNode("Panel", "destination", new(300, 200, 250, 180));
        if (group) session.Host.Selection.SelectNodes([first, second], first);
        else session.SelectNode(first);
        bool reparented = false;
        session.OutputChanged += (_, _) =>
        {
            if (!reparented && session.OutputLines.Last().StartsWith("GESTURE_BEGIN", StringComparison.Ordinal))
            { reparented = true; Assert.True(session.ReparentSelectedNodes(destination)); }
        };
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        var controller = surface.MouseController;
        controller.HandleMouseDown(surface, At(surface, session, resize ? 110 : 40, 35));
        controller.HandleMouseMove(surface, At(surface, session, resize ? 197 : 225, 35));
        controller.HandleMouseUp(surface, At(surface, session, resize ? 197 : 225, 35));
        Assert.True(reparented);
        Assert.Empty(destination.Children);
        Assert.Contains(first, session.Document.Controls);
        Assert.Contains(second, session.Document.Controls);
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.Empty(controller.Guides);
        Assert.False(controller.HasActiveOperation);
        Assert.False(surface.Capture);
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResizeStartsAtEffectiveConstrainedSizeWithEitherSnappingMode(bool enabled)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(400, 200, 100, 30);
        first.Properties["MinimumSize"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Size(160, 20), typeof(Size));
        session.SnappingOptions = new DesignerSnappingOptions { Enabled = enabled };
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 170, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 190, 35));
        surface.MouseController.HandleMouseUp(surface, At(surface, session, 190, 35));
        Assert.Equal(new DesignBounds(10, 20, 180, 30), first.Bounds);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
    }

    [Fact]
    public void LeftResizeHonorsMinimumSizeAndPreservesTheOppositeEdge()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(400, 200, 100, 30);
        first.Properties["MinimumSize"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Size(80, 20), typeof(Size));
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 10, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseUp(surface, At(surface, session, 40, 35));
        Assert.Equal(new DesignBounds(30, 20, 80, 30), first.Bounds);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
    }

    [Fact]
    public void AGuideCannotResizeBeyondMaximumSizeOrLeaveAFalseOverlay()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(165, 90, 60, 30);
        first.Properties["MaximumSize"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Size(150, 60), typeof(Size));
        first.Properties["Margin"] = second.Properties["Margin"] = DesignerPropertyValueEditor.ToDesignPropertyValue(Padding.Empty, typeof(Padding));
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 110, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 258, 35));
        Assert.Equal(new DesignBounds(10, 20, 150, 30), first.Bounds);
        Assert.Empty(surface.MouseController.Guides);
        surface.CancelPointerInteraction();
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
    }

    [Fact]
    public void AnOutOfRangeResizeIsRejectedBeforeAnyAuthoredMutation()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out _);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 10, 35));
        surface.MouseController.HandleMouseMove(surface, new(MouseButtons.Left, 1, int.MinValue, 100, Point.Empty));
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Empty(surface.MouseController.Guides);
        surface.CancelPointerInteraction();
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void LatchedEdgeSurvivesSmallOscillationAndReleasesFromTheRawPointer()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(200, 90, 60, 30);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 225, 35));
        Assert.Equal(200, first.Bounds.X);
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 222, 35));
        Assert.Equal(200, first.Bounds.X);
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 219, 35));
        Assert.Equal(189, first.Bounds.X);
        Assert.Empty(surface.MouseController.Guides);
        surface.CancelPointerInteraction();
        Assert.Equal(10, first.Bounds.X);
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData(Keys.Alt, 195)]
    [InlineData(Keys.Alt | Keys.Control | Keys.AltGraph, 200)]
    public void AltBypassesSnappingWhileAltGraphRetainsItsOwnMeaning(Keys modifiers, int expected)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(200, 90, 60, 30);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 225, 35, modifiers));
        Assert.Equal(expected, first.Bounds.X);
        if (modifiers == Keys.Alt) Assert.Empty(surface.MouseController.Guides);
        surface.CancelPointerInteraction();
    }

    [Fact]
    public void DisablingDuringTheGestureImmediatelyDropsTheLatchWithoutAddingHistory()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(200, 90, 60, 30);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 225, 35));
        Assert.Equal(200, first.Bounds.X);
        session.SnappingOptions = new DesignerSnappingOptions { Enabled = false };
        Assert.Empty(surface.MouseController.Guides);
        Assert.Equal(0, session.CurrentHistory.UndoCount);
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 225, 35));
        Assert.Equal(195, first.Bounds.X);
        surface.MouseController.HandleMouseUp(surface, At(surface, session, 225, 35));
        Assert.Equal(1, session.CurrentHistory.UndoCount);
    }

    [Fact]
    public void GridSnappingAndVisibilityAreIndependentAndDoNotEditTheDocument()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out _);
        session.SnappingOptions = new DesignerSnappingOptions { GridSize = 16, ShowGrid = false, SnapToGrid = true };
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 68, 56));
        Assert.Equal(new DesignBounds(32, 48, 100, 30), first.Bounds);
        surface.CancelPointerInteraction();
    }

    [Fact]
    public void MovingNearASiblingEdgeUsesTheGuideAndRetainsOneUndoUnit()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(200, 90, 60, 30);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 40, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 225, 35));
        surface.MouseController.HandleMouseUp(surface, At(surface, session, 225, 35));
        Assert.Equal(new DesignBounds(200, 20, 100, 30), first.Bounds);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
    }

    [Fact]
    public void ResizingNearASiblingEdgeKeepsTheOppositeEdgeAnchored()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Bounds = new(200, 90, 60, 30);
        session.SelectNode(first);
        using var surface = new DesignerSurface(session) { Width = 1000, Height = 800 };
        surface.MouseController.HandleMouseDown(surface, At(surface, session, 110, 35));
        surface.MouseController.HandleMouseMove(surface, At(surface, session, 197, 35));
        surface.MouseController.HandleMouseUp(surface, At(surface, session, 197, 35));
        Assert.Equal(new DesignBounds(10, 20, 190, 30), first.Bounds);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
    }

    private static MouseEventArgs At(DesignerSurface surface, DesignerSession session, int x, int y, Keys modifiers = Keys.None)
    {
        var view = new DesignerCoordinateMapper().GetView(session, surface.Width, surface.Height);
        return new(MouseButtons.Left, 1, view.ClientX + (int)Math.Round(x * view.Scale),
            view.ClientY + (int)Math.Round(y * view.Scale), Point.Empty, keyData: modifiers);
    }
}
