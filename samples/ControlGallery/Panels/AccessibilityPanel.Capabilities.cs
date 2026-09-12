using System;
using System.Drawing;
using System.Linq;
using ModernFormsNext;
using ModernFormsNext.Accessibility;

namespace ControlGallery.Panels;

public sealed partial class AccessibilityPanel
{
    private const string IdPrefix = "controlgallery.accessibility.";

    private void InitializeCapabilityPages()
    {
        AccessibleName = "Accessibility examples";
        AccessibleAutomationId = IdPrefix + "page";
        var tabs = new TabControl { Dock = DockStyle.Fill, ShowCloseButtons = false,
            AccessibleName = "Accessibility example groups", AccessibleAutomationId = IdPrefix + "tabs" };
        // Keep the original dynamic fixture and its IDs intact, but place it on its own
        // scrollable page so all examples remain reachable in a small Gallery window.
        var dynamic = AddPage(tabs, "Dynamic", "dynamic");
        foreach (var control in Controls.ToArray())
        {
            Controls.Remove(control);
            dynamic.Controls.Add(control);
        }
        AddGridPage(AddPage(tabs, "Grid / Date", "grid-page"));
        AddPartsPage(AddPage(tabs, "Link / Range", "parts-page"));
        AddTextPage(AddPage(tabs, "Text", "text-page"));
        AddViewportPage(AddPage(tabs, "Viewport", "viewport-page"));
        Controls.Add(tabs);
    }

    private static TabPage AddPage(TabControl tabs, string title, string id)
    {
        var page = tabs.TabPages.Add(title);
        page.AutoScroll = true;
        page.AccessibleName = title + " accessibility examples";
        page.AccessibleAutomationId = IdPrefix + id;
        return page;
    }

    private static Label AddCaption(Control page, int top, string text, int height = 30)
        => page.Controls.Add(new Label { Bounds = new(20, top, 640, height), Text = text,
            TextAlign = ModernFormsNext.ContentAlignment.TopLeft });

    private static Button AddExampleAction(Control page, int left, int top, int width,
        string text, string id, Action action)
    {
        var button = page.Controls.Add(new Button { Bounds = new(left, top, width, 34), Text = text,
            AccessibleAutomationId = IdPrefix + id });
        button.Click += (_, _) => action();
        return button;
    }

    private static void AddGridPage(TabPage page)
    {
        AddCaption(page, 16, "Grid / Table: inspect headers and cells; F2 edits the current cell. Sort or remove rows.", 42);
        var grid = page.Controls.Add(new DataGridView {
            Bounds = new(20, 62, 620, 230), RowHeadersVisible = true, RowHeadersWidth = 42,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            AccessibleName = "Sample order lines", AccessibleAutomationId = IdPrefix + "grid" });
        grid.Columns.Add("Product", 230);
        grid.Columns.Add("Quantity", 100);
        grid.Columns.Add("Status", 150);
        grid.Columns.Add("Notes", 230);
        int next = 0;
        void AddRow() { next++; grid.Rows.Add($"Sample item {next:00}", "2", "Ready", "Synthetic example"); }
        for (int i = 0; i < 18; i++) AddRow();
        var report = AddCaption(page, 302, "18 rows. Select a cell to inspect coordinates.");
        report.AccessibleAutomationId = IdPrefix + "grid-status";
        void Report() => report.Text = $"Rows: {grid.Rows.Count}; current column/row: {grid.CurrentCellAddress.X}/{grid.CurrentCellAddress.Y}; read-only: {grid.ReadOnly}";
        grid.SelectionChanged += (_, _) => Report();
        grid.CellValueChanged += (_, _) => Report();
        AddExampleAction(page, 20, 340, 140, "Add row", "grid-add", () => { AddRow(); Report(); });
        AddExampleAction(page, 174, 340, 140, "Sort descending", "grid-sort", () => { grid.SortByColumn(0, SortOrder.Descending); Report(); });
        AddExampleAction(page, 328, 340, 140, "Toggle read-only", "grid-readonly", () => { grid.ReadOnly = !grid.ReadOnly; Report(); });
        AddExampleAction(page, 482, 340, 140, "Remove last", "grid-remove", () => {
            if (grid.Rows.Count > 0) grid.Rows.RemoveAt(grid.Rows.Count - 1);
            Report();
        });

        AddCaption(page, 402, "Date: checkbox and calendar popup; second picker uses segment step buttons.", 38);
        var date = page.Controls.Add(new DateTimePicker { Bounds = new(20, 447, 280, 34),
            Format = DateTimePickerFormat.Short, ShowCheckBox = true,
            MinDate = new(2026, 1, 1), MaxDate = new(2026, 12, 31), Value = new(2026, 9, 12),
            AccessibleName = "Delivery date", AccessibleAutomationId = IdPrefix + "date" });
        var spin = page.Controls.Add(new DateTimePicker { Bounds = new(330, 447, 280, 34),
            Format = DateTimePickerFormat.Short, ShowUpDown = true,
            MinDate = new(2026, 1, 1), MaxDate = new(2026, 12, 31), Value = new(2026, 9, 12),
            AccessibleName = "Stepped date", AccessibleAutomationId = IdPrefix + "date-step" });
        var dateReport = AddCaption(page, 494, "Open the calendar and inspect the real day cells, heading and selected day.", 42);
        dateReport.AccessibleAutomationId = IdPrefix + "date-status";
        date.ValueChanged += (_, _) => dateReport.Text = "Calendar picker value changed.";
        spin.ValueChanged += (_, _) => dateReport.Text = "Segment picker value changed.";
    }

