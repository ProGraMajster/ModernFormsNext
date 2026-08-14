using System.Drawing;
using System.Globalization;
using System.Numerics;
using ModernFormsNext.Drawing;

namespace ModernFormsNext.Designer.Properties;

/// <summary>Provides structured editing for PathGeometry figures and supported segment types.</summary>
internal sealed class DesignerPathGeometryDialog : Form
{
    private readonly PathGeometry geometry;
    private readonly ComboBox fillRuleCombo;
    private readonly TextBox transformTextBox;
    private readonly ListBox figureList;
    private readonly TextBox startXTextBox;
    private readonly TextBox startYTextBox;
    private readonly CheckBox isClosedCheckBox;
    private readonly ListBox segmentList;
    private readonly ComboBox segmentTypeCombo;
    private readonly TextBox control1XTextBox;
    private readonly TextBox control1YTextBox;
    private readonly TextBox control2XTextBox;
    private readonly TextBox control2YTextBox;
    private readonly TextBox endXTextBox;
    private readonly TextBox endYTextBox;
    private readonly Label errorLabel;
    private bool changingSelection;

    public DesignerPathGeometryDialog(PathGeometry? source)
    {
        geometry = Clone(source ?? new PathGeometry());

        Text = "PathGeometry Editor";
        Name = nameof(DesignerPathGeometryDialog);
        Size = new Size(930, 610);
        MinimumSize = new Size(860, 560);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label { Left = 18, Top = 16, Width = 70, Height = 22, Text = "Fill rule" });
        fillRuleCombo = Controls.Add(new ComboBox { Left = 92, Top = 12, Width = 120, Height = 28 });
        fillRuleCombo.Items.AddRange(Enum.GetNames<GeometryFillRule>().Cast<object>().ToArray());
        fillRuleCombo.SelectedItem = geometry.FillRule.ToString();
        Controls.Add(new Label { Left = 232, Top = 16, Width = 76, Height = 22, Text = "Transform" });
        transformTextBox = Controls.Add(new TextBox { Left = 312, Top = 12, Width = 566, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
        transformTextBox.Text = FormatMatrix(geometry.Transform);

        Controls.Add(new Label { Left = 18, Top = 56, Width = 230, Height = 22, Text = "Figures (rendering order)" });
        figureList = Controls.Add(new ListBox { Left = 18, Top = 82, Width = 250, Height = 190 });
        var addFigureButton = Controls.Add(new Button { Left = 18, Top = 280, Width = 54, Height = 30, Text = "Add" });
        var removeFigureButton = Controls.Add(new Button { Left = 80, Top = 280, Width = 70, Height = 30, Text = "Remove" });
        var upFigureButton = Controls.Add(new Button { Left = 158, Top = 280, Width = 50, Height = 30, Text = "Up" });
        var downFigureButton = Controls.Add(new Button { Left = 216, Top = 280, Width = 52, Height = 30, Text = "Down" });

        Controls.Add(new Label { Left = 290, Top = 56, Width = 180, Height = 22, Text = "Selected figure" });
        Controls.Add(new Label { Left = 290, Top = 88, Width = 52, Height = 22, Text = "Start X" });
        startXTextBox = Controls.Add(new TextBox { Left = 350, Top = 84, Width = 100, Height = 28 });
        Controls.Add(new Label { Left = 466, Top = 88, Width = 52, Height = 22, Text = "Start Y" });
        startYTextBox = Controls.Add(new TextBox { Left = 526, Top = 84, Width = 100, Height = 28 });
        isClosedCheckBox = Controls.Add(new CheckBox { Left = 646, Top = 86, Width = 110, Height = 26, Text = "IsClosed" });
        var applyFigureButton = Controls.Add(new Button { Left = 770, Top = 82, Width = 108, Height = 30, Text = "Apply Figure", Anchor = AnchorStyles.Top | AnchorStyles.Right });

        Controls.Add(new Label { Left = 290, Top = 126, Width = 230, Height = 22, Text = "Segments (figure order)" });
        segmentList = Controls.Add(new ListBox { Left = 290, Top = 152, Width = 336, Height = 160 });
        segmentTypeCombo = Controls.Add(new ComboBox { Left = 646, Top = 152, Width = 138, Height = 28 });
        segmentTypeCombo.Items.AddRange(["Line", "Quadratic", "Cubic"]);
        segmentTypeCombo.SelectedIndex = 0;
        var addSegmentButton = Controls.Add(new Button { Left = 792, Top = 150, Width = 86, Height = 30, Text = "Add", Anchor = AnchorStyles.Top | AnchorStyles.Right });
        var removeSegmentButton = Controls.Add(new Button { Left = 646, Top = 190, Width = 86, Height = 30, Text = "Remove" });
        var upSegmentButton = Controls.Add(new Button { Left = 740, Top = 190, Width = 64, Height = 30, Text = "Up" });
        var downSegmentButton = Controls.Add(new Button { Left = 812, Top = 190, Width = 66, Height = 30, Text = "Down", Anchor = AnchorStyles.Top | AnchorStyles.Right });

        int editorTop = 334;
        Controls.Add(new Label { Left = 18, Top = editorTop, Width = 300, Height = 22, Text = "Selected segment coordinates" });
        control1XTextBox = AddCoordinateEditor("Control 1 X", 18, editorTop + 32);
        control1YTextBox = AddCoordinateEditor("Control 1 Y", 310, editorTop + 32);
        control2XTextBox = AddCoordinateEditor("Control 2 X", 18, editorTop + 70);
        control2YTextBox = AddCoordinateEditor("Control 2 Y", 310, editorTop + 70);
        endXTextBox = AddCoordinateEditor("End X", 18, editorTop + 108);
        endYTextBox = AddCoordinateEditor("End Y", 310, editorTop + 108);
        var applySegmentButton = Controls.Add(new Button { Left = 646, Top = editorTop + 106, Width = 126, Height = 30, Text = "Apply Segment" });

        errorLabel = Controls.Add(new Label { Left = 18, Top = 492, Width = 620, Height = 48, Text = string.Empty, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right });
        var okButton = Controls.Add(new Button { Left = 710, Top = 510, Width = 78, Height = 30, Text = "OK", Anchor = AnchorStyles.Bottom | AnchorStyles.Right });
        var cancelButton = Controls.Add(new Button { Left = 800, Top = 510, Width = 78, Height = 30, Text = "Cancel", Anchor = AnchorStyles.Bottom | AnchorStyles.Right });

        figureList.SelectedIndexChanged += (_, _) => { if (!changingSelection) { RefreshSegments(0); LoadFigure(); } };
        segmentList.SelectedIndexChanged += (_, _) => { if (!changingSelection) LoadSegment(); };
        addFigureButton.Click += (_, _) => { geometry.Figures.Add(new PathFigure()); RefreshFigures(geometry.Figures.Count - 1); };
        removeFigureButton.Click += (_, _) => RemoveFigure();
        upFigureButton.Click += (_, _) => MoveFigure(-1);
        downFigureButton.Click += (_, _) => MoveFigure(1);
        applyFigureButton.Click += (_, _) => ApplyFigure();
        addSegmentButton.Click += (_, _) => AddSegment();
        removeSegmentButton.Click += (_, _) => RemoveSegment();
        upSegmentButton.Click += (_, _) => MoveSegment(-1);
        downSegmentButton.Click += (_, _) => MoveSegment(1);
        applySegmentButton.Click += (_, _) => ApplySegment();
        okButton.Click += (_, _) => Commit();
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        RefreshFigures(geometry.Figures.Count > 0 ? 0 : -1);
    }

    public PathGeometry Geometry => Clone(geometry);

    private TextBox AddCoordinateEditor(string label, int left, int top)
    {
        Controls.Add(new Label { Left = left, Top = top + 4, Width = 84, Height = 22, Text = label });
        return Controls.Add(new TextBox { Left = left + 90, Top = top, Width = 170, Height = 28 });
    }

    private void RemoveFigure()
    {
        int index = figureList.SelectedIndex;
        if (index < 0 || index >= geometry.Figures.Count)
            return;
        geometry.Figures.RemoveAt(index);
        RefreshFigures(Math.Min(index, geometry.Figures.Count - 1));
    }

    private void MoveFigure(int delta)
    {
        int oldIndex = figureList.SelectedIndex;
        int newIndex = oldIndex + delta;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= geometry.Figures.Count)
            return;
        geometry.Figures.Move(oldIndex, newIndex);
        RefreshFigures(newIndex);
    }

