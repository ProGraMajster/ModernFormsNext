using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

internal static class GridCalendarScenario
{
    internal static int Run(string handle)
    {
        if (!long.TryParse(handle, out long raw) || raw == 0) return 2;
        try
        {
            var root = AutomationElement.FromHandle(new IntPtr(raw));
            AutomationElement Find(string id) => root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, id)) ?? throw new InvalidOperationException("Missing " + id);
            void Invoke(string id) => ((InvokePattern)Find(id).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            var records = Find("uia.grid.records");
            var grid = (GridPattern)records.GetCurrentPattern(GridPattern.Pattern);
            var table = (TablePattern)records.GetCurrentPattern(TablePattern.Pattern);
            bool counts = grid.Current.RowCount == 12 && grid.Current.ColumnCount == 2;
            var headers = table.Current.GetColumnHeaders();
            bool headerMetadata = headers.Length == 2 && headers[0].Current.Name == "Key" && headers[1].Current.Name == "Value"
                && table.Current.GetRowHeaders().Length == 12 && table.Current.RowOrColumnMajor == RowOrColumnMajor.RowMajor;
            var cell = grid.GetItem(0, 1);
            var item = (GridItemPattern)cell.GetCurrentPattern(GridItemPattern.Pattern);
            var tableItem = (TableItemPattern)cell.GetCurrentPattern(TableItemPattern.Pattern);
            bool itemMetadata = item.Current.Row == 0 && item.Current.Column == 1 && item.Current.RowSpan == 1 && item.Current.ColumnSpan == 1
                && Same(item.Current.ContainingGrid, records) && Same(tableItem.Current.GetColumnHeaderItems()[0], headers[1])
                && tableItem.Current.GetRowHeaderItems()[0].Current.Name == "1";
            var cellValue = (ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern);
            cellValue.SetValue("edited through UIA");
            bool editCommitted = cellValue.Current.Value == "edited through UIA" && Find("uia.grid.events").Current.Name == "begin,value,end";
            var cellSelection = (SelectionItemPattern)cell.GetCurrentPattern(SelectionItemPattern.Pattern);
            cellSelection.Select();
            var selected = ((SelectionPattern)records.GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection();
            bool selection = selected.Length == 1 && Same(selected[0], cell);
            var tail = grid.GetItem(11, 1);
            bool tailInitiallyOffscreen = tail.Current.IsOffscreen;
            ((ScrollItemPattern)tail.GetCurrentPattern(ScrollItemPattern.Pattern)).ScrollIntoView();
            bool revealed = !tail.Current.IsOffscreen && !tail.Current.BoundingRectangle.IsEmpty;
            Invoke("uia.grid.sort");
            bool identityAfterSort = item.Current.Row == 11 && Same(grid.GetItem(11, 1), cell)
                && cellValue.Current.Value == "edited through UIA";
            Invoke("uia.grid.readonly");
            bool readOnly = cellValue.Current.IsReadOnly && Denied(() => cellValue.SetValue("forbidden"))
                && cellValue.Current.Value == "edited through UIA";
            Invoke("uia.grid.remove");
            // UIA may substitute a default value for a stale property read. An action on the
            // retained pattern must fail; it cannot select another row at the former index.
            bool removedPeerDenied = Denied(cellSelection.Select)
                && grid.Current.RowCount == 11;

            var picker = Find("uia.calendar.picker");
            var toggle = (TogglePattern)picker.GetCurrentPattern(TogglePattern.Pattern);
            toggle.Toggle();
            bool uncheckedStillEnabled = picker.Current.IsEnabled && toggle.Current.ToggleState == ToggleState.Off;
            var checkbox = picker.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
            ((TogglePattern)checkbox.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            bool checkboxReenabled = toggle.Current.ToggleState == ToggleState.On;
            var expand = (ExpandCollapsePattern)picker.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            expand.Expand();
            bool expanded = expand.Current.ExpandCollapseState == ExpandCollapseState.Expanded;
            // Popup owns a separate native root. Never synthesize a second calendar under the picker.
            AutomationElement? calendar = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (calendar is null && DateTime.UtcNow < deadline)
            {
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, root.Current.ProcessId));
                foreach (AutomationElement window in windows)
                    calendar ??= window.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Calendar));
                if (calendar is null) Thread.Sleep(20);
            }
            if (calendar is null) throw new InvalidOperationException("Native calendar popup was not discoverable.");
            bool noDuplicateCalendar = picker.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Calendar)) is null;
            var calendarGrid = (GridPattern)calendar.GetCurrentPattern(GridPattern.Pattern);
            var calendarTable = (TablePattern)calendar.GetCurrentPattern(TablePattern.Pattern);
            bool calendarMetadata = calendarGrid.Current.RowCount == 6 && calendarGrid.Current.ColumnCount == 7
                && calendarTable.Current.GetColumnHeaders().Length == 7;
            AutomationElement? targetDate = null;
            for (int row = 0; row < 6; ++row)
                for (int col = 0; col < 7; ++col)
                {
                    var candidate = calendarGrid.GetItem(row, col);
                    if (candidate.Current.Name == "Sunday, September 13, 2026") targetDate = candidate;
                }
            if (targetDate is null) throw new InvalidOperationException("Expected date missing from native calendar.");
            ((SelectionItemPattern)targetDate.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            bool calendarCommitted = Find("uia.calendar.value").Current.Name == "2026-09-13T12:30:05.0000000"
                && expand.Current.ExpandCollapseState == ExpandCollapseState.Collapsed;
            bool retiredCalendarDenied = Denied(() => calendarGrid.GetItem(0, 0));
            Console.WriteLine(JsonSerializer.Serialize(new {
                Counts = counts, HeaderMetadata = headerMetadata, ItemMetadata = itemMetadata, EditCommitted = editCommitted,
                Selection = selection, TailInitiallyOffscreen = tailInitiallyOffscreen, Revealed = revealed,
                IdentityAfterSort = identityAfterSort, ReadOnly = readOnly, RemovedPeerDenied = removedPeerDenied,
                UncheckedStillEnabled = uncheckedStillEnabled, CheckboxReenabled = checkboxReenabled, Expanded = expanded,
                NoDuplicateCalendar = noDuplicateCalendar, CalendarMetadata = calendarMetadata,
                CalendarCommitted = calendarCommitted, RetiredCalendarDenied = retiredCalendarDenied }));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static bool Same(AutomationElement first, AutomationElement second) => first.GetRuntimeId().SequenceEqual(second.GetRuntimeId());
    private static bool Denied(Action action)
    {
        try { action(); return false; }
        catch (Exception error) when (error is InvalidOperationException or ElementNotAvailableException or COMException) { return true; }
    }
}
