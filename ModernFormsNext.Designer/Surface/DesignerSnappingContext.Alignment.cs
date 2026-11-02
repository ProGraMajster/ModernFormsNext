namespace ModernFormsNext.Designer.Surface;

internal sealed partial class DesignerSnappingContext
{
    private Candidate? Resolve(DesignerSnapBounds raw, bool horizontal, bool movingStart, bool movingEnd, float zoom)
    {
        if (invalidated || !ReferenceEquals(initialOptions, state.SnappingOptions) || !float.IsFinite(zoom) || zoom <= 0
            || !ReferenceEquals(themeFont, Theme.UIFont) || themeSize != Theme.FontSize
            || themeLineHeight != Theme.LineHeight || themeLetterSpacing != Theme.LetterSpacing)
        {
            horizontalLatch = verticalLatch = null;
            return null;
        }
        Candidate? best = null, retained = null;
        var latch = horizontal ? horizontalLatch : verticalLatch;
        AddRectangle(ref best, ref retained, raw, container, scopeClip, horizontal, movingStart, movingEnd,
            -1, DesignerSnapGuideKind.Edge, zoom, latch);
        double leading = horizontal ? paddedContainer.Left + (long)movingMargin.Left : paddedContainer.Top + (long)movingMargin.Top;
        double trailing = horizontal ? paddedContainer.Right - (long)movingMargin.Right : paddedContainer.Bottom - (long)movingMargin.Bottom;
        AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, 0, leading,
            -2, 0, DesignerSnapGuideKind.Padding, paddedContainer, zoom, latch);
        AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, 2, trailing,
            -2, 2, DesignerSnapGuideKind.Padding, paddedContainer, zoom, latch);
        foreach (var target in targets)
        {
            // A callback can edit a candidate after the transaction opens. A frozen rectangle
            // must never guide against that newer authored location.
            if (target.Node.Bounds != target.Authored) continue;
            AddRectangle(ref best, ref retained, raw, target.Bounds, target.Visible, horizontal, movingStart, movingEnd,
                target.Order, DesignerSnapGuideKind.Edge, zoom, latch);
            double after = horizontal ? target.Bounds.Right + (long)target.Margin.Right + movingMargin.Left
                : target.Bounds.Bottom + (long)target.Margin.Bottom + movingMargin.Top;
            double before = horizontal ? target.Bounds.Left - (long)target.Margin.Left - movingMargin.Right
                : target.Bounds.Top - (long)target.Margin.Top - movingMargin.Bottom;
            AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, 0, after,
                target.Order, 3, DesignerSnapGuideKind.Margin, target.Bounds, zoom, latch);
            AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, 2, before,
                target.Order, 4, DesignerSnapGuideKind.Margin, target.Bounds, zoom, latch);
        }
        if (movingStart && movingEnd)
        {
            AddSpacing(ref best, ref retained, raw, horizontal, zoom, latch);
            if (!horizontal) AddBaselines(ref best, ref retained, raw, zoom, latch);
        }
        var result = retained ?? best;
        if (horizontal) horizontalLatch = result?.Key;
        else verticalLatch = result?.Key;
        return result;
    }

    private void AddBaselines(ref Candidate? best, ref Candidate? retained, DesignerSnapBounds raw, float zoom, CandidateKey? latch)
    {
        if (state.ControlRenderMode != DesignerControlRenderMode.Runtime) return;
        if (primaryText?.GetBaseline() is not { } original) return;
        double moving = original + raw.Top - start.Top;
        foreach (var target in targets)
        {
            if (target.Node.Bounds != target.Authored || moving < target.Visible.Top - ReleaseDistance / zoom
                || moving > target.Visible.Bottom + ReleaseDistance / zoom || target.GetBaseline() is not { } baseline
                || baseline < target.Visible.Top || baseline > target.Visible.Bottom) continue;
            double correction = baseline - moving;
            var guide = CreateGuide(false, baseline, Math.Min(raw.Left, target.Bounds.Left),
                Math.Max(raw.Right, target.Bounds.Right), DesignerSnapGuideKind.Baseline);
            if (guide is null) continue;
            Consider(ref best, ref retained, new(new(target.Order, 3, 3, DesignerSnapGuideKind.Baseline),
                (long)Math.Round(correction, MidpointRounding.AwayFromZero), Math.Abs(correction) * zoom,
                guide.Value), latch);
        }
    }

    private void AddRectangle(ref Candidate? best, ref Candidate? retained, DesignerSnapBounds raw,
        DesignerSnapBounds target, DesignerSnapBounds visible, bool horizontal, bool movingStart, bool movingEnd, int order,
        DesignerSnapGuideKind kind, float zoom, CandidateKey? latch)
    {
        double first = horizontal ? target.Left : target.Top;
        double last = horizontal ? target.Right : target.Bottom;
        double minimum = horizontal ? visible.Left : visible.Top, maximum = horizontal ? visible.Right : visible.Bottom;
        for (int anchor = 0; anchor <= 2; anchor += 2)
        {
            if (first >= minimum && first <= maximum)
                AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, anchor, first,
                    order, 0, kind, target, zoom, latch);
            if (last >= minimum && last <= maximum)
                AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, anchor, last,
                    order, 2, kind, target, zoom, latch);
        }
        double center = horizontal ? target.CenterX : target.CenterY;
        if (center >= minimum && center <= maximum)
            AddAnchor(ref best, ref retained, raw, horizontal, movingStart, movingEnd, 1,
                center, order, 1, DesignerSnapGuideKind.Center, target, zoom, latch);
    }

    private void AddAnchor(ref Candidate? best, ref Candidate? retained, DesignerSnapBounds raw,
        bool horizontal, bool movingStart, bool movingEnd, int anchor, double coordinate, int owner, int targetAnchor,
        DesignerSnapGuideKind kind, DesignerSnapBounds target, float zoom, CandidateKey? latch)
    {
        if (anchor == 0 && !movingStart || anchor == 2 && !movingEnd) return;
        double source = anchor switch
        {
            0 => horizontal ? raw.Left : raw.Top,
            2 => horizontal ? raw.Right : raw.Bottom,
            _ => horizontal ? raw.CenterX : raw.CenterY
        };
        double correction = coordinate - source;
        // Moving either resize edge changes the center by half as much; preserve the other edge.
        if (anchor == 1 && !(movingStart && movingEnd)) correction *= 2;
        var guide = CreateGuide(horizontal, coordinate,
            horizontal ? Math.Min(raw.Top, target.Top) : Math.Min(raw.Left, target.Left),
            horizontal ? Math.Max(raw.Bottom, target.Bottom) : Math.Max(raw.Right, target.Right), kind);
        if (guide is null) return;
        var value = new Candidate(new(owner, anchor, targetAnchor, kind),
            (long)Math.Round(correction, MidpointRounding.AwayFromZero), Math.Abs(correction) * zoom,
            guide.Value);
        Consider(ref best, ref retained, value, latch);
    }

    private DesignerSnapGuide? CreateGuide(bool vertical, double coordinate, double from, double to,
        DesignerSnapGuideKind kind, double? gap = null)
    {
        double leading = vertical ? scopeClip.Left : scopeClip.Top, trailing = vertical ? scopeClip.Right : scopeClip.Bottom;
        if (coordinate < leading || coordinate > trailing) return null;
        from = Math.Max(from, vertical ? scopeClip.Top : scopeClip.Left);
        to = Math.Min(to, vertical ? scopeClip.Bottom : scopeClip.Right);
        return to < from ? null : new DesignerSnapGuide(vertical, coordinate, from, to, kind, gap);
    }

    private static void Consider(ref Candidate? best, ref Candidate? retained, Candidate candidate, CandidateKey? latch)
    {
        if (candidate.Key == latch && candidate.Distance <= ReleaseDistance) retained = candidate;
        if (candidate.Distance > AcquireDistance) return;
        if (best is not { } previous || candidate.Distance < previous.Distance - 0.000001
            || Math.Abs(candidate.Distance - previous.Distance) < 0.000001 && Compare(candidate.Key, previous.Key) < 0)
            best = candidate;
    }

    private static int Compare(CandidateKey first, CandidateKey second)
    {
        int Priority(DesignerSnapGuideKind kind) => kind switch
        {
            DesignerSnapGuideKind.Baseline => 0, DesignerSnapGuideKind.Padding => 1,
            DesignerSnapGuideKind.Edge => 2, DesignerSnapGuideKind.Center => 3,
            DesignerSnapGuideKind.Margin => 4, _ => 5
        };
        int result = Priority(first.Kind).CompareTo(Priority(second.Kind));
        if (result == 0) result = first.Owner.CompareTo(second.Owner);
        if (result == 0) result = first.MovingAnchor.CompareTo(second.MovingAnchor);
        return result == 0 ? first.TargetAnchor.CompareTo(second.TargetAnchor) : result;
    }
}
