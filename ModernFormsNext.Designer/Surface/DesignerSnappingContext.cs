using System.Drawing;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

/// <summary>Owns bounded, transient guide targets for one existing pointer transaction.</summary>
internal sealed partial class DesignerSnappingContext
{
    private const double AcquireDistance = 6;
    private const double ReleaseDistance = 10;
    private readonly DesignerSession state;
    private readonly DesignDocument document;
    private readonly DesignerSnappingOptions initialOptions;
    private readonly DesignerSnapBounds start;
    private readonly DesignerSnapBounds container;
    private readonly DesignerSnapBounds paddedContainer;
    private readonly DesignerSnapBounds scopeClip;
    private readonly Padding movingMargin;
    private readonly Target[] targets;
    private readonly Target[] horizontalTargets;
    private readonly Target[] verticalTargets;
    private readonly Target? primaryText;
    private readonly SkiaSharp.SKTypeface themeFont = Theme.UIFont;
    private readonly double themeSize = Theme.FontSize;
    private readonly double themeLineHeight = Theme.LineHeight;
    private readonly double themeLetterSpacing = Theme.LetterSpacing;
    private readonly List<DesignerSnapGuide> guides = new(6);
    private CandidateKey? horizontalLatch;
    private CandidateKey? verticalLatch;
    private bool invalidated;

    internal IReadOnlyList<DesignerSnapGuide> Guides => guides;
    internal DesignerSnapBounds StartBounds => start;

    private DesignerSnappingContext(DesignerSession state, DesignerSnapBounds start, DesignerSnapBounds container,
        DesignerSnapBounds paddedContainer, DesignerSnapBounds scopeClip, Padding movingMargin, Target[] targets, Target? primaryText)
    {
        this.state = state;
        document = state.Document;
        initialOptions = state.SnappingOptions;
        this.start = start;
        this.container = container;
        this.paddedContainer = paddedContainer;
        this.scopeClip = scopeClip;
        this.movingMargin = movingMargin;
        this.targets = targets;
        this.primaryText = primaryText;
        horizontalTargets = targets.OrderBy(target => target.Bounds.Left).ThenBy(target => target.Order).ToArray();
        verticalTargets = targets.OrderBy(target => target.Bounds.Top).ThenBy(target => target.Order).ToArray();
    }

