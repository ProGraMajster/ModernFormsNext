using System.Drawing;

namespace ModernFormsNext.PerformanceLab;

/// <summary>Supplies a stable workload while the runner owns the window, profiler and report.</summary>
internal abstract class BenchmarkScenario
{
    internal abstract string Id { get; }
    internal abstract string WorkloadVersion { get; }
    internal abstract Control CreateContent();
    internal abstract void Begin(BenchmarkRun run);
}

internal sealed class StartupScenario : BenchmarkScenario
{
    internal override string Id => "startup";
    internal override string WorkloadVersion => "flow-96-controls-v1";

    internal override Control CreateContent()
    {
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill };
        content.SuspendLayout();
        try
        {
            for (int index = 0; index < 96; index++)
            {
                Control child = index % 4 == 0 ? new Button() : new Label();
                child.Text = FormattableString.Invariant($"Field {index:D2}");
                child.Size = new Size(140, 28);
                content.Controls.Add(child);
            }
        }
        finally
        {
            content.ResumeLayout();
        }
        return content;
    }

    internal override void Begin(BenchmarkRun run) => run.Complete();
}

internal sealed class VirtualizedScrollScenario : BenchmarkScenario
{
    private const int ItemCount = 100_000;
    private const int ItemHeight = 28;
    private const int CacheItems = 2;
    private const int StepPixels = 56;
    private const int StepIntervalMilliseconds = 16;
    private const int WarmupSteps = 12;
    private const int MeasuredSteps = 120;
    private CollectionView view = null!;

    internal override string Id => "virtualized-scroll";
    internal override string WorkloadVersion => "collection-100k-fixed-56px-v1";

    internal override Control CreateContent()
    {
        var rows = new string[ItemCount];
        for (int index = 0; index < rows.Length; index++)
            rows[index] = FormattableString.Invariant($"Entry {index:D6}");

        view = new CollectionView
        {
            Dock = DockStyle.Fill,
            ItemSize = new Size(360, ItemHeight),
            CacheItemCount = CacheItems,
            ItemsSource = rows
        };
        return view;
    }

    internal override void Begin(BenchmarkRun run)
    {
        var scroll = view.VerticalScrollProperties;
        int startPosition = scroll.Value;
        if (scroll.Maximum - startPosition < MeasuredSteps * StepPixels)
            throw new InvalidOperationException("The viewport does not expose the required scroll range.");

        int warmup = 0;
        int measured = 0;
        int resetPaint = 0;
        int lastStepPaint = 0;
        var initial = default(ItemVirtualizationSnapshot);

        run.Schedule(StepIntervalMilliseconds, WarmUp);

        void WarmUp()
        {
            scroll.Value += StepPixels;
            if (++warmup != WarmupSteps)
                return;

            resetPaint = run.PaintPasses;
            scroll.Value = startPosition;
            run.Schedule(StepIntervalMilliseconds, WaitForResetPaint);
        }

        void WaitForResetPaint()
        {
            // The first measured step follows a complete repaint of the reset viewport.
            if (run.PaintPasses == resetPaint)
                return;
            initial = view.GetVirtualizationSnapshot();
            run.BeginMeasurement();
            run.Schedule(StepIntervalMilliseconds, Step);
        }

        void Step()
        {
            lastStepPaint = run.PaintPasses;
            scroll.Value += StepPixels;
            if (++measured == MeasuredSteps)
                run.Schedule(StepIntervalMilliseconds, WaitForFinalPaint);
        }

        void WaitForFinalPaint()
        {
            if (run.PaintPasses == lastStepPaint)
                return;
            var final = view.GetVirtualizationSnapshot();
            if (scroll.Value - startPosition != MeasuredSteps * StepPixels ||
                final.RealizedRange.StartIndex <= initial.RealizedRange.StartIndex ||
                final.RealizedContainerCount >= final.ItemCount ||
                final.ReusedContainerCount <= initial.ReusedContainerCount)
                throw new InvalidOperationException("Expected movement and bounded container recycling were not observed.");

            run.Complete(new(ItemCount, ItemHeight, CacheItems, WarmupSteps, MeasuredSteps,
                StepPixels, StepIntervalMilliseconds, startPosition, scroll.Value, initial, final));
        }
    }
}