    private bool ApplyFigure()
    {
        PathFigure? figure = SelectedFigure();
        if (figure is null)
            return true;
        if (!TryPoint(startXTextBox, startYTextBox, out PointF start))
            return Fail("StartPoint must contain finite invariant X and Y values.");
        figure.StartPoint = start;
        figure.IsClosed = isClosedCheckBox.Checked;
        errorLabel.Text = string.Empty;
        RefreshFigures(figureList.SelectedIndex);
        return true;
    }

    private void AddSegment()
    {
        PathFigure? figure = SelectedFigure();
        if (figure is null)
            return;
        PathSegment segment = segmentTypeCombo.SelectedIndex switch
        {
            1 => new QuadraticBezierSegment(),
            2 => new BezierSegment(),
            _ => new LineSegment()
        };
        figure.Segments.Add(segment);
        RefreshSegments(figure.Segments.Count - 1);
    }

    private void RemoveSegment()
    {
        PathFigure? figure = SelectedFigure();
        int index = segmentList.SelectedIndex;
        if (figure is null || index < 0 || index >= figure.Segments.Count)
            return;
        figure.Segments.RemoveAt(index);
        RefreshSegments(Math.Min(index, figure.Segments.Count - 1));
    }

    private void MoveSegment(int delta)
    {
        PathFigure? figure = SelectedFigure();
        int oldIndex = segmentList.SelectedIndex;
        int newIndex = oldIndex + delta;
        if (figure is null || oldIndex < 0 || newIndex < 0 || newIndex >= figure.Segments.Count)
            return;
        figure.Segments.Move(oldIndex, newIndex);
        RefreshSegments(newIndex);
    }

