using System;
using System.Drawing;
using ModernFormsNext.Animations;
using SkiaSharp;

namespace ModernFormsNext.Validation;

/// <summary>A finite inherited-layout example for native Windows and Android certification.</summary>
/// <remarks>Uses the production dispatcher, scheduler, compositor and semantic geometry. UI thread only.</remarks>
internal sealed class LayoutPolicyCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly Panel moving;
    private readonly Panel zero;
    private readonly LayoutTransition policy = new()
    { ApplyToDescendants = true, Duration = TimeSpan.FromMilliseconds(1200), Easing = Easings.Linear };
    private int assertions;
    internal Panel Root { get; }
    internal int PaintCount => ((PaintProbe)Root).Count;
    internal bool Immediate => !AnimationScheduler.Default.GetPlatformDiagnostics().AnimationsEnabled ||
        AnimationScheduler.Default.Policy.DurationScale == 0;
    internal Rectangle ZeroScreenBounds => new(zero.PointToScreen(Point.Empty), zero.ScaledSize);

    internal LayoutPolicyCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        Root = owner.Add(new PaintProbe { Dock = DockStyle.Fill, Name = "LayoutPolicy", BackColor = SKColors.White });
        moving = Root.Controls.Add(new Panel { Bounds = new(24, 32, 100, 72), BackColor = SKColors.MediumPurple });
        moving.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Inherited", TextAlign = ContentAlignment.MiddleCenter });
        zero = Root.Controls.Add(new Panel { Bounds = new(24, 140, 100, 40), BackColor = SKColors.Teal });
        Root.LayoutTransition = policy;
        Root.BringToFront();
    }

    internal void Begin()
    {
        Check(Root.FindForm() is not null && moving.Created && zero.Created, "actual-created-window-tree");
        Check(moving.LayoutTransition is null && zero.LayoutTransition is null, "inherited-values-stay-local-null");
        moving.Left = 180;
        Check(moving.Left == 180, "logical-target-committed");
        output($"LAYOUT_POLICY native-policy-immediate={Immediate} scale={Root.Scaling}");
        Check(Immediate || AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount > 0, "native-scheduler-demand-or-immediate-policy");
    }

    internal bool IsIntermediate()
    {
        int x = moving.AccessibilityObject.Bounds.Left;
        int origin = Root.PointToScreen(Point.Empty).X;
        return AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount > 0 &&
            x > origin + (int)(24 * moving.ScaleFactor.Width) && x < origin + moving.ScaledLeft;
    }

    internal void VerifyIntermediate()
    {
        Check(IsIntermediate(), "native-intermediate-semantic-geometry");
        Check(moving.AccessibilityObject.Bounds.Location == moving.PointToScreen(Point.Empty), "semantic-input-coordinate-parity");
        // Retarget from the current presentation, then cancel the inherited run through its owner policy.
        Point current = moving.PointToScreen(Point.Empty);
        moving.Left = 220;
        Check(moving.PointToScreen(Point.Empty) == current, "native-retarget-has-no-jump");
    }

    internal void SettleAndVerify()
    {
        policy.Enabled = false;
        // Ordinary composition truncates ScaledLeft, while LogicalToDeviceUnits rounds a distance.
        // Use the actual public composition origin instead of assuming they coincide at fractional DPI.
        output($"LAYOUT_POLICY snap actual={moving.PointToScreen(Point.Empty).X} expected={Root.PointToScreen(Point.Empty).X + moving.ScaledLeft}");
        Check(moving.PointToScreen(Point.Empty).X == Root.PointToScreen(Point.Empty).X + moving.ScaledLeft,
            "native-policy-cancel-snaps");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "native-layout-entries-retired");
        policy.Enabled = true;
        moving.LayoutTransition = new LayoutTransition { Enabled = false };
        moving.Left = 180;
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "native-local-disabled-wins");
        moving.LayoutTransition = null;
        output("LAYOUT_POLICY settled-assertions=" + assertions);
    }

    internal void CollapseAndVerify()
    {
        zero.Width = 0;
        Check(zero.Width == 0 && zero.AccessibilityObject.Bounds.Width == 0, "native-zero-target-has-no-area");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "native-zero-target-no-scheduler-demand");
    }

    internal void RestoreAndVerify()
    {
        zero.Width = 100;
        Check(zero.AccessibilityObject.Bounds.Width == zero.ScaledWidth, "native-zero-source-snaps-positive");
        Check(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0, "native-zero-source-no-scheduler-demand");
        output("LAYOUT_POLICY assertions=" + assertions);
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
