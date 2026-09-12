using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

internal partial class DateTimePickerCalendar
{
    private long calendarViewVersion;
    private DateTime focusedDate = DateTime.Today;
    private int focusedCell;
    internal DateTime FocusedDate => focusedDate;
    internal int FocusedCell => focusedCell;
    protected override void OnVisibleChanged(EventArgs e)
    {
        // ControlAdapter keeps its managed tree addressable while the native window is hidden;
        // inspect the real popup visibility as well as the control's effective visibility.
        try { if ((!Visible || FindWindow()?.Visible == false) && owner.IsCurrentCalendar(this)) owner.CloseDropDown(); }
        finally { base.OnVisibleChanged(e); }
    }
    private bool CanInteract => !IsDisposed && !Disposing && Enabled && Visible
        && !owner.IsDisposed && !owner.Disposing && owner.Enabled && owner.Visible && owner.Checked
        && owner.IsCurrentCalendar(this) && FindWindow() is { Visible: true, InputBindingsClosed: false };

    private Rectangle ScaleCalendarRectangle(Rectangle rectangle)
        => GetScaledBounds(rectangle, ScaleFactor, BoundsSpecified.All);

    internal Rectangle DayHeaderRectangle(int column) => ScaleCalendarRectangle(new(8 + column * 30, PaddingSize + HeaderHeight, 30, 20));

    private void PublishCalendarView()
    {
        ++calendarViewVersion;
        Invalidate();
        NotifyAccessibilityClients(AccessibleEvents.Reorder);
    }

    private void ShowCalendarView(DateTimePickerCalendarViewMode mode)
    {
        if (viewMode == mode) return;
        viewMode = mode;
        yearRangeStart = GetYearRangeStart(displayMonth.Year);
        focusedCell = mode == DateTimePickerCalendarViewMode.Months ? displayMonth.Month - 1
            : Math.Clamp(displayMonth.Year - yearRangeStart, 0, 11);
        PublishCalendarView();
    }

    private bool CanNavigate(int direction)
    {
        int first = viewMode switch
        {
            DateTimePickerCalendarViewMode.Days => displayMonth.Year * 12 + displayMonth.Month - 1,
            DateTimePickerCalendarViewMode.Months => displayMonth.Year,
            _ => yearRangeStart
        };
        int minimum = viewMode == DateTimePickerCalendarViewMode.Days ? MinDate.Year * 12 + MinDate.Month - 1 : MinDate.Year;
        int maximum = viewMode == DateTimePickerCalendarViewMode.Days ? MaxDate.Year * 12 + MaxDate.Month - 1 : MaxDate.Year;
        return direction < 0 ? first > minimum : first + (viewMode == DateTimePickerCalendarViewMode.Years ? 11 : 0) < maximum;
    }

    private bool NavigateCalendar(int direction)
    {
        if (!CanInteract || !CanNavigate(direction)) return false;
        if (viewMode == DateTimePickerCalendarViewMode.Days) displayMonth = displayMonth.AddMonths(direction);
        else if (viewMode == DateTimePickerCalendarViewMode.Months) displayMonth = displayMonth.AddYears(direction);
        else yearRangeStart = Math.Clamp(yearRangeStart + direction * 12, MinDate.Year, Math.Min(MaxDate.Year, 9988));
        if (viewMode == DateTimePickerCalendarViewMode.Days)
            focusedDate = ClampDate(new(displayMonth.Year, displayMonth.Month, Math.Min(focusedDate.Day, DateTime.DaysInMonth(displayMonth.Year, displayMonth.Month))));
        PublishCalendarView();
        return true;
    }

    private DateTime ClampDate(DateTime date) => date < MinDate.Date ? MinDate.Date : date > MaxDate.Date ? MaxDate.Date : date.Date;

    private bool CanActivateCell(int index)
    {
        if (index < 0 || index >= (viewMode == DateTimePickerCalendarViewMode.Days ? 42 : 12)) return false;
        if (viewMode == DateTimePickerCalendarViewMode.Days)
        {
            var date = GetFirstVisibleDate().AddDays(index);
            return date >= MinDate.Date && date <= MaxDate.Date;
        }
        int year = viewMode == DateTimePickerCalendarViewMode.Months ? displayMonth.Year : yearRangeStart + index;
        if (year < MinDate.Year || year > MaxDate.Year || year < 1 || year > 9999) return false;
        if (viewMode == DateTimePickerCalendarViewMode.Years) return true;
        var first = new DateTime(year, index + 1, 1);
        var last = new DateTime(year, index + 1, DateTime.DaysInMonth(year, index + 1));
        return last >= MinDate.Date && first <= MaxDate.Date;
    }

    internal bool IsCalendarCellEnabled(int index) => CanActivateCell(index);

