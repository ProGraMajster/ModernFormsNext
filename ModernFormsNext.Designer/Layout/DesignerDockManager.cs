using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ModernFormsNext;
using SkiaSharp;

namespace ModernFormsNext.Designer.Layout;

internal sealed class DesignerDockManager
{
    private const int Gap = 6;
    private const int TabHeight = 28;
    private const int AutoHideStripSize = 28;
    private const int MinimumToolWindowSize = 120;
    private const int MinimumSegmentLength = 80;
    private const int MinimumCenterWidth = 320;
    private const int MinimumCenterHeight = 220;
    private const int SplitterThickness = 6;
    private const int DragThreshold = 8;
    private const int DockGuideSize = 36;

    private readonly Panel owner;
    private readonly ModernFormsDesignerOptions options;
    private readonly Action invalidateLayout;
    private readonly Dictionary<DesignerToolWindowId, Entry> entries = [];
    private readonly Dictionary<DesignerToolWindowSide, DesignerToolWindowId> activeTabbedWindows = [];
    private readonly Dictionary<DesignerToolWindowSide, DesignerToolWindowId> activeAutoHideWindows = [];
    private readonly Dictionary<DesignerToolWindowId, Form> floatingForms = [];
    private readonly HashSet<DesignerToolWindowId> closingFloatingForms = [];
    private readonly List<SplitterEntry> splitters = [];
    private readonly DockDragOverlay dragOverlay;
    private readonly Action<string>? log;
    private Entry? dragCandidate;
    private Entry? draggingEntry;
    private Point dragStartPoint;
    private Point dragCurrentPoint;
    private Rectangle dragGhostBounds;
    private DropPreview dropPreview = DropPreview.None;
    private DockResizeOperation? resizeOperation;
    private Rectangle lastBodyBounds;
    private int splitterCursor;
    private bool suppressNextChromeClick;

    public DesignerDockManager(
        Panel owner,
        ModernFormsDesignerOptions options,
        Action invalidateLayout,
        Action<string>? log = null)
    {
        this.owner = owner;
        this.options = options;
        this.invalidateLayout = invalidateLayout;
        this.log = log;

        dragOverlay = owner.Controls.Add(new DockDragOverlay(this));
        dragOverlay.Visible = false;
    }

    public void AddWindow(DesignerToolWindowId id, string title, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var tabButton = owner.Controls.Add(CreateChromeButton(title));
        tabButton.Click += (_, _) =>
        {
            if (suppressNextChromeClick)
            {
                suppressNextChromeClick = false;
                return;
            }

            var layout = options.GetToolWindowLayout(id);

            if (layout.Mode == DesignerToolWindowMode.AutoHide)
                ToggleAutoHideWindow(layout.Side, id);
            else
                activeTabbedWindows[layout.Side] = id;

            invalidateLayout();
        };

        var entry = new Entry(id, title, control, tabButton);
        entries[id] = entry;

        RegisterDragSource(tabButton, entry, headerOnly: false);
        RegisterDragSource(control, entry, headerOnly: true);
    }

    public Rectangle Layout(Rectangle bodyBounds)
    {
        lastBodyBounds = bodyBounds;
        SyncFloatingWindows();
        HideAllChrome();
        HideAllSplitters();
        splitterCursor = 0;

        var topHeight = GetReservedSize(DesignerToolWindowSide.Top);
        var leftWidth = GetReservedSize(DesignerToolWindowSide.Left);
        var rightWidth = GetReservedSize(DesignerToolWindowSide.Right);
        var bottomHeight = GetReservedSize(DesignerToolWindowSide.Bottom);

        ClampOpposingSideSizes(ref leftWidth, ref rightWidth, bodyBounds.Width, MinimumCenterWidth);
        ClampOpposingSideSizes(ref topHeight, ref bottomHeight, bodyBounds.Height, MinimumCenterHeight);
        PersistSideSize(DesignerToolWindowSide.Left, leftWidth);
        PersistSideSize(DesignerToolWindowSide.Right, rightWidth);
        PersistSideSize(DesignerToolWindowSide.Top, topHeight);
        PersistSideSize(DesignerToolWindowSide.Bottom, bottomHeight);

        var topGap = topHeight > 0 ? Gap : 0;
        var leftGap = leftWidth > 0 ? Gap : 0;
        var rightGap = rightWidth > 0 ? Gap : 0;
        var bottomGap = bottomHeight > 0 ? Gap : 0;

        var middleTop = bodyBounds.Top + topHeight + topGap;
        var middleHeight = Math.Max(1, bodyBounds.Height - topHeight - bottomHeight - topGap - bottomGap);
        var sideTop = middleTop;
        var sideHeight = middleHeight;
        var centerLeft = bodyBounds.Left + leftWidth + leftGap;
        var centerWidth = Math.Max(1, bodyBounds.Width - leftWidth - rightWidth - leftGap - rightGap);
        var centerHeight = middleHeight;

        if (topHeight > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Top,
                new Rectangle(bodyBounds.Left, bodyBounds.Top, bodyBounds.Width, topHeight));
            UseSplitter(
                SplitterRole.SideThickness,
                DesignerToolWindowSide.Top,
                new Rectangle(bodyBounds.Left, bodyBounds.Top + topHeight, bodyBounds.Width, SplitterThickness),
                previousId: null,
                nextId: null);
        }

