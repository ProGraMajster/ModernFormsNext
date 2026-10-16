using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Validation;

/// <summary>Repeatable production-control workload shared by explicit Windows and Android hosts.</summary>
/// <remarks>
/// All methods run on the existing UI thread. Native hosts pace batches so paint/input can run
/// between them. Sources are synthetic and finite; this fixture introduces no application runtime,
/// virtualizer, timer, focus scope or semantic tree. Reports are observations, not human visual QA.
/// </remarks>
internal sealed class VirtualizationCertification : IDisposable
{
    internal const int SourceCount = 100_003;
    internal const int BatchCount = 120;
    private readonly Action<string> output;
    private readonly ObservableCollection<object> source;
    private readonly VariableList variable;
    private readonly Panel outer;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly List<long> retained = [];
    private long allocated, warmedCreated;
    private int step, assertions;
    internal ListView View { get; }
    internal int PaintCount => ((PaintedList)View).Paints;

    internal VirtualizationCertification(Form form, Action<string> output)
    {
        this.output = output;
        // Prepare the finite initial sources before attachment, as in the Gallery/startup path.
        // Native collection-change notifications are exercised after attachment below; issuing
        // 100,003 separate native initial-load events is not the paced scrolling workload.
        outer = new Panel { Bounds = new(8, 8, 300, 360), AutoScroll = true };
        View = outer.Controls.Add(new PaintedList
        {
            Bounds = new(8, 8, 260, 150), VirtualizationEnabled = true,
            ItemSize = new(110, 28), ItemsLayout = ItemsLayout.Stack
        });
        View.SuspendLayout();
        try
        {
            for (int index = 0; index < SourceCount; index++)
            {
                View.Items.Add($"Entry {index}");
                if (index % 25_000 == 0) output($"SOURCE prepared={index}");
            }
        }
        finally { View.ResumeLayout(); }
        variable = outer.Controls.Add(new VariableList
        {
            Bounds = new(8, 190, 260, 130), ItemSize = new(180, 24), VariableItemSize = true
        });
        source = new ObservableCollection<object>(Enumerable.Range(0, SourceCount).Select(index => (object)$"Value {index}"));
        variable.SetSource(source);
        form.Controls.Add(outer);
        output(FormattableString.Invariant($"HOST scale={View.ScaleFactor} logical={View.ItemCount} viewport={View.Size}"));
    }