    private bool ActivateCell(int index)
    {
        if (!CanInteract || !CanActivateCell(index)) return false;
        if (viewMode == DateTimePickerCalendarViewMode.Days)
        {
            owner.ApplyDropDownValue(GetFirstVisibleDate().AddDays(index));
            return true;
        }
        if (viewMode == DateTimePickerCalendarViewMode.Months)
        {
            displayMonth = new(displayMonth.Year, index + 1, 1);
            focusedDate = ClampDate(new(displayMonth.Year, displayMonth.Month, Math.Min(focusedDate.Day, DateTime.DaysInMonth(displayMonth.Year, displayMonth.Month))));
            viewMode = DateTimePickerCalendarViewMode.Days;
        }
        else
        {
            int year = yearRangeStart + index;
            int month = year == MinDate.Year ? Math.Max(displayMonth.Month, MinDate.Month)
                : year == MaxDate.Year ? Math.Min(displayMonth.Month, MaxDate.Month) : displayMonth.Month;
            displayMonth = new(year, month, 1);
            focusedCell = month - 1;
            viewMode = DateTimePickerCalendarViewMode.Months;
        }
        PublishCalendarView();
        return true;
    }

    private void GoToToday()
    {
        focusedDate = ClampDate(DateTime.Today);
        displayMonth = new(focusedDate.Year, focusedDate.Month, 1);
        viewMode = DateTimePickerCalendarViewMode.Days;
        PublishCalendarView();
    }

    private void HandleCalendarPointer(MouseEventArgs e)
    {
        if (!CanInteract || e.Button != MouseButtons.Left) return;
        UpdateLayoutRects();
        var lifetime = new AccessibilityControlLifetime(this);
        Select();
        if (!lifetime.IsCurrent || !CanInteract) return;
        if (prevButtonRect.Contains(e.Location)) { NavigateCalendar(-1); return; }
        if (nextButtonRect.Contains(e.Location)) { NavigateCalendar(1); return; }
        if (viewMode == DateTimePickerCalendarViewMode.Days && monthTitleRect.Contains(e.Location))
        { ShowCalendarView(DateTimePickerCalendarViewMode.Months); return; }
        if (viewMode != DateTimePickerCalendarViewMode.Years && yearTitleRect.Contains(e.Location))
        { ShowCalendarView(DateTimePickerCalendarViewMode.Years); return; }
        if (viewMode == DateTimePickerCalendarViewMode.Days && todayButtonRect.Contains(e.Location)) { GoToToday(); return; }
        var rectangles = viewMode == DateTimePickerCalendarViewMode.Days ? dayCellRects
            : viewMode == DateTimePickerCalendarViewMode.Months ? monthCellRects : yearCellRects;
        for (int i = 0; i < rectangles.Length; ++i)
            if (rectangles[i].Contains(e.Location)) { ActivateCell(i); return; }
    }

    private void HandleCalendarKey(KeyEventArgs e)
    {
        if (e.Handled || !CanInteract) return;
        if (e.KeyCode == Keys.Escape)
        {
            if (viewMode == DateTimePickerCalendarViewMode.Days) owner.CloseDropDown();
            else ShowCalendarView(DateTimePickerCalendarViewMode.Days);
        }
        else if (e.KeyCode is Keys.Return or Keys.Space)
            ActivateCell(viewMode == DateTimePickerCalendarViewMode.Days ? (focusedDate - GetFirstVisibleDate()).Days : focusedCell);
        else if (e.KeyCode is Keys.PageUp or Keys.PageDown)
            NavigateCalendar(e.KeyCode == Keys.PageUp ? -1 : 1);
        else
        {
            int delta = e.KeyCode switch { Keys.Left => -1, Keys.Right => 1, Keys.Up => viewMode == DateTimePickerCalendarViewMode.Days ? -7 : -4,
                Keys.Down => viewMode == DateTimePickerCalendarViewMode.Days ? 7 : 4, _ => 0 };
            if (delta == 0 && e.KeyCode is not (Keys.Home or Keys.End)) return;
            if (viewMode == DateTimePickerCalendarViewMode.Days)
            {
                var oldMonth = displayMonth;
                focusedDate = e.KeyCode == Keys.Home ? ClampDate(displayMonth) : e.KeyCode == Keys.End
                    ? ClampDate(new(displayMonth.Year, displayMonth.Month, DateTime.DaysInMonth(displayMonth.Year, displayMonth.Month)))
                    : ClampDate(focusedDate.AddDays(delta));
                displayMonth = new(focusedDate.Year, focusedDate.Month, 1);
                if (oldMonth != displayMonth) PublishCalendarView();
            }
            else focusedCell = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? 11 : Math.Clamp(focusedCell + delta, 0, 11);
            Invalidate();
            AccessibilityObject.GetFocused()?.NotifyClients(AccessibleEvents.Focus);
        }
        e.Handled = true;
    }
}
