using System;
using System.Collections.Generic;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Drawing;
using ModernFormsNext.WindowKit.Input;

namespace ControlGallery.Panels;

/// <summary>Demonstrates opt-in profiling and five representative workloads through the normal control pipeline.</summary>
/// <remarks>This page owns its recording session and local shortcut. Leaving the page stops recording; it does not run a background benchmark or publish diagnostics.</remarks>
public sealed class PerformanceDiagnosticsPanel : BasePanel
{
    private readonly List<Button> actions = [];
    private readonly List<Panel> cards = [];
    private readonly Label heading;
    private readonly Label instructions;
    private readonly Label status;
    private readonly Panel stage;
    private readonly LinearGradientBrush gradient;
    private readonly DelegateCommand toggle;
    private readonly KeyBinding binding;
    private InputBindingCollection? pageBindings;
    private PerformanceProfiler? profiler;
    private bool alternate;
    private bool ready;
    private bool unloaded;
    private bool arranging;

    /// <summary>Creates the inactive demonstration. Select Start recording to enable measurement.</summary>
    public PerformanceDiagnosticsPanel()
    {
        AutoScroll = true;
        heading = Controls.Add(new Label { Text = "Performance diagnostics", Height = 32 });
        instructions = Controls.Add(new Label
        {
            Multiline = true,
            Text = "Start explicitly, then exercise gradients, clipping, animation, nested layout and local invalidation. The HUD shows recorded work; it does not drive frames. Ctrl+F12 toggles this page's HUD while focus is inside the page."
        });
        status = Controls.Add(new Label { Multiline = true, Text = "Recording is off. No profiler, sampling timer or benchmark loop is running." });
        stage = Controls.Add(new Panel { AutoScroll = true });
        stage.Style.Border.Width = 1;

        gradient = new LinearGradientBrush { Start = new PointF(0, 0), End = new PointF(1, 1) };
        gradient.GradientStops.AddRange([
            new GradientStop(Color.MediumPurple, 0),
            new GradientStop(Color.CornflowerBlue, 1)
        ]);
        for (int i = 0; i < 24; i++)
        {
            var card = stage.Controls.Add(new Panel
            {
                Bounds = new Rectangle(10 + i % 4 * 145, 10 + i / 4 * 90, 130, 75),
                BackgroundBrush = gradient,
                Padding = new Padding(5)
            });
            var nested = card.Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(3) });
            nested.Controls.Add(new Label { Dock = DockStyle.Fill, Text = $"Card {i + 1}", TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter });
            cards.Add(card);
        }
        cards[0].LayoutTransition = new LayoutTransition { Duration = TimeSpan.FromMilliseconds(600), Easing = Easings.EaseOut };

        AddAction("Start / stop", ToggleRecording);
        AddAction("Show / hide HUD", ToggleOverlay);
        AddAction("Compact / expanded", () => ChangeOverlay(o => o with { Mode = o.Mode == PerformanceOverlayMode.Compact ? PerformanceOverlayMode.Expanded : PerformanceOverlayMode.Compact }));
        AddAction("Next corner", () => ChangeOverlay(o => o with { Corner = (PerformanceOverlayCorner)(((int)o.Corner + 1) % 4) }));
        AddAction("Graph on / off", () => ChangeOverlay(o => o with { ShowFrameGraph = !o.ShowFrameGraph }));
        AddAction("Bounds / repaint", () => ChangeOverlay(o => o with { ShowControlBounds = !o.ShowControlBounds, ShowRepaintRegions = !o.ShowRepaintRegions, ShowClipBounds = !o.ShowClipBounds }));
        AddAction("Gradients", () => { alternate = !alternate; gradient.GradientStops[0].PaintColor = alternate ? Color.Teal : Color.MediumPurple; });
        AddAction("Clipping / scroll", () => { var scroll = stage.VerticalScrollProperties; scroll.Value = scroll.Value == scroll.Minimum ? scroll.Maximum : scroll.Minimum; });
        AddAction("Animate card", () => { alternate = !alternate; cards[0].Bounds = alternate ? new Rectangle(25, 15, 190, 90) : new Rectangle(10, 10, 130, 75); });
        AddAction("Nested layout", () => { alternate = !alternate; foreach (var card in cards) card.Padding = new Padding(alternate ? 10 : 5); });
        AddAction("Local invalidation", () => { cards[5].Invalidate(new Rectangle(0, 0, 12, 12)); status.Text = "Requested a small child region. Compare repainted buffers with cache reuse; the current native root may still repaint its full surface."; });
        AddAction("Read snapshot", ReadSnapshot);

        toggle = new DelegateCommand(ToggleOverlay, () => profiler is not null && !unloaded);
        binding = new KeyBinding(toggle, new KeyGesture(Keys.F12, KeyModifiers.Control));
        pageBindings = InputBindings;
        pageBindings.Add(binding);
        status.TextChanged += (_, _) => { if (ready) ArrangePage(); };
        ready = true;
        ArrangePage();
    }

    private void AddAction(string text, Action action)
        => actions.Add(Controls.Add(new Button { Text = text, Command = new DelegateCommand(action, () => !unloaded) }));

    private void ToggleRecording()
    {
        if (profiler is not null)
        {
            StopRecording();
            status.Text = "Recording stopped. The application keeps its normal rendering and input behavior.";
            return;
        }
        // One profiler is allowed per UI thread. An application-owned existing session is
        // preserved; this demo reports the conflict instead of replacing someone else's recorder.
        PerformanceProfiler started;
        try
        {
            started = PerformanceProfiler.Start(new PerformanceProfilerOptions
            {
                DetailedControls = true,
                TrackAllocations = true,
                TrackGarbageCollections = true,
                Overlay = new PerformanceOverlayOptions { Visible = true, ShowFrameGraph = true }
            });
        }
        catch (InvalidOperationException)
        {
            status.Text = "Recording could not start. Stop any existing profiler on this UI thread before starting this page's session.";
            return;
        }
        profiler = started;
        toggle.RaiseCanExecuteChanged();
        status.Text = "Recording is active with explicit detail/allocation tracking. The display uses the previous frame; Read snapshot also works while the HUD is hidden.";
        Invalidate();
    }

    private void ToggleOverlay() => ChangeOverlay(o => o with { Visible = !o.Visible });

    private void ChangeOverlay(Func<PerformanceOverlayOptions, PerformanceOverlayOptions> change)
    {
        if (profiler is { } active) active.OverlayOptions = change(active.OverlayOptions);
        else status.Text = "Select Start recording first. All workload buttons also work without profiling.";
    }

    private void ReadSnapshot()
    {
        if (profiler is not { } active) { status.Text = "Start recording to inspect a programmatic snapshot."; return; }
        var snapshot = active.Capture();
        status.Text = snapshot.LatestFrame is not { } frame ? "No completed frame yet. Exercise a workload, then read again."
            : $"Recorded {snapshot.TotalFrames} frames. Last source {frame.SourceId}: {frame.Duration.TotalMilliseconds:0.##} ms, {frame.Work.ControlsRepainted} repainted / {frame.Work.ControlCacheHits} reused. This snapshot is available with the HUD hidden. GPU/presentation timing is not inferred.";
    }

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ready) ArrangePage();
    }

    private void ArrangePage()
    {
        if (arranging || unloaded) return;
        arranging = true;
        try
        {
        int width = Math.Max(220, Width - 36);
        heading.Bounds = new Rectangle(16, 14, width, 32);
        int instructionHeight = MeasureLabelHeight(instructions, width);
        instructions.Bounds = new Rectangle(16, 50, width, instructionHeight);
        int minimumButtonWidth = Math.Max(155, (int)Math.Ceiling(TextMeasurer.MeasureText("Compact / expanded", Theme.UIFont, Theme.FontSize).Width) + 24);
        int columns = Math.Max(1, width / minimumButtonWidth);
        int buttonWidth = (width - (columns - 1) * 6) / columns;
        int buttonHeight = Math.Max(34, Theme.FontSize * 2 + 8);
        int top = 58 + instructionHeight;
        for (int i = 0; i < actions.Count; i++)
            actions[i].Bounds = new Rectangle(16 + i % columns * (buttonWidth + 6), top + i / columns * (buttonHeight + 6), buttonWidth, buttonHeight);
        top += ((actions.Count + columns - 1) / columns) * (buttonHeight + 6) + 6;
        status.Bounds = new Rectangle(16, top, width, MeasureLabelHeight(status, width));
        top += status.Height + 8;
        stage.Bounds = new Rectangle(16, top, width, 240);
        }
        finally { arranging = false; }
    }

    private static int MeasureLabelHeight(Label label, int width)
    {
        float scale = Math.Max(0.01f, label.ScaleFactor.Height);
        var measured = TextMeasurer.MeasureText(label.Text, label, new Size(Math.Max(1, (int)(width * scale)), int.MaxValue));
        return Math.Max(28, (int)Math.Ceiling(measured.Height / scale) + 8);
    }

    /// <inheritdoc/>
    protected override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        if (ready) ArrangePage();
    }

    private void StopRecording()
    {
        var previous = profiler;
        profiler = null;
        previous?.Dispose();
        toggle.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    public override void UnloadPanel()
    {
        if (unloaded) return;
        unloaded = true;
        var previousBindings = pageBindings;
        pageBindings = null;
        // Window closure can release the collection before page disposal. Its detached
        // contents are safe to inspect; do not reacquire a collection on a closed owner.
        try { if (previousBindings?.Contains(binding) == true) previousBindings.Remove(binding); }
        finally { StopRecording(); }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        try { if (disposing) UnloadPanel(); }
        finally { base.Dispose(disposing); }
    }
}
