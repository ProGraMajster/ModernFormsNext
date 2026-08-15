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
        var bounds = new Dictionary<DesignControlNode, DesignBounds>();
        var documentBounds = new DesignBounds(0, 0, Math.Max(1, rootSize.Width), Math.Max(1, rootSize.Height));
        var documentContentBounds = document.RootKind == DesignRootKind.UserControl
            ? DesignerLayoutProperties.GetPaddedContentBounds(documentBounds, DesignerLayoutProperties.GetPadding(document.Properties))
            : documentBounds;

        LayoutGenericChildren(document.Controls, documentBounds, documentContentBounds, document.Size, bounds);

        return new DesignerLayoutResult(bounds);
    }

    private static void LayoutGenericChildren(
        DesignControlCollection children,
        DesignBounds parentBounds,
        DesignBounds parentContentBounds,
        DesignSize parentDesignSize,
        IDictionary<DesignControlNode, DesignBounds> bounds)
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
            var localBounds = GetLocalBounds(child, remaining, parentBounds, parentDesignSize);
            var absoluteBounds = new DesignBounds(
                parentBounds.X + localBounds.X,
                parentBounds.Y + localBounds.Y,
                Math.Max(0, localBounds.Width),
                Math.Max(0, localBounds.Height));

            bounds[child] = absoluteBounds;
            remaining = ConsumeDockSpace(child, remaining, localBounds);

            LayoutContainerChildren(child, absoluteBounds, bounds);
        }
    }

    private static void LayoutContainerChildren(
        DesignControlNode container,
        DesignBounds containerBounds,
        IDictionary<DesignControlNode, DesignBounds> bounds)
    {
        DesignerSpecialContainers.EnsureSpecialChildren(container);
        var contentBounds = DesignerLayoutProperties.GetContainerContentBounds(container, containerBounds);

        if (DesignerSpecialContainers.IsSplitContainer(container))
        {
            LayoutSplitContainer(container, contentBounds, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsTabControl(container))
        {
            LayoutTabControl(container, contentBounds, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsFlowLayoutPanel(container))
        {
            LayoutFlowLayoutPanel(container, contentBounds, bounds);
            return;
        }

        if (DesignerSpecialContainers.IsTableLayoutPanel(container))
        {
            LayoutTableLayoutPanel(container, contentBounds, bounds);
            return;
        }

        if (container.Children.Count > 0)
            LayoutGenericChildren(container.Children, containerBounds, contentBounds, GetDesignSize(container), bounds);
    }

    private static void LayoutSplitContainer(
        DesignControlNode splitContainer,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds)
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
                panel1DesignSize,
                effectiveBounds);
            LayoutGenericChildren(
                panel2.Children,
                panel2Bounds,
                DesignerLayoutProperties.GetContainerContentBounds(panel2, panel2Bounds),
                panel2DesignSize,
                effectiveBounds);
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
                panel1DesignSize,
                effectiveBounds);
            LayoutGenericChildren(
                panel2.Children,
                panel2Bounds,
                DesignerLayoutProperties.GetContainerContentBounds(panel2, panel2Bounds),
                panel2DesignSize,
                effectiveBounds);
        }
    }

    private static void LayoutTabControl(
        DesignControlNode tabControl,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds)
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
                pageDesignSizes[selectedPage],
                effectiveBounds);
    }

    private static void LayoutFlowLayoutPanel(
        DesignControlNode panel,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds)
    {
        var direction = DesignerSpecialContainers.GetEnum(panel, DesignerSpecialContainers.FlowDirectionPropertyName, FlowDirection.LeftToRight);
        var wrap = DesignerSpecialContainers.GetBoolean(panel, DesignerSpecialContainers.WrapContentsPropertyName, true);
        var gap = 6;
        var cursorX = 0;
        var cursorY = 0;
        var lineSize = 0;

        foreach (var child in panel.Children)
        {
            var width = Math.Max(1, child.Bounds.Width);
            var height = Math.Max(1, child.Bounds.Height);

            if (direction is FlowDirection.LeftToRight or FlowDirection.RightToLeft)
            {
                if (wrap && cursorX > 0 && cursorX + width > bounds.Width)
                {
                    cursorX = 0;
                    cursorY += lineSize + gap;
                    lineSize = 0;
                }

                var localX = direction == FlowDirection.LeftToRight
                    ? cursorX
                    : Math.Max(0, bounds.Width - cursorX - width);
                SetEffectiveChildBounds(child, bounds, localX, cursorY, width, height, effectiveBounds);
                cursorX += width + gap;
                lineSize = Math.Max(lineSize, height);
            }
            else
            {
                if (wrap && cursorY > 0 && cursorY + height > bounds.Height)
                {
                    cursorY = 0;
                    cursorX += lineSize + gap;
                    lineSize = 0;
                }

                var localY = direction == FlowDirection.TopDown
                    ? cursorY
                    : Math.Max(0, bounds.Height - cursorY - height);
                SetEffectiveChildBounds(child, bounds, cursorX, localY, width, height, effectiveBounds);
                cursorY += height + gap;
                lineSize = Math.Max(lineSize, width);
            }
        }
    }

    private static void LayoutTableLayoutPanel(
        DesignControlNode panel,
        DesignBounds bounds,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds)
    {
        var columns = Math.Max(1, DesignerSpecialContainers.GetInt(panel, DesignerSpecialContainers.ColumnCountPropertyName, 2));
        var rows = Math.Max(1, DesignerSpecialContainers.GetInt(panel, DesignerSpecialContainers.RowCountPropertyName, 2));
        var cellWidth = Math.Max(1, bounds.Width / columns);
        var cellHeight = Math.Max(1, bounds.Height / rows);
        var childIndex = 0;

        foreach (var child in panel.Children)
        {
            var column = DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnPropertyName, childIndex % columns);
            var row = DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowPropertyName, childIndex / columns);
            var columnSpan = Math.Max(1, DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnSpanPropertyName, 1));
            var rowSpan = Math.Max(1, DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowSpanPropertyName, 1));

            column = Math.Clamp(column, 0, columns - 1);
            row = Math.Clamp(row, 0, rows - 1);
            columnSpan = Math.Min(columnSpan, columns - column);
            rowSpan = Math.Min(rowSpan, rows - row);

            var localX = column * cellWidth;
            var localY = row * cellHeight;
            var width = columnSpan == columns - column ? bounds.Width - localX : cellWidth * columnSpan;
            var height = rowSpan == rows - row ? bounds.Height - localY : cellHeight * rowSpan;

            SetEffectiveChildBounds(child, bounds, localX + 3, localY + 3, Math.Max(1, width - 6), Math.Max(1, height - 6), effectiveBounds);
            childIndex++;
        }
    }

    private static void SetEffectiveChildBounds(
        DesignControlNode child,
        DesignBounds parentBounds,
        int localX,
        int localY,
        int width,
        int height,
        IDictionary<DesignControlNode, DesignBounds> effectiveBounds)
    {
        var absolute = new DesignBounds(parentBounds.X + localX, parentBounds.Y + localY, width, height);
        effectiveBounds[child] = absolute;
        LayoutContainerChildren(child, absolute, effectiveBounds);
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
            return dock switch
            {
                DockStyle.Top => new DesignBounds(remaining.X, remaining.Y, remaining.Width, Math.Min(height, remaining.Height)),
                DockStyle.Bottom => new DesignBounds(remaining.X, remaining.Bottom - Math.Min(height, remaining.Height), remaining.Width, Math.Min(height, remaining.Height)),
                DockStyle.Left => new DesignBounds(remaining.X, remaining.Y, Math.Min(width, remaining.Width), remaining.Height),
                DockStyle.Right => new DesignBounds(remaining.Right - Math.Min(width, remaining.Width), remaining.Y, Math.Min(width, remaining.Width), remaining.Height),
                DockStyle.Fill => remaining,
                _ => node.Bounds
            };
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

        return new DesignBounds(x, y, width, height);
    }

    private static DesignSize GetDesignSize(DesignControlNode node)
        => new(Math.Max(1, node.Bounds.Width), Math.Max(1, node.Bounds.Height));

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
            DockStyle.Fill => new DesignBounds(remaining.X, remaining.Y, 0, 0),
            _ => remaining
        };
    }
}
