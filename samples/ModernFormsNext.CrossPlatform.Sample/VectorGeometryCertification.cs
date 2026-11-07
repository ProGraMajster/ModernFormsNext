using System;
using System.Drawing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext.Validation;

/// <summary>A bounded shared vector example for Gallery and native Windows/Android runners.</summary>
/// <remarks>Uses production geometry, composition and input only. Create, mutate and dispose on the UI thread.</remarks>
internal sealed class VectorGeometryCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly CombinedGeometry combined;
    private readonly RectangleGeometry clip;
    private readonly ArcSegment arc;
    private readonly Panel clippedPanel;
    private readonly ModernFormsNext.Path areaPath;
    private int assertions;
    internal Panel Root { get; }
    internal int PaintCount => ((VectorRoot)Root).PaintCount;
    internal int Clicks { get; private set; }
    internal Rectangle ClipPixelBounds => new(clippedPanel.PointToScreen(Point.Empty), clippedPanel.LogicalToDeviceUnits(clippedPanel.Size));
    internal Point AreaCenterPixel => areaPath.PointToScreen(new Point(areaPath.LogicalToDeviceUnits(areaPath.Width / 2), areaPath.LogicalToDeviceUnits(areaPath.Height / 2)));

    internal VectorGeometryCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        Root = owner.Add(new VectorRoot { Dock = DockStyle.Fill, Name = "VectorGeometry", BackgroundBrush = new SolidColorBrush(Color.White) });
        var path = PathGeometry.Parse("M0 30 A30 30 0 1 1 60 30 A30 30 0 1 1 0 30 Z");
        arc = (ArcSegment)path.Figures[0].Segments[0];
        Root.Controls.Add(new ModernFormsNext.Path
        {
            Bounds = new(24, 20, 264, 100), Data = path, Stretch = GeometryStretch.Uniform,
            StrokeThickness = 0, Fill = new SolidColorBrush(Color.CornflowerBlue), Name = "EndpointArcs"
        });
        var rectangle = new RectangleGeometry(new(0, 0, 80, 60));
        var group = new GeometryGroup { Children = { rectangle, rectangle } };
        combined = new(GeometryCombineMode.Xor, group, new RectangleGeometry(new(40, 0, 80, 60)));
        areaPath = Root.Controls.Add(new ModernFormsNext.Path
        {
            Bounds = new(24, 144, 264, 90), Data = combined, Stretch = GeometryStretch.Fill,
            StrokeThickness = 0, Fill = new SolidColorBrush(Color.Gold), Name = "CombinedAreas"
        });
        clip = new RectangleGeometry(new(0, 0, 132, 90));
        clippedPanel = Root.Controls.Add(new Panel { Bounds = new(24, 258, 264, 90), ClipGeometry = clip, Name = "ClippedParent" });
        var target = clippedPanel.Controls.Add(new ModernFormsNext.Path
        {
            Dock = DockStyle.Fill, Data = new RectangleGeometry(new(0, 0, 264, 90)), Stretch = GeometryStretch.Fill,
            StrokeThickness = 0, Fill = new SolidColorBrush(Color.Gold), Name = "ClippedChild"
        });
        target.Click += (_, _) => { Clicks++; output("VECTOR_GEOMETRY:CHILD_CLICK=" + Clicks); };
        Root.Controls.Add(new Label { Bounds = new(24, 370, 280, 80), Multiline = true,
            Text = "Arcs / group + XOR or Union / shared parent clip\nOnly the gold half of the bottom card accepts a pointer." });
        Root.BringToFront();
    }

    internal void Exercise()
    {
        Check(Root.FindForm() is not null && Root.Scaling > 0, "attached-production-window");
        Check(combined.Geometry1 is GeometryGroup { Children.Count: 2 }, "shared-group-operands");
        Check(PathGeometry.Parse("M0 0 a5 10 30 0 1 10 20").ToPathData() == "M 0 0 A 5 10 30 0 1 10 20", "bounded-path-data-roundtrip");
        Check(combined.GetBounds().Width == 120, "boolean-area-bounds");
        output("VECTOR_GEOMETRY:MODEL:PASS:" + assertions);
    }
    internal void PrepareAlternative(bool alternative)
    {
        combined.Mode = alternative ? GeometryCombineMode.Union : GeometryCombineMode.Xor;
        clip.Rect = new(alternative ? 132 : 0, 0, 132, 90);
        arc.RotationAngle = alternative ? 25 : 0;
        Check(combined.Mode == (alternative ? GeometryCombineMode.Union : GeometryCombineMode.Xor) &&
            clip.Rect.X == (alternative ? 132 : 0), "alternative-model-" + alternative);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++; output("PASS vector-" + category);
    }
    public void Dispose() { owner.Remove(Root); Root.Dispose(); }
    private sealed class VectorRoot : Panel
    {
        internal int PaintCount { get; private set; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintCount++; }
    }
}