    internal void Exercise(Action<Keys> nativeKey)
    {
        View.Select();
        nativeKey(Keys.End);
        Check(View.SelectedIndex == SourceCount - 1 && View.GetItemContainer(SourceCount - 1)?.Focused == true,
            "native-key-End-selection-focus");
        var selected = View.SelectedItem;
        var peer = View.AccessibilityObject.GetSelected()!;
        long peerId = peer.RuntimeId;
        View.ScrollToItem(0);
        Check(View.Focused && ReferenceEquals(selected, View.SelectedItem), "focus-retirement-preserves-selection");
        Check((peer.State & AccessibleStates.Offscreen) != 0 && peer.PerformAction(AccessibleActions.Focus), "logical-peer-reveal-focus");
        Check(peerId == peer.RuntimeId && View.GetItemContainer(SourceCount - 1)?.Focused == true, "stable-peer-recycled-focus");
        View.Items.Insert(0, new ListViewItem { Text = "Inserted" });
        Check(View.SelectedIndex == SourceCount && ReferenceEquals(selected, View.SelectedItem), "insert-occurrence");
        View.Items.Move(SourceCount, 50_000);
        Check(View.SelectedIndex == 50_000 && ReferenceEquals(selected, View.SelectedItem), "move-occurrence");
        View.Items.RemoveAt(0);
        Check(View.SelectedIndex == 49_999 && ReferenceEquals(selected, View.SelectedItem), "remove-before-occurrence");
        View.Items[49_999] = new ListViewItem { Text = "Replacement" };
        Check(View.SelectedIndex == -1, "replace-retires-occurrence");
        EventHandler? reenter = null;
        reenter = (_, _) => { View.SelectedIndexChanged -= reenter; View.SelectedIndex = 70_000; };
        View.SelectedIndexChanged += reenter;
        View.SelectedIndex = 20_000;
        Check(View.SelectedIndex == 70_000, "reentrant-selection-newest-wins");

        foreach (var layout in new[] { ItemsLayout.Stack, ItemsLayout.Wrap })
            foreach (var direction in new[] { Orientation.Vertical, Orientation.Horizontal })
            {
                View.ItemsLayout = layout;
                View.ItemOrientation = direction;
                View.ScrollToItem(90_000);
                Check(View.GetItemContainer(90_000) is not null && View.SelectedIndex == 70_000, $"geometry-{layout}-{direction}");
                VerifyBounded(View);
            }
        View.ItemsLayout = ItemsLayout.Stack;
        View.ItemOrientation = Orientation.Vertical;
        View.VariableItemSize = true;
        View.ScrollToItem(60_000);
        Check(View.GetItemContainer(60_000)!.Height == View.ItemSize.Height, "drawn-list-measures-cell-not-empty-focus-container");
        View.VariableItemSize = false;

        View.Location = new(400, 600);
        View.ScrollToItem(60_000);
        Check(outer.VerticalScrollProperties.Value > 0 && outer.HorizontalScrollProperties.Value > 0, "nested-reveal");
        outer.HorizontalScrollProperties.Value = 0;
        outer.VerticalScrollProperties.Value = 0;
        View.Location = new(8, 8);
        View.Size = new(230, 170);
        View.ScrollToItem(70_000);
        Check(View.GetItemContainer(70_000) is not null && View.SelectedIndex == 70_000, "resize-selection-target");
        View.Visible = false;
        View.ScrollToItem(75_000);
        View.Visible = true;
        View.PerformLayout();
        Check(View.GetItemContainer(75_000) is not null, "hide-show-target");

        variable.SelectedIndex = 50_000;
        variable.ScrollToItem(50_000);
        int anchor = variable.RealizedRange.StartIndex + variable.CacheItemCount;
        int top = variable.GetItemContainer(anchor)!.Top;
        variable.ExtraExtent = 7;
        variable.Font = new Font(variable.Font.FamilyName, variable.Font.SizeInPoints + 1);
        Check(variable.GetItemContainer(anchor)?.Top == top, "font-variable-anchor");
        source.Insert(0, "Duplicate");
        source.Move(50_001, 60_000);
        Check(variable.SelectedIndex == 60_000, "adapter-insert-move-occurrence");
        source[60_000] = "Replacement";
        Check(variable.SelectedIndex == -1, "adapter-replace");
        var copy = source.ToArray();
        source.Clear();
        Check(variable.Controls.Count == 0 && variable.ItemCount == 0, "adapter-reset-clears-realization");
        variable.SetSource(copy);
        variable.ScrollToItem(90_000);
        Check(variable.GetVirtualizationSnapshot().MeasuredItemCount > 0, "variable-extents-corrected");
        VerifyBounded(variable);

        Check(ThemeManager.Current.Apply(BuiltInThemes.Dark).Success, "dark-theme-applied");
        View.ScrollToItem(70_000);
        Check(View.SelectedIndex == 70_000, "dark-theme-selection");
        Check(ThemeManager.Current.Apply(BuiltInThemes.Light).Success, "light-theme-applied");
        Check(View.SelectedIndex == 70_000, "light-theme-selection");
        output($"MATRIX assertions={assertions}");
    }

    internal void WarmBatch()
    {
        for (int index = 0; index < 50; index++) ScrollStep();
    }

    internal void CompleteWarmup()
    {
        warmedCreated = View.GetVirtualizationSnapshot().CreatedContainerCount;
        Sample("warm");
    }