    internal static DesignerSnappingContext? Create(DesignerSession state, IReadOnlyList<DesignControlNode> roots)
    {
        if (!state.SnappingOptions.Enabled || roots.Count == 0
            || !new DesignDocumentValidator().ValidateStructure(state.Document).IsValid) return null;
        var parents = new Dictionary<DesignControlNode, DesignControlNode?>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(DesignControlNode Node, DesignControlNode? Parent)>();
        foreach (var node in state.Document.Controls) pending.Push((node, null));
        while (pending.TryPop(out var entry))
        {
            parents.Add(entry.Node, entry.Parent);
            foreach (var child in entry.Node.Children) pending.Push((child, entry.Node));
        }
        var excluded = new HashSet<DesignControlNode>(roots, ReferenceEqualityComparer.Instance);
        HashSet<DesignControlNode>? common = null;
        foreach (var root in roots)
        {
            if (!parents.TryGetValue(root, out var parent) || DesignerLayoutProperties.IsDocked(root)
                || DesignerSpecialContainers.IsSpecialGeneratedPart(root) || !IsManualParent(parent)) return null;
            var ancestors = new HashSet<DesignControlNode>(ReferenceEqualityComparer.Instance);
            while (parent is not null)
            {
                if (state.IsProjectUserControlType(parent.TypeName)) return null;
                ancestors.Add(parent);
                excluded.Add(parent);
                parent = parents[parent];
            }
            if (common is null) common = ancestors;
            else common.IntersectWith(ancestors);
        }
        DesignControlNode? owner = parents[roots[0]];
        while (owner is not null && !common!.Contains(owner)) owner = parents[owner];
        var layout = new DesignerLayoutEngine().Layout(state.Document, state.PreviewResourcePolicy);
        var visibleNodes = new HashSet<DesignControlNode>(new DesignerHitTestService(new DesignerCoordinateMapper())
            .HitTestMarquee(state, new(0, 0, state.Document.Size.Width, state.Document.Size.Height), layout), ReferenceEqualityComparer.Instance);
        if (roots.Any(root => !visibleNodes.Contains(root))) return null;
        var selected = DesignerSnapBounds.From(layout.GetEffectiveBounds(roots[0]));
        var outer = Inflate(selected, DesignerLayoutProperties.GetMargin(roots[0]));
        foreach (var node in roots.Skip(1))
        {
            var bounds = DesignerSnapBounds.From(layout.GetEffectiveBounds(node));
            selected = DesignerSnapBounds.Union(selected, bounds);
            outer = DesignerSnapBounds.Union(outer, Inflate(bounds, DesignerLayoutProperties.GetMargin(node)));
        }
        var box = owner is null ? new DesignBounds(0, 0, state.Document.Size.Width, state.Document.Size.Height)
            : layout.GetEffectiveBounds(owner);
        var padded = DesignerLayoutProperties.GetPaddedContentBounds(box,
            DesignerLayoutProperties.GetPadding(owner?.Properties ?? state.Document.Properties));
        var candidates = new List<Target>();
        int order = 0;
        foreach (var node in owner?.Children ?? state.Document.Controls)
        {
            int index = order++;
            if (excluded.Contains(node) || !visibleNodes.Contains(node)
                || DesignerSpecialContainers.IsSpecialGeneratedPart(node) || layout.PreviewDiagnostics.ContainsKey(node)) continue;
            var visible = layout.GetVisibleBounds(state.Document, node);
            if (visible.Width <= 0 || visible.Height <= 0) continue;
            candidates.Add(new Target(node, DesignerSnapBounds.From(layout.GetEffectiveBounds(node)),
                DesignerSnapBounds.From(visible), DesignerLayoutProperties.GetMargin(node), index));
        }
        var margins = new Padding((int)Math.Clamp(selected.Left - outer.Left, 0, int.MaxValue),
            (int)Math.Clamp(selected.Top - outer.Top, 0, int.MaxValue),
            (int)Math.Clamp(outer.Right - selected.Right, 0, int.MaxValue),
            (int)Math.Clamp(outer.Bottom - selected.Bottom, 0, int.MaxValue));
        Target? primary = state.SelectedNode is { } primaryNode
            ? new(primaryNode, DesignerSnapBounds.From(layout.GetEffectiveBounds(primaryNode)),
                DesignerSnapBounds.From(layout.GetVisibleBounds(state.Document, primaryNode)), Padding.Empty, -3) : null;
        var clip = owner is null ? box : layout.GetVisibleBounds(state.Document, owner);
        return new(state, selected, DesignerSnapBounds.From(box), DesignerSnapBounds.From(padded), DesignerSnapBounds.From(clip),
            margins, candidates.ToArray(), primary);
    }

    private static bool IsManualParent(DesignControlNode? parent)
        => parent is null || !(DesignerSpecialContainers.IsFlowLayoutPanel(parent)
            || DesignerSpecialContainers.IsTableLayoutPanel(parent) || DesignerSpecialContainers.IsTabControl(parent)
            || DesignPageContainers.IsPage(parent.TypeName) || DesignPageContainers.IsType(parent.TypeName, "PageHost"));

    private static DesignerSnapBounds Inflate(DesignerSnapBounds bounds, Padding margin)
        => new(bounds.Left - margin.Left, bounds.Top - margin.Top, bounds.Right + margin.Right, bounds.Bottom + margin.Bottom);

    internal void Invalidate()
    {
        invalidated = true;
        ClearGuides();
    }

    internal void ClearGuides()
    {
        guides.Clear();
        horizontalLatch = verticalLatch = null;
    }

    internal (long X, long Y) Move(long deltaX, long deltaY, float zoom, bool bypass)
    {
        guides.Clear();
        var options = state.SnappingOptions;
        if (bypass || !options.Enabled || !ReferenceEquals(document, state.Document))
        { ClearGuides(); return (deltaX, deltaY); }
        var raw = start.Translate(deltaX, deltaY);
        var x = Resolve(raw, horizontal: true, movingStart: true, movingEnd: true, zoom);
        var y = Resolve(raw, horizontal: false, movingStart: true, movingEnd: true, zoom);
        long dx = x?.Correction ?? (options.SnapToGrid ? GridCorrection(raw.Left, options.GridSize) : 0);
        long dy = y?.Correction ?? (options.SnapToGrid ? GridCorrection(raw.Top, options.GridSize) : 0);
        AddGuides(x); AddGuides(y);
        return (deltaX + dx, deltaY + dy);
    }

