using ModernFormsNext;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerLayoutEngine
{
    public DesignerLayoutResult Layout(DesignDocument document)
        => Layout(document, document.Size);

    public DesignerLayoutResult Layout(DesignDocument document, DesignSize rootSize)
    {
        var normalizedRootSize = new DesignSize(Math.Max(1, rootSize.Width), Math.Max(1, rootSize.Height));
        var baseline = normalizedRootSize == document.Size
            ? null
            : LayoutCore(document, document.Size, baseline: null);
        return LayoutCore(document, normalizedRootSize, baseline);
    }

    private static DesignerLayoutResult LayoutCore(
        DesignDocument document,
        DesignSize rootSize,
        DesignerLayoutResult? baseline)
    {
        var bounds = new Dictionary<DesignControlNode, DesignBounds>();
        var documentBounds = new DesignBounds(0, 0, rootSize.Width, rootSize.Height);
        var documentContentBounds = document.RootKind == DesignRootKind.UserControl
            ? DesignerLayoutProperties.GetPaddedContentBounds(documentBounds, DesignerLayoutProperties.GetPadding(document.Properties))
            : documentBounds;

        LayoutGenericChildren(document.Controls, documentBounds, documentContentBounds, document.Size, bounds, baseline);

        return new DesignerLayoutResult(bounds);
    }

    /// <summary>
    /// Applies a design-root resize while preserving the runtime Anchor result in persisted bounds.
    /// </summary>
    /// <remarks>
    /// The document stores the final authored geometry that generated code will assign at startup.
    /// Calculate against the old root size first, then persist only non-docked bounds; docked
    /// controls retain their authored thickness and are recalculated by the runtime layout engine.
    /// </remarks>
    public void ResizeRoot(DesignDocument document, DesignSize rootSize)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalizedSize = new DesignSize(Math.Max(1, rootSize.Width), Math.Max(1, rootSize.Height));
        if (document.Size == normalizedSize)
            return;

        var layout = Layout(document, normalizedSize);
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

        if (DesignerSpecialContainers.IsFlowLayoutPanel(container))
        {
            LayoutFlowLayoutPanel(container, contentBounds, bounds, baseline);
            return;
        }

        if (DesignerSpecialContainers.IsTableLayoutPanel(container))
        {
            LayoutTableLayoutPanel(container, contentBounds, bounds, baseline);
            return;
        }

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

    private static void LayoutFlowLayoutPanel(
        DesignControlNode panel,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds,
        DesignerLayoutResult? baseline)
    {
        var direction = DesignerSpecialContainers.GetEnum(panel, DesignerSpecialContainers.FlowDirectionPropertyName, FlowDirection.LeftToRight);
        var wrap = DesignerSpecialContainers.GetBoolean(panel, DesignerSpecialContainers.WrapContentsPropertyName, true);
        var cursorX = 0;
        var cursorY = 0;
        var lineSize = 0;

        foreach (var child in panel.Children)
        {
            if (!DesignerLayoutProperties.IsVisible(child))
            {
                SetEffectiveChildBounds(child, bounds, child.Bounds.X, child.Bounds.Y, child.Bounds.Width, child.Bounds.Height, effectiveBounds, baseline);
                continue;
            }

            var constrained = DesignerLayoutProperties.ApplySizeConstraints(child, child.Bounds);
            var margin = DesignerLayoutProperties.GetMargin(child);
            var width = Math.Max(1, constrained.Width);
            var height = Math.Max(1, constrained.Height);
            var requiredWidth = width + margin.Horizontal;
            var requiredHeight = height + margin.Vertical;

            if (direction is FlowDirection.LeftToRight or FlowDirection.RightToLeft)
            {
                if (wrap && cursorX > 0 && cursorX + requiredWidth > bounds.Width)
                {
                    cursorX = 0;
                    cursorY += lineSize;
                    lineSize = 0;
                }

                var localX = direction == FlowDirection.LeftToRight
                    ? cursorX + margin.Left
                    : Math.Max(0, bounds.Width - cursorX - margin.Right - width);
                SetEffectiveChildBounds(child, bounds, localX, cursorY + margin.Top, width, height, effectiveBounds, baseline);
                cursorX += requiredWidth;
                lineSize = Math.Max(lineSize, requiredHeight);
            }
            else
            {
                if (wrap && cursorY > 0 && cursorY + requiredHeight > bounds.Height)
                {
                    cursorY = 0;
                    cursorX += lineSize;
                    lineSize = 0;
                }

                var localY = direction == FlowDirection.TopDown
                    ? cursorY + margin.Top
                    : Math.Max(0, bounds.Height - cursorY - margin.Bottom - height);
                SetEffectiveChildBounds(child, bounds, cursorX + margin.Left, localY, width, height, effectiveBounds, baseline);
                cursorY += requiredHeight;
                lineSize = Math.Max(lineSize, requiredWidth);
            }
        }
    }

    private static void LayoutTableLayoutPanel(
        DesignControlNode panel,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds,
        DesignerLayoutResult? baseline)
    {
        var columns = Math.Max(1, DesignerSpecialContainers.GetInt(panel, DesignerSpecialContainers.ColumnCountPropertyName, 2));
        var rows = Math.Max(1, DesignerSpecialContainers.GetInt(panel, DesignerSpecialContainers.RowCountPropertyName, 2));
        var columnWidths = new int[columns];
        var rowHeights = new int[rows];
        var childIndex = 0;

        // The runtime's default TableLayoutStyle is AutoSize. Size each strip from the
        // authored child plus Margin; unallocated space remains after the final auto strip.
        foreach (var child in panel.Children.Where(DesignerLayoutProperties.IsVisible))
        {
            var column = Math.Clamp(
                DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnPropertyName, childIndex % columns),
                0,
                columns - 1);
            var row = Math.Clamp(
                DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowPropertyName, childIndex / columns),
                0,
                rows - 1);
            var columnSpan = Math.Min(
                Math.Max(1, DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnSpanPropertyName, 1)),
                columns - column);
            var rowSpan = Math.Min(
                Math.Max(1, DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowSpanPropertyName, 1)),
                rows - row);
            var constrained = DesignerLayoutProperties.ApplySizeConstraints(child, child.Bounds);
            var margin = DesignerLayoutProperties.GetMargin(child);
            var perColumn = Math.Max(1, (int)Math.Ceiling((constrained.Width + margin.Horizontal) / (double)columnSpan));
            var perRow = Math.Max(1, (int)Math.Ceiling((constrained.Height + margin.Vertical) / (double)rowSpan));

            for (var index = column; index < column + columnSpan; index++)
                columnWidths[index] = Math.Max(columnWidths[index], perColumn);
            for (var index = row; index < row + rowSpan; index++)
                rowHeights[index] = Math.Max(rowHeights[index], perRow);
            childIndex++;
        }

        childIndex = 0;

        foreach (var child in panel.Children)
        {
            if (!DesignerLayoutProperties.IsVisible(child))
            {
                SetEffectiveChildBounds(child, bounds, child.Bounds.X, child.Bounds.Y, child.Bounds.Width, child.Bounds.Height, effectiveBounds, baseline);
                childIndex++;
                continue;
            }

            var column = DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnPropertyName, childIndex % columns);
            var row = DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowPropertyName, childIndex / columns);
            var columnSpan = Math.Max(1, DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnSpanPropertyName, 1));
            var rowSpan = Math.Max(1, DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowSpanPropertyName, 1));

            column = Math.Clamp(column, 0, columns - 1);
            row = Math.Clamp(row, 0, rows - 1);
            columnSpan = Math.Min(columnSpan, columns - column);
            rowSpan = Math.Min(rowSpan, rows - row);

            var cellX = columnWidths.Take(column).Sum();
            var cellY = rowHeights.Take(row).Sum();
            var cellWidth = columnWidths.Skip(column).Take(columnSpan).Sum();
            var cellHeight = rowHeights.Skip(row).Take(rowSpan).Sum();
            var margin = DesignerLayoutProperties.GetMargin(child);
            var available = new DesignBounds(
                cellX + margin.Left,
                cellY + margin.Top,
                Math.Max(0, cellWidth - margin.Horizontal),
                Math.Max(0, cellHeight - margin.Vertical));
            var constrained = DesignerLayoutProperties.ApplySizeConstraints(child, child.Bounds);
            var anchor = GetUnifiedTableAnchor(child);
            var stretchesHorizontally = (anchor & (AnchorStyles.Left | AnchorStyles.Right)) == (AnchorStyles.Left | AnchorStyles.Right);
            var stretchesVertically = (anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == (AnchorStyles.Top | AnchorStyles.Bottom);
            var width = stretchesHorizontally ? available.Width : Math.Min(constrained.Width, available.Width);
            var height = stretchesVertically ? available.Height : Math.Min(constrained.Height, available.Height);
            var localX = (anchor & AnchorStyles.Left) != 0
                ? available.X
                : (anchor & AnchorStyles.Right) != 0
                    ? available.Right - width
                    : available.X + ((available.Width - width) / 2);
            var localY = (anchor & AnchorStyles.Top) != 0
                ? available.Y
                : (anchor & AnchorStyles.Bottom) != 0
                    ? available.Bottom - height
                    : available.Y + ((available.Height - height) / 2);

            SetEffectiveChildBounds(child, bounds, localX, localY, width, height, effectiveBounds, baseline);
            childIndex++;
        }
    }

    private static AnchorStyles GetUnifiedTableAnchor(DesignControlNode child)
        => DesignerLayoutProperties.GetDock(child) switch
        {
            DockStyle.Top => AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            DockStyle.Bottom => AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right,
            DockStyle.Left => AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
            DockStyle.Right => AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
            DockStyle.Fill => AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            _ => DesignerLayoutProperties.GetAnchor(child)
        };

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
