using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class CheckedListBoxPanel : Panel
    {
        public CheckedListBoxPanel()
        {
            Controls.Add(new Label { Text = "CheckedListBox", Left = 10, Top = 10, Width = 180 });

            var permissions = Controls.Add(new CheckedListBox
            {
                Left = 10,
                Top = 35,
                Width = 180,
                Height = 125,
                ShowHover = true
            });

            permissions.Items.AddRange("Read", "Write", "Admin", "Audit");
            permissions.SetItemChecked(0, true);
            permissions.SetItemCheckState(2, CheckState.Indeterminate);

            Controls.Add(new Label { Text = "CheckOnClick", Left = 220, Top = 10, Width = 180 });

            var quick = Controls.Add(new CheckedListBox
            {
                Left = 220,
                Top = 35,
                Width = 180,
                Height = 125,
                CheckOnClick = true,
                ShowHover = true
            });

            quick.Items.AddRange("Create", "Update", "Delete", "Export");
            quick.SetItemChecked(1, true);

            Controls.Add(new Label { Text = "ItemCheck events", Left = 430, Top = 10, Width = 180 });

            var event_log = Controls.Add(new ListBox
            {
                Left = 430,
                Top = 35,
                Width = 260,
                Height = 125,
                ScrollbarAlwaysVisible = true
            });

            quick.ItemCheck += (_, e) =>
            {
                event_log.Items.Insert(0, $"{e.Index}: {e.CurrentValue} -> {e.NewValue}");

                while (event_log.Items.Count > 5)
                    event_log.Items.RemoveAt(event_log.Items.Count - 1);
            };

            Controls.Add(new Label { Text = "Disabled", Left = 10, Top = 190, Width = 180 });

            var disabled = Controls.Add(new CheckedListBox
            {
                Left = 10,
                Top = 215,
                Width = 180,
                Height = 100,
                Enabled = false
            });

            disabled.Items.AddRange("Offline", "Pending", "Mixed");
            disabled.SetItemChecked(0, true);
            disabled.SetItemCheckState(2, CheckState.Indeterminate);
        }
    }
}