    private bool ApplySegment()
    {
        PathSegment? segment = SelectedSegment();
        if (segment is null)
            return true;
        if (!TryPoint(endXTextBox, endYTextBox, out PointF end))
            return Fail("End point must contain finite invariant X and Y values.");
        switch (segment)
        {
            case LineSegment line:
                line.Point = end;
                break;
            case QuadraticBezierSegment quadratic:
                if (!TryPoint(control1XTextBox, control1YTextBox, out PointF quadraticControl))
                    return Fail("Quadratic control point must contain finite invariant X and Y values.");
                quadratic.ControlPoint = quadraticControl;
                quadratic.Point = end;
                break;
            case BezierSegment cubic:
                if (!TryPoint(control1XTextBox, control1YTextBox, out PointF control1)
                    || !TryPoint(control2XTextBox, control2YTextBox, out PointF control2))
                    return Fail("Cubic control points must contain finite invariant X and Y values.");
                cubic.ControlPoint1 = control1;
                cubic.ControlPoint2 = control2;
                cubic.Point = end;
                break;
        }
        errorLabel.Text = string.Empty;
        RefreshSegments(segmentList.SelectedIndex);
        return true;
    }

    private void Commit()
    {
        // Persist the selected segment before refreshing the figure list; refreshing a figure also
        // reloads its segment selection and would otherwise discard pending coordinate edits.
        if (!ApplySegment() || !ApplyFigure())
            return;
        if (!Enum.TryParse(fillRuleCombo.SelectedItem?.ToString(), out GeometryFillRule fillRule))
        {
            Fail("Select a valid fill rule.");
            return;
        }
        if (!TryParseMatrix(transformTextBox.Text, out Matrix3x2 transform))
        {
            Fail("Transform must contain six finite invariant values: m11,m12,m21,m22,m31,m32.");
            return;
        }
        geometry.FillRule = fillRule;
        geometry.Transform = transform;
        DialogResult = DialogResult.OK;
    }

