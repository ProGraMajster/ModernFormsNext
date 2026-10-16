using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using ModernFormsNext;

namespace ControlGallery.Panels;

/// <summary>Exercises two consumers of the shared item host with 100,000 logical entries.</summary>
public sealed class VirtualizationPanel : BasePanel
{
    private readonly ObservableCollection<Entry> entries;
    private readonly EntryList rows;
    private readonly ListView tiles;
    private readonly Label status;
    private int jump = 50_000;

    /// <summary>Creates actual large sources, bounded templates, mutation/navigation controls and observed diagnostics.</summary>
    public VirtualizationPanel()
    {
        entries = new(Enumerable.Range(0, 100_000).Select(i => new Entry(i)));
        var body = Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) });
        rows = body.Controls.Add(new EntryList { Dock = DockStyle.Left, Width = 360, ItemSize = new Size(300, 32), VariableItemSize = true });
        rows.SetEntries(entries);
        tiles = body.Controls.Add(new ListView { Dock = DockStyle.Fill });
        // Dock consumes the front-most child first. Reserve the left consumer's width
        // before filling the remaining space so both production consumers stay visible.
        rows.BringToFront();
        tiles.SuspendLayout();
        try
        {
            foreach (var entry in entries) tiles.Items.Add(entry.ToString());
            tiles.VirtualizationEnabled = true;
        }
        finally { tiles.ResumeLayout(); }
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 112 });
        var jumpButton = tools.Controls.Add(new Button { Bounds = new Rectangle(8, 8, 120, 34), Text = "Jump to item" });
        var mutate = tools.Controls.Add(new Button { Bounds = new Rectangle(136, 8, 120, 34), Text = "Move first" });
        var metricsButton = tools.Controls.Add(new Button { Bounds = new Rectangle(264, 8, 140, 34), Text = "Read diagnostics" });
        status = tools.Controls.Add(new Label { Bounds = new Rectangle(8, 48, 900, 60), Multiline = true,
            Text = "100,000 entries. Left: reusable ItemsControl with variable stack extents. Right: virtualized ListView tiles.\nUse wheel, Home/End/PageUp/PageDown, resize, change theme and move between DPI monitors." });
        jumpButton.Click += (_, _) =>
        {
            rows.SelectedIndex = jump;
            tiles.SelectedIndex = jump;
            rows.ScrollToItem(jump);
            tiles.ScrollToItem(jump);
            jump = jump == 50_000 ? 99_999 : 50_000;
            UpdateDiagnostics();
        };
        mutate.Click += (_, _) => { entries.Move(0, entries.Count - 1); tiles.Items.Move(0, tiles.Items.Count - 1); UpdateDiagnostics(); };
        metricsButton.Click += (_, _) => UpdateDiagnostics();
        rows.Scroll += (_, _) => UpdateDiagnostics();
        tiles.Scroll += (_, _) => UpdateDiagnostics();
    }

    private void UpdateDiagnostics()
    {
        var left = rows.GetVirtualizationSnapshot();
        var right = tiles.GetVirtualizationSnapshot();
        status.Text = $"Stack: {left.ItemCount:N0} logical / {left.RealizedContainerCount} live / {left.RecycledContainerCount} pool; created {left.CreatedContainerCount}, reused {left.ReusedContainerCount}; measured {left.MeasuredItemCount}, extent {left.ContentExtent.Height:N0}, layout {left.LastLayoutDuration.TotalMilliseconds:F3} ms.\n" +
            $"ListView: {right.ItemCount:N0} logical / {right.RealizedContainerCount} live / {right.RecycledContainerCount} pool; created {right.CreatedContainerCount}, reused {right.ReusedContainerCount}; range {right.RealizedRange.StartIndex}..{right.RealizedRange.EndIndex}.";
    }

    private sealed record Entry(int Number)
    {
        public override string ToString() => $"Entry {Number:N0}";
    }

    private sealed class EntryList : ItemsControl
    {
        internal void SetEntries(IList source) => SetItemsSource(source);
        protected override double MeasureItemContainer(Control row, object? item, int index, Size estimate)
            => item is Entry entry && entry.Number % 7 == 0 ? 48 : 32;
    }
}