    internal DesignBounds Resize(DesignBounds local, DesignBounds original, DesignerResizeHandle handle, float zoom, bool bypass)
    {
        guides.Clear();
        var options = state.SnappingOptions;
        if (bypass || !options.Enabled || !ReferenceEquals(document, state.Document))
        { ClearGuides(); return local; }
        bool left = handle is DesignerResizeHandle.TopLeft or DesignerResizeHandle.Left or DesignerResizeHandle.BottomLeft;
        bool right = handle is DesignerResizeHandle.TopRight or DesignerResizeHandle.Right or DesignerResizeHandle.BottomRight;
        bool top = handle is DesignerResizeHandle.TopLeft or DesignerResizeHandle.Top or DesignerResizeHandle.TopRight;
        bool bottom = handle is DesignerResizeHandle.BottomLeft or DesignerResizeHandle.Bottom or DesignerResizeHandle.BottomRight;
        var raw = new DesignerSnapBounds(start.Left + (long)local.X - original.X, start.Top + (long)local.Y - original.Y,
            start.Left + (long)local.X - original.X + local.Width, start.Top + (long)local.Y - original.Y + local.Height);
        var x = left || right ? Resolve(raw, true, left, right, zoom) : null;
        var y = top || bottom ? Resolve(raw, false, top, bottom, zoom) : null;
        long dx = x?.Correction ?? (options.SnapToGrid && (left || right) ? GridCorrection(left ? raw.Left : raw.Right, options.GridSize) : 0);
        long dy = y?.Correction ?? (options.SnapToGrid && (top || bottom) ? GridCorrection(top ? raw.Top : raw.Bottom, options.GridSize) : 0);
        long l = local.X + (left ? dx : 0), t = local.Y + (top ? dy : 0);
        long r = (long)local.X + local.Width + (right ? dx : 0), b = (long)local.Y + local.Height + (bottom ? dy : 0);
        if (l < int.MinValue || t < int.MinValue || r > int.MaxValue || b > int.MaxValue
            || r - l < 8 || b - t < 8 || r - l > int.MaxValue || b - t > int.MaxValue)
        { ClearGuides(); return local; }
        AddGuides(x); AddGuides(y);
        return new((int)l, (int)t, (int)(r - l), (int)(b - t));
    }

    private static long GridCorrection(long coordinate, int step)
        => (long)Math.Round(coordinate / (double)step, MidpointRounding.AwayFromZero) * step - coordinate;

    private void AddGuides(Candidate? candidate)
    {
        if (candidate is not { } value) return;
        guides.Add(value.Guide);
        if (value.Secondary is { } second) guides.Add(second);
    }

    private sealed class Target(DesignControlNode node, DesignerSnapBounds bounds, DesignerSnapBounds visible, Padding margin, int order)
    {
        internal readonly DesignControlNode Node = node;
        internal readonly DesignBounds Authored = node.Bounds;
        internal readonly DesignerSnapBounds Bounds = bounds;
        internal readonly DesignerSnapBounds Visible = visible;
        internal readonly Padding Margin = margin;
        internal readonly int Order = order;
        private bool baselineMeasured;
        private double? baseline;

        internal double? GetBaseline()
        {
            if (!baselineMeasured)
            {
                baselineMeasured = true;
                if (DesignerTextBaseline.Measure(Node, Bounds) is { } offset) baseline = Bounds.Top + offset;
            }
            return baseline;
        }
    }

    private readonly record struct CandidateKey(int Owner, int MovingAnchor, int TargetAnchor, DesignerSnapGuideKind Kind);
    private readonly record struct Candidate(CandidateKey Key, long Correction, double Distance,
        DesignerSnapGuide Guide, DesignerSnapGuide? Secondary = null);
}
