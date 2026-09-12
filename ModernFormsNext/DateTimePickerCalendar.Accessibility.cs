using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext;

internal partial class DateTimePickerCalendar
{
    protected override AccessibleObject CreateAccessibilityInstance() => new CalendarAccessibleObject(this);

    private sealed class CalendarAccessibleObject(DateTimePickerCalendar calendar) : ControlAccessibleObject(calendar)
    {
        private readonly Dictionary<int, CalendarPartAccessibleObject> parts = [];
        private long observedVersion = -1;
        private CalendarGridProvider? provider;
        // Popup disposal retires its input/tree ownership but need not dispose every child.
        // A retained provider belongs to this exact picker session, not merely a live control.
        internal DateTimePickerCalendar? Calendar => Owner is DateTimePickerCalendar { IsDisposed: false, Disposing: false } value
            && value.owner.IsCurrentCalendar(value) ? value : null;
        public override bool IsSensitive
        {
            get
            {
                if (Calendar is not { } current) return true;
                try
                {
                    // The popup has its own real semantic root. Carry only privacy from the
                    // logical picker owner, using the existing resolver rather than changing
                    // Parent or caching a classification that can change while it is open.
                    var lifetime = new AccessibilityControlLifetime(current.owner);
                    bool sensitive = PlatformAccessibilityPrivacy.HasSensitiveAncestor(
                        PlatformAccessibleObjectAdapter.From(current.owner.AccessibilityObject)!);
                    return sensitive || !lifetime.IsCurrent || !ReferenceEquals(Calendar, current);
                }
                catch
                {
                    // Custom owner privacy getters are application callbacks. Failure to
                    // establish the classification must never authorize payload disclosure.
                    return true;
                }
            }
        }
        internal DateTimePickerCalendar? ReadableCalendar
        {
            get
            {
                var current = Calendar;
                return current is not null && !IsSensitive && ReferenceEquals(Calendar, current) ? current : null;
            }
        }
        public override AccessibleControlType ControlType => AccessibleControlType.Calendar;
        public override AccessibleRole Role => AccessibleRole.Table;
        public override string? Name { get => ReadableCalendar is { } current ? current.AccessibleName ?? current.owner.AccessibleName ?? "Calendar" : null; set { if (Calendar is { } owner) owner.AccessibleName = value; } }
        public override string? Value { get => ReadableCalendar is { } owner ? owner.Value.ToString("D", CultureInfo.CurrentCulture) : null; set { } }
        public override AccessibleGridProvider? GridProvider => ReadableCalendar is not null ? provider ??= new(this) : null;
        internal CalendarPartAccessibleObject Part(int kind)
        {
            if (observedVersion != Calendar!.calendarViewVersion)
            {
                // Only current presentation peers are cached (at most 54). Retained peers carry
                // their captured date/view identity and become inert, never reused for another date.
                observedVersion = Calendar.calendarViewVersion;
                parts.Clear();
            }
            if (!parts.TryGetValue(kind, out var part)) parts.Add(kind, part = new(this, kind, observedVersion));
            return part;
        }
        public override int GetChildCount() => Calendar is null || View == AccessibilityView.Hidden ? 0
            : Calendar?.viewMode == DateTimePickerCalendarViewMode.Days ? 54 : 16;
        public override AccessibleObject? GetChild(int index)
        {
            if (index < 0 || index >= GetChildCount()) return null;
            return Part(Calendar?.viewMode == DateTimePickerCalendarViewMode.Days || index < 4 ? index : index + 8);
        }
        public override AccessibleObject? GetSelected()
        {
            if (ReadableCalendar is not { } owner) return null;
            int index = owner.viewMode switch
            {
                DateTimePickerCalendarViewMode.Days => (owner.Value - owner.GetFirstVisibleDate()).Days,
                DateTimePickerCalendarViewMode.Months when owner.Value.Year == owner.DisplayMonth.Year => owner.Value.Month - 1,
                DateTimePickerCalendarViewMode.Years => owner.Value.Year - owner.yearRangeStart,
                _ => -1
            };
            return index >= 0 && index < (owner.viewMode == DateTimePickerCalendarViewMode.Days ? 42 : 12) ? Part(12 + index) : null;
        }
        public override AccessibleObject? GetFocused()
        {
            if (Calendar is not { IsDisposed: false, Focused: true } owner) return null;
            int index = owner.viewMode == DateTimePickerCalendarViewMode.Days ? (owner.focusedDate - owner.GetFirstVisibleDate()).Days : owner.focusedCell;
            return index >= 0 && index < (owner.viewMode == DateTimePickerCalendarViewMode.Days ? 42 : 12) ? Part(12 + index) : this;
        }
        public override AccessibleObject? HitTest(int x, int y)
        {
            for (int i = GetChildCount() - 1; i >= 0; --i)
                if (GetChild(i)?.HitTest(x, y) is { } hit) return hit;
            return base.HitTest(x, y);
        }
    }

