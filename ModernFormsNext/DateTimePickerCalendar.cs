using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents the calendar surface displayed inside the DateTimePicker popup.
    /// </summary>
    internal class DateTimePickerCalendar : Control
    {
        private const int HeaderHeight = 30;
        private const int DayHeaderHeight = 22;
        private const int CellWidth = 30;
        private const int CellHeight = 28;
        private const int PaddingSize = 8;
        private const int WeekRows = 6;
        private const int FooterHeight = 28;

        private readonly DateTimePicker owner;

        private Rectangle prevButtonRect;
        private Rectangle nextButtonRect;
        private Rectangle monthTitleRect;
        private Rectangle yearTitleRect;
        private Rectangle todayButtonRect;

        private Rectangle[] dayCellRects = Array.Empty<Rectangle> ();
        private Rectangle[] monthCellRects = Array.Empty<Rectangle> ();
        private Rectangle[] yearCellRects = Array.Empty<Rectangle> ();

        private Rectangle hoveredRect = Rectangle.Empty;

        private DateTime value = DateTime.Today;
        private DateTime displayMonth = new (DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime minDate = DateTimePicker.MinimumDateTime;
        private DateTime maxDate = DateTimePicker.MaximumDateTime;

        private DateTimePickerCalendarViewMode viewMode = DateTimePickerCalendarViewMode.Days;
        private int yearRangeStart;

        public DateTimePickerCalendar (DateTimePicker owner)
        {
            this.owner = owner ?? throw new ArgumentNullException (nameof (owner));

            SetControlBehavior (ControlBehaviors.Selectable, true);
            SetControlBehavior (ControlBehaviors.Hoverable, true);

            TabStop = true;
            Size = new Size (
                PaddingSize * 2 + CellWidth * 7,
                PaddingSize * 2 + HeaderHeight + DayHeaderHeight + CellHeight * WeekRows + FooterHeight);

            yearRangeStart = GetYearRangeStart (displayMonth.Year);
            UpdateLayoutRects ();
        }

        public DateTime Value {
            get => value;
            set {
                this.value = value.Date;
                displayMonth = new DateTime (this.value.Year, this.value.Month, 1);
                yearRangeStart = GetYearRangeStart (displayMonth.Year);
                Invalidate ();
            }
        }

        public DateTime MinDate {
            get => minDate;
            set {
                minDate = value.Date;
                Invalidate ();
            }
        }

        public DateTime MaxDate {
            get => maxDate;
            set {
                maxDate = value.Date;
                Invalidate ();
            }
        }

        internal DateTime DisplayMonth => displayMonth;
        internal DateTimePickerCalendarViewMode ViewMode => viewMode;
        internal int YearRangeStart => yearRangeStart;

        internal Rectangle PreviousButtonRectangle => prevButtonRect;
        internal Rectangle NextButtonRectangle => nextButtonRect;
        internal Rectangle MonthTitleRectangle => monthTitleRect;
        internal Rectangle YearTitleRectangle => yearTitleRect;
        internal Rectangle TodayButtonRectangle => todayButtonRect;

        internal Rectangle[] DayCellRectangles => dayCellRects;
        internal Rectangle[] MonthCellRectangles => monthCellRects;
        internal Rectangle[] YearCellRectangles => yearCellRects;

        internal Rectangle HoveredRectangle => hoveredRect;

        internal DayOfWeek FirstDayOfWeek => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

        protected override void OnResize (EventArgs e)
        {
            base.OnResize (e);
            UpdateLayoutRects ();
        }

        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            var newHover = HitTestInteractiveRectangle (e.Location);
            if (hoveredRect != newHover) {
                hoveredRect = newHover;
                Invalidate ();
            }
        }

        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            if (hoveredRect != Rectangle.Empty) {
                hoveredRect = Rectangle.Empty;
                Invalidate ();
            }
        }

        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (prevButtonRect.Contains (e.Location)) {
                NavigatePrevious ();
                return;
            }

            if (nextButtonRect.Contains (e.Location)) {
                NavigateNext ();
                return;
            }

            if (viewMode == DateTimePickerCalendarViewMode.Days) {
                if (monthTitleRect.Contains (e.Location)) {
                    viewMode = DateTimePickerCalendarViewMode.Months;
                    Invalidate ();
                    return;
                }

                if (yearTitleRect.Contains (e.Location)) {
                    viewMode = DateTimePickerCalendarViewMode.Years;
                    yearRangeStart = GetYearRangeStart (displayMonth.Year);
                    Invalidate ();
                    return;
                }

                if (todayButtonRect.Contains (e.Location)) {
                    GoToToday ();
                    return;
                }

                if (TryGetDateAt (e.Location, out var date)) {
                    if (date >= MinDate.Date && date <= MaxDate.Date)
                        owner.ApplyDropDownValue (date);
                }

                return;
            }

            if (viewMode == DateTimePickerCalendarViewMode.Months) {
                if (yearTitleRect.Contains (e.Location)) {
                    viewMode = DateTimePickerCalendarViewMode.Years;
                    yearRangeStart = GetYearRangeStart (displayMonth.Year);
                    Invalidate ();
                    return;
                }

                if (TryGetMonthAt (e.Location, out int month)) {
                    displayMonth = new DateTime (displayMonth.Year, month, 1);
                    viewMode = DateTimePickerCalendarViewMode.Days;
                    Invalidate ();
                }

                return;
            }

            if (viewMode == DateTimePickerCalendarViewMode.Years) {
                if (TryGetYearAt (e.Location, out int year)) {
                    displayMonth = new DateTime (year, displayMonth.Month, 1);
                    viewMode = DateTimePickerCalendarViewMode.Months;
                    Invalidate ();
                }
            }
        }

        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            switch (e.KeyCode) {
                case Keys.Escape:
                    if (viewMode != DateTimePickerCalendarViewMode.Days) {
                        viewMode = DateTimePickerCalendarViewMode.Days;
                        Invalidate ();
                    } else {
                        owner.CloseDropDown ();
                    }

                    e.Handled = true;
                    break;

                case Keys.Left:
                    NavigatePrevious ();
                    e.Handled = true;
                    break;

                case Keys.Right:
                    NavigateNext ();
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        internal DateTime GetFirstVisibleDate ()
        {
            var firstOfMonth = new DateTime (displayMonth.Year, displayMonth.Month, 1);
            int offset = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            return firstOfMonth.AddDays (-offset);
        }

        private void NavigatePrevious ()
        {
            switch (viewMode) {
                case DateTimePickerCalendarViewMode.Days:
                    displayMonth = displayMonth.AddMonths (-1);
                    break;
                case DateTimePickerCalendarViewMode.Months:
                    displayMonth = displayMonth.AddYears (-1);
                    break;
                case DateTimePickerCalendarViewMode.Years:
                    yearRangeStart -= 12;
                    break;
            }

            Invalidate ();
        }

        private void NavigateNext ()
        {
            switch (viewMode) {
                case DateTimePickerCalendarViewMode.Days:
                    displayMonth = displayMonth.AddMonths (1);
                    break;
                case DateTimePickerCalendarViewMode.Months:
                    displayMonth = displayMonth.AddYears (1);
                    break;
                case DateTimePickerCalendarViewMode.Years:
                    yearRangeStart += 12;
                    break;
            }

            Invalidate ();
        }

        private void GoToToday ()
        {
            var today = DateTime.Today;
            if (today < MinDate.Date)
                today = MinDate.Date;
            if (today > MaxDate.Date)
                today = MaxDate.Date;

            displayMonth = new DateTime (today.Year, today.Month, 1);
            viewMode = DateTimePickerCalendarViewMode.Days;
            Invalidate ();
        }

        private void UpdateLayoutRects ()
        {
            prevButtonRect = new Rectangle (PaddingSize, PaddingSize, 24, HeaderHeight);
            nextButtonRect = new Rectangle (Width - PaddingSize - 24, PaddingSize, 24, HeaderHeight);

            int titleX = prevButtonRect.Right + 4;
            int titleWidth = Width - titleX - (Width - nextButtonRect.Left) - 4;

            monthTitleRect = new Rectangle (titleX, PaddingSize, titleWidth / 2, HeaderHeight);
            yearTitleRect = new Rectangle (monthTitleRect.Right, PaddingSize, titleWidth - monthTitleRect.Width, HeaderHeight);

            int startX = PaddingSize;
            int startY = PaddingSize + HeaderHeight + DayHeaderHeight;

            dayCellRects = new Rectangle[42];
            for (int row = 0; row < WeekRows; row++) {
                for (int col = 0; col < 7; col++) {
                    int index = row * 7 + col;
                    dayCellRects[index] = new Rectangle (
                        startX + col * CellWidth,
                        startY + row * CellHeight,
                        CellWidth,
                        CellHeight);
                }
            }

            monthCellRects = new Rectangle[12];
            for (int row = 0; row < 3; row++) {
                for (int col = 0; col < 4; col++) {
                    int index = row * 4 + col;
                    monthCellRects[index] = new Rectangle (
                        PaddingSize + col * 52,
                        PaddingSize + HeaderHeight + 8 + row * 40,
                        48,
                        34);
                }
            }

            yearCellRects = new Rectangle[12];
            for (int row = 0; row < 3; row++) {
                for (int col = 0; col < 4; col++) {
                    int index = row * 4 + col;
                    yearCellRects[index] = new Rectangle (
                        PaddingSize + col * 52,
                        PaddingSize + HeaderHeight + 8 + row * 40,
                        48,
                        34);
                }
            }

            todayButtonRect = new Rectangle (
                PaddingSize,
                Height - PaddingSize - FooterHeight + 2,
                Width - PaddingSize * 2,
                FooterHeight - 4);
        }

        private Rectangle HitTestInteractiveRectangle (Point point)
        {
            if (prevButtonRect.Contains (point))
                return prevButtonRect;

            if (nextButtonRect.Contains (point))
                return nextButtonRect;

            if (monthTitleRect.Contains (point))
                return monthTitleRect;

            if (yearTitleRect.Contains (point))
                return yearTitleRect;

            if (viewMode == DateTimePickerCalendarViewMode.Days) {
                if (todayButtonRect.Contains (point))
                    return todayButtonRect;

                foreach (var rect in dayCellRects)
                    if (rect.Contains (point))
                        return rect;
            } else if (viewMode == DateTimePickerCalendarViewMode.Months) {
                foreach (var rect in monthCellRects)
                    if (rect.Contains (point))
                        return rect;
            } else {
                foreach (var rect in yearCellRects)
                    if (rect.Contains (point))
                        return rect;
            }

            return Rectangle.Empty;
        }

        private bool TryGetDateAt (Point point, out DateTime date)
        {
            var firstVisible = GetFirstVisibleDate ();

            for (int i = 0; i < dayCellRects.Length; i++) {
                if (dayCellRects[i].Contains (point)) {
                    date = firstVisible.AddDays (i);
                    return true;
                }
            }

            date = default;
            return false;
        }

        private bool TryGetMonthAt (Point point, out int month)
        {
            for (int i = 0; i < monthCellRects.Length; i++) {
                if (monthCellRects[i].Contains (point)) {
                    month = i + 1;
                    return true;
                }
            }

            month = 1;
            return false;
        }

        private bool TryGetYearAt (Point point, out int year)
        {
            for (int i = 0; i < yearCellRects.Length; i++) {
                if (yearCellRects[i].Contains (point)) {
                    year = yearRangeStart + i;
                    return true;
                }
            }

            year = displayMonth.Year;
            return false;
        }

        private static int GetYearRangeStart (int year)
        {
            return year - (year % 12);
        }
    }
}
