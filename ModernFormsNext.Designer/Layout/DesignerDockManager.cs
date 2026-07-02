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

    private readonly Panel owner;
    private readonly ModernFormsDesignerOptions options;
    private readonly Action invalidateLayout;
    private readonly Dictionary<DesignerToolWindowId, Entry> entries = [];
    private readonly Dictionary<DesignerToolWindowSide, DesignerToolWindowId> activeTabbedWindows = [];

    public DesignerDockManager(
        Panel owner,
        ModernFormsDesignerOptions options,
        Action invalidateLayout)
    {
        this.owner = owner;
        this.options = options;
        this.invalidateLayout = invalidateLayout;
    }

    public void AddWindow(DesignerToolWindowId id, string title, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var tabButton = owner.Controls.Add(CreateChromeButton(title));
        tabButton.Click += (_, _) =>
        {
            var layout = options.GetToolWindowLayout(id);

            if (layout.Mode == DesignerToolWindowMode.AutoHide)
                layout.Mode = DesignerToolWindowMode.Docked;
            else
                activeTabbedWindows[layout.Side] = id;

            invalidateLayout();
        };

        entries[id] = new Entry(id, title, control, tabButton);
    }

    public Rectangle Layout(Rectangle bodyBounds)
    {
        HideAllChrome();

        var leftWidth = GetReservedSize(DesignerToolWindowSide.Left);
        var rightWidth = GetReservedSize(DesignerToolWindowSide.Right);
        var bottomHeight = GetReservedSize(DesignerToolWindowSide.Bottom);

        var leftGap = leftWidth > 0 ? Gap : 0;
        var rightGap = rightWidth > 0 ? Gap : 0;
        var bottomGap = bottomHeight > 0 ? Gap : 0;

        var centerLeft = bodyBounds.Left + leftWidth + leftGap;
        var centerWidth = Math.Max(1, bodyBounds.Width - leftWidth - rightWidth - leftGap - rightGap);
        var centerHeight = Math.Max(1, bodyBounds.Height - bottomHeight - bottomGap);

        if (leftWidth > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Left,
                new Rectangle(bodyBounds.Left, bodyBounds.Top, leftWidth, bodyBounds.Height));
        }

        if (rightWidth > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Right,
                new Rectangle(bodyBounds.Right - rightWidth, bodyBounds.Top, rightWidth, bodyBounds.Height));
        }

        if (bottomHeight > 0)
        {
            LayoutSide(
                DesignerToolWindowSide.Bottom,
                new Rectangle(centerLeft, bodyBounds.Bottom - bottomHeight, centerWidth, bottomHeight));
        }

        foreach (var entry in entries.Values)
        {
            if (!IsEntryShown(entry))
                entry.Control.Visible = false;
        }

        return new Rectangle(centerLeft, bodyBounds.Top, centerWidth, centerHeight);
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

        return options.GetToolWindowLayout(entry.Id).Mode != DesignerToolWindowMode.Hidden;
    }

    private void LayoutSide(DesignerToolWindowSide side, Rectangle bounds)
    {
        var entriesOnSide = GetSideEntries(side).ToArray();
        var autoHideEntries = entriesOnSide
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.AutoHide)
            .ToArray();
        var dockedEntries = entriesOnSide
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.Docked)
            .ToArray();
        var tabbedEntries = entriesOnSide
            .Where(entry => options.GetToolWindowLayout(entry.Id).Mode == DesignerToolWindowMode.Tabbed)
            .ToArray();

        foreach (var entry in autoHideEntries)
            entry.Control.Visible = false;

        var contentBounds = LayoutAutoHideStrip(side, bounds, autoHideEntries);

        var visibleGroups = dockedEntries.Length + (tabbedEntries.Length > 0 ? 1 : 0);

        if (visibleGroups == 0)
            return;

        var availableLength = side == DesignerToolWindowSide.Bottom
            ? contentBounds.Width
            : contentBounds.Height;
        var gapTotal = Math.Max(0, visibleGroups - 1) * Gap;
        var segmentLength = Math.Max(1, (availableLength - gapTotal) / visibleGroups);
        var cursor = side == DesignerToolWindowSide.Bottom ? contentBounds.Left : contentBounds.Top;

        foreach (var entry in dockedEntries)
        {
            var segment = CreateSegment(side, contentBounds, cursor, segmentLength);
            entry.Control.SetBounds(segment.Left, segment.Top, segment.Width, segment.Height);
            entry.Control.Visible = true;
            cursor += segmentLength + Gap;
        }

        if (tabbedEntries.Length > 0)
        {
            var remainingLength = side == DesignerToolWindowSide.Bottom
                ? Math.Max(1, contentBounds.Right - cursor)
                : Math.Max(1, contentBounds.Bottom - cursor);
            var segment = CreateSegment(side, contentBounds, cursor, remainingLength);
            LayoutTabbedGroup(side, segment, tabbedEntries);
        }
    }

    private Rectangle LayoutAutoHideStrip(
        DesignerToolWindowSide side,
        Rectangle bounds,
        IReadOnlyList<Entry> autoHideEntries)
    {
        if (autoHideEntries.Count == 0)
            return bounds;

        var buttonCursor = side == DesignerToolWindowSide.Bottom ? bounds.Left : bounds.Top;

        foreach (var entry in autoHideEntries)
        {
            if (side == DesignerToolWindowSide.Bottom)
            {
                entry.ChromeButton.SetBounds(buttonCursor, bounds.Bottom - AutoHideStripSize, 120, AutoHideStripSize);
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
            ApplyChromeStyle(entry.ChromeButton, selected: false);
        }

        return side switch
        {
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
            tabCursor += tabWidth;
        }
    }

    private static Rectangle CreateSegment(
        DesignerToolWindowSide side,
        Rectangle contentBounds,
        int cursor,
        int length)
        => side == DesignerToolWindowSide.Bottom
            ? new Rectangle(cursor, contentBounds.Top, length, contentBounds.Height)
            : new Rectangle(contentBounds.Left, cursor, contentBounds.Width, length);

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

    private sealed record Entry(
        DesignerToolWindowId Id,
        string Title,
        Control Control,
        Button ChromeButton);
}