    private sealed class CalendarGridProvider(CalendarAccessibleObject root) : AccessibleGridProvider
    {
        public override int RowCount => root.ReadableCalendar is not { } owner ? 0 : owner.viewMode == DateTimePickerCalendarViewMode.Days ? 6 : 3;
        public override int ColumnCount => root.ReadableCalendar is not { } owner ? 0 : owner.viewMode == DateTimePickerCalendarViewMode.Days ? 7 : 4;
        public override bool IsTable => root.ReadableCalendar?.viewMode == DateTimePickerCalendarViewMode.Days;
        public override AccessibleObject? GetItem(int row, int column)
        {
            if (root.ReadableCalendar is not { } owner) return null;
            int rows = owner.viewMode == DateTimePickerCalendarViewMode.Days ? 6 : 3;
            int columns = owner.viewMode == DateTimePickerCalendarViewMode.Days ? 7 : 4;
            return row >= 0 && column >= 0 && row < rows && column < columns
                ? root.Part(12 + row * columns + column) : null;
        }
        public override IReadOnlyList<AccessibleObject> GetColumnHeaders()
            => root.ReadableCalendar?.viewMode != DateTimePickerCalendarViewMode.Days ? []
                : Enumerable.Range(5, 7).Select(i => (AccessibleObject)root.Part(i)).ToArray();
    }

