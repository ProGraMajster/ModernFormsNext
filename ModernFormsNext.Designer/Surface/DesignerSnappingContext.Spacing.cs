namespace ModernFormsNext.Designer.Surface;

internal sealed partial class DesignerSnappingContext
{
    private void AddSpacing(ref Candidate? best, ref Candidate? retained, DesignerSnapBounds raw, bool horizontal,
        float zoom, CandidateKey? latch)
    {
        var moving = Inflate(raw, movingMargin);
        Target? previous = null, before = null, after = null;
        var sorted = horizontal ? horizontalTargets : verticalTargets;
        foreach (var target in sorted)
        {
            if (target.Node.Bounds != target.Authored) continue;
            var box = Inflate(target.Bounds, target.Margin);
            if (horizontal ? box.Bottom <= moving.Top || box.Top >= moving.Bottom
                : box.Right <= moving.Left || box.Left >= moving.Right) continue;
            long leading = horizontal ? box.Left : box.Top, trailing = horizontal ? box.Right : box.Bottom;
            long movingLeading = horizontal ? moving.Left : moving.Top, movingTrailing = horizontal ? moving.Right : moving.Bottom;
            if (trailing <= movingLeading && (before is null || trailing > (horizontal
                ? Inflate(before.Bounds, before.Margin).Right : Inflate(before.Bounds, before.Margin).Bottom))) before = target;
            if (leading >= movingTrailing && (after is null || leading < (horizontal
                ? Inflate(after.Bounds, after.Margin).Left : Inflate(after.Bounds, after.Margin).Top))) after = target;
            if (previous is not null)
            {
                var prior = Inflate(previous.Bounds, previous.Margin);
                long end = horizontal ? prior.Right : prior.Bottom;
                long gap = leading - end;
                if (gap >= 0)
                {
                    long extent = horizontal ? moving.Width : moving.Height;
                    AddGapCandidate(ref best, ref retained, raw, moving, horizontal, previous, target,
                        trailing + gap, 1, zoom, latch);
                    AddGapCandidate(ref best, ref retained, raw, moving, horizontal, previous, target,
                        (horizontal ? prior.Left : prior.Top) - gap - extent, 2, zoom, latch);
                }
            }
            previous = target;
        }
        if (before is not null && after is not null)
        {
            var first = Inflate(before.Bounds, before.Margin);
            var last = Inflate(after.Bounds, after.Margin);
            long end = horizontal ? first.Right : first.Bottom, begin = horizontal ? last.Left : last.Top;
            long extent = horizontal ? moving.Width : moving.Height;
            if (begin - end >= extent)
            {
                // Integer document positions allocate an odd remaining pixel to the trailing gap.
                long left = end + (begin - end - extent) / 2;
                AddGapCandidate(ref best, ref retained, raw, moving, horizontal, before, after, left, 0, zoom, latch);
            }
        }
    }

    private void AddGapCandidate(ref Candidate? best, ref Candidate? retained, DesignerSnapBounds raw,
        DesignerSnapBounds moving, bool horizontal, Target first, Target second, long desiredLeading, int mode,
        float zoom, CandidateKey? latch)
    {
        long correction = desiredLeading - (horizontal ? moving.Left : moving.Top);
        double distance = Math.Abs((double)correction) * zoom;
        if (distance > ReleaseDistance) return;
        var a = Inflate(first.Bounds, first.Margin);
        var b = Inflate(second.Bounds, second.Margin);
        var shifted = moving.Translate(horizontal ? correction : 0, horizontal ? 0 : correction);
        double firstFrom, firstTo, secondFrom, secondTo;
        if (mode == 0)
        {
            firstFrom = horizontal ? a.Right : a.Bottom;
            firstTo = horizontal ? shifted.Left : shifted.Top;
            secondFrom = horizontal ? shifted.Right : shifted.Bottom;
            secondTo = horizontal ? b.Left : b.Top;
        }
        else
        {
            firstFrom = horizontal ? a.Right : a.Bottom;
            firstTo = horizontal ? b.Left : b.Top;
            secondFrom = mode == 1 ? (horizontal ? b.Right : b.Bottom) : (horizontal ? shifted.Right : shifted.Bottom);
            secondTo = mode == 1 ? (horizontal ? shifted.Left : shifted.Top) : (horizontal ? a.Left : a.Top);
        }
        double line = horizontal ? raw.CenterY : raw.CenterX;
        var kind = DesignerSnapGuideKind.Spacing;
        var referenceGuide = CreateGuide(!horizontal, line, firstFrom, firstTo, kind, firstTo - firstFrom);
        var movingGuide = CreateGuide(!horizontal, line, secondFrom, secondTo, kind, secondTo - secondFrom);
        if (referenceGuide is null || movingGuide is null) return;
        Consider(ref best, ref retained, new(new(first.Order * (Designing.DesignDocumentValidator.MaximumNodeCount + 1) + second.Order,
            0, mode, kind), correction, distance,
            referenceGuide.Value, movingGuide.Value), latch);
    }
}
