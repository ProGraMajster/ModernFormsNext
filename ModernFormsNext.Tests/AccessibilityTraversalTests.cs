using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AccessibilityTraversalTests
{
    [Fact]
    public void DefaultChildEnumerationDoesNotMeasurePresentationGeometry()
    {
        using var viewport = new ScrollableControl { AutoScroll = true, Size = new(300, 100) };
        var children = Enumerable.Range(0, 32).Select(index => new GeometryCountingControl
        {
            Bounds = new(0, index * 30, 80, 24), Rotation = 1
        }).ToArray();
        viewport.Controls.AddRange(children);
        using var surface = new SkiaControlSurface(viewport);
        surface.Resize(300, 100);
        foreach (var child in children)
            Assert.IsType<Control.ControlAccessibleObject>(child.AccessibilityObject);
        foreach (var child in children) child.GeometryReads = 0;

        Assert.Equal(children.Length, viewport.AccessibilityObject.GetChildCount());
        Assert.Same(children[^1].AccessibilityObject, viewport.AccessibilityObject.GetChild(children.Length - 1));
        Assert.Equal(0, children.Sum(child => child.GeometryReads));

        // The counter observes the actual presentation mapping, not a test-only framework hook.
        // Reading State still computes truthful clipping; only membership can avoid that work.
        Assert.True((children[^1].AccessibilityObject.State & AccessibleStates.Offscreen) != 0);
        Assert.True(children[^1].GeometryReads > 0);
    }

    [Fact]
    public void ChildEnumerationKeepsOffscreenPeersButHonorsCustomInvisibleState()
    {
        using var viewport = new ScrollableControl { AutoScroll = true, Size = new(100, 100) };
        var offscreen = viewport.Controls.Add(new Control { Bounds = new(0, 200, 50, 20) });
        var invisible = viewport.Controls.Add(new CustomStateControl(AccessibleStates.Invisible));
        var customOffscreen = viewport.Controls.Add(new CustomStateControl(AccessibleStates.Offscreen));
        var hidden = viewport.Controls.Add(new Control { Visible = false });
        using var surface = new SkiaControlSurface(viewport);
        surface.Resize(100, 100);
        var children = Enumerable.Range(0, viewport.AccessibilityObject.GetChildCount())
            .Select(viewport.AccessibilityObject.GetChild).ToArray();
        Assert.Contains(offscreen.AccessibilityObject, children);
        Assert.Contains(customOffscreen.AccessibilityObject, children);
        Assert.DoesNotContain(invisible.AccessibilityObject, children);
        Assert.DoesNotContain(hidden.AccessibilityObject, children);
    }

    private sealed class GeometryCountingControl : Control
    {
        internal int GeometryReads;
        protected override Rectangle GetScaledBounds(Rectangle bounds, SizeF factor, BoundsSpecified specified)
        {
            GeometryReads++;
            return base.GetScaledBounds(bounds, factor, specified);
        }
    }

    private sealed class CustomStateControl(AccessibleStates state) : Control
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this, state);
        private sealed class Peer(Control owner, AccessibleStates state) : ControlAccessibleObject(owner)
        {
            public override AccessibleStates State => base.State | state;
        }
    }
}
