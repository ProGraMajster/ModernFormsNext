using System.Globalization;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

/// <summary>Owns the real controls exercised by the independent Grid/Table/Calendar UIA client.</summary>
internal static class GridCalendarScenario
{
    internal static void Run()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        using var form = new Form { ClientSize = new(660, 440), Text = "ModernFormsNext native Grid and Calendar integration" };
        var grid = form.Controls.Add(new DataGridView {
            AccessibleAutomationId = "uia.grid.records", AccessibleName = "Records",
            Bounds = new(20, 20, 390, 160), RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect });
        grid.Columns.Add("Key", 140);
        grid.Columns.Add("Value", 180);
        for (int i = 0; i < 12; ++i) grid.Rows.Add(i.ToString("D2", CultureInfo.InvariantCulture), "value " + i);
        var original = grid.Rows[0];
        var status = form.Controls.Add(new Label { AccessibleAutomationId = "uia.grid.events", Bounds = new(20, 190, 600, 28) });
        var events = new List<string>();
        grid.CellBeginEdit += (_, _) => events.Add("begin");
        grid.CellValueChanged += (_, _) => events.Add("value");
        grid.CellEndEdit += (_, _) => { events.Add("end"); status.AccessibleName = string.Join(",", events); };
        void Button(string id, string text, int y, Action action)
        {
            var button = form.Controls.Add(new Button { AccessibleAutomationId = id, Text = text, Bounds = new(440, y, 180, 32) });
            button.Click += (_, _) => action();
        }
        Button("uia.grid.sort", "Sort descending", 20, () => grid.SortByColumn(0, SortOrder.Descending));
        Button("uia.grid.readonly", "Make read-only", 60, () => grid.ReadOnly = true);
        Button("uia.grid.remove", "Remove retained row", 100, () => grid.Rows.Remove(original));
        var picker = form.Controls.Add(new DateTimePicker {
            AccessibleAutomationId = "uia.calendar.picker", AccessibleName = "Appointment",
            Bounds = new(20, 240, 330, 36), ShowCheckBox = true,
            Format = DateTimePickerFormat.Short, Value = new(2026, 9, 11, 12, 30, 5) });
        var dateStatus = form.Controls.Add(new Label { AccessibleAutomationId = "uia.calendar.value", Bounds = new(20, 290, 600, 28) });
        picker.ValueChanged += (_, _) => dateStatus.AccessibleName = picker.Value.ToString("O", CultureInfo.InvariantCulture);
        // Readiness follows startup focus/layout. Tests cannot race Form.Show's final selection.
        form.Shown += (_, _) => Dispatcher.UIThread.Post(() => Console.WriteLine($"HWND:{form.PlatformHandle.Handle.ToInt64()}"));
        Application.Run(form);
    }
}
