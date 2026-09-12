using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DataGridView
{
    private AccessibleScrollInfo? publishedGridScroll;
    private bool publishingGridScroll;
    private int gridScrollUpdateDepth;
    private bool updatingGridScrollBars;
    private bool gridScrollBarsPending;

    private int CountDisplayedRows(int first, int available)
    {
        int count = 0;
        long height = 0;
        for (int row = Math.Max(0, first); row < Rows.Count; row++)
        {
            height += Math.Max(0, LogicalToDeviceUnits(Rows[row].Height));
            if (height > available) break;
            count++;
        }
        return count;
    }

    private Rectangle GridScrollViewport
    {
        get
        {
            var bounds = GetContentArea();
            int headerWidth = RowHeadersVisible ? ScaledRowHeadersWidth : 0;
            int headerHeight = ColumnHeadersVisible ? ScaledHeaderHeight : 0;
            return new(bounds.Left + headerWidth, bounds.Top + headerHeight,
                Math.Max(0, bounds.Width - headerWidth), Math.Max(0, bounds.Height - headerHeight));
        }
    }

    private double GridRowOffset(int index)
    {
        double offset = 0;
        for (int row = 0; row < Math.Min(index, Rows.Count); row++)
            offset += Math.Max(0, LogicalToDeviceUnits(Rows[row].Height)) / (double)ScaleFactor.Height;
        return offset;
    }

    private AccessibleScrollInfo GetGridScrollInfo()
    {
        var viewport = GridScrollViewport;
        double width = viewport.Width / (double)ScaleFactor.Width;
        double height = viewport.Height / (double)ScaleFactor.Height;
        double maximum = vscrollbar.Visible ? GridRowOffset(vscrollbar.Maximum) : 0;
        double offset = Math.Min(maximum, GridRowOffset(top_index));
        double small = top_index < Rows.Count ? Math.Max(0, Rows[top_index].Height) : 0;
        return new(
            new(hscrollbar.Visible ? horizontal_scroll_offset / (double)ScaleFactor.Width : 0, 0,
                hscrollbar.Visible ? hscrollbar.Maximum / (double)ScaleFactor.Width : 0,
                width, hscrollbar.SmallChange / (double)ScaleFactor.Width, width),
            new(offset, 0, maximum, height, small, height),
            GridScreenBounds(this, viewport));
    }

    private void NotifyAccessibleScrollChanged()
    {
        if (gridScrollUpdateDepth != 0 || publishingGridScroll || IsDisposed || Disposing
            || vscrollbar is null || hscrollbar is null) return;
        publishingGridScroll = true;
        try
        {
            // Commit before notifying; callbacks may change the viewport again, but never recurse.
            for (int pass = 0; pass < 64; pass++)
            {
                if (IsDisposed || Disposing) return;
                var current = GetGridScrollInfo();
                if (publishedGridScroll == current) return;
                publishedGridScroll = current;
                NotifyAccessibilityClients(AccessibleEvents.ScrollChanged);
            }
            throw new InvalidOperationException("Grid viewport notifications did not stabilize after 64 changes.");
        }
        finally { publishingGridScroll = false; }
    }

    private bool PerformGridScroll(AccessibleScrollRequest request)
    {
        var lifetime = new AccessibilityControlLifetime(this);
        if (!lifetime.IsCurrent || !Enabled || !Visible) return false;
        var info = GetGridScrollInfo();
        if (!request.TryGetOffset(info.Horizontal, true, out var horizontal)
            || !request.TryGetOffset(info.Vertical, false, out var vertical)) return false;
        int verticalTarget = 0;
        DataGridViewRow? targetRow = null;
        long targetRowVersion = 0;
        if (vertical is { } y)
        {
            double sum = 0;
            while (verticalTarget < vscrollbar.Maximum && sum < y)
            {
                double next = sum + Math.Max(0, LogicalToDeviceUnits(Rows[verticalTarget].Height)) / (double)ScaleFactor.Height;
                if (next > y && y < info.Vertical.Offset) break;
                sum = next;
                verticalTarget++;
            }
            if (verticalTarget < Rows.Count) { targetRow = Rows[verticalTarget]; targetRowVersion = targetRow.OwnershipVersion; }
        }
        gridScrollUpdateDepth++;
        try
        {
            if (horizontal is { } x)
                hscrollbar.Value = (int)Math.Clamp(Math.Round(x * ScaleFactor.Width), 0, hscrollbar.Maximum);
            if (!lifetime.IsCurrent || !Enabled || !Visible) return false;
            if (vertical.HasValue)
            {
                // The canonical grid scrolls at row boundaries. Choose the first boundary at or
                // beyond a forward request, and the last boundary at or before a backward one.
                // Do not reinterpret a prevalidated row request after the horizontal callback
                // has replaced, resized or reordered its rows.
                if (GetGridScrollInfo().Vertical != info.Vertical || targetRow is null
                    || targetRow.OwnershipVersion != targetRowVersion || targetRow.DataGridView != this
                    || targetRow.Index != verticalTarget) return false;
                vscrollbar.Value = verticalTarget;
            }
            return lifetime.IsCurrent && Enabled && Visible;
        }
        finally { gridScrollUpdateDepth--; NotifyAccessibleScrollChanged(); }
    }

    private sealed partial class GridAccessibleObject
    {
        public override AccessibleScrollInfo? ScrollInfo => Grid is { } grid && !IsSensitive ? grid.GetGridScrollInfo() : null;
        public override AccessibleActions SupportedActions => base.SupportedActions
            | (Grid is { Enabled: true, Visible: true } grid && grid.FindWindow()?.InputBindingsClosed != true
                && !IsSensitive ? AccessibleActions.Scroll : AccessibleActions.None);
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
            => action == AccessibleActions.Scroll
                ? Grid is { } grid && !IsSensitive && parameter is AccessibleScrollRequest request && grid.PerformGridScroll(request)
                : base.PerformAction(action, parameter);
    }
}