    internal void Batch(int batch)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < 50; index++) ScrollStep();
        allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Check(View.GetVirtualizationSnapshot().CreatedContainerCount == warmedCreated, "warmed-container-plateau", log: false);
        if (batch % 20 == 19) Sample($"batch-{batch + 1};last_batch_ms={milliseconds.ToString("F3", CultureInfo.InvariantCulture)}");
    }

    internal void Complete()
    {
        Sample("complete");
        // Numeric metric storage is already sized for this finite source. Allow runtime/render
        // caches while rejecting retained managed growth of a second large source/tree.
        Check(retained[^1] - retained[0] < 16 * 1024 * 1024, "retained-managed-growth-under-16MiB");
        output(FormattableString.Invariant($"SOAK steps={step} elapsed_ms={elapsed.Elapsed.TotalMilliseconds:F0} warmed_workload_allocated={allocated} assertions={assertions}"));
    }

    private void ScrollStep()
    {
        int target = (int)((long)step++ * 7919 % SourceCount);
        View.ScrollToItem(target);
        variable.ScrollToItem(target);
        Check(View.GetItemContainer(target) is not null && variable.GetItemContainer(target) is not null, "jump-target", log: false);
        VerifyBounded(View);
        VerifyBounded(variable);
    }

    private void VerifyBounded(ItemsControl view)
    {
        var snapshot = view.GetVirtualizationSnapshot();
        // Both fixture templates have a minimum primary extent of 24 and fixed cross-axis
        // cells of at least 110. A 230/260 by 130/170 viewport needs fewer than 40 rows/cells.
        Check(snapshot.RealizedContainerCount is > 0 and <= 40 && view.Controls.Count == snapshot.RealizedContainerCount,
            "live-controls-bounded", log: false);
        Check(snapshot.RecycledContainerCount <= 64 && snapshot.CreatedContainerCount <= 80, "pool-create-bounded", log: false);
        if (view == variable)
            for (int index = snapshot.RealizedRange.StartIndex; index < snapshot.RealizedRange.EndIndex; index++)
                Check(variable.GetItemContainer(index)?.Text == variable.ValueAt(index)?.ToString(), "binding-map", log: false);
    }

    private void Sample(string stage)
    {
        long memory = GC.GetTotalMemory(forceFullCollection: true);
        retained.Add(memory);
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var snapshot = View.GetVirtualizationSnapshot();
        var measured = variable.GetVirtualizationSnapshot();
        output(FormattableString.Invariant($"MEMORY {stage} elapsed_ms={elapsed.Elapsed.TotalMilliseconds:F0} managed={memory} private={process.PrivateMemorySize64} working={process.WorkingSet64} live={snapshot.RealizedContainerCount} pool={snapshot.RecycledContainerCount} created={snapshot.CreatedContainerCount} reused={snapshot.ReusedContainerCount} extent={snapshot.ContentExtent} measured={measured.MeasuredItemCount} variable_extent={measured.ContentExtent} layout_ms={measured.LastLayoutDuration.TotalMilliseconds:F3}"));
    }

    private void Check(bool condition, string category, bool log = true)
    {
        if (!condition) throw new InvalidOperationException($"Virtualization certification: {category}");
        assertions++;
        if (log) output("PASS " + category);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        var rows = View.Controls.Concat(variable.Controls).ToArray();
        int disposed = 0;
        foreach (var row in rows) row.Disposed += (_, _) => disposed++;
        outer.Parent?.Controls.Remove(outer);
        outer.Dispose();
        Check(disposed == rows.Length, "all-live-containers-disposed");
    }

    private sealed class VariableList : ItemsControl
    {
        internal int ExtraExtent;
        internal void SetSource(IList values) => SetItemsSource(values);
        internal object? ValueAt(int index) => GetItem(index);
        protected override double MeasureItemContainer(Control container, object? item, int index, Size estimatedSize)
            => 24 + (index % 7 == 0 ? 12 : 0) + ExtraExtent;
    }

    private sealed class PaintedList : ListView
    {
        internal int Paints;
        protected override void OnPaint(PaintEventArgs e) { Paints++; base.OnPaint(e); }
    }
}