        if (leftWidth > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Left,
                new Rectangle(bodyBounds.Left, sideTop, leftWidth, sideHeight));
            UseSplitter(
                SplitterRole.SideThickness,
                DesignerToolWindowSide.Left,
                new Rectangle(bodyBounds.Left + leftWidth, sideTop, SplitterThickness, sideHeight),
                previousId: null,
                nextId: null);
        }

        if (rightWidth > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Right,
                new Rectangle(bodyBounds.Right - rightWidth, sideTop, rightWidth, sideHeight));
            UseSplitter(
                SplitterRole.SideThickness,
                DesignerToolWindowSide.Right,
                new Rectangle(bodyBounds.Right - rightWidth - rightGap, sideTop, SplitterThickness, sideHeight),
                previousId: null,
                nextId: null);
        }

        if (bottomHeight > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Bottom,
                new Rectangle(bodyBounds.Left, bodyBounds.Bottom - bottomHeight, bodyBounds.Width, bottomHeight));
            UseSplitter(
                SplitterRole.SideThickness,
                DesignerToolWindowSide.Bottom,
                new Rectangle(bodyBounds.Left, bodyBounds.Bottom - bottomHeight - bottomGap, bodyBounds.Width, SplitterThickness),
                previousId: null,
                nextId: null);
        }

        foreach (var entry in entries.Values)
        {
            if (!IsEntryShown(entry) && options.GetToolWindowLayout(entry.Id).Mode != DesignerToolWindowMode.Floating)
                entry.Control.Visible = false;
        }

        HideUnusedSplitters();
        foreach (var splitter in splitters.Where(splitter => splitter.Control.Visible))
            ShowAboveWorkspace(splitter.Control);

        if (dragOverlay.Visible)
            ShowAboveWorkspace(dragOverlay);

        return new Rectangle(centerLeft, sideTop, centerWidth, centerHeight);
    }

    private int GetReservedSize(DesignerToolWindowSide side)
    {
        var sideEntries = GetSideEntries(side).ToArray();

        if (sideEntries.Length == 0)
            return 0;

        if (sideEntries.All(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.AutoHide))
            return AutoHideStripSize;

        return Math.Max(
            MinimumToolWindowSize,
            sideEntries
                .Where(entry => options.GetToolWindowLayout(entry.Id).Mode != DesignerToolWindowMode.AutoHide)
                .Select(entry => options.GetToolWindowLayout(entry.Id).Size)
                .DefaultIfEmpty(AutoHideStripSize)
                .Max());
    }

    private IEnumerable<Entry> GetSideEntries(DesignerToolWindowSide side)
        => entries.Values
            .Where(entry => IsEntryShown(entry) && options.GetToolWindowLayout(entry.Id).Side == side)
            .OrderBy(entry => options.GetToolWindowLayout(entry.Id).Order)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase);

    private bool IsEntryShown(Entry entry)
    {
        if (entry.Id == DesignerToolWindowId.Output && !options.ShowOutputPanel)
            return false;

        if (entry.Id == DesignerToolWindowId.SolutionExplorer && !options.ShowSolutionExplorer)
            return false;

        var mode = options.GetToolWindowLayout(entry.Id).Mode;
        return mode is not DesignerToolWindowMode.Hidden and not DesignerToolWindowMode.Floating;
    }

    private void SyncFloatingWindows()
    {
        foreach (var entry in entries.Values)
        {
            var layout = options.GetToolWindowLayout(entry.Id);
            if (layout.Mode == DesignerToolWindowMode.Floating)
                EnsureFloatingWindow(entry, layout);
            else
                ReturnFloatingWindow(entry);
        }
    }

    private void EnsureFloatingWindow(Entry entry, DesignerToolWindowLayout layout)
    {
        if (floatingForms.ContainsKey(entry.Id))
            return;

        var floatingSize = CreateFloatingClientSize(entry, layout);
        var form = new Form
        {
            Name = $"{entry.Id}ToolWindow",
            Text = entry.Title,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = floatingSize
        };

        entry.Control.Dock = DockStyle.Fill;
        entry.Control.Visible = true;
        form.Controls.Add(entry.Control);
        form.Closed += (_, _) =>
        {
            if (closingFloatingForms.Contains(entry.Id))
                return;

            floatingForms.Remove(entry.Id);
            entry.Control.Dock = DockStyle.None;
            owner.Controls.Add(entry.Control);
            layout.Mode = DesignerToolWindowMode.Docked;
            SyncLegacyVisibilityOptions(entry.Id);
            log?.Invoke($"Docked {entry.Title} after closing floating window.");
            invalidateLayout();
        };

        floatingForms[entry.Id] = form;
        form.Show();
        log?.Invoke($"Floated {entry.Title}.");
    }

    private void ReturnFloatingWindow(Entry entry)
    {
        if (!floatingForms.Remove(entry.Id, out var form))
            return;

        closingFloatingForms.Add(entry.Id);
        try
        {
            entry.Control.Dock = DockStyle.None;
            owner.Controls.Add(entry.Control);
            form.Close();
        }
        finally
        {
            closingFloatingForms.Remove(entry.Id);
        }
    }

    private static Size CreateFloatingClientSize(Entry entry, DesignerToolWindowLayout layout)
    {
        var sourceBounds = entry.Control.Visible
            ? entry.Control.Bounds
            : entry.ChromeButton.Bounds;
        var width = Math.Max(240, sourceBounds.Width);
        var height = Math.Max(180, sourceBounds.Height);

        if (IsHorizontalSide(layout.Side))
        {
            width = Math.Max(width, layout.Length > 0 ? layout.Length : MinimumSegmentLength);
            height = Math.Max(height, layout.Size);
        }
        else
        {
            width = Math.Max(width, layout.Size);
            height = Math.Max(height, layout.Length > 0 ? layout.Length : MinimumSegmentLength);
        }

        return new Size(width, height);
    }

    private void LayoutSide(DesignerToolWindowSide side, Rectangle bounds)
    {
        var entriesOnSide = GetSideEntries(side).ToArray();
        var autoHideEntries = entriesOnSide
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.AutoHide)
            .ToArray();
        var dockedEntries = entriesOnSide
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.Docked)
            .Select(entry => new LayoutGroup(
                [entry],
                options.GetToolWindowLayout(entry.Id).Length,
                GetMinimumSegmentLength(entry.Id)))
            .ToList();
        var tabbedEntries = entriesOnSide
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.Tabbed)
            .ToArray();

        foreach (var entry in autoHideEntries)
            entry.Control.Visible = false;

        var contentBounds = LayoutAutoHideStrip(side, bounds, autoHideEntries);
        var groups = dockedEntries;
        if (tabbedEntries.Length > 0)
            groups.Add(new LayoutGroup(
                tabbedEntries,
                tabbedEntries
                    .Select(entry => options.GetToolWindowLayout(entry.Id).Length)
                    .DefaultIfEmpty(0)
                    .Max(),
                tabbedEntries
                    .Select(entry => GetMinimumSegmentLength(entry.Id))
                    .DefaultIfEmpty(MinimumSegmentLength)
                    .Max()));

        if (groups.Count == 0)
            return;

        var availableLength = IsHorizontalSide(side)
            ? contentBounds.Width
            : contentBounds.Height;
        var segmentLengths = CalculateSegmentLengths(groups, availableLength);
        var cursor = IsHorizontalSide(side) ? contentBounds.Left : contentBounds.Top;

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var segmentLength = segmentLengths[i];
            var segment = CreateSegment(side, contentBounds, cursor, segmentLength);
            PersistGroupLength(group, segmentLength);

            if (group.Entries.Length == 1 && options.GetToolWindowLayout(group.Entries[0].Id).Mode == DesignerToolWindowMode.Docked)
            {
                var entry = group.Entries[0];
                entry.Control.SetBounds(segment.Left, segment.Top, segment.Width, segment.Height);
                entry.Control.Visible = true;
                ShowAboveWorkspace(entry.Control);
            }
            else
            {
                LayoutTabbedGroup(side, segment, group.Entries);
            }

            cursor += segmentLength;

            if (i < groups.Count - 1)
            {
                var previousId = group.PrimaryId;
                var nextId = groups[i + 1].PrimaryId;
                var splitterBounds = CreateSegmentSplitterBounds(side, contentBounds, cursor);
                UseSplitter(SplitterRole.SegmentLength, side, splitterBounds, previousId, nextId);
                cursor += Gap;
            }
        }
    }

    private static int[] CalculateSegmentLengths(IReadOnlyList<LayoutGroup> groups, int availableLength)
    {
        var groupCount = groups.Count;
        var gapTotal = Math.Max(0, groupCount - 1) * Gap;
        var usableLength = Math.Max(1, availableLength - gapTotal);
        var lengths = new int[groupCount];
        var flexibleIndexes = new List<int>();
        var consumed = 0;

        if (GetMinimumLengthSum(groups) >= usableLength)
            return DistributeConstrainedLength(groupCount, usableLength);

        for (var i = 0; i < groupCount; i++)
        {
            var configuredLength = groups[i].PreferredLength;
            if (configuredLength > 0)
            {
                lengths[i] = Math.Max(groups[i].MinimumLength, configuredLength);
                consumed += lengths[i];
            }
            else
            {
                flexibleIndexes.Add(i);
            }
        }

        if (flexibleIndexes.Count > 0)
        {
            var remaining = Math.Max(groupCount, usableLength - consumed);
            var flexibleLength = Math.Max(MinimumSegmentLength, remaining / flexibleIndexes.Count);
            foreach (var index in flexibleIndexes)
                lengths[index] = Math.Max(groups[index].MinimumLength, flexibleLength);
        }

        var total = lengths.Sum();
        if (total < usableLength)
        {
            lengths[^1] += usableLength - total;
            return lengths;
        }

        if (total == usableLength)
            return lengths;

        var overflow = total - usableLength;
        for (var i = groupCount - 1; i >= 0 && overflow > 0; i--)
        {
            var removable = Math.Max(0, lengths[i] - groups[i].MinimumLength);
            var delta = Math.Min(removable, overflow);
            lengths[i] -= delta;
            overflow -= delta;
        }

        if (overflow > 0)
            return DistributeConstrainedLength(groupCount, usableLength);

        return lengths;
    }

    private static int GetMinimumLengthSum(IReadOnlyList<LayoutGroup> groups)
        => groups.Sum(group => group.MinimumLength);

    private static int[] DistributeConstrainedLength(int groupCount, int usableLength)
    {
        var lengths = new int[groupCount];
        var baseLength = Math.Max(1, usableLength / groupCount);
        var remainder = Math.Max(0, usableLength - (baseLength * groupCount));

        for (var i = 0; i < groupCount; i++)
            lengths[i] = baseLength + (i < remainder ? 1 : 0);

        return lengths;
    }

    private void PersistGroupLength(LayoutGroup group, int segmentLength)
    {
        foreach (var entry in group.Entries)
            options.GetToolWindowLayout(entry.Id).Length = Math.Max(1, segmentLength);
    }

    private Rectangle LayoutAutoHideStrip(
        DesignerToolWindowSide side,
        Rectangle bounds,
        IReadOnlyList<Entry> autoHideEntries)
    {
        if (autoHideEntries.Count == 0)
            return bounds;

        var buttonCursor = IsHorizontalSide(side) ? bounds.Left : bounds.Top;

        foreach (var entry in autoHideEntries)
        {
            var selected = activeAutoHideWindows.TryGetValue(side, out var activeId) && activeId == entry.Id;

            if (IsHorizontalSide(side))
            {
                var y = side == DesignerToolWindowSide.Top ? bounds.Top : bounds.Bottom - AutoHideStripSize;
                entry.ChromeButton.SetBounds(buttonCursor, y, 120, AutoHideStripSize);
                buttonCursor += 120 + 1;
            }
            else
            {
                var x = side == DesignerToolWindowSide.Left ? bounds.Left : bounds.Right - AutoHideStripSize;
                entry.ChromeButton.SetBounds(x, buttonCursor, AutoHideStripSize, 120);
                buttonCursor += 120 + 1;
            }

            entry.ChromeButton.Text = entry.Title;
            entry.ChromeButton.Visible = true;
            ApplyChromeStyle(entry.ChromeButton, selected);
            ShowAboveWorkspace(entry.ChromeButton);
        }

        LayoutActiveAutoHideWindow(side, bounds, autoHideEntries);

        return side switch
        {
            DesignerToolWindowSide.Top => new Rectangle(
                bounds.Left,
                bounds.Top + AutoHideStripSize,
                bounds.Width,
                Math.Max(1, bounds.Height - AutoHideStripSize)),
            DesignerToolWindowSide.Left => new Rectangle(
                bounds.Left + AutoHideStripSize,
                bounds.Top,
                Math.Max(1, bounds.Width - AutoHideStripSize),
                bounds.Height),
            DesignerToolWindowSide.Right => new Rectangle(
                bounds.Left,
                bounds.Top,
                Math.Max(1, bounds.Width - AutoHideStripSize),
                bounds.Height),
            _ => new Rectangle(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                Math.Max(1, bounds.Height - AutoHideStripSize))
        };
    }

    private void LayoutTabbedGroup(DesignerToolWindowSide side, Rectangle bounds, IReadOnlyList<Entry> tabbedEntries)
    {
        if (tabbedEntries.Count == 0)
            return;

        var activeId = activeTabbedWindows.TryGetValue(side, out var configuredActive)
            && tabbedEntries.Any(entry => entry.Id == configuredActive)
                ? configuredActive
                : tabbedEntries[0].Id;
        activeTabbedWindows[side] = activeId;

        var tabWidth = Math.Max(80, bounds.Width / Math.Max(1, tabbedEntries.Count));
        var tabCursor = bounds.Left;

        foreach (var entry in tabbedEntries)
        {
            var selected = entry.Id == activeId;
            entry.ChromeButton.SetBounds(tabCursor, bounds.Top, Math.Min(tabWidth, Math.Max(1, bounds.Right - tabCursor)), TabHeight);
            entry.ChromeButton.Text = entry.Title;
            entry.ChromeButton.Visible = true;
            ApplyChromeStyle(entry.ChromeButton, selected);

            entry.Control.SetBounds(
                bounds.Left,
                bounds.Top + TabHeight,
                bounds.Width,
                Math.Max(1, bounds.Height - TabHeight));
            entry.Control.Visible = selected;
            if (selected)
                ShowAboveWorkspace(entry.Control);
            tabCursor += tabWidth;
        }
    }

    private static Rectangle CreateSegment(
        DesignerToolWindowSide side,
        Rectangle contentBounds,
        int cursor,
        int length)
        => IsHorizontalSide(side)
            ? new Rectangle(cursor, contentBounds.Top, length, contentBounds.Height)
            : new Rectangle(contentBounds.Left, cursor, contentBounds.Width, length);

    private static Rectangle CreateSegmentSplitterBounds(
        DesignerToolWindowSide side,
        Rectangle contentBounds,
        int cursor)
        => IsHorizontalSide(side)
            ? new Rectangle(cursor, contentBounds.Top, Gap, contentBounds.Height)
            : new Rectangle(contentBounds.Left, cursor, contentBounds.Width, Gap);

    private void ToggleAutoHideWindow(DesignerToolWindowSide side, DesignerToolWindowId id)
    {
        if (activeAutoHideWindows.TryGetValue(side, out var activeId) && activeId == id)
        {
            activeAutoHideWindows.Remove(side);
            return;
        }

        activeAutoHideWindows[side] = id;
    }

    private void RegisterDragSource(Control control, Entry entry, bool headerOnly)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (headerOnly && e.Y > TabHeight)
                return;

            var screenPoint = control.PointToScreen(e.Location);
            dragCandidate = entry;
            draggingEntry = null;
            dragStartPoint = screenPoint;
            dragCurrentPoint = PointToOwnerClient(screenPoint);
            control.Capture = true;
        };

        control.MouseMove += (_, e) =>
        {
            if (dragCandidate is null || e.Button != MouseButtons.Left)
                return;

            var screenPoint = control.PointToScreen(e.Location);
            if (draggingEntry is null)
            {
                var movedX = Math.Abs(screenPoint.X - dragStartPoint.X);
                var movedY = Math.Abs(screenPoint.Y - dragStartPoint.Y);

                if (movedX < DragThreshold && movedY < DragThreshold)
                    return;

                draggingEntry = dragCandidate;
                BeginDragPreview(draggingEntry, PointToOwnerClient(screenPoint));
            }

            if (draggingEntry is not null)
                UpdateDragPreview(draggingEntry, PointToOwnerClient(screenPoint));
        };

        control.MouseUp += (_, e) =>
        {
            control.Capture = false;

            if (dragCandidate is null)
                return;

            var entryToDrop = draggingEntry;
            dragCandidate = null;
            draggingEntry = null;
            EndDragPreview();

            if (entryToDrop is null)
                return;

            suppressNextChromeClick = !headerOnly;
            DropToolWindow(entryToDrop, PointToOwnerClient(control.PointToScreen(e.Location)));
        };
    }

    private Point PointToOwnerClient(Point screenPoint)
    {
        // Mouse events can originate from a nested tool-window child while the dock preview
        // is painted by the owner surface. Convert through real screen coordinates so the
        // preview rectangle, central guides, and final drop target use one coordinate space.
        var ownerOrigin = owner.PointToScreen(Point.Empty);
        return new Point(screenPoint.X - ownerOrigin.X, screenPoint.Y - ownerOrigin.Y);
    }

    private void BeginDragPreview(Entry entry, Point point)
    {
        dragOverlay.SetBounds(0, 0, Math.Max(1, owner.Width), Math.Max(1, owner.Height));
        dragOverlay.Visible = true;
        dragOverlay.Capture = false;
        ShowAboveWorkspace(dragOverlay);
        UpdateDragPreview(entry, point);
    }

    private void UpdateDragPreview(Entry entry, Point point)
    {
        dragCurrentPoint = point;
        dragGhostBounds = CreateDragGhostBounds(entry, point);
        dropPreview = CreateDropPreview(entry, point);
        dragOverlay.Invalidate();
    }

    private void EndDragPreview()
    {
        dropPreview = DropPreview.None;
        dragGhostBounds = Rectangle.Empty;
        dragOverlay.Visible = false;
        dragOverlay.Invalidate();
    }

    private Rectangle CreateDragGhostBounds(Entry entry, Point point)
    {
        var sourceBounds = entry.Control.Visible
            ? entry.Control.Bounds
            : entry.ChromeButton.Bounds;
        var width = Math.Clamp(sourceBounds.Width, 180, 360);
        var height = Math.Clamp(sourceBounds.Height, 120, 260);
        return new Rectangle(point.X - width / 2, point.Y - 18, width, height);
    }

    private DropPreview CreateDropPreview(Entry draggedEntry, Point point)
    {
        var targetEntry = FindDropTarget(draggedEntry, point);
        if (targetEntry is not null)
        {
            var targetBounds = targetEntry.Control.Visible
                ? targetEntry.Control.Bounds
                : targetEntry.ChromeButton.Bounds;
            return new DropPreview(null, targetEntry, targetBounds, targetBounds);
        }

        if (ShouldFloatDrop(point))
            return DropPreview.None;

        var side = DetermineDropSide(point, options.GetToolWindowLayout(draggedEntry.Id).Side);
        return new DropPreview(side, null, CreateSidePreviewBounds(side), Rectangle.Empty);
    }

    private Rectangle CreateSidePreviewBounds(DesignerToolWindowSide side)
    {
        var body = lastBodyBounds.IsEmpty
            ? new Rectangle(0, 0, Math.Max(1, owner.Width), Math.Max(1, owner.Height))
            : lastBodyBounds;
        var width = Math.Max(MinimumToolWindowSize, Math.Min(360, body.Width / 4));
        var height = Math.Max(MinimumToolWindowSize, Math.Min(260, body.Height / 4));

        return side switch
        {
            DesignerToolWindowSide.Top => new Rectangle(body.Left, body.Top, body.Width, height),
            DesignerToolWindowSide.Bottom => new Rectangle(body.Left, body.Bottom - height, body.Width, height),
            DesignerToolWindowSide.Left => new Rectangle(body.Left, body.Top, width, body.Height),
            _ => new Rectangle(body.Right - width, body.Top, width, body.Height)
        };
    }

    private void DropToolWindow(Entry entry, Point dropPoint)
    {
        var layout = options.GetToolWindowLayout(entry.Id);
        var targetEntry = FindDropTarget(entry, dropPoint);

        if (targetEntry is not null)
        {
            var targetLayout = options.GetToolWindowLayout(targetEntry.Id);
            layout.Side = targetLayout.Side;
            layout.Mode = DesignerToolWindowMode.Tabbed;
            targetLayout.Mode = DesignerToolWindowMode.Tabbed;
            layout.Order = targetLayout.Order + 1;
            layout.Length = targetLayout.Length;
            ClearAutoHideActivation(entry.Id);
            activeTabbedWindows[layout.Side] = entry.Id;
            SyncLegacyVisibilityOptions(entry.Id);
            SyncLegacyVisibilityOptions(targetEntry.Id);
            log?.Invoke($"Tabbed {entry.Title} with {targetEntry.Title}.");
            invalidateLayout();
            return;
        }

        if (ShouldFloatDrop(dropPoint))
        {
            ClearAutoHideActivation(entry.Id);
            layout.Mode = DesignerToolWindowMode.Floating;
            if (layout.Length <= 0)
                layout.Length = MinimumSegmentLength;
            layout.Size = Math.Max(MinimumToolWindowSize, layout.Size);
            SyncLegacyVisibilityOptions(entry.Id);
            invalidateLayout();
            return;
        }

        var side = DetermineDropSide(dropPoint, layout.Side);
        var order = GetNextOrder(side, entry.Id);
        ClearAutoHideActivation(entry.Id);
        layout.Side = side;
        layout.Mode = DesignerToolWindowMode.Docked;
        layout.Order = order;
        if (layout.Length <= 0)
            layout.Length = MinimumSegmentLength;
        activeTabbedWindows[side] = entry.Id;
        SyncLegacyVisibilityOptions(entry.Id);
        log?.Invoke($"Docked {entry.Title} to {side}.");
        invalidateLayout();
    }

    private Entry? FindDropTarget(Entry draggedEntry, Point dropPoint)
        => entries.Values
            .Where(entry => !ReferenceEquals(entry, draggedEntry))
            .Where(IsEntryShown)
            .FirstOrDefault(entry =>
                entry.Control.Visible && entry.Control.Bounds.Contains(dropPoint)
            || entry.ChromeButton.Visible && entry.ChromeButton.Bounds.Contains(dropPoint));

    private bool ShouldFloatDrop(Point point)
    {
        if (owner.Width <= 0 || owner.Height <= 0)
            return false;

        if (point.X < 0 || point.Y < 0 || point.X > owner.Width || point.Y > owner.Height)
            return true;

        var edgeBand = Math.Clamp(Math.Min(owner.Width, owner.Height) / 8, 72, 140);
        return point.X > edgeBand
            && point.X < owner.Width - edgeBand
            && point.Y > edgeBand
            && point.Y < owner.Height - edgeBand;
    }

    private DesignerToolWindowSide DetermineDropSide(Point dropPoint, DesignerToolWindowSide fallback)
    {
        if (owner.Width <= 0 || owner.Height <= 0)
            return fallback;

        var leftDistance = Math.Max(0, dropPoint.X);
        var rightDistance = Math.Max(0, owner.Width - dropPoint.X);
        var topDistance = Math.Max(0, dropPoint.Y);
        var bottomDistance = Math.Max(0, owner.Height - dropPoint.Y);
        var minimum = Math.Min(Math.Min(leftDistance, rightDistance), Math.Min(topDistance, bottomDistance));

        if (minimum == topDistance)
            return DesignerToolWindowSide.Top;

        if (minimum == bottomDistance)
            return DesignerToolWindowSide.Bottom;

        return minimum == leftDistance
            ? DesignerToolWindowSide.Left
            : DesignerToolWindowSide.Right;
    }

    private int GetNextOrder(DesignerToolWindowSide side, DesignerToolWindowId excludedId)
        => entries.Values
            .Where(entry => entry.Id != excludedId && options.GetToolWindowLayout(entry.Id).Side == side)
            .Select(entry => options.GetToolWindowLayout(entry.Id).Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;

    private void ClearAutoHideActivation(DesignerToolWindowId id)
    {
        foreach (var side in activeAutoHideWindows
            .Where(pair => pair.Value == id)
            .Select(pair => pair.Key)
            .ToArray())
        {
            activeAutoHideWindows.Remove(side);
        }
    }

    private void SyncLegacyVisibilityOptions(DesignerToolWindowId id)
    {
        var layout = options.GetToolWindowLayout(id);

        if (id == DesignerToolWindowId.Output)
            options.ShowOutputPanel = layout.Mode != DesignerToolWindowMode.Hidden;

        if (id == DesignerToolWindowId.SolutionExplorer)
        {
            options.ShowSolutionExplorer = layout.Mode != DesignerToolWindowMode.Hidden;
            options.SolutionExplorerWidth = layout.Size;
            options.SolutionExplorerDockMode = layout.Mode switch
            {
                DesignerToolWindowMode.AutoHide => DesignerDockPanelMode.AutoHide,
                DesignerToolWindowMode.Tabbed => DesignerDockPanelMode.RightTabbed,
                _ => DesignerDockPanelMode.RightSplit
            };
        }
    }

    private void LayoutActiveAutoHideWindow(
        DesignerToolWindowSide side,
        Rectangle bounds,
        IReadOnlyList<Entry> autoHideEntries)
    {
        if (!activeAutoHideWindows.TryGetValue(side, out var activeId))
            return;

        var activeEntry = autoHideEntries.FirstOrDefault(entry => entry.Id == activeId);
        if (activeEntry is null)
        {
            activeAutoHideWindows.Remove(side);
            return;
        }

        var layout = options.GetToolWindowLayout(activeEntry.Id);
        var size = Math.Max(MinimumToolWindowSize, layout.Size);
        var flyoutBounds = side switch
        {
            DesignerToolWindowSide.Top => new Rectangle(
                bounds.Left,
                bounds.Top + AutoHideStripSize,
                bounds.Width,
                size),
            DesignerToolWindowSide.Left => new Rectangle(
                bounds.Left + AutoHideStripSize,
                bounds.Top,
                size,
                bounds.Height),
            DesignerToolWindowSide.Right => new Rectangle(
                bounds.Right - AutoHideStripSize - size,
                bounds.Top,
                size,
                bounds.Height),
            _ => new Rectangle(
                bounds.Left,
                bounds.Bottom - AutoHideStripSize - size,
                bounds.Width,
                size)
        };

        activeEntry.Control.SetBounds(
            flyoutBounds.Left,
            flyoutBounds.Top,
            flyoutBounds.Width,
            flyoutBounds.Height);
        activeEntry.Control.Visible = true;
        ShowAboveWorkspace(activeEntry.Control);
    }

    private SplitterEntry UseSplitter(
        SplitterRole role,
        DesignerToolWindowSide side,
        Rectangle bounds,
        DesignerToolWindowId? previousId,
        DesignerToolWindowId? nextId)
    {
        var splitter = splitterCursor < splitters.Count
            ? splitters[splitterCursor]
            : CreateSplitter();

        splitterCursor++;
        splitter.Role = role;
        splitter.Side = side;
        splitter.PreviousId = previousId;
        splitter.NextId = nextId;
        splitter.Control.SetBounds(bounds.Left, bounds.Top, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
        splitter.Control.Cursor = IsHorizontalSplitter(side, role) ? Cursors.SizeWestEast : Cursors.SizeNorthSouth;
        splitter.Control.Visible = true;
        splitter.Control.Invalidate();
        ShowAboveWorkspace(splitter.Control);
        return splitter;
    }

    private SplitterEntry CreateSplitter()
    {
        var splitter = new SplitterEntry(owner.Controls.Add(new DockSplitterControl(this)));
        splitters.Add(splitter);
        return splitter;
    }

    private void HideAllSplitters()
    {
        foreach (var splitter in splitters)
            splitter.Control.Visible = false;
    }

    private void HideUnusedSplitters()
    {
        for (var i = splitterCursor; i < splitters.Count; i++)
            splitters[i].Control.Visible = false;
    }

    private void BeginResize(DockSplitterControl control, Point point)
    {
        var splitter = splitters.FirstOrDefault(entry => ReferenceEquals(entry.Control, control));
        if (splitter is null)
            return;

        resizeOperation = new DockResizeOperation(
            splitter,
            point,
            GetSideSize(splitter.Side),
            splitter.PreviousId is null ? 0 : GetLength(splitter.PreviousId.Value),
            splitter.NextId is null ? 0 : GetLength(splitter.NextId.Value));
        control.Capture = true;
    }

    private void UpdateResize(Point point)
    {
        if (resizeOperation is null)
            return;

        var operation = resizeOperation;
        var splitter = operation.Splitter;
        if (splitter.Role == SplitterRole.SideThickness)
        {
            var delta = GetSideResizeDelta(splitter.Side, operation.StartPoint, point);
            SetSideSize(splitter.Side, operation.StartSideSize + delta);
        }
        else
        {
            var delta = IsHorizontalSide(splitter.Side)
                ? point.X - operation.StartPoint.X
                : point.Y - operation.StartPoint.Y;
            SetAdjacentSegmentLengths(operation, delta);
        }

        invalidateLayout();
    }

    private void EndResize()
    {
        if (resizeOperation is not null)
            log?.Invoke("Updated designer tool window layout.");

        resizeOperation = null;
    }

    private int GetSideSize(DesignerToolWindowSide side)
        => GetSideEntries(side)
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode != DesignerToolWindowMode.AutoHide)
            .Select(entry => options.GetToolWindowLayout(entry.Id).Size)
            .DefaultIfEmpty(MinimumToolWindowSize)
            .Max();

    private void SetSideSize(DesignerToolWindowSide side, int size)
    {
        var maximum = GetMaximumSideSize(side);
        var minimum = Math.Min(MinimumToolWindowSize, maximum);
        var clamped = Math.Clamp(size, minimum, maximum);
        PersistSideSize(side, clamped);
    }

    private void PersistSideSize(DesignerToolWindowSide side, int size)
    {
        if (size <= 0)
            return;

        foreach (var entry in GetSideEntries(side)
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode != DesignerToolWindowMode.AutoHide))
        {
            options.GetToolWindowLayout(entry.Id).Size = size;
            SyncLegacyVisibilityOptions(entry.Id);
        }
    }

    private int GetMaximumSideSize(DesignerToolWindowSide side)
    {
        var bodyBounds = lastBodyBounds.IsEmpty
            ? new Rectangle(0, 0, Math.Max(1, owner.Width), Math.Max(1, owner.Height))
            : lastBodyBounds;

        if (side is DesignerToolWindowSide.Left or DesignerToolWindowSide.Right)
        {
            var oppositeSide = side == DesignerToolWindowSide.Left
                ? DesignerToolWindowSide.Right
                : DesignerToolWindowSide.Left;
            var oppositeSize = GetReservedSize(oppositeSide);
            var gapBudget = (oppositeSize > 0 ? Gap : 0) + Gap;
            return Math.Max(1, bodyBounds.Width - oppositeSize - gapBudget - MinimumCenterWidth);
        }

        var oppositeVerticalSide = side == DesignerToolWindowSide.Top
            ? DesignerToolWindowSide.Bottom
            : DesignerToolWindowSide.Top;
        var oppositeHeight = GetReservedSize(oppositeVerticalSide);
        var verticalGapBudget = (oppositeHeight > 0 ? Gap : 0) + Gap;
        return Math.Max(1, bodyBounds.Height - oppositeHeight - verticalGapBudget - MinimumCenterHeight);
    }

    private int GetLength(DesignerToolWindowId id)
    {
        var layout = options.GetToolWindowLayout(id);
        return layout.Length > 0 ? layout.Length : MinimumSegmentLength;
    }

    private void SetLength(DesignerToolWindowId id, int length)
        => options.GetToolWindowLayout(id).Length = Math.Max(GetMinimumSegmentLength(id), length);

    private void SetAdjacentSegmentLengths(DockResizeOperation operation, int delta)
    {
        var previousId = operation.Splitter.PreviousId;
        var nextId = operation.Splitter.NextId;

        if (previousId is null && nextId is null)
            return;

        if (previousId is null)
        {
            SetLength(nextId!.Value, operation.StartNextLength - delta);
            return;
        }

        if (nextId is null)
        {
            SetLength(previousId.Value, operation.StartPreviousLength + delta);
            return;
        }

        var previousMinimum = GetMinimumSegmentLength(previousId.Value);
        var nextMinimum = GetMinimumSegmentLength(nextId.Value);
        var total = Math.Max(
            previousMinimum + nextMinimum,
            operation.StartPreviousLength + operation.StartNextLength);
        var previousLength = Math.Clamp(
            operation.StartPreviousLength + delta,
            previousMinimum,
            total - nextMinimum);

        SetLength(previousId.Value, previousLength);
        SetLength(nextId.Value, total - previousLength);
    }

    private static int GetMinimumSegmentLength(DesignerToolWindowId id)
        => id switch
        {
            DesignerToolWindowId.Toolbox => 150,
            DesignerToolWindowId.DocumentOutline => 150,
            DesignerToolWindowId.Properties => 180,
            DesignerToolWindowId.SolutionExplorer => 150,
            DesignerToolWindowId.Output => 90,
            _ => MinimumSegmentLength
        };

    private static void ClampOpposingSideSizes(
        ref int first,
        ref int second,
        int available,
        int minimumCenterSize)
    {
        if (first <= 0 && second <= 0)
            return;

        var gapBudget = (first > 0 ? Gap : 0) + (second > 0 ? Gap : 0);
        var maximumCombined = Math.Max(0, available - gapBudget - minimumCenterSize);
        var combined = first + second;

        if (combined <= maximumCombined)
            return;

        if (maximumCombined <= 0)
        {
            first = 0;
            second = 0;
            return;
        }

        var firstShare = combined == 0
            ? maximumCombined / 2
            : (int)Math.Round(maximumCombined * (first / (double)combined));
        first = Math.Clamp(firstShare, 0, maximumCombined);
        second = maximumCombined - first;
    }

    private static int GetSideResizeDelta(DesignerToolWindowSide side, Point startPoint, Point currentPoint)
        => side switch
        {
            DesignerToolWindowSide.Right => startPoint.X - currentPoint.X,
            DesignerToolWindowSide.Bottom => startPoint.Y - currentPoint.Y,
            DesignerToolWindowSide.Top => currentPoint.Y - startPoint.Y,
            _ => currentPoint.X - startPoint.X
        };

    private void PaintDragOverlay(PaintEventArgs e)
    {
        if (!dragOverlay.Visible)
            return;

        DrawDockGuides(e);

        if (!dropPreview.PreviewBounds.IsEmpty)
        {
            e.Canvas.FillRectangle(dropPreview.PreviewBounds, new SKColor(0, 122, 204, 52));
            e.Canvas.DrawRectangle(dropPreview.PreviewBounds, DesignerColors.Accent, 2);
        }

        if (!dragGhostBounds.IsEmpty)
        {
            e.Canvas.FillRectangle(dragGhostBounds, new SKColor(37, 41, 46, 210));
            e.Canvas.DrawRectangle(dragGhostBounds, DesignerColors.Accent, 2);
            e.Canvas.DrawText(
                draggingEntry?.Title ?? string.Empty,
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new Rectangle(dragGhostBounds.Left + 10, dragGhostBounds.Top, Math.Max(1, dragGhostBounds.Width - 20), 28),
                DesignerColors.Text,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);
        }

        if (dragCurrentPoint != Point.Empty)
        {
            e.Canvas.DrawLine(dragCurrentPoint.X - 6, dragCurrentPoint.Y, dragCurrentPoint.X + 6, dragCurrentPoint.Y, DesignerColors.Accent, 1);
            e.Canvas.DrawLine(dragCurrentPoint.X, dragCurrentPoint.Y - 6, dragCurrentPoint.X, dragCurrentPoint.Y + 6, DesignerColors.Accent, 1);
        }
    }

    private void DrawDockGuides(PaintEventArgs e)
    {
        var cx = Math.Max(0, owner.Width / 2 - DockGuideSize / 2);
        var cy = Math.Max(0, owner.Height / 2 - DockGuideSize / 2);
        var guideBounds = new[]
        {
            new Rectangle(cx, cy - DockGuideSize - 10, DockGuideSize, DockGuideSize),
            new Rectangle(cx, cy + DockGuideSize + 10, DockGuideSize, DockGuideSize),
            new Rectangle(cx - DockGuideSize - 10, cy, DockGuideSize, DockGuideSize),
            new Rectangle(cx + DockGuideSize + 10, cy, DockGuideSize, DockGuideSize),
            new Rectangle(cx, cy, DockGuideSize, DockGuideSize)
        };

        foreach (var bounds in guideBounds)
        {
            e.Canvas.FillRectangle(bounds, new SKColor(30, 34, 38, 220));
            e.Canvas.DrawRectangle(bounds, DesignerColors.PanelBorder, 1);
        }

        e.Canvas.DrawLine(cx + 8, cy + 4, cx + DockGuideSize - 8, cy + 4, DesignerColors.MutedText, 2);
        e.Canvas.DrawLine(cx + 8, cy + DockGuideSize - 4, cx + DockGuideSize - 8, cy + DockGuideSize - 4, DesignerColors.MutedText, 2);
        e.Canvas.DrawLine(cx + 4, cy + 8, cx + 4, cy + DockGuideSize - 8, DesignerColors.MutedText, 2);
        e.Canvas.DrawLine(cx + DockGuideSize - 4, cy + 8, cx + DockGuideSize - 4, cy + DockGuideSize - 8, DesignerColors.MutedText, 2);
        e.Canvas.DrawRectangle(cx + 9, cy + 9, DockGuideSize - 18, DockGuideSize - 18, DesignerColors.MutedText, 1);
    }

    private static bool IsHorizontalSide(DesignerToolWindowSide side)
        => side is DesignerToolWindowSide.Top or DesignerToolWindowSide.Bottom;

    private static bool IsHorizontalSplitter(DesignerToolWindowSide side, SplitterRole role)
        => role == SplitterRole.SideThickness
            ? side is DesignerToolWindowSide.Left or DesignerToolWindowSide.Right
            : IsHorizontalSide(side);

    private static Button CreateChromeButton(string title)
    {
        var button = new Button
        {
            Text = title,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        button.Style.Border.Width = 1;
        button.Style.Border.Color = DesignerColors.PanelBorder;
        return button;
    }

    private static void ApplyChromeStyle(Button button, bool selected)
    {
        button.Style.BackgroundColor = selected ? DesignerColors.Selection : DesignerColors.PanelHeader;
        button.Style.ForegroundColor = selected ? SKColors.White : DesignerColors.Text;
    }

    private void HideAllChrome()
    {
        foreach (var entry in entries.Values)
            entry.ChromeButton.Visible = false;
    }

    private void ShowAboveWorkspace(Control control)
    {
        // ModernFormsNext paints children in collection order, so the last child is visually
        // on top and receives hit testing first. Set the index explicitly instead of relying
        // on z-order helper names while the framework is still aligning WinForms semantics.
        owner.Controls.SetChildIndex(control, owner.Controls.Count - 1);
    }

    private sealed record Entry(
        DesignerToolWindowId Id,
        string Title,
        Control Control,
        Button ChromeButton);

    private sealed class LayoutGroup
    {
        public LayoutGroup(Entry[] entries, int preferredLength, int minimumLength)
        {
            Entries = entries;
            PreferredLength = preferredLength;
            MinimumLength = minimumLength;
        }

        public Entry[] Entries { get; }

        public DesignerToolWindowId PrimaryId => Entries[0].Id;

        public int PreferredLength { get; }

        public int MinimumLength { get; }
    }

    private sealed record DropPreview(
        DesignerToolWindowSide? Side,
        Entry? TargetEntry,
        Rectangle PreviewBounds,
        Rectangle TargetBounds)
    {
        public static readonly DropPreview None = new(null, null, Rectangle.Empty, Rectangle.Empty);
    }

    private enum SplitterRole
    {
        SideThickness,
        SegmentLength
    }

    private sealed class SplitterEntry
    {
        public SplitterEntry(DockSplitterControl control)
        {
            Control = control;
        }

        public DockSplitterControl Control { get; }

        public SplitterRole Role { get; set; }

        public DesignerToolWindowSide Side { get; set; }

        public DesignerToolWindowId? PreviousId { get; set; }

        public DesignerToolWindowId? NextId { get; set; }
    }

    private sealed record DockResizeOperation(
        SplitterEntry Splitter,
        Point StartPoint,
        int StartSideSize,
        int StartPreviousLength,
        int StartNextLength);

    private sealed class DockSplitterControl : Control
    {
        private readonly DesignerDockManager manager;
        private bool hovering;
        private bool resizing;

        public DockSplitterControl(DesignerDockManager manager)
        {
            this.manager = manager;
            TabStop = false;
            SetControlBehavior(ControlBehaviors.Selectable, false);
            Style.BackgroundColor = SKColors.Transparent;

            MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                    return;

                resizing = true;
                manager.BeginResize(this, PointToScreen(e.Location));
                Invalidate();
            };
            MouseMove += (_, e) =>
            {
                if (resizing)
                    manager.UpdateResize(PointToScreen(e.Location));
            };
            MouseUp += (_, _) =>
            {
                resizing = false;
                Capture = false;
                manager.EndResize();
                Invalidate();
            };
            MouseEnter += (_, _) =>
            {
                hovering = true;
                Invalidate();
            };
            MouseLeave += (_, _) =>
            {
                hovering = false;
                Invalidate();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var color = resizing
                ? DesignerColors.Accent
                : hovering
                    ? new SKColor(0, 122, 204, 150)
                    : new SKColor(90, 98, 108, 95);
            var bounds = ClientRectangle;

            if (Width >= Height)
            {
                var y = Math.Max(0, Height / 2 - 1);
                e.Canvas.FillRectangle(0, y, Math.Max(1, Width), 2, color);
            }
            else
            {
                var x = Math.Max(0, Width / 2 - 1);
                e.Canvas.FillRectangle(x, 0, 2, Math.Max(1, Height), color);
            }

            if (hovering || resizing)
                e.Canvas.DrawRectangle(bounds, color, 1);
        }
    }

    private sealed class DockDragOverlay : Control
    {
        private readonly DesignerDockManager manager;

        public DockDragOverlay(DesignerDockManager manager)
        {
            this.manager = manager;
            TabStop = false;
            SetControlBehavior(ControlBehaviors.Transparent, true);
            SetControlBehavior(ControlBehaviors.ReceivesMouseEvents, false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            manager.PaintDragOverlay(e);
        }
    }
}
