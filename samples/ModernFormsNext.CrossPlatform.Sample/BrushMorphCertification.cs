using System;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Validation;

/// <summary>Exercises opt-in brush transitions through real native controls and the shared scheduler.</summary>
/// <remarks>UI-thread fixture shared by Windows and Android. Enabled-state changes are synthetic
/// input coverage. Native runners separately verify paints and committed pixels.</remarks>
internal sealed class BrushMorphCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly Panel encoded, linear, glass;
    private int assertions;
    internal Panel Root { get; }
    internal int PaintCount => ((PaintProbe)Root).Count;
    internal Point EncodedCenter => Center(encoded);
    internal Point LinearCenter => Center(linear);
    internal Point GlassCenter => Center(glass);
    // PointToScreen accepts device-pixel control coordinates on both native backends.
    private static Point Center(Control control) => control.PointToScreen(new Point(control.ScaledWidth / 2, control.ScaledHeight / 2));
    internal bool Immediate => !AnimationScheduler.Default.Policy.AnimationsEnabled ||
        AnimationScheduler.Default.Policy.ReducedMotion || AnimationScheduler.Default.Policy.DurationScale == 0 ||
        !AnimationScheduler.Default.GetPlatformDiagnostics().AnimationsEnabled;
    internal bool IsIntermediate => encoded.CurrentStyle.BackgroundBrush is SolidColorBrush a && a.PaintColor.R is > 96 and < 160 &&
        linear.CurrentStyle.BackgroundBrush is SolidColorBrush b && b.PaintColor.R > a.PaintColor.R + 35 &&
        glass.CurrentStyle.BackgroundBrush is GlassBrush g && g.Opacity is > 0.35f and < 0.65f;
    internal bool IsSettled => ReferenceEquals(encoded.CurrentStyle.BackgroundBrush, encoded.StyleDisabled.BackgroundBrush) &&
        ReferenceEquals(linear.CurrentStyle.BackgroundBrush, linear.StyleDisabled.BackgroundBrush) &&
        ReferenceEquals(glass.CurrentStyle.BackgroundBrush, glass.StyleDisabled.BackgroundBrush) &&
        AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0;

    internal BrushMorphCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        Root = owner.Add(new PaintProbe { Dock = DockStyle.Fill, BackColor = SKColors.LightGray, Name = "BrushMorph" });
        encoded = Card(20, "Encoded", ColorInterpolationSpace.Srgb, false);
        linear = Card(125, "Linear", ColorInterpolationSpace.LinearSrgb, false);
        glass = Card(230, "Glass", ColorInterpolationSpace.Srgb, true);
        Root.BringToFront();
    }

    private Panel Card(int left, string name, ColorInterpolationSpace space, bool noPaint)
    {
        var card = Root.Controls.Add(new Panel { Bounds = new(left, 40, 90, 90), Name = name });
        card.Style.BackgroundBrush = noPaint ? new NoBrush() : new SolidColorBrush(Color.Black);
        card.StyleDisabled.BackgroundBrush = noPaint
            ? new GlassBrush { Tint = Color.White, SecondaryTint = Color.White, ShowHighlight = false, ShowInnerBorder = false }
            : new SolidColorBrush(Color.White);
        card.StyleTransitions.Add(VisualState.Normal, VisualState.Disabled,
            new VisualStateTransition { Duration = TimeSpan.FromSeconds(4), Easing = Easings.Linear,
                BrushInterpolation = new() { ColorSpace = space, EnableExtendedMorphing = true } });
        return card;
    }

    internal void VerifySource()
    {
        Check(Root.FindForm() is not null && encoded.Created && linear.Created && glass.Created, "native-created-brush-tree");
        Check(ReferenceEquals(encoded.CurrentStyle.BackgroundBrush, encoded.Style.BackgroundBrush) &&
            ReferenceEquals(linear.CurrentStyle.BackgroundBrush, linear.Style.BackgroundBrush), "exact-authored-source-brushes");
        Check(glass.CurrentStyle.BackgroundBrush is NoBrush, "explicit-no-paint-source");
    }

    internal void Begin()
    {
        encoded.Enabled = linear.Enabled = glass.Enabled = false;
        output($"BRUSH_MORPH immediate={Immediate} scale={Root.Scaling}");
        Check(Immediate || AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 3,
            "three-existing-state-scheduler-owners-or-immediate");
    }

    internal void VerifyIntermediate()
    {
        Check(IsIntermediate, "distinct-temporal-rgb-and-no-paint-opacity");
        Check(((SolidColorBrush)encoded.Style.BackgroundBrush!).PaintColor.ToArgb() == Color.Black.ToArgb() &&
            ((SolidColorBrush)linear.StyleDisabled.BackgroundBrush!).PaintColor.ToArgb() == Color.White.ToArgb(),
            "authored-endpoint-colors-unchanged");
        Check(!ReferenceEquals(glass.CurrentStyle.BackgroundBrush, glass.StyleDisabled.BackgroundBrush), "private-glass-presentation");
    }

    internal void VerifySettled()
    {
        Check(IsSettled, "exact-targets-and-idle-scheduler");
        Check(((GlassBrush)glass.CurrentStyle.BackgroundBrush!).Opacity == 1, "exact-glass-target-opacity");
        Check(glass.Style.BackgroundBrush is NoBrush, "authored-no-paint-not-replaced");
    }

    internal void Restore()
    {
        foreach (var card in new[] { encoded, linear, glass })
        {
            card.StyleTransitions.Clear();
            card.Enabled = true;
        }
        Check(ReferenceEquals(encoded.CurrentStyle.BackgroundBrush, encoded.Style.BackgroundBrush) &&
            ReferenceEquals(linear.CurrentStyle.BackgroundBrush, linear.Style.BackgroundBrush) &&
            ReferenceEquals(glass.CurrentStyle.BackgroundBrush, glass.Style.BackgroundBrush), "exact-restored-sources");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "no-retired-animation-demand");
        output("BRUSH_MORPH assertions=" + assertions);
    }

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
