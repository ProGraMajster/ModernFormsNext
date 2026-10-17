using System.Drawing;

namespace ModernFormsNext;

public partial class ScrollableControl
{
    // These hooks belong to the existing surface router. The borrowed identity is its actual
    // pointer-state object, allowing built-in consumers to reject a retired completion even
    // when application callbacks start another gesture with the same platform pointer ID.
    // Origins, total movement and deltas are logical pixels, not painting/device coordinates.
    internal virtual bool CanStartTouchScroll(Point origin, Point movement)
    {
        if (!AutoScroll) return false;
        // Small orthogonal finger jitter must not let a horizontal child capture a
        // predominantly vertical pull. Subsequent deltas still use normal scrollbar logic.
        bool horizontal = Math.Abs((long)movement.X) >= Math.Abs((long)movement.Y);
        return horizontal
            ? hscrollbar.Visible && (movement.X > 0 && hscrollbar.Value > hscrollbar.Minimum ||
                                    movement.X < 0 && hscrollbar.Value < hscrollbar.Maximum)
            : vscrollbar.Visible && (movement.Y > 0 && vscrollbar.Value > vscrollbar.Minimum ||
                                    movement.Y < 0 && vscrollbar.Value < vscrollbar.Maximum);
    }

    internal virtual void BeginTouchScroll(object gesture, Point origin, Point movement) { }

    internal virtual bool ContinueTouchScroll(object gesture, Point movement, Point delta)
        => ScrollByTouchDelta(delta);

    internal virtual void EndTouchScroll(object gesture, bool canceled) { }
}
