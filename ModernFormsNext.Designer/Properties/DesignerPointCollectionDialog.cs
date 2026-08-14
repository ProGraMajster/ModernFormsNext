using System.Drawing;
using System.Globalization;
using ModernFormsNext.Drawing;

namespace ModernFormsNext.Designer.Properties;

/// <summary>Provides ordered, structured editing for Polygon and Polyline point collections.</summary>
internal sealed class DesignerPointCollectionDialog : Form
{
    private readonly List<PointF> points;
    private readonly ListBox pointList;
    private readonly TextBox xTextBox;
    private readonly TextBox yTextBox;
    private readonly Button removeButton;
    private readonly Button upButton;
    private readonly Button downButton;
    private readonly Button applyPointButton;
    private readonly Label errorLabel;
    private bool changingSelection;

    public DesignerPointCollectionDialog(PointCollection? source)
    {
        points = source is null ? [] : [.. source];

        Text = "Point Collection Editor";
        Name = nameof(DesignerPointCollectionDialog);
        Size = new Size(610, 430);
        MinimumSize = new Size(560, 390);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label { Left = 18, Top = 16, Width = 250, Height = 22, Text = "Points (rendering order)" });
        pointList = Controls.Add(new ListBox
        {
            Left = 18,
            Top = 42,
            Width = 270,
            Height = 280,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        });

        var addButton = Controls.Add(new Button { Left = 18, Top = 332, Width = 58, Height = 30, Text = "Add", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        removeButton = Controls.Add(new Button { Left = 84, Top = 332, Width = 72, Height = 30, Text = "Remove", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        upButton = Controls.Add(new Button { Left = 164, Top = 332, Width = 56, Height = 30, Text = "Up", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        downButton = Controls.Add(new Button { Left = 228, Top = 332, Width = 60, Height = 30, Text = "Down", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });

        Controls.Add(new Label { Left = 316, Top = 16, Width = 230, Height = 22, Text = "Selected point" });
        Controls.Add(new Label { Left = 316, Top = 54, Width = 36, Height = 24, Text = "X" });
        xTextBox = Controls.Add(new TextBox { Left = 360, Top = 50, Width = 190, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
        Controls.Add(new Label { Left = 316, Top = 92, Width = 36, Height = 24, Text = "Y" });
        yTextBox = Controls.Add(new TextBox { Left = 360, Top = 88, Width = 190, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
        applyPointButton = Controls.Add(new Button { Left = 430, Top = 128, Width = 120, Height = 30, Text = "Apply Point", Anchor = AnchorStyles.Top | AnchorStyles.Right });
        errorLabel = Controls.Add(new Label { Left = 316, Top = 170, Width = 234, Height = 72, Text = string.Empty, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });

        var okButton = Controls.Add(new Button { Left = 398, Top = 332, Width = 72, Height = 30, Text = "OK", Anchor = AnchorStyles.Bottom | AnchorStyles.Right });
        var cancelButton = Controls.Add(new Button { Left = 478, Top = 332, Width = 72, Height = 30, Text = "Cancel", Anchor = AnchorStyles.Bottom | AnchorStyles.Right });

        pointList.SelectedIndexChanged += (_, _) => LoadSelectedPoint();
        addButton.Click += (_, _) => AddPoint();
        removeButton.Click += (_, _) => RemovePoint();
        upButton.Click += (_, _) => MovePoint(-1);
        downButton.Click += (_, _) => MovePoint(1);
        applyPointButton.Click += (_, _) => ApplySelectedPoint();
        okButton.Click += (_, _) => Commit();
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        RefreshList(points.Count > 0 ? 0 : -1);
    }

    public PointCollection Points => new(points);

    private void AddPoint()
    {
        points.Add(PointF.Empty);
        RefreshList(points.Count - 1);
    }

    private void RemovePoint()
    {
        int index = pointList.SelectedIndex;
        if (index < 0 || index >= points.Count)
            return;

        points.RemoveAt(index);
        RefreshList(Math.Min(index, points.Count - 1));
    }

    private void MovePoint(int delta)
    {
        int oldIndex = pointList.SelectedIndex;
        int newIndex = oldIndex + delta;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= points.Count)
            return;

        PointF point = points[oldIndex];
        points.RemoveAt(oldIndex);
        points.Insert(newIndex, point);
        RefreshList(newIndex);
    }

    private bool ApplySelectedPoint()
    {
        int index = pointList.SelectedIndex;
        if (index < 0 || index >= points.Count)
            return true;

        if (!TryReadFiniteFloat(xTextBox.Text, out float x) || !TryReadFiniteFloat(yTextBox.Text, out float y))
        {
            errorLabel.Text = "X and Y must be finite invariant numbers, for example 12.5.";
            return false;
        }

        points[index] = new PointF(x, y);
        errorLabel.Text = string.Empty;
        RefreshList(index);
        return true;
    }

    private void Commit()
    {
        if (ApplySelectedPoint())
            DialogResult = DialogResult.OK;
    }

    private void LoadSelectedPoint()
    {
        if (changingSelection)
            return;

        int index = pointList.SelectedIndex;
        bool selected = index >= 0 && index < points.Count;
        if (selected)
        {
            PointF point = points[index];
            xTextBox.Text = point.X.ToString("R", CultureInfo.InvariantCulture);
            yTextBox.Text = point.Y.ToString("R", CultureInfo.InvariantCulture);
        }
        else
        {
            xTextBox.Text = string.Empty;
            yTextBox.Text = string.Empty;
        }

        xTextBox.Enabled = selected;
        yTextBox.Enabled = selected;
        applyPointButton.Enabled = selected;
        removeButton.Enabled = selected;
        upButton.Enabled = selected && index > 0;
        downButton.Enabled = selected && index < points.Count - 1;
    }

    private void RefreshList(int selectedIndex)
    {
        changingSelection = true;
        pointList.Items.Clear();
        for (int index = 0; index < points.Count; index++)
        {
            PointF point = points[index];
            pointList.Items.Add($"{index + 1} | {point.X.ToString("R", CultureInfo.InvariantCulture)} | {point.Y.ToString("R", CultureInfo.InvariantCulture)}");
        }
        pointList.SelectedIndex = points.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, points.Count - 1);
        changingSelection = false;
        LoadSelectedPoint();
    }

    private static bool TryReadFiniteFloat(string text, out float value)
        => float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value);
}