    private static void AddPartsPage(TabPage page)
    {
        AddCaption(page, 16, "Each link range has its own identity. Numeric field and scrollbar expose real ranges.", 42);
        var links = page.Controls.Add(new LinkLabel { Bounds = new(20, 66, 600, 44),
            Text = "Read guide or show help", TextAlign = ModernFormsNext.ContentAlignment.MiddleLeft,
            AccessibleName = "Example links", AccessibleAutomationId = IdPrefix + "links" });
        links.Links.Clear();
        links.Links.Add(5, 5).Name = IdPrefix + "link-guide";
        links.Links.Add(19, 4).Name = IdPrefix + "link-help";
        var linkReport = AddCaption(page, 118, "No external browser is opened; activation updates this counter.");
        linkReport.AccessibleAutomationId = IdPrefix + "links-status";
        int clicks = 0;
        links.LinkClicked += (_, _) => linkReport.Text = $"Link activations: {++clicks}";
        AddCaption(page, 173, "Quantity: -5.00 to 5.00 in 0.25 steps.");
        var number = page.Controls.Add(new NumericUpDown { Bounds = new(20, 211, 200, 36),
            Minimum = -5, Maximum = 5, DecimalPlaces = 2, Increment = 0.25m, Value = 1.5m,
            AccessibleName = "Quarter-step quantity", AccessibleAutomationId = IdPrefix + "number" });
        var rangeReport = AddCaption(page, 262, "Change the numeric editor, step buttons or scrollbar.");
        rangeReport.AccessibleAutomationId = IdPrefix + "range-status";
        int numericChanges = 0;
        number.ValueChanged += (_, _) => rangeReport.Text = $"Numeric value changes: {++numericChanges}";
        var bar = page.Controls.Add(new HorizontalScrollBar { Bounds = new(20, 316, 440, 24),
            Minimum = 0, Maximum = 100, SmallChange = 5, LargeChange = 20, Value = 35,
            AccessibleName = "Standalone horizontal range", AccessibleAutomationId = IdPrefix + "range-bar" });
        bar.ValueChanged += (_, _) => rangeReport.Text = $"Standalone range position: {bar.Value}";
        AddCaption(page, 362, "A standalone scrollbar is a numeric range. The Viewport tab exposes its owner's ScrollPattern.", 42);
    }

