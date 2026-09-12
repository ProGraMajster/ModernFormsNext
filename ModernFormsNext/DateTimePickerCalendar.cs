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
    internal partial class DateTimePickerCalendar : Control
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
        private Size layoutSize;
        private SizeF layoutScale;

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
                focusedDate = this.value;
                PublishCalendarView();
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

        internal Rectangle PreviousButtonRectangle { get { UpdateLayoutRects(); return prevButtonRect; } }
        internal Rectangle NextButtonRectangle { get { UpdateLayoutRects(); return nextButtonRect; } }
        internal Rectangle MonthTitleRectangle { get { UpdateLayoutRects(); return monthTitleRect; } }
        internal Rectangle YearTitleRectangle { get { UpdateLayoutRects(); return yearTitleRect; } }
        internal Rectangle TodayButtonRectangle { get { UpdateLayoutRects(); return todayButtonRect; } }

        internal Rectangle[] DayCellRectangles { get { UpdateLayoutRects(); return dayCellRects; } }
        internal Rectangle[] MonthCellRectangles { get { UpdateLayoutRects(); return monthCellRects; } }
        internal Rectangle[] YearCellRectangles { get { UpdateLayoutRects(); return yearCellRects; } }

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
            UpdateLayoutRects();

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
            base.OnMouseDown(e);
            HandleCalendarPointer(e);
        }

        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown(e);
            HandleCalendarKey(e);
        }
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            UpdateLayoutRects();
            RenderManager.Render (this, e);
        }

        internal DateTime GetFirstVisibleDate ()
        {
            var firstOfMonth = new DateTime (displayMonth.Year, displayMonth.Month, 1);
            int offset = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            return firstOfMonth.AddDays (-offset);
        }

        private void UpdateLayoutRects ()
        {
            if (layoutSize == Size && layoutScale == ScaleFactor && dayCellRects.Length != 0) return;
            layoutSize = Size;
            layoutScale = ScaleFactor;
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
            prevButtonRect = ScaleCalendarRectangle(prevButtonRect);
            nextButtonRect = ScaleCalendarRectangle(nextButtonRect);
            monthTitleRect = ScaleCalendarRectangle(monthTitleRect);
            yearTitleRect = ScaleCalendarRectangle(yearTitleRect);
            todayButtonRect = ScaleCalendarRectangle(todayButtonRect);
            for (int i = 0; i < dayCellRects.Length; ++i) dayCellRects[i] = ScaleCalendarRectangle(dayCellRects[i]);
            for (int i = 0; i < monthCellRects.Length; ++i) monthCellRects[i] = ScaleCalendarRectangle(monthCellRects[i]);
            for (int i = 0; i < yearCellRects.Length; ++i) yearCellRects[i] = ScaleCalendarRectangle(yearCellRects[i]);
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

        private static int GetYearRangeStart (int year)
        {
            return Math.Clamp(year - (year % 12), 1, 9988);
        }
    }
}
