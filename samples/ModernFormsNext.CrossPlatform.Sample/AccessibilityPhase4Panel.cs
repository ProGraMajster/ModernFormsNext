using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Contains real shared controls for the separately enabled Phase 4 native accessibility checks.</summary>
/// <remarks>Opened only through the Android ACCESSIBILITY_PHASE4 intent; the historical Phase 3 fixture is unchanged.</remarks>
internal sealed class AccessibilityPhase4Panel : Panel
{
    internal LinkLabel Links { get; } = new() { Text = "P4 linked action", AccessibleName = "P4 Links" };
    internal NumericUpDown Number { get; } = new() { AccessibleName = "P4 Number", Minimum = 0, Maximum = 10, Value = 2, DecimalPlaces = 1, Increment = .5m, ReadOnly = true };
    internal DateTimePicker Date { get; } = new()
    {
        AccessibleName = "P4 Date", ShowCheckBox = true, ShowUpDown = true,
        Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Value = new DateTime(2026, 9, 12)
    };
    internal DateTimePicker WindowlessDate { get; } = new()
    {
        AccessibleName = "P4 Windowless date", Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd", Value = new DateTime(2026, 9, 12)
    };
    internal RichTextBox Editor { get; } = new() { AccessibleName = "P4 Rich editor", Text = "alpha 😀 beta\nsecond line", MultiLine = true };
    internal TextBox Password { get; } = new()
    {
        AccessibleName = "P4 Password", TextInputOptions = new TextInputOptions { Scope = TextInputScope.Password }
    };
    internal DataGridView Grid { get; } = new() { AccessibleName = "P4 Grid", ColumnHeadersVisible = true, RowHeadersVisible = true };
    internal ScrollableControl Viewport { get; } = new() { AccessibleName = "P4 Viewport", AutoScroll = true };
    internal Button FarButton { get; } = new() { Text = "P4 Far action", Top = 340, Left = 8, Width = 180, Height = 36 };
    internal int LinkInvocations { get; private set; }
    internal int FarInvocations { get; private set; }
    internal int ProtectedInvocations { get; private set; }
    internal string PrivateFixtureValue { get; } = Guid.NewGuid().ToString("N");
    internal TextBox ProtectedEditor { get; } = new();
    internal NumericUpDown ProtectedNumber { get; } = new() { Minimum = 0, Maximum = 10, Value = 2 };
    private readonly SensitivePanel protectedPanel = new() { AccessibleName = "P4 Protected group", AccessibleAutomationId = "p4-protected" };
    private readonly Label title = new() { Text = "Accessibility Phase 4 — explicit native fixture", Multiline = true };

    internal AccessibilityPhase4Panel()
    {
        Dock = DockStyle.Fill;
        Links.Links.Clear();
        Links.Links.Add(0, Links.Text.Length);
        Links.LinkClicked += (_, _) => LinkInvocations++;
        Password.Text = PrivateFixtureValue;
        var protectedChild = new Button
        {
            Text = PrivateFixtureValue, AccessibleName = PrivateFixtureValue,
            AccessibleDescription = PrivateFixtureValue, AccessibleAutomationId = PrivateFixtureValue,
            Bounds = new Rectangle(0, 0, 280, 36)
        };
        protectedChild.Click += (_, _) => ProtectedInvocations++;
        ProtectedEditor.Text = PrivateFixtureValue;
        ProtectedEditor.AccessibleName = ProtectedNumber.AccessibleName = PrivateFixtureValue;
        // Real text/range controls make protected-capability rejection observable. An ordinary
        // button must remain usable: sensitivity redacts payloads, rather than authorizing actions.
        protectedPanel.Controls.AddRange([protectedChild, ProtectedEditor, ProtectedNumber]);
        Grid.Columns.Add(new DataGridViewColumn("P4 Item") { Width = 190 });
        Grid.Columns.Add(new DataGridViewColumn("P4 Quantity") { Width = 120 });
        Grid.Rows.Add("Zulu", "9");
        Grid.Rows.Add("Alpha", "1");
        for (int index = 2; index < 20; index++) Grid.Rows.Add($"Row {index:00}", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Viewport.Controls.Add(new Label { Text = "P4 viewport origin", Bounds = new Rectangle(8, 4, 240, 30) });
        Viewport.Controls.Add(FarButton);
        FarButton.Click += (_, _) => FarInvocations++;
        Controls.AddRange([title, Links, Number, Date, WindowlessDate, Editor, Password, protectedPanel, Grid, Viewport]);
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int width = Math.Max(180, Width - 24), y = 8;
        Row(title, 32); Row(Links, 32); Row(Number, 36); Row(Date, 36); Row(WindowlessDate, 36);
        Row(Editor, 80); Row(Password, 36); Row(protectedPanel, 38); Row(Grid, 182); Row(Viewport, 108);
        int protectedWidth = protectedPanel.Width / 3;
        for (int index = 0; index < protectedPanel.Controls.Count; index++)
            protectedPanel.Controls[index].SetBounds(index * protectedWidth, 0, protectedWidth, 36);
        void Row(Control control, int height) { control.SetBounds(12, y, width, height); y += height + 5; }
    }

    // A real control's normal accessibility override defines protection for its existing subtree.
    // It does not manufacture fixture nodes or replace native/canonical tree traversal.
    private sealed class SensitivePanel : Panel
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new SensitivePeer(this);
        private sealed class SensitivePeer(Control owner) : ControlAccessibleObject(owner)
        {
            public override bool IsSensitive => true;
        }
    }
}