    private static void AddTextPage(TabPage page)
    {
        AddCaption(page, 16, "Text: inspect document/range geometry, move selection and compare read-only or protected input.", 42);
        var plain = page.Controls.Add(new TextBox { Bounds = new(20, 65, 620, 125), MultiLine = true,
            Text = "First synthetic line.\r\nSecond line contains café and 😀.\r\nSelect a word or reveal a text range.\r\nFourth line.\r\nFifth line.\r\nLast line.",
            AccessibleName = "Multiline plain text", AccessibleAutomationId = IdPrefix + "text-plain" });
        var rich = page.Controls.Add(new RichTextBox { Bounds = new(20, 240, 620, 145),
            Text = "Formatted sample\nBold heading, normal body and Unicode: café 😀.\nSelect across two formatting runs.",
            AccessibleName = "Rich text document", AccessibleAutomationId = IdPrefix + "text-rich" });
        rich.Select(0, "Formatted sample".Length);
        rich.SelectionFont = new ModernFormsNext.Font("Segoe UI", 14, ModernFormsNext.FontStyle.Bold);
        rich.SelectionColor = Theme.AccentColor;
        rich.DeselectAll();
        var report = AddCaption(page, 399, "Status reports counts only, without copying entered text.");
        report.AccessibleAutomationId = IdPrefix + "text-status";
        int changes = 0;
        plain.TextChanged += (_, _) => report.Text = $"Text changes: {++changes}";
        rich.TextChanged += (_, _) => report.Text = $"Text changes: {++changes}";
        AddExampleAction(page, 20, 198, 180, "Select first word", "text-select", () => {
            plain.SelectionStart = 0;
            plain.SelectionEnd = Math.Min(5, plain.Text.Length);
            plain.Select();
        });
        AddExampleAction(page, 215, 198, 180, "Toggle read-only", "text-readonly", () => {
            plain.ReadOnly = !plain.ReadOnly; report.Text = $"Plain text read-only: {plain.ReadOnly}";
        });
        AddCaption(page, 453, "Protected editor: its explicit label remains available; its entered value is redacted.", 38);
        page.Controls.Add(new TextBox { Bounds = new(20, 499, 300, 36), PasswordCharacter = '*',
            Text = "synthetic-only", AccessibleName = "Protected example", AccessibleAutomationId = IdPrefix + "text-protected" });
    }

    private static void AddViewportPage(TabPage page)
    {
        AddCaption(page, 16, "The inner viewport scrolls on both axes. Reveal moves the target without selecting it.", 42);
        var viewport = page.Controls.Add(new ScrollableControl { Bounds = new(20, 66, 520, 235), AutoScroll = true,
            AccessibleName = "Two-axis example viewport", AccessibleAutomationId = IdPrefix + "viewport" });
        viewport.Style.Border.Width = 1;
        viewport.Controls.Add(new Label { Bounds = new(12, 12, 250, 32), Text = "Start of the content" });
        viewport.Controls.Add(new Label { Bounds = new(390, 270, 250, 32), Text = "Middle of the content" });
        var target = viewport.Controls.Add(new Button { Bounds = new(780, 500, 140, 38), Text = "Far target",
            AccessibleName = "Far viewport target", AccessibleAutomationId = IdPrefix + "viewport-target" });
        var report = AddCaption(page, 363, "Use scrollbars, UIA Scroll / ScrollItem, or the buttons above.", 50);
        report.AccessibleAutomationId = IdPrefix + "viewport-status";
        void Report() {
            if (viewport.AccessibilityObject.ScrollInfo is { } info)
                report.Text = $"Horizontal {info.Horizontal.Percent:0}% / vertical {info.Vertical.Percent:0}%; target focused: {target.Focused}";
        }
        AddExampleAction(page, 20, 315, 150, "Scroll to start", "viewport-start", () => {
            viewport.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(0, 0)); Report();
        });
        AddExampleAction(page, 185, 315, 150, "Scroll to end", "viewport-end", () => {
            viewport.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, 100)); Report();
        });
        AddExampleAction(page, 350, 315, 190, "Reveal far target", "viewport-reveal", () => {
            target.AccessibilityObject.PerformAction(AccessibleActions.ScrollIntoView); Report();
        });
        viewport.Scroll += (_, _) => Report();
        target.Click += (_, _) => report.Text = "Far target invoked.";
        AddCaption(page, 430, "At a smaller window size, the containing page has its own viewport. Both use the normal framework tree.", 50);
    }
}
