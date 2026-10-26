using System;
using System.Drawing;
using ModernFormsNext.Animations;
using SkiaSharp;

namespace ModernFormsNext.Validation;

/// <summary>A bounded native example for shared visual-state text and corner metrics.</summary>
/// <remarks>Uses public production controls, state resolution, shaping and the shared scheduler.
/// Enabled-state changes exercise the same planner without claiming manual pointer/IME coverage.
/// UI thread only; removing the owned root retires all of its animation owners.</remarks>
internal sealed class VisualStateMetricCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly Button caption;
    private readonly TextBox editor;
    private readonly Rectangle captionBounds;
    private readonly Rectangle editorBounds;
    private float sourceTextWidth;
    private int assertions;

    internal Panel Root { get; }
    internal int PaintCount => ((PaintProbe)Root).Count;
    internal Rectangle CaptionScreenBounds => new(caption.PointToScreen(Point.Empty), caption.ScaledSize);
    internal Rectangle EditorScreenBounds => new(editor.PointToScreen(Point.Empty), editor.ScaledSize);
    internal bool Immediate => !AnimationScheduler.Default.Policy.AnimationsEnabled ||
        AnimationScheduler.Default.Policy.ReducedMotion || AnimationScheduler.Default.Policy.DurationScale == 0 ||
        !AnimationScheduler.Default.GetPlatformDiagnostics().AnimationsEnabled;
    internal bool IsSettled => ReferenceEquals(caption.CurrentStyle, caption.StyleDisabled) &&
        ReferenceEquals(editor.CurrentStyle, editor.StyleDisabled) &&
        AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0;

    internal VisualStateMetricCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        Root = owner.Add(new PaintProbe { Dock = DockStyle.Fill, Name = "VisualStateMetrics", BackColor = SKColors.LightGray });
        caption = Root.Controls.Add(new Button { Name = "MetricCaption", Bounds = new(24, 28, 300, 112),
            Text = "Shared typography", BackColor = SKColors.White, ForeColor = SKColors.Black, TabStop = false });
        editor = Root.Controls.Add(new TextBox { Name = "MetricEditor", Bounds = new(24, 172, 300, 54),
            Text = "Cache width", BackColor = SKColors.White, ForeColor = SKColors.Black, TabStop = false });
        Configure(caption);
        Configure(editor);
        captionBounds = caption.Bounds;
        editorBounds = editor.Bounds;
        Root.BringToFront();
    }

    private static void Configure(Control control)
    {
        control.Style.FontSize = 12;
        control.Style.LineHeight = 1;
        control.Style.LetterSpacing = 0;
        control.Style.Border.Radius = 2;
        control.StyleDisabled.FontSize = 20;
        control.StyleDisabled.LineHeight = 1.5;
        control.StyleDisabled.LetterSpacing = 0.75;
        control.StyleDisabled.Border.Radius = 18;
        var transition = new VisualStateTransition { Duration = TimeSpan.FromSeconds(4), Easing = Easings.Linear };
        control.StyleTransitions.Add(VisualState.Normal, VisualState.Disabled, transition);
        control.StyleTransitions.Add(VisualState.Disabled, VisualState.Normal, transition);
    }

    internal void VerifySource()
    {
        Check(Root.FindForm() is not null && caption.Created && editor.Created, "actual-created-metric-tree");
        Check(caption.CurrentStyle.GetFontSize() == 12 && editor.CurrentStyle.GetFontSize() == 12,
            "native-source-font-metrics");
        Check(ReferenceEquals(caption.Style.GetFont(), caption.StyleDisabled.GetFont()) &&
            ReferenceEquals(editor.Style.GetFont(), editor.StyleDisabled.GetFont()), "native-compatible-borrowed-typefaces");
        sourceTextWidth = MeasureEditorWidth();
        Check(sourceTextWidth > 0, "native-source-shared-shaping");
    }

    internal void Begin()
    {
        caption.Enabled = false;
        editor.Enabled = false;
        output($"VISUAL_STATE_METRICS immediate={Immediate} scale={Root.Scaling} platformScale={AnimationScheduler.Default.GetPlatformDiagnostics().PlatformDurationScale}");
        Check(caption.VisualState == VisualState.Disabled && editor.VisualState == VisualState.Disabled,
            "native-disabled-state-resolution");
        Check(Immediate || AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 2,
            "native-two-shared-scheduler-owners-or-immediate");
    }

    internal bool IsIntermediate()
        => caption.CurrentStyle.GetFontSize() is >= 14 and <= 18 &&
            editor.CurrentStyle.GetFontSize() is >= 14 and <= 18 &&
            caption.CurrentStyle.Border.GetRadius() is > 2 and < 18 &&
            AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 2;

    internal void VerifyIntermediate()
    {
        Check(IsIntermediate(), "native-intermediate-text-and-radius");
        Check(caption.CurrentStyle.GetLineHeight() is > 1 and < 1.5 &&
            editor.CurrentStyle.GetLetterSpacing() is > 0 and < 0.75, "native-intermediate-shared-typography");
        Check(caption.Bounds == captionBounds && editor.Bounds == editorBounds, "native-fixed-logical-bounds");
    }

    internal void VerifySettled()
    {
        Check(IsSettled, "native-exact-target-and-retired-ticks");
        Check(caption.CurrentStyle.GetFontSize() == 20 && editor.CurrentStyle.GetFontSize() == 20 &&
            caption.CurrentStyle.GetLineHeight() == 1.5 && editor.CurrentStyle.GetLetterSpacing() == 0.75 &&
            caption.CurrentStyle.Border.GetRadius() == 18, "native-exact-text-and-corner-targets");
        Check(caption.Font.Size == 12 && editor.Font.Size == 12 && caption.Style.Border.Radius == 2,
            "native-persistent-values-unchanged");
        Check(MeasureEditorWidth() > sourceTextWidth, "native-shared-shaping-width-increased");
        Check(caption.Bounds == captionBounds && editor.Bounds == editorBounds, "native-target-logical-bounds-stable");
    }

    internal void CancelAndRestore()
    {
        editor.Enabled = true;
        Check(Immediate || AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 1,
            "native-reverse-owner-started-or-immediate");
        Root.Controls.Remove(editor);
        Check(editor.CurrentStyle.GetFontSize() == 12 && AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0,
            "native-detach-retires-and-snaps-latest-metrics");
        Root.Controls.Add(editor);
        caption.StyleTransitions.Clear();
        caption.Enabled = true;
        Check(caption.CurrentStyle.GetFontSize() == 12 && editor.CurrentStyle.GetFontSize() == 12 &&
            caption.CurrentStyle.Border.GetRadius() == 2, "native-restored-source-metrics");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "native-no-retired-owner-demand");
        output("VISUAL_STATE_METRICS assertions=" + assertions);
    }

    private float MeasureEditorWidth()
        => TextMeasurer.MeasureText(editor.Text, editor, new Size(editor.ScaledWidth, int.MaxValue)).Width;

    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++;
        output("PASS " + category);
    }

    public void Dispose() { owner.Remove(Root); Root.Dispose(); }

    private sealed class PaintProbe : Panel
    {
        internal int Count { get; private set; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); Count++; }
    }
}
