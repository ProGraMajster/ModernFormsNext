using ModernFormsNext;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerLayoutEngine
{
    public DesignerLayoutResult Layout(DesignDocument document, DesignerPreviewResourcePolicy? resourcePolicy = null)
        => Layout(document, document.Size, resourcePolicy);

    public DesignerLayoutResult Layout(DesignDocument document, DesignSize rootSize, DesignerPreviewResourcePolicy? resourcePolicy = null)
    {
        var validation = new DesignDocumentValidator().ValidateStructure(document);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(document));
        using var performance = PerformanceRecorder.Measure(PerformanceActivityKind.DesignerLayout);
        var normalizedRootSize = new DesignSize(Math.Max(1, rootSize.Width), Math.Max(1, rootSize.Height));
        var baseline = normalizedRootSize == document.Size
            ? null
            : LayoutCore(document, document.Size, baseline: null, resourcePolicy ?? DesignerPreviewResourcePolicy.Default);
        var result = LayoutCore(document, normalizedRootSize, baseline, resourcePolicy ?? DesignerPreviewResourcePolicy.Default);
        performance.Complete();
        return result;
    }

    private static DesignerLayoutResult LayoutCore(
        DesignDocument document,
        DesignSize rootSize,
        DesignerLayoutResult? baseline,
        DesignerPreviewResourcePolicy resourcePolicy)
    {
        // This projection-wide budget also bounds the combined work of sibling tables.
        // The fallback pass below only visits authored nodes and never materializes strips.
        DesignerTablePreviewAdmission.TryValidate(document.Controls, resourcePolicy, out string? tableError);
        var bounds = new Dictionary<DesignControlNode, DesignBounds>();
        var documentBounds = new DesignBounds(0, 0, rootSize.Width, rootSize.Height);
        var documentContentBounds = document.RootKind == DesignRootKind.UserControl
            ? DesignerLayoutProperties.GetPaddedContentBounds(documentBounds, DesignerLayoutProperties.GetPadding(document.Properties))
            : documentBounds;

        LayoutGenericChildren(document.Controls, documentBounds, documentContentBounds, document.Size, bounds, baseline);
        var visibleBounds = new Dictionary<DesignControlNode, DesignBounds>();
        var diagnostics = new Dictionary<DesignControlNode, string>();
        CaptureRuntimeContainers(document.Controls, bounds, visibleBounds, diagnostics,
            ResolveDirection(document.Properties, RightToLeft.No), resourcePolicy, tableError);
        return new DesignerLayoutResult(bounds, visibleBounds, diagnostics);
    }

    private static void CaptureRuntimeContainers(IEnumerable<DesignControlNode> nodes, IDictionary<DesignControlNode, DesignBounds> bounds,
        IDictionary<DesignControlNode, DesignBounds> visibleBounds, IDictionary<DesignControlNode, string> diagnostics,
        RightToLeft? inheritedDirection, DesignerPreviewResourcePolicy resourcePolicy, string? tableError)
    {
        foreach (var node in nodes)
        {
            RightToLeft? direction = ResolveDirection(node.Properties, inheritedDirection);
            if (!DesignerPagePreview.IsRuntimeLayoutContainer(node))
            {
                CaptureRuntimeContainers(node.Children, bounds, visibleBounds, diagnostics, direction, resourcePolicy, tableError);
                continue;
            }
            var area = bounds[node];
            string? error = tableError ?? "Invalid inherited RightToLeft selection.";
            using var preview = tableError is null && direction is { } effective
                ? DesignerPagePreview.TryCreate(node, new System.Drawing.Size(area.Width, area.Height), out error, effective, resourcePolicy)
                : null;
            if (preview is null)
            {
                diagnostics[node] = error!;
                // Only the diagnostic placeholder is painted. Its unavailable descendants
                // must not become invisible hit/drop targets from generic fallback geometry.
                var pending = new Stack<DesignControlNode>(node.Children);
                while (pending.TryPop(out var child))
                {
                    visibleBounds[child] = new DesignBounds(0, 0, 0, 0);
                    foreach (var descendant in child.Children) pending.Push(descendant);
                }
                continue;
            }
            preview.Capture(new System.Drawing.Point(area.X, area.Y), bounds, visibleBounds);
        }
    }

    // Resolve authored data only. An explicit boundary can recover from an invalid ancestor;
    // inherited invalid data produces a diagnostic instead of activating an arbitrary enum type.
    private static RightToLeft? ResolveDirection(IReadOnlyDictionary<string, DesignPropertyValue> properties,
        RightToLeft? inheritedDirection)
    {
        if (!properties.TryGetValue(nameof(Control.RightToLeft), out var value)) return inheritedDirection;
        if (!DesignerLayoutProperties.TryReadRightToLeft(value, out RightToLeft selection)) return null;
        return selection == RightToLeft.Inherit ? inheritedDirection : selection;
    }

    /// <summary>
    /// Applies a design-root resize while preserving the runtime Anchor result in persisted bounds.
    /// </summary>
    /// <remarks>
    /// The document stores the final authored geometry that generated code will assign at startup.
    /// Calculate against the old root size first, then persist only non-docked bounds; docked
    /// controls retain their authored thickness and are recalculated by the runtime layout engine.
    /// </remarks>
    public void ResizeRoot(DesignDocument document, DesignSize rootSize, DesignerPreviewResourcePolicy? resourcePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalizedSize = new DesignSize(Math.Max(1, rootSize.Width), Math.Max(1, rootSize.Height));
        if (document.Size == normalizedSize)
            return;

        var layout = Layout(document, normalizedSize, resourcePolicy);
        PersistAnchoredBounds(document.Controls, parentBounds: default, parentNode: null, layout);
        document.Size = normalizedSize;
    }

    private static void LayoutGenericChildren(
        DesignControlCollection children,
        DesignBounds parentBounds,
        DesignBounds parentContentBounds,
        DesignSize parentDesignSize,
        IDictionary<DesignControlNode, DesignBounds> bounds,
        DesignerLayoutResult? baseline)
    {
        var remaining = new DesignBounds(
            parentContentBounds.X - parentBounds.X,
            parentContentBounds.Y - parentBounds.Y,
            Math.Max(0, parentContentBounds.Width),
            Math.Max(0, parentContentBounds.Height));

        // The persisted collection is front-to-back. As at runtime, the front-most child consumes
        // dock space first; authored X/Y values are ignored for docked controls while thickness is
        // taken from Height (Top/Bottom) or Width (Left/Right).
        foreach (var child in children)
        {
            var participatesInLayout = DesignerLayoutProperties.IsVisible(child);
            var localBounds = participatesInLayout
                ? GetLocalBounds(child, remaining, parentBounds, parentDesignSize)
                : DesignerLayoutProperties.ApplySizeConstraints(child, child.Bounds);
            var absoluteBounds = new DesignBounds(
                parentBounds.X + localBounds.X,
                parentBounds.Y + localBounds.Y,
                Math.Max(0, localBounds.Width),
                Math.Max(0, localBounds.Height));

            bounds[child] = absoluteBounds;
            if (participatesInLayout)
                remaining = ConsumeDockSpace(child, remaining, localBounds);

            LayoutContainerChildren(child, absoluteBounds, bounds, baseline);
        }
    }

    private static void LayoutContainerChildren(
        DesignControlNode container,
        DesignBounds containerBounds,
        IDictionary<DesignControlNode, DesignBounds> bounds,
        DesignerLayoutResult? baseline)
    {
        DesignerSpecialContainers.EnsureSpecialChildren(container);
        var contentBounds = DesignerLayoutProperties.GetContainerContentBounds(container, containerBounds);

        if (DesignerSpecialContainers.IsSplitContainer(container))
        {
            LayoutSplitContainer(container, contentBounds, bounds, baseline);
            return;
        }

        if (DesignerSpecialContainers.IsTabControl(container))
        {
            LayoutTabControl(container, contentBounds, bounds, baseline);
            return;
        }

        // Flow/Table bounds are captured once through the production engine below.
        // The generic pass only seeds authored geometry: it never allocates strip arrays
        // or scans empty cells, including for a deferred, very large table preview.
        if (container.Children.Count > 0)
            LayoutGenericChildren(
                container.Children,
                containerBounds,
                contentBounds,
                GetBaselineContainerSize(container, containerBounds, baseline),
                bounds,
                baseline);
    }

    private static void LayoutSplitContainer(
        DesignControlNode splitContainer,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds,
        DesignerLayoutResult? baseline)
    {
        var panel1 = splitContainer.Children.FirstOrDefault(DesignerSpecialContainers.IsSplitPanel1);
        var panel2 = splitContainer.Children.FirstOrDefault(DesignerSpecialContainers.IsSplitPanel2);

        if (panel1 is null || panel2 is null)
            return;

        var orientation = DesignerSpecialContainers.GetEnum(splitContainer, DesignerSpecialContainers.OrientationPropertyName, Orientation.Horizontal);
        var splitterWidth = Math.Max(1, DesignerSpecialContainers.GetInt(splitContainer, DesignerSpecialContainers.SplitterWidthPropertyName, 5));
        var panel1Minimum = Math.Max(0, DesignerSpecialContainers.GetInt(splitContainer, "Panel1MinimumSize", 25));
        var panel2Minimum = Math.Max(0, DesignerSpecialContainers.GetInt(splitContainer, "Panel2MinimumSize", 25));
        var panel1DesignSize = GetDesignSize(panel1);
        var panel2DesignSize = GetDesignSize(panel2);

        if (orientation == Orientation.Horizontal)
        {
            var maximum = Math.Max(panel1Minimum, bounds.Width - splitterWidth - panel2Minimum);
            var distance = Math.Clamp(
                DesignerSpecialContainers.GetInt(splitContainer, DesignerSpecialContainers.SplitterDistancePropertyName, Math.Max(panel1Minimum, bounds.Width / 2)),
                panel1Minimum,
                maximum);
            var panel1Bounds = new DesignBounds(bounds.X, bounds.Y, Math.Max(0, distance), bounds.Height);
            var panel2X = bounds.X + distance + splitterWidth;
            var panel2Bounds = new DesignBounds(panel2X, bounds.Y, Math.Max(0, bounds.Right - panel2X), bounds.Height);

            effectiveBounds[panel1] = panel1Bounds;
            effectiveBounds[panel2] = panel2Bounds;
            panel1.Bounds = new DesignBounds(0, 0, panel1Bounds.Width, panel1Bounds.Height);
            panel2.Bounds = new DesignBounds(distance + splitterWidth, 0, panel2Bounds.Width, panel2Bounds.Height);
            LayoutGenericChildren(
                panel1.Children,
                panel1Bounds,
                DesignerLayoutProperties.GetContainerContentBounds(panel1, panel1Bounds),
                GetBaselineContainerSize(panel1, panel1Bounds, baseline, panel1DesignSize),
                effectiveBounds,
                baseline);
            LayoutGenericChildren(
                panel2.Children,
                panel2Bounds,
                DesignerLayoutProperties.GetContainerContentBounds(panel2, panel2Bounds),
                GetBaselineContainerSize(panel2, panel2Bounds, baseline, panel2DesignSize),
                effectiveBounds,
                baseline);
        }
        else
        {
            var maximum = Math.Max(panel1Minimum, bounds.Height - splitterWidth - panel2Minimum);
            var distance = Math.Clamp(
                DesignerSpecialContainers.GetInt(splitContainer, DesignerSpecialContainers.SplitterDistancePropertyName, Math.Max(panel1Minimum, bounds.Height / 2)),
                panel1Minimum,
                maximum);
            var panel1Bounds = new DesignBounds(bounds.X, bounds.Y, bounds.Width, Math.Max(0, distance));
            var panel2Y = bounds.Y + distance + splitterWidth;
            var panel2Bounds = new DesignBounds(bounds.X, panel2Y, bounds.Width, Math.Max(0, bounds.Bottom - panel2Y));

            effectiveBounds[panel1] = panel1Bounds;
            effectiveBounds[panel2] = panel2Bounds;
            panel1.Bounds = new DesignBounds(0, 0, panel1Bounds.Width, panel1Bounds.Height);
            panel2.Bounds = new DesignBounds(0, distance + splitterWidth, panel2Bounds.Width, panel2Bounds.Height);
            LayoutGenericChildren(
                panel1.Children,
                panel1Bounds,
                DesignerLayoutProperties.GetContainerContentBounds(panel1, panel1Bounds),
                GetBaselineContainerSize(panel1, panel1Bounds, baseline, panel1DesignSize),
                effectiveBounds,
                baseline);
            LayoutGenericChildren(
                panel2.Children,
                panel2Bounds,
                DesignerLayoutProperties.GetContainerContentBounds(panel2, panel2Bounds),
                GetBaselineContainerSize(panel2, panel2Bounds, baseline, panel2DesignSize),
                effectiveBounds,
                baseline);
        }
    }

    private static void LayoutTabControl(
        DesignControlNode tabControl,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds,
        DesignerLayoutResult? baseline)
    {
        var headerHeight = Math.Min(28, Math.Max(20, bounds.Height / 4));
        var pageBounds = new DesignBounds(
            bounds.X + 2,
            bounds.Y + headerHeight,
            Math.Max(0, bounds.Width - 4),
            Math.Max(0, bounds.Height - headerHeight - 2));
        var pageDesignSizes = tabControl.Children
            .Where(DesignerSpecialContainers.IsTabPage)
            .ToDictionary(page => page, GetDesignSize);

        foreach (var page in pageDesignSizes.Keys)
        {
            effectiveBounds[page] = pageBounds;
            page.Bounds = new DesignBounds(0, headerHeight, pageBounds.Width, pageBounds.Height);
        }

        if (DesignerSpecialContainers.GetSelectedTabPage(tabControl) is { } selectedPage)
            LayoutGenericChildren(
                selectedPage.Children,
                pageBounds,
                DesignerLayoutProperties.GetContainerContentBounds(selectedPage, pageBounds),
                GetBaselineContainerSize(selectedPage, pageBounds, baseline, pageDesignSizes[selectedPage]),
                effectiveBounds,
                baseline);
    }

    private static void SetEffectiveChildBounds(
        DesignControlNode child,
        DesignBounds parentBounds,
        int localX,
        int localY,
        int width,
        int height,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds,
        DesignerLayoutResult? baseline)
    {
        var absolute = new DesignBounds(parentBounds.X + localX, parentBounds.Y + localY, width, height);
        effectiveBounds[child] = absolute;
        LayoutContainerChildren(child, absolute, effectiveBounds, baseline);
    }

    private static DesignBounds GetLocalBounds(
        DesignControlNode node,
        DesignBounds remaining,
        DesignBounds parentBounds,
        DesignSize parentDesignSize)
    {
        var dock = DesignerLayoutProperties.GetDock(node);
        var width = Math.Max(0, node.Bounds.Width);
        var height = Math.Max(0, node.Bounds.Height);

        if (dock != DockStyle.None)
        {
            var docked = dock switch
            {
                DockStyle.Top => new DesignBounds(remaining.X, remaining.Y, remaining.Width, Math.Min(height, remaining.Height)),
                DockStyle.Bottom => new DesignBounds(remaining.X, remaining.Bottom - Math.Min(height, remaining.Height), remaining.Width, Math.Min(height, remaining.Height)),
                DockStyle.Left => new DesignBounds(remaining.X, remaining.Y, Math.Min(width, remaining.Width), remaining.Height),
                DockStyle.Right => new DesignBounds(remaining.Right - Math.Min(width, remaining.Width), remaining.Y, Math.Min(width, remaining.Width), remaining.Height),
                DockStyle.Fill => remaining,
                _ => node.Bounds
            };

            return DesignerLayoutProperties.ApplySizeConstraints(node, docked);
        }

        var anchor = DesignerLayoutProperties.GetAnchor(node);
        var x = node.Bounds.X;
        var y = node.Bounds.Y;
        // Runtime anchor distances are initialized from DisplayRectangle, so a constant padding
        // value cancels from the resize delta. Authored non-docked coordinates remain relative to
        // the outer parent bounds; only Dock consumes the padded content rectangle above.
        var widthDelta = parentBounds.Width - Math.Max(1, parentDesignSize.Width);
        var heightDelta = parentBounds.Height - Math.Max(1, parentDesignSize.Height);
        var anchoredLeft = (anchor & AnchorStyles.Left) != 0;
        var anchoredRight = (anchor & AnchorStyles.Right) != 0;
        var anchoredTop = (anchor & AnchorStyles.Top) != 0;
        var anchoredBottom = (anchor & AnchorStyles.Bottom) != 0;

        if (anchoredLeft && anchoredRight)
            width = Math.Max(0, width + widthDelta);
        else if (!anchoredLeft && anchoredRight)
            x += widthDelta;
        else if (!anchoredLeft)
            x += widthDelta / 2;

        if (anchoredTop && anchoredBottom)
            height = Math.Max(0, height + heightDelta);
        else if (!anchoredTop && anchoredBottom)
            y += heightDelta;
        else if (!anchoredTop)
            y += heightDelta / 2;

        return DesignerLayoutProperties.ApplySizeConstraints(node, new DesignBounds(x, y, width, height));
    }

    private static DesignSize GetDesignSize(DesignControlNode node)
        => new(Math.Max(1, node.Bounds.Width), Math.Max(1, node.Bounds.Height));

    private static DesignSize GetBaselineContainerSize(
        DesignControlNode container,
        DesignBounds currentBounds,
        DesignerLayoutResult? baseline,
        DesignSize? fallback = null)
    {
        if (baseline is null)
        {
            return fallback ?? new DesignSize(
                Math.Max(1, currentBounds.Width),
                Math.Max(1, currentBounds.Height));
        }

        var baselineBounds = baseline.GetEffectiveBounds(container);
        return new DesignSize(Math.Max(1, baselineBounds.Width), Math.Max(1, baselineBounds.Height));
    }

    private static void PersistAnchoredBounds(
        DesignControlCollection children,
        DesignBounds parentBounds,
        DesignControlNode? parentNode,
        DesignerLayoutResult layout)
    {
        var parentOwnsChildPlacement = parentNode is null
            || (!DesignerSpecialContainers.IsFlowLayoutPanel(parentNode)
                && !DesignerSpecialContainers.IsTableLayoutPanel(parentNode)
                && !DesignerSpecialContainers.IsSplitContainer(parentNode)
                && !DesignerSpecialContainers.IsTabControl(parentNode));

        foreach (var child in children)
        {
            var absoluteBounds = layout.GetEffectiveBounds(child);

            if (parentOwnsChildPlacement && !DesignerLayoutProperties.IsDocked(child))
            {
                child.Bounds = new DesignBounds(
                    absoluteBounds.X - parentBounds.X,
                    absoluteBounds.Y - parentBounds.Y,
                    absoluteBounds.Width,
                    absoluteBounds.Height);
            }

            PersistAnchoredBounds(child.Children, absoluteBounds, child, layout);
        }
    }

    private static DesignBounds ConsumeDockSpace(
        DesignControlNode node,
        DesignBounds remaining,
        DesignBounds usedBounds)
    {
        return DesignerLayoutProperties.GetDock(node) switch
        {
            DockStyle.Top => new DesignBounds(remaining.X, remaining.Y + usedBounds.Height, remaining.Width, Math.Max(0, remaining.Height - usedBounds.Height)),
            DockStyle.Bottom => new DesignBounds(remaining.X, remaining.Y, remaining.Width, Math.Max(0, remaining.Height - usedBounds.Height)),
            DockStyle.Left => new DesignBounds(remaining.X + usedBounds.Width, remaining.Y, Math.Max(0, remaining.Width - usedBounds.Width), remaining.Height),
            DockStyle.Right => new DesignBounds(remaining.X, remaining.Y, Math.Max(0, remaining.Width - usedBounds.Width), remaining.Height),
            // Fill uses the current remaining rectangle but, like the production runtime engine,
            // does not consume it. A later front-to-back edge dock can still reserve space and a
            // subsequent layout pass will resize the fill control to the final remainder.
            DockStyle.Fill => remaining,
            _ => remaining
        };
    }
}