    private void RefreshFigures(int selectedIndex)
    {
        changingSelection = true;
        figureList.Items.Clear();
        for (int index = 0; index < geometry.Figures.Count; index++)
        {
            PathFigure figure = geometry.Figures[index];
            figureList.Items.Add($"Figure {index + 1}: {FormatPoint(figure.StartPoint)}, {figure.Segments.Count} segments{(figure.IsClosed ? ", closed" : string.Empty)}");
        }
        figureList.SelectedIndex = geometry.Figures.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, geometry.Figures.Count - 1);
        changingSelection = false;
        LoadFigure();
        RefreshSegments(0);
    }

    private void RefreshSegments(int selectedIndex)
    {
        changingSelection = true;
        segmentList.Items.Clear();
        PathFigure? figure = SelectedFigure();
        if (figure is not null)
        {
            for (int index = 0; index < figure.Segments.Count; index++)
                segmentList.Items.Add($"{index + 1}: {Describe(figure.Segments[index])}");
            segmentList.SelectedIndex = figure.Segments.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, figure.Segments.Count - 1);
        }
        changingSelection = false;
        LoadSegment();
    }

    private void LoadFigure()
    {
        PathFigure? figure = SelectedFigure();
        bool enabled = figure is not null;
        startXTextBox.Enabled = enabled;
        startYTextBox.Enabled = enabled;
        isClosedCheckBox.Enabled = enabled;
        if (figure is null)
        {
            startXTextBox.Text = startYTextBox.Text = string.Empty;
            isClosedCheckBox.Checked = false;
            return;
        }
        startXTextBox.Text = FormatFloat(figure.StartPoint.X);
        startYTextBox.Text = FormatFloat(figure.StartPoint.Y);
        isClosedCheckBox.Checked = figure.IsClosed;
    }

    private void LoadSegment()
    {
        PathSegment? segment = SelectedSegment();
        SetPointEditors(control1XTextBox, control1YTextBox, null, false);
        SetPointEditors(control2XTextBox, control2YTextBox, null, false);
        SetPointEditors(endXTextBox, endYTextBox, null, segment is not null);
        switch (segment)
        {
            case LineSegment line:
                SetPointEditors(endXTextBox, endYTextBox, line.Point, true);
                break;
            case QuadraticBezierSegment quadratic:
                SetPointEditors(control1XTextBox, control1YTextBox, quadratic.ControlPoint, true);
                SetPointEditors(endXTextBox, endYTextBox, quadratic.Point, true);
                break;
            case BezierSegment cubic:
                SetPointEditors(control1XTextBox, control1YTextBox, cubic.ControlPoint1, true);
                SetPointEditors(control2XTextBox, control2YTextBox, cubic.ControlPoint2, true);
                SetPointEditors(endXTextBox, endYTextBox, cubic.Point, true);
                break;
        }
    }

    private PathFigure? SelectedFigure()
        => figureList.SelectedIndex >= 0 && figureList.SelectedIndex < geometry.Figures.Count
            ? geometry.Figures[figureList.SelectedIndex]
            : null;

    private PathSegment? SelectedSegment()
    {
        PathFigure? figure = SelectedFigure();
        return figure is not null && segmentList.SelectedIndex >= 0 && segmentList.SelectedIndex < figure.Segments.Count
            ? figure.Segments[segmentList.SelectedIndex]
            : null;
    }

    private bool TryPoint(TextBox xTextBox, TextBox yTextBox, out PointF point)
    {
        bool validX = TryFloat(xTextBox.Text, out float x);
        bool validY = TryFloat(yTextBox.Text, out float y);
        bool valid = validX && validY;
        point = valid ? new PointF(x, y) : default;
        return valid;
    }

    private bool Fail(string message)
    {
        errorLabel.Text = message;
        return false;
    }

    private static PathGeometry Clone(PathGeometry source)
        => (PathGeometry)DesignerPropertyValueEditor.FromDesignPropertyValue(
            DesignerPropertyValueEditor.ToDesignPropertyValue(source, typeof(Geometry)),
            typeof(Geometry))!;

    private static string Describe(PathSegment segment)
        => segment switch
        {
            LineSegment line => $"Line to {FormatPoint(line.Point)}",
            QuadraticBezierSegment quadratic => $"Quadratic via {FormatPoint(quadratic.ControlPoint)} to {FormatPoint(quadratic.Point)}",
            BezierSegment cubic => $"Cubic via {FormatPoint(cubic.ControlPoint1)}, {FormatPoint(cubic.ControlPoint2)} to {FormatPoint(cubic.Point)}",
            _ => segment.GetType().Name
        };

    private static void SetPointEditors(TextBox x, TextBox y, PointF? point, bool enabled)
    {
        x.Enabled = y.Enabled = enabled;
        x.Text = point is { } value ? FormatFloat(value.X) : string.Empty;
        y.Text = point is { } value2 ? FormatFloat(value2.Y) : string.Empty;
    }

    private static bool TryParseMatrix(string text, out Matrix3x2 matrix)
    {
        string[] parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = new float[6];
        if (parts.Length != values.Length)
        {
            matrix = default;
            return false;
        }
        for (int index = 0; index < values.Length; index++)
        {
            if (!TryFloat(parts[index], out values[index]))
            {
                matrix = default;
                return false;
            }
        }
        matrix = new Matrix3x2(values[0], values[1], values[2], values[3], values[4], values[5]);
        return true;
    }

    private static bool TryFloat(string text, out float value)
        => float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    private static string FormatFloat(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string FormatPoint(PointF point) => $"{FormatFloat(point.X)},{FormatFloat(point.Y)}";

    private static string FormatMatrix(Matrix3x2 matrix)
        => string.Join(',', new[] { matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32 }.Select(FormatFloat));
}
