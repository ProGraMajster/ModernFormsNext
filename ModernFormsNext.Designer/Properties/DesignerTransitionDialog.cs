using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerTransitionDialog : Form
{
    private readonly DesignerSession session;
    private readonly IDictionary<string, DesignPropertyValue> properties;
    private readonly string propertyName;
    private readonly bool isLayout;
    private readonly Label errorLabel;
    private DesignerLayoutTransitionEditorModel? layoutModel;
    private DesignerVisualStateTransitionEditorModel? visualModel;
    private CheckBox? layoutEnabled;
    private NumericUpDown? layoutDuration;
    private ComboBox? layoutEasing;
    private DataGridView? transitionGrid;
    private ComboBox? fromState;
    private ComboBox? toState;
    private NumericUpDown? visualDuration;
    private ComboBox? visualEasing;
    private Button? addTransitionButton;
    private Button? removeButton;
    private Button? okButton;
    private int loadedTransitionIndex = -1;
    private bool loadingControls;

    public DesignerTransitionDialog(
        DesignerSession session,
        IDictionary<string, DesignPropertyValue> properties,
        bool isLayout)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
        this.isLayout = isLayout;
        propertyName = isLayout
            ? LayoutTransitionDesignValue.PropertyName
            : VisualStateTransitionDesignValue.PropertyName;
        properties.TryGetValue(propertyName, out DesignPropertyValue? stored);

        Text = isLayout ? "Layout Transition Editor" : "Visual State Transitions Editor";
        Name = "DesignerTransitionDialog";
        StartPosition = FormStartPosition.CenterParent;

        if (isLayout)
        {
            Size = new System.Drawing.Size(470, 300);
            MinimumSize = new System.Drawing.Size(430, 280);
            errorLabel = Controls.Add(new Label
            {
                Left = 18,
                Top = 166,
                Width = 418,
                Height = 38,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            });
            BuildLayoutEditor(stored);
        }
        else
        {
            Size = new System.Drawing.Size(780, 580);
            MinimumSize = new System.Drawing.Size(780, 520);
            errorLabel = Controls.Add(new Label
            {
                Left = 18,
                Top = 476,
                Width = 728,
                Height = 34,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            });
            BuildVisualStateEditor(stored);
        }
    }

    private void BuildLayoutEditor(DesignPropertyValue? stored)
    {
        layoutModel = new DesignerLayoutTransitionEditorModel(stored);
        Controls.Add(new Label { Left = 18, Top = 24, Width = 120, Height = 24, Text = "Enabled" });
        layoutEnabled = Controls.Add(new CheckBox
        {
            Left = 154,
            Top = 20,
            Width = 220,
            Height = 28,
            Text = "Animate layout changes"
        });
        Controls.Add(new Label { Left = 18, Top = 64, Width = 120, Height = 24, Text = "Duration" });
        layoutDuration = Controls.Add(CreateDurationEditor(154, 60, 180));
        layoutDuration.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Controls.Add(new Label { Left = 342, Top = 64, Width = 32, Height = 24, Text = "ms" });
        Controls.Add(new Label { Left = 18, Top = 104, Width = 120, Height = 24, Text = "Easing" });
        layoutEasing = Controls.Add(CreateEasingEditor(154, 100, 220));
        layoutEasing.Anchor = AnchorStyles.Top | AnchorStyles.Left;

        var resetButton = Controls.Add(new Button
        {
            Left = 162,
            Top = 218,
            Width = 84,
            Height = 30,
            Text = "Reset",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });
        okButton = Controls.Add(new Button
        {
            Left = 258,
            Top = 218,
            Width = 84,
            Height = 30,
            Text = "OK",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });
        var cancelButton = Controls.Add(new Button
        {
            Left = 354,
            Top = 218,
            Width = 84,
            Height = 30,
            Text = "Cancel",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });

        layoutEnabled.CheckedChanged += (_, _) => UpdateLayoutModel();
        layoutDuration.ValueChanged += (_, _) => UpdateLayoutModel();
        layoutEasing.SelectedIndexChanged += (_, _) => UpdateLayoutModel();
        resetButton.Click += (_, _) => ResetLayout();
        okButton.Click += (_, _) => CommitLayout();
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        LoadLayoutControls();
        if (layoutModel.LoadError is not null)
            DisableInvalidLayout(layoutModel.LoadError);
    }

    private void BuildVisualStateEditor(DesignPropertyValue? stored)
    {
        visualModel = new DesignerVisualStateTransitionEditorModel(stored);
        Controls.Add(new Label
        {
            Left = 18,
            Top = 16,
            Width = 300,
            Height = 22,
            Text = "Directional visual-state transitions"
        });
        transitionGrid = Controls.Add(new DataGridView
        {
            Left = 18,
            Top = 42,
            Width = 728,
            Height = 280,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        });
        transitionGrid.Columns.Add("From", 150);
        transitionGrid.Columns.Add("To", 150);
        transitionGrid.Columns.Add("Duration", 170);
        transitionGrid.Columns.Add("Easing", 210);

        addTransitionButton = Controls.Add(new Button
        {
            Left = 18,
            Top = 332,
            Width = 84,
            Height = 30,
            Text = "Add",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });
        removeButton = Controls.Add(new Button
        {
            Left = 114,
            Top = 332,
            Width = 84,
            Height = 30,
            Text = "Remove",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        });

        Controls.Add(new Label { Left = 18, Top = 382, Width = 64, Height = 24, Text = "From", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        fromState = Controls.Add(CreateStateEditor(82, 378));
        Controls.Add(new Label { Left = 274, Top = 382, Width = 38, Height = 24, Text = "To", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        toState = Controls.Add(CreateStateEditor(316, 378));
        Controls.Add(new Label { Left = 508, Top = 382, Width = 68, Height = 24, Text = "Duration", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        visualDuration = Controls.Add(CreateDurationEditor(580, 378, 126));
        Controls.Add(new Label { Left = 712, Top = 382, Width = 28, Height = 24, Text = "ms", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        Controls.Add(new Label { Left = 18, Top = 426, Width = 64, Height = 24, Text = "Easing", Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
        visualEasing = Controls.Add(CreateEasingEditor(82, 422, 188));

        var resetButton = Controls.Add(new Button
        {
            Left = 470,
            Top = 518,
            Width = 84,
            Height = 30,
            Text = "Reset",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });
        okButton = Controls.Add(new Button
        {
            Left = 566,
            Top = 518,
            Width = 84,
            Height = 30,
            Text = "OK",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });
        var cancelButton = Controls.Add(new Button
        {
            Left = 662,
            Top = 518,
            Width = 84,
            Height = 30,
            Text = "Cancel",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        });

        transitionGrid.SelectionChanged += (_, _) => ChangeTransitionSelection();
        addTransitionButton.Click += (_, _) => AddTransition();
        removeButton.Click += (_, _) => RemoveTransition();
        resetButton.Click += (_, _) => ResetVisualStates();
        okButton.Click += (_, _) => CommitVisualStates();
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        RefreshTransitionGrid(visualModel.Entries.Count > 0 ? 0 : -1);
        if (visualModel.LoadError is not null)
            DisableInvalidVisualStates(visualModel.LoadError);
    }

    private static NumericUpDown CreateDurationEditor(int left, int top, int width)
        => new()
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 30,
            Minimum = 0m,
            Maximum = decimal.MaxValue,
            AllowDecimalValues = true,
            DecimalPlaces = 2,
            Increment = 1m,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

    private static ComboBox CreateEasingEditor(int left, int top, int width)
    {
        var combo = new ComboBox
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        combo.Items.AddRange(KnownEasingDesignValue.Identifiers.Cast<object>().ToArray());
        return combo;
    }

    private static ComboBox CreateStateEditor(int left, int top)
    {
        var combo = new ComboBox
        {
            Left = left,
            Top = top,
            Width = 170,
            Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        combo.Items.AddRange(DesignerVisualStateTransitionEditorModel.States.Cast<object>().ToArray());
        return combo;
    }

    private void UpdateLayoutModel()
    {
        if (loadingControls || layoutModel is null || layoutEnabled is null || layoutDuration is null || layoutEasing is null)
            return;

        string easing = SelectedText(layoutEasing);
        if (!layoutModel.TrySet(layoutEnabled.Checked, (double)layoutDuration.Value, easing, out string? error))
            ShowError(error ?? "The layout transition is invalid.");
        else
            errorLabel.Text = string.Empty;
    }

    private void LoadLayoutControls()
    {
        loadingControls = true;
        layoutEnabled!.Checked = layoutModel!.Enabled;
        layoutDuration!.Value = ClampToDecimal(layoutModel.DurationMilliseconds);
        SelectText(layoutEasing!, layoutModel.Easing);
        loadingControls = false;
    }

    private void ResetLayout()
    {
        layoutModel!.Reset();
        LoadLayoutControls();
        SetLayoutControlsEnabled(true);
        errorLabel.Text = "Default restored: no explicit LayoutTransition will be stored.";
    }

    private void CommitLayout()
    {
        if (!layoutModel!.TryCreateValue(out DesignPropertyValue? value, out string? error))
        {
            ShowError(error ?? "The layout transition is invalid.");
            return;
        }

        if (value is null)
            properties.Remove(propertyName);
        else
            properties[propertyName] = value;
        DialogResult = DialogResult.OK;
    }

    private void AddTransition()
    {
        if (!TryCommitLoadedTransition())
            return;
        if (!visualModel!.TryAddDefault(out int index, out string? error))
        {
            ShowError(error ?? "A transition could not be added.");
            return;
        }
        RefreshTransitionGrid(index);
    }

    private void RemoveTransition()
    {
        int index = transitionGrid!.SelectedRowIndex;
        if (!visualModel!.RemoveAt(index))
            return;
        loadedTransitionIndex = -1;
        RefreshTransitionGrid(Math.Min(index, visualModel.Entries.Count - 1));
    }

    private void ChangeTransitionSelection()
    {
        if (loadingControls)
            return;

        int selectedIndex = transitionGrid!.SelectedRowIndex;
        if (selectedIndex == loadedTransitionIndex)
            return;
        if (!TryCommitLoadedTransition())
        {
            loadingControls = true;
            transitionGrid.SelectedRowIndex = loadedTransitionIndex;
            loadingControls = false;
            return;
        }

        loadedTransitionIndex = selectedIndex;
        LoadSelectedTransition();
    }

    private bool TryCommitLoadedTransition()
    {
        if (loadedTransitionIndex < 0 || loadedTransitionIndex >= visualModel!.Entries.Count)
            return true;

        if (!visualModel.TryUpdate(
            loadedTransitionIndex,
            SelectedText(fromState!),
            SelectedText(toState!),
            (double)visualDuration!.Value,
            SelectedText(visualEasing!),
            out string? error))
        {
            ShowError(error ?? "The selected transition is invalid.");
            return false;
        }

        UpdateTransitionGridRow(loadedTransitionIndex);
        errorLabel.Text = string.Empty;
        return true;
    }

    private void LoadSelectedTransition()
    {
        loadingControls = true;
        DesignerVisualStateTransitionEditorModel model = visualModel!;
        bool selected = loadedTransitionIndex >= 0 && loadedTransitionIndex < model.Entries.Count;
        if (selected)
        {
            DesignVisualStateTransition entry = model.Entries[loadedTransitionIndex];
            SelectText(fromState!, entry.From);
            SelectText(toState!, entry.To);
            visualDuration!.Value = ClampToDecimal(entry.DurationMilliseconds);
            SelectText(visualEasing!, entry.Easing);
        }
        else
        {
            fromState!.SelectedIndex = -1;
            toState!.SelectedIndex = -1;
            visualDuration!.Value = 0m;
            visualEasing!.SelectedIndex = -1;
        }
        SetVisualEditorsEnabled(selected);
        loadingControls = false;
    }

    private void RefreshTransitionGrid(int selectIndex)
    {
        loadingControls = true;
        transitionGrid!.Rows.Clear();
        foreach (DesignVisualStateTransition entry in visualModel!.Entries)
        {
            transitionGrid.Rows.Add(
                entry.From,
                entry.To,
                $"{entry.DurationMilliseconds:0.##} ms",
                entry.Easing);
        }
        transitionGrid.SelectedRowIndex = visualModel.Entries.Count == 0
            ? -1
            : Math.Clamp(selectIndex, 0, visualModel.Entries.Count - 1);
        loadedTransitionIndex = transitionGrid.SelectedRowIndex;
        loadingControls = false;
        LoadSelectedTransition();
    }

    private void UpdateTransitionGridRow(int index)
    {
        DesignVisualStateTransition entry = visualModel!.Entries[index];
        DataGridViewRow row = transitionGrid!.Rows[index];
        row.Cells[0].Value = entry.From;
        row.Cells[1].Value = entry.To;
        row.Cells[2].Value = $"{entry.DurationMilliseconds:0.##} ms";
        row.Cells[3].Value = entry.Easing;
        transitionGrid.Invalidate();
    }

    private void ResetVisualStates()
    {
        visualModel!.Reset();
        SetVisualCollectionEnabled(true);
        RefreshTransitionGrid(-1);
        errorLabel.Text = "Default restored: no visual-state transitions will be stored.";
    }

    private void CommitVisualStates()
    {
        if (!TryCommitLoadedTransition())
            return;
        if (!visualModel!.TryCreateValue(out DesignPropertyValue value, out string? error))
        {
            ShowError(error ?? "The visual-state transition collection is invalid.");
            return;
        }

        if (visualModel.Entries.Count == 0)
            properties.Remove(propertyName);
        else
            properties[propertyName] = value;
        DialogResult = DialogResult.OK;
    }

    private void DisableInvalidLayout(string error)
    {
        SetLayoutControlsEnabled(false);
        okButton!.Enabled = false;
        ShowError(error + " Use Reset to restore the default.");
    }

    private void DisableInvalidVisualStates(string error)
    {
        SetVisualCollectionEnabled(false);
        okButton!.Enabled = false;
        ShowError(error + " Use Reset to remove the invalid optional value.");
    }

    private void SetLayoutControlsEnabled(bool enabled)
    {
        layoutEnabled!.Enabled = enabled;
        layoutDuration!.Enabled = enabled;
        layoutEasing!.Enabled = enabled;
        okButton!.Enabled = enabled;
    }

    private void SetVisualCollectionEnabled(bool enabled)
    {
        transitionGrid!.Enabled = enabled;
        addTransitionButton!.Enabled = enabled;
        removeButton!.Enabled = enabled && transitionGrid.SelectedRowIndex >= 0;
        okButton!.Enabled = enabled;
        SetVisualEditorsEnabled(enabled && transitionGrid.SelectedRowIndex >= 0);
    }

    private void SetVisualEditorsEnabled(bool enabled)
    {
        fromState!.Enabled = enabled;
        toState!.Enabled = enabled;
        visualDuration!.Enabled = enabled;
        visualEasing!.Enabled = enabled;
        removeButton!.Enabled = enabled;
    }

    private void ShowError(string message)
    {
        errorLabel.Text = message;
        session.Log($"Transition editor: {message}");
    }

    private static string SelectedText(ComboBox comboBox)
        => comboBox.SelectedItem?.ToString()
            ?? (comboBox.SelectedIndex >= 0 ? comboBox.Items[comboBox.SelectedIndex]?.ToString() : null)
            ?? string.Empty;

    private static void SelectText(ComboBox comboBox, string value)
    {
        comboBox.SelectedIndex = comboBox.Items
            .Select((item, index) => new { Text = item?.ToString(), Index = index })
            .FirstOrDefault(item => string.Equals(item.Text, value, StringComparison.Ordinal))?.Index ?? -1;
    }

    private static decimal ClampToDecimal(double value)
    {
        if (value <= (double)decimal.MinValue)
            return decimal.MinValue;
        if (value >= (double)decimal.MaxValue)
            return decimal.MaxValue;
        return (decimal)value;
    }
}