    private sealed class CalendarPartAccessibleObject(CalendarAccessibleObject root, int kind, long version) : AccessibleObject
    {
        private DateTimePickerCalendar? Calendar => root.Calendar;
        private bool Attached => Calendar is { IsDisposed: false } owner && owner.calendarViewVersion == version;
        private bool IsCell => kind >= 12;
        private bool IsHeader => kind is >= 5 and < 12;
        private bool Available => Attached && Calendar!.CanInteract && root.View != AccessibilityView.Hidden;
        public override AccessibleObject? Parent => Attached ? root : null;
        public override AccessibilityView View => Attached ? root.View : AccessibilityView.Hidden;
        public override bool IsSensitive => root.IsSensitive;
        public override AccessibleControlType ControlType => IsCell ? AccessibleControlType.DataItem : IsHeader ? AccessibleControlType.HeaderItem : AccessibleControlType.Button;
        public override AccessibleRole Role => IsCell ? AccessibleRole.Cell : IsHeader ? AccessibleRole.ColumnHeader : AccessibleRole.PushButton;
        public override string? Name
        {
            get
            {
                if (!Attached) return null;
                var owner = Calendar!;
                var culture = CultureInfo.CurrentCulture;
                if (IsCell) return owner.viewMode switch
                {
                    DateTimePickerCalendarViewMode.Days => FormatDateName(owner.GetFirstVisibleDate().AddDays(kind - 12), "D", "yyyy-MM-dd", culture),
                    DateTimePickerCalendarViewMode.Months => FormatDateName(new DateTime(owner.DisplayMonth.Year, kind - 11, 1), "Y", "yyyy-MM", culture),
                    _ => (owner.yearRangeStart + kind - 12).ToString(culture)
                };
                if (IsHeader) return culture.DateTimeFormat.GetDayName((DayOfWeek)(((int)owner.FirstDayOfWeek + kind - 5) % 7));
                return kind switch { 0 => "Previous", 1 => "Next", 2 => culture.DateTimeFormat.GetMonthName(owner.DisplayMonth.Month),
                    3 => owner.DisplayMonth.Year.ToString(culture), _ => "Today" };
            }
            set { }
        }
        private static string FormatDateName(DateTime date, string format, string fallback, CultureInfo culture)
        {
            // Adjacent, unavailable cells still represent real Gregorian dates. Some culture
            // calendars (for example UmAlQura) cover only part of their first/last month. Keep
            // those labels unambiguous without asking that calendar to format an invalid date.
            var calendar = culture.DateTimeFormat.Calendar;
            return date < calendar.MinSupportedDateTime || date > calendar.MaxSupportedDateTime
                ? date.ToString(fallback, CultureInfo.InvariantCulture) : date.ToString(format, culture);
        }
        public override Rectangle Bounds
        {
            get
            {
                if (!Attached || Calendar is not { Visible: true } owner) return Rectangle.Empty;
                var bounds = IsCell ? (owner.viewMode == DateTimePickerCalendarViewMode.Days ? owner.DayCellRectangles
                    : owner.viewMode == DateTimePickerCalendarViewMode.Months ? owner.MonthCellRectangles : owner.YearCellRectangles)[kind - 12]
                    : IsHeader ? owner.DayHeaderRectangle(kind - 5) : kind switch
                    { 0 => owner.PreviousButtonRectangle, 1 => owner.NextButtonRectangle, 2 => owner.MonthTitleRectangle, 3 => owner.YearTitleRectangle, _ => owner.TodayButtonRectangle };
                return CalendarAccessibleGeometry.ToScreen(owner, bounds);
            }
        }
        public override AccessibleGridCellInfo? GridCell
        {
            get
            {
                if (!Attached || !IsCell || root.ReadableCalendar is not { } owner || !Attached) return null;
                int columns = owner.viewMode == DateTimePickerCalendarViewMode.Days ? 7 : 4;
                return new(root, (kind - 12) / columns, (kind - 12) % columns,
                    columnHeaders: owner.viewMode == DateTimePickerCalendarViewMode.Days ? [root.Part(5 + (kind - 12) % 7)] : null);
            }
        }
        public override AccessibleStates State
        {
            get
            {
                if (!Attached || Calendar is not { } owner) return AccessibleStates.Unavailable | AccessibleStates.Invisible | AccessibleStates.Offscreen;
                var state = root.State & ~(AccessibleStates.Focused | AccessibleStates.Focusable);
                if (Bounds.IsEmpty) state |= AccessibleStates.Offscreen;
                if (IsCell)
                {
                    state |= AccessibleStates.Selectable | AccessibleStates.Focusable;
                    if (ReferenceEquals(root.GetSelected(), this)) state |= AccessibleStates.Selected;
                    if (ReferenceEquals(root.GetFocused(), this)) state |= AccessibleStates.Focused;
                    if (!owner.CanActivateCell(kind - 12)) state |= AccessibleStates.Unavailable;
                }
                if (!IsHeader && SupportedActions == 0) state |= AccessibleStates.Unavailable;
                return Attached ? state : AccessibleStates.Unavailable | AccessibleStates.Invisible | AccessibleStates.Offscreen;
            }
        }
        public override AccessibleActions SupportedActions => !Available || IsHeader ? AccessibleActions.None : IsCell
            ? Calendar!.CanActivateCell(kind - 12) ? AccessibleActions.Select | AccessibleActions.Invoke | AccessibleActions.Focus : AccessibleActions.None
            : kind switch
            {
                0 when Calendar!.CanNavigate(-1) => AccessibleActions.Invoke,
                1 when Calendar!.CanNavigate(1) => AccessibleActions.Invoke,
                2 when Calendar!.viewMode == DateTimePickerCalendarViewMode.Days => AccessibleActions.Invoke,
                3 when Calendar!.viewMode != DateTimePickerCalendarViewMode.Years => AccessibleActions.Invoke,
                4 when Calendar!.viewMode == DateTimePickerCalendarViewMode.Days => AccessibleActions.Invoke,
                _ => AccessibleActions.None
            };
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (parameter is not null || action == 0 || (((int)action & ((int)action - 1)) != 0) || (SupportedActions & action) == 0) return false;
            var owner = Calendar!;
            if (IsCell)
            {
                if (action != AccessibleActions.Focus) return owner.ActivateCell(kind - 12);
                var lifetime = new AccessibilityControlLifetime(owner);
                owner.Select();
                if (!lifetime.IsCurrent || !Available) return false;
                if (owner.viewMode == DateTimePickerCalendarViewMode.Days) owner.focusedDate = owner.GetFirstVisibleDate().AddDays(kind - 12);
                else owner.focusedCell = kind - 12;
                owner.Invalidate();
                NotifyClients(AccessibleEvents.Focus);
                return true;
            }
            switch (kind)
            {
                case 0: return owner.NavigateCalendar(-1);
                case 1: return owner.NavigateCalendar(1);
                case 2: owner.ShowCalendarView(DateTimePickerCalendarViewMode.Months); return true;
                case 3: owner.ShowCalendarView(DateTimePickerCalendarViewMode.Years); return true;
                case 4: owner.GoToToday(); return true;
                default: return false;
            }
        }
        public override void DoDefaultAction() => PerformAction(AccessibleActions.Invoke);
        public override void Select(AccessibleSelection flags) => PerformAction((flags & AccessibleSelection.TakeFocus) != 0 ? AccessibleActions.Focus : AccessibleActions.Select);
        public override AccessibleObject? Navigate(AccessibleNavigation direction) => CalendarAccessibleGeometry.Navigate(this, direction);
    }
}
